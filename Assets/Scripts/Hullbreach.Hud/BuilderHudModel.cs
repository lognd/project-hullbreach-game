using System.Collections.Generic;
using Hullbreach.Builder;

namespace Hullbreach.Hud
{
    // Pure function of a BuilderSession (D3): produces the exact text the
    // old BuilderHud.OnGUI drew, so the uGUI view has no formatting of its own.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public readonly struct BuilderHudModel
    {
        // Always "Palette (keys 1-7)".
        public readonly string Title;

        // One line per palette entry, in BlockPalette.All() order.
        public readonly IReadOnlyList<string> Rows;

        public readonly string TotalMassLine;

        public readonly string BlockCountLine;

        public readonly string StateLine;

        // Null when there is no hover to report (old OnGUI skipped the line entirely).
        public readonly string HoverLine;

        BuilderHudModel(string title, IReadOnlyList<string> rows, string totalMassLine,
            string blockCountLine, string stateLine, string hoverLine)
        {
            Title = title;
            Rows = rows;
            TotalMassLine = totalMassLine;
            BlockCountLine = blockCountLine;
            StateLine = stateLine;
            HoverLine = hoverLine;
        }

        // Pure: no side effects, no UnityEngine dependency, directly unit-testable (D3).
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static BuilderHudModel Build(BuilderSession session, string hoverVerdictText)
        {
            var rows = new List<string>();
            foreach (var entry in BlockPalette.All())
            {
                bool selected = entry.TypeId == session.SelectedTypeId;
                string marker = selected ? "> " : "  ";
                rows.Add($"{marker}{entry.Name}  mass {entry.Mass:0.0}  cost {entry.Cost}");
            }

            string hoverLine = string.IsNullOrEmpty(hoverVerdictText)
                ? null
                : $"Hover: {hoverVerdictText}";

            return new BuilderHudModel(
                "Palette (keys 1-7)",
                rows,
                $"Total mass: {session.TotalMass:0.0}",
                $"Block count: {session.BlockCount}",
                $"State: {session.State}",
                hoverLine);
        }
    }
}
