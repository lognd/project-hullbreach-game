using UnityEngine;
using UnityEngine.UI;
using Hullbreach.Hud;

namespace Hullbreach.Game
{
    // uGUI view over BuilderHudModel (D4): copies model strings into
    // serialized Text references every LateUpdate, no formatting or
    // layout math here. Keeps the old class name and DemoMode.ApplyState's
    // enable/disable contract (D8).
    // frob:doc docs/demo-scene.md#the-ugui-hud-u1
    public sealed class BuilderHud : MonoBehaviour
    {
        [SerializeField] BuilderController controller;

        // Toggled with this component's enabled state: MonoBehaviour.enabled
        // alone does not stop uGUI children from rendering.
        [SerializeField] GameObject panelRoot;

        [SerializeField] Text titleText;

        [SerializeField] RectTransform rowContainer;

        // Cloned once per BlockPalette entry; views never create UI objects
        // at runtime except rows cloned from a template (D4).
        [SerializeField] Text rowTemplate;

        [SerializeField] Text totalMassText;

        [SerializeField] Text blockCountText;

        [SerializeField] Text stateText;

        [SerializeField] Text hoverText;

        Text[] _rows;

        void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        // Matches DemoMode.ApplyState enabling this component.
        void OnEnable()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
        }

        // Matches DemoMode.ApplyState disabling this component.
        void OnDisable()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        void LateUpdate()
        {
            if (controller == null || controller.Session == null) return;

            var model = BuilderHudModel.Build(controller.Session, controller.HoverVerdictText);

            if (titleText != null) titleText.text = model.Title;

            EnsureRowCount(model.Rows.Count);
            // Bounded by _rows.Length, not model.Rows.Count: if rowTemplate/
            // rowContainer are not wired yet, EnsureRowCount leaves _rows
            // empty rather than crashing every frame.
            int rowCount = _rows.Length < model.Rows.Count ? _rows.Length : model.Rows.Count;
            for (int i = 0; i < rowCount; i++)
            {
                _rows[i].text = model.Rows[i];
            }

            if (totalMassText != null) totalMassText.text = model.TotalMassLine;
            if (blockCountText != null) blockCountText.text = model.BlockCountLine;
            if (stateText != null) stateText.text = model.StateLine;

            if (hoverText != null)
            {
                bool hasHover = model.HoverLine != null;
                hoverText.gameObject.SetActive(hasHover);
                if (hasHover) hoverText.text = model.HoverLine;
            }
        }

        // The palette never changes size at runtime, so this only ever grows once.
        void EnsureRowCount(int count)
        {
            if (_rows != null && _rows.Length == count) return;
            if (rowTemplate == null || rowContainer == null)
            {
                _rows = System.Array.Empty<Text>();
                return;
            }

            _rows = new Text[count];
            for (int i = 0; i < count; i++)
            {
                var row = Instantiate(rowTemplate, rowContainer);
                row.gameObject.SetActive(true);
                _rows[i] = row;
            }
        }
    }
}
