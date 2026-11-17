using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hullbreach.Hud
{
    // One filled bar: how full (0..1), its text, and whether it is nearly spent (the view turns it orange).
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public readonly struct BarReadout
    {
        public readonly float Fill;
        public readonly string Label;
        public readonly bool Low;

        public BarReadout(float fill, string label, bool low) { Fill = fill; Label = label; Low = low; }
    }

    // Where the local player is in a match, for the READY button.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public enum ReadyPhase
    {
        Building,   // not ready yet
        Ready,      // pressed READY, waiting for the other player
        Live,       // the match is running
    }

    // Engine-free text for the online build panel: the block hotbar, the budget and size bars and the READY button.
    // The views only draw these, so what a player reads is unit tested.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public static class BuildPanelModel
    {
        // Pre-match build points left, as a bar that drains as the player spends.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static BarReadout Budget(int pointsLeft, int pointsTotal)
        {
            if (pointsTotal <= 0) return new BarReadout(0f, "Points", false);
            int left = Math.Max(0, Math.Min(pointsLeft, pointsTotal));
            return new BarReadout((float)left / pointsTotal, Fmt($"Points {left}/{pointsTotal}"), left * 5 <= pointsTotal);
        }

        // Block count against the size cap, as a bar that fills as the ship grows.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static BarReadout Size(int blocks, int maxBlocks)
        {
            if (maxBlocks <= 0) return new BarReadout(0f, "Blocks", false);
            int n = Math.Max(0, Math.Min(blocks, maxBlocks));
            return new BarReadout((float)n / maxBlocks, Fmt($"Blocks {n}/{maxBlocks}"), n * 10 >= maxBlocks * 9);
        }

        // Second line of a hotbar slot: the spare count once the match runs, the point cost before it.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static string SlotDetail(bool matchRunning, int stock, int cost) =>
            matchRunning ? (stock >= 1 ? Fmt($"x{stock}") : "none left") : Fmt($"{cost} pt");

        // Whether a slot can be used right now (the view greys it out otherwise).
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static bool SlotUsable(bool matchRunning, int stock, int cost, int pointsLeft) =>
            matchRunning ? stock >= 1 : cost <= pointsLeft;

        // The READY button text, and whether it can be pressed.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static string ReadyLabel(ReadyPhase phase, bool opponentPresent, out bool pressable)
        {
            pressable = false;
            switch (phase)
            {
                case ReadyPhase.Live: return string.Empty;
                case ReadyPhase.Ready: return "READY - waiting for opponent";
                default:
                    if (!opponentPresent) return "Waiting for an opponent to join";
                    pressable = true;
                    return "READY";
            }
        }

        // At least this many unspent points before READY asks "ready anyway?" (a cannon's worth: below it nothing useful is left).
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public const int UnspentWarnAt = 5;

        // Whether READY should ask for a second press because a good chunk of the budget is still unspent.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static bool ShouldConfirmReady(int pointsLeft) => pointsLeft >= UnspentWarnAt;

        // READY text that also names the unspent points; once the first press has asked, it reads "READY ANYWAY?".
        // Not a blocker: the second press always goes through.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static string ReadyLabel(ReadyPhase phase, bool opponentPresent, int pointsLeft, bool confirming, out bool pressable)
        {
            string label = ReadyLabel(phase, opponentPresent, out pressable);
            if (phase != ReadyPhase.Building || !pressable || !ShouldConfirmReady(pointsLeft)) return label;
            return confirming ? "READY ANYWAY?" : Fmt($"READY ({pointsLeft} pts unspent)");
        }

        // As above, but a press that warned the design will snap reads "FLY ANYWAY?" (the view shows "Fix it" beside it).
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static string ReadyLabel(ReadyPhase phase, bool opponentPresent, int pointsLeft, bool confirming, bool structureWarned, out bool pressable)
        {
            string label = ReadyLabel(phase, opponentPresent, pointsLeft, confirming, out pressable);
            return phase == ReadyPhase.Building && pressable && confirming && structureWarned ? "FLY ANYWAY?" : label;
        }

        static string Fmt(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
    }

    // Short-lived messages (build refusals, pickup notices) shown one at a time, newest wins, repeats are not stacked.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public sealed class ToastQueue
    {
        readonly List<(string text, float until)> _items = new List<(string, float)>();
        readonly float _seconds;
        readonly int _capacity;

        public ToastQueue(float seconds = 2.5f, int capacity = 3) { _seconds = seconds; _capacity = capacity; }

        // Adds a message at `now`; an identical message already showing is just extended.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public void Push(string text, float now)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].text == text) { _items[i] = (text, now + _seconds); return; }
            if (_items.Count >= _capacity) _items.RemoveAt(0);
            _items.Add((text, now + _seconds));
        }

        // The lines to show at `now`, oldest first; expired ones are dropped.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public string Text(float now)
        {
            _items.RemoveAll(i => i.until <= now);
            return string.Join("\n", _items.ConvertAll(i => i.text));
        }

        // Drops one message early (a status that stopped being true, such as "waiting for your opponent" once the match runs).
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public void Remove(string text) => _items.RemoveAll(i => i.text == text);

        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public void Clear() => _items.Clear();
    }
}
