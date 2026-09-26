using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hullbreach.Core;
using Hullbreach.Game;
using Hullbreach.Hud;

namespace Hullbreach.Demo.Tests
{
    // The two ends of the structural calibration: the stock ship must
    // never break itself, and a badly built ship must still come apart.
    public sealed class DemoStructureTests : DemoSceneFixture
    {
        const float SafeRatio = 0.4f;

        const float SafeLoadFactor = 2f;

        [UnityTest]
        public IEnumerator StockShip_SurvivesEveryControlCombination()
        {
            yield return LoadDemoScene();
            Demo.SetState(DemoState.Fly);
            yield return FixedSteps(0.5f);

            int blocksAtStart = Player.Ship.Grid.Count;
            var combinations = new (float thrust, float steer, string name)[]
            {
                (1f, 0f, "full thrust"),
                (-1f, 0f, "full reverse"),
                (0f, 1f, "full steer right"),
                (0f, -1f, "full steer left"),
                (1f, 1f, "thrust + hard right"),
                (1f, -1f, "thrust + hard left"),
                (-1f, 1f, "reverse + hard right"),
            };

            float worstEver = 0f;
            float lowestLoadFactor = float.PositiveInfinity;

            foreach (var combo in combinations)
            {
                Input.Thrust = combo.thrust;
                Input.SteerAxis = combo.steer;

                // Sampled every fixed step, so a transient spike is not
                // missed at the end.
                float ratio = 0f;
                string worstBlock = "none";
                float clf = float.PositiveInfinity;
                float peakDuctile = 0f, peakBrittle = 0f, peakBuckling = 0f;
                int countBefore = Player.Ship.Grid.Count;

                int steps = Mathf.CeilToInt(10f / Time.fixedDeltaTime);
                for (int i = 0; i < steps; i++)
                {
                    yield return new WaitForFixedUpdate();
                    float sample = MaxStressRatio(out string where);
                    if (sample > ratio) { ratio = sample; worstBlock = where; }
                    float sampleClf = Demo.PlayerStructure.Solver.CriticalLoadFactor;
                    if (!float.IsInfinity(sampleClf) && sampleClf < clf) clf = sampleClf;
                    StressBreakdown(out float d, out float br, out float bu);
                    peakDuctile = Mathf.Max(peakDuctile, d);
                    peakBrittle = Mathf.Max(peakBrittle, br);
                    peakBuckling = Mathf.Max(peakBuckling, bu);

                    if (Player.Ship.Grid.Count < countBefore)
                    {
                        Debug.Log($"Calibration [{combo.name}]: LOST a block at t = {i * Time.fixedDeltaTime:0.00} s "
                                  + $"({countBefore} -> {Player.Ship.Grid.Count}), peak ratio so far {ratio:0.000} "
                                  + $"at {worstBlock}, lowest clf {clf:0.00}, throttle "
                                  + $"fwd {Player.Ship.ForwardThrottleMean:0.00} rev {Player.Ship.ReverseThrottleMean:0.00} "
                                  + $"steer {Player.Ship.SteerThrottleMean:0.00}, thrusters {Player.Ship.ThrusterKeys.Length}");
                        countBefore = Player.Ship.Grid.Count;
                    }
                }

                worstEver = Mathf.Max(worstEver, ratio);
                if (!float.IsInfinity(clf)) lowestLoadFactor = Mathf.Min(lowestLoadFactor, clf);

                Debug.Log($"Calibration [{combo.name}]: peak ratio {ratio:0.000} at {worstBlock}, "
                          + $"lowest critical load factor {(float.IsInfinity(clf) ? "inf" : clf.ToString("0.00"))}, "
                          + $"blocks {Player.Ship.Grid.Count}, warning {Demo.Warning}, "
                          + $"bars fwd {Player.Ship.ForwardThrottleMean:0.00} rev {Player.Ship.ReverseThrottleMean:0.00} "
                          + $"steer {Player.Ship.SteerThrottleMean:0.00} | peaks ductile {peakDuctile:0.000} "
                          + $"brittle {peakBrittle:0.000} buckling {peakBuckling:0.000}");

                Assert.AreEqual(blocksAtStart, Player.Ship.Grid.Count,
                    $"the stock ship lost blocks under [{combo.name}]");
                Assert.Less(ratio, SafeRatio,
                    $"[{combo.name}] loaded the stock ship to {ratio:0.00} of yield");
                Assert.IsTrue(float.IsInfinity(clf) || clf > SafeLoadFactor,
                    $"[{combo.name}] dropped the critical load factor to {clf:0.00}");
                Assert.AreEqual(HullWarning.Ok, Demo.Warning,
                    $"[{combo.name}] alarmed the player on a perfectly sound ship");
            }

            Debug.Log($"Calibration summary: worst ratio across every control combination {worstEver:0.000}, "
                      + $"lowest critical load factor "
                      + $"{(float.IsInfinity(lowestLoadFactor) ? "inf" : lowestLoadFactor.ToString("0.00"))}");
        }

        [UnityTest]
        public IEnumerator LongArm_WarnsThenBreaks()
        {
            yield return LoadDemoScene();
            var layout = LongArmShip();
            Player.ReplaceBlocks(layout);
            int blocksAtStart = Player.Ship.Grid.Count;
            Debug.Log($"LongArm: built a {blocksAtStart}-block ship with a 12-cell 1-wide arm");
            Assert.AreEqual(layout.Count, blocksAtStart, "the test ship did not build fully");

            // Coasting must not break even a badly shaped ship: only the
            // player's own thrust through that arm should.
            Demo.SetState(DemoState.Fly);
            yield return FixedSteps(1f);
            Assert.AreEqual(blocksAtStart, Player.Ship.Grid.Count,
                $"the long-arm ship fell apart while just coasting "
                + $"(max ratio {MaxStressRatio(out _):0.000})");

            Input.Thrust = 1f;

            bool warnedFirst = false;
            bool broke = false;
            float worstSeen = 0f;
            float lowestLoadFactor = float.PositiveInfinity;

            for (float t = 0f; t < 8f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                worstSeen = Mathf.Max(worstSeen, MaxStressRatio(out _));
                lowestLoadFactor = Mathf.Min(lowestLoadFactor, Demo.PlayerStructure.Solver.CriticalLoadFactor);

                if (Player.Ship.Grid.Count < blocksAtStart)
                {
                    broke = true;
                    StressBreakdown(out float d, out float br, out float bu);
                    float clf = Demo.PlayerStructure.Solver.CriticalLoadFactor;
                    Debug.Log($"LongArm: first block came off at t = {t:0.00} s "
                              + $"(count {blocksAtStart} -> {Player.Ship.Grid.Count}), "
                              + $"peak ratio {worstSeen:0.000}, at failure ductile {d:0.000} "
                              + $"brittle {br:0.000} buckling {bu:0.000}, critical load factor "
                              + $"{(float.IsInfinity(clf) ? "inf" : clf.ToString("0.000"))}, "
                              + $"warning {Demo.Warning}");
                    break;
                }

                if (Demo.Warning == HullWarning.Critical && !warnedFirst)
                {
                    warnedFirst = true;
                    Debug.Log($"LongArm: HUD reached CRITICAL at t = {t:0.00} s, "
                              + $"max ratio {Demo.MaxStressRatio:0.000}, "
                              + $"{Demo.CriticalBlockCount} block(s) in the red");
                }
            }

            Debug.Log($"LongArm: lowest critical load factor seen "
                      + $"{(float.IsInfinity(lowestLoadFactor) ? "inf" : lowestLoadFactor.ToString("0.000"))}");
            Assert.IsTrue(warnedFirst, "the HUD never warned the player before the arm failed");
            Assert.IsTrue(broke, $"a 12-block unsupported arm at full thrust never failed "
                                 + $"(peak ratio only {worstSeen:0.000})");
        }

        // A deliberately bad ship: a 1-wide arm carries the whole thrust
        // in bending.
        static List<AuthoredBlock> LongArmShip()
        {
            var blocks = new List<AuthoredBlock> { new AuthoredBlock(0, 0, BlockTypes.Core) };

            // A 5x6 hull body around the core (skipping the core cell), which
            // together with the arm puts the ship over 40 blocks.
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 3; y++)
                {
                    if (x == 0 && y == 0) continue;
                    blocks.Add(new AuthoredBlock(x, y, BlockTypes.Hull));
                }
            }

            // The arm: 12 hull cells +x from the body, thruster at the tip,
            // loaded in pure bending.
            const int armLength = 12;
            for (int i = 1; i <= armLength; i++)
            {
                blocks.Add(new AuthoredBlock(2 + i, 0, BlockTypes.Hull));
            }
            blocks.Add(new AuthoredBlock(2 + armLength + 1, 0, BlockTypes.Thruster));

            return blocks;
        }
    }
}
