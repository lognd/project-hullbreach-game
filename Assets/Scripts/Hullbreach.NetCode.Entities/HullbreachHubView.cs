using Hullbreach.Bots;
using Hullbreach.Hud;
using Hullbreach.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// The Play hub: the first thing the lobby scene shows. Play vs Bot (Easy / Normal / Hard), the Cadet Trial, the online
    /// queue, LAN host / join and back to the title. Only the queue needs an account; everything else works as a guest, so
    /// no sign-in form stands between a new player and a match. Built at runtime over the lobby scene, so no scene is rebuilt.
    /// </summary>
    public sealed class HullbreachHubView : MonoBehaviour
    {
        /// <summary>Set by the title's Sign in button: the lobby opens on the sign-in form instead of the hub.</summary>
        public static bool OpenOnSignIn;

        GameObject _hub;
        GameObject _menuButton;
        GameObject _botRow;
        TextMeshProUGUI _note, _account;
        Button _online;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Spawn(scene);

        // The hub lives in the lobby scene only, and comes back each time the lobby loads (after a match).
        static void Spawn(Scene scene)
        {
            if (scene.name != "LobbyScene" || FindFirstObjectByType<HullbreachHubView>() != null) return;
            new GameObject("PlayHub").AddComponent<HullbreachHubView>();
        }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            BuildHub();
            BuildMenuButton();
            if (OpenOnSignIn) { OpenOnSignIn = false; ShowHub(false); }
        }

        void OnEnable() => RefreshAccount();

        void BuildHub()
        {
            _hub = new GameObject("Hub", typeof(RectTransform), typeof(Image));
            _hub.transform.SetParent(transform, false);
            Stretch((RectTransform)_hub.transform);
            _hub.GetComponent<Image>().color = BrandColors.Background;

            var column = new GameObject("Column", typeof(RectTransform));
            column.transform.SetParent(_hub.transform, false);
            var crt = (RectTransform)column.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(520f, 640f);

            Text(crt, "PLAY", 54, FontStyles.Bold, BrandColors.Ink, 300f, 250f, 70f);
            _account = Text(crt, string.Empty, 20, FontStyles.Normal, BrandColors.Muted, 300f, 200f, 34f);

            float y = 140f;
            Button(crt, "PLAY VS BOT", y, true, ToggleBotRow); y -= 66f;
            _botRow = new GameObject("BotRow", typeof(RectTransform));
            _botRow.transform.SetParent(crt, false);
            var rrt = (RectTransform)_botRow.transform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(0f, y); rrt.sizeDelta = new Vector2(480f, 52f);
            for (int tier = 0; tier < 3; tier++)
            {
                BotDifficulty difficulty = (BotDifficulty)tier;
                var b = SmallButton(rrt, HubMenuModel.BotTierNames[tier], (tier - 1) * 164f, 150f, () => StartBot(difficulty, false));
            }
            _botRow.SetActive(false);
            y -= 62f;
            Button(crt, HullbreachCadetTrialView.EverCompleted ? "CADET TRIAL  (replay)" : "CADET TRIAL  (learn to fly)", y, false, () => StartBot(BotDifficulty.Easy, true)); y -= 66f;
            _online = Button(crt, HubMenuModel.OnlineLabel(PlatformSession.IsSignedIn), y, false, OpenOnline); y -= 66f;
            Button(crt, "LAN  HOST / JOIN", y, false, OpenLan); y -= 66f;
            Button(crt, "BACK TO TITLE", y, false, () => SceneManager.LoadScene("TitleScene"));
            _note = Text(crt, string.Empty, 20, FontStyles.Normal, BrandColors.Orange, 300f, -245f, 34f);
        }

        void BuildMenuButton()
        {
            _menuButton = new GameObject("MenuButton", typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button));
            _menuButton.transform.SetParent(transform, false);
            var rt = (RectTransform)_menuButton.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -16f); rt.sizeDelta = new Vector2(160f, 40f);
            _menuButton.GetComponent<Image>().color = BrandColors.Raised;
            _menuButton.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => ShowHub(true));
            var label = Text(rt, "< PLAY MENU", 18, FontStyles.Bold, BrandColors.Cyan, 160f, 0f, 40f);
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            _menuButton.SetActive(false);
        }

        void ShowHub(bool show)
        {
            _hub.SetActive(show);
            _menuButton.SetActive(!show);
            if (show) { _note.text = string.Empty; RefreshAccount(); }
        }

        void RefreshAccount()
        {
            if (_account == null) return;
            _account.text = HubMenuModel.AccountLine(PlatformSession.IsSignedIn, PlatformSession.IsOffline, PlatformSession.Username);
            if (_online != null) _online.GetComponentInChildren<TextMeshProUGUI>().text = HubMenuModel.OnlineLabel(PlatformSession.IsSignedIn);
        }

        void ToggleBotRow() => _botRow.SetActive(!_botRow.activeSelf);

        void StartBot(BotDifficulty difficulty, bool trial)
        {
            if (!HullbreachBotMatch.Start(difficulty, trial, out string error))
                _note.text = "Cannot start: " + FriendlyError.From(error);
        }

        void OpenOnline()
        {
            ShowHub(false);
            if (HubMenuModel.OnlineTarget(PlatformSession.IsSignedIn) == HubDestination.OnlineQueue)
            {
                var queue = FindFirstObjectByType<HullbreachQueueView>();
                if (queue != null) queue.Open();
            }
            // Otherwise the sign-in form behind the hub is what the player now sees.
        }

        void OpenLan()
        {
            ShowHub(false);
            var signIn = FindFirstObjectByType<HullbreachSignInView>();
            if (signIn != null) signIn.ContinueAsGuest();
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static TextMeshProUGUI Text(Transform parent, string text, float size, FontStyles style, Color color, float width, float y, float height)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color; t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y); rt.sizeDelta = new Vector2(Mathf.Max(width, 480f), height);
            return t;
        }

        // Primary buttons: orange for the main action, raised ink for the rest, like the title screen.
        static Button Button(Transform parent, string text, float y, bool primary, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = primary ? BrandColors.Orange : BrandColors.Raised;
            go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(onClick);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y); rt.sizeDelta = new Vector2(480f, 56f);
            var t = Text(rt, text, 24, FontStyles.Bold, primary ? BrandColors.Background : BrandColors.Cyan, 480f, 0f, 56f);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            return go.GetComponent<UnityEngine.UI.Button>();
        }

        static Button SmallButton(Transform parent, string text, float x, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(char.ToUpper(text[0]) + text.Substring(1).ToLower(), typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.35f, 0.5f, 1f);
            go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(onClick);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(width, 48f);
            var t = Text(rt, text, 22, FontStyles.Bold, BrandColors.Ink, width, 0f, 48f);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            return go.GetComponent<UnityEngine.UI.Button>();
        }
    }
}
