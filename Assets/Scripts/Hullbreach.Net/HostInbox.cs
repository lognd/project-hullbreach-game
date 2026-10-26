namespace Hullbreach.Net
{
    // Routes one datagram from a peer into the server by its MessageKind byte, so every
    // host (NetDemo, tests) decodes client messages the same way.
    // frob:ticket 01M5VNXAT5CA5MSTM07TYX3ATQ
    // frob:doc docs/reference/hullbreach-net.md#hostinbox
    public static class HostInbox
    {
        // Returns false for an empty, malformed or non-client message; nothing is applied then.
        // Peer identity is the transport's `peer`, never anything inside the message.
        // frob:ticket 01M5VNXAT5CA5MSTM07TYX3ATQ
        // frob:doc docs/reference/hullbreach-net.md#hostinbox
        public static bool Dispatch(ServerSimulation server, int peer, byte[] buffer, int length)
        {
            if (length < 1) return false;
            var r = new ByteReader(buffer, 0, length);
            switch ((MessageKind)buffer[0])
            {
                case MessageKind.Input:
                {
                    var input = InputMessage.Read(ref r);
                    if (r.Failed) return false;
                    server.SetInput(peer, input);
                    return true;
                }
                case MessageKind.ReadyUp:
                {
                    var ready = ReadyUp.Read(ref r);
                    if (r.Failed) return false;
                    server.SetReady(peer, ready.Ready);
                    return true;
                }
                default:
                    return false;
            }
        }
    }
}
