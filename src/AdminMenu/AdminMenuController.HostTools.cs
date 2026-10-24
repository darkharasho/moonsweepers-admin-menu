using System;
using System.Collections.Generic;
using System.Linq;
using MoonCleaners;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>The Money, Give and World tabs. Game calls are in <see cref="HostTools"/>.</summary>
    internal sealed partial class AdminMenuController
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

        private bool NeedShift()
        {
            if (_shift)
                return false;
            GUILayout.Label("These work in a shift. Start or load one first.", Theme.MutedText);
            return true;
        }

        // ---- Money ---------------------------------------------------------------------------------------------

        private void DrawMoney()
        {
            GUILayout.Label("CREW MONEY", Theme.Heading);
            if (NeedShift())
                return;
            GUILayout.Label($"Balance  {HostTools.Money(_balance)}", Theme.Title);
            GUILayout.Label("The crew shares one balance; everyone sees the change straight away.", Theme.MutedText);
            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            foreach (var (label, dollars) in QuickMoney)
            {
                if (GUILayout.Button(label, Theme.Button, GUILayout.Height(40f)))
                    Run("Add money", () => HostTools.AddMoney(dollars));
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Label("AMOUNT", Theme.Heading);
            GUILayout.BeginHorizontal();
            _amount = GUILayout.TextField(_amount ?? "", 16, Theme.Field, GUILayout.Width(240f), GUILayout.Height(40f));
            if (GUILayout.Button("Add", Theme.Button, GUILayout.Height(40f)))
                RunAmount("Add money", HostTools.AddMoney);
            if (GUILayout.Button("Remove", Theme.Button, GUILayout.Height(40f)))
                RunAmount("Remove money", d => HostTools.AddMoney(-d));
            if (GUILayout.Button("Set balance", Theme.Button, GUILayout.Height(40f)))
                RunAmount("Set balance", HostTools.SetMoney);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label("Type 2500, $2,500, 2.5k, 3m or 1b.", Theme.MutedText);
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

        private void DrawGive()
        {
            GUILayout.Label("GIVE TO", Theme.Heading);
            if (NeedShift())
                return;
            GUILayout.BeginHorizontal();
            foreach (var member in _receivers)
            {
                var name = member.local ? "You" : (string.IsNullOrEmpty(member.name) ? Kicks.NameFor(member.id) : member.name);
                if (GUILayout.Button(name, member.id == _giveTo ? Theme.TabOn : Theme.Button, GUILayout.Height(40f)))
                    _giveTo = member.id;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label("Gifts are free: the crew's balance stays the same. Crewmates get them within a few seconds.", Theme.MutedText);

            GUILayout.BeginHorizontal();
            ConfirmButton(_giveTo, "Give everything", () => HostTools.GiveEverything(_giveTo));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Label("TOOLS", Theme.Heading);
            foreach (var tool in _tools)
            {
                GUILayout.BeginHorizontal(Theme.RowStyle, GUILayout.Height(52f));
                GUILayout.Label(tool.Name, Theme.Body, GUILayout.Height(36f));
                GUILayout.FlexibleSpace();
                if (_owned.Contains(tool.Id))
                    GUILayout.Label("Owned", Theme.MutedText, GUILayout.Height(36f));
                else if (GUILayout.Button("Give", Theme.Button, GUILayout.Height(36f)))
                    Run("Give tool", () => HostTools.GiveTool(_giveTo, tool.Id));
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("UPGRADES", Theme.Heading);
            GUILayout.Label("Giving an upgrade also gives what it needs first: its tool and the upgrades before it.", Theme.MutedText);
            string branch = null;
            foreach (var upgrade in _upgrades)
            {
                if (upgrade.Branch != branch)
                {
                    branch = upgrade.Branch;
                    GUILayout.Label(branch, Theme.MutedText);
                }
                GUILayout.BeginHorizontal(Theme.RowStyle, GUILayout.Height(52f));
                GUILayout.Label(upgrade.Name, Theme.Body, GUILayout.Height(36f));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{upgrade.Level} / {upgrade.Max}", Theme.MutedText, GUILayout.Width(70f), GUILayout.Height(36f));
                if (upgrade.Level < upgrade.Max)
                {
                    if (GUILayout.Button("+1", Theme.Button, GUILayout.Width(70f), GUILayout.Height(36f)))
                        Run("Give upgrade", () => HostTools.GiveUpgrade(_giveTo, upgrade.Id));
                    if (upgrade.Max > 1 && GUILayout.Button("Max", Theme.Button, GUILayout.Width(80f), GUILayout.Height(36f)))
                        Run("Max upgrade", () => HostTools.MaxUpgrade(_giveTo, upgrade.Id));
                }
                GUILayout.EndHorizontal();
            }
        }

        // ---- World ---------------------------------------------------------------------------------------------

        private void DrawWorld()
        {
            GUILayout.Label("TIME", Theme.Heading);
            if (NeedShift())
                return;
            var time = HostTools.TimeOfDay();
            if (time.HasValue)
            {
                var minutes = Mathf.FloorToInt(time.Value * HostTools.DaySeconds() / 60f);
                Slider($"Time of day  ·  {minutes} min (0 new Earth, 30 full)", time.Value, 0f, 1f, HostTools.SetTimeOfDay);
            }
            GUILayout.BeginHorizontal();
            Slider($"Time speed  ·  × {HostTools.TimeSpeed:0}", HostTools.TimeSpeed, 1f, 60f, v => HostTools.SetTimeSpeed(Mathf.Round(v)));
            if (GUILayout.Button("Normal", Theme.Button, GUILayout.Height(36f)))
                Run("Time speed", () => { HostTools.SetTimeSpeed(1f); return "Time runs at normal speed."; });
            GUILayout.EndHorizontal();
            GUILayout.Label("The crew shares your clock; crewmates' skies follow within a few seconds.", Theme.MutedText);

            GUILayout.Label("CLEANING", Theme.Heading);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clean this hex", Theme.Button, GUILayout.Height(40f)))
                Run("Clean", () => HostTools.CleanAround(0));
            ConfirmButton(1, "Clean 2 rings around", () => HostTools.CleanAround(2));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label("Marks the ground where you stand clean for the whole crew, as if someone swept it.", Theme.MutedText);

            GUILayout.Label("YOU", Theme.Heading);
            GUILayout.BeginHorizontal();
            var mode = HostTools.FlyMode();
            for (var i = 0; i < FlyModes.Length; i++)
            {
                var chosen = i;
                if (GUILayout.Button(FlyModes[i], i == mode ? Theme.TabOn : Theme.Button, GUILayout.Height(40f)) && i != mode)
                    Run(FlyModes[i], () => { HostTools.SetFlyMode(chosen); return chosen == 0 ? "Walking." : "Flying: Space up, Ctrl down, Shift fast."; });
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            Slider($"Fly speed  ·  {HostTools.FlySpeed:0} m/s", HostTools.FlySpeed, 4f, 150f, HostTools.SetFlySpeed);
            Slider($"Walk speed  ·  × {HostTools.WalkSpeed:0.0}", HostTools.WalkSpeed, 1f, 6f, HostTools.SetWalkSpeed);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Go to where you aim", Theme.Button, GUILayout.Height(40f)))
            {
                Run("Go to aim", HostTools.TeleportToAim);
                Close(lost: false);
            }
            if (GUILayout.Button("Refill tools", Theme.Button, GUILayout.Height(40f)))
                Run("Refill tools", HostTools.RefillTools);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label("These only change you; crewmates just see you move.", Theme.MutedText);
        }

        /// <summary>A labelled slider that calls <paramref name="set"/> while it's dragged.</summary>
        private void Slider(string label, float value, float min, float max, Action<float> set)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(label, Theme.Body);
            var next = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(560f), GUILayout.Height(24f));
            GUILayout.EndVertical();
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
