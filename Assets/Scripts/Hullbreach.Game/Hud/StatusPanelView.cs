using TMPro;
using UnityEngine;
using Hullbreach.Hud;

namespace Hullbreach.Game
{
    // uGUI view over StatusPanelModel and FlightTelemetryModel (D4): bottom-left,
    // always visible; owns three nested ChannelBar instances and a powerup row list.
    // frob:doc docs/demo-scene.md#the-ugui-hud-u3
    public sealed class StatusPanelView : MonoBehaviour
    {
        [SerializeField] DemoMode demoMode;
        [SerializeField] TMP_Text modeText;
        [SerializeField] TMP_Text[] controlLineTexts;
        [SerializeField] TMP_Text overlayText;
        [SerializeField] TMP_Text massBlocksText;
        [SerializeField] GameObject flightOnlyRoot;
        [SerializeField] TMP_Text speedText;
        [SerializeField] ChannelBar thrustBar;
        [SerializeField] ChannelBar reverseBar;
        [SerializeField] ChannelBar steerBar;
        [SerializeField] RectTransform powerupContainer;
        [SerializeField] TMP_Text powerupRowTemplate;

        TMP_Text[] _powerupRows = System.Array.Empty<TMP_Text>();

        void Awake() => powerupRowTemplate.gameObject.SetActive(false);

        void LateUpdate()
        {
            bool building = demoMode.State == DemoState.Build;
            var ship = demoMode.PlayerShip != null ? demoMode.PlayerShip.Ship : null;
            float mass = ship != null ? ship.Grid.Mass.Total : 0f;
            int blockCount = ship != null ? ship.Grid.Count : 0;
            string overlayName = demoMode.PlayerRenderer != null ? demoMode.PlayerRenderer.Overlay.ToString() : "n/a";
            var powerups = demoMode.ActivePowerups();

            var model = StatusPanelModel.Build(building, overlayName, mass, blockCount, powerups);

            modeText.text = model.ModeLine;
            for (int i = 0; i < controlLineTexts.Length; i++)
            {
                bool active = i < model.ControlLines.Count;
                controlLineTexts[i].gameObject.SetActive(active);
                if (active) controlLineTexts[i].text = model.ControlLines[i];
            }

            bool hasOverlay = model.OverlayLine != null;
            overlayText.gameObject.SetActive(hasOverlay);
            if (hasOverlay) overlayText.text = model.OverlayLine;

            massBlocksText.text = model.MassBlocksLine;

            bool fly = !building;
            flightOnlyRoot.SetActive(fly);
            if (fly && ship != null)
            {
                var telemetry = FlightTelemetryModel.Build(ship.Velocity.x, ship.Velocity.y, ship.AngularVelocity,
                    ship.ForwardThrottleMean, ship.ReverseThrottleMean, ship.SteerThrottleMean);
                speedText.text = telemetry.SpeedLine;
                thrustBar.Set(telemetry.Thrust);
                reverseBar.Set(telemetry.Reverse);
                steerBar.Set(telemetry.Steer);
            }

            SetPowerupRows(model.PowerupLines);
        }

        // Grows the row pool once, but destroys extra rows when the count
        // shrinks: powerups come and go as they expire (spec U3).
        void SetPowerupRows(System.Collections.Generic.IReadOnlyList<string> lines)
        {
            if (lines.Count > _powerupRows.Length)
            {
                var grown = new TMP_Text[lines.Count];
                System.Array.Copy(_powerupRows, grown, _powerupRows.Length);
                for (int i = _powerupRows.Length; i < grown.Length; i++)
                {
                    var row = Instantiate(powerupRowTemplate, powerupContainer);
                    row.gameObject.SetActive(true);
                    grown[i] = row;
                }
                _powerupRows = grown;
            }
            else if (lines.Count < _powerupRows.Length)
            {
                for (int i = lines.Count; i < _powerupRows.Length; i++) Destroy(_powerupRows[i].gameObject);
                var shrunk = new TMP_Text[lines.Count];
                System.Array.Copy(_powerupRows, shrunk, lines.Count);
                _powerupRows = shrunk;
            }

            for (int i = 0; i < lines.Count; i++) _powerupRows[i].text = lines[i];
        }
    }
}
