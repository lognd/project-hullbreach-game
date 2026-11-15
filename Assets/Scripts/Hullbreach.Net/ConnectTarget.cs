using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Hullbreach.Platform;

namespace Hullbreach.Net
{
    // The numeric endpoint a ConnectRequest dials, with the name already resolved (S29-3b).
    // Resolution is injected so tests run offline and the Unity side picks async or blocking DNS.
    // frob:doc docs/reference/hullbreach-net.md#connecttarget
    // frob:ticket 01M5XACEYZRD4KJYBEJYTVQZRX
    public sealed class ConnectTarget
    {
        ConnectTarget(string address, ushort port, ConnectRequest request)
        {
            Address = address;
            Port = port;
            Request = request;
        }

        // Dotted IPv4 the transport can dial.
        // frob:doc docs/reference/hullbreach-net.md#connecttarget
        public string Address { get; }

        // frob:doc docs/reference/hullbreach-net.md#connecttarget
        public ushort Port { get; }

        // The request this target came from; its MatchId goes out in the JoinTicket.
        // frob:doc docs/reference/hullbreach-net.md#connecttarget
        public ConnectRequest Request { get; }

        // The ticket the client sends right after the transport connects.
        // frob:doc docs/reference/hullbreach-net.md#connecttarget
        public JoinTicket Ticket => new JoinTicket(Request.MatchId);

        // frob:doc docs/reference/hullbreach-net.md#connecttarget
        public override string ToString() => $"{Address}:{Port}";

        // Numeric IPv4 is used as is; a name goes through `resolve` (first IPv4 answer wins).
        // A throwing or empty resolver, or a name that is not a plausible host, is an error value.
        // frob:doc docs/reference/hullbreach-net.md#connecttarget
        public static ConfigResult<ConnectTarget> Resolve(ConnectRequest request,
            Func<string, IReadOnlyList<string>> resolve)
        {
            if (request == null) return ConfigResult<ConnectTarget>.Fail("no connect request");

            string host = request.Host.Trim();
            if (LanEndpoint.TryParseAddress(host, out string numeric))
                return ConfigResult<ConnectTarget>.Ok(new ConnectTarget(numeric, request.Port, request));
            if (!LooksLikeHostName(host))
                return ConfigResult<ConnectTarget>.Fail($"server host '{host}' is not a valid address");
            if (resolve == null)
                return ConfigResult<ConnectTarget>.Fail($"cannot resolve server host '{host}'");

            IReadOnlyList<string> answers;
            try
            {
                answers = resolve(host);
            }
            catch (Exception e) when (e is SocketException || e is ArgumentException || e is InvalidOperationException)
            {
                return ConfigResult<ConnectTarget>.Fail($"cannot resolve server host '{host}'");
            }
            if (answers != null)
            {
                foreach (string answer in answers)
                {
                    if (answer == null || !IPAddress.TryParse(answer, out IPAddress ip)) continue;
                    if (ip.AddressFamily != AddressFamily.InterNetwork) continue;
                    return ConfigResult<ConnectTarget>.Ok(new ConnectTarget(ip.ToString(), request.Port, request));
                }
            }
            return ConfigResult<ConnectTarget>.Fail($"server host '{host}' has no IPv4 address");
        }

        // Letters, digits, dots and hyphens only, so a hostile handoff cannot smuggle odd text into the resolver.
        static bool LooksLikeHostName(string host)
        {
            if (host.Length == 0 || host.Length > 253) return false;
            foreach (char c in host)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '.';
                if (!ok) return false;
            }
            return host[0] != '.' && host[0] != '-' && host[host.Length - 1] != '.';
        }
    }
}
