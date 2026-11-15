namespace Hullbreach.Hud
{
    // Engine-free maths for the follow camera (D3): how far ahead of the ship to aim and how far to zoom out.
    // A plain exponential follow trails a ship by speed / smoothing, which at 60 u/s pushed it off screen.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public static class CameraRigModel
    {
        // Distance to aim ahead of the ship along its velocity, capped so a runaway ship cannot drag the frame away.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static float LeadDistance(float speed, float leadSeconds, float maxLead)
        {
            if (float.IsNaN(speed) || speed <= 0f) return 0f;
            float lead = speed * leadSeconds;
            return lead > maxLead ? maxLead : lead;
        }

        // Orthographic size that grows linearly from baseSize at rest to maxSize at fullSpeed, then holds.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static float ZoomSize(float speed, float baseSize, float maxSize, float fullSpeed)
        {
            if (float.IsNaN(speed) || speed <= 0f || fullSpeed <= 0f) return baseSize;
            float t = speed >= fullSpeed ? 1f : speed / fullSpeed;
            return baseSize + (maxSize - baseSize) * t;
        }

        // Where a backdrop star at basePos draws on one axis: it drifts at parallax times the camera and wraps so the
        // field tiles forever around the camera, which is what gives a ship in an empty void a sense of motion.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static float StarOffset(float basePos, float cameraPos, float parallax, float span)
        {
            float shifted = basePos - cameraPos * parallax;
            float wrapped = shifted - span * (float)System.Math.Floor(shifted / span) - span * 0.5f;
            return cameraPos + wrapped;
        }
    }
}
