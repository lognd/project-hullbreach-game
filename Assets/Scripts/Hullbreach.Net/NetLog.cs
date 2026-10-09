using System;

namespace Hullbreach.Net
{
    // The one logging seam for plain-C# netcode; the host (Unity or headless) assigns Sink.
    // frob:doc docs/reference/hullbreach-net.md#netlog
    public static class NetLog
    {
        // Null by default so unit tests and tools stay silent unless they opt in.
        // frob:doc docs/reference/hullbreach-net.md#netlog
        public static Action<string> Sink;

        // Rare-path events only (join, leave, refusals, drops); never per tick.
        // frob:doc docs/reference/hullbreach-net.md#netlog
        public static void Write(string message) => Sink?.Invoke(message);
    }
}
