using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hullbreach.Core;
using Hullbreach.Game;

namespace Hullbreach.Demo.Tests
{
    /// <summary>
    /// Saves PNGs of the demo in each state worth looking at. Not an
    /// assertion suite: it exists so a human (or a reviewer) can SEE that the
    /// ship renders, the HUD reads sensibly and the thruster effects fire,
    /// which no numeric assertion covers.
    ///
    /// Skipped unless HULLBREACH_SHOTS names a directory, and must be run
    /// WITHOUT -nographics or every capture comes out blank.
    /// </summary>
    public sealed class DemoScreenshots : DemoSceneFixture
    {
        /// <summary>Environment variable naming the output directory.</summary>
        public const string OutputVariable = "HULLBREACH_SHOTS";

        string _directory;

        [UnityTest]
        public IEnumerator CaptureDemoStates()
        {
            _directory = Environment.GetEnvironmentVariable(OutputVariable);
            if (string.IsNullOrEmpty(_directory))
            {
                // Fall back to a path that always exists, and say so loudly,
                // rather than silently producing nothing.
                _directory = Path.Combine(Application.persistentDataPath, "hullbreach-shots");
                Debug.Log($"{OutputVariable} is unset; writing screenshots to {_directory} instead.");
            }
            Directory.CreateDirectory(_directory);
            Debug.Log($"Screenshot directory: {_directory}");

            yield return LoadDemoScene();

            // 1. Build mode, with the placement preview pinned over a legal
            //    cell so the green indicator and the palette HUD both show.
            Demo.SetState(DemoState.Build);
            Demo.Builder.Session.Select(BlockTypes.Hull);
            Demo.Builder.PreviewHoverAt(BlockKey.Pack(1, 0));
            yield return Capture("01-build-mode-hover");

            // 2. Fly mode after two seconds of full thrust: red flames and a
            //    particle trail out the back of both thrusters.
            Demo.Builder.PreviewHoverAt(null);
            Demo.SetState(DemoState.Fly);
            Input.Thrust = 1f;
            yield return FixedSteps(2f);
            yield return Capture("02-fly-full-thrust-red");

            // 3. Reversing: green retro flames out the forward-facing nozzles.
            Input.Thrust = -1f;
            yield return FixedSteps(2.5f);
            yield return Capture("03-fly-reverse-green");

            // 4. The stress overlay over a thrusting ship.
            Input.Thrust = 1f;
            Demo.PlayerRenderer.Overlay = OverlayMode.Stress;
            yield return FixedSteps(1.5f);
            yield return Capture("04-stress-overlay");

            // 5. Just after firing, with a round in flight.
            Demo.PlayerRenderer.Overlay = OverlayMode.None;
            Input.Thrust = 0f;
            yield return FixedSteps(0.5f);
            Input.PressFire();
            yield return null;
            yield return FixedSteps(0.15f);
            yield return Capture("05-after-firing");

            Assert.Pass($"Screenshots written to {_directory}");
        }

        /// <summary>Captures one PNG and waits for the file to appear;
        /// ScreenCapture writes at the end of a later frame, not inline.</summary>
        IEnumerator Capture(string name)
        {
            string path = Path.Combine(_directory, name + ".png");
            ScreenCapture.CaptureScreenshot(path);

            for (int i = 0; i < 30 && !File.Exists(path); i++) yield return null;
            Debug.Log(File.Exists(path)
                ? $"Captured {path} ({new FileInfo(path).Length} bytes)"
                : $"WARNING: capture did not appear at {path}");
        }
    }
}
