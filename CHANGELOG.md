## v1.0.0

- Initial release
- Map of caves and mines that shows only the path you have walked, opened with the map key (default M)
- Map in the style of the game's map: black background, title and "last updated" top left, cave name top right, back button bottom right
- Texts and cave names in the game language; caves without an own name are shown like on entering (e.g. "Cave – Hushed River Valley"), otherwise the scene name
- Cave icon at every entrance and exit
- Player is locked and the mouse cursor is shown while the map is open; close with the map key, Esc or Back
- "Path Radius" setting (meters): the path is drawn as a band of this radius in map scale, a new point is added after moving this far
- Cave maps are stored inside the save game with ModData: always the correct save, saved together with the game, deleted with the save
- Places can be excluded in `Mods\CaveExplorerExclude.json` (`ExcludedScenes`, default: `MineConcentratorBuilding`)
- Settings in `Mods\CaveExplorerSettings.json` (Mod Settings menu)
