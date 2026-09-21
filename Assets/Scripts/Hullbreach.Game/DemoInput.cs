using UnityEngine;

namespace Hullbreach.Game
{
    /// <summary>
    /// Every player intent the demo scene reads, behind one interface so a
    /// play-mode test can script it. UnityEngine.Input cannot be driven from
    /// a test (there is no way to synthesise a key press the legacy Input
    /// Manager will report), so the only way to prove the demo is playable is
    /// to make "where input comes from" a seam instead of a hardcoded call.
    ///
    /// Level-triggered axes are read every frame; the *Pressed members are
    /// EDGE-triggered and must report true for exactly one Update, the same
    /// contract Input.GetKeyDown has, because ShipController latches them.
    /// </summary>
    public interface IDemoInput
    {
        /// <summary>Main thrust axis, -1 (reverse) to +1 (forward).</summary>
        float ThrustAxis { get; }

        /// <summary>Steering axis, -1 to +1.</summary>
        float Steer { get; }

        /// <summary>Fire was pressed this frame (edge, not held).</summary>
        bool FirePressed { get; }

        /// <summary>Build/Fly toggle was pressed this frame (edge).</summary>
        bool TogglePressed { get; }

        /// <summary>Reset-to-start was pressed this frame (edge).</summary>
        bool ResetPressed { get; }

        /// <summary>Cycle-overlay was pressed this frame (edge).</summary>
        bool OverlayPressed { get; }
    }

    /// <summary>
    /// The shipping implementation: the legacy Input Manager bindings the
    /// demo has always used (W/S or Up/Down, A/D or Left/Right, Space, Tab,
    /// R, O). Stateless, so one shared instance serves every component.
    /// </summary>
    public sealed class LegacyDemoInput : IDemoInput
    {
        /// <summary>Shared instance; this type holds no state, so there is
        /// no reason for every component to allocate its own.</summary>
        public static readonly LegacyDemoInput Instance = new LegacyDemoInput();

        // TODO [A5]: Migrate to the new Input System alongside S27
        //            (rebindable keys). activeInputHandler is currently 2
        //            ("Both"), so these legacy calls still work. Confining
        //            them to this one class is the point of the seam: the
        //            migration becomes a second IDemoInput, not a sweep.

        /// <summary>Raw (unsmoothed) Vertical axis; ShipBody does its own
        /// throttle ramping and must not be double-smoothed on top.</summary>
        public float ThrustAxis => Input.GetAxisRaw("Vertical");

        /// <summary>Raw (unsmoothed) Horizontal axis.</summary>
        public float Steer => Input.GetAxisRaw("Horizontal");

        /// <summary>Space or the Fire1 binding, edge-triggered.</summary>
        public bool FirePressed => Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Fire1");

        /// <summary>Tab, edge-triggered.</summary>
        public bool TogglePressed => Input.GetKeyDown(KeyCode.Tab);

        /// <summary>R, edge-triggered.</summary>
        public bool ResetPressed => Input.GetKeyDown(KeyCode.R);

        /// <summary>O, edge-triggered.</summary>
        public bool OverlayPressed => Input.GetKeyDown(KeyCode.O);
    }

    /// <summary>
    /// A scripted IDemoInput whose every member is a settable field, for
    /// play-mode tests. The edge-triggered members auto-clear after one read
    /// so a test can write `input.FirePressed = true` and get exactly the
    /// one-frame pulse a real key press produces, rather than a key that is
    /// held down forever.
    /// </summary>
    public sealed class ScriptedDemoInput : IDemoInput
    {
        /// <summary>Held thrust, -1 to +1; persists until the test changes it.</summary>
        public float Thrust;

        /// <summary>Held steer, -1 to +1; persists until the test changes it.</summary>
        public float SteerAxis;

        bool _fire;
        bool _toggle;
        bool _reset;
        bool _overlay;

        /// <summary>Queue a one-frame fire press.</summary>
        public void PressFire() => _fire = true;

        /// <summary>Queue a one-frame Build/Fly toggle press.</summary>
        public void PressToggle() => _toggle = true;

        /// <summary>Queue a one-frame reset press.</summary>
        public void PressReset() => _reset = true;

        /// <summary>Queue a one-frame overlay-cycle press.</summary>
        public void PressOverlay() => _overlay = true;

        float IDemoInput.ThrustAxis => Thrust;
        float IDemoInput.Steer => SteerAxis;
        bool IDemoInput.FirePressed => Consume(ref _fire);
        bool IDemoInput.TogglePressed => Consume(ref _toggle);
        bool IDemoInput.ResetPressed => Consume(ref _reset);
        bool IDemoInput.OverlayPressed => Consume(ref _overlay);

        static bool Consume(ref bool flag)
        {
            bool was = flag;
            flag = false;
            return was;
        }
    }
}
