using System;
using System.Collections.Generic;
using MoonCleaners;
using Steamworks;

namespace AdminMenu
{
    /// <summary>
    /// Kicking, banning and every call into the game's crew code they need.
    ///
    /// Moon Sweepers has no kick: a crew is a Steam lobby, and every member talks to every other member over Steam
    /// networking, accepting anyone the lobby lists (see <c>MoonCrewSession</c>). Steam doesn't let the lobby owner
    /// remove a member, so instead the host stops treating the player as one: <see cref="LobbyPatches"/> drop them from
    /// <c>MoonSteamLobby.Members</c>, which the game uses to decide whose packets to take and who to send to, and from
    /// <c>MoonSteamLobby.IsMember</c>, which decides whose network sessions to accept. A kicked player gets nothing more
    /// of the host's Moon; if they try to join a shift the game's own 45 second join timeout sends them back out of the
    /// lobby.
    ///
    /// The host also publishes the list in lobby data under <see cref="LobbyKey"/>. Crewmates with this mod drop the
    /// listed players the same way, and a listed player with this mod leaves straight away.
    /// </summary>
    internal static class Kicks
    {
        public const string LobbyKey = "darkharasho.kicked";

        public static readonly KickList List = new KickList();

        private static HashSet<ulong> _published = new HashSet<ulong>();
        private static ulong _lobby;
        private static string _written;
        private static bool _leavePending;

        public static void Init()
        {
            List.LoadBans(Plugin.Bans.Value);
        }

        /// <summary>Whether kicking can work at all on this game version.</summary>
        public static bool Available => Plugin.PatchesApplied;

        /// <summary>
        /// Whether this machine should ignore <paramref name="id"/>: the host goes by its own list, crewmates by the
        /// host's published one. Never yourself or the host.
        /// </summary>
        public static bool IsBlocked(ulong id)
        {
            if (id == 0 || !MoonSteamLobby.InLobby || id == MoonSteam.SteamId || id == MoonSteamLobby.HostId)
                return false;
            return MoonSteamLobby.IsHost ? List.Contains(id) : _published.Contains(id);
        }

        /// <summary>Called every frame by the controller, outside any Steam callback.</summary>
        public static void Tick()
        {
            var lobby = MoonSteamLobby.InLobby ? MoonSteamLobby.LobbyId : 0UL;
            if (lobby != _lobby)
            {
                _lobby = lobby;
                List.ClearKicks();
                _published = new HashSet<ulong>();
                _written = null;
                _leavePending = false;
            }

            if (_leavePending)
            {
                _leavePending = false;
                LeaveAsKicked();
                return;
            }

            if (lobby != 0 && MoonSteamLobby.IsHost)
                Publish();
        }

        /// <summary>
        /// Runs after the game rebuilds its member list (a member joined or left, or lobby data changed): reads the
        /// host's list on a crewmate, then takes blocked players out of the list.
        /// </summary>
        public static void AfterRefreshMembers()
        {
            if (!MoonSteamLobby.InLobby || !MoonSteam.Initialized)
                return;

            if (!MoonSteamLobby.IsHost)
            {
                // Only the lobby owner can set lobby data, so this list is the host's.
                _published = KickList.DecodeIds(SteamMatchmaking.GetLobbyData(new CSteamID(MoonSteamLobby.LobbyId), LobbyKey));
                if (KickRules.ShouldLeave(false, MoonSteam.SteamId, _published))
                    _leavePending = true;
            }

            var members = MoonSteamLobby.members;
            if (members == null)
                return;
            var removed = 0;
            for (var i = members.Count - 1; i >= 0; i--)
            {
                var m = members.get_Item(i);
                if (m != null && !m.local && !m.host && IsBlocked(m.id))
                {
                    members.RemoveAt(i);
                    removed++;
                }
            }
            if (removed > 0)
            {
                MoonSteamLobby.UpdatePresence();
                // The game raised Changed with the unfiltered list a moment ago; raise it again for the menus showing it.
                MoonSteamLobby.Changed?.Invoke();
            }
        }

        /// <summary>Kicks or bans a crewmate. Throws <see cref="InvalidOperationException"/> with a reason when it can't.</summary>
        public static string Remove(ulong id, bool ban)
        {
            RequireHost();
            if (!KickRules.CanRemove(id, MoonSteam.SteamId, MoonSteamLobby.HostId))
                throw new InvalidOperationException("you can't remove yourself");

            var name = NameFor(id);
            if (ban)
            {
                List.Ban(id, name);
                SaveBans();
            }
            else
            {
                List.Kick(id, name);
            }

            Publish();
            Refresh();
            MoonSteamLobby.CloseSession(id);
            var message = $"{name} was {(ban ? "banned" : "kicked")}";
            Notify(message);
            Plugin.Log.LogInfo($"{message} ({id})");
            return ban ? $"Banned {name}. They can't join any crew you host until you allow them back." : $"Kicked {name}.";
        }

        /// <summary>Takes a player off both lists.</summary>
        public static string Pardon(ulong id)
        {
            var name = NameFor(id);
            var wasBanned = List.IsBanned(id);
            if (!List.Pardon(id))
                return $"{name} wasn't removed.";
            if (wasBanned)
                SaveBans();
            if (MoonSteamLobby.InLobby && MoonSteamLobby.IsHost)
            {
                Publish();
                Refresh();
            }
            Plugin.Log.LogInfo($"Allowed {name} ({id}) back");
            return $"{name} can join again.";
        }

        /// <summary>The best name we have: Steam's, else the one saved with the kick or ban.</summary>
        public static string NameFor(ulong id)
        {
            string steam = null;
            try
            {
                steam = MoonSteamLobby.PlayerName(id);
            }
            catch (Exception)
            {
                // Steam not ready.
            }
            if (!string.IsNullOrEmpty(steam) && steam != "[unknown]" && steam != "Crewmate")
                return steam;
            var saved = List.NameOf(id);
            return string.IsNullOrEmpty(saved) ? id.ToString() : saved;
        }

        private static void RequireHost()
        {
            if (!Available)
                throw new InvalidOperationException("unavailable on this game version, see the BepInEx log");
            if (!MoonSteam.Initialized || !MoonSteamLobby.InLobby)
                throw new InvalidOperationException("you're not in a crew");
            if (!MoonSteamLobby.IsHost)
                throw new InvalidOperationException("only the host can do that");
        }

        /// <summary>Writes the list to lobby data when it changed.</summary>
        private static void Publish()
        {
            if (!MoonSteam.Initialized || !MoonSteamLobby.InLobby || !MoonSteamLobby.IsHost)
                return;
            var encoded = List.EncodeIds();
            if (encoded == _written)
                return;
            if (SteamMatchmaking.SetLobbyData(new CSteamID(MoonSteamLobby.LobbyId), LobbyKey, encoded))
                _written = encoded;
        }

        /// <summary>Rebuilds the game's member list now, so a kick takes effect this frame.</summary>
        private static void Refresh() => MoonSteamLobby.RefreshMembers();

        private static void SaveBans() => Plugin.Bans.Value = List.SaveBans();

        /// <summary>This player is on the host's list: go back to the main menu the way leaving normally does.</summary>
        private static void LeaveAsKicked()
        {
            Plugin.Log.LogInfo("The host removed you from their crew; leaving.");
            if (MoonSession.InGame)
                MoonSession.ReturnToMenu();
            else
                MoonSteamLobby.Leave();
            MoonSteamLobby.ReportError("The host removed you from their crew.");
        }

        /// <summary>The game's own in-shift notice line; does nothing outside a shift.</summary>
        private static void Notify(string message)
        {
            try
            {
                if (MoonSession.InGame && MoonCrewSession.Instance != null)
                    MoonCrewSession.Instance.Notify(message);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't show notice: {e.Message}");
            }
        }
    }
}
