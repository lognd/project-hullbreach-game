using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hullbreach.Game;

namespace Hullbreach.Editor
{
    // Builds the HUD's uGUI prefabs and wires them into DemoScene (D6);
    // after the first run, prefabs are owned by hand and this is kept only
    // as a "reset to default layout" tool. Never overwrites an existing
    // prefab unless told to.
    //
    // Adding a panel for U2/U3: write one more AddXxxPanel(GameObject
    // canvasRoot) method following AddBuilderPanel's shape (build the
    // hierarchy, return the panel root) and call it from
    // BuildHudCanvasPrefab alongside the existing call.
    // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
    public static class HudPrefabBuilder
    {
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public const string HudCanvasPrefabPath = "Assets/Prefabs/UI/HudCanvas.prefab";

        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public const string BuilderPanelPrefabPath = "Assets/Prefabs/UI/BuilderPanel.prefab";

        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public const string DemoScenePath = "Assets/Scenes/DemoScene.unity";

        // Menu entry: rebuilds both prefabs (refusing to overwrite either if
        // it already exists) and re-wires DemoScene.
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        [MenuItem("Hullbreach/UI/Rebuild default HUD prefabs")]
        public static void RebuildDefaultHudPrefabsMenuItem()
        {
            Run(force: false);
        }

        // -executeMethod entry point: does not overwrite existing prefabs.
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public static void Build()
        {
            Run(force: false);
        }

        // -executeMethod entry point that DOES overwrite existing prefabs.
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public static void BuildForce()
        {
            Run(force: true);
        }

        // Builds (or rebuilds, if force) the prefabs, then wires DemoScene.
        // frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
        public static void Run(bool force)
        {
            BuildHudCanvasPrefab(force);
            WireDemoScene();
        }

        // Screen Space Overlay canvas at 1920x1080 reference resolution
        // (match 0.5), with a GraphicRaycaster and the nested BuilderPanel.
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
            // U2/U3: call your own AddXxxPanel(canvasGo) here, one call each.

            PrefabUtility.SaveAsPrefabAsset(canvasGo, HudCanvasPrefabPath);
            Object.DestroyImmediate(canvasGo);
            AssetDatabase.SaveAssets();
            Debug.Log($"HudPrefabBuilder: wrote {HudCanvasPrefabPath} and {BuilderPanelPrefabPath}.");
        }

        // Top-left palette panel: VerticalLayoutGroup + ContentSizeFitter,
        // a title, a row template BuilderHud clones per palette entry, and
        // the total-mass/block-count/state/hover labels.
        static GameObject AddBuilderPanel(GameObject canvasRoot)
        {
            var panel = new GameObject("BuilderPanel", typeof(RectTransform));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(canvasRoot.transform, false);
            // Top-left anchored, growing down and right from a 10px inset.
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

        // D2 fallback: headless TMP Essential Resources import did not
        // survive -quit in this environment (AssetDatabase.ImportPackage
        // is asynchronous; the process ended before it wrote anything), so
        // labels use UI.Text + the built-in LegacyRuntime.ttf instead.
        static Text AddLabel(Transform parent, string name, string text)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        // Adds one HudCanvas instance and one EventSystem to DemoScene, and
        // rewires Demo's BuilderHud reference, removing the old one so
        // there is exactly one. Idempotent: running it twice does not
        // duplicate either object.
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
