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
    public sealed partial class AdminMenuController : MonoBehaviour
    {
        public AdminMenuController(IntPtr ptr) : base(ptr) { }

        private const float ReferenceHeight = 1080f;
        private const float WindowWidth = 1000f;
        private const float WindowHeight = 760f;

        private enum Tab { Crew, Money, Give, World }

        private static readonly string[] TabNames = { "Crew", "Money", "Give", "World" };
        private Tab _tab;

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
        private float _scroll;
        private float _contentHeight;
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
                if (_open)
                    MenuInput.Poll(Screen.height / ReferenceHeight);
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
                if (MenuInput.Focus >= 0 && Hotkey.EscapePressed())
                {
                    MenuInput.Focus = -1;
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

            if (Hotkey.WasPressed(Plugin.ToggleShortcut))
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

            MenuInput.Reset();
            _open = true;
            _scroll = 0f;
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
            MenuInput.Reset();

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
            foreach (var walker in HostTools.Walkers())
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
                var type = Event.current.type;
                if (_open && type == EventType.KeyDown)
                    MenuInput.Key(Event.current);
                if (_open && type == EventType.Layout)
                    SnapshotLists();
                // Only Repaint draws and handles input, so a click is acted on exactly once per frame.
                if (type != EventType.Repaint)
                    return;

                Theme.Ensure();
                GUI.depth = -1000;
                var scale = Screen.height / ReferenceHeight;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                var width = Screen.width / scale;

                if (_open)
                {
                    MenuInput.BeginPass();
                    DrawMenu(width);
                    MenuInput.EndPass();
                }
                else
                {
                    DrawToast(width);
                }
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
            var w = Theme.TextWidth(_toast, Theme.Toast) + 40f;
            var h = Theme.Toast.fontSize * 1.35f + 24f;
            GUI.Label(new Rect((width - w) / 2f, ReferenceHeight * 0.12f, w, h), _toast, Theme.Toast);
        }

        private void SnapshotLists()
        {
            _members = new List<MoonSteamLobby.Member>();
            var live = MoonSteamLobby.members;
            for (var i = 0; live != null && i < live.Count; i++)
                _members.Add(live[i]);
            _removed = Kicks.List.Entries();
            SnapshotHostTools();
        }

        private void DrawMenu(float width)
        {
            GUI.DrawTexture(new Rect(0f, 0f, width, ReferenceHeight), Theme.Dim);
            var window = new Rect((width - WindowWidth) / 2f, (ReferenceHeight - WindowHeight) / 2f, WindowWidth, WindowHeight);
            GUI.Label(window, "", Theme.WindowStyle);

            var x = window.x + 24f;
            var w = window.width - 48f;
            var top = window.y + 20f;
            var headerHeight = 54f + 30f + 48f;
            var statusHeight = 30f;
            var view = new Rect(x, top + headerHeight, w, window.yMax - 20f - statusHeight - (top + headerHeight));

            // Content first; the covers below hide any row that is half outside the viewport.
            DrawScrolled(view);
            var inner = new Rect(window.x + 3f, window.y + 3f, window.width - 6f, window.height - 6f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, view.y - inner.y), "", Theme.Cover);
            GUI.Label(new Rect(inner.x, view.yMax, inner.width, inner.yMax - view.yMax), "", Theme.Cover);

            var y = top;
            GUI.Label(new Rect(x, y, w - 130f, 48f), "ADMIN MENU", Theme.Title);
            if (Header(new Rect(x + w - 110f, y + 4f, 110f, 40f), "Close", Theme.Button))
                Close(lost: false);
            y += 54f;

            var crewName = string.IsNullOrEmpty(MoonSteamLobby.CrewName) ? "Your crew" : MoonSteamLobby.CrewName;
            GUI.Label(new Rect(x, y, w, 26f), $"{crewName} · {_members.Count} of {MoonSteamLobby.MaxCrew}", Theme.MutedText);
            y += 30f;

            for (var i = 0; i < TabNames.Length; i++)
            {
                if (Header(new Rect(x + i * 148f, y, 140f, 40f), TabNames[i], (Tab)i == _tab ? Theme.TabOn : Theme.Button) && (Tab)i != _tab)
                {
                    _tab = (Tab)i;
                    _scroll = 0f;
                    _armedId = 0;
                }
            }

            GUI.Label(new Rect(x, window.yMax - 20f - statusHeight + 4f, w, statusHeight), string.IsNullOrEmpty(_status) ? " " : _status, _statusError ? Theme.ErrorText : Theme.MutedText);
        }

        /// <summary>A button outside the scrolled area.</summary>
        private static bool Header(Rect rect, string text, Btn style)
        {
            var hot = rect.Contains(MenuInput.Pos);
            GUI.Label(rect, text, hot ? style.Hover : style.Normal);
            if (!hot || !MenuInput.Click)
                return false;
            MenuInput.Click = false;
            return true;
        }

        /// <summary>The active tab in a viewport: mouse wheel scrolls, a thin bar on the right shows where you are.</summary>
        private void DrawScrolled(Rect view)
        {
            const float bar = 6f;
            var maxScroll = Mathf.Max(0f, _contentHeight - view.height);
            if (MenuInput.Wheel != 0f && view.Contains(MenuInput.Pos))
                _scroll += MenuInput.Wheel * 60f;
            _scroll = Mathf.Clamp(_scroll, 0f, maxScroll);

            var pane = new Pane(view, view.width - bar - 14f, _scroll);
            switch (_tab)
            {
                case Tab.Money:
                    DrawMoney(pane);
                    break;
                case Tab.Give:
                    DrawGive(pane);
                    break;
                case Tab.World:
                    DrawWorld(pane);
                    break;
                default:
                    DrawCrew(pane);
                    break;
            }
            _contentHeight = pane.Y + _scroll;

            if (maxScroll > 0f)
            {
                GUI.Label(new Rect(view.xMax - bar, view.y, bar, view.height), "", Theme.Track);
                var thumbHeight = Mathf.Max(30f, view.height * view.height / _contentHeight);
                var thumbY = view.y + (view.height - thumbHeight) * (_scroll / maxScroll);
                GUI.Label(new Rect(view.xMax - bar, thumbY, bar, thumbHeight), "", Theme.Fill);
            }
        }

        private void DrawCrew(Pane pane)
        {
            pane.Heading("CREW");
            foreach (var member in _members)
                DrawMember(pane, member);

            pane.Heading("REMOVED");
            if (_removed.Count == 0)
                pane.Text("Nobody. Kicks last for this crew; bans apply to every crew you host.", Theme.MutedText);
            foreach (var player in _removed)
                DrawRemoved(pane, player);

            pane.Space(8f);
            pane.Text(
                "A kicked player is cut off from your Moon and can't rejoin your shift. Without this mod they stay in the " +
                "Steam lobby until they leave, and crewmates without it can still see them; anyone with the mod drops them too. " +
                "Knock down bowls a crewmate over the way a vehicle does; they get straight back up.",
                Theme.MutedText);
        }

        private void DrawMember(Pane pane, MoonSteamLobby.Member member)
        {
            var line = pane.Card();
            var id = member.id;
            var name = string.IsNullOrEmpty(member.name) ? Kicks.NameFor(id) : (string)member.name;
            var tag = member.local ? "  (you, host)" : member.host ? "  (host)" : "";
            // Right-hand buttons first so the name takes what's left.
            if (KickRules.CanRemove(id, MoonSteam.SteamId, MoonSteamLobby.HostId))
            {
                ConfirmButton(line, id, "Ban", () => Kicks.Remove(id, ban: true));
                ConfirmButton(line, id, "Kick", () => Kicks.Remove(id, ban: false));
                if (_inShift.Contains(id) && line.ButtonRight("Knock down", Theme.Button))
                    Run("Knock down", () => HostTools.Knock(id));
            }
            line.Label(name + tag, Theme.Body);
        }

        private void DrawRemoved(Pane pane, RemovedPlayer player)
        {
            var line = pane.Card();
            var id = player.Id;
            if (line.ButtonRight("Allow back", Theme.Button))
                Run("Allow back", () => Kicks.Pardon(id));
            if (!player.Banned)
                ConfirmButton(line, id, "Ban", () => Kicks.Remove(id, ban: true));
            line.Label($"{Kicks.NameFor(id)}  · {(player.Banned ? "banned" : "kicked")}", Theme.Body);
        }

        /// <summary>The first click only arms the button for three seconds; the second does the work. Placed from the right.</summary>
        private void ConfirmButton(Line line, ulong id, string action, Func<string> work, bool fromRight = true)
        {
            var armed = _armedId == id && _armedAction == action && Time.unscaledTime < _armedUntil;
            var text = armed ? $"Click again: {action}" : action;
            var clicked = fromRight ? line.ButtonRight(text, Theme.DangerButton) : line.Button(text, Theme.DangerButton);
            if (!clicked)
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
