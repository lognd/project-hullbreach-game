using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Hullbreach.NetCode.Entities
{
    // Client -> server, once per connection: the player's platform session token
    // (empty when playing offline) and the username the sign-in screen received.
    // frob:doc docs/platform-link.md#handshake
    public struct HullbreachPlatformHelloRpc : IRpcCommand
    {
        public FixedString512Bytes Token;
        public FixedString64Bytes Username;
    }

    // Server -> client: the admission verdict, so a rejected player sees why before the drop.
    // frob:doc docs/platform-link.md#handshake
    public struct HullbreachPlatformAdmissionRpc : IRpcCommand
    {
        public bool Admitted;
        public bool Verified;
        public FixedString64Bytes DisplayName;
        public FixedString512Bytes Reason;
    }

    // Server-side tag on a connection the admission gate let in; gameplay join keys off it.
    // frob:doc docs/platform-link.md#handshake
    public struct HullbreachPlatformAdmitted : IComponentData
    {
    }

    // Server-side tag: the gate has seen this connection (OnConnected already called).
    // frob:doc docs/platform-link.md#handshake
    public struct HullbreachPlatformTracked : IComponentData
    {
    }

    // Client-side tag: the hello RPC for this connection has been sent.
    // frob:doc docs/platform-link.md#handshake
    public struct HullbreachPlatformHelloSent : IComponentData
    {
    }
}
