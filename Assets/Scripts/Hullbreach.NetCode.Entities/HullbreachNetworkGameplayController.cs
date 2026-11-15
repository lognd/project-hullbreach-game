using System.Collections.Generic;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Game;
using Hullbreach.Hud;
using Hullbreach.Net;
using Hullbreach.Ship;
using Unity.Mathematics;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// Local input and build-mode presentation. Simulation and validation stay
    /// server-authoritative; this component only previews and queues requests.
    /// </summary>
    [AddComponentMenu("Hullbreach/Online/Network Gameplay Controller")]
    public sealed class HullbreachNetworkGameplayController : MonoBehaviour
    {
        internal readonly struct BuildRequest
        {
            public readonly sbyte X;
            public readonly sbyte Y;
            public readonly byte TypeId;
            public readonly byte Modifiers;
            public readonly HullbreachBuildAction Action;

            public BuildRequest(int x, int y, byte typeId, byte modifiers, HullbreachBuildAction action)
            {
                X = (sbyte)x;
                Y = (sbyte)y;
                TypeId = typeId;
                Modifiers = modifiers;
                Action = action;
            }
        }

        static readonly Queue<BuildRequest> Requests = new Queue<BuildRequest>();

        // ---- test hook: a mouse without hardware ----
        // Play Mode checks cannot move the real cursor, so they set a screen position and send a click here. Both go
        // through exactly the code a real mouse does (hover cell, over-UI test, placement), nothing is bypassed.
        static Vector2? s_scriptedMouse;
        static int s_scriptedLeftFrame = int.MinValue, s_scriptedRightFrame = int.MinValue;

        /// <summary>Pretends the cursor is at this screen position (null returns to the real mouse).</summary>
        public static void ScriptMouse(Vector2? screenPosition) => s_scriptedMouse = screenPosition;

        /// <summary>Pretends the left (or right) button went down this frame; it stays valid for one more frame.</summary>
        public static void ScriptClick(bool right = false)
        {
            if (right) s_scriptedRightFrame = Time.frameCount; else s_scriptedLeftFrame = Time.frameCount;
        }

        static Vector3 MousePosition => s_scriptedMouse.HasValue ? (Vector3)s_scriptedMouse.Value : Input.mousePosition;

        static bool MouseDown(int button)
        {
            int frame = button == 0 ? s_scriptedLeftFrame : s_scriptedRightFrame;
            if ((long)Time.frameCount - frame <= 1)
            {
                if (button == 0) s_scriptedLeftFrame = int.MinValue; else s_scriptedRightFrame = int.MinValue;
                return true;
            }
            return Input.GetMouseButtonDown(button);
        }

        // True when a UI element sits under the pointer; with a scripted pointer this is a real raycast through the
        // EventSystem, so clicking the build panel does not place a block behind it.
        static bool PointerOverUi()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            if (!s_scriptedMouse.HasValue) return es.IsPointerOverGameObject();
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = s_scriptedMouse.Value };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            return hits.Count > 0;
        }

        [SerializeField] Camera gameplayCamera;
        [SerializeField] float cameraSmoothing = 6f;

        // Same lead and speed zoom as the offline CameraFollow: a plain follow trails a fast ship off screen.
        [SerializeField] float leadSeconds = 0.17f;
        /// <summary>Resting camera half-height in world units; the scene asks for 16, which left ships small at 1280x720.</summary>
        public const float DefaultOrthographicSize = 10f;
        [SerializeField] float maxOrthographicSize = 30f;
        [SerializeField] float fullSpeed = 40f;

        float _baseOrthographicSize;
        static float s_lastPlaceTime = -1000f;

        GameObject _hover;
        int _hoverKey;
        bool _hasHover;
        bool _hoverValid;
        bool _hoverWait;
        GameObject _arrow;
        int? _pendingKey;
        string _buildStatus = "Select a block with 1-6";

        public static bool Active { get; private set; }
        public static bool IsBuildMode { get; private set; } = true;
        public static byte SelectedTypeId { get; private set; } = BlockTypes.Hull;
        public static string BuildStatus { get; private set; } = "Select a block with 1-6";

        // What the build panel shows: refusals from the server and from the local preview, and the READY state.
        public static readonly ToastQueue Toasts = new ToastQueue();
        public static bool ReadySent { get; private set; }
        public static string HoverReason { get; private set; } = string.Empty;
        // True once the server has started the match (both players ready): stock replaces the point budget.
        public static bool MatchRunning
        {
            get
            {
                var replica = HullbreachNetCodeClient.Replica;
                return replica != null && replica.Economy.HasSnapshot && replica.Economy.BudgetLeft < 0;
            }
        }
        public static EconomyConfig Economy { get; } = EconomyConfig.Default();

        public static void SelectType(byte typeId)
        {
            if (typeId == BlockTypes.Core || !BlockTypes.IsValid(typeId)) return;
            SelectedTypeId = typeId;
        }

        public static void RequestReady()
        {
            if (ReadySent) return;
            ReadySent = true;
            Requests.Enqueue(new BuildRequest(0, 0, 0, 0, HullbreachBuildAction.Ready));
            Toasts.Push("Ready. The match starts when your opponent is ready too.", Time.unscaledTime);
        }

        // The server only enforces the cooldown once a match is live (two or more ships), so only show it then.
        // frob:ticket 01M3DG5Y25YGZ3DV7JSDB4BNMF
        public static float BuildCooldownRemaining
        {
            get
            {
                var replica = HullbreachNetCodeClient.Replica;
                if (replica == null || replica.Ships.Count < 2) return 0f;
                return BuildCooldownModel.Remaining(Time.time - s_lastPlaceTime, ServerSimulation.DefaultBuildCooldownSeconds);
            }
        }

        void OnEnable()
        {
            Active = true;
            IsBuildMode = true;
            Requests.Clear();
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            // Capped (not scaled) so a rematch in the same scene does not shrink it again; ships read better at 1280x720 this close.
            if (gameplayCamera != null) _baseOrthographicSize = Mathf.Min(gameplayCamera.orthographicSize, DefaultOrthographicSize);
            s_lastPlaceTime = -1000f;
            ReadySent = false;
            Toasts.Clear();
            PublishStatus();
        }

        void OnDisable()
        {
            Active = false;
            Requests.Clear();
            if (_hover != null) Destroy(_hover);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                IsBuildMode = !IsBuildMode;
                _pendingKey = null;
                _buildStatus = IsBuildMode ? "Build mode" : "Flight mode";
                PublishStatus();
            }

            if (!IsBuildMode)
            {
                DrainServerVerdicts();
                SetHoverVisible(false);
                return;
            }

            DrainServerVerdicts();

            // Keys 1-6 pick Hull, Armor, Thruster, Cannon, Fin, Retro (the key number is the block id; the core is not buildable).
            for (int i = 1; i < BlockTypes.Count && i < 10; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha0 + i)) continue;
                SelectedTypeId = (byte)i;
                _pendingKey = null;
                _buildStatus = $"Selected {BlockTypes.Get(SelectedTypeId).Name}";
                PublishStatus();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _pendingKey = null;
                _buildStatus = "Placement cancelled";
                PublishStatus();
            }

            if (!TryGetLocalShip(out ShipBody ship) || !TryGetHoveredKey(ship, out _hoverKey))
            {
                _hasHover = false;
                SetHoverVisible(false);
                return;
            }

            _hasHover = true;
            byte modifiers = _pendingKey.HasValue ? FacingTowards(_pendingKey.Value, _hoverKey) : (byte)0;
            int placementKey = _pendingKey ?? _hoverKey;
            _hoverValid = _pendingKey.HasValue
                ? PlacementRules.CanPlace(ship.Grid, placementKey, SelectedTypeId, modifiers, out _)
                : BlockPalette.IsSymmetric(SelectedTypeId)
                    ? PlacementRules.CanPlace(ship.Grid, placementKey, SelectedTypeId, 0, out _)
                    : CanPlaceAnyFacing(ship, placementKey);
            HoverReason = string.Empty;
            if (_hoverValid)
            {
                BlockKey.Unpack(placementKey, out int hx, out int hy);
                var preview = HullbreachNetCodeClient.Replica.Economy.PreviewPlacement(ship.Grid, Economy, MatchRunning,
                    HullbreachNetCodeClient.Replica.SimulationTick, hx, hy, SelectedTypeId, modifiers, out byte detail);
                if (preview != PlacementResult.Ok)
                {
                    HoverReason = EconomyRules.Describe(preview, detail);
                    _hoverValid = preview == PlacementResult.Cooldown; // cooldown is amber, not a mistake
                    _hoverWait = preview == PlacementResult.Cooldown;
                }
                else _hoverWait = false;
            }
            else _hoverWait = false;
            UpdateHover(ship, placementKey, modifiers);

            bool overUi = PointerOverUi();
            if (overUi) return;

            if (MouseDown(1) || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Delete))
            {
                if (PlacementRules.CanRemove(ship.Grid, _hoverKey))
                {
                    BlockKey.Unpack(_hoverKey, out int x, out int y);
                    Requests.Enqueue(new BuildRequest(x, y, 0, 0, HullbreachBuildAction.Remove));
                    _buildStatus = $"Removing ({x}, {y})";
                }
                else { _buildStatus = "That block cannot be removed"; Toasts.Push(EconomyRules.Describe(PlacementResult.CannotRemove), Time.unscaledTime); }
                _pendingKey = null;
                PublishStatus();
            }

            if (!MouseDown(0)) return;

            if (!BlockPalette.IsSymmetric(SelectedTypeId) && !_pendingKey.HasValue)
            {
                if (!_hoverValid) return;
                _pendingKey = _hoverKey;
                _buildStatus = "Choose a facing with the second click";
                PublishStatus();
                return;
            }

            int key = _pendingKey ?? _hoverKey;
            modifiers = _pendingKey.HasValue ? FacingTowards(key, _hoverKey) : (byte)0;
            if (!PlacementRules.CanPlace(ship.Grid, key, SelectedTypeId, modifiers, out PlacementVerdict why))
            {
                _buildStatus = BuilderSession.DescribeVerdict(why);
                Toasts.Push(_buildStatus, Time.unscaledTime);
                PublishStatus();
                return;
            }
            if (HoverReason.Length > 0 && !_hoverWait)
            {
                _buildStatus = HoverReason;
                Toasts.Push(HoverReason, Time.unscaledTime);
                PublishStatus();
                return;
            }
            if (_hoverWait)
            {
                Toasts.Push(HoverReason, Time.unscaledTime);
                return;
            }

            BlockKey.Unpack(key, out int px, out int py);
            Requests.Enqueue(new BuildRequest(px, py, SelectedTypeId, modifiers, HullbreachBuildAction.Place));
            _pendingKey = null;
            s_lastPlaceTime = Time.time;
            _buildStatus = $"Placing {BlockTypes.Get(SelectedTypeId).Name}";
            PublishStatus();
        }

        void LateUpdate()
        {
            if (gameplayCamera == null || !TryGetLocalShip(out ShipBody ship)) return;
            FeelFx.UndoShake(gameplayCamera.transform);
            float speed = math.length(ship.Velocity);
            float2 lead = speed > 0f
                ? ship.Velocity / speed * CameraRigModel.LeadDistance(speed, leadSeconds, maxOrthographicSize * 0.5f)
                : float2.zero;
            Vector3 desired = new Vector3(ship.Position.x + lead.x, ship.Position.y + lead.y, gameplayCamera.transform.position.z);
            float t = 1f - Mathf.Exp(-cameraSmoothing * Time.deltaTime);
            gameplayCamera.transform.position = Vector3.Lerp(gameplayCamera.transform.position, desired, t);
            if (_baseOrthographicSize > 0f)
            {
                float size = CameraRigModel.ZoomSize(speed, _baseOrthographicSize, maxOrthographicSize, fullSpeed);
                gameplayCamera.orthographicSize = Mathf.Lerp(gameplayCamera.orthographicSize, size, t * 0.5f);
            }
            FeelFx.ApplyShake(gameplayCamera.transform);
        }

        bool TryGetHoveredKey(ShipBody ship, out int key)
        {
            key = 0;
            if (gameplayCamera == null) return false;
            Vector3 world = gameplayCamera.ScreenToWorldPoint(MousePosition);
            float2 local = ship.WorldToLocal(new float2(world.x, world.y));
            int x = Mathf.FloorToInt(local.x);
            int y = Mathf.FloorToInt(local.y);
            if (!BlockKey.InRange(x, y)) return false;
            key = BlockKey.Pack(x, y);
            return true;
        }

        void UpdateHover(ShipBody ship, int key, byte facing)
        {
            if (_hover == null)
            {
                _hover = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _hover.name = "NetworkBuildPreview";
                Destroy(_hover.GetComponent<Collider>());
                _arrow = new GameObject("FacingArrow", typeof(MeshFilter), typeof(MeshRenderer));
                _arrow.transform.SetParent(_hover.transform, false);
                _arrow.GetComponent<MeshFilter>().sharedMesh = ArrowMesh();
                var m = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
                _arrow.GetComponent<MeshRenderer>().sharedMaterial = m;
            }
            _hover.SetActive(_hasHover);
            float2 world = ship.LocalToWorld(BlockGrid.CenterOf(key));
            _hover.transform.SetPositionAndRotation(new Vector3(world.x, world.y, -0.2f),
                Quaternion.Euler(0f, 0f, ship.Rotation * Mathf.Rad2Deg));
            // green = placeable, amber = placeable once the cooldown ends, red = refused (the reason is in the toast line)
            Color color = !_hoverValid ? new Color(1f, 0.2f, 0.2f) : _hoverWait ? new Color(1f, 0.7f, 0.2f) : new Color(0.2f, 1f, 0.35f);
            color.a = 0.5f;
            _hover.GetComponent<MeshRenderer>().material.color = color;
            // Block facing: 0 = +y, 1 = +x, 2 = -y, 3 = -x; only blocks that have a facing show the arrow.
            bool directional = !BlockPalette.IsSymmetric(SelectedTypeId);
            _arrow.SetActive(directional);
            _arrow.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * facing);
            _arrow.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        }

        static Mesh s_arrow;
        static Mesh ArrowMesh()
        {
            if (s_arrow != null) return s_arrow;
            s_arrow = new Mesh { name = "FacingArrow" };
            s_arrow.vertices = new[] { new Vector3(0f, 0.42f), new Vector3(-0.3f, -0.05f), new Vector3(0.3f, -0.05f), new Vector3(-0.1f, -0.05f), new Vector3(0.1f, -0.05f), new Vector3(-0.1f, -0.38f), new Vector3(0.1f, -0.38f) };
            s_arrow.triangles = new[] { 0, 2, 1, 3, 4, 5, 4, 6, 5 };
            return s_arrow;
        }

        // Server verdicts (stock, budget, cooldown, buckling) become toasts so a refused block is never silent.
        void DrainServerVerdicts()
        {
            var replica = HullbreachNetCodeClient.Replica;
            if (replica == null) return;
            while (replica.Economy.TryDequeueResult(out PlacementResultMessage msg))
                if (msg.Result != PlacementResult.Ok && msg.Result != PlacementResult.Cooldown)
                    Toasts.Push(EconomyRules.Describe(msg.Result, msg.Detail), Time.unscaledTime);
        }

        void SetHoverVisible(bool visible)
        {
            if (_hover != null) _hover.SetActive(visible);
        }

        static bool TryGetLocalShip(out ShipBody ship)
        {
            ship = null;
            var replica = HullbreachNetCodeClient.Replica;
            return replica != null && replica.Ships.TryGetValue(HullbreachNetCodeClient.LocalNetworkId, out ship);
        }

        static bool CanPlaceAnyFacing(ShipBody ship, int key)
        {
            for (byte facing = 0; facing < 4; facing++)
                if (PlacementRules.CanPlace(ship.Grid, key, SelectedTypeId, facing, out _)) return true;
            return false;
        }

        static byte FacingTowards(int from, int to)
        {
            BlockKey.Unpack(from, out int fx, out int fy);
            BlockKey.Unpack(to, out int tx, out int ty);
            int dx = tx - fx;
            int dy = ty - fy;
            if (dx == 0 && dy == 0) return 0;
            if (System.Math.Abs(dx) >= System.Math.Abs(dy)) return dx >= 0 ? (byte)1 : (byte)3;
            return dy >= 0 ? (byte)0 : (byte)2;
        }

        void PublishStatus()
        {
            BuildStatus = _buildStatus;
        }

        internal static bool TryDequeueBuild(out BuildRequest request)
        {
            if (Requests.Count == 0)
            {
                request = default;
                return false;
            }
            request = Requests.Dequeue();
            return true;
        }
    }
}
