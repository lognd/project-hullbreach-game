using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hullbreach.Game
{
    // Every uGUI button clicks (FeelFx.PlayClick, which honours the sound-effects setting). A DontDestroyOnLoad
    // watcher tags buttons as scenes and runtime panels create them, so no scene or prefab needs wiring.
    public sealed class ButtonClickSound : MonoBehaviour
    {
        static bool _installed;
        float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _installed = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_installed || !Application.isPlaying) return;
            _installed = true;
            var go = new GameObject("ButtonClickSound");
            DontDestroyOnLoad(go);
            go.AddComponent<ButtonClickSound>();
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.4f;
            foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.GetComponent<Tag>() == null) b.gameObject.AddComponent<Tag>();
        }

        sealed class Tag : MonoBehaviour, IPointerClickHandler, ISubmitHandler
        {
            public void OnPointerClick(PointerEventData e) => Click();
            public void OnSubmit(BaseEventData e) => Click();

            void Click()
            {
                var b = GetComponent<Button>();
                if (b != null && b.IsInteractable()) FeelFx.PlayClick();
            }
        }
    }
}
