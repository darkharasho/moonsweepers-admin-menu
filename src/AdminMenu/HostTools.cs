using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MoonCleaners;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AdminMenu
{
    /// <summary>
    /// Every call into the game for the host tools: money, giving tools and upgrades, time of day, cleaning, the host's
    /// own movement tools, and knocking a crewmate down.
    ///
    /// The host's <c>MoonWorld</c> owns the crew's money and everyone's tools and upgrades, and the game sends any
    /// change to every crewmate on its own (the wallet message and the upgrade "truth" sync), so none of this needs the
    /// mod on crewmates. The game's built-in dev menu does some of the same, but it sits behind
    /// <c>MoonDevTools.Enabled</c>, which is false in release builds and also spawns a vehicle test yard when true, so
    /// these call the methods underneath instead.
    ///
    /// Grants go through the game's own purchase methods with the price added to the wallet first, so they follow
    /// every rule a purchase does (saving, syncing, container tiers needing the tool) and leave the balance where it
    /// was. Money the mod adds or removes is taken back out of the host's Steam achievement counters, the way the dev
    /// menu does.
    /// </summary>
    internal static class HostTools
    {
        /// <summary>Highest balance the menu will set, well inside what the game's decimal money and UI can show.</summary>
        public const decimal MaxBalance = 1_000_000_000_000m;

        private static readonly MethodInfo CommitRegion = AccessTools.Method(typeof(MoonWorld), "CommitRegion");

        public sealed class Tool
        {
            public string Id;
            public string Name;
        }

        public sealed class Upgrade
        {
            public string Id;
            public string Name;
            public string Branch;
            public int Level;
            public int Max;
        }

        // ---- Session -------------------------------------------------------------------------------------------

        /// <summary>The local player in a shift, or null in the menu or while loading.</summary>
        public static MoonWalker Walker()
        {
            if (!MoonSession.InGame || MoonSession.Loading)
                return null;
            foreach (var walker in Object.FindObjectsByType<MoonWalker>(FindObjectsInactive.Exclude))
            {
                if (walker != null && walker.world != null)
                    return walker;
            }
            return null;
        }

        private static MoonWorld World()
        {
            RequireHost();
            var world = Walker()?.world;
            if (world == null)
                throw new InvalidOperationException("start a shift first");
            return world;
        }

        private static void RequireHost()
        {
            if (!MoonSteamLobby.InLobby || !MoonSteamLobby.IsHost || MoonCrew.Joined)
                throw new InvalidOperationException("only the host can do that");
        }

        /// <summary>The game's id for a crew member's tools, upgrades and money: "local" for the host.</summary>
        public static string PlayerId(ulong steamId) => MoonCrew.PlayerId(steamId);

        // ---- Money ---------------------------------------------------------------------------------------------

        /// <summary>The crew's shared balance, or null outside a shift.</summary>
        public static decimal? Balance()
        {
            var world = Walker()?.world;
            return world == null ? (decimal?)null : world.WalletDollars();
        }

        public static string Money(decimal dollars) => MoonWorld.FormatMoney(dollars);

        /// <summary>Adds (or with a negative amount removes) money, clamped to 0 and <see cref="MaxBalance"/>.</summary>
        public static string AddMoney(decimal amount)
        {
            var world = World();
            var before = world.WalletDollars();
            var after = Math.Min(MaxBalance, Math.Max(0m, before + amount));
            ChangeMoney(world, after - before);
            return amount >= 0m
                ? $"Added {Money(after - before)}. The crew has {Money(after)}."
                : $"Removed {Money(before - after)}. The crew has {Money(after)}.";
        }

        public static string SetMoney(decimal balance)
        {
            var world = World();
            var after = Math.Min(MaxBalance, Math.Max(0m, balance));
            ChangeMoney(world, after - world.WalletDollars());
            return $"The crew has {Money(after)}.";
        }

        /// <summary>Changes the wallet without counting it toward the host's earned or spent achievements.</summary>
        private static void ChangeMoney(MoonWorld world, decimal delta)
        {
            if (delta == 0m)
                return;
            world.CrewBuildMoney("local", delta);
            MoonAchievements.NotEarned("local", delta);
            world.RequestSave();
        }

        // ---- Tools and upgrades --------------------------------------------------------------------------------

        public static List<Tool> Tools()
        {
            var catalog = Walker()?.world?.UpgradeCatalog;
            if (catalog?.tools == null)
                return new List<Tool>();
            return catalog.tools
                .Where(o => o?.tool != null && !string.IsNullOrEmpty(o.tool.toolId))
                .Select(o => new Tool { Id = o.tool.toolId, Name = MoonLoc.T(o.tool.displayName) })
                .ToList();
        }

        public static bool HasTool(ulong steamId, string toolId) =>
            Walker()?.world != null && Walker().world.ToolLevel(toolId, PlayerId(steamId)) >= 0;

        /// <summary>Every tree and gear upgrade with this player's level in it.</summary>
        public static List<Upgrade> Upgrades(ulong steamId)
        {
            var world = Walker()?.world;
            var result = new List<Upgrade>();
            if (world == null)
                return result;
            var player = PlayerId(steamId);
            // Gear first, then the tree branch by branch, each in the game's order.
            foreach (var node in AllNodes().OrderBy(n => MoonUpgradeTree.IsGear(n) ? -1 : (int)n.branch))
            {
                result.Add(new Upgrade
                {
                    Id = node.id,
                    Name = MoonLoc.T(node.title),
                    Branch = MoonUpgradeTree.IsGear(node) ? "Gear" : MoonUpgradeTree.BranchNames[(int)node.branch],
                    Level = world.TreeLevel(player, node.id),
                    Max = world.TreeMaxLevel(node),
                });
            }
            return result;
        }

        public static string GiveTool(ulong steamId, string toolId)
        {
            var world = World();
            var player = PlayerId(steamId);
            if (world.ToolLevel(toolId, player) >= 0)
                return $"{Kicks.NameFor(steamId)} already has that tool.";
            if (!world.TryGetToolPrice(toolId, player, out var price))
                throw new InvalidOperationException("that tool can't be given");
            Grant(world, price, () => (world.TryPurchaseTool(player, toolId, -1, out var m), m));
            return $"Gave {Kicks.NameFor(steamId)} the {ToolName(world, toolId)}.";
        }

        /// <summary>
        /// Raises an upgrade one level, first giving anything it needs: the tool for a container upgrade and the
        /// upgrades it connects from.
        /// </summary>
        public static string GiveUpgrade(ulong steamId, string id)
        {
            var world = World();
            var node = MoonUpgradeTree.Find(id) ?? throw new InvalidOperationException("unknown upgrade");
            var player = PlayerId(steamId);
            var level = world.TreeLevel(player, id);
            if (level >= world.TreeMaxLevel(node))
                return $"{MoonLoc.T(node.title)} is already maxed for {Kicks.NameFor(steamId)}.";
            Prepare(world, player, node, new HashSet<string>());
            Raise(world, player, node);
            return $"{MoonLoc.T(node.title)} is now level {world.TreeLevel(player, id)} for {Kicks.NameFor(steamId)}.";
        }

        public static string MaxUpgrade(ulong steamId, string id)
        {
            var world = World();
            var node = MoonUpgradeTree.Find(id) ?? throw new InvalidOperationException("unknown upgrade");
            var player = PlayerId(steamId);
            Prepare(world, player, node, new HashSet<string>());
            while (world.TreeLevel(player, id) < world.TreeMaxLevel(node))
                Raise(world, player, node);
            return $"{MoonLoc.T(node.title)} is maxed for {Kicks.NameFor(steamId)}.";
        }

        /// <summary>Every tool, then every upgrade at its top level.</summary>
        public static string GiveEverything(ulong steamId)
        {
            var world = World();
            var player = PlayerId(steamId);
            var given = 0;
            foreach (var tool in Tools())
            {
                if (world.ToolLevel(tool.Id, player) >= 0 || !world.TryGetToolPrice(tool.Id, player, out var price))
                    continue;
                Grant(world, price, () => (world.TryPurchaseTool(player, tool.Id, -1, out var m), m));
                given++;
            }
            foreach (var node in AllNodes())
            {
                Prepare(world, player, node, new HashSet<string>());
                while (world.TreeLevel(player, node.id) < world.TreeMaxLevel(node))
                {
                    Raise(world, player, node);
                    given++;
                }
            }
            return given == 0
                ? $"{Kicks.NameFor(steamId)} already has everything."
                : $"Gave {Kicks.NameFor(steamId)} every tool and upgrade ({given} in all).";
        }

        private static IEnumerable<MoonUpgradeTree.Node> AllNodes() =>
            MoonUpgradeTree.Gear.Concat(MoonUpgradeTree.Nodes).Where(n => n != null && n.branch != MoonUpgradeTree.Branch.Core);

        /// <summary>Gives what <paramref name="node"/> needs before its next level can be bought.</summary>
        private static void Prepare(MoonWorld world, string player, MoonUpgradeTree.Node node, HashSet<string> seen)
        {
            if (!seen.Add(node.id))
                return;
            if (world.TreeToolMissing(player, node) && world.TryGetToolPrice(node.tool, player, out var price))
                Grant(world, price, () => (world.TryPurchaseTool(player, node.tool, -1, out var m), m));
            if (world.TreeRequirementsMet(player, node))
                return;
            // For "any of" requirements one is enough; take the first.
            foreach (var (id, level) in node.requiresAny ? node.requires.Take(1) : node.requires)
            {
                var required = MoonUpgradeTree.Find(id);
                if (required == null)
                    continue;
                Prepare(world, player, required, seen);
                while (world.TreeLevel(player, id) < level && world.TreeLevel(player, id) < world.TreeMaxLevel(required))
                    Raise(world, player, required);
            }
        }

        private static void Raise(MoonWorld world, string player, MoonUpgradeTree.Node node)
        {
            var level = world.TreeLevel(player, node.id);
            if (!world.TryGetTreePrice(player, node.id, out var price))
                throw new InvalidOperationException($"{MoonLoc.T(node.title)} can't be raised");
            Grant(world, price, () => (world.TryPurchaseTreeNode(player, node.id, level, out var m), m));
        }

        /// <summary>Pays for one purchase out of the mod's pocket: adds the price, buys, and takes it back on failure.</summary>
        private static void Grant(MoonWorld world, decimal price, Func<(bool ok, string message)> buy)
        {
            ChangeMoney(world, price);
            (bool ok, string message) result;
            try
            {
                result = buy();
            }
            catch
            {
                ChangeMoney(world, -price);
                throw;
            }
            if (result.ok)
            {
                // The purchase spent the price again; that's not the host spending money either.
                MoonAchievements.NotEarned("local", -price);
                return;
            }
            ChangeMoney(world, -price);
            throw new InvalidOperationException(string.IsNullOrEmpty(result.message) ? "the game refused" : result.message);
        }

        private static string ToolName(MoonWorld world, string toolId) =>
            MoonLoc.T(world.UpgradeCatalog?.Find(toolId)?.tool?.displayName ?? toolId);

        // ---- World ---------------------------------------------------------------------------------------------

        private static MoonSolarSystem Solar() => Object.FindAnyObjectByType<MoonSolarSystem>();

        /// <summary>Lunar time 0..1 (0 new Earth), or null outside a shift.</summary>
        public static float? TimeOfDay()
        {
            var solar = Walker() != null ? Solar() : null;
            return solar == null ? (float?)null : solar.LunarTime;
        }

        public static float DaySeconds() => Solar()?.lunarDaySeconds ?? 0f;

        /// <summary>The host's clock is the crew's; crewmates follow it within a few seconds.</summary>
        public static void SetTimeOfDay(float fraction)
        {
            World();
            var solar = Solar() ?? throw new InvalidOperationException("no sky here");
            solar.SetLunarTime(fraction);
        }

        public static float TimeSpeed => MoonSolarSystem.TimeSpeed;

        public static void SetTimeSpeed(float speed)
        {
            World();
            MoonSolarSystem.TimeSpeed = Mathf.Clamp(speed, 0f, 60f);
        }

        /// <summary>Cleans the hex you're standing on and <paramref name="rings"/> rings around it, for the whole crew.</summary>
        public static string CleanAround(int rings)
        {
            var world = World();
            var regions = world.Regions;
            if (regions == null || CommitRegion == null)
                throw new InvalidOperationException("cleaning is unavailable on this game version");
            var start = regions.Find(Walker().transform.position - world.transform.position);
            var all = new List<int> { start };
            var seen = new HashSet<int> { start };
            var from = 0;
            for (var ring = 0; ring < rings; ring++)
            {
                var count = all.Count;
                for (var i = from; i < count; i++)
                {
                    for (var k = 0; k < regions.NeighbourCount(all[i]); k++)
                    {
                        var next = regions.Neighbour(all[i], k);
                        if (seen.Add(next))
                            all.Add(next);
                    }
                }
                from = count;
            }
            var cleaned = 0;
            foreach (var region in all)
            {
                if (world.IsRegionComplete(region))
                    continue;
                CommitRegion.Invoke(world, new object[] { region, false, null, false });
                cleaned++;
            }
            return cleaned == 0 ? "Already clean here." : $"Cleaned {cleaned} {(cleaned == 1 ? "hex" : "hexes")}.";
        }

        // ---- The host's own movement and tools -----------------------------------------------------------------

        /// <summary>0 walking, 1 flying, 2 flying through walls.</summary>
        public static int FlyMode()
        {
            var walker = Walker();
            if (walker == null || !walker.Flying)
                return 0;
            return MoonDevTools.Noclip ? 2 : 1;
        }

        public static void SetFlyMode(int mode)
        {
            World();
            var walker = Walker();
            if (walker.Mounted)
                throw new InvalidOperationException("leave the vehicle first");
            if (mode > 0)
                MoonDevTools.Noclip = mode == 2;
            walker.SetFlying(mode > 0, MoonDevTools.Noclip);
        }

        public static float FlySpeed => Walker()?.FlySpeed ?? 20f;
        public static float WalkSpeed => Walker()?.DevSpeedMultiplier ?? 1f;

        public static void SetFlySpeed(float speed)
        {
            World();
            Walker().FlySpeed = Mathf.Clamp(speed, 4f, 150f);
        }

        public static void SetWalkSpeed(float multiplier)
        {
            World();
            Walker().DevSpeedMultiplier = Mathf.Clamp(multiplier, 1f, 6f);
        }

        public static string RefillTools()
        {
            World();
            var equipment = Walker().GetComponent<MoonPrototypeEquipment>() ?? throw new InvalidOperationException("no tools to refill");
            equipment.ServiceTools();
            return "Brush serviced, battery full.";
        }

        /// <summary>Moves you to the ground or structure the camera points at, the way the dev menu's "go to aim" does.</summary>
        public static string TeleportToAim()
        {
            var world = World();
            var walker = Walker();
            if (walker.Mounted)
                throw new InvalidOperationException("leave the vehicle first");
            var camera = walker.viewCamera ?? throw new InvalidOperationException("no camera");
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            var best = float.MaxValue;
            var point = Vector3.zero;
            foreach (var hit in Physics.RaycastAll(ray, 8000f, -5, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < best && !hit.transform.IsChildOf(walker.transform))
                {
                    best = hit.distance;
                    point = hit.point;
                }
            }
            var centre = world.transform.position;
            if (best == float.MaxValue && world.Terrain != null)
            {
                for (var d = 2f; d < 8000f; d += 2f)
                {
                    var along = ray.GetPoint(d);
                    var ground = centre + world.Terrain.Point((along - centre).normalized);
                    if ((along - centre).sqrMagnitude <= (ground - centre).sqrMagnitude)
                    {
                        point = ground;
                        best = d;
                        break;
                    }
                }
            }
            if (best == float.MaxValue)
                throw new InvalidOperationException("aim at the ground or a structure");
            walker.Teleport(point + (point - centre).normalized * 1.2f);
            return $"Moved {best:0} m.";
        }

        // ---- Crewmates -----------------------------------------------------------------------------------------

        /// <summary>Whether this crewmate is in your shift (only then is there a body to knock down).</summary>
        public static bool InShift(ulong steamId) => Crewmate(steamId) != null;

        private static MoonRemotePlayer Crewmate(ulong steamId)
        {
            var session = MoonCrewSession.Instance;
            if (session == null || !MoonSession.InGame)
                return null;
            return session.Crewmates.FirstOrDefault(c => c != null && c.SteamId == steamId);
        }

        /// <summary>
        /// Knocks a crewmate over, the only thing the game lets one player do to another's body: it's the message a
        /// vehicle sends when it runs someone over, so they're told your push sweeper hit them, and they get up again.
        /// </summary>
        public static string Knock(ulong steamId)
        {
            World();
            var crewmate = Crewmate(steamId) ?? throw new InvalidOperationException("they're not in your shift");
            MoonCrewSession.Instance.RunOver(crewmate, 0);
            return $"Knocked {Kicks.NameFor(steamId)} over.";
        }
    }
}
