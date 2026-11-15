using System.Collections.Generic;
using System.Net;
using Hullbreach.Net;
using Hullbreach.Platform;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    // Turns a found match into a connection: ConnectRequest, name resolution, dial, scene load (S29-3b).
    // The decisions live in the engine-free ConnectRequest/ConnectTarget; this only does the Unity calls.
    // Written without running the editor: untested in play mode.
    // frob:ticket 01M5XACEYZRD4KJYBEJYTVQZRX
    // frob:doc docs/netcode.md#queue-connect-flow
    [AddComponentMenu("Hullbreach/Online/Queue Connect")]
    public sealed class HullbreachQueueConnect : MonoBehaviour
    {
        [SerializeField] HullbreachQueueView queueView;

        // Cosmetic skin ids to present; there is no data source for them yet (S29-3c), so this stays empty.
        readonly List<string> _equippedSkins = new List<string>();

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

            if (!HullbreachLanLobby.TryConnect(target.Value, out string error))
                Debug.LogWarning("Hullbreach connect: " + error);
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
