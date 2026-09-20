using UnityEngine;
using Hullbreach.Core;
using Hullbreach.Builder;

namespace Hullbreach.Game
{
    /// <summary>
    /// The MonoBehaviour adapter for BuilderSession (S30-S33). Lifecycle,
    /// mouse-to-grid conversion and Gizmo drawing ONLY -- every rule about
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

        int _hoverKey;
        bool _hasHover;

        void Awake()
        {
            Session = new BuilderSession();
            if (builderCamera == null) builderCamera = Camera.main;
        }

        void Update()
        {
            // TODO [A5]: migrate to the new Input System alongside S27; the
            //            legacy calls below rely on activeInputHandler being
            //            "Both".
            for (int i = 0; i < 5 && i < BlockTypes.Count; i++)
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
                Session.Hover(key);

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
