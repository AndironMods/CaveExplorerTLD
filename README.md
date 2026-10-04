# CaveExplorer – Cave Map for The Long Dark

## What Is This Mod?

**CaveExplorer draws a map of the caves and mines you explore.** The game has no map for these places, so it is easy to get lost. With this mod the map key shows the path you have walked so far.

- Works only inside caves and mines (places you enter through a loading screen)
- Shows only your own path and your current position
- The path is saved per save game and is still there when you come back
- Outside of caves the normal map works as before


## How To Use

1. Enter a cave or mine
2. Walk around – your path is recorded automatically
3. Press the **map key** (default **M**) to open or close the cave map. **Esc** or the **Back** button also close it

The map looks like the game's map: black where you have not been, your path as a light line, a cave icon at every entrance and exit. The title and "last updated" are shown top left, the cave name top right, all in your game language. North is at the top. The red dot is your position, the short red line shows where you are looking.


## Installation

1. **Download MelonLoader** - Required foundation for mods  
   Visit: https://github.com/LavaGang/MelonLoader/releases

2. **Download ModSettings** - Lets you configure the mod in-game  
   Visit: https://github.com/DigitalzombieTLD/ModSettings/

3. **Download ModData** - Stores the cave maps inside your save games  
   Visit: https://github.com/dommrogers/ModData/releases

4. **Copy `CaveExplorer.dll` into your Mods folder:**
   ```
   C:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\
   ```

5. **Launch game** - Mod loads automatically


## Customization (In-Game Menu)

Open **Options → Mod Settings → CaveExplorer**:

- **Enabled** - Turn the mod on or off
- **Sample Interval** - How often your position is checked
- **Path Radius** - Radius in meters around you that is shown as explored, so the path fills the passages you walked through. A new point is added after moving this far

The settings are stored in `Mods\CaveExplorerSettings.json`.


## Requirements

- **The Long Dark** v2.55 or higher
- **MelonLoader** v0.7.x
- **ModSettings** v2.2.5
- **ModData** v1.5.5


## Excluding Places

Every place you enter through a loading screen whose internal name contains "Cave" or "Mine" gets a map. To turn it off for single places, add their scene names to `Mods\CaveExplorerExclude.json` if nessessary.
The scene name is shown in the MelonLoader console when you enter a cave ("Entered cave …"). Changes are used the next time you enter a place, no restart needed. If the file has an error, the mod shows it in the console, keeps using the last valid list and does not change the file.


## Notes

- Small open caves that belong to the outdoor world (no loading screen) are not mapped.
- The cave maps are stored with ModData inside each save game in `Mods\ModData\<slot>.moddata`. After a death or when you load an older save, the map shows the state of that save. Deleting a save deletes its cave maps too.
