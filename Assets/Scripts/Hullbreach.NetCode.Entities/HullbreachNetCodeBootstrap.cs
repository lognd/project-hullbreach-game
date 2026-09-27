using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// Creates the Netcode client/server worlds and makes a local host work without scene wiring.
    /// Multiplayer Play Mode can still override the client address in the Editor.
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class HullbreachNetCodeBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            Application.runInBackground = true;
            bool useDirectConnection = HasDirectConnectionArguments();
            AutoConnectPort = useDirectConnection ? ReadPortFromCommandLine() : (ushort)0;
            DefaultConnectAddress = useDirectConnection
                ? ReadConnectAddressFromCommandLine(AutoConnectPort)
                : NetworkEndpoint.LoopbackIpv4;
            DefaultListenAddress = NetworkEndpoint.AnyIpv4;

            bool initialized = base.Initialize(defaultWorldName);
            if (!initialized) return false;

            foreach (var world in ServerWorlds)
            {
                var tickRate = new ClientServerTickRate
                {
                    SimulationTickRate = HullbreachNetCodeConstants.SimulationTickRate,
                    NetworkTickRate = HullbreachNetCodeConstants.SimulationTickRate,
                };
                tickRate.ResolveDefaults();
                world.EntityManager.CreateSingleton(tickRate, "Hullbreach NetCode Tick Rate");
            }

            return true;
        }

        internal static bool HasDirectConnectionArguments()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-hullbreachDirect" || args[i] == "-hullbreachConnect" || args[i] == "-hullbreachPort")
                    return true;
            }
            return false;
        }

        static ushort ReadPortFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-hullbreachPort" && ushort.TryParse(args[i + 1], out ushort port) && port != 0)
                    return port;
            }
            return HullbreachNetCodeConstants.DefaultPort;
        }

        static NetworkEndpoint ReadConnectAddressFromCommandLine(ushort port)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] != "-hullbreachConnect") continue;
                if (NetworkEndpoint.TryParse(args[i + 1], port, out NetworkEndpoint endpoint))
                    return endpoint;
                Debug.LogWarning($"Invalid -hullbreachConnect address '{args[i + 1]}'; using loopback.");
                break;
            }
            return NetworkEndpoint.LoopbackIpv4;
        }
    }
}
