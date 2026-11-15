using System;
using Unity.Mathematics;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Bots
{
    // What the pilot is doing right now; exposed for the arena report and debugging overlays.
    // frob:doc docs/reference/hullbreach-bots.md#botpilot
    public enum BotMode
    {
        Idle = 0,
        Engage = 1,
        Strafe = 2,
        Dodge = 3,
        Retreat = 4,
        AvoidPlanet = 5,
        ReturnToRing = 6,
    }

    // The layered brain: survive (planets, arena edge, storm ring), then fight (approach, hold range,
    // lead and fire, strafe, dodge, retreat). Decisions run at the profile's decision rate on a delayed view
    // of the enemy; steering runs every tick. Plain C#, deterministic for a given BotRng.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    // frob:doc docs/reference/hullbreach-bots.md#botpilot
    public sealed class BotPilot : IShipInputSource
    {
        const float TwoPi = 6.2831855f;
        const float Pi = 3.14159265f;

        readonly BotProfile _profile;
        readonly BotRng _rng;
        readonly GravityField _gravity = new GravityField();
        int _gravityBodyCount = -1;

        // Ring of recent enemy views, so decisions can run on a reaction-delayed picture.
        readonly EnemyView[] _delay;
        int _delayHead;
        int _delayFilled;

        uint _nextDecisionTick;
        uint _nextFireTick;
        uint _nextRollTick;
        bool _rollPassed;
        uint _modeUntilTick;
        uint _nextStrafeTick;
        float _strafeSign = 1f;
        bool _retreating;

        // Standing orders between decisions.
        BotMode _mode = BotMode.Idle;
        float2 _headDir = new float2(0f, 1f);
        float2 _headAxisLocal = new float2(0f, 1f);
        float2 _accelDir;
        bool _accelWanted;
        float _alignCos = 0.85f;
        float2 _aimDir = new float2(0f, 1f);
        float _aimRange;
        float _aimFlight;
        bool _aimValid;
        EnemyView _lastEnemy;

        // frob:doc docs/reference/hullbreach-bots.md#botpilot
        public BotMode Mode => _mode;

        // frob:doc docs/reference/hullbreach-bots.md#botpilot
        public BotProfile Profile => _profile;

        // Shots the pilot has asked for (the server may still hold one back for its own cooldown).
        // frob:doc docs/reference/hullbreach-bots.md#botpilot
        public int FireRequests { get; private set; }

        // frob:doc docs/reference/hullbreach-bots.md#botpilot
        public BotPilot(BotProfile profile, BotRng rng)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _delay = new EnemyView[Math.Max(1, profile.ReactionTicks + 1)];
            _nextStrafeTick = (uint)(_rng.Range(2f, 6f) * 50f);
        }

        // frob:doc docs/reference/hullbreach-bots.md#botpilot
        public ShipInput Next(BotObservation obs)
        {
            if (obs == null) throw new ArgumentNullException(nameof(obs));
            SyncGravity(obs);
            PushDelay(obs.Enemy);
            if (!obs.Self.Alive) return new ShipInput(0f, 0f, false);

            if (obs.Tick >= _nextDecisionTick)
            {
                Decide(obs, DelayedEnemy());
                _nextDecisionTick = obs.Tick + (uint)Math.Max(1, _profile.DecisionTicks);
            }
            return Control(obs);
        }

        // ---- delayed perception -------------------------------------------------------------

        void PushDelay(EnemyView view)
        {
            _delay[_delayHead] = view;
            _delayHead = (_delayHead + 1) % _delay.Length;
            if (_delayFilled < _delay.Length) _delayFilled++;
        }

        EnemyView DelayedEnemy()
        {
            // Once full, the slot the head points at is the oldest: exactly ReactionTicks behind the newest.
            if (_delayFilled < _delay.Length) return _delay[0];
            return _delay[_delayHead];
        }

        void SyncGravity(BotObservation obs)
        {
            if (_gravityBodyCount == obs.Planets.Count) return;
            _gravity.Clear();
            foreach (GravityBody body in obs.Planets) _gravity.Add(body);
            _gravityBodyCount = obs.Planets.Count;
        }

        // ---- decisions ----------------------------------------------------------------------

        void Decide(BotObservation obs, EnemyView enemy)
        {
            SelfView self = obs.Self;
            _lastEnemy = enemy;
            _headAxisLocal = new float2(0f, 1f);
            _alignCos = 0.85f;
            _accelWanted = false;
            _aimValid = false;

            // Layer 1: survive. Planets first, then the arena edge, then the storm ring.
            if (PlanetEscape(obs, out float2 escape, out bool urgent))
            {
                _mode = BotMode.AvoidPlanet;
                DriveToward(self, escape, urgent);
                if (enemy.Visible) SolveAim(obs, enemy);
                return;
            }
            if (RingReturn(obs, out float2 back))
            {
                _mode = BotMode.ReturnToRing;
                DriveToward(self, back, false);
                if (enemy.Visible) SolveAim(obs, enemy);
                return;
            }

            if (!enemy.Visible)
            {
                _mode = BotMode.Idle;
                return;
            }

            // Layer 2: the fight.
            float2 toEnemy = enemy.Position - self.Position;
            float range = math.max(0.01f, math.length(toEnemy));
            float2 u = toEnemy / range;
            float2 vRel = enemy.Velocity - self.Velocity;
            float closing = -math.dot(vRel, u);
            SolveAim(obs, enemy);

            // Hysteresis on retreat: leave at the threshold, return once the fight has turned or range is safe.
            bool hurt = self.CoreDamage >= _profile.RetreatCoreDamage && self.CoreDamage > enemy.CoreDamage + 0.05f;
            if (hurt) _retreating = true;
            else if (_retreating && (self.CoreDamage < _profile.RetreatCoreDamage * 0.6f || range > _profile.EngageRange * 2.2f)) _retreating = false;

            if (_retreating)
            {
                _mode = BotMode.Retreat;
                _headDir = -u;
                _accelDir = -u;
                _accelWanted = true;
                return;
            }

            // A burn already under way finishes before anything new is decided.
            if (obs.Tick < _modeUntilTick && (_mode == BotMode.Dodge || _mode == BotMode.Strafe))
            {
                ApplyLateral(u, _mode);
                return;
            }
            if (_profile.DodgeSkill > 0f && TryFindThreat(obs, out float2 dodgeAxis) && _rng.Chance(_profile.DodgeSkill))
            {
                _mode = BotMode.Dodge;
                _modeUntilTick = obs.Tick + 40;
                _strafeSign = math.dot(dodgeAxis, new float2(-u.y, u.x)) <= 0f ? 1f : -1f;
                ApplyLateral(u, BotMode.Dodge);
                return;
            }
            if (_profile.StrafePeriod > 0f && obs.Tick >= _nextStrafeTick && range < _profile.EngageRange * 1.8f && range > 7f)
            {
                _mode = BotMode.Strafe;
                _modeUntilTick = obs.Tick + 45;
                _strafeSign = _rng.Chance(0.5f) ? 1f : -1f;
                _nextStrafeTick = obs.Tick + (uint)(_profile.StrafePeriod * _rng.Range(0.7f, 1.4f) * 50f);
                ApplyLateral(u, BotMode.Strafe);
                return;
            }

            // Hold range: a closing-speed controller toward the preferred distance.
            _mode = BotMode.Engage;
            _headDir = _aimValid ? _aimDir : u;
            _headAxisLocal = self.HasCannon ? self.AimLocal : new float2(0f, 1f);
            float slack = _profile.EngageRange * _profile.RangeSlack;
            float desiredClosing = math.clamp(0.3f * (range - _profile.EngageRange), -5f, 8f);
            if (range < 9f) desiredClosing = math.min(desiredClosing, -1f);
            if (math.abs(range - _profile.EngageRange) < slack * 0.5f) desiredClosing = 0f;
            float error = desiredClosing - closing;
            _alignCos = 0.8f;
            if (error > 0.8f) { _accelDir = u; _accelWanted = true; }
            else if (error < -0.8f) { _accelDir = -u; _accelWanted = true; }
        }

        void ApplyLateral(float2 u, BotMode mode)
        {
            float2 side = new float2(-u.y, u.x) * _strafeSign;
            _headDir = side;
            _headAxisLocal = new float2(0f, 1f);
            _accelDir = side;
            _accelWanted = true;
            _alignCos = 0.8f;
            _mode = mode;
        }

        // Points the nose at a wanted acceleration; uses the retros when the nose is far off and time matters.
        void DriveToward(in SelfView self, float2 dir, bool urgent)
        {
            float2 nose = Rotate(new float2(0f, 1f), self.Rotation);
            float c = math.dot(nose, dir);
            _accelDir = dir;
            _accelWanted = true;
            _alignCos = urgent ? 0.7f : 0.85f;
            _headAxisLocal = new float2(0f, 1f);
            if (urgent && c < -0.3f && self.MaxRetroAccel > 0f) _headDir = -dir;
            else _headDir = dir;
        }

        // Predicts a coasting trajectory under the visible gravity; asks for outward thrust if it grazes a planet.
        bool PlanetEscape(BotObservation obs, out float2 escape, out bool urgent)
        {
            escape = float2.zero;
            urgent = false;
            SelfView self = obs.Self;
            float horizon = _profile.PlanetLookahead;
            const float h = 0.1f;
            float2 pos = self.Position, vel = self.Velocity;
            float margin = 8f;
            for (float t = 0f; t < horizon; t += h)
            {
                vel += _gravity.AccelerationAt(pos) * h;
                pos += vel * h;
                for (int i = 0; i < obs.Planets.Count; i++)
                {
                    GravityBody p = obs.Planets[i];
                    float clearance = math.length(pos - p.Position) - (p.Radius + margin);
                    if (clearance >= 0f) continue;
                    float2 outward = math.normalizesafe(self.Position - p.Position, new float2(0f, 1f));
                    // Bias the burn so it also cancels the inbound speed instead of only pushing outward.
                    float inboundSpeed = math.dot(self.Velocity, outward);
                    float2 inbound = inboundSpeed < 0f ? -inboundSpeed * outward : float2.zero;
                    // A prograde component lifts the orbit; pure radial thrust only hovers against a deep well.
                    float2 tangent = math.normalizesafe(self.Velocity - math.dot(self.Velocity, outward) * outward);
                    escape = math.normalizesafe(outward + 0.15f * inbound + 0.7f * tangent, outward);
                    urgent = t < horizon * 0.6f;
                    return true;
                }
            }
            return false;
        }

        // The arena edge and, when there is one, the shrinking storm ring.
        bool RingReturn(BotObservation obs, out float2 back)
        {
            back = float2.zero;
            SelfView self = obs.Self;
            if (obs.Bounds.Radius > 0f && obs.Bounds.Depth(self.Position) > 0.35f)
            {
                back = math.normalizesafe(obs.Bounds.Center - self.Position);
                return true;
            }
            if (!obs.HasStorm) return false;
            StormState storm = obs.Storm;
            if (storm.IsSuddenDeath || storm.Radius <= 0f) return false;
            float margin = 9f + 0.6f * math.length(self.Velocity);
            float signed = storm.SignedDistance(self.Position);
            bool preposition = storm.Stage == StormStage.Hold && storm.TicksUntilShrink < 450;
            // Pre-position for the next ring a little before the shrink starts.
            if (preposition) signed = math.min(signed, storm.NextRadius - math.length(self.Position - storm.NextCenter));
            if (signed >= margin) return false;
            float2 pull = storm.PullDirection(self.Position);
            if (preposition) pull = math.normalizesafe(storm.NextCenter - self.Position, pull);
            back = pull;
            return true;
        }

        // Closest approach analysis for every hostile shot; true when one will pass within a ship's width.
        bool TryFindThreat(BotObservation obs, out float2 axis)
        {
            axis = float2.zero;
            SelfView self = obs.Self;
            float best = float.MaxValue;
            for (int i = 0; i < obs.Projectiles.Count; i++)
            {
                ProjectileView pr = obs.Projectiles[i];
                if (!pr.Hostile) continue;
                float2 r = pr.Position - self.Position;
                float2 w = pr.Velocity - self.Velocity;
                float ww = math.dot(w, w);
                if (ww < 1e-4f) continue;
                float t = -math.dot(r, w) / ww;
                if (t < 0.15f || t > 1.4f) continue;
                float2 miss = r + w * t;
                if (math.length(miss) > 2.6f || t >= best) continue;
                best = t;
                axis = math.normalizesafe(miss, new float2(-w.y, w.x));
            }
            return best < float.MaxValue;
        }

        // ---- aiming -------------------------------------------------------------------------

        void SolveAim(BotObservation obs, EnemyView enemy)
        {
            SelfView self = obs.Self;
            if (!self.HasCannon || !enemy.Visible) return;
            float speed = math.max(1f, self.Projectile.Speed);
            float lifetime = math.max(0.1f, self.Projectile.LifetimeSeconds);
            float2 x0 = self.Position;
            float2 target = enemy.CorePosition;
            float2 r = target - x0;
            float2 w = enemy.Velocity - self.Velocity;
            float t0 = SolveFlightTime(r, w, speed);
            if (t0 <= 0f) t0 = math.length(r) / speed;
            t0 = math.min(t0, lifetime);
            float2 direct = math.normalizesafe(r, new float2(0f, 1f));
            float2 dir = math.normalizesafe(r + w * t0 * _profile.LeadQuality, direct);

            if (_profile.GravityAwareLead && _gravity.Count > 0)
            {
                // Iterative shooting method: simulate both bodies under gravity, correct by the miss vector.
                float2 relDir = math.normalizesafe(r + w * t0, dir);
                for (int iter = 0; iter < 3; iter++)
                {
                    SimulateIntercept(x0, self.Velocity + relDir * speed, target, enemy.Velocity, t0,
                        out float2 pEnd, out float2 eEnd);
                    float2 miss = (eEnd - pEnd) * _profile.LeadQuality;
                    float2 v0Rel = relDir * speed + miss / math.max(0.2f, t0);
                    relDir = math.normalizesafe(v0Rel, relDir);
                    t0 = math.clamp(math.length(eEnd - x0) / speed, 0.1f, lifetime);
                }
                dir = relDir;
            }

            // Human-scale error, redrawn per decision so it reads as aiming wobble and not tick noise.
            float err = _rng.Gaussian() * _profile.AimSigma;
            float cs = math.cos(err), sn = math.sin(err);
            dir = new float2(dir.x * cs - dir.y * sn, dir.x * sn + dir.y * cs);

            _aimDir = dir;
            _aimRange = math.length(r);
            _aimFlight = t0;
            _aimValid = true;
        }

        static float SolveFlightTime(float2 r, float2 w, float speed)
        {
            float a = math.dot(w, w) - speed * speed;
            float b = 2f * math.dot(r, w);
            float c = math.dot(r, r);
            if (math.abs(a) < 1e-4f) return b < 0f ? -c / b : -1f;
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return -1f;
            float sq = math.sqrt(disc);
            float t1 = (-b - sq) / (2f * a), t2 = (-b + sq) / (2f * a);
            float lo = math.min(t1, t2), hi = math.max(t1, t2);
            if (lo > 0f) return lo;
            return hi > 0f ? hi : -1f;
        }

        void SimulateIntercept(float2 x0, float2 pv0, float2 e0, float2 ev0, float time,
            out float2 pEnd, out float2 eEnd)
        {
            const float h = 0.1f;
            float2 p = x0, pv = pv0, e = e0, ev = ev0;
            float t = 0f;
            while (t < time - 1e-4f)
            {
                float step = math.min(h, time - t);
                pv += _gravity.AccelerationAt(p) * step;
                p += pv * step;
                ev += _gravity.AccelerationAt(e) * step;
                e += ev * step;
                t += step;
            }
            pEnd = p;
            eEnd = e;
        }

        // ---- per-tick control ---------------------------------------------------------------

        ShipInput Control(BotObservation obs)
        {
            SelfView self = obs.Self;
            float targetRot = HeadingFor(_headAxisLocal, _headDir);
            float err = Wrap(targetRot - self.Rotation);
            float alpha = math.max(0.05f, self.MaxAngularAccel);

            // Bang-coast-bang: command the spin rate that brakes to zero exactly at the target.
            float wMax = math.min(2.5f, math.sqrt(alpha * 4f));
            float wDes = math.sign(err) * math.min(wMax, math.sqrt(2f * alpha * 0.55f * math.max(0f, math.abs(err) - 0.012f)));
            float cmd = (wDes - self.AngularVelocity) / (alpha * 0.45f);
            // Positive steer drives the fins to a clockwise torque, so the sign is flipped here.
            float steer = self.MaxAngularAccel > 0f ? -math.clamp(cmd, -1f, 1f) : 0f;

            float thrust = 0f;
            if (_accelWanted)
            {
                float2 nose = Rotate(new float2(0f, 1f), self.Rotation);
                float c = math.dot(nose, _accelDir);
                if (c > _alignCos && self.MaxForwardAccel > 0f) thrust = 1f;
                else if (c < -_alignCos && self.MaxRetroAccel > 0f) thrust = -1f;
            }

            bool fire = ShouldFire(obs);
            if (fire) FireRequests++;
            return new ShipInput(thrust, steer, fire);
        }

        bool ShouldFire(BotObservation obs)
        {
            SelfView self = obs.Self;
            if (!self.HasCannon || !_aimValid || !_lastEnemy.Visible) return false;
            if (obs.Tick < _nextFireTick) return false;
            if (_mode == BotMode.AvoidPlanet || _mode == BotMode.ReturnToRing || _mode == BotMode.Idle) return false;
            if (_aimFlight > self.Projectile.LifetimeSeconds * 0.9f) return false;

            float2 axisWorld = Rotate(self.AimLocal, self.Rotation);
            float aimErr = math.abs(Wrap(AngleOf(axisWorld) - AngleOf(_aimDir)));
            float tolerance = _profile.FireToleranceScale * math.atan2(math.max(0.8f, _lastEnemy.Radius * 0.7f), math.max(3f, _aimRange));
            if (aimErr > tolerance) { _rollPassed = false; return false; }
            if (PlanetBlocks(obs, self.Position, _lastEnemy.CorePosition)) return false;

            // One discipline roll per cooldown window: a hesitant bot skips whole opportunities, not single ticks.
            if (!_rollPassed)
            {
                if (obs.Tick < _nextRollTick) return false;
                if (!_rng.Chance(_profile.TriggerDiscipline))
                {
                    _nextRollTick = obs.Tick + 12;
                    return false;
                }
                _rollPassed = true;
            }
            _nextFireTick = obs.Tick + (uint)math.ceil(self.CannonCooldownSeconds / math.max(1e-3f, obs.Dt)) + 1u;
            _rollPassed = false;
            return true;
        }

        static bool PlanetBlocks(BotObservation obs, float2 from, float2 to)
        {
            float2 d = to - from;
            float len2 = math.dot(d, d);
            if (len2 < 1e-4f) return false;
            for (int i = 0; i < obs.Planets.Count; i++)
            {
                GravityBody p = obs.Planets[i];
                float t = math.clamp(math.dot(p.Position - from, d) / len2, 0f, 1f);
                if (math.length(from + d * t - p.Position) < p.Radius + 0.3f) return true;
            }
            return false;
        }

        // ---- small maths --------------------------------------------------------------------

        static float AngleOf(float2 v) => math.atan2(v.y, v.x);

        // The ship rotation that points `axisLocal` along `worldDir`.
        static float HeadingFor(float2 axisLocal, float2 worldDir) => AngleOf(worldDir) - AngleOf(axisLocal);

        static float Wrap(float a)
        {
            while (a > Pi) a -= TwoPi;
            while (a < -Pi) a += TwoPi;
            return a;
        }

        static float2 Rotate(float2 v, float r)
        {
            float s = math.sin(r), c = math.cos(r);
            return new float2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
