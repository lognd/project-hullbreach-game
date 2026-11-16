using System;
using Hullbreach.Net;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hullbreach.NetCode.Entities
{
    using World = Unity.Entities.World;
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

        // Opening MultiplayerGame straight from the editor has no lobby to host it, so it would wait for a connection
        // forever. Host it on loopback instead (editor only; a player build always starts from the lobby or a direct role).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void HostWhenOpenedDirectly()
        {
#if UNITY_EDITOR
            if (Plan.IsDirect || SceneManager.GetActiveScene().name != DirectLaunchPlan.GameScene) return;
            if (HullbreachLanLobby.TryHostHere(DirectLaunchPlan.DefaultPort, out string error))
                Debug.Log("Hullbreach: MultiplayerGame opened directly; hosting on loopback.");
            else
                Debug.LogWarning("Hullbreach: MultiplayerGame opened directly but cannot host: " + error);
#endif
        }

        // Statics survive play-mode restarts when domain reload is off; start clean each run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_plan = null; HullbreachSparring.Reset(); }
    }

    // Asks the server for a passive stand-in opponent while only one player is connected (-hullbreachSparring, or the
    // queue's bot offer). The server system owns the virtual peer.
    public static class HullbreachSparring
    {
        public const int PeerId = ServerSimulation.MaxPeerId - 1;
        public static bool Requested;
        public static bool Trial; // Cadet Trial: an Easy bot plus the controls spelled out in the feed
        public static Hullbreach.Bots.BotDifficulty Difficulty = Hullbreach.Bots.BotDifficulty.Normal;

        // Shown wherever the opponent is named; a bot match never counts for ranking.
        public static string DisplayName => $"BOT ({Difficulty.ToString().ToUpperInvariant()})";
        public const string UnrankedLabel = "Bot match - unranked";

        public static void Request(Hullbreach.Bots.BotDifficulty difficulty) { Difficulty = difficulty; Requested = true; }

        public static void Reset()
        {
            Requested = Array.IndexOf(Environment.GetCommandLineArgs(), "-hullbreachSparring") >= 0;
            Difficulty = Hullbreach.Bots.BotDifficulty.Normal;
            Trial = false;
        }
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
                    MaxSimulationStepsPerFrame = 4,
                    MaxSimulationStepBatchSize = 1,
                    // Catch up with several real ticks instead of batching them into one long tick (which logs the
                    // 'Server Tick Batching' warning every time a throttled editor frame runs long, and loses input).
                };
                tickRate.ResolveDefaults();
                world.EntityManager.CreateSingleton(tickRate, "Hullbreach NetCode Tick Rate");
            }

            return true;
        }

        static void RequestConnect(Unity.Entities.World client, DirectLaunchPlan plan)
        {
            // The plan only hands out numeric IPv4 addresses, so this parse cannot fail.
            NetworkEndpoint.TryParse(plan.ConnectAddress, plan.Port, out NetworkEndpoint endpoint);
            Entity request = client.EntityManager.CreateEntity(typeof(NetworkStreamRequestConnect));
            client.EntityManager.SetComponentData(request, new NetworkStreamRequestConnect { Endpoint = endpoint });
            HullbreachNetCodeClient.RememberEndpoint(endpoint);
            Debug.Log($"Hullbreach direct: client connecting to {plan.ConnectAddress}:{plan.Port}.");
        }
    }
}
