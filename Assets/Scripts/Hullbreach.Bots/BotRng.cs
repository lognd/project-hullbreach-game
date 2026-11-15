
namespace Hullbreach.Bots
{
    // Seedable splitmix64 generator. Injected into every bot component so a match replays bit for bit;
    // System.Random is avoided on purpose because its stream is not guaranteed across runtimes.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    // frob:doc docs/reference/hullbreach-bots.md#botrng
    public sealed class BotRng
    {
        ulong _state;

        // frob:doc docs/reference/hullbreach-bots.md#botrng
        public BotRng(ulong seed)
        {
            _state = seed ^ 0x9E3779B97F4A7C15UL;
            NextULong();
        }

        // frob:doc docs/reference/hullbreach-bots.md#botrng
        public ulong NextULong()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        // Uniform in [0, 1).
        // frob:doc docs/reference/hullbreach-bots.md#botrng
        public float NextFloat() => (NextULong() >> 40) * (1f / (1 << 24));

        // frob:doc docs/reference/hullbreach-bots.md#botrng
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        // Uniform integer in [min, max).
        // frob:doc docs/reference/hullbreach-bots.md#botrng
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextULong() % (ulong)(max - min));

        // frob:doc docs/reference/hullbreach-bots.md#botrng
        public bool Chance(float probability) => NextFloat() < probability;

        // Approximately N(0,1): the sum of four uniforms, rescaled. Bounded in [-3.46, 3.46], which suits aim noise.
        // frob:doc docs/reference/hullbreach-bots.md#botrng
        public float Gaussian()
        {
            float s = NextFloat() + NextFloat() + NextFloat() + NextFloat();
            return (s - 2f) * 1.7320508f;
        }

        // An independent stream derived from this one, so adding a consumer never shifts the others.
        // frob:doc docs/reference/hullbreach-bots.md#botrng
        public BotRng Fork(ulong salt) => new BotRng(NextULong() ^ (salt * 0xD6E8FEB86659FD93UL));
    }
}
