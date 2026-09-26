using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Hullbreach.Hud;
using Hullbreach.Game;

namespace Hullbreach.Demo.Tests
{
    // Play-mode proof of ui-port.md section 5 criterion 4: one HudCanvas/EventSystem,
    // panel visibility per mode, on-screen text matching the model, and a layout
    // check standing in for the visual check ScreenCapture cannot do headless (D7).
    public sealed class DemoHudTests : DemoSceneFixture
    {
        // Private [SerializeField]/instance fields are read by reflection rather
        // than made public, so the views stay exactly the thin MonoBehaviours D4
        // specifies; the test reaches in instead of widening their contract.
        static T GetField<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType()} has no field '{name}'");
            return (T)field.GetValue(target);
        }

        [UnityTest]
        public IEnumerator Scene_HasExactlyOneHudCanvasAndOneEventSystem()
        {
            yield return LoadDemoScene();

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            int hudCanvases = 0;
            foreach (var c in canvases) if (c.gameObject.name == "HudCanvas") hudCanvases++;
            Assert.AreEqual(1, hudCanvases, "DemoScene must have exactly one HudCanvas.");

            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            Assert.AreEqual(1, eventSystems.Length, "DemoScene must have exactly one EventSystem.");
        }

        [UnityTest]
        public IEnumerator Panels_ActiveOnlyInTheirMode()
        {
            yield return LoadDemoScene();

            var builderHud = Object.FindAnyObjectByType<BuilderHud>();
            var banner = Object.FindAnyObjectByType<HullWarningBanner>();
            Assert.IsNotNull(builderHud, "DemoScene has no BuilderHud.");
            Assert.IsNotNull(banner, "DemoScene has no HullWarningBanner.");

            GameObject builderPanelRoot = GetField<GameObject>(builderHud, "panelRoot");
            GameObject bannerPanelRoot = GetField<GameObject>(banner, "panelRoot");

            Demo.SetState(DemoState.Build);
            yield return null;
            Assert.IsTrue(builderPanelRoot.activeSelf, "builder panel is not active in Build mode.");
            Assert.IsFalse(bannerPanelRoot.activeSelf, "hull banner is active in Build mode.");

            Demo.SetState(DemoState.Fly);
            yield return null;
            Assert.IsFalse(builderPanelRoot.activeSelf, "builder panel is still active in Fly mode.");
            Assert.IsTrue(bannerPanelRoot.activeSelf, "hull banner is not active in Fly mode.");
        }

        [UnityTest]
        public IEnumerator BuilderPanelText_MatchesModel_InBuildMode()
        {
            yield return LoadDemoScene();
            Demo.SetState(DemoState.Build);
            yield return null;

            var builderHud = Object.FindAnyObjectByType<BuilderHud>();
            var controller = Demo.Builder;
            var model = BuilderHudModel.Build(controller.Session, controller.HoverVerdictText);

            var titleText = GetField<TMP_Text>(builderHud, "titleText");
            var totalMassText = GetField<TMP_Text>(builderHud, "totalMassText");
            var blockCountText = GetField<TMP_Text>(builderHud, "blockCountText");
            var stateText = GetField<TMP_Text>(builderHud, "stateText");
            var rows = GetField<TMP_Text[]>(builderHud, "_rows");

            Assert.AreEqual(model.Title, titleText.text);
            Assert.AreEqual(model.TotalMassLine, totalMassText.text);
            Assert.AreEqual(model.BlockCountLine, blockCountText.text);
            Assert.AreEqual(model.StateLine, stateText.text);
            Assert.AreEqual(model.Rows.Count, rows.Length, "row count does not match the palette.");
            for (int i = 0; i < rows.Length; i++)
            {
                Assert.AreEqual(model.Rows[i], rows[i].text, $"row {i} text does not match the model.");
            }
        }

        [UnityTest]
        public IEnumerator StatusPanelAndThrustBarText_MatchesModel_AfterThrusting()
        {
            yield return LoadDemoScene();
            Demo.SetState(DemoState.Fly);
            yield return FixedSteps(0.5f);

            Input.Thrust = 1f;
            yield return FixedSteps(0.3f);
            yield return null; // let LateUpdate copy this frame's telemetry into the views

            var statusView = Object.FindAnyObjectByType<StatusPanelView>();
            Assert.IsNotNull(statusView, "DemoScene has no StatusPanelView.");

            var ship = Player.Ship;
            var expectedStatus = StatusPanelModel.Build(false, Demo.PlayerRenderer.Overlay.ToString(),
                ship.Grid.Mass.Total, ship.Grid.Count, Demo.ActivePowerups());
            var expectedTelemetry = FlightTelemetryModel.Build(ship.Velocity.x, ship.Velocity.y,
                ship.AngularVelocity, ship.ForwardThrottleMean, ship.ReverseThrottleMean, ship.SteerThrottleMean);

            var modeText = GetField<TMP_Text>(statusView, "modeText");
            var speedText = GetField<TMP_Text>(statusView, "speedText");
            Assert.AreEqual(expectedStatus.ModeLine, modeText.text);
            Assert.AreEqual(expectedTelemetry.SpeedLine, speedText.text);

            var thrustBar = GetField<ChannelBar>(statusView, "thrustBar");
            var thrustLabel = GetField<TMP_Text>(thrustBar, "labelText");
            var thrustFillRect = GetField<RectTransform>(thrustBar, "fillRect");
            Assert.AreEqual(expectedTelemetry.Thrust.Label, thrustLabel.text);
            Assert.AreEqual(expectedTelemetry.Thrust.Fill, thrustFillRect.anchorMax.x, 0.001f,
                "the thrust bar's fill anchor does not match the model's fill fraction.");
        }

        [UnityTest]
        public IEnumerator Layout_ActivePanelsDoNotOverlap_AtWideAndShortWindow()
        {
            yield return LoadDemoScene();

            var canvasGo = GameObject.Find("HudCanvas");
            Assert.IsNotNull(canvasGo, "DemoScene has no HudCanvas.");
            var canvas = canvasGo.GetComponent<Canvas>();
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            var canvasRect = canvasGo.GetComponent<RectTransform>();

            var builderHud = Object.FindAnyObjectByType<BuilderHud>();
            var banner = Object.FindAnyObjectByType<HullWarningBanner>();
            RectTransform builderPanel = GetField<GameObject>(builderHud, "panelRoot").GetComponent<RectTransform>();
            RectTransform bannerPanel = GetField<GameObject>(banner, "panelRoot").GetComponent<RectTransform>();
            RectTransform statusPanel = GameObject.Find("StatusPanel").GetComponent<RectTransform>();

            // Screen.SetResolution does not take effect in this headless batchmode
            // host (measured identical world corners with and without it), so a
            // real window resize cannot drive this check. Instead this reproduces
            // CanvasScaler's own ScaleWithScreenSize/MatchWidthOrHeight formula to
            // compute the reference-space canvas extent a given screen size WOULD
            // produce, and feeds that straight into the root canvas RectTransform
            // (switched to WorldSpace so it stops being screen-driven) so every
            // child panel's anchors/sizes lay out exactly as they would for real.
            var originalRenderMode = canvas.renderMode;
            var originalScalerEnabled = scaler.enabled;
            var originalAnchorMin = canvasRect.anchorMin;
            var originalAnchorMax = canvasRect.anchorMax;
            var originalSizeDelta = canvasRect.sizeDelta;
            try
            {
                canvas.renderMode = RenderMode.WorldSpace;
                scaler.enabled = false;
                canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
                canvasRect.anchorMax = new Vector2(0.5f, 0.5f);

                var resolutions = new[] { (1920, 1080), (1920, 480) };
                var modes = new[] { DemoState.Build, DemoState.Fly };

                foreach (var (width, height) in resolutions)
                {
                    canvasRect.sizeDelta = EffectiveCanvasSize(width, height, scaler.referenceResolution,
                        scaler.matchWidthOrHeight);

                    foreach (var mode in modes)
                    {
                        Demo.SetState(mode);
                        yield return null;
                        Canvas.ForceUpdateCanvases();

                        var active = new System.Collections.Generic.List<(string name, RectTransform rect)>
                        {
                            ("StatusPanel", statusPanel),
                        };
                        if (mode == DemoState.Build) active.Add(("BuilderPanel", builderPanel));
                        if (mode == DemoState.Fly) active.Add(("HullWarningBanner", bannerPanel));

                        for (int i = 0; i < active.Count; i++)
                        {
                            for (int j = i + 1; j < active.Count; j++)
                            {
                                AssertNoOverlap(active[i], active[j], width, height, mode);
                            }
                        }
                    }
                }
            }
            finally
            {
                canvas.renderMode = originalRenderMode;
                scaler.enabled = originalScalerEnabled;
                canvasRect.anchorMin = originalAnchorMin;
                canvasRect.anchorMax = originalAnchorMax;
                canvasRect.sizeDelta = originalSizeDelta;
            }
        }

        // CanvasScaler's own ScaleWithScreenSize/MatchWidthOrHeight math: the
        // reference-space size the canvas would end up with for a given real
        // screen size, so the fake window above lays out identically to a real one.
        static Vector2 EffectiveCanvasSize(int screenWidth, int screenHeight, Vector2 referenceResolution, float match)
        {
            float scaleFactor = Mathf.Pow(screenWidth / referenceResolution.x, 1f - match)
                                 * Mathf.Pow(screenHeight / referenceResolution.y, match);
            return new Vector2(screenWidth / scaleFactor, screenHeight / scaleFactor);
        }

        static void AssertNoOverlap((string name, RectTransform rect) a, (string name, RectTransform rect) b,
            int width, int height, DemoState mode)
        {
            var boundsA = WorldBounds(a.rect);
            var boundsB = WorldBounds(b.rect);
            bool overlap = boundsA.xMin < boundsB.xMax && boundsB.xMin < boundsA.xMax
                            && boundsA.yMin < boundsB.yMax && boundsB.yMin < boundsA.yMax;
            Debug.Log($"Layout {width}x{height} {mode}: {a.name} {boundsA}, {b.name} {boundsB}");
            Assert.IsFalse(overlap, $"{a.name} and {b.name} overlap at {width}x{height} in {mode} mode.");
        }

        static Rect WorldBounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float xMin = Mathf.Min(corners[0].x, corners[2].x);
            float xMax = Mathf.Max(corners[0].x, corners[2].x);
            float yMin = Mathf.Min(corners[0].y, corners[2].y);
            float yMax = Mathf.Max(corners[0].y, corners[2].y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
