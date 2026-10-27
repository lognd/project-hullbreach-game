using System.Collections.Generic;

namespace Hullbreach.Platform
{
    // Engine-free Tab ordering so the form's focus walk is unit-tested; the uGUI side only supplies flags.
    // frob:doc docs/platform-link.md#sign-in-screen
    // frob:ticket 01M5XZJRNREX0M9DF1M6PE7G7V
    public static class TabOrder
    {
        // Next usable index after current (before it when backward), or -1 if none. current -1 starts at the edge.
        // frob:doc docs/platform-link.md#sign-in-screen
        // frob:ticket 01M5XZJRNREX0M9DF1M6PE7G7V
        public static int Step(IReadOnlyList<bool> usable, int current, bool backward, bool wrap = true)
        {
            int count = usable.Count;
            int dir = backward ? -1 : 1;
            int i = current;
            if (current < 0 || current >= count)
            {
                i = backward ? count : -1;
            }

            for (int tries = 0; tries < count; tries++)
            {
                i += dir;
                if (i < 0 || i >= count)
                {
                    if (!wrap)
                    {
                        return -1;
                    }

                    i = (i + count) % count;
                }

                if (usable[i])
                {
                    return i;
                }
            }

            return -1;
        }

        // Position of item in order, or -1 (also for null).
        // frob:doc docs/platform-link.md#sign-in-screen
        // frob:ticket 01M5XZJRNREX0M9DF1M6PE7G7V
        public static int IndexOf<T>(IReadOnlyList<T> order, T item) where T : class
        {
            if (item == null)
            {
                return -1;
            }

            for (int i = 0; i < order.Count; i++)
            {
                if (ReferenceEquals(order[i], item))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
