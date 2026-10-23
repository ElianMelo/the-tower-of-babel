# Floor and step construction chunks

Floor and step now share the pillar/arch construction structure. Each asset has 80 irregular closed chunks, a sparse blue stage-0 outline, nine cumulative random-fill meshes, and its unchanged completed mesh at stage 10. Eight chunks are added per step. Only incoming chunks form through the cyan dissolve shader; they then fly and rotate into their exact positions while existing pieces stay solid.

The finished floor is the current 136-triangle radial patch, sourced from `BuildingKit_RadialFloor.blend`, rather than the older rounded floor in the original kit source. The finished step remains its 284-triangle rounded slab at the existing `(1,1,0.35)` prefab scale. Exterior UVs, white/blue color bands, shared materials, prefab GUIDs, transforms, build costs and interaction durations are retained.

## Curved floor and walking surfaces

Floor chunks are bent by the same radius/scale cache as their destination stage. Original patch coordinates remain in UV2 for the aquatic material. Temporary pieces use the CPU-warped material mode, so they do not bend again around their moving origins. The step keeps its original nonuniform scale and box collider.

The floor's collider always uses the completed bent patch, including at blueprint and partial stages. Both the nearby asset pool and authoring generator retain that walking surface. The bush generator also measures stair clearance against the completed step bounds, avoiding the smaller blueprint outline.

Live updates to an already active pooled asset now call `ApplyStage` instead of rebinding it as an initial snapshot. This preserves the confirmation/landing animation. Fresh rentals still bind immediately to their saved stage. A focus cancellation does not interrupt a stage already confirmed by the server; unbinding or disabling still removes all temporary presentation.

## Assets and regeneration

- `ArtSource/Buildings/FrutigerAero/Deck_ConstructionStages.blend`, scene `Aero_DeckConstruction`: editable floor and step stages/chunks.
- `Tools/BuildingAero/build_deck_stages.py`: shared pillar cutting helper, floor seed `146892`, step seed `962415`, capped cuts, original UVs and exports.
- `Assets/_Project/Models/Buildings/FrutigerAero/Construction/Floor_TileStages.fbx` and `Step_TileStages.fbx`: combined stages 0–9.
- Matching `Floor_TileChunks.fbx` / `Step_TileChunks.fbx` and `.asset` libraries: separate incoming meshes and stage assignments.
- Existing `Floor_Tile.prefab` and `Step_Tile.prefab`: configured with the shared `PillarConstructionMotion` component and their own libraries. The type name is retained for compatibility.

Run the Blender script through Blender MCP, then call `DeckConstructionAuthoring.Apply()` or use **Tower of Babel → Buildings → Apply floor and step construction chunks**. Full-kit and radial-floor import helpers preserve these stage slots. Regenerate the chunk assets if the completed source geometry changes.

## Review

- [Floor and step arrival animation](Previews/Deck-Chunks-Arrival.gif)
- [Floor animation](Previews/Floor_Tile-Chunks-Arrival.gif) and [step animation](Previews/Step_Tile-Chunks-Arrival.gif)
- [Floor stages](Previews/Floor_Tile-Construction-Stages.png) and [step stages](Previews/Step_Tile-Construction-Stages.png)
- [Floor stage sequence](Previews/Floor_Tile-Construction-Sequence.gif) and [step stage sequence](Previews/Step_Tile-Construction-Sequence.gif)

`DeckConstructionPreview.CaptureStages()` renders every stage and records its walking collider. `PillarChunkPreview.Capture(true,"Floor_Tile")` and the corresponding `"Step_Tile"` call sample the real formation/flight controller. The floor preview uses a radius-12 annular cell; the step uses its saved scale. `Tools/BuildingAero/assemble_deck_construction.py` assembles the rendered frames.

## Inspection and limits

`DeckConstructionInspection.json` records preserved identities, completed meshes, materials, transforms and collider serialization; eighty chunks per asset; one submesh per stage; and complete UVs, normals and tangents. Pillar and arch prefab dependency hashes remain unchanged by authoring. Partial floor stages use 212–3,324 triangles; step stages use 324–3,254. Finished meshes remain 136 and 284 triangles respectively.

The floor/step topology reports record zero boundary or nonmanifold edges for all chunks and stages. Their different chunk sizes come from the random cells rather than equal grid sections.

`DeckPreviewInspection.json` records fixed collider bounds and successful downward walking-surface rays at height zero throughout all eleven stages. All 80 floor pieces were inspected at radii 4 and 12: vertices are finite, and patch coordinates, normals and tangents are retained. Separate motion reports record incoming-only dissolve, unchanged source geometry during flight, fixed colliders, restored source visibility and no shader diagnostics.

C# compilation and rendered Edit Mode inspection passed. No automated tests or Play Mode sessions were run. Live network delivery, the pooled confirmation path, full tower regeneration and target-device performance remain unverified in gameplay. Tower placement/cache data and existing bush placements were not regenerated.
