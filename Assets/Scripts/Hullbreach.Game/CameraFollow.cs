using UnityEngine;

namespace Hullbreach.Game
{
    // frob:doc docs/reference/hullbreach-game.md#camerafollow
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;

        [SerializeField] float smoothing = 5f;

        // frob:doc docs/reference/hullbreach-game.md#camerafollow
        public void SetTarget(Transform newTarget) => target = newTarget;

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        }
    }
}
