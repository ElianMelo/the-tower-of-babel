# Tower clearing and wave mountains

The four existing boundary pillars define the tower footprint in XZ. The generator fits a circle to their positions, ignoring their 500-metre center heights. The fitted center is approximately **(-0.240, 0.013)**. The current 96-metre tower radius plus a 12-metre margin produces a **108-metre grass-free clearing**. The clearing is flat at world Y ≈ 0, beneath the first tower floor; the player and nearby workstations remain on that flat approach.

The center uses matte green ground (`TowerGreen`) without grass blades. A 14-metre feather blends into vivid green meadow, followed by rounded hills. This replaces the earlier pearl-colored clearing. Long sine/cosine waves with gentle coordinate warping shape flowing, varied ridgelines instead of jagged noise or uniform circular terraces. Foothills begin 16 metres beyond the clearing and ease upward over 105 metres. The default height budget is 88 metres; sampled peaks reach about 79 metres. The outermost 65 metres blend back to the source terrain to retain its edge and shoreline heights.

Four small lakes sit outside the protected tower circle. Their elliptical footprints have gently irregular rims, smooth underwater bowls and eased green banks. They expose the existing `StylizedWater_Desktop` ocean at world Y = -1; no duplicate water planes are created. Basins are about 3 metres deep, with water radii of roughly 16–29 metres. Lake offsets, radii and rotations, depth and bank width are editable on the terrain component. The generator rejects banks that would enter the tower clearing or terrain perimeter. It also clears submerged grass and excludes submerged trees.

This is a baked, wave-shaped landscape, not terrain that oscillates during gameplay. Terrain rendering and collision use the same generated heightfield. Existing grass-detail prototypes and the exterior grass painting are reused, with the central circle removed and density reduced smoothly at its edge and on steep slopes. Trees, if later present in the source, are excluded from the clearing. Grass and ground textures stay matte, with soft organic color variation, avoiding glossy white ribbons on the hills.

## Regenerating and tuning

Use **Tower of Babel → Terrain → Generate Frutiger Wave Mountains** in the saved Tower scene. It updates the project-owned terrain, material and three ground layers, then saves the scene. A fresh setup copies the source terrain first. Later runs regenerate from the same original source rather than repeatedly multiplying grass density or adding heights.

The terrain child `Terrain/Terrain (-2 height for lake)` has an **Aero Tower Terrain** component. Tune its clearing padding/elevation, mountain height, wavelength, foothill width/setback, seed, grass feather, grass-slope limit and lake settings. Lake offsets are relative to the fitted tower center. Its component context menu **Generate Frutiger Aero Landscape** rebuilds the assigned generated TerrainData with those values. Save terrain and scene changes afterward when using this component command directly. The authoring menu preserves existing tuning, binds the existing ocean surface and refreshes the minimum tower radius from `TowerGenerator`.

Implementation:

- `Assets/_Project/Scripts/World/AeroTowerTerrain.cs` — deterministic heightfield, circle fit, ground paint and grass clearing; no per-frame terrain regeneration.
- `Assets/_Project/Editor/AeroTowerTerrainAuthoring.cs` — project-owned assets, authoring menu and scene assignment.
- `Assets/_Project/Editor/AeroTowerTerrainPreview.cs` — overhead and ground-level captures that restore the original camera afterward.
- `Assets/_Project/Environment/FrutigerTerrain/` — generated TerrainData, URP Terrain Lit material, terrain layers and small seamless ground textures.

The original `Assets/FullOpaqueGrass/Prefabs/Terrain/Terrain/Terrain.asset` and third-party grass materials/prefabs remain unchanged. Its dependency hash was `7f50bd5cc8a083027aff1610ae1d0561` before and after generation. The boundary pillars, tower generator, water and player/workstation transforms were preserved. The pre-existing Tower scene changes remain in the saved scene.

## Review and validation

- [Landscape overview](Previews/Terrain-Overview.png)
- [Circular clearing from above](Previews/Terrain-Clearing.png)
- [View from inside the clearing](Previews/Terrain-Ground.png)
- [Rounded mountain profiles](Previews/Terrain-Mountains.png)
- [Lake and green shoreline](Previews/Terrain-Lake-1.png)
- [Smaller lake](Previews/Terrain-Lake-4.png)

Unity compilation passed. Terrain-data inspection found **zero grass instances in 38,437 detail cells** inside the clearing and **zero grass instances in 6,875 submerged detail cells**. All 56 landscape collider samples and four lake-center samples hit the generated surface. Lake bottoms are approximately Y = -4.004, below the ocean surface at Y = -1. The Terrain and TerrainCollider reference the same generated asset. Detailed samples are saved in `TerrainInspection.json`.

Captures and collider inspection were performed in Edit Mode. The camera and its settings were restored, and the scene is saved and clean. No automated tests or Play Mode sessions were run; actual player traversal and multiplayer runtime loading remain unverified.
