using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Hud
{
    // One active-powerup row's plain inputs (D3): the view/DemoMode gathers
    // these from a ShipBody so this assembly needs no Hullbreach.Ship reference.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public readonly struct ActivePowerup
    {
        public readonly string TypeName;

        public readonly int X;

        public readonly int Y;

        public readonly byte TypeId;

        public readonly byte Variant;

        public readonly float TimeLeft;

        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public ActivePowerup(string typeName, int x, int y, byte typeId, byte variant, float timeLeft)
        {
            TypeName = typeName;
            X = x;
            Y = y;
            TypeId = typeId;
            Variant = variant;
            TimeLeft = timeLeft;
        }
    }

    // Pure function of demo/ship state (D3); see docs for details.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public readonly struct StatusPanelModel
    {
        public readonly string ModeLine;

        // One or more lines of per-mode control hints (Build: 3, Fly: 2).
        public readonly IReadOnlyList<string> ControlLines;

        // Null in Build (only shown in Fly, matching the old drawing).
        public readonly string OverlayLine;

        public readonly string MassBlocksLine;

        public readonly IReadOnlyList<string> PowerupLines;

        StatusPanelModel(string modeLine, IReadOnlyList<string> controlLines, string overlayLine,
            string massBlocksLine, IReadOnlyList<string> powerupLines)
        {
            ModeLine = modeLine;
            ControlLines = controlLines;
            OverlayLine = overlayLine;
            MassBlocksLine = massBlocksLine;
            PowerupLines = powerupLines;
        }

        // Human-readable name for a (TypeId, variant) pair, moved verbatim
        // from DemoMode.VariantLabel (D3): falls back to a generic "variant N".
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static string VariantLabel(byte typeId, byte variant)
        {
            if (typeId == BlockTypes.Cannon && variant == 1) return "Gravity gun";
            if (typeId == BlockTypes.Cannon && variant == 2) return "Anti-gravity gun";
            if (typeId == BlockTypes.Thruster && variant == 1) return "Seeking thruster";
            return $"variant {variant}";
        }

        // Pure: no side effects, no UnityEngine/Hullbreach.Ship dependency (D3).
        // Format strings copied verbatim from DemoMode.DrawStatusPanel/DrawActivePowerups.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static StatusPanelModel Build(bool building, string overlayName, float mass, int blockCount,
            IReadOnlyList<ActivePowerup> powerups)
        {
            string modeLine = $"Mode: {(building ? "Build" : "Fly")}  (Tab to switch)";

            List<string> controls;
            string overlayLine = null;
            if (building)
            {
                controls = new List<string>
                {
                    "Left click: place   Right click: remove",
                    "Ctrl+Z: undo   Ctrl+Shift+Z: redo   Esc: cancel orientation",
                    "Keys 1-7: select palette entry",
                };
            }
            else
            {
                controls = new List<string>
                {
                    "W/S or Up/Down: thrust/reverse   A/D or Left/Right: steer",
                    "Space: fire   O: cycle overlay   R: reset to start",
                };
                overlayLine = $"Overlay: {overlayName}";
            }

            string massBlocksLine = $"Mass: {mass:0.0}   Blocks: {blockCount}";

            var powerupLines = new List<string>();
            if (powerups != null)
            {
                foreach (var p in powerups)
                {
                    string label = VariantLabel(p.TypeId, p.Variant);
                    powerupLines.Add($"{p.TypeName} ({p.X},{p.Y}): {label} {p.TimeLeft:0.0} s");
                }
            }

            return new StatusPanelModel(modeLine, controls, overlayLine, massBlocksLine, powerupLines);
        }
    }
}
