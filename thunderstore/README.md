# Admin Menu

A host admin menu for Moon Sweepers. Press **F1** while you host a crew, in the crew lobby or during a shift.

## Players
- **Kick** a crewmate: they're cut off from your Moon for the rest of this crew.
- **Ban** a crewmate: the same, for every crew you host, saved across restarts.
- **Allow back** anyone you kicked or banned.
- Kick and Ban ask for a second click, so a stray click can't remove anyone.

## How kicking works
Moon Sweepers has no kick of its own, and Steam won't let a lobby owner remove a member. So the kick happens on your end:
- Your game stops taking anything from the kicked player and stops sending them your Moon. Their avatar disappears for you.
- If they try to join your shift again, they never get your Moon, and the game sends them out of the lobby after 45 seconds.
- Crewmates who also have this mod drop the kicked player too. A kicked player who has the mod goes straight back to the main menu.
- A kicked player without the mod stays in the Steam lobby until they leave, and crewmates without the mod can still see them.

## Notes
- Only the host can use the menu. It's safe for everyone to install: crewmates who have it enforce your kicks.
- Esc or F1 closes the menu. The key, the Esc behaviour and the ban list are in `BepInEx/config/com.darkharasho.moonsweepers.adminmenu.cfg`.
- Without a mod manager, install BepInEx 5 into the game folder. On Linux/Steam Deck, set the launch options to `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
