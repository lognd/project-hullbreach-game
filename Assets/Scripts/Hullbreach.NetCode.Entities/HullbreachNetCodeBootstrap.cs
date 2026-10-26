using System;
using Hullbreach.Net;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hullbreach.NetCode.Entities
{
    // The direct/LAN plan for this process, parsed once from the real command line.
    // frob:doc docs/netcode.md#direct-and-lan-launch
    public static class HullbreachDirectLaunch
    {
        static DirectLaunchPlan s_plan;

        // frob:doc docs/netcode.md#direct-and-lan-launch
        public static DirectLaunchPlan Plan => s_plan ??= Read();

        // A Dedicated Server build is always a server, whatever its arguments say;
        // NetCode already resolves that (and the editor's play-mode choice) for us.
        static bool IsServerBuild =>
            ClientServerBootstrap.RequestedPlayType == ClientServerBootstrap.PlayType.Server;

        static DirectLaunchPlan Read()
        {
            DirectLaunchPlan plan = DirectLaunchPlan.Parse(Environment.GetCommandLineArgs(), IsServerBuild);
            foreach (string warning in plan.Warnings) Debug.LogWarning("Hullbreach direct: " + warning);
            if (plan.IsDirect) Debug.Log(plan.Describe());
            return plan;
        }

        // Direct roles never see the lobby's START GAME, so load the match here (S47-5).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void LoadGameSceneForDirectRoles()
        {
            if (!Plan.LoadsGameScene) return;
            if (SceneManager.GetActiveScene().name == DirectLaunchPlan.GameScene) return;
            Debug.Log($"Hullbreach direct: loading {DirectLaunchPlan.GameScene} for role={Plan.Role.ToString().ToLowerInvariant()}.");
            SceneManager.LoadScene(DirectLaunchPlan.GameScene, LoadSceneMode.Single);
        }

        // Statics survive play-mode restarts when domain reload is off; start clean each run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_plan = null;
    }

    // Creates the Netcode worlds the launch plan asks for: lobby keeps the stock
    // behaviour, host gets both worlds, client and server get only their own.
    // frob:doc docs/netcode.md#direct-and-lan-launch
    [UnityEngine.Scripting.Preserve]
    public sealed class HullbreachNetCodeBootstrap : ClientServerBootstrap
    {
        // frob:doc docs/netcode.md#direct-and-lan-launch
        public override bool Initialize(string defaultWorldName)
        {
            Application.runInBackground = true;
            DirectLaunchPlan plan = HullbreachDirectLaunch.Plan;
            DefaultListenAddress = NetworkEndpoint.AnyIpv4;
            DefaultConnectAddress = NetworkEndpoint.LoopbackIpv4;

            switch (plan.Role)
            {
                case LaunchRole.Client:
                    if (!DetermineIfBootstrappingEnabled()) return false;
                    // No autoconnect: in a ClientAndServer player NetCode forces loopback,
                    // so the client dials the plan's address itself.
                    AutoConnectPort = 0;
                    World client = CreateClientWorld("ClientWorld");
                    RequestConnect(client, plan);
                    break;
                case LaunchRole.Server:
                    if (!DetermineIfBootstrappingEnabled()) return false;
                    AutoConnectPort = plan.Port;
                    CreateServerWorld("ServerWorld");
                    break;
                case LaunchRole.Host:
                    AutoConnectPort = plan.Port;
                    if (!base.Initialize(defaultWorldName)) return false;
                    break;
                default:
                    AutoConnectPort = 0;
                    if (!base.Initialize(defaultWorldName)) return false;
                    break;
            }

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

        static void RequestConnect(World client, DirectLaunchPlan plan)
        {
            // The plan only hands out numeric IPv4 addresses, so this parse cannot fail.
            NetworkEndpoint.TryParse(plan.ConnectAddress, plan.Port, out NetworkEndpoint endpoint);
            Entity request = client.EntityManager.CreateEntity(typeof(NetworkStreamRequestConnect));
            client.EntityManager.SetComponentData(request, new NetworkStreamRequestConnect { Endpoint = endpoint });
            Debug.Log($"Hullbreach direct: client connecting to {plan.ConnectAddress}:{plan.Port}.");
        }
    }
}
