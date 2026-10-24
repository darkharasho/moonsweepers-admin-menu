# Manual verification, 0.1.0

Needs two Steam accounts (host with the mod; crewmate with and without it).

- [ ] Game starts with the mod; `BepInEx/LogOutput.log` shows `AdminMenu 0.1.0 loaded` and no patch errors.
- [ ] F1 with no crew shows "Admin menu: host a crew first"; as a crewmate, "Admin menu: host only".
- [ ] Host, crew lobby: F1 opens the menu over the main menu; clicks don't reach the lobby buttons; Esc and F1 close it.
- [ ] Host, in a shift: menu opens, the player can't move or look, the cursor is free; Esc closes without opening the pause menu; play resumes with the cursor locked.
- [ ] F1 does nothing with the pause menu, garage or chat open.
- [ ] Kick (two clicks) a vanilla crewmate in a shift: their avatar disappears for the host within ~30s, the host's Moon stops updating for them, they move to REMOVED.
- [ ] The kicked vanilla player rejoins: they're never given the Moon and leave the lobby after ~45s.
- [ ] Kick a crewmate who has the mod: they're returned to the main menu with "The host removed you from their crew."
- [ ] A third crewmate with the mod stops seeing the kicked player.
- [ ] Ban survives a restart (config `Bans`), and a banned player can't get into a new crew's shift.
- [ ] Allow back: the player can join again.
