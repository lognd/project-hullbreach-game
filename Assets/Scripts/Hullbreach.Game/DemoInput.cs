using UnityEngine;

namespace Hullbreach.Game
{
    // UnityEngine.Input cannot be driven from a test (there is no way to
    // synthesise a key press the legacy Input Manager will report), so the
    // only way to prove the demo is playable is to make "where input comes
    // from" a seam instead of a hardcoded call. The *Pressed members are
    // EDGE-triggered (true for exactly one Update), the same contract
    // Input.GetKeyDown has, because ShipController latches them.
    // frob:doc docs/reference/hullbreach-game.md#idemoinput
    public interface IDemoInput
    {
        // frob:doc docs/reference/hullbreach-game.md#idemoinput
        float ThrustAxis { get; }

        // frob:doc docs/reference/hullbreach-game.md#idemoinput
        float Steer { get; }

        // frob:doc docs/reference/hullbreach-game.md#idemoinput
        bool FirePressed { get; }

        // frob:doc docs/reference/hullbreach-game.md#idemoinput
        bool TogglePressed { get; }

        // frob:doc docs/reference/hullbreach-game.md#idemoinput
        bool ResetPressed { get; }

        // frob:doc docs/reference/hullbreach-game.md#idemoinput
        bool OverlayPressed { get; }
    }

    // The shipping implementation: the legacy Input Manager bindings the
    // demo has always used (W/S or Up/Down, A/D or Left/Right, Space, Tab,
    // R, O). Stateless, so one shared instance serves every component.
    // frob:doc docs/reference/hullbreach-game.md#legacydemoinput
    public sealed class LegacyDemoInput : IDemoInput
    {
        // frob:doc docs/reference/hullbreach-game.md#legacydemoinput
        public static readonly LegacyDemoInput Instance = new LegacyDemoInput();

        // TODO [A5]: Migrate to the new Input System alongside S27
        //            (rebindable keys). activeInputHandler is currently 2
        //            ("Both"), so these legacy calls still work. Confining
        //            them to this one class is the point of the seam: the
        //            migration becomes a second IDemoInput, not a sweep.

        // Raw (unsmoothed): ShipBody does its own throttle ramping and must
        // not be double-smoothed on top.
        // frob:doc docs/reference/hullbreach-game.md#legacydemoinput
        public float ThrustAxis => Input.GetAxisRaw("Vertical");

        // frob:doc docs/reference/hullbreach-game.md#legacydemoinput
        public float Steer => Input.GetAxisRaw("Horizontal");

        // frob:doc docs/reference/hullbreach-game.md#legacydemoinput
        public bool FirePressed => Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Fire1");

        // frob:doc docs/reference/hullbreach-game.md#legacydemoinput
        public bool TogglePressed => Input.GetKeyDown(KeyCode.Tab);

        // frob:doc docs/reference/hullbreach-game.md#legacydemoinput
        public bool ResetPressed => Input.GetKeyDown(KeyCode.R);

        // frob:doc docs/reference/hullbreach-game.md#legacydemoinput
        public bool OverlayPressed => Input.GetKeyDown(KeyCode.O);
    }

    // A scripted IDemoInput whose every member is a settable field, for
    // play-mode tests. The edge-triggered members auto-clear after one read
    // so a test can write `input.FirePressed = true` and get exactly the
    // one-frame pulse a real key press produces, rather than a key that is
    // held down forever.
    // frob:doc docs/reference/hullbreach-game.md#scripteddemoinput
    public sealed class ScriptedDemoInput : IDemoInput
    {
        // frob:doc docs/reference/hullbreach-game.md#scripteddemoinput
        public float Thrust;

        // frob:doc docs/reference/hullbreach-game.md#scripteddemoinput
        public float SteerAxis;

        bool _fire;
        bool _toggle;
        bool _reset;
        bool _overlay;

        // frob:doc docs/reference/hullbreach-game.md#scripteddemoinput
        public void PressFire() => _fire = true;

        // frob:doc docs/reference/hullbreach-game.md#scripteddemoinput
        public void PressToggle() => _toggle = true;

        // frob:doc docs/reference/hullbreach-game.md#scripteddemoinput
        public void PressReset() => _reset = true;

        // frob:doc docs/reference/hullbreach-game.md#scripteddemoinput
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
