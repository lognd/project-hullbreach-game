using UnityEngine;
using Hullbreach.Core;
using Hullbreach.Builder;

namespace Hullbreach.Game
{
    // Lifecycle, mouse-to-grid conversion and Gizmo drawing ONLY; see the
    // reference page for what this adapter deliberately leaves out.
    // frob:doc docs/reference/hullbreach-game.md#buildercontroller
    public sealed class BuilderController : MonoBehaviour
    {
        [SerializeField] Camera builderCamera;

        [SerializeField] Transform shipRoot;

        // frob:doc docs/reference/hullbreach-game.md#buildercontroller
        public BuilderSession Session { get; private set; }

        // Optional: standalone use (e.g. a future dedicated build scene
        // with no ShipBody yet) leaves these null.
        [SerializeField] ShipRenderer shipRenderer;
        [SerializeField] ShipCollider shipCollider;
        [SerializeField] ShipController shipController;

        int _hoverKey;
        bool _hasHover;
        Hullbreach.Builder.PlacementVerdict _hoverVerdict;
        bool _hoverValid;

        SpriteRenderer _hoverIndicator;

        // frob:doc docs/reference/hullbreach-game.md#buildercontroller
        public bool HoverValid => _hoverValid;

        // frob:doc docs/reference/hullbreach-game.md#buildercontroller
        public string HoverVerdictText => _hasHover ? BuilderSession.DescribeVerdict(_hoverVerdict) : string.Empty;

        void Awake()
        {
            // Shares the ShipController's grid when one is wired up; see
            // the reference page.
            Session = shipController != null && shipController.Ship != null
                ? new BuilderSession(shipController.Ship.Grid)
                : new BuilderSession();

            if (builderCamera == null) builderCamera = Camera.main;

            Session.Changed += OnSessionChanged;
        }

        void OnDestroy()
        {
            if (Session != null) Session.Changed -= OnSessionChanged;
        }

        // See the reference page for why this matters in Fly mode.
        void OnDisable()
        {
            _hasHover = false;
            _hoverOverride = null;
            if (_hoverIndicator != null) _hoverIndicator.gameObject.SetActive(false);
            Session?.Cancel();
        }

        // frob:doc docs/reference/hullbreach-game.md#buildercontroller
        public GameObject HoverIndicator => _hoverIndicator != null ? _hoverIndicator.gameObject : null;

        void OnSessionChanged()
        {
            if (shipRenderer != null) shipRenderer.MarkDirty();
            if (shipCollider != null) shipCollider.MarkDirty();
            if (shipController != null && shipController.Ship != null) shipController.Ship.RebuildDerivedViews();
        }

        // Public so a play-mode test can drive it; see the reference page.
        // frob:doc docs/reference/hullbreach-game.md#buildercontroller
        public bool TryPlaceAt(int key) => Session != null && Session.Click(key);

        // frob:doc docs/reference/hullbreach-game.md#buildercontroller
        public bool TryRemoveAt(int key) => Session != null && Session.Remove(key);

        // frob:doc docs/reference/hullbreach-game.md#buildercontroller
        public Hullbreach.Builder.PlacementVerdict VerdictAt(int key)
            => Session != null ? Session.Hover(key).Verdict : Hullbreach.Builder.PlacementVerdict.OutOfRange;

        // Pass null to hand the hover back to the mouse; see the reference page.
        // frob:doc docs/reference/hullbreach-game.md#buildercontroller
        public void PreviewHoverAt(int? key) => _hoverOverride = key;

        int? _hoverOverride;

        void Update()
        {
            // TODO [A5]: migrate to the new Input System alongside S27
            //            (activeInputHandler must stay "Both" until then).
            for (int i = 0; i < 9 && i < BlockTypes.Count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    Session.Select((byte)i);
                }
            }

            if (_hoverOverride.HasValue)
            {
                _hasHover = true;
                _hoverKey = _hoverOverride.Value;
                var pinned = Session.Hover(_hoverKey);
                _hoverValid = pinned.Valid;
                _hoverVerdict = pinned.Verdict;
            }
            else if (TryGetHoveredKey(out int key))
            {
                _hasHover = true;
                _hoverKey = key;
                var hover = Session.Hover(key);
                _hoverValid = hover.Valid;
                _hoverVerdict = hover.Verdict;

                if (Input.GetMouseButtonDown(0))
                {
                    Session.Click(key);
                }
                if (Input.GetMouseButtonDown(1))
                {
                    Session.Remove(key);
                }
            }
            else
            {
                _hasHover = false;
            }

            UpdateHoverIndicator();

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.Z))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    Session.Redo();
                }
                else
                {
                    Session.Undo();
                }
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Session.Cancel();
            }
        }

        bool TryGetHoveredKey(out int key)
        {
            key = 0;
            if (builderCamera == null || shipRoot == null) return false;

            Vector3 world = builderCamera.ScreenToWorldPoint(Input.mousePosition);
            world.z = shipRoot.position.z;
            Vector3 local = shipRoot.InverseTransformPoint(world);

            int x = Mathf.FloorToInt(local.x);
            int y = Mathf.FloorToInt(local.y);
            if (!BlockKey.InRange(x, y)) return false;

            key = BlockKey.Pack(x, y);
            return true;
        }

        // See the reference page for why this exists separately from Gizmos.
        void UpdateHoverIndicator()
        {
            if (_hoverIndicator == null)
            {
                var go = new GameObject("HoverIndicator");
                if (shipRoot != null) go.transform.SetParent(shipRoot, false);
                _hoverIndicator = go.AddComponent<SpriteRenderer>();
                _hoverIndicator.sprite = ShipRenderer.MakeSprite();
                _hoverIndicator.sortingOrder = 10;
            }

            _hoverIndicator.gameObject.SetActive(_hasHover);
            if (!_hasHover) return;

            BlockKey.Unpack(_hoverKey, out int x, out int y);
            _hoverIndicator.transform.localPosition = new Vector3(x + 0.5f, y + 0.5f, -0.05f);
            var color = _hoverValid ? Color.green : Color.red;
            color.a = 0.35f;
            _hoverIndicator.color = color;
        }

        void OnDrawGizmos()
        {
            if (!_hasHover || shipRoot == null) return;

            BlockKey.Unpack(_hoverKey, out int x, out int y);
            Vector3 center = shipRoot.TransformPoint(new Vector3(x + 0.5f, y + 0.5f, 0f));
            Vector3 size = shipRoot.lossyScale;

            bool valid = Session.State == BuilderState.Orienting
                ? true
                : PlacementRules.CanPlace(Session.Grid, _hoverKey, Session.SelectedTypeId);

            Gizmos.color = valid ? Color.green : Color.red;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
