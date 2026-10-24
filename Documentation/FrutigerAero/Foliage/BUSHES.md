# Exterior tower bushes

Three rounded, broadleaf bush assets add small green details to the tower. They have different crown shapes and leaf arrangements as well as different sizes. The approximate mesh sizes are 0.53 × 0.43 × 0.60 m, 0.93 × 0.74 × 1.09 m, and 1.52 × 0.98 × 1.57 m. All three prefabs have a single mesh, a shared instanced material, no collider and no gameplay scripts.

The existing white/blue building palette is preserved: these are separate green plants, rather than green surfaces on the architecture.

## Placement

`TowerBushScatter` is attached to the scene's `TowerGenerator`. `GenerateTower()` refreshes its decoration data after construction geometry generation. The current saved Tower scene also has a generated layout, so rebuilding the full construction cache is unnecessary.

- Plants use only the exterior terraces created by setback floors. No shrubs are placed inside the wall radius, on non-setback floors without a supporting ledge, or beyond the ledge edge.
- Continuous random angles, uneven clusters, empty terraces, variable size, yaw and proportions avoid aligning vegetation with the repeating pillar/arch positions. A local seed preserves the layout without changing Unity's global random state.
- Placement reserves the full canopy radius plus wind movement, at least 0.55 m beyond the wall and 0.15 m inside the outer ledge edge. Large variants are skipped when the available width cannot support them.
- Stair rejection uses the actual step mesh bounds and the generator's four-flight placement math. It includes incoming stairs just below the terrace, outgoing steps and a 0.70 m walking margin beyond the canopy. The inspection helper separately checks the saved results against the scene's cached stair transforms.
- Plants also maintain space from one another. They are visual details without collision or construction stages.

The latest support repair regenerates **165 bushes**, retaining deterministic irregular clusters and empty terraces. Every full canopy/wind footprint is now checked against actual cached floor sectors, including missing stair sectors and support pads. Unsupported candidates are rejected. Each root uses its mesh's lowest point and is seated 4 cm into the real floor surface, so a single low leaf tip cannot leave the rest of the shrub visibly suspended. Tuning seed, terrace coverage or average cluster spacing regenerates the arrangement through **Regenerate exterior bushes**. Save the scene afterward.

## Wind and rendering

During gameplay, each terrace's bushes stay hidden until every cached construction asset on that floor reaches stage 10 and the floor is unlocked. The instanced renderer checks completion before submitting each terrace batch. Completion reveals the bushes immediately; rollback hides them again. Edit Mode authoring previews continue to show the foliage layout.

The opaque URP shader bends upper foliage in two slow sine/cosine waves and adds smaller leaf flutter. Vertex alpha increases the response with height, keeping the base effectively anchored. World position changes each plant's phase and green tone, avoiding synchronized motion. Forward lighting, shadow casting, depth and depth normals all use the same displaced geometry.

The scene stores placement data on the generator, outside the temporary construction hierarchy. It survives `ConfigureTower` clearing its generated objects after caching. Runtime rendering batches by terrace and size, with frustum and 150 m distance culling. No per-bush GameObject, collider, Update loop, network identity or persistent building ID is created at runtime. The tower now has 89,477 construction assets after adding base and ceiling supports; foliage support checks run during authoring, with no per-frame physics queries.

## Review and authoring

- [Three sizes](Previews/Bush-Sizes.png)
- [Wind motion](Previews/Bush-Wind.gif)
- [Actual terrace placement](Previews/Bush-Terrace.png)
- [Overhead stair and exterior clearance](Previews/Bush-Terrace-Overhead.png)
- Blender source: `ArtSource/Foliage/Aero_GreenBushes.blend`, scene `Aero_GreenBushes`.
- Mesh source script: `Tools/BuildingAero/build_aero_bushes.py`, run through Blender MCP.
- Unity authoring menu: **Tower of Babel → Foliage → Import and scatter exterior bushes**. This imports the meshes, updates the three project-owned prefabs and shared material, regenerates only foliage and saves Tower. Existing scatter tuning is preserved.

`TowerBushPreview.Capture()` renders actual assets, sampled shader motion and a completed representative section of cached tower geometry in a disposable Edit Mode scene. The preview does not build the live tower or alter construction stages. The source Tower scene and camera state remain unchanged after capture.

The initial authoring inspection in `BushInspection.json` records zero bush colliders, zero overlaps against 21,120 cached stairs, a minimum wall clearance of approximately 0.563 m and a minimum ledge margin of approximately 0.164 m, including the reserved wind footprint. Compilation and shader inspection passed at that time.

The subsequent progression integration passed 25 relevant Edit Mode checks and 5 Play Mode checks. Using the actual bush mesh and material, the runtime rendering check confirms zero submitted bush batches before completion, one terrace after its last asset reaches stage 10, additional terraces after their completion, and immediate hiding after rollback. Traversal and multiplayer startup were not rechecked in this foliage change.
