using System.Collections.Generic;
using Hullbreach.Builder;
using Hullbreach.Core;

namespace Hullbreach.Hud
{
    // Pure function of a BuilderSession (D3): produces the exact text the
    // old IMGUI HUD drew, so the uGUI view has no formatting of its own.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public readonly struct BuilderHudModel
    {
        public readonly string Title;

        // One line per palette entry, in BlockPalette.All() order.
        public readonly IReadOnlyList<string> Rows;

        public readonly string TotalMassLine;

        public readonly string BlockCountLine;

        public readonly string StateLine;

        // Null when there is no hover to report (the old IMGUI HUD skipped the line entirely).
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
        // session must be non-null (a null session is a caller bug and throws ArgumentNullException).
        // Title advertises keys 1..min(9, BlockTypes.Count), the range BuilderController binds.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static BuilderHudModel Build(BuilderSession session, string hoverVerdictText)
        {
            if (session == null) throw new System.ArgumentNullException(nameof(session));

            var rows = new List<string>();
            foreach (var entry in BlockPalette.All())
            {
                bool selected = entry.TypeId == session.SelectedTypeId;
                string marker = selected ? "> " : "  ";
                rows.Add(System.FormattableString.Invariant($"{marker}{entry.Name}  mass {entry.Mass:0.0}  cost {entry.Cost}"));
            }

            string hoverLine = string.IsNullOrEmpty(hoverVerdictText)
                ? null
                : $"Hover: {hoverVerdictText}";

            return new BuilderHudModel(
                System.FormattableString.Invariant($"Palette (keys 1-{System.Math.Min(9, BlockTypes.Count)})"),
                rows,
                System.FormattableString.Invariant($"Total mass: {session.TotalMass:0.0}"),
                System.FormattableString.Invariant($"Block count: {session.BlockCount}"),
                $"State: {session.State}",
                hoverLine);
        }
    }
}
