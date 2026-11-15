using UnityEngine;
using Hullbreach.Hud;

namespace Hullbreach.Game
{
    // frob:doc docs/reference/hullbreach-game.md#camerafollow
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;

        [SerializeField] float smoothing = 5f;

        // Aim this many seconds ahead; the exponential follow trails by 1/smoothing, so ~0.2 cancels the lag.
        [SerializeField] float leadSeconds = 0.2f;

        // Zoom out toward this orthographic size as the ship nears fullSpeed, so planets and fast ships stay in frame.
        [SerializeField] float maxOrthographicSize = 24f;
        [SerializeField] float fullSpeed = 40f;

        Camera _camera;
        Rigidbody2D _targetBody;
        float _baseSize;

        // frob:doc docs/reference/hullbreach-game.md#camerafollow
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _targetBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
        }

        void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera != null) _baseSize = _camera.orthographicSize;
            if (target != null) _targetBody = target.GetComponent<Rigidbody2D>();
        }

        void LateUpdate()
        {
            if (target == null) return;

            Vector2 velocity = _targetBody != null ? _targetBody.linearVelocity : Vector2.zero;
            float speed = velocity.magnitude;
            Vector2 lead = speed > 0f ? velocity / speed * CameraRigModel.LeadDistance(speed, leadSeconds, maxOrthographicSize * 0.5f) : Vector2.zero;
            FeelFx.UndoShake(transform);
            Vector3 desired = new Vector3(target.position.x + lead.x, target.position.y + lead.y, transform.position.z);
            float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, blend);
            FeelFx.ApplyShake(transform);
            FeelFx.ReportPlayer(target, _targetBody);

            if (_camera != null && _camera.orthographic)
            {
                float size = CameraRigModel.ZoomSize(speed, _baseSize, maxOrthographicSize, fullSpeed);
                _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, size, blend * 0.5f);
            }
        }
    }
}
