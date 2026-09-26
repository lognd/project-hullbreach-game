using TMPro;
using UnityEngine;
using Hullbreach.Hud;

namespace Hullbreach.Game
{
    // uGUI view over HullWarningModel (D4): top-center, shown only in Fly.
    // frob:doc docs/demo-scene.md#the-ugui-hud-u1-u3
    public sealed class HullWarningBanner : MonoBehaviour
    {
        [SerializeField] DemoMode demoMode;
        [SerializeField] GameObject panelRoot;
        [SerializeField] TMP_Text headlineText;
        [SerializeField] TMP_Text detailText;
        [SerializeField] TMP_Text hintText;

        void LateUpdate()
        {
            bool fly = demoMode.State == DemoState.Fly;
            panelRoot.SetActive(fly);
            if (!fly) return;

            var model = HullWarningModel.Build(demoMode.Warning, demoMode.MaxStressRatio,
                demoMode.CriticalBlockCount, demoMode.CriticalBlockName, Time.unscaledTime);

            var color = new Color(model.Color.R, model.Color.G, model.Color.B, model.Color.A);
            headlineText.text = model.Headline;
            headlineText.color = color;

            detailText.gameObject.SetActive(model.ShowDetails);
            hintText.gameObject.SetActive(model.ShowDetails);
            if (model.ShowDetails)
            {
                detailText.text = model.Detail;
                detailText.color = color;
                hintText.text = model.Hint;
                hintText.color = color;
            }
        }
    }
}
