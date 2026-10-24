namespace AdminMenu
{
    /// <summary>What pressing the toggle key should do.</summary>
    internal enum ToggleDecision
    {
        Open,
        Close,
        /// <summary>Not in a crew lobby: there's nobody to manage.</summary>
        NotHosting,
        /// <summary>A crewmate, not the host: tell them the menu is for the host.</summary>
        HostOnly,
        /// <summary>Loading, typing, or a game menu (pause, garage, lockers) is open: do nothing.</summary>
        Busy,
    }

    /// <summary>
    /// When the menu may open and when it must close itself. Closing is always allowed, so a scene change or a lost
    /// lobby can never leave the menu stuck open.
    /// </summary>
    internal static class MenuGate
    {
        public static ToggleDecision Decide(bool menuOpen, bool inLobby, bool isHost, bool busy)
        {
            if (menuOpen)
                return ToggleDecision.Close;
            if (!inLobby)
                return ToggleDecision.NotHosting;
            if (!isHost)
                return ToggleDecision.HostOnly;
            if (busy)
                return ToggleDecision.Busy;
            return ToggleDecision.Open;
        }

        public static string Message(ToggleDecision decision)
        {
            switch (decision)
            {
                case ToggleDecision.NotHosting:
                    return "Admin menu: host a crew first";
                case ToggleDecision.HostOnly:
                    return "Admin menu: host only";
                default:
                    return null;
            }
        }

        public static bool ShouldAutoClose(bool inLobby, bool isHost, bool loading) => !inLobby || !isHost || loading;
    }
}
