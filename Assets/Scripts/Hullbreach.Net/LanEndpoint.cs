using System;
using System.Net;

namespace Hullbreach.Net
{
    // Engine-free reading of the address a player types into the lobby (S28-2):
    // "192.168.1.23", "192.168.1.23:7979" or "localhost[:port]". Shared with the
    // command line so both entry points accept exactly the same addresses.
    // frob:doc docs/reference/hullbreach-net.md#lanendpoint
    public readonly struct LanEndpoint
    {
        // frob:doc docs/reference/hullbreach-net.md#lanendpoint
        public LanEndpoint(string address, ushort port)
        {
            Address = address;
            Port = port;
        }

        // Numeric dotted IPv4 address; "localhost" is already mapped to loopback.
        // frob:doc docs/reference/hullbreach-net.md#lanendpoint
        public string Address { get; }

        // frob:doc docs/reference/hullbreach-net.md#lanendpoint
        public ushort Port { get; }

        // frob:doc docs/reference/hullbreach-net.md#lanendpoint
        public override string ToString() => $"{Address}:{Port}";

        // Numeric IPv4 or localhost only; no DNS, so the check never blocks the UI.
        // frob:doc docs/reference/hullbreach-net.md#lanendpoint
        public static bool TryParseAddress(string text, out string address)
        {
            address = null;
            if (text == null) return false;
            string trimmed = text.Trim();
            if (string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                address = DirectLaunchPlan.LoopbackAddress;
                return true;
            }
            if (trimmed.IndexOf(':') >= 0 || trimmed.Split('.').Length != 4) return false;
            if (!IPAddress.TryParse(trimmed, out IPAddress parsed)) return false;
            address = parsed.ToString();
            return true;
        }

        // `error` is a player-facing sentence when this returns false.
        // frob:doc docs/reference/hullbreach-net.md#lanendpoint
        public static bool TryParse(string text, ushort defaultPort, out LanEndpoint endpoint, out string error)
        {
            endpoint = default;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Enter the host's IP address, for example 192.168.1.23.";
                return false;
            }

            string trimmed = text.Trim();
            string addressPart = trimmed;
            ushort port = defaultPort;
            int colon = trimmed.LastIndexOf(':');
            if (colon >= 0)
            {
                addressPart = trimmed.Substring(0, colon);
                string portPart = trimmed.Substring(colon + 1);
                if (!ushort.TryParse(portPart, out port) || port == 0)
                {
                    error = $"'{portPart}' is not a valid port (use 1-65535).";
                    return false;
                }
            }

            if (!TryParseAddress(addressPart, out string address))
            {
                error = $"'{addressPart}' is not an IPv4 address such as 192.168.1.23.";
                return false;
            }

            endpoint = new LanEndpoint(address, port);
            return true;
        }
    }
}
