using System;
using Hullbreach.Core;
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

        void OnEnable() => leaveButton.onClick.AddListener(Leave);
        void OnDisable() => leaveButton.onClick.RemoveListener(Leave);

        void Update()
        {
            var replica = HullbreachNetCodeClient.Replica;
            int shipCount = replica?.Ships.Count ?? 0;
            string mode = HullbreachNetworkGameplayController.IsBuildMode ? "BUILD" : "FLIGHT";
            string controls = HullbreachNetworkGameplayController.IsBuildMode
                ? $"1-7 select - click place - right-click remove - Tab fly\n{HullbreachNetworkGameplayController.BuildStatus}"
                : $"{ShipControls.ThrustSteerLine}\n{ShipControls.OnlineFireBuildLine}"; // frob:ticket 01M6WV815GYEJ58MTM4HSS4Z6K
            statusText.text = HullbreachNetCodeClient.IsConnected
                ? $"Connected as #{HullbreachNetCodeClient.LocalNetworkId}  -  Replicated ships: {shipCount}  -  {mode}\n{HullbreachPlatformAdmission.StatusLine}\n{controls}"
                : "Waiting for Netcode connection...";
        }

        async void Leave()
        {
            leaveButton.interactable = false;
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
