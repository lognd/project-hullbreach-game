using System;
using Hullbreach.Platform;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Hullbreach.NetCode.Entities
{
    // Tab / Shift+Tab walk the EventSystem selection through a fixed widget order; ordering is TabOrder.
    // Also sets explicit up/down navigation so the arrow keys follow the same order.
    // frob:doc docs/platform-link.md#sign-in-screen
    // frob:ticket 01M5XZJRNREX0M9DF1M6PE7G7V
    [DisallowMultipleComponent]
    public sealed class HullbreachTabNavigation : MonoBehaviour
    {
        Selectable[] _order = Array.Empty<Selectable>();
        bool[] _usable = Array.Empty<bool>();
        IPlatformLog _log = NullPlatformLog.Instance;

        // Sets the Tab order (first to last) and wires matching explicit up/down navigation.
        // frob:doc docs/platform-link.md#sign-in-screen
        public void SetOrder(IPlatformLog log, params Selectable[] order)
        {
            _log = log ?? NullPlatformLog.Instance;
            _order = order;
            _usable = new bool[order.Length];
            ApplyExplicitNavigation();
            _log.Info("Hullbreach tab navigation: order set, " + order.Length + " widgets");
        }

        // Every widget gets up = previous, down = next (wrapping), the same walk Tab takes.
        void ApplyExplicitNavigation()
        {
            var all = new bool[_order.Length];
            Array.Fill(all, true);
            for (int i = 0; i < _order.Length; i++)
            {
                Selectable s = _order[i];
                if (s == null)
                {
                    continue;
                }

                int up = TabOrder.Step(all, i, true);
                int down = TabOrder.Step(all, i, false);
                Navigation nav = s.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = up >= 0 && up != i ? _order[up] : null;
                nav.selectOnDown = down >= 0 && down != i ? _order[down] : null;
                s.navigation = nav;
            }
        }

        void Update()
        {
            if (_order.Length == 0 || EventSystem.current == null || !TabPressed(out bool backward))
            {
                return;
            }

            for (int i = 0; i < _order.Length; i++)
            {
                Selectable s = _order[i];
                _usable[i] = s != null && s.IsActive() && s.IsInteractable();
            }

            GameObject current = EventSystem.current.currentSelectedGameObject;
            int from = -1;
            for (int i = 0; i < _order.Length; i++)
            {
                if (_order[i] != null && current != null && _order[i].gameObject == current)
                {
                    from = i;
                    break;
                }
            }

            int to = TabOrder.Step(_usable, from, backward);
            if (to < 0)
            {
                _log.Info("Hullbreach tab navigation: no usable widget to move to");
                return;
            }

            _log.Info("Hullbreach tab navigation: " + (backward ? "back" : "forward") + " to " + _order[to].name);
            EventSystem.current.SetSelectedGameObject(_order[to].gameObject);
        }

        // Reads Tab and Shift from whichever input backend the player settings enable.
        static bool TabPressed(out bool backward)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            backward = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return Input.GetKeyDown(KeyCode.Tab);
#elif ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            backward = keyboard != null && keyboard.shiftKey.isPressed;
            return keyboard != null && keyboard.tabKey.wasPressedThisFrame;
#else
            backward = false;
            return false;
#endif
        }
    }
}
