using System.IO;
using Hullbreach.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    // Inspector-wired settings screen (S27-1, S37-2); all logic is in SettingsScreenModel.
    // Widgets live in SettingsCanvas.prefab so they can be restyled without code changes.
    // frob:ticket 01M3DG5Y1GSP9K4A0MY9MSHS37
    // frob:ticket 01M3DG5Y2DX8GJYANMCGM0B19P
    // frob:doc docs/design/ui-port.md#settings-screen
    [AddComponentMenu("Hullbreach/Online/Settings View")]
    public sealed class HullbreachSettingsView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] Slider masterSlider;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider effectsSlider;
        [SerializeField] TMP_Text masterValue;
        [SerializeField] TMP_Text musicValue;
        [SerializeField] TMP_Text effectsValue;
        [SerializeField] Button resolutionPrevious;
        [SerializeField] Button resolutionNext;
        [SerializeField] TMP_Text resolutionText;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] Toggle colorblindToggle;
        [SerializeField] Button applyButton;
        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text messageText;

        SettingsScreenModel _model;
        bool _loading;

        // True while the panel is showing.
        // frob:doc docs/design/ui-port.md#settings-screen
        public bool IsOpen => root != null && root.activeSelf;

        // Loads the saved settings into the controls and shows the panel.
        // frob:doc docs/design/ui-port.md#settings-screen
        public void Open()
        {
            var store = new GameSettingsStore(Path.Combine(Application.persistentDataPath, "settings.txt"), Debug.Log);
            _model = new SettingsScreenModel(store.Load(), store, Debug.Log);
            _model.Changed += Refresh;
            root.SetActive(true);
            Refresh();
            Debug.Log("Hullbreach settings: screen opened.");
        }

        // Hides the panel; unsaved edits are dropped.
        // frob:doc docs/design/ui-port.md#settings-screen
        public void Close()
        {
            if (_model != null) _model.Changed -= Refresh;
            _model = null;
            root.SetActive(false);
            Debug.Log("Hullbreach settings: screen closed.");
        }

        void Awake()
        {
            masterSlider.onValueChanged.AddListener(v => { if (!_loading) _model.SetMaster(v); });
            musicSlider.onValueChanged.AddListener(v => { if (!_loading) _model.SetMusic(v); });
            effectsSlider.onValueChanged.AddListener(v => { if (!_loading) _model.SetEffects(v); });
            resolutionPrevious.onClick.AddListener(() => _model.StepResolution(-1));
            resolutionNext.onClick.AddListener(() => _model.StepResolution(1));
            fullscreenToggle.onValueChanged.AddListener(v => { if (!_loading) _model.SetFullscreen(v); });
            colorblindToggle.onValueChanged.AddListener(v => { if (!_loading) _model.SetColorblind(v); });
            applyButton.onClick.AddListener(Apply);
            closeButton.onClick.AddListener(Close);
            root.SetActive(false);
        }

        // Saves, then pushes the new resolution to the player window.
        void Apply()
        {
            if (!_model.Apply()) return;
            GameSettings s = _model.Settings;
            Screen.SetResolution(s.ResolutionWidth, s.ResolutionHeight, s.Fullscreen);
        }

        void Refresh()
        {
            _loading = true;
            GameSettings s = _model.Settings;
            masterSlider.value = s.MasterVolume;
            musicSlider.value = s.MusicVolume;
            effectsSlider.value = s.EffectsVolume;
            masterValue.text = SettingsScreenModel.PercentLabel(s.MasterVolume);
            musicValue.text = SettingsScreenModel.PercentLabel(s.MusicVolume);
            effectsValue.text = SettingsScreenModel.PercentLabel(s.EffectsVolume);
            resolutionText.text = _model.ResolutionLabel;
            fullscreenToggle.isOn = s.Fullscreen;
            colorblindToggle.isOn = s.ColorblindPalette;
            messageText.text = _model.Message;
            messageText.color = _model.IsError ? BrandColors.Orange : BrandColors.Ink;
            _loading = false;
        }
    }
}
