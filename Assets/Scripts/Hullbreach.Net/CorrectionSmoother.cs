using Unity.Mathematics;

namespace Hullbreach.Net
{
    // Visual-only offset that hides a reconcile correction: the drawn ship keeps
    // its old pose and the offset eases to zero, while the sim state is exact.
    // frob:ticket 01M6WV815GYEJ58MTM4HSS4Z6K
    // frob:doc docs/reference/hullbreach-net.md#correctionsmoother
    public sealed class CorrectionSmoother
    {
        // Corrections bigger than this (world units) are real teleports; show them at once.
        // frob:doc docs/reference/hullbreach-net.md#correctionsmoother
        public const float SnapDistance = 2f;

        // Rotation counterpart of SnapDistance, in radians.
        // frob:doc docs/reference/hullbreach-net.md#correctionsmoother
        public const float SnapAngle = 0.5f;

        // Seconds for the offset to fall to 1/e of itself.
        // frob:doc docs/reference/hullbreach-net.md#correctionsmoother
        public const float DecaySeconds = 0.1f;

        // Below this the offset is dropped, so it settles to exactly zero.
        const float Epsilon = 1e-3f;

        // Offset to add to the predicted position when drawing.
        // frob:doc docs/reference/hullbreach-net.md#correctionsmoother
        public float2 PositionOffset { get; private set; }

        // Offset to add to the predicted rotation when drawing (radians).
        // frob:doc docs/reference/hullbreach-net.md#correctionsmoother
        public float RotationOffset { get; private set; }

        // Takes (old pose - new pose) so the drawn pose is continuous; a jump past the
        // snap limits clears the offset instead.
        // frob:doc docs/reference/hullbreach-net.md#correctionsmoother
        public void Add(float2 positionDelta, float rotationDelta)
        {
            float2 position = PositionOffset + positionDelta;
            float rotation = RotationOffset + rotationDelta;
            if (math.length(position) > SnapDistance || math.abs(rotation) > SnapAngle)
            {
                Clear();
                return;
            }

            PositionOffset = position;
            RotationOffset = rotation;
        }

        // Eases the offset toward zero over dt seconds, independent of frame rate.
        // frob:doc docs/reference/hullbreach-net.md#correctionsmoother
        public void Advance(float dt)
        {
            float keep = math.exp(-math.max(dt, 0f) / DecaySeconds);
            float2 position = PositionOffset * keep;
            float rotation = RotationOffset * keep;
            PositionOffset = math.length(position) < Epsilon ? float2.zero : position;
            RotationOffset = math.abs(rotation) < Epsilon ? 0f : rotation;
        }

        // Drops the offset so the next frame shows the exact predicted pose.
        // frob:doc docs/reference/hullbreach-net.md#correctionsmoother
        public void Clear()
        {
            PositionOffset = float2.zero;
            RotationOffset = 0f;
        }
    }
}
