using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Hullbreach.Hud;

namespace Hullbreach.Game
{
    // uGUI view over a Hullbreach.Hud.ChannelBarValue (D4); see docs for why.
    // frob:doc docs/design/ui-port.md#hullbreachgame-hud-channelbar
    public sealed class ChannelBar : MonoBehaviour
    {
        [SerializeField] TMP_Text labelText;
        [SerializeField] Image trackImage;
        [SerializeField] Image fillImage;
        [SerializeField] RectTransform fillRect;
        [SerializeField] RectTransform centerTick;

        // Sets the label text, the fill color and whether the bar reads a
        // centered -1..1 value (steer) or a 0..1 value (thrust/reverse).
        // frob:doc docs/design/ui-port.md#hullbreachgame-hud-channelbar
        public void Set(ChannelBarValue value)
        {
            labelText.text = value.Label;
            trackImage.color = ToColor(value.TrackColor);
            fillImage.color = ToColor(value.FillColor);
            centerTick.gameObject.SetActive(value.Centered);

            if (value.Centered)
            {
                float half = value.Fill * 0.5f;
                float lo = half >= 0f ? 0.5f : 0.5f + half;
                float hi = half >= 0f ? 0.5f + half : 0.5f;
                fillRect.anchorMin = new Vector2(lo, fillRect.anchorMin.y);
                fillRect.anchorMax = new Vector2(hi, fillRect.anchorMax.y);
            }
            else
            {
                fillRect.anchorMin = new Vector2(0f, fillRect.anchorMin.y);
                fillRect.anchorMax = new Vector2(value.Fill, fillRect.anchorMax.y);
            }
        }

        static Color ToColor(HudColor c) => new Color(c.R, c.G, c.B, c.A);
    }
}
