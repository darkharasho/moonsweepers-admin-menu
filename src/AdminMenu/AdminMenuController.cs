using System;
using System.Collections.Generic;
using MoonCleaners;
using UnityEngine;
using UnityEngine.EventSystems;
using Cursor = UnityEngine.Cursor;

namespace AdminMenu
{
    /// <summary>
    /// Owns the menu: reads the hotkey, opens and closes the window, takes the game's input while it's open, and draws
    /// it. Input is taken the way the game's own menus and chat do: <c>MoonInput.Typing</c> on (the game reads no
    /// keyboard or mouse for play, Esc included, while it's set), <c>MoonWalker.UIBlocked</c> on in a shift, a free
    /// cursor, and the uGUI event system off so clicks don't reach the game's buttons underneath.
    /// </summary>
    internal sealed class AdminMenuController : MonoBehaviour
    {
        private const float ReferenceHeight = 1080f;
        private const float WindowWidth = 920f;
        private const float WindowHeight = 680f;

        private bool _open;
        private MoonWalker _walker;
        private bool _setTyping;
        private bool _blockedWalker;
        private EventSystem _eventSystem;
        private bool _disabledEvents;
        private CursorLockMode _priorLock;
        private bool _priorVisible;
        private bool _loggedError;

        private string _status = "";
        private bool _statusError;
        private string _toast;
        private float _toastUntil;
        private Vector2 _scroll;
        /// <summary>Taken on Layout and drawn on Repaint, so a kick between the two can't change IMGUI's control count.</summary>
        private List<MoonSteamLobby.Member> _members = new List<MoonSteamLobby.Member>();
        private List<RemovedPlayer> _removed = new List<RemovedPlayer>();

        private ulong _armedId;
        private string _armedAction;
        private float _armedUntil;

        private void Update()
        {
            try
            {
                Kicks.Tick();
                Tick();
            }
            catch (Exception e)
            {
                LogOnce("update", e);
                if (_open)
                    Close(lost: true);
            }
        }

        /// <summary>Re-asserted after the game's own Update, which may relock the cursor.</summary>
        private void LateUpdate()
        {
            if (_open)
                KeepInputLocked();
        }

        private void OnDestroy()
        {
            if (_open)
                Close(lost: true);
        }

        private void Tick()
        {
            if (_open)
            {
                if (MenuGate.ShouldAutoClose(MoonSteamLobby.InLobby, MoonSteamLobby.IsHost, Loading()))
                {
                    Close(lost: true);
                    return;
                }
                if (Plugin.CloseOnEscape.Value && Hotkey.EscapePressed())
                {
                    // MoonInput.Typing stays on for the rest of this frame, so the pause menu doesn't see this Esc.
                    Close(lost: false);
                    return;
                }
                KeepInputLocked();
            }

            if (Hotkey.WasPressed(Plugin.ToggleKey.Value))
                Toggle();
        }

        private void Toggle()
        {
            var decision = MenuGate.Decide(_open, MoonSteamLobby.InLobby, MoonSteamLobby.IsHost, GameBusy());
            switch (decision)
            {
                case ToggleDecision.Open:
                    Open();
                    break;
                case ToggleDecision.Close:
                    Close(lost: false);
                    break;
                default:
                    var message = MenuGate.Message(decision);
                    if (message != null)
                        ShowToast(message);
                    break;
            }
        }

        private void Open()
        {
            _walker = MoonSession.InGame ? FindLocalWalker() : null;
            _priorLock = Cursor.lockState;
            _priorVisible = Cursor.visible;

            if (!MoonInput.Typing)
            {
                MoonInput.Typing = true;
                _setTyping = true;
            }
            if (_walker != null && !_walker.UIBlocked)
            {
                _walker.UIBlocked = true;
                _blockedWalker = true;
            }
            _eventSystem = EventSystem.current;
            if (_eventSystem != null && _eventSystem.enabled)
            {
                _eventSystem.enabled = false;
                _disabledEvents = true;
            }

            _open = true;
            _scroll = Vector2.zero;
            _armedId = 0;
            SetStatus(Kicks.Available ? "" : "Kicking is unavailable on this game version: see the BepInEx log.", !Kicks.Available);
            KeepInputLocked();
        }

        /// <param name="lost">The lobby or scene went away: hand input back but leave the cursor to the game.</param>
        private void Close(bool lost)
        {
            if (!_open)
                return;
            _open = false;

            if (_setTyping)
                MoonInput.Typing = false;
            _setTyping = false;
            if (_blockedWalker && _walker != null)
                _walker.UIBlocked = false;
            _blockedWalker = false;
            _walker = null;
            if (_disabledEvents && _eventSystem != null)
                _eventSystem.enabled = true;
            _disabledEvents = false;
            _eventSystem = null;

            if (!lost)
            {
                Cursor.lockState = _priorLock;
                Cursor.visible = _priorVisible;
            }
        }

        private static void KeepInputLocked()
        {
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;
        }

        private static bool Loading() => MoonSession.Loading || MoonLoadingScreen.Active;

        /// <summary>Loading, chatting, signing the contract, or a game menu (pause, garage, lockers) is up.</summary>
        private static bool GameBusy()
        {
            if (Loading() || MoonInput.Typing || MoonNewHire.Active)
                return true;
            if (!MoonSession.InGame)
                return false;
            var walker = FindLocalWalker();
            return walker == null || walker.UIBlocked;
        }

        /// <summary>The local player, found the way MoonCrewSession finds it.</summary>
        private static MoonWalker FindLocalWalker()
        {
            foreach (var walker in FindObjectsByType<MoonWalker>(FindObjectsInactive.Exclude))
            {
                if (walker != null && walker.world != null)
                    return walker;
            }
            return null;
        }

        private void OnGUI()
        {
            if (!_open && (_toast == null || Time.unscaledTime >= _toastUntil))
                return;
            try
            {
                Theme.Ensure();
                GUI.depth = -1000;
                var scale = Screen.height / ReferenceHeight;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                var width = Screen.width / scale;

                if (_open)
                    DrawMenu(width);
                else
                    DrawToast(width);
            }
            catch (Exception e)
            {
                LogOnce("draw", e);
                if (_open)
                    Close(lost: true);
            }
            finally
            {
                GUI.matrix = Matrix4x4.identity;
            }
        }

        private void DrawToast(float width)
        {
            var content = new GUIContent(_toast);
            var size = Theme.Toast.CalcSize(content);
            GUI.Label(new Rect((width - size.x) / 2f, ReferenceHeight * 0.12f, size.x, size.y), content, Theme.Toast);
        }

        private void DrawMenu(float width)
        {
            GUI.DrawTexture(new Rect(0f, 0f, width, ReferenceHeight), Theme.Dim);
            var window = new Rect((width - WindowWidth) / 2f, (ReferenceHeight - WindowHeight) / 2f, WindowWidth, WindowHeight);
            GUI.Box(window, GUIContent.none, Theme.WindowStyle);

            GUILayout.BeginArea(new Rect(window.x + 24f, window.y + 20f, window.width - 48f, window.height - 40f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("ADMIN MENU", Theme.Title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", Theme.Button, GUILayout.Width(110f), GUILayout.Height(40f)))
                Close(lost: false);
            GUILayout.EndHorizontal();

            if (Event.current.type == EventType.Layout)
            {
                _members = new List<MoonSteamLobby.Member>(MoonSteamLobby.Members);
                _removed = Kicks.List.Entries();
            }
            var crewName = string.IsNullOrEmpty(MoonSteamLobby.CrewName) ? "Your crew" : MoonSteamLobby.CrewName;
            GUILayout.Label($"{crewName} · {_members.Count} of {MoonSteamLobby.MaxCrew}", Theme.MutedText);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            GUILayout.Label("CREW", Theme.Heading);
            foreach (var member in _members)
                DrawMember(member);

            GUILayout.Label("REMOVED", Theme.Heading);
            if (_removed.Count == 0)
                GUILayout.Label("Nobody. Kicks last for this crew; bans apply to every crew you host.", Theme.MutedText);
            foreach (var player in _removed)
                DrawRemoved(player);

            GUILayout.EndScrollView();

            GUILayout.Label(
                "A kicked player is cut off from your Moon and can't rejoin your shift. Without this mod they stay in the " +
                "Steam lobby until they leave, and crewmates without it can still see them; anyone with the mod drops them too.",
                Theme.MutedText);
            GUILayout.Space(6f);
            GUILayout.Label(string.IsNullOrEmpty(_status) ? " " : _status, _statusError ? Theme.ErrorText : Theme.MutedText);

            GUILayout.EndArea();
        }

        private void DrawMember(MoonSteamLobby.Member member)
        {
            GUILayout.BeginHorizontal(Theme.RowStyle, GUILayout.Height(52f));
            var name = string.IsNullOrEmpty(member.name) ? Kicks.NameFor(member.id) : member.name;
            var tag = member.local ? "  (you, host)" : member.host ? "  (host)" : "";
            GUILayout.Label(name + tag, Theme.Body, GUILayout.Height(36f));
            GUILayout.FlexibleSpace();
            if (KickRules.CanRemove(member.id, MoonSteam.SteamId, MoonSteamLobby.HostId))
            {
                ConfirmButton(member.id, "Kick", () => Kicks.Remove(member.id, ban: false));
                ConfirmButton(member.id, "Ban", () => Kicks.Remove(member.id, ban: true));
            }
            GUILayout.EndHorizontal();
        }

        private void DrawRemoved(RemovedPlayer player)
        {
            GUILayout.BeginHorizontal(Theme.RowStyle, GUILayout.Height(52f));
            GUILayout.Label($"{Kicks.NameFor(player.Id)}  · {(player.Banned ? "banned" : "kicked")}", Theme.Body, GUILayout.Height(36f));
            GUILayout.FlexibleSpace();
            if (!player.Banned)
                ConfirmButton(player.Id, "Ban", () => Kicks.Remove(player.Id, ban: true));
            if (GUILayout.Button("Allow back", Theme.Button, GUILayout.Height(36f)))
                Run("Allow back", () => Kicks.Pardon(player.Id));
            GUILayout.EndHorizontal();
        }

        /// <summary>The first click only arms the button for three seconds; the second does the work.</summary>
        private void ConfirmButton(ulong id, string action, Func<string> work)
        {
            var armed = _armedId == id && _armedAction == action && Time.unscaledTime < _armedUntil;
            if (!GUILayout.Button(armed ? $"Click again: {action}" : action, Theme.DangerButton, GUILayout.Height(36f)))
                return;
            if (!armed)
            {
                _armedId = id;
                _armedAction = action;
                _armedUntil = Time.unscaledTime + 3f;
                return;
            }
            _armedId = 0;
            Run(action, work);
        }

        /// <summary>Runs one button's work; its message, or "action failed: reason", goes to the status line.</summary>
        private void Run(string action, Func<string> work)
        {
            try
            {
                SetStatus(work() ?? action, false);
            }
            catch (InvalidOperationException e)
            {
                SetStatus($"{action} failed: {e.Message}", true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"{action} failed: {e}");
                SetStatus($"{action} failed: {e.Message}", true);
            }
        }

        private void SetStatus(string text, bool error)
        {
            _status = text ?? "";
            _statusError = error;
        }

        private void ShowToast(string message)
        {
            _toast = message;
            _toastUntil = Time.unscaledTime + 3f;
        }

        private void LogOnce(string where, Exception e)
        {
            if (_loggedError)
                return;
            _loggedError = true;
            Plugin.Log.LogError($"Admin menu {where} failed; closing the menu. {e}");
        }
    }
}
