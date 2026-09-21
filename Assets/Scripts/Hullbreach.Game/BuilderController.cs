using UnityEngine;
using Hullbreach.Core;
using Hullbreach.Builder;

namespace Hullbreach.Game
{
    /// <summary>
    /// The MonoBehaviour adapter for BuilderSession (S30-S33). Lifecycle,
    /// mouse-to-grid conversion and Gizmo drawing ONLY: every rule about
    /// what is a legal click lives in BuilderSession/PlacementRules, which
    /// have no UnityEngine dependency and are exercised in edit-mode tests.
    ///
    /// NOT compiled by tools/plaincs/run_tests.sh (it depends on UnityEngine),
    /// so keep this file thin and let the harness catch regressions in the
    /// logic it calls into.
    /// </summary>
    public sealed class BuilderController : MonoBehaviour
    {
        /// <summary>Camera used to convert the mouse position to world space.</summary>
        [SerializeField] Camera builderCamera;

        /// <summary>The ship's root transform; the grid is authored in this transform's local space.</summary>
        [SerializeField] Transform shipRoot;

        /// <summary>Underlying state machine; exposed read-only so a HUD can bind to it.</summary>
        public BuilderSession Session { get; private set; }

        /// <summary>Renderer/collider to notify after every mutation, so the
        /// demo scene's ShipRenderer/ShipCollider rebuild without polling.
        /// Optional: standalone use (e.g. a future dedicated build scene
        /// with no ShipBody yet) leaves these null.</summary>
        [SerializeField] ShipRenderer shipRenderer;
        [SerializeField] ShipCollider shipCollider;
        [SerializeField] ShipController shipController;

        int _hoverKey;
        bool _hasHover;
        Hullbreach.Builder.PlacementVerdict _hoverVerdict;
        bool _hoverValid;

        SpriteRenderer _hoverIndicator;

        /// <summary>Whether the last Hover this frame was valid; drives the
        /// green/red tint on the runtime hover indicator and the HUD text.</summary>
        public bool HoverValid => _hoverValid;

        /// <summary>Human text for why the hovered cell is (in)valid, for
        /// BuilderHud to display without duplicating BuilderSession's switch.</summary>
        public string HoverVerdictText => _hasHover ? BuilderSession.DescribeVerdict(_hoverVerdict) : string.Empty;

        void Awake()
        {
            // Build over the SAME grid a ShipController is simulating, when
            // one is wired up. Otherwise the builder and the flying ship
            // would silently diverge onto two different grids. Falls back to
            // a private grid so this component still works standalone.
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

        /// <summary>Propagates any grid mutation to the renderer/collider and
        /// re-derives ShipBody's thruster/fin/weapon key lists, since a
        /// placed or removed block can add or remove any of those.</summary>
        void OnSessionChanged()
        {
            if (shipRenderer != null) shipRenderer.MarkDirty();
            if (shipCollider != null) shipCollider.MarkDirty();
            if (shipController != null && shipController.Ship != null) shipController.Ship.RebuildDerivedViews();
        }

        void Update()
        {
            // TODO [A5]: migrate to the new Input System alongside S27; the
            //            legacy calls below rely on activeInputHandler being
            //            "Both".
            for (int i = 0; i < 9 && i < BlockTypes.Count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    Session.Select((byte)i);
                }
            }

            if (TryGetHoveredKey(out int key))
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

        /// <summary>
        /// Convert the mouse position through the camera and shipRoot into a
        /// floored grid key, or return false when the mouse is off the plane
        /// (e.g. no camera assigned).
        /// </summary>
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

        /// <summary>
        /// Positions and colors a runtime-only quad over the hovered cell, so
        /// the green/red preview is visible in a running build, not just the
        /// Scene view (OnDrawGizmos never renders in Play mode's Game view).
        /// </summary>
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
