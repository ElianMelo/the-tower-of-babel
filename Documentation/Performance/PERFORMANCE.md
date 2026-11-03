# Performance (2026-11-03)

Feedback: outside the tower the game ran at ~30 fps, the first LOD attempt (cheap far shaders + vine image cards at 35–40 m) popped visibly and made vines disappear. That attempt was reverted. Target: 60 fps minimum, ~100 on the user's hardware.

## What is in place

- **GPU-resident far instancing.** `InstancedFarChunkRenderer` uploads each render cell's transforms (and inverses) to GPU buffers only when the cell rebuilds and draws pages with `Graphics.RenderMeshIndirect`. The tower shaders (pillar, arch, floor, glass floor, glass step, vine) have `#pragma instancing_options procedural:AeroFarSetup` (`Shaders/Aero/AeroFarInstancing.hlsl`); shading is unchanged. The old per-frame `RenderMeshInstanced` path remains as fallback (`GpuResidentInstancing`).
- **Mesh-only LOD, same material.** Finished Floor, Stair, Pillar, Arch and GlassWaterFloor draw ~50% triangle meshes past 70 m and ~30% past 140 m (stairs stay at 50%). At 1080p those distances keep the change below a pixel; tests compare full and LOD renders at game pixel density. Vines always use their full mesh. Build with **Tower of Babel/Performance/Build far mesh LODs** (`TowerMeshLodAuthoring`).
- Shadow budgets: sun 120 m / 3 cascades / 2048; spot-light atlas 1024 with 256 px shadows (fixes "Too many additional punctual lights shadows"); river cookie 1024, river shadows within 20 m.
- Nearby water light skipped when no lake is in view; river glass floors that refract nothing share one render pass.
- Rejected after measuring: depth priming (the far tower is vertex bound; the prepass cost 10 ms).

## Measured (Editor Play Mode, 1920×1080, `FrameRateBenchmarkPlayModeTests`)

| View | Before | Now |
| --- | --- | --- |
| Outside, ground, 160 m | 31.5 ms | 23.6 ms (GPU 18.5) |
| Outside, 260 m | 24.3 ms | 21.9 ms (GPU 12.4) |
| Outside, air, 300 m | 24.2 ms | 21.5 ms (GPU 13.1) |
| Inside, across the tower | 22.7 ms | 19.0 ms |

Not yet at 60 fps outside. The frame is now held by CPU render work.

## Next steps

1. Draw each render cell as one indirect command per mesh level instead of one per 511-instance page (~830 draws from outside; the render thread spends ~11 ms on per-draw state).
2. River glass composition costs ~6 ms of CPU (it re-sends each page's matrices every frame); move it to the same GPU-resident buffers.
3. The base scene without the tower already costs ~11 ms of GPU outside (shadows 3.6 ms, terrain, grass); profile it separately.
4. Profile a player build: Editor numbers include ~17 ms of Game view work on the main thread.
