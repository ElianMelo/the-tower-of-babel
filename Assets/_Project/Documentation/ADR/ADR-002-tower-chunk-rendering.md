# ADR-002: Data-Driven Tower Generation and Two-Tier Chunk Rendering

- Status: Accepted
- Date: 2026-08-29
- Scope: Editor tower generation, runtime chunk ownership, near-prefab pooling, and far GPU instancing
- Related architecture: `plan.md`, sections 2.6-2.7, 5-7, 15-17, 33, 40, and 44-46
- Related implementation: `ConfigureTower`, `TowerGenerator`, `ChunkManager`, `NearTowerAssetPool`, `IFarChunkRenderer`, and `InstancedFarChunkRenderer`

## Context

The tower contains tens of thousands of repeated structural assets. Keeping every generated floor, stair, pillar, and arch as an active scene GameObject scales CPU transform work, renderer bookkeeping, collision components, draw submission, scene size, and Editor overhead with the complete tower rather than with the player's visible neighborhood.

The measured development workload contains 84,629 assets in 965 chunks. At 45 or more floors, the previous representation produced approximately 4,000 batches, 13-20 million triangles, a 13.5 ms main thread, a 9 ms render thread, and about 60 FPS while moving in the Unity Editor on the target development machine.

The game nevertheless needs full prefabs near the player because those objects provide the authored hierarchy, colliders, and future interaction components. Distant chunks need only structural presentation. Construction progress also changes per asset, so rendering cannot own the authoritative stage state.

The architecture therefore needs to separate:

- temporary Editor authoring objects;
- persistent static chunk data and mutable stage data;
- near, interactive prefab presentation;
- far, non-interactive GPU presentation.

## Decision

### Ownership and data flow

`ChunkManager` is the runtime owner and lookup boundary for cached tower chunks. Renderers consume snapshots or chunk cache entries and do not discover tower topology by scanning the scene during gameplay.

```text
TowerGenerator (Editor-only temporary GameObjects)
        |
        v
ConfigureTower
        |
        v
ChunkManager cached chunk data + prefab references
        |
        +--> NearTowerAssetPool (3x3x3 full-prefab view)
        |
        +--> IFarChunkRenderer (GPU-oriented distant view)
```

After configuration, the children generated below `TowerParent` are destroyed. The scene retains serialized chunk records and references to prefab assets, not one scene GameObject per structural asset.

This scene cache is the current implementation of the static tower-data contract. The planned `TowerGenerationConfig.bin` may replace its serialization later without changing the rendering ownership boundary.

### Editor configuration pipeline

`ConfigureTower` is the single orchestration entry point. It runs only in the Unity Editor and executes these steps in order:

1. `ClearGeneratedTower`;
2. wait until the generator is no longer busy;
3. `GenerateTower`;
4. wait until generation is finished;
5. ensure the near and far renderer components exist;
6. copy one prefab reference for each supported asset type;
7. `CacheChunks`;
8. destroy all temporary generated assets below `TowerParent`;
9. wait until destruction is finished and mark the scene dirty.

The pipeline stops without deleting the generated objects when caching produces no assets, leaving the failed output available for inspection.

`TowerGenerator` may continue to use instantiated GameObjects, physics queries, and `TowerAsset` markers while authoring. Every generated object must be classified as exactly one of:

- `Floor`;
- `Stair`;
- `Pillar`;
- `Arch`.

The generator also exposes the complete prefab for each type. The prefab reference, rather than a mesh-only reference, is used by the near renderer.

### Chunk cache

`ChunkManager.CacheChunks` groups typed authoring objects by `ChunkKey`, using horizontal chunk size and floor height. It sorts chunks and assets deterministically before assigning a chunk-local index.

Each `ChunkAssetData` record contains only runtime data needed to recreate presentation:

```text
LocalIndex
AssetType
World Position
World Rotation
World Scale
Stage (byte)
```

The initial iteration defaults every asset to completed stage `10`. Stages `0-9` remain valid data values, but only stage 10 has a structural model in the current near and far render paths. Stage 0-9 therefore hides the completed model until intermediate-stage presentation is implemented.

Each cached chunk has a non-serialized version counter. A successful stage mutation increments the version so derived far-render data can be reused while unchanged and invalidated when required.

Static topology and transforms are not network rendering state. Future network synchronization sends mutable stage data keyed by the baked chunk and local asset order.

### Immediate near/far switching

The near neighborhood is the Chebyshev-radius-one set centered on the local player's chunk:

```text
X:     current - 1 ... current + 1
Floor: current - 1 ... current + 1
Z:     current - 1 ... current + 1
```

This creates at most 27 near chunk slots. A chunk transition updates presentation immediately; there is no fade or delayed handoff in this iteration.

When the player crosses a chunk boundary, `ChunkManager` computes the next desired set and updates only the difference:

- outgoing chunks are removed from the near pool and loaded into the far renderer;
- incoming chunks are removed from the far renderer and leased from the near pool;
- chunks present in both sets retain their existing prefab instances.

A direct stage change updates only the affected near pooled asset. A far stage change invalidates and reloads only that chunk's derived far data and spatial cell.

### Near rendering

Near chunks use the entire prefab, including its hierarchy and colliders. `NearTowerAssetPool` maintains a separate growable pool for floors, stairs, pillars, and arches. Pool capacity is demand-driven and must be able to cover all stage-10 assets present in the current 3x3x3 neighborhood.

The pool tracks one lease per active chunk and one pooled instance slot per cached local asset index. On a boundary crossing it:

- preserves leases belonging to retained chunks;
- makes outgoing instances immediately available to incoming chunks of the same type;
- defers deactivation until incoming rentals are complete, avoiding redundant disable/enable work;
- grows a type pool only when reusable instances cannot satisfy demand;
- deactivates surplus objects after synchronization.

Transform application for a sufficiently large incoming slab is scheduled through a Burst-compiled `IJobParallelForTransform`. Job completion remains immediate because the presentation switch must finish in the same update. Small updates remain serial to avoid job setup overhead.

Near runtime materials enable instancing and use light probes. Realtime shadow casting, shadow receiving, reflection probes, and motion vectors are disabled for tower structural renderers.

### Far rendering

Far rendering is accessed only through `IFarChunkRenderer`. `ChunkManager` passes a `FarChunkSnapshot` containing the chunk key, typed asset records, and cache version. Gameplay code must not depend on the concrete GPU implementation.

`InstancedFarChunkRenderer` uses one stage-10 render model for each asset type. Models may be configured explicitly or resolved from `ChunkManager`'s prefab set. The current implementation requires a root `MeshFilter` and `MeshRenderer` on each far model prefab.

Completed far assets are converted to object-to-world matrices and grouped by:

1. asset type;
2. spatial render cell;
3. render page of at most 1,023 instances.

Prepared per-chunk typed matrix lists are cached by chunk version. Moving back and forth across a boundary therefore reuses unchanged matrix preparation. Adding, removing, or changing a chunk dirties only the affected spatial cell; the global far-tower matrix set is not rebuilt.

Each page stores world bounds. Before submission, pages may be culled against the render camera frustum and an optional maximum draw distance. Cell floor span, horizontal span, and maximum distance are serialized tuning values so designers can balance culling precision against draw-call count for different tower sizes.

Far rendering uses `Graphics.RenderMeshInstanced`, instancing-enabled runtime materials, no collision, no realtime shadows, no reflection probes, and no motion vectors. When light probes are enabled, spherical-harmonic data is sampled once at page rebuild time and reused through a `MaterialPropertyBlock`; probes are not sampled for every instance every frame.

### Runtime and server boundary

The rendering components disable themselves in `UNITY_SERVER` builds. Headless simulation consumes tower/chunk data and never creates near pools, render pages, colliders, or presentation materials.

`ChunkManager` may currently coordinate both client visibility and player spatial indexing, but render-specific logic remains behind the near-pool and far-renderer boundaries. Authoritative simulation must not depend on pooled GameObjects or GPU state.

### Profiling contract

The implementation exposes counters for active near chunks and assets, added/removed chunks, positioned assets, loaded far chunks and instances, visible/culled pages, and far draw calls.

Profiler markers cover:

- `Tower.NearPool.Synchronize`;
- `Tower.NearPool.ApplyTransforms`;
- `Tower.FarRenderer.Render`;
- `Tower.FarRenderer.RebuildPages`.

The performance goal for the development workload is more than 100 FPS at 1080p while moving. This is a measured target, not a guarantee of the architecture: it must be validated in both the Editor and a development player build whenever prefab complexity, materials, chunk dimensions, or maximum floor count changes.

## Consequences

### Positive

- Scene GameObject count no longer scales with the complete generated tower.
- Runtime chunk configuration remains usable after all authoring objects are destroyed.
- Only a bounded 3x3x3 neighborhood pays for complete prefabs and collision.
- Retained near chunks keep object identity across a boundary crossing.
- Pool growth is bounded by observed near demand rather than total tower size.
- Far chunks use instanced draw submission and spatial CPU culling.
- Unchanged far chunks reuse prepared matrices when repeatedly promoted and demoted.
- Construction stage data has one owner and both render tiers derive presentation from it.
- The far renderer can be replaced without changing gameplay or chunk-cache callers.
- Server builds do not pay client rendering costs.

### Negative and tradeoffs

- Immediate switching can produce a CPU spike on a dense boundary even though only the entering slab changes.
- Completing the transform job in the same update limits latency but prevents it from being spread freely across frames.
- Full near prefabs retain the CPU and memory cost of their colliders and component hierarchies.
- Lazy pool growth can cause instantiation spikes the first time the player enters a denser neighborhood.
- One light-probe sample per far page is cheaper but less spatially accurate than one sample per instance.
- Spatial cells trade draw calls against culling granularity and require profiling-driven tuning.
- `Graphics.RenderMeshInstanced` still submits visible matrix pages every frame; it is not a persistent GPU-buffer solution.
- The current far model contract supports one root mesh/material per asset type and does not preserve arbitrary multi-renderer prefab hierarchy.
- The scene-serialized chunk cache is an interim format and increases scene serialization size until the binary bake is complete.

## Alternatives considered

### Keep every generated tower object in the scene

Rejected because renderer, transform, collider, and Editor overhead scale with the entire tower. The measured 45-floor workload did not meet the desired frame rate.

### Instantiate and destroy the 27 near chunks on every transition

Rejected because retained chunks would churn identical GameObjects and dense boundary crossings would create avoidable allocation, activation, and destruction work.

### Rebuild all near pooled objects on every transition or stage change

Rejected because only one chunk slab changes at a normal boundary and only one asset changes for a local stage update.

### Use GPU instancing for near chunks as well

Rejected for the initial iteration because near assets require the complete prefab and collision. The near material may still benefit from Unity's compatible instancing or batching, but object identity remains available.

### Render far chunks as individual prefab instances

Rejected because it recreates most of the renderer and transform overhead removed by the data bake.

### Maintain one global far matrix list per asset type

Rejected because one promoted, demoted, or modified chunk would invalidate large global lists and global bounds would prevent useful frustum culling.

### Delay or fade chunk switching

Rejected for this iteration. Immediate correctness at the chunk boundary was explicitly selected. A presentation transition may be reconsidered later without changing chunk ownership.

### Preallocate pools for the theoretical densest 27 chunks

Rejected as the default because it moves the largest allocation and instantiation spike to startup and may reserve much more memory than normal neighborhoods require. Pools grow to actual demand and retain capacity for reuse.

## Validation

EditMode coverage includes:

- deterministic negative-coordinate chunk mapping;
- exactly 27 unique keys in a radius-one neighborhood;
- deterministic cache ordering and local indices;
- cache survival after authoring-object destruction;
- the complete `ConfigureTower` generate/cache/destroy sequence;
- immediate near/far switching;
- boundary updates that retain unchanged near instances and reposition only the incoming slab;
- stage changes that do not recycle unrelated near objects;
- typed far instance loading/removal;
- spatial far-page frustum culling.

During implementation profiling, incremental boundary synchronization reduced a central nine-chunk/1,107-asset transition from approximately 11.9 ms to approximately 6.5 ms after outgoing-instance reuse. A settled Editor sample reached approximately 112 FPS, although Editor/tool activity produced substantial sample variance. Continuous movement profiling in a development build remains the acceptance measurement.

## Deferred work

- stage-specific meshes and materials for construction stages 0-9;
- current packed stage snapshots through `ApplyChunkStageSnapshot` without chunk reload;
- prewarming based on measured or baked per-type neighborhood maxima if first-entry spikes remain visible;
- persistent GPU buffers and `RenderMeshIndirect` if per-frame matrix submission remains material;
- occlusion or hierarchical culling if frustum and distance culling are insufficient;
- support for far prefabs containing multiple meshes or materials;
- production `TowerGenerationConfig.bin` writer/reader and content-hash validation;
- immutable/versioned job inputs that avoid temporary native allocations on every dense near transition;
- automated performance regression scenes for maximum configured floor count;
- final cell-size, distance, probe, collider, and material tuning from player-build profiling.
