using UnityEngine;

namespace Hullbreach.Game
{
    // A seam so UnityEngine.Input can be swapped for a script in tests;
    // see the reference page. *Pressed members are EDGE-triggered.
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

    // The legacy Input Manager bindings the demo has always used.
    // Stateless, so one shared instance serves every component.
    // frob:doc docs/reference/hullbreach-game.md#legacydemoinput
    public sealed class LegacyDemoInput : IDemoInput
    {
        // frob:doc docs/reference/hullbreach-game.md#legacydemoinput
        public static readonly LegacyDemoInput Instance = new LegacyDemoInput();

        // TODO [A5]: migrate to the new Input System alongside S27.

        // Raw (unsmoothed): ShipBody does its own throttle ramping.
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

    // A settable IDemoInput for play-mode tests; edge-triggered members
    // auto-clear after one read. See the reference page.
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
