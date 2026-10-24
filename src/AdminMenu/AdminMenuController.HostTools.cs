using System;
using System.Collections.Generic;
using System.Linq;
using MoonCleaners;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>The Money, Give and World tabs. Game calls are in <see cref="HostTools"/>.</summary>
    public sealed partial class AdminMenuController
    {
        private static readonly (string label, decimal dollars)[] QuickMoney =
        {
            ("+ $1,000", 1_000m), ("+ $10,000", 10_000m), ("+ $100,000", 100_000m), ("+ $1,000,000", 1_000_000m),
        };

        private static readonly string[] FlyModes = { "Walk", "Fly", "Fly, noclip" };

        // Taken on Layout with the member list, so the controls drawn can't change between Layout and Repaint.
        private HashSet<ulong> _inShift = new HashSet<ulong>();
        private bool _shift;
        private decimal _balance;
        private List<HostTools.Tool> _tools = new List<HostTools.Tool>();
        private HashSet<string> _owned = new HashSet<string>();
        private List<HostTools.Upgrade> _upgrades = new List<HostTools.Upgrade>();
        private List<MoonSteamLobby.Member> _receivers = new List<MoonSteamLobby.Member>();

        private string _amount = "";
        private ulong _giveTo;

        private void SnapshotHostTools()
        {
            var balance = HostTools.Balance();
            _shift = balance.HasValue;
            _balance = balance ?? 0m;
            _inShift = new HashSet<ulong>(_members.Where(m => HostTools.InShift(m.id)).Select(m => m.id));

            // You, then crewmates in the shift: only they have tools and upgrades in your save.
            _receivers = _members.Where(m => m.local || _inShift.Contains(m.id)).ToList();
            if (_receivers.All(m => m.id != _giveTo))
                _giveTo = _receivers.FirstOrDefault(m => m.local)?.id ?? MoonSteam.SteamId;

            if (_tab != Tab.Give || !_shift)
                return;
            _tools = HostTools.Tools();
            _owned = new HashSet<string>(_tools.Where(t => HostTools.HasTool(_giveTo, t.Id)).Select(t => t.Id));
            _upgrades = HostTools.Upgrades(_giveTo);
        }

        private bool NeedShift(Pane pane)
        {
            if (_shift)
                return false;
            pane.Text("These work in a shift. Start or load one first.", Theme.MutedText);
            return true;
        }

        // ---- Money ---------------------------------------------------------------------------------------------

        private void DrawMoney(Pane pane)
        {
            pane.Heading("CREW MONEY");
            if (NeedShift(pane))
                return;
            var title = pane.Row(48f);
            title.Label($"Balance  {HostTools.Money(_balance)}", Theme.Title);
            pane.Text("The crew shares one balance; everyone sees the change straight away.", Theme.MutedText);
            pane.Space(10f);

            var quick = pane.Row(40f);
            foreach (var (label, dollars) in QuickMoney)
            {
                var amount = dollars;
                if (quick.Button(label, Theme.Button))
                    Run("Add money", () => HostTools.AddMoney(amount));
            }

            pane.Heading("AMOUNT");
            var line = pane.Row(40f);
            _amount = line.TextField(_amount ?? "", 16, Theme.Field, 240f);
            if (line.Button("Add", Theme.Button))
                RunAmount("Add money", HostTools.AddMoney);
            if (line.Button("Remove", Theme.Button))
                RunAmount("Remove money", d => HostTools.AddMoney(-d));
            if (line.Button("Set balance", Theme.Button))
                RunAmount("Set balance", HostTools.SetMoney);
            pane.Text("Type 2500, $2,500, 2.5k, 3m or 1b.", Theme.MutedText);
        }

        private void RunAmount(string action, Func<decimal, string> work)
        {
            if (!MoneyInput.TryParse(_amount, out var dollars))
            {
                SetStatus($"{action} failed: type an amount like 2500 or 2.5k", true);
                return;
            }
            Run(action, () => work(dollars));
        }

        // ---- Give ----------------------------------------------------------------------------------------------

        private void DrawGive(Pane pane)
        {
            pane.Heading("GIVE TO");
            if (NeedShift(pane))
                return;
            var who = pane.Row(40f);
            foreach (var member in _receivers)
            {
                var name = member.local ? "You" : (string.IsNullOrEmpty(member.name) ? Kicks.NameFor(member.id) : (string)member.name);
                if (who.Button(name, member.id == _giveTo ? Theme.TabOn : Theme.Button))
                    _giveTo = member.id;
            }
            pane.Text("Gifts are free: the crew's balance stays the same. Crewmates get them within a few seconds.", Theme.MutedText);

            var target = _giveTo;
            ConfirmButton(pane.Row(40f), target, "Give everything", () => HostTools.GiveEverything(target), fromRight: false);

            pane.Heading("TOOLS");
            foreach (var tool in _tools)
            {
                var id = tool.Id;
                var line = pane.Card();
                if (_owned.Contains(id))
                    line.LabelRight("Owned", Theme.MutedText, 90f);
                else if (line.ButtonRight("Give", Theme.Button, 90f))
                    Run("Give tool", () => HostTools.GiveTool(target, id));
                line.Label(tool.Name, Theme.Body);
            }

            pane.Heading("UPGRADES");
            pane.Text("Giving an upgrade also gives what it needs first: its tool and the upgrades before it.", Theme.MutedText);
            string branch = null;
            foreach (var upgrade in _upgrades)
            {
                if (upgrade.Branch != branch)
                {
                    branch = upgrade.Branch;
                    pane.Text(branch, Theme.MutedText);
                }
                var id = upgrade.Id;
                var line = pane.Card();
                if (upgrade.Level < upgrade.Max)
                {
                    if (upgrade.Max > 1 && line.ButtonRight("Max", Theme.Button, 80f))
                        Run("Max upgrade", () => HostTools.MaxUpgrade(target, id));
                    if (line.ButtonRight("+1", Theme.Button, 70f))
                        Run("Give upgrade", () => HostTools.GiveUpgrade(target, id));
                }
                line.LabelRight($"{upgrade.Level} / {upgrade.Max}", Theme.MutedText, 70f);
                line.Label(upgrade.Name, Theme.Body);
            }
        }

        // ---- World ---------------------------------------------------------------------------------------------

        private void DrawWorld(Pane pane)
        {
            pane.Heading("TIME");
            if (NeedShift(pane))
                return;
            var time = HostTools.TimeOfDay();
            if (time.HasValue)
            {
                var minutes = Mathf.FloorToInt(time.Value * HostTools.DaySeconds() / 60f);
                Slider(pane, $"Time of day  ·  {minutes} min (0 new Earth, 30 full)", time.Value, 0f, 1f, HostTools.SetTimeOfDay);
            }
            Slider(pane, $"Time speed  ·  × {HostTools.TimeSpeed:0}", HostTools.TimeSpeed, 1f, 60f, v => HostTools.SetTimeSpeed(Mathf.Round(v)), "Normal",
                () => Run("Time speed", () => { HostTools.SetTimeSpeed(1f); return "Time runs at normal speed."; }));
            pane.Text("The crew shares your clock; crewmates' skies follow within a few seconds.", Theme.MutedText);

            pane.Heading("CLEANING");
            var clean = pane.Row(40f);
            if (clean.Button("Clean this hex", Theme.Button))
                Run("Clean", () => HostTools.CleanAround(0));
            ConfirmButton(clean, 1, "Clean 2 rings around", () => HostTools.CleanAround(2), fromRight: false);
            pane.Text("Marks the ground where you stand clean for the whole crew, as if someone swept it.", Theme.MutedText);

            pane.Heading("YOU");
            var modes = pane.Row(40f);
            var mode = HostTools.FlyMode();
            for (var i = 0; i < FlyModes.Length; i++)
            {
                var chosen = i;
                if (modes.Button(FlyModes[i], i == mode ? Theme.TabOn : Theme.Button) && i != mode)
                    Run(FlyModes[i], () => { HostTools.SetFlyMode(chosen); return chosen == 0 ? "Walking." : "Flying: Space up, Ctrl down, Shift fast."; });
            }
            Slider(pane, $"Fly speed  ·  {HostTools.FlySpeed:0} m/s", HostTools.FlySpeed, 4f, 150f, HostTools.SetFlySpeed);
            Slider(pane, $"Walk speed  ·  × {HostTools.WalkSpeed:0.0}", HostTools.WalkSpeed, 1f, 6f, HostTools.SetWalkSpeed);
            var tools = pane.Row(40f);
            if (tools.Button("Go to where you aim", Theme.Button))
            {
                Run("Go to aim", HostTools.TeleportToAim);
                Close(lost: false);
            }
            if (tools.Button("Refill tools", Theme.Button))
                Run("Refill tools", HostTools.RefillTools);
            pane.Text("These only change you; crewmates just see you move.", Theme.MutedText);
        }

        /// <summary>A labelled slider that calls <paramref name="set"/> while it's dragged, with an optional button beside it.</summary>
        private void Slider(Pane pane, string label, float value, float min, float max, Action<float> set, string buttonText = null, Action onButton = null)
        {
            var title = pane.Row(30f, 0f);
            title.Label(label, Theme.Body);
            var line = pane.Row(36f);
            var next = line.Slider(value, min, max, 560f);
            if (buttonText != null && line.Button(buttonText, Theme.Button))
                onButton?.Invoke();
            if (Mathf.Approximately(next, value))
                return;
            try
            {
                set(next);
            }
            catch (InvalidOperationException e)
            {
                SetStatus(e.Message, true);
            }
        }
    }
}
