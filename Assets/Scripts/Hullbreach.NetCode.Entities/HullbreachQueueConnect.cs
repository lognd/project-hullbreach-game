using System.Collections.Generic;
using System.Net;
using Hullbreach.Net;
using Hullbreach.Platform;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hullbreach.NetCode.Entities
{
    // Turns a found match into a connection: ConnectRequest, name resolution, dial, scene load (S29-3b).
    // The decisions live in the engine-free ConnectRequest/ConnectTarget; this only does the Unity calls.
    // Attaches itself to the lobby's queue view at runtime (no scene or prefab edit), and presents the match id as a
    // JoinTicket RPC once the connection is up (HullbreachJoinTicketClientSystem).
    // frob:ticket 01M5XACEYZRD4KJYBEJYTVQZRX
    // frob:doc docs/netcode.md#queue-connect-flow
    [AddComponentMenu("Hullbreach/Online/Queue Connect")]
    public sealed class HullbreachQueueConnect : MonoBehaviour
    {
        [SerializeField] HullbreachQueueView queueView;

        // Cosmetic skin ids to present. The platform's queue status carries skin slugs, but MatchHandoff does not keep
        // them and no ship renders a skin yet, so this stays empty and everyone plays the default skin.
        readonly List<string> _equippedSkins = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Spawn(scene);

        static void Spawn(Scene scene)
        {
            if (scene.name != "LobbyScene") return;
            HullbreachQueueTicket.Clear(); // a ticket belongs to one queue match, never to the next LAN or bot game
            if (FindFirstObjectByType<HullbreachQueueConnect>(FindObjectsInactive.Include) != null) return;
            new GameObject("QueueConnect").AddComponent<HullbreachQueueConnect>();
        }

        void Awake()
        {
            if (queueView == null) queueView = FindFirstObjectByType<HullbreachQueueView>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            if (queueView != null) queueView.Matched += OnMatched;
        }

        void OnDisable()
        {
            if (queueView != null) queueView.Matched -= OnMatched;
        }

        void OnMatched(MatchHandoff handoff)
        {
            ConfigResult<ConnectRequest> request = ConnectRequest.FromHandoff(handoff, _equippedSkins);
            if (!request.IsOk)
            {
                Debug.LogWarning("Hullbreach connect: match handoff rejected: " + request.Error);
                return;
            }

            // Blocking DNS on the main thread: acceptable for one lookup at match start, and the
            // resolver is injectable if it ever needs to move off-thread.
            ConfigResult<ConnectTarget> target = ConnectTarget.Resolve(request.Value, ResolveHost);
            if (!target.IsOk)
            {
                Debug.LogWarning("Hullbreach connect: " + target.Error);
                return;
            }

            HullbreachQueueTicket.Set(request.Value.MatchId);
            if (!HullbreachLanLobby.TryConnect(target.Value, out string error))
            {
                HullbreachQueueTicket.Clear();
                Debug.LogWarning("Hullbreach connect: " + error);
            }
        }

        static IReadOnlyList<string> ResolveHost(string host)
        {
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            var text = new List<string>(addresses.Length);
            foreach (IPAddress address in addresses) text.Add(address.ToString());
            return text;
        }
    }
}
