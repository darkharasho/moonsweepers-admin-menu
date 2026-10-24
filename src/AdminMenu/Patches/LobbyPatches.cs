using System;
using HarmonyLib;
using MoonCleaners;

namespace AdminMenu
{
    /// <summary>Hides kicked players from the game's crew code. See <see cref="Kicks"/> for why this is a kick.</summary>
    internal static class LobbyPatches
    {
        private static bool _loggedError;

        [HarmonyPatch(typeof(MoonSteamLobby), "RefreshMembers")]
        private static class RefreshMembersPatch
        {
            private static void Postfix()
            {
                try
                {
                    Kicks.AfterRefreshMembers();
                }
                catch (Exception e)
                {
                    LogOnce(e);
                }
            }
        }

        /// <summary>Used when a player asks to open a network session: a kicked player's is refused.</summary>
        [HarmonyPatch(typeof(MoonSteamLobby), nameof(MoonSteamLobby.IsMember))]
        private static class IsMemberPatch
        {
            private static void Postfix(ulong id, ref bool __result)
            {
                try
                {
                    if (__result && Kicks.IsBlocked(id))
                        __result = false;
                }
                catch (Exception e)
                {
                    LogOnce(e);
                }
            }
        }

        private static void LogOnce(Exception e)
        {
            if (_loggedError)
                return;
            _loggedError = true;
            Plugin.Log.LogError($"Kick filter failed: {e}");
        }
    }
}
