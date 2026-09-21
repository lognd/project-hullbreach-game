using UnityEngine;

namespace Hullbreach.Game
{
    /// <summary>
    /// Smoothly follows a target transform on the XY plane, keeping the
    /// camera's own Z (its distance from the 2D scene). Orthographic size is
    /// left to the Inspector/scene value rather than hardcoded here, so
    /// designers can still tune framing without touching code.
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;

        /// <summary>Higher is snappier; lower drifts more before catching up.</summary>
        [SerializeField] float smoothing = 5f;

        /// <summary>Assign a new follow target at runtime, e.g. after
        /// switching which ship is "the player".</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        }
    }
}
