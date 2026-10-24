## Project Context

A BepInEx/Harmony mod for Moon Sweepers (DragDropGames, Steam app 5335510, Unity 6000.6 Mono, Steamworks.NET, Input System) that adds a host admin menu on F1 for kicking and banning crewmates. The game's code lives in the `MoonCleaners` namespace. Packaged for Thunderstore under `darkharasho`.

## How the kick works

There's no kick in the game, and its multiplayer is a Steam lobby plus a full mesh of `SteamNetworkingMessages` sessions: every member sends to every member in `MoonSteamLobby.Members` and accepts sessions from anyone `MoonSteamLobby.IsMember` approves (see `MoonCrewSession`: `Receive`, `SendToCrew`, `DropMissing`, the session-request callback). Steam won't let the owner remove a lobby member, so:

- `Patches/LobbyPatches.cs` postfixes `MoonSteamLobby.RefreshMembers` (removes blocked players from the private static `members` list) and `MoonSteamLobby.IsMember` (returns false for them). On the host that drops their packets, stops sends to them, destroys their avatar and closes their session. A client that joins a shift needs `WorldRequest` answered; when it's ignored the game's own 45s join timeout makes them leave the lobby.
- The host publishes the list as lobby data `darkharasho.kicked` (comma-separated SteamID64s). Modded crewmates read it in the same postfix and filter the same way; a listed player leaves (`MoonSession.ReturnToMenu`).
- A vanilla kicked player already in a shift stays in the Steam lobby with a frozen copy of the Moon until they leave; vanilla crewmates still see them. This is the known limit.

## UI and input

IMGUI (`AdminMenuController.OnGUI`, styles in `Theme.cs`): the game has no UI Toolkit documents to borrow a theme from. While open: `MoonInput.Typing = true` (PlayKeyboard/PlayMouse go null, so no play input and no pause on Esc; it stays "typing" for the frame it's cleared), `MoonWalker.UIBlocked = true` in a shift, free cursor, `EventSystem.current` disabled. IMGUI lists are snapshotted on the Layout event so a kick mid-frame can't break layout.

## Layout

- `src/AdminMenu/Plugin.cs`: config (ToggleKey, CloseOnEscape, Bans), Harmony, creates the controller
- `src/AdminMenu/AdminMenuController.cs`: hotkey, open/close, input lock, drawing
- `src/AdminMenu/Kicks.cs`: every call into the game for kicking; `Patches/LobbyPatches.cs`
- `src/AdminMenu/KickList.cs`, `MenuGate.cs`: pure logic, tested by `tests/AdminMenu.Tests`
- `scripts/package.sh` builds the Thunderstore zip; `scripts/update-refs.sh` regenerates `lib/refs` for CI
- Release: bump the version in `Plugin.cs`, the csproj, `thunderstore/manifest.json` and `CHANGELOG.md`, then push tag `v<version>` (Thunderstore and GitHub), or `v<version>-<suffix>` for a GitHub-only prerelease

## Notes

- Decompile with `DOTNET_ROOT=/home/linuxbrew/.linuxbrew/opt/dotnet/libexec nice -n 19 taskset -c 0-3 ~/.dotnet/tools/ilspycmd -p -o /tmp/moonsweepers-src ".../Moon Sweepers/MoonSweepers_Data/Managed/Assembly-CSharp.dll"`. Run builds and tests the same low-priority way; this machine freezes under full CPU load.
- BepInEx and the mod are managed by HML (`~/Documents/GitHub/hml`): Moon Sweepers is a custom Steam game there (`steam-5335510`, BepInEx 5), profile `default`, with the mod installed from this repo's GitHub releases (pre-releases on). Start it with HML's **Start modded**, which also sets up the Proton prefix; nothing goes in the game folder. `dotnet build` deploys into that profile's `BepInEx/plugins/darkharasho-moonsweepers-admin-menu`, overwriting the release DLL until HML next updates it.
- Thunderstore had no Moon Sweepers community at first release. The workflow's `--community moon-sweepers` is a placeholder with `continue-on-error`; set the real slug once it exists and remove `continue-on-error`.
- Protocol byte is 38 (`MoonCrewSession.Protocol`); the lobby version check means everyone runs the same game build.
