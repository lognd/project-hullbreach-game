using System.Collections.Generic;
using Hullbreach.Builder;

namespace Hullbreach.Hud
{
    /// <summary>
    /// Everything the builder palette panel shows, as plain strings, built by
    /// a pure function of a <see cref="BuilderSession"/> and the current
    /// hover verdict text (D3). This is the exact text
    /// <c>Assets/Scripts/Hullbreach.Game/BuilderHud.cs</c>'s old
    /// <c>OnGUI</c> produced, so the uGUI view (D4) is a straight copy with
    /// no formatting logic of its own.
    /// </summary>
    public readonly struct BuilderHudModel
    {
        /// <summary>Always "Palette (keys 1-7)": the panel's fixed title line.</summary>
        public readonly string Title;

        /// <summary>One line per palette entry, in <c>BlockPalette.All()</c> order,
        /// e.g. "> Hull  mass 1.0  cost 1" ("> " marks the selected entry, two
        /// spaces otherwise).</summary>
        public readonly IReadOnlyList<string> Rows;

        /// <summary>"Total mass: {0:0.0}".</summary>
        public readonly string TotalMassLine;

        /// <summary>"Block count: {0}".</summary>
        public readonly string BlockCountLine;

        /// <summary>"State: {0}".</summary>
        public readonly string StateLine;

        /// <summary>"Hover: {0}", or null when there is no hover to report
        /// (matches the old OnGUI's "only draw when non-empty" behaviour).</summary>
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

        /// <summary>
        /// Builds the model from a live session and the controller's hover
        /// verdict text. Pure: no side effects, no UnityEngine dependency, so
        /// this is directly unit-testable (D3).
        /// </summary>
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
