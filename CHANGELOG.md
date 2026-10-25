# Changelog

## 0.3.1

- Built against the game update of 2026-10-25 (Steam build 26203101). No changes to the menu.
- If BepInEx stops loading after a game update ("Interop assemblies are possibly out of date" in the log, then no plugins), the interop assemblies need regenerating; this isn't caused by the mod.

## 0.3.0

- Ported to the game's IL2CPP build (BepInEx 6). Needs BepInExPack_IL2CPP; the Mono build (0.2.0 and older) no longer loads.
- The menu is drawn without IMGUI's automatic layout, which doesn't work in the IL2CPP build. Look and features are the same.
- The toggle key setting is now text (`F1`, or `F1 + LeftControl`).

## 0.2.0

- Money tab: add, remove or set the crew's balance.
- Give tab: give yourself or a crewmate any tool or upgrade, or everything, for free.
- World tab: time of day and time speed, clean the ground around you, and fly, noclip, speed and go-to-aim for yourself.
- Knock down a crewmate from the Crew tab.

## 0.1.0

- First release: a host admin menu on F1 with a Players list.
- Kick a crewmate for the current crew, or ban them from every crew you host (saved in the config).
- Allow kicked or banned players back.
- Crewmates with the mod enforce the host's kicks; a kicked player with the mod returns to the main menu.
