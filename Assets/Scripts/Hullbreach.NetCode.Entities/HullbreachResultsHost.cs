using Hullbreach.Match;
using Hullbreach.Platform;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    // Thin glue: owns a ResultsFlow that listens to MatchEndFeed.Shared and shows the results view;
    // rematch / lobby picks go to HullbreachLobbyService. All logic lives in ResultsFlow.
    // frob:ticket 01M5VF1PBQZ9D0Y25J0PQB6Q8N
    // frob:doc docs/reference/hullbreach-match.md#resultsflow
    [AddComponentMenu("Hullbreach/Match/Results Host")]
    public sealed class HullbreachResultsHost : MonoBehaviour
    {
        [SerializeField] HullbreachResultsView view;

        ResultsFlow _flow;

        void OnEnable()
        {
            _flow = new ResultsFlow(view, Rematch, ReturnToLobby);
            _flow.Attach(MatchEndFeed.Shared);
        }

        void OnDisable()
        {
            _flow?.Dispose();
            _flow = null;
        }

        // Only the lobby host can restart the game scene; a guest waits for the host's start.
        void Rematch()
        {
            HullbreachLobbyService lobby = HullbreachLobbyService.Instance;
            if (lobby.Session != null && lobby.Session.IsHost) _ = lobby.StartGameAsync();
            else Debug.Log("Hullbreach results: rematch requested; waiting for the lobby host to start.");
        }

        void ReturnToLobby() => _ = HullbreachLobbyService.Instance.LeaveAndReturnToLobbyAsync();
    }
}
