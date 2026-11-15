using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hullbreach.Hud
{
    // How worried a player should be about a core.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public enum CoreDanger
    {
        Healthy,
        Low,
        Critical,
    }

    // Engine-free text and colour for the match HUD's core-health bars (P0-1). `health` is 0..1 (1 = undamaged).
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public static class CoreHealthModel
    {
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static CoreDanger Danger(float health) => health <= 0.25f ? CoreDanger.Critical : health <= 0.5f ? CoreDanger.Low : CoreDanger.Healthy;

        // "YOUR CORE 72%". A core that is gone reads 0%.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static string Label(string who, float health)
        {
            int pct = (int)Math.Round(Math.Max(0f, Math.Min(1f, health)) * 100f);
            return string.Format(CultureInfo.InvariantCulture, "{0} CORE {1}%", who, pct);
        }

        // Bar colour: the owner's colour while healthy, amber then red as the core fails.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static HudColor Colour(float health, HudColor healthy)
        {
            switch (Danger(health))
            {
                case CoreDanger.Critical: return HudColor.WarningCriticalRed;
                case CoreDanger.Low: return HudColor.WarningStrainYellow;
                default: return healthy;
            }
        }

        // 0..1 pulse for a core in danger (flashes faster when critical); 1 for a healthy one.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static float Pulse(float health, float timeSeconds)
        {
            var danger = Danger(health);
            if (danger == CoreDanger.Healthy) return 1f;
            float hz = danger == CoreDanger.Critical ? 3f : 1.2f;
            return 0.6f + 0.4f * (0.5f + 0.5f * (float)Math.Sin(timeSeconds * hz * 2.0 * Math.PI));
        }
    }

    // One line of the event feed as the view draws it.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public readonly struct FeedLine
    {
        public readonly string Text;
        public readonly HudColor Colour;
        public readonly float Alpha;

        public FeedLine(string text, HudColor colour, float alpha) { Text = text; Colour = colour; Alpha = alpha; }
    }

    // Short-lived match event lines ("ENEMY CORE HIT", "You lost a block"): newest last, a repeat within a second is
    // folded into "x3", and each fades over its final second.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public sealed class EventFeedModel
    {
        struct Entry { public string Base; public int Count; public HudColor Colour; public float Born, Last; }

        readonly List<Entry> _entries = new List<Entry>();
        readonly float _seconds;
        readonly int _max;

        public EventFeedModel(float seconds = 6f, int maxLines = 5) { _seconds = seconds; _max = maxLines; }

        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public void Push(string text, HudColor colour, float now)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.Base != text || now - e.Last > 1f) continue;
                e.Count++; e.Last = now; e.Born = now;
                _entries[i] = e;
                return;
            }
            if (_entries.Count >= _max) _entries.RemoveAt(0);
            _entries.Add(new Entry { Base = text, Count = 1, Colour = colour, Born = now, Last = now });
        }

        // The lines to draw at `now`, oldest first. Expired ones are dropped.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public void Visible(float now, List<FeedLine> into)
        {
            into.Clear();
            _entries.RemoveAll(e => now - e.Born >= _seconds);
            foreach (var e in _entries)
            {
                float left = _seconds - (now - e.Born);
                string text = e.Count > 1 ? string.Format(CultureInfo.InvariantCulture, "{0} x{1}", e.Base, e.Count) : e.Base;
                into.Add(new FeedLine(text, e.Colour, left >= 1f ? 1f : Math.Max(0f, left)));
            }
        }

        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public void Clear() => _entries.Clear();
    }
}
