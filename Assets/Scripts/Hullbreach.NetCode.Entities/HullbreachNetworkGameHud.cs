using System;
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
            statusText.text = HullbreachNetCodeClient.IsConnected
                ? $"Connected as #{HullbreachNetCodeClient.LocalNetworkId}  •  Replicated ships: {shipCount}"
                : "Waiting for Netcode connection...";
        }

        async void Leave()
        {
            leaveButton.interactable = false;
            try
            {
                await HullbreachLobbyService.Instance.LeaveAndReturnToLobbyAsync();
            }
            catch (Exception exception)
            {
                leaveButton.interactable = true;
                statusText.text = exception.Message;
                Debug.LogException(exception);
            }
        }
    }
}
