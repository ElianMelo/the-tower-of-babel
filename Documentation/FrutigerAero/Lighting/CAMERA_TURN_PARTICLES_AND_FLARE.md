# Camera turning, floating particles and direct-sun flare

Implemented 2026-10-25. Performance is an optimization goal, with no fixed 2 ms cap on feature scope.

## Camera-turn optimization

The reflection path previously transformed every candidate instance's bounds again on each capture and submitted separate draws for each chunk. Deck selection also rebuilt candidate tile transforms and bounds on every query.

- Far render cells now cache each instance's world bounds and floor identity when their chunk data changes. Main-view page bounds reuse those same cached bounds.
- Reflection collection rejects whole spatial pages before checking individual instances, then groups eligible instances across chunks by mesh/material. The inspected floor-0 view submits the same 127 instances using five draws at 960 × 540.
- Completed deck bounds are cached by chunk object and construction version. Construction changes, replacement chunk data, hidden floors and scene teardown continue to invalidate or reject stale data. No reflection resolution, capture range or visual features were reduced.

The manually started `AeroCameraTurnReview` uses the actual completed tower fixture, two 360-degree sweeps per mode at three degrees per frame, and 220 measured frames per mode. It does not enter Play Mode or enable GPU profiling. [Before](CameraTurn/Before.json) and [after](CameraTurn/After.json) retain the measurements.

| Live-reflection render time | Before | After |
| --- | ---: | ---: |
| Median | 15.44 ms | 6.92 ms |
| 95th percentile | 24.32 ms | 9.91 ms |
| Worst sampled frame | 180.09 ms | 11.75 ms |
| Managed allocation in measured frames | 0 bytes | 0 bytes |

These are synchronous CPU-side Editor render timings on the RTX 4060 / Direct3D 12, including submission and possible driver waits. They are not gameplay FPS or GPU timings. Runs were in separate editor sessions following the initial failure below, so driver and warm-up variability remain a limitation. The controlled fixture measures building rendering; particles and the sun flare were reviewed separately.

## Floating particles

`AeroDayNight.asset` now exposes population and horizontal coverage multipliers of **1.3** and **1.15**. Runtime daylight/nighttime emission retains its existing density and interior gating, multiplied by 1.3. Capacity increases from 96 to 125.

The emitter changes from 20 × 20 m to approximately 21.448 × 21.448 m, yielding exactly 15% more horizontal area; its 2.4 m height is retained. The prefab's saved daylight emission changes from 1.95 to 2.535 particles/second. Colors, sizes, lifetimes, buoyant motion and floor-change clearing remain as before. Actual simultaneous counts vary with the existing random lifetimes and fades.

[Particle comparison](../../../_Screenshots-and-previews-to-delete/Documentation/FrutigerAero/Lighting/CameraTurn/Particles-After.png) and [appearance observations](CameraTurn/Appearance.json) use the real prefab with a fixed seed and a 14-second Edit Mode simulation. That sampled instant contains 21 particles before and 29 afterward; the emission increase is exactly 30%.

## Direct-sun flare

The existing native URP sun-flare system now has four soft image elements: a bright white core, warm halo, horizontal streak and a restrained cyan ghost. A small shared radial-gradient texture avoids a hard circular edge. The day/night profile drives intensity at 0.85 in daylight, fading to zero at night.

The flare attenuates toward the screen edge, rejects offscreen placement, and retains 32-sample depth occlusion. Camera post-processing and the existing URP lens-flare support remain enabled. [Direct view](../../../_Screenshots-and-previews-to-delete/Documentation/FrutigerAero/Lighting/CameraTurn/Sun-Look-After.png), [effect disabled](../../../_Screenshots-and-previews-to-delete/Documentation/FrutigerAero/Lighting/CameraTurn/Sun-Look-Before.png), [looking away](../../../_Screenshots-and-previews-to-delete/Documentation/FrutigerAero/Lighting/CameraTurn/Sun-Away.png) and [opaque occlusion](../../../_Screenshots-and-previews-to-delete/Documentation/FrutigerAero/Lighting/CameraTurn/Sun-Occluded.png) were rendered in a disposable scene using the production flare asset. The preview sun uses the same layer as its review camera.

## Validation and limitations

C# compilation, shader diagnostics, material/profile settings, reflection lifecycle inspection and controlled visual captures were used. Automated tests and Play Mode were omitted under the standing preference. The original scene and editor state are restored after review.

The initial turning sweep completed its measurements, but the editor subsequently exited with another native D3D12 fence failure. Recovery backups were preserved and the same project was reopened. Preview teardown now synchronizes submitted offscreen rendering before destroying its scene-owned targets. The updated sweep and subsequent visual/lifecycle reviews completed without another editor exit. This does not establish the native driver's underlying cause or prove live gameplay stability. No bug report was sent to Unity.
