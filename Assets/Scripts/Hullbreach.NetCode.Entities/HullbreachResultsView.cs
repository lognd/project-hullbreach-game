using Hullbreach.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    // Inspector-wired uGUI results screen shown when a match ends (S46-3); all logic is in
    // ResultsScreenModel. Call Show with the finished match; the buttons forward to the model.
    // frob:ticket 01M3DG5Y37VGGE65ERV1QQDH8X
    // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
    [AddComponentMenu("Hullbreach/Match/Results View")]
    public sealed class HullbreachResultsView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text headlineText;
        [SerializeField] TMP_Text reasonText;
        [SerializeField] TMP_Text durationText;
        [SerializeField] TMP_Text playerAText;
        [SerializeField] TMP_Text playerBText;
        [SerializeField] Button rematchButton;
        [SerializeField] Button lobbyButton;

        ResultsScreenModel _model;

        // Shows the screen for a finished match; replaces any earlier one.
        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public void Show(ResultsScreenModel model)
        {
            Unbind();
            _model = model;
            rematchButton.onClick.AddListener(_model.Rematch);
            lobbyButton.onClick.AddListener(_model.ReturnToLobby);
            headlineText.text = model.Headline;
            reasonText.text = model.Reason;
            durationText.text = "Duration " + model.Duration;
            playerAText.text = Lines(model.PlayerA);
            playerBText.text = Lines(model.PlayerB);
            root.SetActive(true);
        }

        // Hides the screen; the owner calls it after acting on the model's choice events.
        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public void Hide()
        {
            Unbind();
            root.SetActive(false);
        }

        void OnDisable() => Unbind();

        void Unbind()
        {
            if (_model == null) return;
            rematchButton.onClick.RemoveListener(_model.Rematch);
            lobbyButton.onClick.RemoveListener(_model.ReturnToLobby);
            _model = null;
        }

        static string Lines(ResultsPlayerRow p) =>
            (p.IsWinner ? p.Name + " (winner)" : p.Name)
            + "\nDamage dealt: " + p.DamageDealt
            + "\nBlocks destroyed: " + p.BlocksDestroyed
            + "\nBlocks placed: " + p.BlocksPlaced
            + "\nTime alive: " + p.TimeAlive;
    }
}
