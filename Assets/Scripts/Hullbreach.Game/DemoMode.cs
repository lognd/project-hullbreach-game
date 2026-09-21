using UnityEngine;
using Hullbreach.Core;
using Hullbreach.World;

namespace Hullbreach.Game
{
    /// <summary>Which of the two demo scene states is active.</summary>
    public enum DemoState { Build, Fly }

    /// <summary>How close the most loaded block is to failing, as three
    /// bands the HUD can shout about without the player reading numbers.</summary>
    public enum HullWarning
    {
        /// <summary>Max ratio below 0.5 and buckling comfortably far off.</summary>
        Ok,
        /// <summary>Max ratio 0.5 to 0.8: the structure is working hard.</summary>
        Strain,
        /// <summary>Max ratio 0.8+, or critical load factor below 1.5:
        /// something is about to come off.</summary>
        Critical,
    }

    /// <summary>
    /// Top-level demo scene conductor: toggles between Build (ship frozen
    /// exactly where it is, BuilderController editing the live grid) and Fly
    /// (simulation on, WASD + arrows + Space + O + R), and draws the
    /// always-on OnGUI status panel including the control-channel bars and
    /// the structural warning readout.
    ///
    /// Build mode PAUSES the ship rather than resetting it: switching modes
    /// must never move the ship (that was the "Tab teleports me" bug). Only R
    /// repositions anything.
    /// </summary>
    public sealed class DemoMode : MonoBehaviour
    {
        [SerializeField] ShipController playerShip;
        [SerializeField] Rigidbody2D playerBody;
        [SerializeField] BuilderController builder;
        [SerializeField] BuilderHud builderHud;
        [SerializeField] ShipRenderer playerRenderer;
        [SerializeField] ShipStructure playerStructure;

        /// <summary>When set, the ship STARTS on (and the R key returns it
        /// to) a preset circular orbit (orbitStartPosition around
        /// orbitBodyIndex) instead of dead rest at the origin. Off by default
        /// so scenes without a GravityWorld (RocketScene) behave exactly as
        /// before.</summary>
        [SerializeField] bool startInOrbit = false;

        /// <summary>World-space position the ship starts at, and that the R
        /// key resets to, when startInOrbit is set; the orbital velocity is
        /// computed from this position, not authored separately, so moving
        /// the start point in the Inspector can never leave a mismatched
        /// velocity behind.</summary>
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

        /// <summary>The structural warning band computed on the most recent
        /// frame, exposed so a play-mode test can assert the player is warned
        /// BEFORE blocks start coming off.</summary>
        public HullWarning Warning { get; private set; } = HullWarning.Ok;

        /// <summary>Max of DuctileRatio/BrittleRatio/BucklingRatio across the
        /// player's blocks on the most recent frame; the number behind
        /// <see cref="Warning"/>.</summary>
        public float MaxStressRatio { get; private set; }

        /// <summary>How many of the player's blocks are above the flashing-red
        /// threshold (0.8) right now.</summary>
        public int CriticalBlockCount { get; private set; }

        /// <summary>Ratio at or above which a block is "in the red": flashed
        /// by ShipRenderer and counted in <see cref="CriticalBlockCount"/>.</summary>
        public const float CriticalRatio = 0.8f;

        /// <summary>Ratio at or above which the HUD reads STRAIN.</summary>
        public const float StrainRatio = 0.5f;

        /// <summary>Critical load factor below which buckling alone escalates
        /// the warning to CRITICAL, however low the stress ratios are.</summary>
        public const float CriticalLoadFactorFloor = 1.5f;

        string _criticalBlockName = string.Empty;

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
            // Put the ship where it is meant to fly BEFORE the first physics
            // step. Previously the scene authored the ship at the origin and
            // only R ever moved it onto the orbit, so the demo opened with
            // the ship falling straight at the planet.
            ResetPlayer();
            ApplyState();
        }

        /// <summary>
        /// Switches mode and applies it. Public so a play-mode test can drive
        /// the toggle without synthesising a Tab key press. Switching modes
        /// never moves the ship: Build pauses it in place, Fly resumes it.
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
        /// This is the ONLY thing that moves the ship without the player
        /// flying it, and it is bound to R alone.
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

            UpdateWarning();
        }

        /// <summary>
        /// Recomputes the structural warning band from this frame's solve and
        /// tells ShipRenderer which blocks to flash. Runs every frame in both
        /// modes so the player is never looking at a stale "OK".
        /// </summary>
        void UpdateWarning()
        {
            MaxStressRatio = 0f;
            CriticalBlockCount = 0;
            _criticalBlockName = string.Empty;

            if (playerStructure == null || playerShip == null || playerShip.Ship == null)
            {
                Warning = HullWarning.Ok;
                return;
            }

            var grid = playerShip.Ship.Grid;
            foreach (var kvp in playerStructure.Solver.BlockStresses)
            {
                var stress = kvp.Value;
                float ratio = Mathf.Max(stress.DuctileRatio, Mathf.Max(stress.BrittleRatio, stress.BucklingRatio));
                if (ratio > MaxStressRatio) MaxStressRatio = ratio;
                if (ratio < CriticalRatio) continue;

                CriticalBlockCount++;
                if (_criticalBlockName.Length == 0 && grid.TryGet(kvp.Key, out var block))
                {
                    _criticalBlockName = BlockTypes.Get(block.TypeId).Name;
                }
            }

            float clf = playerStructure.Solver.CriticalLoadFactor;
            bool bucklingCritical = !float.IsInfinity(clf) && clf < CriticalLoadFactorFloor;

            Warning = MaxStressRatio >= CriticalRatio || bucklingCritical ? HullWarning.Critical
                    : MaxStressRatio >= StrainRatio ? HullWarning.Strain
                    : HullWarning.Ok;

            if (playerRenderer != null) playerRenderer.FlashRatioThreshold = CriticalRatio;
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

        /// <summary>
        /// Applies the current mode. Build freezes the ship IN PLACE: the
        /// plain-C# simulation stops stepping AND the Rigidbody2D stops
        /// simulating, so neither can drift away from the other while the
        /// player is editing. Fly resumes both from exactly that state.
        /// </summary>
        void ApplyState()
        {
            bool building = State == DemoState.Build;

            // Disable the builder FIRST when leaving Build: its OnDisable is
            // what hides the hover preview and cancels a pending placement,
            // and doing it before flight resumes means no frame ever renders
            // the build cursor over a flying ship.
            if (!building)
            {
                if (builder != null) builder.enabled = false;
                if (builderHud != null) builderHud.enabled = false;
            }

            if (playerShip != null)
            {
                playerShip.InputEnabled = !building;
                playerShip.SimulationEnabled = !building;
            }
            if (playerBody != null) playerBody.simulated = !building;

            if (building)
            {
                if (builder != null) builder.enabled = true;
                if (builderHud != null) builderHud.enabled = true;
            }
        }

        void OnGUI()
        {
            DrawStatusPanel();
            if (State == DemoState.Fly) DrawHullWarning();
        }

        /// <summary>Height the bottom-left status panel needs in the current
        /// mode. Build lists four short lines; Fly adds speed, three control
        /// bars and a line per active powerup. BuilderHud subtracts this from
        /// the window height so the two panels can never overlap, which they
        /// did at small window sizes.</summary>
        public static float StatusPanelHeight(bool building) => building ? 140f : 250f;

        void DrawStatusPanel()
        {
            float height = StatusPanelHeight(State == DemoState.Build);
            GUILayout.BeginArea(new Rect(10, Screen.height - height, 440, height - 10), GUI.skin.box);
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
                GUILayout.Label("Space: fire   O: cycle overlay   R: reset to start");
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

                    DrawChannelBar("Thrust ", ship.ForwardThrottleMean, new Color(0.9f, 0.2f, 0.15f));
                    DrawChannelBar("Reverse", ship.ReverseThrottleMean, new Color(0.2f, 0.85f, 0.3f));
                    DrawSteerBar(ship.SteerThrottleMean);

                    DrawActivePowerups(ship);
                }
            }
            GUILayout.EndArea();
        }

        /// <summary>One labelled 0..100% bar with a colored fill, used for
        /// the thrust and reverse channels. Drawn with GUI.DrawTexture over a
        /// reserved layout rect, which needs no skin assets.</summary>
        void DrawChannelBar(string label, float value01, Color fill)
        {
            value01 = Mathf.Clamp01(value01);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value01 * 100f:0}%", GUILayout.Width(110));
            Rect track = GUILayoutUtility.GetRect(240f, 14f);
            DrawBarTrack(track);
            var filled = new Rect(track.x, track.y, track.width * value01, track.height);
            DrawSolid(filled, fill);
            GUILayout.EndHorizontal();
        }

        /// <summary>A centered -100..+100% bar that fills left or right from
        /// the middle, for the steer channel.</summary>
        void DrawSteerBar(float value)
        {
            value = Mathf.Clamp(value, -1f, 1f);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Steer   {value * 100f:+0;-0;0}%", GUILayout.Width(110));
            Rect track = GUILayoutUtility.GetRect(240f, 14f);
            DrawBarTrack(track);

            float mid = track.x + track.width * 0.5f;
            float half = track.width * 0.5f * Mathf.Abs(value);
            var filled = value >= 0f
                ? new Rect(mid, track.y, half, track.height)
                : new Rect(mid - half, track.y, half, track.height);
            DrawSolid(filled, Color.white);
            DrawSolid(new Rect(mid - 1f, track.y, 2f, track.height), new Color(0.6f, 0.6f, 0.6f));
            GUILayout.EndHorizontal();
        }

        static void DrawBarTrack(Rect track) => DrawSolid(track, new Color(0.1f, 0.1f, 0.12f, 0.9f));

        static Texture2D _barTexture;

        /// <summary>Fills `rect` with `color` using a single shared 1x1 white
        /// texture, so the HUD needs no imported sprite assets.</summary>
        static void DrawSolid(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            if (_barTexture == null)
            {
                _barTexture = new Texture2D(1, 1);
                _barTexture.SetPixel(0, 0, Color.white);
                _barTexture.Apply();
            }
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _barTexture);
            GUI.color = previous;
        }

        /// <summary>
        /// Top-center structural readout: green OK, yellow STRAIN, flashing
        /// red CRITICAL, with how many blocks are in the red and one line of
        /// advice. Deliberately loud and separate from the corner panel: the
        /// player needs to know the hull is about to fail without reading a
        /// number or switching overlays.
        /// </summary>
        void DrawHullWarning()
        {
            string text;
            string hint;
            Color color;

            switch (Warning)
            {
                case HullWarning.Critical:
                    text = "Hull: CRITICAL";
                    hint = _criticalBlockName.Length > 0 && CriticalBlockCount > 1
                        ? "brace the arm"
                        : "ease off thrust";
                    // Flash: alternate between full and dim red a few times a
                    // second so it reads as an alarm, not a label.
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 12f);
                    color = new Color(1f, 0.15f * pulse, 0.15f * pulse, 1f);
                    break;
                case HullWarning.Strain:
                    text = "Hull: STRAIN";
                    hint = "ease off thrust";
                    color = new Color(1f, 0.85f, 0.2f);
                    break;
                default:
                    text = "Hull: OK";
                    hint = string.Empty;
                    color = new Color(0.3f, 1f, 0.4f);
                    break;
            }

            var area = new Rect(Screen.width * 0.5f - 170f, 10f, 340f, 62f);
            GUILayout.BeginArea(area, GUI.skin.box);
            var previous = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label($"{text}   (max ratio {MaxStressRatio:0.00})");
            if (Warning != HullWarning.Ok)
            {
                string where = CriticalBlockCount > 0
                    ? $"{CriticalBlockCount} block(s) in the red, worst: {_criticalBlockName}"
                    : "load factor low";
                GUILayout.Label(where);
                GUILayout.Label(hint);
            }
            GUI.contentColor = previous;
            GUILayout.EndArea();
        }
    }
}
