# Admin Menu

A host admin menu for Moon Sweepers: kick and ban, give money, tools and upgrades, and change the time of day. Press **F1** while you host a crew, in the crew lobby or during a shift.

## Crew
- **Kick** a crewmate: they're cut off from your Moon for the rest of this crew.
- **Ban** a crewmate: the same, for every crew you host, saved across restarts.
- **Allow back** anyone you kicked or banned.
- Kick and Ban ask for a second click, so a stray click can't remove anyone.

## Money
- Add $1,000 to $1,000,000 with one click, or type an amount (2500, 2.5k, 3m) to add, remove or set the balance.
- The crew shares one balance in Moon Sweepers, so everyone sees the change straight away. It doesn't count toward your money achievements.

## Give
- Pick yourself or a crewmate in your shift, then give any tool, raise any upgrade a level or max it, or **Give everything**.
- Gifts are free: the crew's balance doesn't change. An upgrade comes with what it needs first (its tool and the upgrades before it).
- Crewmates get gifts within a few seconds, and they're saved with your Moon.

## World
- Set the time of day and how fast it passes; the whole crew's sky follows yours.
- Clean the hex you stand on, or two rings around it, for the whole crew.
- For you only: walk, fly or fly through walls (Space up, Ctrl down, Shift fast), fly and walk speed, go to where you aim, and refill your tools.

## Knock down
- On the Crew tab, **Knock down** bowls a crewmate in your shift over, as if a vehicle hit them. They get straight back up.

## How kicking works
Moon Sweepers has no kick of its own, and Steam won't let a lobby owner remove a member. So the kick happens on your end:
- Your game stops taking anything from the kicked player and stops sending them your Moon. Their avatar disappears for you.
- If they try to join your shift again, they never get your Moon, and the game sends them out of the lobby after 45 seconds.
- Crewmates who also have this mod drop the kicked player too. A kicked player who has the mod goes straight back to the main menu.
- A kicked player without the mod stays in the Steam lobby until they leave, and crewmates without the mod can still see them.

## Notes
- Only the host can use the menu, and nothing in it needs crewmates to have the mod. It's safe for everyone to install: crewmates who have it enforce your kicks.
- Esc or F1 closes the menu. The key (text such as `F1` or `F1 + LeftControl`), the Esc behaviour and the ban list are in `BepInEx/config/com.darkharasho.moonsweepers.adminmenu.cfg`.
- Needs BepInEx 6 for IL2CPP (BepInExPack_IL2CPP); the game is IL2CPP since 2026-10-24. Without a mod manager, install it into the game folder. On Linux/Steam Deck, set the launch options to `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
