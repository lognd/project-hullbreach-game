using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Hullbreach.Core;
using Hullbreach.Game;

namespace Hullbreach.Demo.Tests
{
    /// <summary>
    /// Shared setup for every demo play-mode test: loads DemoScene, finds the
    /// scene's DemoMode, injects a ScriptedDemoInput, and fails the test if
    /// ANY error or exception is logged while it runs.
    ///
    /// The log guard is not decoration. "Play mode instantly breaks" is the
    /// bug these tests exist to catch, and the first symptom of most of its
    /// causes is a NullReference or an assert in the log that nobody reads.
    /// </summary>
    public abstract class DemoSceneFixture
    {
        /// <summary>The scene's conductor, found after the scene loads.</summary>
        protected DemoMode Demo { get; private set; }

        /// <summary>The player's ShipController.</summary>
        protected ShipController Player => Demo.PlayerShip;

        /// <summary>The scripted input every test drives the demo through.</summary>
        protected ScriptedDemoInput Input { get; private set; }

        readonly List<string> _errors = new List<string>();

        /// <summary>
        /// Loads DemoScene and waits for Awake/Start/the first fixed step, so
        /// a test body starts from a scene that is genuinely running.
        /// </summary>
        protected IEnumerator LoadDemoScene()
        {
            _errors.Clear();
            Application.logMessageReceived += OnLog;

            yield return SceneManager.LoadSceneAsync("DemoScene", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForFixedUpdate();

            Demo = Object.FindAnyObjectByType<DemoMode>();
            Assert.IsNotNull(Demo, "DemoScene has no DemoMode.");
            Assert.IsNotNull(Demo.PlayerShip, "DemoMode has no player ShipController.");

            Input = new ScriptedDemoInput();
            Demo.InputSource = Input;
        }

        [TearDown]
        public void AssertNothingLoggedAnError()
        {
            Application.logMessageReceived -= OnLog;
            LogAssert.NoUnexpectedReceived();
            if (_errors.Count > 0)
            {
                Assert.Fail($"{_errors.Count} error(s)/exception(s) logged during the test:\n"
                            + string.Join("\n", _errors));
            }
        }

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors.Add($"[{type}] {message}");
            }
        }

        /// <summary>Advances `seconds` of FIXED steps (not rendered frames),
        /// so a test measures the same timeline the simulation runs on.</summary>
        protected static IEnumerator FixedSteps(float seconds)
        {
            int steps = Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
        }

        /// <summary>The player ship's world position as a Vector2, read from
        /// the authoritative plain-C# body rather than the transform.</summary>
        protected Vector2 PlayerPosition()
            => new Vector2(Player.Ship.Position.x, Player.Ship.Position.y);

        /// <summary>The player ship's world velocity as a Vector2.</summary>
        protected Vector2 PlayerVelocity()
            => new Vector2(Player.Ship.Velocity.x, Player.Ship.Velocity.y);

        /// <summary>The player's ship-local +y (its thrust/cannon direction)
        /// rotated into world space.</summary>
        protected Vector2 PlayerForward()
        {
            var f = Player.Ship.RotateLocalToWorld(new Unity.Mathematics.float2(0f, 1f));
            return new Vector2(f.x, f.y);
        }

        /// <summary>Highest max(ductile, brittle, buckling) ratio across the
        /// player's blocks right now, with the worst block's name, for the
        /// calibration assertions and for printing into the run log.</summary>
        protected float MaxStressRatio(out string worstBlock)
        {
            worstBlock = "none";
            var structure = Demo.PlayerStructure;
            if (structure == null) return 0f;

            float worst = 0f;
            foreach (var kvp in structure.Solver.BlockStresses)
            {
                var s = kvp.Value;
                float ratio = Mathf.Max(s.DuctileRatio, Mathf.Max(s.BrittleRatio, s.BucklingRatio));
                if (ratio <= worst) continue;
                worst = ratio;
                if (Player.Ship.Grid.TryGet(kvp.Key, out var block))
                {
                    BlockKey.Unpack(kvp.Key, out int x, out int y);
                    worstBlock = $"{BlockTypes.Get(block.TypeId).Name}({x},{y})";
                }
            }
            return worst;
        }

        /// <summary>
        /// The three failure channels separately (ductile, brittle,
        /// buckling), because "the max ratio is 3.7" says nothing about WHICH
        /// mechanism is firing, and the two are calibrated by different
        /// knobs (LoadScale vs MaterialStiffnessScale).
        /// </summary>
        protected void StressBreakdown(out float ductile, out float brittle, out float buckling)
        {
            ductile = 0f;
            brittle = 0f;
            buckling = 0f;
            var structure = Demo.PlayerStructure;
            if (structure == null) return;

            foreach (var kvp in structure.Solver.BlockStresses)
            {
                var s = kvp.Value;
                if (s.DuctileRatio > ductile) ductile = s.DuctileRatio;
                if (s.BrittleRatio > brittle) brittle = s.BrittleRatio;
                if (s.BucklingRatio > buckling) buckling = s.BucklingRatio;
            }
        }

        /// <summary>True when the position has gone non-finite or absurd, the
        /// two ways "play mode instantly breaks" shows up numerically.</summary>
        protected static bool IsSane(Vector2 v, float bound = 500f)
            => !float.IsNaN(v.x) && !float.IsNaN(v.y)
               && !float.IsInfinity(v.x) && !float.IsInfinity(v.y)
               && Mathf.Abs(v.x) < bound && Mathf.Abs(v.y) < bound;
    }
}
