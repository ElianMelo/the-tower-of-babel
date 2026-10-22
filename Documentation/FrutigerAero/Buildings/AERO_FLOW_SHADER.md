# Aquatic finish shader

**Current finish restored by user request.** The subsequent [see-through border experiment](BORDER_GLASS.md) was rejected. All four materials again use this opaque aquatic finish, border width 0.11 m and strength 0.7. The glass keyword is off and the additional feature is removed from both renderers. [Restoration inspection](AquaticFinishRestored.json) confirms clean shader/C# compilation and preserved live motion clocks. The rejected pass/source remain inactive as history; they are not part of current rendering.

2026-10-21 shader revision; awaiting user review. Geometry follows the user-selected aquarium interior, `Assets/_Project/References/Buildings/frutiger-building-sample-2.jpg`, and the accepted rounded kit in [AQUATIC_ARCHITECTURE.md](AQUATIC_ARCHITECTURE.md). White architecture remains dominant. Blue water appears in the recessed arch panels, integral aqua bands and narrow slab borders. No green building colors are introduced.

![Current kit in Unity](Previews/AeroFlow-Kit.png)

![Animated water inset](Previews/AeroFlow-Motion.gif)

## Research and chosen approach

The building kit uses one opaque lit material and one submesh per model. The tower can contain tens of thousands of overlapping instances. For this setting, the implementation uses a frosted glass appearance with an apparent water layer beneath its surface, retaining a solid white structure and predictable depth, shadows and instancing. It does not render actual transparency or distort the scene behind the asset.

- [Unity URP asset documentation](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/universalrp-asset.html): scene-color refraction would require the opaque texture, captured before transparent objects. Avoiding that dependency also avoids relying on transparent sorting for this repeated kit. This is a project-specific design choice, not a claim that URP cannot render glass.
- [Unity Complex Lit documentation](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/shader-complex-lit.html): clear coat supplies a reflective dielectric layer over the base. The new shader uses URP's clear-coat lighting, with restrained reflections and a Fresnel edge over the aqua regions. Very bright probe highlights are limited in these regions so they do not erase the water tint.
- [Valve: Water Flow in Portal 2](https://cdn.fastly.steamstatic.com/apps/valve/2010/siggraph2010_vlachos_waterflow.pdf): two offset flow phases blend together to conceal repeating UV resets. The independently written flow helper uses this principle for both ripple normals and caustic layers.
- [NVIDIA GPU Gems: Rendering Water Caustics](https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-2-rendering-water-caustics): a convincing real-time caustic impression can use an approximation. Here, two moving texture layers provide shallow depth and white-blue light within the glaze; this is not a physical volume or a world-space caustic projector.
- [tuxalin/water-shader](https://github.com/tuxalin/water-shader): reviewed its depth/Fresnel/normal-map direction. Its README lists caustics as future work, so it is not credited as an implemented caustic algorithm. No code was copied.

The project's Stylized Water 3 normal/caustic libraries were inspected for visual compatibility. The building shader is independent and does not copy the proprietary asset's implementation.

## Implementation

- `Assets/_Project/Shaders/BuildingAquaticFinish.shader`: active `TowerOfBabel/Buildings/Aquatic Finish` shader, URP PBR, clear coat, opaque forward lighting, fog, instancing, reflection probes, shadow, depth and depth-normal passes. Water normals are consistent between forward and depth-normal passes.
- `Assets/_Project/Shaders/BuildingAquaticFlow.hlsl`: position-derived stable variation, flow blending, shallow refracted view offset, ripple normals, layered caustic light and distance fading. No additional runtime component or renderer feature is required.
- `Tools/BuildingAero/make_aquatic_fields.py`: reproducible periodic flow and normal fields. The 512-square flow map stores RG flow vectors, B phase noise and A height; the ripple map derives from the same periodic field. Import uses linear data, mipmaps, repeat wrapping and trilinear filtering. The ripple texture imports as a Unity normal map.
- `Tools/BuildingAero/ApplyWhiteMarble.cs`: configures the four existing material assets and texture import settings through Unity, retaining material/prefab GUIDs and model remaps. The older file name is retained for continuity; it now applies Aquatic Finish.

Blue regions come from the existing baked arch color and UV1 geometric color boundaries. A rounded rectangle distance field adds an approximately 0.11 m frosted strip at slab borders, with a soft transition. The step accounts for its existing 0.35 Z scale. This is surface shading; meshes and colliders do not move or gain vertices.

Each instance's world origin supplies a stable seed. Copies sharing the same material receive different pattern offsets, rotations, scale, phase and speed without separate materials. Both apparent depth layers use the same two-phase reset blending. A view-dependent shallow offset separates them, while the white body receives a much weaker moving water-light impression. This does not project light onto other scene objects.

Water detail stays at full strength to approximately 51 m and fades out by 85 m. Beyond that, the added flow, ripple and caustic texture samples are skipped, leaving stable glazing. Existing marble and baked maps remain. This limits distant shimmer and added texture work; no frame-time benchmark or target-device build was performed.

## Material controls

The active assets remain `Aero_WhiteMarble_Arch`, `Aero_WhiteMarble_Floor_Tile`, `Aero_WhiteMarble_Pillar` and `Aero_WhiteMarble_Step_Tile` under `Assets/_Project/Models/Buildings/FrutigerAero/`.

| Model | Polish | Pattern scale | Ripple strength | Water interior | Interior caustics | Apparent depth |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Arch | 0.82 | 0.85 | 0.035 | 0.45 | 0.40 | 0.060 |
| Floor | 0.88 | 0.45 | 0.025 | 0.25 | 0.25 | 0.035 |
| Pillar | 0.80 | 0.65 | 0.025 | 0.30 | 0.25 | 0.035 |
| Step | 0.85 | 0.55 | 0.025 | 0.30 | 0.28 | 0.035 |

Common settings: flow speed 0.075, deformation 0.9, variation 1, clear coat 0.55, border strength 0.7, Fresnel edge 0.15, frost reflection balance 0.25, body water light 0.12. Marble veins remain 0.10, fine relief 0.12 and baked normal strength 0.65. `Water pattern size` increases repeat frequency as its value increases; `Water color within glass` controls the interior tint, not transparency. `Preview clock` remains -1 on all saved materials so Unity time drives motion. Nonnegative values are for authoring captures only.

The previous `BuildingWhiteMarble.shader` and `BuildingWaterGlass.shader` remain as historical alternatives. Neither is assigned to the four active presets.

## Verification

No automated tests were created or run. Verification used compilation, source review, fixed-camera rendering, image comparison and the user-authorized final live Editor inspection.

- [AeroFlowInspection.json](AeroFlowInspection.json): 3,012 triangles total, unchanged bounds/pivots/GUIDs, one root mesh/submesh/material, no children, complete normals/tangents/UVs, eleven matching construction stages and empty shader diagnostics.
- [AeroFlowMaterialsInspection.json](AeroFlowMaterialsInspection.json): imported baked maps and all four opaque material presets.
- [AeroFlowCaptureInspection.json](AeroFlowCaptureInspection.json): 32 controlled animation frames, 0.16 s apart, with all material clocks restored to -1. Temporary comparison instances are destroyed after capture.
- [AeroFlowImageInspection.json](AeroFlowImageInspection.json): inset pixel differences for motion and two copies sharing the same material, compared against a variation-disabled control. Small control differences can come from scene lighting/reflections. These are visual evidence, not performance measurements.
- [AeroFlowLiveFrame0.json](AeroFlowLiveFrame0.json) and [AeroFlowLiveFrame1.json](AeroFlowLiveFrame1.json): actual Unity times 11.498 and 19.993, frames 2,200 and 3,857, both with preview override -1. Fixed-camera live images show changing water patterns.
- [AeroFlowTowerRuntime.json](AeroFlowTowerRuntime.json): 717 nearby pieces and 83,912 distant instances; all 44 far-stage batches use the new shader, maps, live clock and instancing. All 98 nearby arch colliders have volume. Sampled passage is open, upper panel and leg solid. Shader diagnostics are empty.
- [AeroFlowConsoleInspection.json](AeroFlowConsoleInspection.json): final Unity console/compilation inspection. An initial inspection helper queried `_WaterLight` instead of `_WaterLightMap`, producing 48 observer errors. The query was corrected, the live inspection rerun successfully, and these diagnostic messages preserved in the report before clearing the Editor console. They did not originate in the shader or runtime building code.
- [AeroFlowFinalEditorState.json](AeroFlowFinalEditorState.json): saved Tower scene restored in Edit Mode, clean, background overrides and project setting off.

Additional views: [arch](Previews/AeroFlow-Arch.png), [floor](Previews/AeroFlow-Floor.png), [step](Previews/AeroFlow-Step.png), [pillar](Previews/AeroFlow-Pillar.png), [assembly](Previews/AeroFlow-Assembly.png), [instance comparison](Previews/AeroFlow-InstanceVariation.png).
