using UnityEngine;
using Hullbreach.Core;
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

        /// <summary>
        /// Where player intent comes from. Defaults to the legacy Input
        /// Manager bindings; a play-mode test swaps in a ScriptedDemoInput.
        /// Assigning this also pushes the same source onto the player's
        /// ShipController, so a test only has to wire one object.
        /// </summary>
        public IDemoInput InputSource
        {
            get => _inputSource;
            set
            {
                _inputSource = value ?? LegacyDemoInput.Instance;
                if (playerShip != null) playerShip.InputSource = _inputSource;
            }
        }

        IDemoInput _inputSource = LegacyDemoInput.Instance;

        /// <summary>The player's ShipController, exposed so play-mode tests
        /// (and any future HUD) can reach the live ship without a scene
        /// search that depends on GameObject names.</summary>
        public ShipController PlayerShip => playerShip;

        /// <summary>The player's ShipRenderer; see <see cref="PlayerShip"/>.</summary>
        public ShipRenderer PlayerRenderer => playerRenderer;

        /// <summary>The player's ShipStructure; see <see cref="PlayerShip"/>.</summary>
        public ShipStructure PlayerStructure => playerStructure;

        /// <summary>The scene's BuilderController; see <see cref="PlayerShip"/>.</summary>
        public BuilderController Builder => builder;

        /// <summary>Where R sends the ship when startInOrbit is set.</summary>
        public Vector2 OrbitStartPosition => orbitStartPosition;

        /// <summary>Whether this scene starts the player on a circular orbit.</summary>
        public bool StartsInOrbit => startInOrbit;

        /// <summary>
        /// Switches mode and applies it. Public so a play-mode test can drive
        /// the toggle without synthesising a Tab key press.
        /// </summary>
        public void SetState(DemoState state)
        {
            State = state;
            ApplyState();
        }

        /// <summary>Toggles Build and Fly, exactly as pressing Tab does.</summary>
        public void ToggleState() => SetState(State == DemoState.Build ? DemoState.Fly : DemoState.Build);

        /// <summary>
        /// Puts the player back on the start state: the preset circular orbit
        /// when startInOrbit is set (velocity derived from the position, so
        /// the two can never disagree), otherwise dead rest at the origin.
        /// </summary>
        public void ResetPlayer()
        {
            if (playerShip == null) return;

            var field = GravityWorld.Field;
            if (startInOrbit && field != null)
            {
                playerShip.ResetTo(orbitStartPosition, OrbitStartVelocity());
            }
            else
            {
                playerShip.ResetToOrigin();
            }
        }

        /// <summary>The circular-orbit velocity implied by orbitStartPosition,
        /// or zero when this scene has no gravity field. Exposed so a test can
        /// assert the reset lands on exactly this velocity rather than
        /// recomputing the formula itself.</summary>
        public Vector2 OrbitStartVelocity()
        {
            var field = GravityWorld.Field;
            if (!startInOrbit || field == null) return Vector2.zero;

            var worldPos = new Unity.Mathematics.float2(orbitStartPosition.x, orbitStartPosition.y);
            var v = OrbitHelper.CircularOrbitVelocity(field, orbitBodyIndex, worldPos);
            return new Vector2(v.x, v.y);
        }

        void Awake()
        {
            if (playerShip == null) Debug.LogError("DemoMode requires a player ShipController.");
            if (playerBody == null && playerShip != null) playerBody = playerShip.GetComponent<Rigidbody2D>();
            if (builder == null) Debug.LogError("DemoMode requires a BuilderController.");
            if (playerRenderer == null && playerShip != null) playerRenderer = playerShip.GetComponent<ShipRenderer>();
            if (playerStructure == null && playerShip != null) playerStructure = playerShip.GetComponent<ShipStructure>();
            if (playerShip != null) playerShip.InputSource = _inputSource;
        }

        void Start()
        {
            ApplyState();
        }

        void Update()
        {
            var input = _inputSource ?? LegacyDemoInput.Instance;

            if (input.TogglePressed) ToggleState();

            if (State == DemoState.Fly)
            {
                if (input.FirePressed && playerShip != null) playerShip.RequestFire();
                if (input.OverlayPressed && playerRenderer != null)
                {
                    playerRenderer.Overlay = NextOverlay(playerRenderer.Overlay);
                }
                if (input.ResetPressed) ResetPlayer();
            }
        }

        /// <summary>Lists every block on `ship` with an active (nonzero,
        /// unexpired) powerup variant, one HUD line each, e.g.
        /// "Cannon (2,0): Gravity gun 7.3 s".</summary>
        static void DrawActivePowerups(Hullbreach.Ship.ShipBody ship)
        {
            foreach (var kv in ship.Grid.All)
            {
                byte variant = BlockVariants.Get(kv.Value.Modifiers);
                if (variant == 0) continue;
                float timeLeft = ship.VariantTimeLeft(kv.Key);
                if (timeLeft <= 0f) continue;

                BlockKey.Unpack(kv.Key, out int x, out int y);
                string typeName = BlockTypes.Get(kv.Value.TypeId).Name;
                GUILayout.Label($"{typeName} ({x},{y}): {VariantLabel(kv.Value.TypeId, variant)} {timeLeft:0.0} s");
            }
        }

        /// <summary>Human-readable name for a (TypeId, variant) pair, for
        /// the HUD; falls back to a generic "variant N" for anything not
        /// explicitly named here.</summary>
        static string VariantLabel(byte typeId, byte variant)
        {
            if (typeId == BlockTypes.Cannon && variant == 1) return "Gravity gun";
            if (typeId == BlockTypes.Cannon && variant == 2) return "Anti-gravity gun";
            if (typeId == BlockTypes.Thruster && variant == 1) return "Seeking thruster";
            return $"variant {variant}";
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

                    DrawActivePowerups(ship);
                }
            }
            GUILayout.EndArea();
        }
    }
}
