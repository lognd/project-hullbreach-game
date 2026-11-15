using UnityEngine;
using Unity.Mathematics;
using Hullbreach.Hud;
using Hullbreach.World;

namespace Hullbreach.Game
{
    // Offline storm for the DemoScene: a local StormZone built from the scene's gravity bodies with a
    // fixed seed, advanced by physics ticks while the ship is flying (so Build mode never burns the clock).
    // Online the server owns the zone; see HullbreachStormHudSource.
    // frob:doc docs/design/ui-port.md#stormhudview
    public sealed class DemoStormHudSource : MonoBehaviour, IStormHudSource
    {
        [SerializeField] DemoMode demoMode;
        [SerializeField] uint seed = 1337;
        // Optional stand-in for the opponent; the demo has none, so the radar and arrow stay quiet.
        [SerializeField] Transform enemy;

        StormZone _zone;
        MinimapDisc[] _planets;
        float _tickRate;
        int _tick;
        float2 _arenaCenter;

        public Transform Enemy { get => enemy; set => enemy = value; }

        // Sim ticks since the storm started; tests and the playtest can jump it.
        public int Tick { get => _tick; set => _tick = Mathf.Max(0, value); }

        // Restart the clock (R resets the ship; the storm restarts with it).
        public void Restart() => _tick = 0;

        void Awake()
        {
            if (demoMode == null) demoMode = FindFirstObjectByType<DemoMode>();
        }

        void FixedUpdate()
        {
            if (demoMode == null || demoMode.State == DemoState.Fly) _tick++;
            PushBackIntoArena();
        }

        // Same soft edge the online server applies (ArenaBounds: a velocity change only), sized to the storm's
        // starting ring, so the offline ship cannot fly away into an empty void (it used to reach y=1000).
        void PushBackIntoArena()
        {
            if (demoMode == null || demoMode.State != DemoState.Fly || demoMode.PlayerShip == null) return;
            var body = demoMode.PlayerShip.GetComponent<Rigidbody2D>();
            if (body == null) return;
            EnsureZone();
            var bounds = new ArenaBounds(_arenaCenter, _zone.Config.StartRadius, 20f, 8f);
            float2 position = new float2(body.position.x, body.position.y);
            if (!bounds.InPushZone(position)) return;
            float2 push = bounds.PushAcceleration(position) * Time.fixedDeltaTime;
            body.linearVelocity += new Vector2(push.x, push.y);
        }

        void EnsureZone()
        {
            if (_zone != null) return;
            _planets = StormHudPlanets.FromField(GravityWorld.Field);
            var cfg = StormConfig.Default();
            _tickRate = 1f / Time.fixedDeltaTime;
            cfg = cfg.WithTickRate(_tickRate);
            _arenaCenter = float2.zero;
            _zone = new StormZone(cfg, _arenaCenter, StormHudPlanets.Centers(_planets), seed);
        }

        public bool TryGetFrame(out StormHudFrame frame)
        {
            frame = default;
            EnsureZone();
            frame.HasStorm = true;
            frame.Storm = _zone.At(_tick);
            frame.TickRate = _tickRate;
            frame.Planets = _planets;
            frame.ArenaCenter = new Vector2(_arenaCenter.x, _arenaCenter.y);
            frame.ArenaRadius = _zone.Config.StartRadius;
            if (demoMode != null && demoMode.PlayerShip != null)
            {
                frame.HasLocal = true;
                frame.LocalPosition = demoMode.PlayerShip.transform.position;
            }
            if (enemy != null)
            {
                frame.HasEnemy = true;
                frame.EnemyPosition = enemy.position;
            }
            return true;
        }
    }
}
