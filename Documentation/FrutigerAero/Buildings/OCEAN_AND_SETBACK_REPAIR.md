# Continuous ocean and connected setback supports

The user's annotated [bug screenshot](../../../Frutiger%20Reference/Asset/Tower-Setback-And-Water-Bugs-User.png) identified a nearby water boundary, disconnected pillar pads at a shrinking floor, and unnecessary pads across the stairs.

## Water

The existing `StylizedWater_Desktop` keeps its owned water material and elevation at -1. Its fixed 1,000-metre plane is replaced by one continuous mesh: a 64-by-64 near grid shares boundary vertices with four increasingly coarse outer rings. There are no internal open mesh edges. The surface extends 131,072 metres from its camera-following origin; game cameras tagged MainCamera receive at least a 65,536-metre far clip. The ocean's outer edge therefore stays outside the visible range, including when the player flies.

`AeroOceanSurface` snaps the origin to the near grid while retaining water height. World-space UVs keep the water pattern anchored; near waves fade before the coarse distant rings and distance normals retain the existing surface shading. The mesh is a saved asset, not rebuilt or allocated each frame. It uses 5,249 vertices and 10,240 triangles in one draw, compared with the old plane's 10,201 vertices and 20,000 triangles. This is a geometry budget comparison, not a measured FPS claim. The installed package's 111,381-vertex ocean prefab was inspected but not installed.

`AeroOceanAuthoring.Apply()` configures the asset, renderer bounds, material and camera through Unity. `AeroOceanPreview.Capture()` reviews the actual scene water from 14 and 270 metres and restores the camera and ocean position. Terrain/grass budgets and localized lake-caustic footprints remain unchanged.

## Shrinking floors

The repair removes the old standalone support sectors, rebuilds required supports for the current pillar line and connects shrinking-floor supports inward to existing floor sectors. The connection searches for a landing supporting the entire width, then overlaps it at the same height. The complete floor mesh supplies walking collision along the connection.

The opposite support row for the preceding floor's cap line is no longer generated. Ordinary deck sectors, pillars, arches, stairs and their stages are preserved. The saved cache has 1,435 connected shrinking-floor supports, 1,505 fewer outer cap pads and 87,972 total construction assets. All 37,560 non-floor assets are preserved. Shrubs regenerate only on surviving supported deck; 164 current placements are grounded.

`TowerLightingRepairPreview.Capture(false,2)` reviews 2,108 cached assets around the second setback using completed stage meshes in a disposable scene; it changes no saved construction stages. [Connected setback](Previews/Setback-Connected.png), [ocean at 14 metres](Previews/Ocean-Horizon-14m.png), [ocean at 270 metres](Previews/Ocean-Horizon-270m.png).

## Verification

EditMode checks cover every internal ocean edge, triangle winding, the saved water asset, pixel coverage at multiple heights and after large camera moves, physical bridge collision, all inward bridge-edge overlaps, removal of outer islands, generation with stairs, every pillar/bush, existing lighting and tower progression. Final automated PlayMode verification covers ocean following without mesh replacement, lighting/resource cleanup and sequential tower visibility. Results are recorded in `OceanSupportValidation.json`. No manual Play Mode was used.
