using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdminMenu
{
    /// <summary>
    /// Reads a <see cref="KeyboardShortcut"/> straight from the keyboard through the Input System, so it works
    /// while the game's own input actions are disabled.
    /// </summary>
    internal static class Hotkey
    {
        private static readonly Dictionary<KeyCode, Key> Renamed = new Dictionary<KeyCode, Key>
        {
            [KeyCode.Return] = Key.Enter,
            [KeyCode.KeypadEnter] = Key.NumpadEnter,
            [KeyCode.LeftControl] = Key.LeftCtrl,
            [KeyCode.RightControl] = Key.RightCtrl,
            [KeyCode.LeftCommand] = Key.LeftMeta,
            [KeyCode.RightCommand] = Key.RightMeta,
            [KeyCode.BackQuote] = Key.Backquote,
            [KeyCode.KeypadPlus] = Key.NumpadPlus,
            [KeyCode.KeypadMinus] = Key.NumpadMinus,
            [KeyCode.KeypadMultiply] = Key.NumpadMultiply,
            [KeyCode.KeypadDivide] = Key.NumpadDivide,
            [KeyCode.KeypadPeriod] = Key.NumpadPeriod,
        };

        public static bool WasPressed(KeyboardShortcut shortcut)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || shortcut.MainKey == KeyCode.None)
                return false;
            if (!TryGetKey(shortcut.MainKey, out var main) || !keyboard[main].wasPressedThisFrame)
                return false;
            foreach (var modifier in shortcut.Modifiers)
            {
                if (!TryGetKey(modifier, out var key) || !keyboard[key].isPressed)
                    return false;
            }
            return true;
        }

        public static bool EscapePressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        public static bool EscapeHeld() => Keyboard.current != null && Keyboard.current.escapeKey.isPressed;

        private static bool TryGetKey(KeyCode code, out Key key)
        {
            if (Renamed.TryGetValue(code, out key))
                return true;

            var name = code.ToString();
            if (name.StartsWith("Alpha"))
                name = "Digit" + name.Substring("Alpha".Length);
            else if (name.StartsWith("Keypad"))
                name = "Numpad" + name.Substring("Keypad".Length);

            return Enum.TryParse(name, true, out key) && key != Key.None;
        }
    }
}
