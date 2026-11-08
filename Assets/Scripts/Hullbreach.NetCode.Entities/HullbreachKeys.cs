using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Hullbreach.NetCode.Entities
{
    // Backend-neutral key reads for the menu screens (legacy Input Manager or Input System).
    // frob:doc docs/netcode.md#lobby-keyboard-flow
    // frob:ticket 01M6WV5HP3KVSZNJT6H99MFS3Z
    public static class HullbreachKeys
    {
        // True on the frame Esc goes down.
        public static bool EscapePressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }
}
