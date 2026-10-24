using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AdminMenu
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.darkharasho.moonsweepers.adminmenu";
        public const string PluginName = "AdminMenu";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<bool> CloseOnEscape;
        internal static ConfigEntry<string> Bans;

        /// <summary>False when patching failed; kicking is then disabled.</summary>
        internal static bool PatchesApplied { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            ToggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F1), new ConfigDescription(
                "Opens and closes the admin menu. Only the host of a crew can open it.",
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

            // Its own object, so it outlives scene loads and doesn't depend on BepInEx's manager object staying alive.
            var host = new GameObject("AdminMenu");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<AdminMenuController>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded; press {ToggleKey.Value} while hosting a crew to open the admin menu");
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
