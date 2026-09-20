using UnityEngine;
using Hullbreach.Core;
using Hullbreach.Builder;

namespace Hullbreach.Game
{
    /// <summary>
    /// Minimal OnGUI HUD for the builder (S33): lists the palette with mass
    /// and cost, shows total mass and block count, and highlights the current
    /// selection. Deliberately OnGUI, not a Canvas -- it needs no scene setup
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

            GUILayout.BeginArea(new Rect(10, 10, 260, 400), GUI.skin.box);
            GUILayout.Label("Palette (keys 1-5)");

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
            GUILayout.EndArea();
        }
    }
}
