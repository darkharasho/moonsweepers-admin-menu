using System.Globalization;

namespace AdminMenu
{
    /// <summary>Reads a dollar amount typed into the menu: "2500", "$2,500", "2.5k", "3m", "1b".</summary>
    internal static class MoneyInput
    {
        public static bool TryParse(string text, out decimal dollars)
        {
            dollars = 0m;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var s = text.Trim().Replace("$", "").Replace(",", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
            var scale = 1m;
            if (s.Length > 0)
            {
                switch (s[s.Length - 1])
                {
                    case 'k': scale = 1_000m; break;
                    case 'm': scale = 1_000_000m; break;
                    case 'b': scale = 1_000_000_000m; break;
                }
                if (scale != 1m)
                    s = s.Substring(0, s.Length - 1);
            }
            if (s.Length == 0 || s.StartsWith("-") || s.StartsWith("+"))
                return false;
            if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                return false;
            if (number > 1_000_000_000_000m / scale)
                return false;
            dollars = decimal.Round(number * scale, 2);
            return true;
        }
    }
}
