using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// A key plus optional modifiers, written in config as "F1" or "F1 + LeftControl" (the format BepInEx 5's
    /// KeyboardShortcut used, which BepInEx 6 IL2CPP doesn't have).
    /// </summary>
    internal readonly struct Shortcut
    {
        public readonly KeyCode MainKey;
        public readonly KeyCode[] Modifiers;

        public Shortcut(KeyCode main, params KeyCode[] modifiers)
        {
            MainKey = main;
            Modifiers = modifiers ?? Array.Empty<KeyCode>();
        }

        public static Shortcut Parse(string text, KeyCode fallback = KeyCode.F1)
        {
            var parts = (text ?? "").Split(new[] { '+', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var keys = new List<KeyCode>();
            foreach (var part in parts)
            {
                if (Enum.TryParse<KeyCode>(part, true, out var key) && key != KeyCode.None)
                    keys.Add(key);
            }
            if (keys.Count == 0)
                return new Shortcut(fallback);
            return new Shortcut(keys[0], keys.GetRange(1, keys.Count - 1).ToArray());
        }

        public override string ToString() => Modifiers.Length == 0 ? MainKey.ToString() : MainKey + " + " + string.Join(" + ", Modifiers);
    }
}
