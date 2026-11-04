# Tower construction progression

Construction is shared across players. Only the lowest unfinished floor can be built. Every cached asset on that floor must reach stage 10, including floor tiles, stairs, pillars and arches in all of its chunks, before the next unfinished floor unlocks.

## Structure and detail phases

Each floor is built in two phases. **Structure** is the floor tiles, stairs, pillars and arches. **Detail** sits on top of the structure to make it denser. The detail is buildable vines, which replaced the first placeholder detail bushes on 2026-10-31. There are twelve shapes (`TowerAssetType.Vine0`–`Vine11`): some climb from the pillar onto the arches, some stay on the pillar, and some cover only the arch. The outside and inside face of each pillar are separate building blocks.

- Detail is hidden and cannot be built until every structure asset on that floor reaches stage 10. Completing the structure releases it immediately, near and distant, and refreshes interaction prompts.
- The floor counts as complete, and the next floor unlocks, only after the detail also reaches stage 10.
- Rolling any structure asset back withdraws the floor's detail again. Built detail is also left out of distant rendering while it is withdrawn.
- `ChunkManager.GetFloorPhase`, `IsStructureComplete` and `IsAssetReleased` expose the phase; `DetailReleaseChanged` fires once per release or withdrawal. Server build validation goes through the same `CanConstructAsset` check.

Since 2026-11-04 the detail also includes the **lava lamps**: every lamp sconce on the pillars is a buildable block (`TowerAssetType.LavaLamp`) costing 1 **Iron Ingot** per stage, and it lights up only once fully built. See [lava lamps](FrutigerAero/Lighting/LAVA_LAMPS.md#building-the-lamps) and the [iron workshop](FrutigerAero/Resources/IRON_WORKSHOP.md).

Each vine costs 1 **Living Grass** per stage, the processed material grown from gathered Grass. See [vines](FrutigerAero/Foliage/VINES.md) for the shapes, placement and growth, and [grass and living grass](FrutigerAero/Resources/GRASS_WORKSHOP.md) for the resource chain.

The detail vines are separate from the decorative exterior bushes described below, which still appear only once the whole floor, including its detail, is complete. Completed floors and missing floor indices are skipped. The client presentation and server build validation use the same cached progression; the server checks both the start and completion of a build.

Only completed floors and the current unlocked floor are visible. Every higher floor stays hidden, including blueprints, partially built meshes, completed meshes and colliders, until all lower floors reach stage 10. Unlocking refreshes visibility immediately without requiring the player to move. Completed floors remain solid and cannot be built again. If construction is rolled back, later floors are hidden again.

On visible floors, stage 0 blueprints appear only within the existing 3 × 3 × 3 near-chunk neighborhood. Stages 1–10 continue through the instanced distant renderer. Cached asset data and stable identifiers are preserved while locked-floor objects are absent from the near pool.

Press **1** (number row or keypad) to highlight unfinished construction on the unlocked floor within **15 metres** of the player. The existing green outline lasts **2 seconds** using unscaled time. Pieces remain discoverable when materials are missing; material requirements still control whether building can begin. Pressing 1 again refreshes the scan. Completion, pooling, disconnecting, opening a modal menu or disabling the player clears the relevant scan highlights. Normal aimed interaction highlighting can remain after the scan ends.

The scan radius is configurable on `PlayerInteractionRaycaster`. Scans inspect materialized near assets and do not create distant objects or change networked construction state.

Exterior bushes appear only after their own floor is fully built: every construction asset on that floor must reach stage 10, and the floor must be unlocked. Bush rendering reads the current construction data every frame, so completion reveals the plants immediately and rollback hides them again. Placement, wind and distance culling are unchanged.

## Verification

Detail vines (revised 2026-11-01): `DetailPhaseEditModeTests` (11), `VineGrowthEditModeTests` (7) and `GrassWorkshopEditModeTests` (6) all pass. The project's EditMode assembly runs 222 of 232 green; the 10 failures are the same pre-existing ones listed below.


Detail phase (2026-10-31): all 9 new `DetailPhaseEditModeTests` passed. The project's EditMode assembly ran 207 of 217 green, and the progression (5/5) and cheat-menu (9/9) PlayMode suites passed. The 10 EditMode failures and the `GlassWaterFloorPlayModeTests.ChunkProjection` failure also fail with these changes removed: the font glyph, resource cancel, floor-tile collider, saved-island asset count and river-glass light intensity checks.

Earlier verification:

Verified with Unity CLI 1.0.0-beta.14 and Unity MCP in this project's Unity 6000.3.11f1 Editor. Compilation passed. All 25 chunk/progression Edit Mode checks and all 5 floor-unlock, foliage and scan Play Mode checks passed. Coverage includes hiding locked near and distant chunks even when snapshots contain built stages, revealing each next floor only after every prior asset reaches stage 10, retaining non-interactive completed floors, hiding later floors after rollback, bush batch submission only on completed floors, and the scan's expiry, retriggering, hover preservation, pooling, disconnects and modal menus. The Editor was restored to the Tower scene in Edit Mode.

The earlier wider Edit Mode suite passed 92 of 95 checks. Outstanding failures concern the experience prefab's middle-dot font glyph, an older resource-transform cancellation expectation, and the floor tile's interaction collider losing a raycast against coplanar ground. The wider Play Mode suite also exposed failures in the existing click-feedback, menu-font and paused-hover checks. These checks are outside this progression change; the overall suite is not green.
