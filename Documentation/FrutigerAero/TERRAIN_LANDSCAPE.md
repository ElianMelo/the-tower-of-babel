# Tower clearing and wave mountains

The four existing boundary pillars define the tower footprint in XZ. The generator fits a circle to their positions, ignoring their 500-metre center heights. The fitted center is approximately **(-0.240, 0.013)**. The current 96-metre tower radius plus a 2-metre margin produces a **98-metre grass-free foundation**. The actual tower footprint stays level at world Y ≈ 0 so its floor and workstations remain grounded. The wider 108-metre flat clearing is superseded by this requested revision.

The center uses matte green ground (`TowerGreen`) without grass blades. A 3.5-metre grass feather leads into rolling meadows, with up to 10 metres of gentle variation outside the foundation. Long domain-warped sine/cosine waves and staggered ridge strengths avoid a uniform circular mountain wall. Foothills begin 32 metres beyond the clearing and ease upward over 180 metres; the ridge height budget is 78 metres, using a 310-metre primary wavelength. The outermost 65 metres blend back to the source terrain to retain its perimeter heights.

Three lakes sit outside the protected tower circle. The main lake is southwest of the tower at offset (-50, -190), with radii (52, 34) metres. The other lakes lie at (195, 70) and (-190, 140). Broad low approaches suppress nearby hills before carving the basins, so the water is visible rather than hidden inside steep wells. Gently irregular rims, 18-metre shore transitions and smooth underwater bowls expose the existing `StylizedWater_Desktop` ocean at world Y = -1. Basins remain about 3 metres deep. The generator rejects banks that enter the tower clearing or terrain perimeter, clears submerged grass and excludes submerged trees.

This is a baked landscape. Terrain rendering and collision share the generated heightfield. Existing grass-detail prototypes are reused, with deterministic grass across the dry outer terrain. The center and water stay free of blades, with density softened at the border and on steep slopes. The 250-metre grass setting is superseded by the performance pass below. Meadow textures continue into the distance. Trees, if later present in the source, stay outside the clearing. Grass and ground surfaces remain matte.

## Performance pass: sand and glass update

The generated terrain now uses a **513 × 513 heightfield**, **256 × 256 ground map**, **128 base map**, and **512 × 512 grass map** with 32-cell patches. Its one-kilometre bounds, foundation, lakes and hill shapes are preserved. The source stays at its original resolution; only the perimeter is sampled from it during baking. Terrain geometry uses instancing, with pixel error 8 during gameplay and 12 in the editor. The generated asset fell from 15,293,484 to 1,164,428 bytes.

Grass is explicitly authored in **InstanceCountMode**, capped at 4 clumps per map cell, with **708,232 total authored clumps before visibility/density scaling**. The previous coverage map contained 243,709,906 density units; those coverage units are not literal instance counts and are no longer inherited. Runtime grass draws within **95 metres at 0.55 density**. `TerrainPresentationQuality` uses only **35 metres at 0.08 density in Edit Mode**, then automatically selects the runtime settings when the game starts. It changes settings only on activation, validation or a play-state transition. No terrain or grass map is regenerated each frame.

These are measured asset/data reductions, not an FPS benchmark. Runtime checks use the automated Play Mode runner; visual captures use disposable Edit Mode previews.

## Regenerating and tuning

Use **Tower of Babel → Terrain → Generate Frutiger Wave Mountains** in the saved Tower scene. It updates the project-owned terrain, material and three ground layers, then saves the scene. A fresh setup copies the source terrain first. Later runs regenerate from the same original source rather than repeatedly multiplying grass density or adding heights.

The terrain child `Terrain/Terrain (-2 height for lake)` has an **Aero Tower Terrain** component. Tune its clearing padding/elevation, meadow height, mountain height, wavelength, foothill width/setback, seed, grass feather, slope limit and lake settings. Lake offsets are relative to the fitted tower center. The component context command rebuilds using its current values; save terrain and scene changes afterward. The authoring menu reapplies the requested rolling-meadow preset, binds the existing ocean, and refreshes the tower radius from `TowerGenerator`.

Implementation:

- `Assets/_Project/Scripts/World/AeroTowerTerrain.cs` — deterministic heightfield, circle fit, ground paint and grass clearing; no per-frame terrain regeneration.
- `Assets/_Project/Editor/AeroTowerTerrainAuthoring.cs` — project-owned assets, authoring menu and scene assignment.
- `Assets/_Project/Editor/AeroTowerTerrainPreview.cs` — overhead and ground-level captures that restore the original camera afterward.
- `Assets/_Project/Environment/FrutigerTerrain/` — generated TerrainData, URP Terrain Lit material, terrain layers and small seamless ground textures.

The original `Assets/FullOpaqueGrass/Prefabs/Terrain/Terrain/Terrain.asset` and third-party grass materials/prefabs remain unchanged. Its dependency hash was `7f50bd5cc8a083027aff1610ae1d0561` before and after generation. The boundary pillars, tower generator, water and player/workstation transforms were preserved. The pre-existing Tower scene changes remain in the saved scene.

## Review and validation

- [Landscape overview](Previews/Terrain-Overview-Lightweight.png)
- [Tower foundation from above](Previews/Terrain-Clearing-Lightweight.png)
- [View from inside the foundation](Previews/Terrain-Ground-Lightweight.png)
- [Rounded mountain profiles](Previews/Terrain-Mountains-Lightweight.png)
- [Main lake and low approach](Previews/Terrain-Lake-1-Lightweight.png)
- [Eastern lake](Previews/Terrain-Lake-2-Lightweight.png)
- [Northwestern lake](Previews/Terrain-Lake-3-Lightweight.png)

Terrain-data inspection found **zero grass instances in 7,903 detail cells** inside the foundation and **zero grass instances in 3,476 submerged detail cells**. All inspected landscape and lake-center collider samples hit the generated surface. Lake bottoms remain approximately Y = -4, below the ocean at Y = -1. Terrain and TerrainCollider reference the same generated asset. Detailed samples are saved in `TerrainInspection.json`.

Captures and collider inspection use Edit Mode and restore the original camera afterward. New captures temporarily apply runtime grass before restoring sparse editor grass. Focused automated checks verify foundation elevations, meadow heights, lakes, no center/underwater grass, populated dry outer meadow, terrain data limits and both quality settings. The latest resource/performance regression run passed **63 Edit Mode and 16 Play Mode checks**; see `Resources/SandGlassValidation.json`.
