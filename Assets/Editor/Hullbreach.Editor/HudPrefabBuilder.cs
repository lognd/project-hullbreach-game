using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Hullbreach.Game;
using Hullbreach.Hud;

namespace Hullbreach.Editor
{
    // Builds the HUD's uGUI prefabs and wires DemoScene (D6); see
    // docs/design/ui-port.md for the "adding a panel" how-to.
    // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
    public static class HudPrefabBuilder
    {
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public const string HudCanvasPrefabPath = "Assets/Prefabs/UI/HudCanvas.prefab";

        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public const string BuilderPanelPrefabPath = "Assets/Prefabs/UI/BuilderPanel.prefab";

        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public const string DemoScenePath = "Assets/Scenes/DemoScene.unity";

        // Standalone widget prefab (U2), nested three times under StatusPanel (U3).
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public const string ChannelBarPrefabPath = "Assets/Prefabs/UI/ChannelBar.prefab";

        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public const string StatusPanelPrefabPath = "Assets/Prefabs/UI/StatusPanel.prefab";

        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public const string HullWarningBannerPrefabPath = "Assets/Prefabs/UI/HullWarningBanner.prefab";

        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        [MenuItem("Hullbreach/UI/Rebuild default HUD prefabs")]
        public static void RebuildDefaultHudPrefabsMenuItem() => Run(force: false);

        // -executeMethod entry point: does not overwrite existing prefabs.
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public static void Build() => Run(force: false);

        // -executeMethod entry point that DOES overwrite existing prefabs.
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public static void BuildForce() => Run(force: true);

        // -executeMethod entry point for the standalone ChannelBar prefab (U2).
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public static void BuildChannelBar() => BuildChannelBarPrefab(force: false);

        // -executeMethod entry point that DOES overwrite the ChannelBar prefab.
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public static void BuildChannelBarForce() => BuildChannelBarPrefab(force: true);

        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public static void Run(bool force)
        {
            BuildHudCanvasPrefab(force);
            WireDemoScene();
        }

        // Screen Space Overlay canvas, 1920x1080 reference resolution, match 0.5 (D5).
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public static void BuildHudCanvasPrefab(bool force)
        {
            if (!force && (File.Exists(BuilderPanelPrefabPath) || File.Exists(HudCanvasPrefabPath)))
            {
                Debug.Log("HudPrefabBuilder: prefabs already exist, skipping (pass force=true to overwrite).");
                return;
            }

            Directory.CreateDirectory("Assets/Prefabs/UI");

            var canvasGo = new GameObject("HudCanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            AddBuilderPanel(canvasGo);
            AddStatusPanel(canvasGo);
            AddHullWarningBanner(canvasGo);

            PrefabUtility.SaveAsPrefabAsset(canvasGo, HudCanvasPrefabPath);
            Object.DestroyImmediate(canvasGo);
            AssetDatabase.SaveAssets();
            Debug.Log($"HudPrefabBuilder: wrote {HudCanvasPrefabPath}, {BuilderPanelPrefabPath}, {StatusPanelPrefabPath} and {HullWarningBannerPrefabPath}.");
        }

        // Top-left palette panel: see docs/design/ui-port.md for the layout contract.
        static GameObject AddBuilderPanel(GameObject canvasRoot)
        {
            var panel = new GameObject("BuilderPanel", typeof(RectTransform));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(canvasRoot.transform, false);
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(10f, -10f);
            panelRect.sizeDelta = new Vector2(260f, 400f);

            var image = panel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = AddLabel(panel.transform, "Title", "Palette (keys 1-7)");

            var rowTemplateGo = AddLabel(panel.transform, "RowTemplate", "  Hull  mass 1.0  cost 1");
            rowTemplateGo.gameObject.SetActive(false);

            var totalMass = AddLabel(panel.transform, "TotalMassText", "Total mass: 0.0");
            var blockCount = AddLabel(panel.transform, "BlockCountText", "Block count: 0");
            var state = AddLabel(panel.transform, "StateText", "State: Idle");
            var hover = AddLabel(panel.transform, "HoverText", "Hover: ");

            var builderHud = canvasRoot.AddComponent<BuilderHud>();
            var so = new SerializedObject(builderHud);
            so.FindProperty("panelRoot").objectReferenceValue = panel;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("rowContainer").objectReferenceValue = panelRect;
            so.FindProperty("rowTemplate").objectReferenceValue = rowTemplateGo;
            so.FindProperty("totalMassText").objectReferenceValue = totalMass;
            so.FindProperty("blockCountText").objectReferenceValue = blockCount;
            so.FindProperty("stateText").objectReferenceValue = state;
            so.FindProperty("hoverText").objectReferenceValue = hover;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(panel, BuilderPanelPrefabPath);
            return panel;
        }

        // Bottom-left status panel: see docs/design/ui-port.md for the layout contract.
        static GameObject AddStatusPanel(GameObject canvasRoot)
        {
            var panel = new GameObject("StatusPanel", typeof(RectTransform));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(canvasRoot.transform, false);
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(10f, 10f);
            panelRect.sizeDelta = new Vector2(440f, 240f);

            var image = panel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var modeText = AddLabel(panel.transform, "ModeText", "Mode: Build  (Tab to switch)");

            var controlLine1 = AddLabel(panel.transform, "ControlLine1", "Left click: place   Right click: remove");
            var controlLine2 = AddLabel(panel.transform, "ControlLine2", "Ctrl+Z: undo   Ctrl+Shift+Z: redo   Esc: cancel orientation");
            var controlLine3 = AddLabel(panel.transform, "ControlLine3", "Keys 1-7: select palette entry");

            var overlayText = AddLabel(panel.transform, "OverlayText", "Overlay: None");
            var massBlocksText = AddLabel(panel.transform, "MassBlocksText", "Mass: 0.0   Blocks: 0");

            var flightOnlyRoot = new GameObject("FlightOnly", typeof(RectTransform));
            var flightRect = flightOnlyRoot.GetComponent<RectTransform>();
            flightRect.SetParent(panel.transform, false);
            var flightLayout = flightOnlyRoot.AddComponent<VerticalLayoutGroup>();
            flightLayout.spacing = 2f;
            flightLayout.childAlignment = TextAnchor.UpperLeft;
            flightLayout.childControlWidth = true;
            flightLayout.childControlHeight = true;
            flightLayout.childForceExpandWidth = true;
            flightLayout.childForceExpandHeight = false;
            var flightFitter = flightOnlyRoot.AddComponent<ContentSizeFitter>();
            flightFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var speedText = AddLabel(flightOnlyRoot.transform, "SpeedText", "Speed: 0.0   Angular speed: 0.00");

            var channelBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChannelBarPrefabPath);
            var thrustBarGo = (GameObject)PrefabUtility.InstantiatePrefab(channelBarPrefab, flightOnlyRoot.transform);
            thrustBarGo.name = "ThrustBar";
            var reverseBarGo = (GameObject)PrefabUtility.InstantiatePrefab(channelBarPrefab, flightOnlyRoot.transform);
            reverseBarGo.name = "ReverseBar";
            var steerBarGo = (GameObject)PrefabUtility.InstantiatePrefab(channelBarPrefab, flightOnlyRoot.transform);
            steerBarGo.name = "SteerBar";

            var powerupContainer = new GameObject("PowerupContainer", typeof(RectTransform));
            var powerupRect = powerupContainer.GetComponent<RectTransform>();
            powerupRect.SetParent(flightOnlyRoot.transform, false);
            var powerupLayout = powerupContainer.AddComponent<VerticalLayoutGroup>();
            powerupLayout.spacing = 2f;
            powerupLayout.childAlignment = TextAnchor.UpperLeft;
            powerupLayout.childControlWidth = true;
            powerupLayout.childControlHeight = true;
            powerupLayout.childForceExpandWidth = true;
            powerupLayout.childForceExpandHeight = false;
            var powerupFitter = powerupContainer.AddComponent<ContentSizeFitter>();
            powerupFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var powerupRowTemplate = AddLabel(powerupRect, "PowerupRowTemplate", "Cannon (2,0): Gravity gun 7.3 s");
            powerupRowTemplate.gameObject.SetActive(false);

            // demoMode is wired later by WireDemoScene: this prefab is built
            // standalone, before DemoScene's Demo object exists to point at.
            var view = canvasRoot.AddComponent<StatusPanelView>();
            var so = new SerializedObject(view);
            so.FindProperty("modeText").objectReferenceValue = modeText;
            var controlLines = so.FindProperty("controlLineTexts");
            controlLines.arraySize = 3;
            controlLines.GetArrayElementAtIndex(0).objectReferenceValue = controlLine1;
            controlLines.GetArrayElementAtIndex(1).objectReferenceValue = controlLine2;
            controlLines.GetArrayElementAtIndex(2).objectReferenceValue = controlLine3;
            so.FindProperty("overlayText").objectReferenceValue = overlayText;
            so.FindProperty("massBlocksText").objectReferenceValue = massBlocksText;
            so.FindProperty("flightOnlyRoot").objectReferenceValue = flightOnlyRoot;
            so.FindProperty("speedText").objectReferenceValue = speedText;
            so.FindProperty("thrustBar").objectReferenceValue = thrustBarGo.GetComponent<ChannelBar>();
            so.FindProperty("reverseBar").objectReferenceValue = reverseBarGo.GetComponent<ChannelBar>();
            so.FindProperty("steerBar").objectReferenceValue = steerBarGo.GetComponent<ChannelBar>();
            so.FindProperty("powerupContainer").objectReferenceValue = powerupRect;
            so.FindProperty("powerupRowTemplate").objectReferenceValue = powerupRowTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(panel, StatusPanelPrefabPath);
            return panel;
        }

        // Top-center hull warning banner, shown only in Fly: see docs/design/ui-port.md.
        static GameObject AddHullWarningBanner(GameObject canvasRoot)
        {
            var panel = new GameObject("HullWarningBanner", typeof(RectTransform));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(canvasRoot.transform, false);
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -10f);
            panelRect.sizeDelta = new Vector2(340f, 62f);

            var image = panel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var headline = AddLabel(panel.transform, "HeadlineText", "Hull: OK   (max ratio 0.00)");
            headline.alignment = TextAlignmentOptions.Center;
            var detail = AddLabel(panel.transform, "DetailText", "0 block(s) in the red, worst: Hull");
            detail.alignment = TextAlignmentOptions.Center;
            var hint = AddLabel(panel.transform, "HintText", "ease off thrust");
            hint.alignment = TextAlignmentOptions.Center;

            // demoMode is wired later by WireDemoScene; see AddStatusPanel.
            var view = canvasRoot.AddComponent<HullWarningBanner>();
            var so = new SerializedObject(view);
            so.FindProperty("panelRoot").objectReferenceValue = panel;
            so.FindProperty("headlineText").objectReferenceValue = headline;
            so.FindProperty("detailText").objectReferenceValue = detail;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(panel, HullWarningBannerPrefabPath);
            return panel;
        }

        // Builds the reusable labelled-fill widget as its own top-level prefab
        // (U2); U3 nests three instances under the status panel.
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public static void BuildChannelBarPrefab(bool force)
        {
            if (!force && File.Exists(ChannelBarPrefabPath))
            {
                Debug.Log("HudPrefabBuilder: ChannelBar prefab already exists, skipping (pass force=true to overwrite).");
                return;
            }

            Directory.CreateDirectory("Assets/Prefabs/UI");

            var root = new GameObject("ChannelBar", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(350f, 20f);

            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var label = AddLabel(root.transform, "Label", "Thrust  0%");
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 110f;

            var track = new GameObject("Track", typeof(RectTransform));
            var trackRect = track.GetComponent<RectTransform>();
            trackRect.SetParent(root.transform, false);
            var trackLayout = track.AddComponent<LayoutElement>();
            trackLayout.flexibleWidth = 1f;
            trackLayout.preferredHeight = 14f;
            var trackImage = track.AddComponent<Image>();
            trackImage.color = new Color(HudColor.TrackDark.R, HudColor.TrackDark.G, HudColor.TrackDark.B, HudColor.TrackDark.A);

            var fill = new GameObject("Fill", typeof(RectTransform));
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.SetParent(trackRect, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(HudColor.ThrustRed.R, HudColor.ThrustRed.G, HudColor.ThrustRed.B, HudColor.ThrustRed.A);

            var tick = new GameObject("CenterTick", typeof(RectTransform));
            var tickRect = tick.GetComponent<RectTransform>();
            tickRect.SetParent(trackRect, false);
            tickRect.anchorMin = new Vector2(0.5f, 0f);
            tickRect.anchorMax = new Vector2(0.5f, 1f);
            tickRect.sizeDelta = new Vector2(2f, 0f);
            var tickImage = tick.AddComponent<Image>();
            var tickColor = HudColor.CenterTickGrey;
            tickImage.color = new Color(tickColor.R, tickColor.G, tickColor.B, tickColor.A);
            tick.SetActive(false);

            var channelBar = root.AddComponent<ChannelBar>();
            var so = new SerializedObject(channelBar);
            so.FindProperty("labelText").objectReferenceValue = label;
            so.FindProperty("trackImage").objectReferenceValue = trackImage;
            so.FindProperty("fillImage").objectReferenceValue = fillImage;
            so.FindProperty("fillRect").objectReferenceValue = fillRect;
            so.FindProperty("centerTick").objectReferenceValue = tickRect;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ChannelBarPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"HudPrefabBuilder: wrote {ChannelBarPrefabPath}.");
        }

        static TMP_Text AddLabel(Transform parent, string name, string text)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 18;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        // Adds one HudCanvas instance and one EventSystem to DemoScene; idempotent.
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public static void WireDemoScene()
        {
            var scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                SceneManager.MoveGameObjectToScene(es, scene);
            }

            var existingCanvas = GameObject.Find("HudCanvas");
            GameObject canvasInstance;
            if (existingCanvas == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudCanvasPrefabPath);
                canvasInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                canvasInstance.name = "HudCanvas";
            }
            else
            {
                canvasInstance = existingCanvas;
            }

            var newBuilderHud = canvasInstance.GetComponentInChildren<BuilderHud>(true);
            var statusPanelView = canvasInstance.GetComponentInChildren<StatusPanelView>(true);
            var hullWarningBanner = canvasInstance.GetComponentInChildren<HullWarningBanner>(true);

            var demoGo = GameObject.Find("Demo");
            if (demoGo != null)
            {
                var demoMode = demoGo.GetComponent<DemoMode>();
                var oldBuilderHud = demoGo.GetComponent<BuilderHud>();

                if (demoMode != null && newBuilderHud != null)
                {
                    var so = new SerializedObject(demoMode);
                    so.FindProperty("builderHud").objectReferenceValue = newBuilderHud;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                if (demoMode != null && statusPanelView != null)
                {
                    var statusSo = new SerializedObject(statusPanelView);
                    statusSo.FindProperty("demoMode").objectReferenceValue = demoMode;
                    statusSo.ApplyModifiedPropertiesWithoutUndo();
                }

                if (demoMode != null && hullWarningBanner != null)
                {
                    var bannerSo = new SerializedObject(hullWarningBanner);
                    bannerSo.FindProperty("demoMode").objectReferenceValue = demoMode;
                    bannerSo.ApplyModifiedPropertiesWithoutUndo();
                }

                var playerShipGo = GameObject.Find("PlayerShip");
                var builderController = playerShipGo != null
                    ? playerShipGo.GetComponent<Hullbreach.Game.BuilderController>()
                    : null;
                if (newBuilderHud != null && builderController != null)
                {
                    var hudSo = new SerializedObject(newBuilderHud);
                    hudSo.FindProperty("controller").objectReferenceValue = builderController;
                    hudSo.ApplyModifiedPropertiesWithoutUndo();
                }

                if (oldBuilderHud != null)
                {
                    Object.DestroyImmediate(oldBuilderHud);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("HudPrefabBuilder: wired HudCanvas + EventSystem into DemoScene.");
        }
    }
}
