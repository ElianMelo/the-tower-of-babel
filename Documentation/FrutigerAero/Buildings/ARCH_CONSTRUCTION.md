# Arch construction chunks

The arch now follows the pillar's construction structure: a sparse blue blueprint at stage 0, randomly filled stages 1–9, and the unchanged completed arch at stage 10. Eight of its 80 irregular chunks arrive at each step. Their sizes and positions come from a seeded Voronoi partition, with a shuffled fill order across both legs and the upper panel. No horizontal lift or repeated column pattern determines the progression.

Incoming pieces form outside the arch using the existing cyan dissolve shader, then fly and rotate into their exact final positions. Only these pieces dissolve; the previously built stage remains solid. The same `PillarConstructionMotion` controller and `PillarChunkLibrary` data structure are reused with an arch-specific mesh library. Their original type names are retained for compatibility with the pillar prefab and existing `Building` hooks.

The finished geometry, recessed aqua glazing, curved doorway, flat top, white/blue material, prefab identity, root transform and nonconvex finished mesh collider are preserved. The collider remains fixed during assembly. The original baked color and surface UVs remain on exterior faces; capped cut faces sample a white region of the existing texture. The source's two open glazing sheets receive tiny inward backings in the fracture copy to create closed pieces. This does not modify the original completed mesh or its collider.

## Assets and regeneration

- `ArtSource/Buildings/FrutigerAero/Arch_ConstructionStages.blend`, scene `Aero_ArchConstruction`: editable stages and individual chunks.
- `Tools/BuildingAero/build_arch_stages.py`: seed `914317`, arch outline guides, random cells, closed cuts, UVs and exports. Reuses the pillar cutting helper and original finished arch outline.
- `Assets/_Project/Models/Buildings/FrutigerAero/Construction/ArchStages.fbx`: combined stages 0–9.
- `ArchChunks.fbx` and `ArchChunks.asset` in the same folder: 80 incoming meshes and their Unity stage assignments.
- `Assets/_Project/Prefabs/Buildings/Arch.prefab`: existing prefab, now assigned the stage meshes and shared motion controller.

Run the Blender script through Blender MCP, then call `ArchConstructionAuthoring.Apply()` or use **Tower of Babel → Buildings → Apply arch construction chunks** in Unity. The full-kit import helper preserves all four construction asset stage assignments. [Floor and step now use the same structure](DECK_CONSTRUCTION.md).

`ArchConstructionPreview.CaptureStages()` captures every static stage. `PillarChunkPreview.Capture(true,"Arch")` samples the shared formation/flight controller using the arch library in a disposable Edit Mode scene. `Tools/BuildingAero/assemble_arch_construction.py` assembles the review images and animations.

## Review

- [Chunk formation and arrival](Previews/Arch-Chunks-Arrival.gif)
- [All construction stages](Previews/Arch-Construction-Stages.png)
- [Stage sequence](Previews/Arch-Construction-Sequence.gif)
- [Forming](Previews/Arch-Chunks-Forming.png), [flying](Previews/Arch-Chunks-Flying.png), [landed](Previews/Arch-Chunks-Landed.png)

The animation preview demonstrates stage 2→3 with a sample interaction progress. Live motion follows the real progress and authoritative stage notification. Initial pool binding and distant instanced batches keep the static saved stage meshes, as with the pillar.

## Inspection and limits

`ArchConstructionInspection.json` records preserved prefab GUID, completed mesh, material, root position/scale, collider, and unchanged pillar/floor/step prefab dependency hashes. Each construction mesh has one submesh and complete UVs, normals and tangents. Stage meshes range from 860 to 13,776 triangles; the finished original remains 1,680 triangles. Partial stages carry extra capped interior faces.

`ArchConstructionTopology.json` records zero boundary/nonmanifold edges in all 80 chunks and the partial stages. The original completed source keeps its two intentionally open glazing sheets. `ArchChunkVolumeInspection.json` records chunk volumes of about 0.0048–0.0941 cubic metres. Their summed volume matches the closed fracture source within about 0.004%.

`ArchChunkMotionInspection.json` records dissolve only on incoming renderers, an unchanged source mesh during flight, fixed collider bounds, restored source visibility, and no shader diagnostics. `ArchPreviewInspection.json` records an open passage ray and solid leg/upper-panel rays against the preserved collider. C# compilation and rendered Edit Mode inspection passed. No automated tests or Play Mode sessions were run. Live multiplayer timing and target-device performance remain unverified.
