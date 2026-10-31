# Pillar chunks, formation and arrival

> **Superseded mesh, current method (2026-10-30).** The pillar now uses the [Bubble Tube pillar](BUBBLE_TUBE_PILLAR.md). Its stages and chunks are generated the same way: 80 Voronoi chunks, seed 712809, eight per step, a blueprint stage 0, and the same `PillarConstructionMotion` and dissolve. They come from `build_bubble_tube_pillar.py` and are applied by `BubbleTubePillarAuthoring`. The assets and scripts below describe the previous white pillar and remain as history.

The pillar is divided into 80 closed, irregular Voronoi chunks of different sizes. Seeded random selection fills scattered areas across the whole pillar, without horizontal lifts, radial columns or a bottom-to-top pattern. Stage 0 keeps the accepted sparse blueprint; stages 1–9 add eight chunks each; the final stage 10 uses the unchanged original complete pillar and fills the remaining eight. The percentage labels refer to piece counts, not equal volume. The [arch now reuses the same controller and library structure](ARCH_CONSTRUCTION.md) with its own chunks.

## Animation sequence

1. Each incoming chunk appears outside the pillar through a flowing Unity dissolve, with a cyan formation edge.
2. Once formed, it flies along a short curved path, turning toward its final orientation.
3. It lands at its exact original mesh location. The existing pillar pieces and blueprint remain solid throughout.
4. Once the server confirms the new stage and the incoming pieces have landed, the temporary presentation hands off to the equivalent saved stage mesh.

The shader is enabled only on the incoming chunk renderers through a material property block. The permanent pillar and the previous-stage presentation keep `_BuildMode=0`; there is no whole-pillar dissolve or outgoing-stage crossfade. Formation finishes before flight starts for each piece. The eight arrivals are staggered across interaction progress.

`PillarConstructionMotion` is shared by the pillar and arch prefabs, each with its own library. `Building` supplies interaction progress, cancellation, local completion and authoritative stage changes. Cancel/reject/unbind/disable removes transient pieces and restores the source renderer. Initial pool binding applies the saved mesh immediately; nearby live stage changes can play the assembly sequence. Distant instanced batches retain their static stage meshes. No inventory or stage authority is moved into the animation.

## Models and assets

- `ArtSource/Buildings/FrutigerAero/Pillar_ConstructionStages.blend`, scene `Aero_PillarConstruction`: editable chunk and stage source.
- `Tools/BuildingAero/build_pillar_stages.py`: reproducible Voronoi cutting, capped interiors, seed `712809`, shuffled fill order and exports.
- `Assets/_Project/Models/Buildings/FrutigerAero/Construction/PillarStages.fbx`: combined stages 0–9 for the existing near/far stage system.
- `PillarChunks.fbx` in the same folder: 80 separate incoming-piece meshes, grouped by the step that adds them.
- `PillarChunks.asset`: Unity mesh-to-stage library assigned to the motion component.
- `Assets/_Project/Shaders/BuildingAquaticFinish.shader`: retains the aquatic finish; the optional incoming-piece dissolve uses the same clip in forward, depth, depth-normal and shadow passes.

Solid outer surfaces retain the source UVs, white/aqua color boundaries and original profile. Cut faces are capped. Stage meshes have one submesh and share the existing pillar material. The prefab GUID, root transform, capsule collider and completed mesh are preserved. Arch, floor and step construction stages remain unchanged.

## Review and validation

- [Chunk formation and arrival](Previews/Pillar-Chunks-Arrival.gif)
- [Forming](Previews/Pillar-Chunks-Forming.png), [flying](Previews/Pillar-Chunks-Flying.png), [landed](Previews/Pillar-Chunks-Landed.png)
- [All random-fill stages](Previews/Pillar-Construction-Lineup.png)
- [Stage sheet](Previews/Pillar-Construction-Stages.png) and [stage sequence](Previews/Pillar-Construction-Sequence.gif)
- Separate review scene: `Assets/_Project/Scenes/PillarConstructionReview.unity`.

`PillarConstructionAuthoring.Apply()` imports the stage and piece meshes, builds the library, and configures the existing prefab. Its review commands create the separate scene and capture stage views. `PillarChunkPreview.Capture()` samples the actual controller and shader in a disposable Edit Mode scene. `Tools/BuildingAero/assemble_pillar_chunks.py` assembles the resulting frames.

`PillarConstructionInspection.json` records 80 chunks, eight per step, preserved identities, UVs, normals and tangents. Combined construction meshes range from 812 to 7,106 triangles; the completed original remains 764 triangles. The topology report records zero boundary or nonmanifold edges across all stage and piece meshes. Individual chunk volumes range from about 0.0046 to 0.0792 cubic metres.

`PillarChunkMotionInspection.json` records incoming-only dissolve, an unchanged source mesh during flight, a fixed collider and restored source visibility. C# compilation and shader diagnostics passed. These are rendered Edit Mode samples; no automated tests or Play Mode sessions were run. Live network timing and target-device performance remain unverified.
