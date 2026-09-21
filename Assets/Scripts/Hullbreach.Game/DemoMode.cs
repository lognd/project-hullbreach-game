using UnityEngine;
using Hullbreach.World;

namespace Hullbreach.Game
{
    /// <summary>Which of the two demo scene states is active.</summary>
    public enum DemoState { Build, Fly }

    /// <summary>
    /// Top-level demo scene conductor: toggles between Build (frozen ship,
    /// BuilderController editing the live grid) and Fly (physics on, WASD +
    /// arrows + Space + O + R), and draws the always-on OnGUI status panel.
    /// Everything it touches (ShipController.InputEnabled, Rigidbody2D.
    /// simulated, BuilderController.enabled) already exists on those
    /// components; this class only orchestrates the switch.
    /// </summary>
    public sealed class DemoMode : MonoBehaviour
    {
        [SerializeField] ShipController playerShip;
        [SerializeField] Rigidbody2D playerBody;
        [SerializeField] BuilderController builder;
        [SerializeField] BuilderHud builderHud;
        [SerializeField] ShipRenderer playerRenderer;
        [SerializeField] ShipStructure playerStructure;

        /// <summary>When set, the R key resets the player onto a preset
        /// circular orbit (orbitStartPosition around orbitBodyIndex) instead
        /// of dead rest at the origin. Off by default so scenes without a
        /// GravityWorld (RocketScene) behave exactly as before.</summary>
        [SerializeField] bool startInOrbit = false;

        /// <summary>World-space position the R key resets the player to
        /// when startInOrbit is set; the orbital velocity is computed from
        /// this position, not authored separately, so moving the start point
        /// in the Inspector can never leave a mismatched velocity behind.</summary>
        [SerializeField] Vector2 orbitStartPosition = Vector2.zero;

        /// <summary>Index (in GravityWorld's planet list / add order) of the
        /// body the orbit start position orbits.</summary>
        [SerializeField] int orbitBodyIndex = 0;

        /// <summary>Current mode, Build until the player presses Tab.</summary>
        public DemoState State { get; private set; } = DemoState.Build;

        void Awake()
        {
            if (playerShip == null) Debug.LogError("DemoMode requires a player ShipController.");
            if (playerBody == null && playerShip != null) playerBody = playerShip.GetComponent<Rigidbody2D>();
            if (builder == null) Debug.LogError("DemoMode requires a BuilderController.");
            if (playerRenderer == null && playerShip != null) playerRenderer = playerShip.GetComponent<ShipRenderer>();
            if (playerStructure == null && playerShip != null) playerStructure = playerShip.GetComponent<ShipStructure>();
        }

        void Start()
        {
            ApplyState();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                State = State == DemoState.Build ? DemoState.Fly : DemoState.Build;
                ApplyState();
            }

            if (State == DemoState.Fly)
            {
                if (Input.GetKeyDown(KeyCode.Space) && playerShip != null) playerShip.RequestFire();

                if (Input.GetKeyDown(KeyCode.O) && playerRenderer != null)
                {
                    playerRenderer.Overlay = NextOverlay(playerRenderer.Overlay);
                }

                if (Input.GetKeyDown(KeyCode.R) && playerShip != null)
                {
                    var field = GravityWorld.Field;
                    if (startInOrbit && field != null)
                    {
                        var worldPos = new Unity.Mathematics.float2(orbitStartPosition.x, orbitStartPosition.y);
                        var orbitVelocity = OrbitHelper.CircularOrbitVelocity(field, orbitBodyIndex, worldPos);
                        playerShip.ResetTo(orbitStartPosition, new Vector2(orbitVelocity.x, orbitVelocity.y));
                    }
                    else
                    {
                        playerShip.ResetToOrigin();
                    }
                }
            }
        }

        static OverlayMode NextOverlay(OverlayMode current) => current switch
        {
            OverlayMode.None => OverlayMode.Stress,
            OverlayMode.Stress => OverlayMode.LoadBearing,
            OverlayMode.LoadBearing => OverlayMode.Damage,
            OverlayMode.Damage => OverlayMode.Buckling,
            _ => OverlayMode.None,
        };

        void ApplyState()
        {
            bool building = State == DemoState.Build;

            if (playerShip != null) playerShip.InputEnabled = !building;
            if (playerBody != null) playerBody.simulated = !building;
            if (builder != null) builder.enabled = building;
            if (builderHud != null) builderHud.enabled = building;
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, Screen.height - 170, 420, 160), GUI.skin.box);
            GUILayout.Label($"Mode: {State}  (Tab to switch)");

            if (State == DemoState.Build)
            {
                GUILayout.Label("Left click: place   Right click: remove");
                GUILayout.Label("Ctrl+Z: undo   Ctrl+Shift+Z: redo   Esc: cancel orientation");
                GUILayout.Label("Keys 1-7: select palette entry");
            }
            else
            {
                GUILayout.Label("W/S or Up/Down: thrust/reverse   A/D or Left/Right: steer");
                GUILayout.Label("Space: fire   O: cycle overlay   R: reset to origin");
                string overlay = playerRenderer != null ? playerRenderer.Overlay.ToString() : "n/a";
                GUILayout.Label($"Overlay: {overlay}");
            }

            if (playerShip != null && playerShip.Ship != null)
            {
                var ship = playerShip.Ship;
                GUILayout.Label($"Mass: {ship.Grid.Mass.Total:0.0}   Blocks: {ship.Grid.Count}");
                if (State == DemoState.Fly)
                {
                    float speed = Mathf.Sqrt(ship.Velocity.x * ship.Velocity.x + ship.Velocity.y * ship.Velocity.y);
                    GUILayout.Label($"Speed: {speed:0.0}   Angular speed: {Mathf.Abs(ship.AngularVelocity):0.00}");

                    if (playerStructure != null)
                    {
                        float clf = playerStructure.Solver.CriticalLoadFactor;
                        string clfText = float.IsInfinity(clf) ? "inf" : clf.ToString("0.00");
                        GUILayout.Label($"Critical load factor: {clfText}");
                    }
                }
            }
            GUILayout.EndArea();
        }
    }
}
