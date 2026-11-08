using Hullbreach.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    // Inspector-wired title screen (S26-1..3): Play, Build, Settings, Sign in, Quit.
    // The labels and the signed-in line come from TitleMenuModel; scene names are inspector fields.
    // frob:ticket 01M3DG5Y1D78FFHNKW4AB7YB9B
    // frob:ticket 01M3DG5Y1ECEZ8A5Y7AJT2NM7X
    // frob:ticket 01M3DG5Y1FGWV5H8MC0MW0A41P
    // frob:doc docs/platform-link.md#title-screen
    [AddComponentMenu("Hullbreach/Online/Title View")]
    public sealed class HullbreachTitleView : MonoBehaviour
    {
        [SerializeField] Button playButton;
        [SerializeField] Button buildButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button signInButton;
        [SerializeField] Button quitButton;
        [SerializeField] TMP_Text accountText;
        [SerializeField] HullbreachSettingsView settings;
        [SerializeField] string playScene = "LobbyScene";
        [SerializeField] string buildScene = "DemoScene";
        [SerializeField] string signInScene = "LobbyScene";

        void OnEnable()
        {
            // Labels and order come from the model so the tests cover them.
            var items = TitleMenuModel.Items(PlatformSession.IsSignedIn);
            Bind(playButton, items[0], () => Load(playScene));
            Bind(buildButton, items[1], () => Load(buildScene));
            Bind(settingsButton, items[2], settings.Open);
            Bind(signInButton, items[3], () => Load(signInScene));
            Bind(quitButton, items[4], Quit);
            accountText.text = TitleMenuModel.AccountLine(PlatformSession.IsSignedIn, PlatformSession.IsOffline,
                PlatformSession.Username);
            HullbreachTabNavigation tabs = GetComponent<HullbreachTabNavigation>();
            if (tabs == null) tabs = gameObject.AddComponent<HullbreachTabNavigation>();
            tabs.SetOrder(HullbreachPlatformRuntime.Log, playButton, buildButton, settingsButton, signInButton, quitButton);
        }

        void OnDisable()
        {
            foreach (Button b in new[] { playButton, buildButton, settingsButton, signInButton, quitButton })
                b.onClick.RemoveAllListeners();
        }

        static void Bind(Button button, TitleItem item, UnityEngine.Events.UnityAction action)
        {
            button.GetComponentInChildren<TMP_Text>().text = item.Label;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        static void Load(string scene)
        {
            Debug.Log("Hullbreach title: loading " + scene + ".");
            SceneManager.LoadScene(scene);
        }

        static void Quit()
        {
            Debug.Log("Hullbreach title: quit requested.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
