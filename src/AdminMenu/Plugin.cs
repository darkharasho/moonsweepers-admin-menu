using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace AdminMenu
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BasePlugin
    {
        public const string PluginGuid = "com.darkharasho.moonsweepers.adminmenu";
        public const string PluginName = "AdminMenu";
        public const string PluginVersion = "0.3.0";

        internal static new ManualLogSource Log;
        internal static ConfigEntry<string> ToggleKey;
        /// <summary>The parsed <see cref="ToggleKey"/>.</summary>
        internal static Shortcut ToggleShortcut => Shortcut.Parse(ToggleKey.Value);
        internal static ConfigEntry<bool> CloseOnEscape;
        internal static ConfigEntry<string> Bans;

        /// <summary>False when patching failed; kicking is then disabled.</summary>
        internal static bool PatchesApplied { get; private set; }

        private Harmony _harmony;

        public override void Load()
        {
            Log = base.Log;

            ToggleKey = Config.Bind("General", "ToggleKey", "F1", new ConfigDescription(
                "Opens and closes the admin menu (a Unity KeyCode, optionally with modifiers: F1 or F1 + LeftControl). Only the host of a crew can open it.",
                null,
                new ConfigurationManagerAttributes { DispName = "Toggle key", Order = 3 }));
            CloseOnEscape = Config.Bind("General", "CloseOnEscape", true, new ConfigDescription(
                "Esc closes the admin menu (without opening the pause menu).",
                null,
                new ConfigurationManagerAttributes { DispName = "Close on Esc", Order = 2 }));
            Bans = Config.Bind("Players", "Bans", "", new ConfigDescription(
                "Players banned from every crew you host, as SteamID64:name pairs separated by commas. Managed from the menu.",
                null,
                new ConfigurationManagerAttributes { DispName = "Banned players", Order = 1 }));

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll();
                PatchesApplied = true;
            }
            catch (Exception e)
            {
                Log.LogError($"Patching failed, likely a game update; kicking is unavailable. {e}");
                _harmony.UnpatchSelf();
            }
            Kicks.Init();

            // Registers the class with IL2CPP and puts it on BepInEx's persistent manager object.
            AddComponent<AdminMenuController>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded; press {ToggleShortcut} while hosting a crew to open the admin menu");
        }

        public override bool Unload()
        {
            _harmony?.UnpatchSelf();
            return true;
        }
    }
}
