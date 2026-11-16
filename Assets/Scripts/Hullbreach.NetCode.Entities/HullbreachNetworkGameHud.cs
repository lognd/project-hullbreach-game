using System;
using Hullbreach.Core;
using Hullbreach.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>Small editable uGUI diagnostic/status panel for the network gameplay scene.</summary>
    [AddComponentMenu("Hullbreach/Online/Network Game HUD")]
    public sealed class HullbreachNetworkGameHud : MonoBehaviour
    {
        [SerializeField] TMP_Text statusText;
        [SerializeField] Button leaveButton;

        // Width kept free on the right for the Leave button, so the controls line wraps instead of running under it.
        const float LeaveButtonReserve = 120f;

        void OnEnable()
        {
            leaveButton.onClick.AddListener(Leave);
            var rect = statusText.rectTransform;
            if (rect.anchorMax.x >= 0.99f) rect.offsetMax = new Vector2(-LeaveButtonReserve, rect.offsetMax.y);
            statusText.textWrappingMode = TextWrappingModes.Normal;
        }

        void OnDisable() => leaveButton.onClick.RemoveListener(Leave);

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) _debug = !_debug;
            var replica = HullbreachNetCodeClient.Replica;
            int shipCount = replica?.Ships.Count ?? 0;
            bool build = HullbreachNetworkGameplayController.IsBuildMode;
            string mode = build ? "BUILD" : "FLIGHT";
            string controls = build
                ? "1-6 pick  X remove  Tab fly"
                : $"{ShipControls.ThrustSteerLine}";
            if (!HullbreachNetCodeClient.IsConnected) { statusText.text = "Waiting for Netcode connection..."; return; }
            statusText.text = _debug
                ? $"{mode}  #{HullbreachNetCodeClient.LocalNetworkId}  ships {shipCount}  {HullbreachPlatformAdmission.StatusLine}\n{controls}"
                : $"{mode}\n{controls}"; // frob:ticket 01M6WV815GYEJ58MTM4HSS4Z6K
        }

        bool _debug;

        async void Leave()
        {
            leaveButton.interactable = false;
            HullbreachNetCodeClient.SendLeave(); // a deliberate leave: the server frees the ship at once
            if (HullbreachMatchExit.IsLocalMatch && !HullbreachDirectLaunch.Plan.IsDirect && HullbreachResultsOverlay.Instance != null)
            {
                HullbreachResultsOverlay.Instance.LeaveLocalToLobby(); // a bot or LAN match: rebuild the worlds, then the lobby
                return;
            }
            // A direct/LAN match has no lobby to go back to, so Leave closes the game.
            if (HullbreachDirectLaunch.Plan.IsDirect)
            {
                Debug.Log("Hullbreach direct: leaving the match closes the game.");
                Application.Quit();
                return;
            }
            try
            {
                await HullbreachLobbyService.Instance.LeaveAndReturnToLobbyAsync();
            }
            catch (Exception exception)
            {
                leaveButton.interactable = true;
                statusText.text = Hullbreach.Platform.FriendlyError.From(exception.Message); // frob:ticket 01M6WV5HP3KVSZNJT6H99MFS3Z
                Debug.LogException(exception);
            }
        }
    }
}
