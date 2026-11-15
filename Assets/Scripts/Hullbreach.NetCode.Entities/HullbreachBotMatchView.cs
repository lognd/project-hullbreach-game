using Hullbreach.Bots;
using Hullbreach.Net;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>Starts a local match against a bot: the lobby's "Play vs Bot" panel, the Cadet Trial, and the queue's bot offer.</summary>
    public static class HullbreachBotMatch
    {
        /// <summary>Hosts the match on loopback with a bot opponent. Needs the lobby launch's idle worlds.</summary>
        public static bool Start(BotDifficulty difficulty, bool trial, out string error)
        {
            HullbreachSparring.Request(difficulty);
            HullbreachSparring.Trial = trial;
            bool ok = HullbreachLanLobby.TryHost(DirectLaunchPlan.DefaultPort, out error);
            if (!ok) { HullbreachSparring.Requested = false; HullbreachSparring.Trial = false; }
            return ok;
        }
    }

    /// <summary>
    /// Lobby overlay (UX P0-4/P0-5): "Play vs Bot" with an Easy / Normal / Hard choice, and the Cadet Trial (Easy bot with
    /// the controls spelled out in the match feed). Built at runtime so no scene needs rebuilding. Bot matches are unranked.
    /// </summary>
    public sealed class HullbreachBotMatchView : MonoBehaviour
    {
        TextMeshProUGUI _note;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Spawn(scene);

        // The panel lives in the lobby scene only, and comes back each time the lobby loads (after a match).
        static void Spawn(Scene scene)
        {
            if (scene.name != "LobbyScene" || FindFirstObjectByType<HullbreachBotMatchView>() != null) return;
            new GameObject("BotMatchMenu").AddComponent<HullbreachBotMatchView>();
        }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 0f);
            prt.anchoredPosition = new Vector2(16f, 16f);
            prt.sizeDelta = new Vector2(430f, 190f);
            panel.GetComponent<Image>().color = new Color(0.04f, 0.1f, 0.16f, 0.9f);

            Label(prt, "PLAY VS BOT  (unranked)", 24, new Vector2(12f, -10f), new Vector2(400f, 34f));
            Button(prt, "Easy", new Vector2(12f, -52f), new Vector2(128f, 44f), () => Go(BotDifficulty.Easy, false));
            Button(prt, "Normal", new Vector2(150f, -52f), new Vector2(128f, 44f), () => Go(BotDifficulty.Normal, false));
            Button(prt, "Hard", new Vector2(288f, -52f), new Vector2(128f, 44f), () => Go(BotDifficulty.Hard, false));
            Button(prt, "CADET TRIAL  (learn to fly and shoot)", new Vector2(12f, -104f), new Vector2(404f, 44f), () => Go(BotDifficulty.Easy, true));
            _note = Label(prt, string.Empty, 20, new Vector2(12f, -154f), new Vector2(404f, 30f));
        }

        void Go(BotDifficulty difficulty, bool trial)
        {
            if (!HullbreachBotMatch.Start(difficulty, trial, out string error))
                _note.text = "Cannot start: " + Hullbreach.Platform.FriendlyError.From(error);
        }

        static TextMeshProUGUI Label(Transform parent, string text, float size, Vector2 pos, Vector2 box)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.raycastTarget = false; t.enableWordWrapping = false;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = box;
            return t;
        }

        static void Button(Transform parent, string text, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.35f, 0.5f, 1f);
            go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(onClick);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = Label(go.transform, text, 20, Vector2.zero, size);
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = FontStyles.Bold;
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
