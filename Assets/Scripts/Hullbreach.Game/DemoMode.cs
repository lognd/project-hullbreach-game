using UnityEngine;
using Hullbreach.Core;
using Hullbreach.World;
using Hullbreach.Hud;

namespace Hullbreach.Game
{
    /// <summary>Which of the two demo scene states is active.</summary>
    public enum DemoState { Build, Fly }

    /// <summary>
    /// Top-level demo scene conductor: toggles between Build (ship frozen
    /// exactly where it is, BuilderController editing the live grid) and Fly
    /// (simulation on, WASD + arrows + Space + O + R). The status panel and
    /// hull warning banner are uGUI views (StatusPanelView,
    /// HullWarningBanner) driven by the read-only state exposed here (D8).
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

        /// <summary>Name of the first critical block found on the most
        /// recent frame (empty if none); the "worst block" named by the
        /// hull warning banner.</summary>
        public string CriticalBlockName => _criticalBlockName;

        /// <summary>The active powerups on the player's ship right now, as
        /// plain data StatusPanelModel can consume without a ShipBody
        /// reference (D3).</summary>
        public System.Collections.Generic.IReadOnlyList<ActivePowerup> ActivePowerups()
        {
            var result = new System.Collections.Generic.List<ActivePowerup>();
            if (playerShip == null || playerShip.Ship == null) return result;

            var ship = playerShip.Ship;
            foreach (var kv in ship.Grid.All)
            {
                byte variant = BlockVariants.Get(kv.Value.Modifiers);
                if (variant == 0) continue;
                float timeLeft = ship.VariantTimeLeft(kv.Key);
                if (timeLeft <= 0f) continue;

                BlockKey.Unpack(kv.Key, out int x, out int y);
                string typeName = BlockTypes.Get(kv.Value.TypeId).Name;
                result.Add(new ActivePowerup(typeName, x, y, kv.Value.TypeId, variant, timeLeft));
            }
            return result;
        }

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
    }
}
