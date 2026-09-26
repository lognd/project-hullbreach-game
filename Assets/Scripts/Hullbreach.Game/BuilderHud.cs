using UnityEngine;
using TMPro;
using Hullbreach.Hud;

namespace Hullbreach.Game
{
    // uGUI view over BuilderHudModel (D4); see docs/design/ui-port.md for the wiring contract.
    // frob:doc docs/demo-scene.md#the-ugui-hud-u1
    public sealed class BuilderHud : MonoBehaviour
    {
        [SerializeField] BuilderController controller;
        [SerializeField] GameObject panelRoot;
        [SerializeField] TMP_Text titleText;
        [SerializeField] RectTransform rowContainer;
        [SerializeField] TMP_Text rowTemplate;
        [SerializeField] TMP_Text totalMassText;
        [SerializeField] TMP_Text blockCountText;
        [SerializeField] TMP_Text stateText;
        [SerializeField] TMP_Text hoverText;

        TMP_Text[] _rows;

        void Awake()
        {
            rowTemplate.gameObject.SetActive(false);
            BuildRows();
        }

        // Matches DemoMode.ApplyState's enable/disable of this component (D8).
        void OnEnable() => panelRoot.SetActive(true);

        void OnDisable() => panelRoot.SetActive(false);

        void LateUpdate()
        {
            if (controller.Session == null) return;

            var model = BuilderHudModel.Build(controller.Session, controller.HoverVerdictText);

            titleText.text = model.Title;
            for (int i = 0; i < _rows.Length; i++) _rows[i].text = model.Rows[i];
            totalMassText.text = model.TotalMassLine;
            blockCountText.text = model.BlockCountLine;
            stateText.text = model.StateLine;

            bool hasHover = model.HoverLine != null;
            hoverText.gameObject.SetActive(hasHover);
            if (hasHover) hoverText.text = model.HoverLine;
        }

        // The palette is fixed-size, so rows are built once, not grown per frame.
        void BuildRows()
        {
            int count = 0;
            foreach (var _ in Hullbreach.Builder.BlockPalette.All()) count++;

            _rows = new TMP_Text[count];
            for (int i = 0; i < count; i++)
            {
                var row = Instantiate(rowTemplate, rowContainer);
                row.gameObject.SetActive(true);
                _rows[i] = row;
            }
        }
    }
}
