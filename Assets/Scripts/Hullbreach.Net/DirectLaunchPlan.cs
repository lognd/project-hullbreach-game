using System;
using System.Collections.Generic;
using System.Net;

namespace Hullbreach.Net
{
    // What this process is for when started without the lobby: decides which
    // Netcode worlds exist and whether to jump straight into the match scene.
    // frob:doc docs/reference/hullbreach-net.md#launchrole
    public enum LaunchRole
    {
        Lobby,
        Host,
        Client,
        Server,
    }

    // Engine-free reading of the direct/LAN command line, so the Unity bootstrap
    // stays a thin switch and every rule here is unit-tested (S47-5).
    // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
    public sealed class DirectLaunchPlan
    {
        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public const string DirectArg = "-hullbreachDirect";

        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public const string ConnectArg = "-hullbreachConnect";

        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public const string PortArg = "-hullbreachPort";

        // Lets a normal desktop player run as a headless dedicated server.
        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public const string ServerArg = "-hullbreachServer";

        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public const ushort DefaultPort = 7979;

        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public const string GameScene = "MultiplayerGame";

        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public const string LoopbackAddress = "127.0.0.1";

        readonly List<string> _warnings;

        DirectLaunchPlan(LaunchRole role, ushort port, string connectAddress, List<string> warnings)
        {
            Role = role;
            Port = port;
            ConnectAddress = connectAddress;
            _warnings = warnings;
        }

        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public LaunchRole Role { get; }

        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public ushort Port { get; }

        // Numeric IP the client role dials; loopback for every other role.
        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public string ConnectAddress { get; }

        // Bad arguments fall back to defaults; these say what was ignored and why.
        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public IReadOnlyList<string> Warnings => _warnings;

        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public bool IsDirect => Role != LaunchRole.Lobby;

        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public bool CreatesServerWorld => Role == LaunchRole.Host || Role == LaunchRole.Server;

        // A client-only process must not open its own server, or two copies on
        // one machine fight over the port and the client dials itself.
        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public bool CreatesClientWorld => Role == LaunchRole.Host || Role == LaunchRole.Client;

        // Direct roles skip the lobby, so nothing else would ever load the match.
        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public bool LoadsGameScene => IsDirect;

        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public static DirectLaunchPlan Parse(IReadOnlyList<string> args, bool isServerBuild)
        {
            args = args ?? Array.Empty<string>();
            var warnings = new List<string>();

            bool direct = Has(args, DirectArg);
            bool server = isServerBuild || Has(args, ServerArg);
            bool connect = Has(args, ConnectArg);
            bool portGiven = Has(args, PortArg);

            ushort port = ReadPort(args, warnings);
            LaunchRole role = server ? LaunchRole.Server
                : connect ? LaunchRole.Client
                : direct || portGiven ? LaunchRole.Host
                : LaunchRole.Lobby;

            if (server && connect)
                warnings.Add($"{ConnectArg} is ignored by a dedicated server.");

            string address = role == LaunchRole.Client ? ReadAddress(args, warnings) : LoopbackAddress;
            return new DirectLaunchPlan(role, port, address, warnings);
        }

        // One greppable line per process; scripts and the doctor look for it.
        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public string Describe()
        {
            switch (Role)
            {
                case LaunchRole.Host:
                    return $"Hullbreach direct: role=host listen=0.0.0.0:{Port} scene={GameScene}";
                case LaunchRole.Client:
                    return $"Hullbreach direct: role=client connect={ConnectAddress}:{Port} scene={GameScene}";
                case LaunchRole.Server:
                    return $"Hullbreach direct: role=server listen=0.0.0.0:{Port} scene={GameScene}";
                default:
                    return "Hullbreach direct: role=lobby (no direct arguments)";
            }
        }

        // Proof line a client logs whenever its match view changes: the network
        // id it was given and how many ships the server replicated to it.
        // frob:doc docs/reference/hullbreach-net.md#directlaunchplan
        public static string MatchStatus(int networkId, int replicatedShips) =>
            $"Hullbreach match: connected as #{networkId}; replicated ships: {replicatedShips}";

        static bool Has(IReadOnlyList<string> args, string name)
        {
            for (int i = 0; i < args.Count; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string ValueAfter(IReadOnlyList<string> args, string name)
        {
            for (int i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) continue;
                if (i + 1 >= args.Count || args[i + 1].StartsWith("-", StringComparison.Ordinal)) return null;
                return args[i + 1];
            }
            return null;
        }

        static ushort ReadPort(IReadOnlyList<string> args, List<string> warnings)
        {
            if (!Has(args, PortArg)) return DefaultPort;
            string text = ValueAfter(args, PortArg);
            if (ushort.TryParse(text, out ushort port) && port != 0) return port;
            warnings.Add($"Invalid {PortArg} '{text}'; using {DefaultPort}.");
            return DefaultPort;
        }

        static string ReadAddress(IReadOnlyList<string> args, List<string> warnings)
        {
            string text = ValueAfter(args, ConnectArg);
            if (text == null)
            {
                warnings.Add($"{ConnectArg} needs an IP address; using {LoopbackAddress}.");
                return LoopbackAddress;
            }
            string trimmed = text.Trim();
            if (string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase)) return LoopbackAddress;
            if (IPAddress.TryParse(trimmed, out IPAddress parsed) && trimmed.IndexOf(':') < 0 && trimmed.Split('.').Length == 4)
                return parsed.ToString();
            warnings.Add($"Invalid {ConnectArg} address '{text}' (use a numeric IPv4 address such as 192.168.1.23); using {LoopbackAddress}.");
            return LoopbackAddress;
        }
    }
}
