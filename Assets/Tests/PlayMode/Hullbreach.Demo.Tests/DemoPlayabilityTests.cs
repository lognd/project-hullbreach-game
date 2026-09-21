using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Game;

namespace Hullbreach.Demo.Tests
{
    /// <summary>
    /// One test per playability requirement the user reported broken: the
    /// ship must survive being left alone, Tab must not teleport, thrust and
    /// steer must do what they say, firing must hit, building must build, and
    /// the overlay and reset keys must not throw.
    /// </summary>
    public sealed class DemoPlayabilityTests : DemoSceneFixture
    {
        [UnityTest]
        public IEnumerator NoInput_ShipSurvivesTenSeconds()
        {
            yield return LoadDemoScene();

            int blocksAtStart = Player.Ship.Grid.Count;
            Demo.SetState(DemoState.Fly);
            yield return FixedSteps(10f);

            float worstRatio = MaxStressRatio(out string worstBlock);
            Debug.Log($"NoInput: blocks {blocksAtStart} -> {Player.Ship.Grid.Count}, "
                      + $"pos {PlayerPosition()}, max ratio {worstRatio:0.000} at {worstBlock}");

            Assert.AreEqual(blocksAtStart, Player.Ship.Grid.Count,
                "the ship shed blocks with nobody touching the controls");
            Assert.IsTrue(IsSane(PlayerPosition()), $"position went wild: {PlayerPosition()}");
            Assert.IsTrue(IsSane(PlayerVelocity(), 100f), $"velocity went wild: {PlayerVelocity()}");
        }

        [UnityTest]
        public IEnumerator ToggleMode_DoesNotTeleport()
        {
            yield return LoadDemoScene();
            Demo.SetState(DemoState.Fly);
            yield return FixedSteps(1f);

            var sequence = new[] { DemoState.Build, DemoState.Fly, DemoState.Build, DemoState.Fly };
            foreach (var next in sequence)
            {
                Vector2 before = PlayerPosition();
                Vector2 velocityBefore = PlayerVelocity();

                Demo.SetState(next);
                yield return new WaitForFixedUpdate();

                Vector2 after = PlayerPosition();
                Vector2 velocityAfter = PlayerVelocity();
                float jump = Vector2.Distance(before, after);
                float velocityJump = Vector2.Distance(velocityBefore, velocityAfter);
                Debug.Log($"Toggle -> {next}: moved {jump:0.000} u, dv {velocityJump:0.000} u/s");

                Assert.Less(jump, 0.5f, $"switching to {next} teleported the ship {jump:0.00} units");
                Assert.Less(velocityJump, 5f, $"switching to {next} spiked velocity by {velocityJump:0.00} u/s");

                if (next == DemoState.Fly)
                {
                    var indicator = Demo.Builder.HoverIndicator;
                    Assert.IsTrue(indicator == null || !indicator.activeSelf,
                        "the build hover cursor was still visible in Fly mode");
                    Assert.AreEqual(BuilderState.Idle, Demo.Builder.Session.State,
                        "a half-finished placement survived leaving Build mode");
                }

                yield return FixedSteps(1f);
            }
        }

        [UnityTest]
        public IEnumerator Thrust_MovesForwardWithoutBreaking()
        {
            yield return LoadDemoScene();
            Demo.SetState(DemoState.Fly);
            yield return FixedSteps(0.5f);

            int blocksAtStart = Player.Ship.Grid.Count;
            Vector2 velocityBefore = PlayerVelocity();
            Vector2 forward = PlayerForward();

            Debug.Log($"Thrust: thrusters {Player.Ship.ThrusterKeys.Length}, retros {Player.Ship.RetroKeys.Length}, "
                      + $"fins {Player.Ship.FinKeys.Length}, "
                      + $"input source is scripted: {ReferenceEquals(Player.InputSource, Input)}");

            Input.Thrust = 1f;
            // The stock ramp level takes 1 s to reach full throttle, so the
            // bar must still be climbing at 0.3 s and be at (or very near)
            // full by 1.2 s. A jump straight to 100% would mean the ramp was
            // bypassed and the structure eats a step load.
            yield return FixedSteps(0.3f);
            float early = Player.Ship.ForwardThrottleMean;
            yield return FixedSteps(2.7f);
            float late = Player.Ship.ForwardThrottleMean;

            float worstRatio = MaxStressRatio(out string worstBlock);
            Vector2 dv = PlayerVelocity() - velocityBefore;
            Debug.Log($"Thrust: throttle {early:0.00} -> {late:0.00}, dv {dv}, "
                      + $"|w| {Mathf.Abs(Player.Ship.AngularVelocity):0.000}, "
                      + $"max ratio {worstRatio:0.000} at {worstBlock}");

            Assert.Greater(early, 0.05f, "throttle had not started ramping after 0.3 s");
            Assert.Less(early, 0.75f, "throttle jumped instead of ramping over the configured 1 s");
            Assert.Greater(late, 0.95f, "throttle never reached full");
            // Along the ship's own nose, not raw speed: the demo flies in a
            // gravity well, and thrusting radially outward from a circular
            // orbit raises the orbit and LOWERS speed. Asserting "speed went
            // up" would be asserting that orbital mechanics is wrong.
            Assert.Greater(Vector2.Dot(dv, forward), 1f,
                "thrust did not accelerate the ship along its own forward direction");
            Assert.AreEqual(blocksAtStart, Player.Ship.Grid.Count, "thrusting tore blocks off the ship");
            Assert.Less(Mathf.Abs(Player.Ship.AngularVelocity), 2f,
                "a symmetric ship spun up under pure forward thrust");
        }

        [UnityTest]
        public IEnumerator Steer_TurnsAndFadesOnRelease()
        {
            yield return LoadDemoScene();
            Demo.SetState(DemoState.Fly);
            yield return FixedSteps(0.5f);

            Input.SteerAxis = 1f;
            yield return FixedSteps(0.3f);
            float earlySteer = Mathf.Abs(Player.Ship.SteerThrottleMean);
            yield return FixedSteps(0.7f);

            float fullSteer = Mathf.Abs(Player.Ship.SteerThrottleMean);

            // Peak spin is reached shortly AFTER the key is released: the fin
            // throttle ramps down over the same second it ramped up, so it is
            // still applying torque for a moment. Sample the peak across the
            // release rather than the instant of release, or the test would
            // be asserting against a number the ship has not reached yet.
            Input.SteerAxis = 0f;
            float peakSpin = Mathf.Abs(Player.Ship.AngularVelocity);
            int settleSteps = Mathf.CeilToInt(1.5f / Time.fixedDeltaTime);
            for (int i = 0; i < settleSteps; i++)
            {
                yield return new WaitForFixedUpdate();
                peakSpin = Mathf.Max(peakSpin, Mathf.Abs(Player.Ship.AngularVelocity));
            }

            float releasedSteer = Mathf.Abs(Player.Ship.SteerThrottleMean);
            yield return FixedSteps(2f);
            float faded = Mathf.Abs(Player.Ship.AngularVelocity);

            Debug.Log($"Steer: bar {earlySteer:0.00} -> {fullSteer:0.00} -> {releasedSteer:0.00}, "
                      + $"|w| peak {peakSpin:0.000} -> {faded:0.000} two seconds later");

            Assert.Greater(earlySteer, 0.05f, "steer bar had not started ramping after 0.3 s");
            Assert.Less(earlySteer, 0.75f, "steer bar jumped instead of ramping");
            Assert.Greater(fullSteer, 0.95f, "the steer bar never reached full deflection");
            Assert.Greater(peakSpin, 0.01f, "holding steer produced no rotation");
            Assert.Less(releasedSteer, 0.05f, "the fins kept deflecting after the key was released");
            Assert.Less(faded, peakSpin * 0.9f,
                "spin did not decay after releasing steer: the ship can only be stopped by counter-steering");
        }

        [UnityTest]
        public IEnumerator Fire_SpawnsProjectileAndHitsTarget()
        {
            yield return LoadDemoScene();
            Demo.SetState(DemoState.Fly);

            var targetObject = GameObject.Find("TargetShip");
            Assert.IsNotNull(targetObject, "DemoScene has no TargetShip.");
            var target = targetObject.GetComponent<ShipController>();
            Assert.IsNotNull(target, "TargetShip has no ShipController.");
            yield return FixedSteps(0.2f);

            // Line the player up dead below the target so its +y cannon points
            // straight at it, and hold both still: the point of this test is
            // the fire/hit path, not marksmanship.
            Vector2 targetPosition = new Vector2(target.Ship.Position.x, target.Ship.Position.y);
            Player.ResetTo(targetPosition - new Vector2(0f, 8f), Vector2.zero);
            yield return new WaitForFixedUpdate();

            int targetBlocksBefore = target.Ship.Grid.Count;
            int targetDamageBefore = TotalDamage(target);

            Input.PressFire();
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;

            var projectiles = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            Debug.Log($"Fire: {projectiles.Length} projectile(s) alive one frame after the press");
            Assert.Greater(projectiles.Length, 0, "pressing fire spawned no projectile");

            bool hit = false;
            for (float t = 0f; t < 4f && !hit; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                hit = target.Ship.Grid.Count < targetBlocksBefore
                      || TotalDamage(target) > targetDamageBefore;
            }

            Debug.Log($"Fire: target blocks {targetBlocksBefore} -> {target.Ship.Grid.Count}, "
                      + $"damage {targetDamageBefore} -> {TotalDamage(target)}");
            Assert.IsTrue(hit, "the shot never damaged or removed a block on the target ship");
        }

        [UnityTest]
        public IEnumerator Build_PlaceAndRemove()
        {
            yield return LoadDemoScene();
            Demo.SetState(DemoState.Build);
            yield return null;

            var builder = Demo.Builder;
            Assert.IsNotNull(builder, "DemoMode has no BuilderController.");
            var grid = Player.Ship.Grid;
            builder.Session.Select(BlockTypes.Hull);

            // (-2,0) is empty, touches the hull at (-1,... ) via the core row
            // only if adjacent; use (1,0), which is 4-adjacent to the core.
            int freeKey = BlockKey.Pack(1, 0);
            int countBefore = grid.Count;
            Assert.IsTrue(builder.TryPlaceAt(freeKey),
                $"placing a hull beside the core was refused: {builder.VerdictAt(freeKey)}");
            Assert.AreEqual(countBefore + 1, grid.Count, "placement did not add a block");

            // Directly behind the thruster at (-1,-1): its exhaust cell, which
            // the clearance rules reserve.
            int exhaustKey = BlockKey.Pack(-1, -2);
            var verdict = builder.VerdictAt(exhaustKey);
            Debug.Log($"Build: placing into the thruster exhaust cell gives verdict {verdict}");
            Assert.IsFalse(builder.TryPlaceAt(exhaustKey), "a hull was allowed to block a thruster exhaust");
            Assert.AreNotEqual(PlacementVerdict.Ok, verdict);
            Assert.IsTrue(verdict == PlacementVerdict.InsideReservedCell
                          || verdict == PlacementVerdict.BlocksExhaust,
                $"expected a clearance refusal, got {verdict}");

            Assert.IsTrue(builder.TryRemoveAt(freeKey), "removing the hull just placed was refused");
            Assert.AreEqual(countBefore, grid.Count, "removal did not take the block off");

            Assert.IsTrue(builder.Session.Undo(), "undo of the removal was refused");
            Assert.AreEqual(countBefore + 1, grid.Count, "undo did not put the block back");
        }

        [UnityTest]
        public IEnumerator Overlay_CyclesWithoutErrors()
        {
            yield return LoadDemoScene();
            Demo.SetState(DemoState.Fly);
            yield return FixedSteps(0.5f);

            var seen = new System.Collections.Generic.List<OverlayMode>();
            for (int i = 0; i < 6; i++)
            {
                seen.Add(Demo.PlayerRenderer.Overlay);
                Input.PressOverlay();
                yield return null;
                yield return new WaitForFixedUpdate();
            }

            Debug.Log("Overlay cycle: " + string.Join(" -> ", seen));
            Assert.Contains(OverlayMode.Stress, seen);
            Assert.Contains(OverlayMode.LoadBearing, seen);
            Assert.Contains(OverlayMode.Damage, seen);
            Assert.Contains(OverlayMode.Buckling, seen);
            Assert.AreEqual(OverlayMode.Stress, Demo.PlayerRenderer.Overlay,
                "the overlay cycle did not come back around");
        }

        [UnityTest]
        public IEnumerator Reset_ReturnsToStart()
        {
            yield return LoadDemoScene();
            Demo.SetState(DemoState.Fly);

            Input.Thrust = 1f;
            Input.SteerAxis = 1f;
            yield return FixedSteps(3f);
            Input.Thrust = 0f;
            Input.SteerAxis = 0f;

            Vector2 expectedPosition = Demo.OrbitStartPosition;
            Vector2 expectedVelocity = Demo.OrbitStartVelocity();

            Input.PressReset();
            yield return null;
            yield return new WaitForFixedUpdate();

            Debug.Log($"Reset: pos {PlayerPosition()} (want {expectedPosition}), "
                      + $"vel {PlayerVelocity()} (want {expectedVelocity})");

            Assert.Less(Vector2.Distance(PlayerPosition(), expectedPosition), 1f,
                "R did not return the ship to the orbit start");
            Assert.Less(Vector2.Distance(PlayerVelocity(), expectedVelocity), 0.5f,
                "R did not restore the orbital velocity");
        }

        [UnityTest]
        public IEnumerator RammingTheTarget_DoesNotFlingEitherShip()
        {
            yield return LoadDemoScene();
            Demo.SetState(DemoState.Fly);
            yield return FixedSteps(0.2f);

            var target = GameObject.Find("TargetShip").GetComponent<ShipController>();
            Assert.IsNotNull(target, "DemoScene has no TargetShip with a ShipController.");

            // Park the player six units off the target and point its nose
            // (ship-local +y, the thrust direction) straight at it.
            Vector2 targetPosition = new Vector2(target.Ship.Position.x, target.Ship.Position.y);
            Vector2 approach = (targetPosition - PlayerPosition()).normalized;
            if (approach.sqrMagnitude < 0.5f) approach = Vector2.right;
            Player.ResetTo(targetPosition - approach * 4f,
                           new Vector2(target.Ship.Velocity.x, target.Ship.Velocity.y));
            Player.Ship.Rotation = Mathf.Atan2(approach.y, approach.x) - Mathf.PI * 0.5f;
            yield return new WaitForFixedUpdate();

            Input.Thrust = 1f;
            float closestApproach = float.MaxValue;
            float fastestSeen = 0f;
            int steps = Mathf.CeilToInt(3f / Time.fixedDeltaTime);
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                Vector2 targetNow = new Vector2(target.Ship.Position.x, target.Ship.Position.y);
                closestApproach = Mathf.Min(closestApproach, Vector2.Distance(PlayerPosition(), targetNow));
                fastestSeen = Mathf.Max(fastestSeen, Mathf.Max(
                    PlayerVelocity().magnitude,
                    new Vector2(target.Ship.Velocity.x, target.Ship.Velocity.y).magnitude));
            }

            Vector2 targetEnd = new Vector2(target.Ship.Position.x, target.Ship.Position.y);
            Debug.Log($"Ram: closest approach {closestApproach:0.00} u, fastest either ship went "
                      + $"{fastestSeen:0.0} u/s, player ended at {PlayerPosition()}, target at {targetEnd}");

            Assert.Less(closestApproach, 3.5f, "the player never actually reached the target ship");
            Assert.Less(PlayerPosition().magnitude, 120f, "the player was flung out of the arena");
            Assert.Less(targetEnd.magnitude, 120f, "the target ship was flung out of the arena");
            Assert.Less(fastestSeen, 40f, $"a ship reached {fastestSeen:0.0} u/s: the contact was explosive");
            Assert.IsTrue(IsSane(PlayerPosition()) && IsSane(targetEnd), "a ship position went non-finite");
        }

        static int TotalDamage(ShipController ship)
        {
            int sum = 0;
            foreach (var kv in ship.Ship.Grid.All) sum += kv.Value.Damage;
            return sum;
        }
    }
}
