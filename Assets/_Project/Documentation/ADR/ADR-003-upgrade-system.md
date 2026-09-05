# ADR-003: Config-Driven, Server-Authoritative Job Upgrades

- Status: Accepted
- Date: 2026-09-05
- Scope: MVP job progression, upgrade configuration, purchases, gameplay effects, and upgrade UI
- Related architecture: [plan.md](../../../../plan.md), section 24
- Related decisions: [ADR-001: Resource System](ADR-001-resource-system.md), [ADR-002: Tower Chunk Rendering](ADR-002-tower-chunk-rendering.md)

## Context

Gathering and construction need progression rewards that designers can tune in the Unity Inspector. Each job needs independent experience, levels, upgrade points, and purchases. The client must present available upgrades, while the server decides purchases and applies their gameplay effects.

The MVP runs through FishNet services and uses in-memory player state. Its upgrade implementation must fit those boundaries without requiring a network object for every player or upgrade button.

The architecture plan describes the longer-term progression system. This record documents the implemented MVP: a spatial purchase board, levels starting at zero, and string upgrade IDs. Persistence, entitlement gates, and compact upgrade masks remain deferred.

## Decision

### Separate authored definitions, player progress, authority, and presentation

| Component | Responsibility |
| --- | --- |
| `UpgradeTreeConfig`, `UpgradeBoardData`, `UpgradeData` | Authored boards, stable IDs, grid coordinates, effect types, and values |
| `UpgradeJobProgress`, `PlayerUpgradeProgress` | Per-job progression, purchase eligibility, purchased IDs, and effect totals |
| `NetworkUpgradeService` | Server-owned player progress, request validation, targeted snapshots, and gameplay effect access |
| `UpgradeUI`, `UpgradeButton` | Instantiate boards, display progression and purchase states, and send purchase requests |

The progress classes are ordinary C# objects rather than scene components. They can be tested without starting a network session. ScriptableObjects contain shared definitions; purchasing an upgrade changes player progress rather than mutating those definitions.

### Author boards through one explicit configuration asset

`NetworkUpgradeService` references [UpgradeTreeConfig.asset](../../Resources/UpgradeTreeConfig.asset). The fixed jobs are `Gather`, `Process`, and `Build`; no generic role registry is introduced.

Each board currently contains a 7-by-7 grid and one additional capstone, for 50 upgrades per job. Each upgrade defines:

- a stable string ID, such as `gather_3_3`;
- a display name;
- zero-based row and column;
- whether it is the special `IsLevelFiftyUpgrade` node;
- one `UpgradeEffectType` and numeric `Value`.

IDs identify purchases in requests and snapshots; grid coordinates determine adjacency and layout. IDs must be unique within a job and should remain stable when names or values change. Configuration is shared content shipped to both server and client, not transmitted in progress snapshots.

### Keep progression independent per job

Each job starts at level 0 with zero experience, points, and purchases. The experience required for the next level is `(currentLevel + 1) * 10`. Reaching a threshold resets that level's experience counter and awards one upgrade point. Additional experience in the same grant continues toward subsequent levels.

Level 50 is the maximum. Normal progression awards 50 points in total per job; additional experience is ignored at the cap. Each upgrade costs one point and can be purchased once.

The center node at `[3,3]` is initially revealed. Purchasing a grid node reveals its orthogonal neighbors: up, down, left, and right. Diagonal contact does not reveal a node. The capstone is revealed by purchasing the bottom-center grid node at `[6,3]`.

Despite its `Level 50` label, the current capstone has no level-50 eligibility check. Its prerequisites are the bottom-center purchase and an available point. This is current behavior, also covered by the existing purchase tests; the label must not be interpreted as an implemented level gate.

### Own purchases and experience on the server

One `NetworkUpgradeService` keeps a `PlayerUpgradeProgress` per FishNet `ClientId`. RPCs do not require ownership of the service object; they identify the player from the sending `NetworkConnection`, not a player ID supplied by the client.

The purchase flow is:

```text
UpgradeButton selection
  -> request purchase with job and upgrade ID
  -> server validates job, ID, configuration, points, reveal path, and purchase state
  -> server records purchase and deducts one point
  -> targeted job snapshot updates the requesting client
  -> LocalProgressChanged refreshes presentation
```

The client does not optimistically deduct points or mark purchases as complete. A well-formed but ineligible purchase receives the current authoritative snapshot; malformed requests are ignored. A snapshot contains job, level, experience, available points, and purchased IDs sorted in ordinal order.

On client startup, the client requests snapshots for all three jobs. Server state is removed on disconnect and cleared when the server stops. Client state resets when the client stops. Progress does not survive reconnects or server restarts.

Normal gameplay awards experience after a validated successful action. Gathering and construction currently grant one point of job experience per completed action through `ServerGrantActionExperience`. Cancelled or rejected actions do not reach those success paths.

### Apply additive effects using shared formulas

Purchased values are summed by job and effect type. The service exposes corresponding server and client calculations; the server uses authoritative progress, and the client uses its last snapshot for presentation.

| Effect | Calculation | Authored value convention |
| --- | --- | --- |
| Efficiency | `max(0.1 seconds, baseDuration - totalEfficiency)` | Positive values reduce duration in seconds |
| Cost | `max(1, baseCost + Mathf.RoundToInt(totalCost))` | Negative values reduce resource cost |
| Production | `max(1, baseAmount + Mathf.RoundToInt(totalProduction))` | Positive values increase output |

Gathering applies Efficiency and Production. Its server timer and reward amount are captured when the gather is accepted, and resource capacity still clamps the final reward. Construction applies Efficiency and Cost, capturing duration and resource cost when the build is accepted.

The Process board and progression state exist, but no processing action currently consumes their effects or awards normal gameplay experience. Build Production is also not connected to construction: a build still advances one stage. These definitions must not be mistaken for completed gameplay integrations.

### Generate button amounts from gameplay values

`UpgradeUI` instantiates the shared [UpgradeButton prefab](../../Prefabs/Interface/UpgradeButton.prefab) from each configured board. It positions grid nodes by their coordinates and places the capstone beneath the grid. Hidden nodes are inactive; revealed nodes are buyable only when points are available; purchased nodes remain visible with the purchased visual state.

`UpgradeButton.Configure` and `SetupUpgradeData` generate the displayed amount from `EffectType` and `Value`. The first line of `DisplayName` supplies the title. Any previously authored numeric line is ignored. Efficiency negates the configured value and adds `s`; Cost and Production use the signed configured value. Formatting uses invariant culture and up to three decimal places. Capstones include the effect name on the amount line.

For example, `gather_3_3` with Efficiency value `2` displays `Efficiency` followed by `-2s`, even if its saved display name still contains `-0.2s`. This prevents duplicated numeric text from drifting away from gameplay configuration. The amount describes the individual upgrade's configured effect, not the resulting duration after all upgrades and clamps.

Labels are generated when buttons are configured. Editing the asset while existing buttons are alive does not automatically refresh those labels; live configuration reload is outside this decision.

### Integrate the menu with existing input and control ownership

The upgrade menu starts hidden and toggles through `Player/Upgrade`, bound to Tab. Inventory uses I. This updates the earlier Tab inventory binding recorded in ADR-001.

Opening the upgrade menu shows and unlocks the cursor and applies the modal input lock through `PlayerControlStateMachine`. Closing or disabling it restores the previous cursor state and releases its lock. `InterfaceManager` remains the entry point for menu toggling. Progress events refresh the selected board; changing jobs or opening the menu also refreshes it.

## Consequences

### Positive

- Designers can balance definitions without rewriting purchase or UI code.
- Server validation controls normal purchases and gameplay rewards.
- Independent job state and ordinary C# progression classes keep the core rules testable.
- Targeted snapshots avoid broadcasting private progression to all players.
- Numeric button labels use the same authored values as gameplay effects.

### Negative and tradeoffs

- Connection IDs provide session ownership, not persistent player identity.
- Full snapshots and string ID sets favor simplicity over the compact masks proposed in the architecture plan.
- Client and server must ship matching configuration; there is no content hash handshake.
- The UI duplicates adjacency rules for presentation, so changes must stay aligned with server eligibility rules.
- Configuration validation does not yet enforce unique IDs, unique coordinates, or complete boards.
- Runtime changes to definitions affect effect lookups, while accepted server actions retain their captured values and existing button labels require reconfiguration.
- Development cheat RPCs currently allow connected clients to grant XP or points, set levels, and reset progress without an administrator or build-mode gate. Server execution alone does not restrict those commands.

## Alternatives considered

- **Hardcode upgrades or author every button separately:** would couple balancing and layout to scene or UI changes. The explicit configuration and shared prefab keep definitions reusable.
- **Store numeric effects in display names:** would duplicate gameplay values and permit stale labels. Numeric text is generated from the effect data.
- **Let the client purchase locally and synchronize later:** would allow temporary divergence and require rollback. The selected flow waits for authoritative progress before displaying a purchase.
- **Use a fixed linear unlock sequence:** would not support the implemented choice of orthogonal paths from the center of the board.
- **Implement persistence and bitmasks now:** would introduce identity, storage, and content-index migration concerns beyond the current session-based MVP. Stable string IDs keep those future mappings explicit.

## Validation

[UpgradeSystemEditModeTests](../../Tests/EditMode/UpgradeSystemEditModeTests.cs) covers independent experience progression, level thresholds and the cap, point grants and resets, configured board shape, center and orthogonal reveal rules, capstone eligibility, input bindings, and instantiated prefab labels for Efficiency, Cost, Production, and capstones.

The latest implementation verification before this documentation change passed all 13 tests in that fixture. Those tests do not establish end-to-end multiplayer purchase synchronization, processing integration, or persistence. This ADR introduces no runtime changes.

## Deferred work

- Stable player identity, persistence, and reconnect restoration.
- Steam entitlement checks and the planned premium progression rules.
- A decision on whether the capstone should require level 50.
- Processing actions, Build Production, and other planned effects such as baggage, cosmetics, and social unlocks.
- Compact upgrade masks, configuration versioning, and content validation.
- Development-only or administrator authorization for cheat commands.
- Shared reveal-rule evaluation for UI and authority, and network integration tests for purchases and snapshot recovery.
- Live configuration refresh and localization of generated labels.
