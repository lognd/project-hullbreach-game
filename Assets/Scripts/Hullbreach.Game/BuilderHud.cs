using UnityEngine;
using Hullbreach.Core;
using Hullbreach.Builder;

namespace Hullbreach.Game
{
    /// <summary>
    /// Minimal OnGUI HUD for the builder (S33): lists the palette with mass
    /// and cost, shows total mass and block count, and highlights the current
    /// selection. Deliberately OnGUI, not a Canvas: it needs no scene setup
    /// and is only meant to make the palette/criteria testable by hand.
    /// </summary>
    public sealed class BuilderHud : MonoBehaviour
    {
        /// <summary>The controller whose session this HUD reflects.</summary>
        [SerializeField] BuilderController controller;

        void OnGUI()
        {
            if (controller == null || controller.Session == null) return;
            var session = controller.Session;

            // Clamped to the space DemoMode's bottom-left panel leaves, so
            // the palette and the status panel never draw over each other on
            // a short window (they did at 341 px tall, which is what a
            // batch-mode screenshot run produces).
            float available = Screen.height - DemoMode.StatusPanelHeight(true) - 20f;
            float height = Mathf.Clamp(available, 120f, 400f);
            GUILayout.BeginArea(new Rect(10, 10, 260, height), GUI.skin.box);
            GUILayout.Label("Palette (keys 1-7)");

            foreach (var entry in BlockPalette.All())
            {
                bool selected = entry.TypeId == session.SelectedTypeId;
                string marker = selected ? "> " : "  ";
                GUILayout.Label($"{marker}{entry.Name}  mass {entry.Mass:0.0}  cost {entry.Cost}");
            }

            GUILayout.Space(8);
            GUILayout.Label($"Total mass: {session.TotalMass:0.0}");
            GUILayout.Label($"Block count: {session.BlockCount}");
            GUILayout.Label($"State: {session.State}");
            if (!string.IsNullOrEmpty(controller.HoverVerdictText))
            {
                GUILayout.Label($"Hover: {controller.HoverVerdictText}");
            }
            GUILayout.EndArea();
        }
    }
}
