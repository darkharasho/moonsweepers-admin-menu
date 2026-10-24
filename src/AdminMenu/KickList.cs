using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdminMenu
{
    /// <summary>One removed player as the menu lists them.</summary>
    internal struct RemovedPlayer
    {
        public ulong Id;
        public string Name;
        public bool Banned;
    }

    /// <summary>
    /// Who the host has removed. Kicks last for the current lobby; bans are saved in the config and apply to every
    /// crew the host runs. Kept free of game and Steam types for tests.
    /// </summary>
    internal sealed class KickList
    {
        /// <summary>Steam caps one lobby data value at 8192 bytes; leave room for the terminator.</summary>
        public const int MaxEncodedLength = 8000;

        private readonly Dictionary<ulong, string> _kicked = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, string> _banned = new Dictionary<ulong, string>();

        public bool Contains(ulong id) => _kicked.ContainsKey(id) || _banned.ContainsKey(id);

        public bool IsBanned(ulong id) => _banned.ContainsKey(id);

        public int Count => _kicked.Count + _banned.Count;

        public void Kick(ulong id, string name)
        {
            if (id == 0 || _banned.ContainsKey(id))
                return;
            _kicked[id] = name ?? "";
        }

        /// <summary>Bans, moving the player off the kick list if they were on it.</summary>
        public void Ban(ulong id, string name)
        {
            if (id == 0)
                return;
            if (string.IsNullOrEmpty(name) && _kicked.TryGetValue(id, out var kickedAs))
                name = kickedAs;
            _kicked.Remove(id);
            _banned[id] = name ?? "";
        }

        /// <returns>True if the player was on either list.</returns>
        public bool Pardon(ulong id) => _kicked.Remove(id) | _banned.Remove(id);

        /// <summary>A new lobby starts with no kicks; bans carry over.</summary>
        public void ClearKicks() => _kicked.Clear();

        public string NameOf(ulong id) =>
            _banned.TryGetValue(id, out var name) || _kicked.TryGetValue(id, out name) ? name : "";

        /// <summary>Bans first, then kicks, each by name.</summary>
        public List<RemovedPlayer> Entries()
        {
            var bans = _banned.Select(p => new RemovedPlayer { Id = p.Key, Name = p.Value, Banned = true });
            var kicks = _kicked.Select(p => new RemovedPlayer { Id = p.Key, Name = p.Value, Banned = false });
            return bans.OrderBy(p => p.Name).Concat(kicks.OrderBy(p => p.Name)).ToList();
        }

        public IEnumerable<ulong> Ids => _banned.Keys.Concat(_kicked.Keys);

        /// <summary>The config form of the bans: <c>id:name</c> pairs separated by commas.</summary>
        public string SaveBans() =>
            string.Join(",", _banned.OrderBy(p => p.Key).Select(p => p.Key + ":" + CleanName(p.Value)));

        /// <summary>Replaces the bans with the ones in <paramref name="text"/>, skipping anything malformed.</summary>
        public void LoadBans(string text)
        {
            _banned.Clear();
            if (string.IsNullOrWhiteSpace(text))
                return;
            foreach (var part in text.Split(','))
            {
                var colon = part.IndexOf(':');
                var idText = (colon < 0 ? part : part.Substring(0, colon)).Trim();
                if (!ulong.TryParse(idText, out var id) || id == 0)
                    continue;
                _kicked.Remove(id);
                _banned[id] = colon < 0 ? "" : part.Substring(colon + 1).Trim();
            }
        }

        /// <summary>
        /// The lobby data form: sorted ids separated by commas, cut short (bans first) to fit Steam's limit. Sorted so
        /// the same list always encodes the same way and the host only rewrites lobby data when it changes.
        /// </summary>
        public string EncodeIds()
        {
            var sb = new StringBuilder();
            foreach (var id in _banned.Keys.OrderBy(i => i).Concat(_kicked.Keys.OrderBy(i => i)))
            {
                var next = id.ToString();
                if (sb.Length + next.Length + 1 > MaxEncodedLength)
                    break;
                if (sb.Length > 0)
                    sb.Append(',');
                sb.Append(next);
            }
            return sb.ToString();
        }

        public static HashSet<ulong> DecodeIds(string text)
        {
            var ids = new HashSet<ulong>();
            if (string.IsNullOrEmpty(text))
                return ids;
            foreach (var part in text.Split(','))
            {
                if (ulong.TryParse(part.Trim(), out var id) && id != 0)
                    ids.Add(id);
            }
            return ids;
        }

        /// <summary>Names can't carry the separators of <see cref="SaveBans"/>.</summary>
        private static string CleanName(string name) =>
            string.IsNullOrEmpty(name) ? "" : name.Replace(",", " ").Replace(":", " ").Trim();
    }

    /// <summary>Who may be removed, and when a crewmate has to leave.</summary>
    internal static class KickRules
    {
        /// <summary>The host can't remove themself, and nobody can remove the host.</summary>
        public static bool CanRemove(ulong id, ulong localId, ulong hostId) => id != 0 && id != localId && id != hostId;

        /// <summary>A crewmate (never the host) who finds themself on the host's published list leaves the crew.</summary>
        public static bool ShouldLeave(bool isHost, ulong localId, ICollection<ulong> published) =>
            !isHost && localId != 0 && published != null && published.Contains(localId);
    }
}
