using Unity.Mathematics;

namespace Hullbreach.Net
{
    // How far a graze may land before the server calls it a miss. The server is the only judge,
    // so "defender-favored" means a borderline shot costs the shooter, never the player shot at.
    // frob:doc docs/reference/hullbreach-net.md#hittolerance
    public readonly struct HitTolerance
    {
        // Seconds of lag forgiven to the defender: the shooter's screen may show the target this
        // far behind where the server has it.
        // frob:doc docs/reference/hullbreach-net.md#hittolerance
        public readonly float GraceSeconds;

        // Cap in world units so a fast ship cannot make itself untouchable.
        // frob:doc docs/reference/hullbreach-net.md#hittolerance
        public readonly float MaxMargin;

        // frob:doc docs/reference/hullbreach-net.md#hittolerance
        public HitTolerance(float graceSeconds, float maxMargin)
        {
            GraceSeconds = math.max(0f, graceSeconds);
            MaxMargin = math.max(0f, maxMargin);
        }

        // 100 ms matches the throttled-link test (S48-3); 0.5 is half a block.
        // frob:doc docs/reference/hullbreach-net.md#hittolerance
        public static HitTolerance Default => new HitTolerance(0.1f, 0.5f);

        // No forgiveness at all: every overlap is a hit.
        // frob:doc docs/reference/hullbreach-net.md#hittolerance
        public static HitTolerance None => new HitTolerance(0f, 0f);

        // Distance a graze is forgiven for a defender moving at `defenderSpeed`.
        // frob:doc docs/reference/hullbreach-net.md#hittolerance
        public float MarginFor(float defenderSpeed)
            => math.min(MaxMargin, math.max(0f, defenderSpeed) * GraceSeconds);
    }
}
