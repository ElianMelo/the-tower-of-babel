# Tower of Babel — Final Technical Architecture Plan

**Status:** Architecture baseline / implementation source of truth  
**Scope:** MVP-to-launch architecture only  
**Team:** Solo developer  
**Primary target:** 1,000+ concurrent connected users per regional dedicated server  
**Client target:** 1080p / 60 FPS  
**Engine:** Unity 6.3 LTS — 6000.3.11f1, URP  
**Networking:** FishNet Networking Evolved 4.7.2R  
**Database:** SQLite  
**Distribution / Identity:** Steam, mandatory authentication  
**UI:** uGUI  
**Core packages:** NaughtyAttributes 2.1.6, Cinemachine 3.1.7, Input System 1.19.0, Multiplayer Center 1.0.1, Test Framework 1.6.0, Shader Graph 17.3.0

---

# 0. Purpose and Source Precedence

This document consolidates and supersedes:

- `idea.md`
- `ideia-answers.md`
- `plan-gepete.md`
- `plan-kimi.md`
- `plan-claudio.md`
- `TowerGenerator.cs`
- The architecture decisions made after those files were written.

When an older plan conflicts with a later explicit decision, the later decision wins.

The document is intentionally implementation-oriented. It defines system boundaries, ownership and threading rules, static and runtime data layout, binary bake format, asset identity, networking contracts, chunk and interest-management behavior, rendering strategy, persistence, progression, social behavior, testing, provisional performance budgets, project hierarchy, and implementation order.

Future post-launch systems are deliberately excluded. Any material future scope should receive a new design pass rather than being pre-engineered here.

---

# 1. Product and Runtime Model

Tower of Babel is a first-person social MMO incremental game in which a regional player population collaborates on the construction of one persistent tower.

Players are not assigned classes. Their effective role is determined by activity:

- Gatherer
- Processor
- Builder

The loop is:

```text
Gather resources
    ↓
Process resources
    ↓
Carry / deposit / withdraw resources
    ↓
Build predefined tower assets
    ↓
Gain role-specific XP
    ↓
Unlock efficiency + social + cosmetic progression
    ↓
Repeat
```

The tower is structurally predictable and authored before the live world starts. Players do not freely place architecture.

The five major tower asset categories are:

```text
Floor Tile
Pillar
Arch
Stair
Furniture
```

Furniture is special:

- structural geometry is procedurally created in the authoring scene;
- furniture is hand placed;
- both are scanned by the same final bake process;
- furniture is player-buildable;
- furniture becomes available only after the structural portion of the floor is complete.

---

# 2. Non-Negotiable Architecture Invariants

These rules should be treated as architecture tests, not suggestions.

## 2.1 Static tower topology never crosses the network

The following data is baked and identical on client and server:

```text
AssetId
AssetType
FloorIndex
ChunkX
ChunkZ
Position
Rotation
Build resource definition
Stage mesh mapping
Static flags
Furniture / structural category
```

The network must never resend position, rotation, chunk ownership, or asset type for predefined tower assets.

Runtime replication sends only mutable state.

Example:

```text
AssetId
Stage
```

not:

```text
AssetId
Position
Rotation
AssetType
Chunk
Stage
```

## 2.2 Static tower topology is not duplicated in SQLite

SQLite persists world progress, not the baked world definition.

The static binary is part of the world identity.

## 2.3 Server validates persistent gameplay outcomes

Movement and interaction presentation are client-authoritative to avoid noticeable latency.

The server performs plausibility / exploit checks and owns acceptance of persistent state changes.

Small movement discrepancies are ignored. Only extreme violations cause reconciliation.

## 2.4 No NetworkTransform or NetworkAnimator

Frequent player replication is implemented with custom compact state packets.

## 2.5 Maximum watched player count is 100

A client never observes more than 100 remote players.

Friends consume the same 100-player budget and have first priority.

## 2.6 Runtime gameplay state is data, not world GameObjects

Server simulation workers operate on structs, arrays, IDs, timers, and queues.

They do not manipulate GameObjects, Transforms, Unity Physics, or rendering components.

## 2.7 Current and adjacent chunks are high-detail client views

High-detail view is a 3×3×3 neighborhood centered on the player's current chunk:

```text
X: current - 1 ... current + 1
Floor: current - 1 ... current + 1
Z: current - 1 ... current + 1
```

Far chunks are rendered through a GPU-oriented abstraction.

## 2.8 Database persistence never freezes simulation

Snapshots are copied and written asynchronously.

A controlled server shutdown always attempts a final save.

---

# 3. Deployment Architecture

## 3.1 Regional worlds

Launch regions:

```text
US
SA
EU
```

Each region has one dedicated server and one independent world.

Each regional server owns its own tower progress, player progression, player resources, player names, global totem, construction state, and SQLite database.

There is no cross-region progression synchronization.

A Steam account can therefore have different progression in different regions.

## 3.2 Build targets

The codebase is shared, but builds are separate:

```text
TowerOfBabel.Client
TowerOfBabel.DedicatedServer
```

The dedicated server build excludes client-only presentation code.

The client build excludes dedicated-server-only implementation where practical through assembly/platform constraints.

## 3.3 Server discovery

The client presents a server list.

Each entry exposes:

```text
Region display name
Region flag
Ping
Online / unavailable state
Capacity status
```

No login queue is required for MVP.

When the configured connection capacity is reached:

```text
Connection rejected
Reason: Server Full
```

## 3.4 RegionConfig.json

Region-specific runtime tuning belongs in a server configuration file.

Recommended shape:

```json
{
  "RegionId": "SA",
  "DisplayName": "South America",
  "ListenPort": 7777,
  "MaxPlayers": 1000,
  "SimulationTickHz": 10,
  "PlayerReplicationHz": 10,
  "ChunkSizeMeters": 32,
  "ObserverBudget": 100,
  "ObserverReevaluationMinutes": 30,
  "ObserverAnchorDistanceChunks": 10,
  "NearChunkReplicationSeconds": 0.5,
  "FarChunkReplicationSeconds": 60.0,
  "SaveIntervalMinutes": 30,
  "DatabasePath": "./Data/tower-sa.db",
  "TowerBinaryPath": "./Data/TowerGenerationConfig.bin"
}
```

Values shown above are defaults/proposals where not explicitly fixed.

---

# 4. Authentication, Identity and Entitlement

## 4.1 Steam authentication

Steam authentication is mandatory.

No anonymous account mode is required.

At connection:

```text
Client
  ↓ Steam authentication ticket
Dedicated Server
  ↓ validates identity
Session created
```

The permanent external account identifier is SteamID64.

An internal compact runtime `PlayerId` / `ConnectionId` should be used for frequent network traffic rather than repeatedly transmitting SteamID64.

## 4.2 In-game name

The player may choose an in-game name different from the Steam display name.

Persist:

```text
SteamId
InGameName
```

## 4.3 Premium DLC

The Steam DLC grants access to premium progression beyond level 20.

DLC entitlement is validated on every login.

SQLite is not the source of authority for DLC ownership.

A cached field may exist for diagnostics, but gameplay access is based on the current Steam entitlement check.

---

# 5. Tower Authoring and Bake Pipeline

`TowerGenerator.cs` is the authoring basis for predictable geometry.

Its existing behavior includes shrinking radius across floors, radial pillars, radial arches, concentric floor tiles, four stair flights per floor, each stair flight rotating 90 degrees while ascending, and floor tiles removed where the stairs need openings.

The current in-scene authoring operations are valid and useful.

The production pipeline is therefore not runtime procedural generation. It is:

```text
Authoring Configuration
       ↓
TowerGenerator generates GameObjects in editor scene
       ↓
Developer visually tests / edits scene
       ↓
Hand-place furniture
       ↓
Adjust / delete / create authoring objects as required
       ↓
Final Bake Tool scans scene chunk by chunk
       ↓
Assign deterministic final IDs
       ↓
Write TowerGenerationConfig.bin
       ↓
Same binary shipped to server and client
```

## 5.1 Why scene operations remain part of authoring

The generator is allowed to instantiate GameObjects, raycast, delete floor objects, use authoring colliders, use NaughtyAttributes buttons, and expose live editor tuning.

Those operations are editor-time content generation.

They are not server simulation logic.

The final bake output is the stable contract.

## 5.2 Generator immutability after production

Once the production tower is finalized:

```text
Generator output is immutable for that world.
```

A production `TowerGenerationConfig.bin` belongs to a specific regional world database.

Changing the baked topology is considered creation of a new world/version, not a normal save migration.

Development builds may regenerate freely before world lock.

---

# 6. TowerGenerationConfig.bin

## 6.1 Format

One monolithic custom binary contains the entire static tower definition.

Both client and dedicated server load the entire definition into RAM at startup.

Meshes, textures, materials, and shaders remain Unity content assets; raw mesh payloads are not embedded inside the tower binary.

The binary references compact content identifiers.

## 6.2 Recommended header

```text
Magic
FormatVersion
GeneratorVersion
ContentHash
ChunkSizeMeters
FloorHeight
FloorCount
ChunkCount
AssetCount
ResourceDefinitionHash
RecipeDefinitionHash
OffsetToChunkTable
OffsetToAssetTable
```

Exact packing can be finalized with the serializer implementation.

## 6.3 Client/server compatibility

During authentication the client sends the tower content hash.

Server compares:

```text
Client Tower Hash
Server Tower Hash
```

Mismatch:

```text
Reject connection
Reason: Content Version Mismatch
```

This check occurs before player spawn.

## 6.4 Chunk table

Each static chunk descriptor should contain enough information to locate its contiguous asset range:

```csharp
struct StaticChunkRecord
{
    int FloorIndex;
    int ChunkX;
    int ChunkZ;

    uint FirstAssetIndex;
    uint AssetCount;

    uint FirstStructuralAssetIndex;
    uint StructuralAssetCount;

    uint FirstFurnitureAssetIndex;
    uint FurnitureAssetCount;
}
```

This makes chunk scans allocation-free and avoids dictionaries for the hot path.

## 6.5 Static asset record

Conceptual representation:

```csharp
struct StaticTowerAsset
{
    ulong AssetId;

    ushort AssetTypeId;
    ushort BuildResourceId;

    int FloorIndex;
    int ChunkX;
    int ChunkZ;

    Vector3 Position;
    Quaternion Rotation;

    ushort CostPerStage;
    byte CategoryFlags;
}
```

The actual binary serializer may quantize fields later if profiling justifies it.

Static data is loaded once and never mutated.

---

# 7. Deterministic Asset Identity

Asset IDs are 64-bit explicit bit fields.

IDs are assigned during the final scene bake, scanning chunk by chunk.

Logical identity is based on:

```text
FloorIndex
ChunkX
ChunkZ
AssetType
LocalIndex
```

The ID is not a hash. It is reversible.

## 7.1 Proposed 64-bit allocation

Because no final maximum radius/floor count has been defined, use a conservative initial layout:

```text
14 bits  FloorIndex
14 bits  ChunkX (biased signed coordinate)
14 bits  ChunkZ (biased signed coordinate)
4 bits   AssetType
18 bits  LocalIndex
--------------------------------
64 bits total
```

Capacity:

```text
Floors:       0 .. 16,383
ChunkX:      -8,192 .. +8,191
ChunkZ:      -8,192 .. +8,191
Asset types:  0 .. 15
LocalIndex:   0 .. 262,143
```

These limits are intentionally far above the expected MVP.

If the project ever exceeds these limits, that is a binary format change and requires a new world format version.

## 7.2 Chunk identity

Vertical identity is the floor index.

There is no need to encode world Y as chunk ownership data.

Conceptual chunk key:

```csharp
struct ChunkKey
{
    int FloorIndex;
    int X;
    int Z;
}
```

Runtime hashing derives a stable worker owner from this key.

---

# 8. Chunk Model

## 8.1 Spatial partition

Chunks are a regular X/Z world grid.

Default:

```text
32m × 32m × one floor
```

Chunk size is configurable before a tower bake.

The chunk system no longer uses angular sectors.

Asset chunk membership is derived from final baked world position.

Example:

```text
ChunkX = Floor(Position.x / ChunkSize)
ChunkZ = Floor(Position.z / ChunkSize)
Floor  = baked floor index
```

The exact handling of negative coordinates must use one shared deterministic floor-division implementation in editor, server, client, and tests.

## 8.2 High-detail neighborhood

Client high-detail world:

```text
3 × 3 × 3 chunks
```

That is current floor + floor above + floor below, and current X/Z chunk + 8 horizontal neighbors.

Maximum geometric high-detail chunk slots:

```text
27
```

Chunks that do not exist are skipped.

## 8.3 Chunk construction state

Mutable construction state is compact.

For a chunk with `N` buildable assets:

```csharp
byte[] StageByAssetIndex;
```

Each value:

```text
0 .. 10
```

No per-asset runtime class is required.

A byte is used initially for implementation simplicity.

A future nibble-packed representation is possible because 4 bits can represent 0–15, but should only be introduced if memory/persistence profiling shows value.

## 8.4 Chunk completion states

Use explicit state:

```csharp
enum ChunkConstructionState : byte
{
    BuildingStructural,
    BuildingFurniture,
    Complete
}
```

### Structural completion

Required assets:

```text
Floor
Pillar
Arch
Stair
```

When all structural assets reach stage 10:

```text
StructuralComplete
```

Effects:

- furniture becomes available;
- floor internal tools/stations become available;
- construction is not fully frozen yet.

### Full completion

When structural + furniture assets are all stage 10:

```text
Complete
```

Effects:

- construction interactions in the chunk are rejected;
- movement remains active;
- chat remains active;
- emotes remain active;
- social/interactable props may remain active;
- geometry is eligible for aggressive finished GPU batching.

---

# 9. Threading and Ownership

## 9.1 Main Unity thread responsibilities

The main thread owns all Unity/FishNet-facing and global player/session state:

```text
FishNet transport / callbacks
Authentication
Connection lifecycle
Player session records
Player resources / baggage
Player progression
Player name
Steam entitlement
Global totem
Observer set management
Outbound network dispatch
Inbound request routing
Persistence snapshot coordination
```

This follows the explicit decision to keep player mutable state and the global totem on the main thread instead of creating additional economy/player ownership threads.

## 9.2 Chunk worker responsibilities

Chunk workers modify chunk-owned data only.

Examples:

```text
Construction stage arrays
Chunk completion state
Resource node availability timers
Processing station runtime job state
Dirty flags
Chunk-local timers
```

Workers never directly access Unity GameObjects.

Workers never directly call FishNet APIs.

Workers never directly modify player baggage/progression.

## 9.3 Worker count

Automatic default:

```text
max(1, Environment.ProcessorCount - 1)
```

The Unity/main/network thread remains reserved.

## 9.4 Chunk-to-worker mapping

Ownership is deterministic hash partitioning.

Conceptually:

```text
worker = StableHash(FloorIndex, ChunkX, ChunkZ) % WorkerCount
```

Chunk ownership does not change merely because a player moves.

A player entering a new chunk simply changes which chunk data is addressed by interaction validation.

## 9.5 Command flow

Example build completion:

```text
Client completes local wait interaction
        ↓
BuildRequest reaches FishNet/main thread
        ↓
Main thread validates:
    session
    interaction distance
    current chunk
    baggage availability
    rate / exploit rules
        ↓
Command queued to owning ChunkWorker
        ↓
ChunkWorker serially checks:
    asset exists
    asset belongs to current chunk
    chunk not construction-frozen
    actual remaining stages
        ↓
ChunkWorker applies actual stage advance
        ↓
Result queued back to main thread
        ↓
Main thread deducts resource for actual applied stages
Main thread grants actual Builder XP
        ↓
Chunk dirty
Player dirty
        ↓
Replication scheduled
```

The worker determines the actual accepted stage count before resources are deducted.

This prevents resource loss when multiple players complete interactions against the same nearly-finished object.

---

# 10. Simulation Tick

Authoritative server simulation frequency:

```text
10 Hz
```

Tick interval:

```text
100 ms
```

Player network-state replication:

```text
10 Hz
```

Client render/interpolation:

```text
60 FPS target
```

Not every system must run work every tick.

Examples:

```text
Player validation             10 Hz / packet-driven
Construction commands         event-driven
Observer rebuild              minutes / trigger-driven
Near chunk replication        <= 1 second
Far chunk replication         ~60 seconds
Persistence                   20–60 minutes configurable
```

---

# 11. Player Movement

## 11.1 Controller

Client:

```text
Unity CharacterController
First person
WASD
Mouse look
Jump
Interact
```

## 11.2 Authority model

Movement presentation is client-authoritative.

The local player immediately moves without waiting for a server round trip.

Client transmits its state at 10 Hz.

Server checks for exploit-scale violations.

The server does not attempt full authoritative CharacterController reproduction.

## 11.3 Validation

Recommended server plausibility checks:

```text
Maximum displacement over time
Maximum horizontal speed
Maximum vertical speed
Impossible teleport distance
Out-of-world bounds
Invalid floor/chunk transitions
Gross penetration / impossible coordinates where cheaply testable
Malformed quaternion/state
```

Small disagreement is ignored.

Extreme violation:

```text
Server sends correction
Client reconciles
```

The objective is exploit resistance without turning a social MMO into a latency-sensitive server-movement simulation.

## 11.4 PlayerState

Velocity is not transmitted.

Clients calculate remote velocity from samples.

Semantically:

```csharp
struct PlayerState
{
    PlayerNetId Id;
    Vector3 Position;
    Quaternion Rotation;
    byte AnimationState;
    byte MovementFlags;
}
```

The wire representation should be compact and may use custom quaternion compression.

"Full quaternion" means the complete orientation semantics are retained; it does not require four 32-bit floats on the wire.

---

# 12. Proposed PlayerState Wire Format

Provisional compact layout:

```text
PlayerNetId             uint32      4 bytes
WorldPositionX          float32     4 bytes
WorldPositionY          float32     4 bytes
WorldPositionZ          float32     4 bytes
QuaternionCompressed    ~7 bytes
AnimationState          uint8       1 byte
MovementFlags           uint8       1 byte
--------------------------------------------
Approx payload          25 bytes/state
```

A "smallest-three" normalized quaternion encoding is recommended.

Do not quantize world position until profiling demonstrates that 12-byte world coordinates are a real bottleneck.

Correctness and predictable tower-height behavior are more important initially.

---

# 13. Player Interest Management

The observer system is one of the primary scalability systems.

## 13.1 Hard budget

Per client:

```text
100 watched remote players maximum
```

Never exceed 100.

## 13.2 Priority order

Build candidate set in this order:

```text
1. Connected Steam friends
2. Players in current chunk
3. Players in adjacent chunks
4. Players in progressively farther chunks
```

If 100 Steam friends are online:

```text
100 friend slots
0 locality slots
```

If 99 are online:

```text
99 friend slots
1 locality slot
```

If 1 is online:

```text
1 friend slot
99 locality slots
```

## 13.3 Stable observer sets

Consistency is preferred over continuously choosing the mathematically nearest 100.

Default:

```text
ObserverReevaluationMinutes = 30
```

The same observer set is retained where possible.

A slot opens immediately when a watched player disconnects, becomes invalid, or leaves the relevant distance envelope.

Vacant slots are filled from current/farther chunk candidates.

## 13.4 Anchor-based reevaluation

At observer-build time:

```text
ObserverAnchorChunk = current player ChunkKey
```

Immediate reevaluation occurs when:

```text
Manhattan/GridDistance(CurrentChunk, ObserverAnchorChunk) >= 10
```

The threshold is configurable.

A large distance change by a watched non-friend also makes that member eligible for eviction/replacement.

Friends remain highest priority and may be immediately reselected even when geographically distant.

## 13.5 Crowded chunk selection

When more local candidates exist than free slots, compare Chebyshev chunk distance only
and retain stable hysteresis. Do not use `Vector3.Distance` for player selection. Players
at the same chunk distance are randomized; in particular, selection among players in the
same chunk must not depend on their physical positions.

Do not reshuffle the set every network tick.

A candidate should replace an existing non-friend only if a slot is open, an observer rebuild is triggered, or the current member is outside the configured validity envelope.

## 13.6 Watched-player frequency

All 100 watched players:

```text
10 Hz
```

No distance-based player update tiers for MVP.

## 13.7 FishNet joining-client roster handshake

Do not send the initial player roster with a `TargetRpc` directly from
`ServerManager.OnRemoteConnectionState` / the remote-connection `Started` callback.
At that point FishNet may not yet have added the joining connection as an observer of
the scene `NetworkObject`; FishNet validates `TargetRpc` targets and rejects the send
when the target is not already an observer. This creates an asymmetric failure where
existing clients see the new player, but the new client never learns about existing
players.

Required sequence:

```text
Server registers the connection
    -> existing observers receive the player-joined notification
Joining client reaches NetworkBehaviour.OnStartClient
    -> client registers its local PlayerInstance
    -> client sends a ServerRpc requesting the roster
Server replies with a TargetRpc now that the client observes the NetworkObject
Client indexes the complete roster by chunk
    -> one deferred priority search runs on the next Update
    -> client submits its watched-player IDs to the server
```

Roster entries must be indexed as a batch before searching. Do not run a chunk search
once per roster entry. Request one deferred search so every roster entry is available,
then preserve the normal 2-5 second vacancy-search cadence afterward.

---

# 14. Social Replication

Social events are observable only for watched players.

A player outside the observer budget is effectively invisible to that client:

```text
no avatar
no animation events
no chat bubble
no emote
```

This prevents a separate social-message path from bypassing interest management.

## 14.1 Local chat

Chat is:

```text
proximity/local
ephemeral
not persisted
128 characters maximum
minimum 3 seconds between messages
```

Server-side local filtering is required so a modified client cannot bypass moderation.

No external moderation API is required for MVP.

Filter categories include profanity, off-platform contact solicitation patterns, and basic spam/repeated-message patterns.

## 14.2 Friends

Friend priority comes from the Steam Friend List.

There is no custom `Friendships` SQLite table.

No MVP guilds, parties, mail, direct trading, or private messages.

---

# 15. Remote Avatar Presentation

Remote avatars are driven by the custom `PlayerState` stream.

Pipeline:

```text
10 Hz states
   ↓
snapshot buffer
   ↓
interpolation
   ↓
60 FPS Transform presentation
   ↓
Animator state mapping
```

No `NetworkAnimator`.

Humanoid Mecanim is used.

Typical compact animation state values:

```text
Idle
Walk
Run
Jump
Gather
Process
Build
Emote
Dance
```

Velocity for animation blending is derived locally from state deltas.

Far remote players are not client-side physics simulation entities.

They are presentation objects following replicated position/rotation.

---

# 16. Rendering Architecture

## 16.1 Rendering tiers

### High-detail / near

3×3×3 chunk neighborhood.

Uses a mixture of:

```text
GameObjects
asset colliders
Graphics.RenderMesh
normal URP materials
interaction highlighting
```

GameObjects exist where needed for collision, interaction, stage visual, and authoring-compatible prefab behavior.

### Far

Uses:

```text
IFarChunkRenderer
```

Exact GPU backend is intentionally abstracted.

Candidate implementations:

```text
Graphics.RenderMeshInstanced
RenderMeshIndirect
other URP-compatible batched renderer
```

The choice is made by profiling.

## 16.2 IFarChunkRenderer

Conceptual contract:

```csharp
public interface IFarChunkRenderer
{
    void LoadChunk(in FarChunkSnapshot snapshot);
    void ApplyChunkStageSnapshot(in FarChunkStageSnapshot snapshot);
    void RemoveChunk(ChunkKey key);
    void SetVisible(bool visible);
}
```

No gameplay logic depends on the concrete rendering backend.

## 16.3 Construction stage meshes

Each buildable has:

```text
Stage 0
Stage 1
...
Stage 10
```

Stages 1–10 may use completely different meshes.

The general footprint remains predictable.

The game is low-poly by default, so no additional conventional distance LOD requirement is imposed initially.

`Stage mesh` and `distance LOD` are different concepts; MVP relies primarily on the stage mesh plus near/far rendering tiers.

## 16.4 Stage 0

Stage 0 has no built geometry.

When the player points at / targets the predefined build location, render a shader/highlight around the final intended shape.

The interaction proxy and static tower definition allow the player to select the build location even though the stage-0 mesh is absent.

## 16.5 Collision

High-detail chunks use the collision associated with the active asset/stage prefab.

The 3×3×3 promotion window is intended to load collision before the player reaches a chunk boundary.

Far chunks have no gameplay physics/collision representation.

## 16.6 Completed chunk rendering

Fully completed chunks are eligible for aggressive GPU grouping/batching.

Individual static objects no longer need to remain individual near-style render objects while far.

---

# 17. Dynamic Chunk Replication

Static geometry is local content.

Network chunk traffic contains dynamic state only.

## 17.1 Initial near synchronization

When a chunk enters the 3×3×3 high-detail neighborhood:

```text
Client already knows static assets
       ↓
Client requests / server schedules dynamic chunk snapshot
       ↓
Packed stage array
Runtime resource-node state if applicable
Relevant station state
Chunk completion flags
       ↓
Client instantiates / updates high-detail representation
```

## 17.2 Near update interval

Target default:

```text
0.5 seconds
```

Requirement:

```text
1 second or lower
```

Dirty changes are grouped rather than sending one message per construction action when practical.

## 17.3 Far update interval

Target default:

```text
60 seconds
```

Far chunks use low-frequency packed state refresh.

Finished/frozen chunks can be treated specially because their all-stage-10 state is immutable.

## 17.4 Promotion correctness

When a far chunk becomes near, the server sends a current packed snapshot regardless of the last far update time.

This prevents the client from entering a high-detail chunk with stale construction data.

---

# 18. Resource Nodes

Resource nodes are:

```text
outside the tower
ground level
shared
regenerating
server-managed
not persisted
```

Only nearby clients receive their runtime state.

On server restart, node cooldown state may reset.

Gather interaction:

```text
Client starts local wait
Movement locked
Player may cancel by attempting to move
       ↓
If cancelled:
    no reward
       ↓
If completed:
    request sent
    server validates
    reward granted
    Gather XP granted
    node enters cooldown
```

---

# 19. Resource Model

Resources are generic and data-driven.

Do not hardcode Stone, Sand, Ink, Glass, Paint, or ProcessedStone into player-state classes or database columns.

These are content examples, not schema.

## 19.1 ResourceDefinition

Authoring content:

```csharp
ResourceDefinition
{
    ushort ResourceId;
    string Name;
    Sprite Icon;
}
```

The numeric `ResourceId` is baked/stable content shared by client/server.

Packets and persistence use the ID.

## 19.2 Player resources

Runtime conceptual model:

```text
ResourceId -> Amount
```

No item instances.

No inventory slots.

No drag and drop.

## 19.3 Baggage capacity

Each resource has an independent carry maximum.

However the maximum value is identical for every resource for a given player.

Do not store a separate max per resource.

Calculate:

```text
EffectiveCapacityPerResource =
    BaseCapacity
    + progression baggage bonus
```

An upgrade increases capacity by the same amount for every resource.

Validation:

```text
0 <= Amount(ResourceId) <= EffectiveCapacityPerResource
```

---

# 20. Processing System

Processing is recipe-driven.

No resource conversion is hardcoded in gameplay services.

## 20.1 RecipeDefinition

Conceptual:

```csharp
struct RecipeDefinition
{
    ushort RecipeId;
    ushort InputResourceId;
    int InputAmount;
    ushort OutputResourceId;
    int OutputAmount;
    float DurationSeconds;
}
```

A future recipe can be extended to multiple inputs/outputs if the design requires it, but MVP should use the simplest representation matching actual content.

## 20.2 Station behavior

Stations are shared.

Multiple players can process at the same station simultaneously.

Each player has an independent processing interaction/timer.

There is no exclusive station lock.

## 20.3 Interaction lifecycle

```text
Start interaction
    ↓
Server/client validates recipe availability
    ↓
Movement locked
    ↓
Wait
    ↓
Move input may cancel
```

Cancellation:

```text
No resources consumed
No output granted
No XP granted
```

Successful completion:

```text
Server validates input still exists
Input consumed
Output added within baggage capacity
Processor XP granted
```

Resource mutation is atomic on the main thread.

---

# 21. Global Totem

There is one global resource totem/bank for the regional world.

Players may deposit and withdraw.

No direct player-to-player transfer exists.

## 21.1 Baggage restriction

Withdrawal is limited by the player's per-resource baggage capacity.

This prevents using the totem as a direct bypass of carry limits.

## 21.2 Ownership

Global totem data is owned on the main thread.

No chunk worker modifies it directly.

## 21.3 Persistence

Persist generic non-zero resource amounts:

```sql
CREATE TABLE GlobalTotemResource (
    ResourceId INTEGER PRIMARY KEY,
    Amount INTEGER NOT NULL CHECK (Amount > 0)
);
```

Zero rows are deleted / omitted.

---

# 22. Construction System

## 22.1 Predefined assets

Players build only baked assets.

No free placement.

Every target is identified by `AssetId`.

## 22.2 Build requirements

Each static buildable definition contains:

```text
BuildResourceId
CostPerStage
```

Each successful action requests an advance of one or more stages.

Upgrade effects may increase stages per completed build action.

## 22.3 No percentage

Construction progression is discrete:

```text
0
1
2
...
10
```

There is no percentage value in the authoritative state.

## 22.4 Multi-stage action

Example:

```text
Upgrade allows +3 stages
Current stage = 7
```

Successful action:

```text
7 -> 10
```

Actual applied stages:

```text
3
```

Resource cost:

```text
3 × CostPerStage
```

If current stage = 9:

```text
9 -> 10
Actual applied stages = 1
Resource cost = 1 × CostPerStage
XP = actual work only
```

## 22.5 Resource source

Construction can consume only from player baggage.

To use globally shared resources:

```text
Totem
  ↓ withdraw
Baggage
  ↓
Build
```

Construction never directly debits the global totem.

## 22.6 Interaction lifecycle

```text
Player targets build asset
       ↓
Start local wait
       ↓
Movement locked
       ↓
Move input cancels
       ↓
On successful wait completion:
    BuildRequest
       ↓
Server validates
       ↓
Chunk worker resolves actual available stages
       ↓
Main thread deducts actual resource cost
       ↓
Builder XP granted
       ↓
Chunk stage data changed
       ↓
Replication scheduled
```

There is no need to client-predict persistent stage/resource outcomes.

Local interaction animation/UI begins immediately.

---

# 23. Furniture and Floor Unlocks

Furniture is a separate build category.

## 23.1 Structural phase

Build:

```text
Floor Tiles
Pillars
Arches
Stairs
```

When all required structural assets for the floor are complete:

```text
Floor.StructuralComplete = true
```

Unlock:

```text
Furniture build sites
Interior processing/tools
```

## 23.2 Furniture phase

Players build hand-authored furniture sites using the same discrete 0–10 stage model.

## 23.3 Final construction phase

When structural + required furniture state is complete:

```text
Floor / relevant chunks become construction-complete
```

Construction requests against completed build sites are rejected.

---

# 24. Progression Architecture

Roles are fixed:

```text
Gatherer
Processor
Builder
```

Do not build a generic role registry.

## 24.1 Experience

Server grants XP only after validated completed activity.

```text
Gather -> Gather XP
Process -> Processor XP
Build -> Builder XP
```

## 24.2 Levels

Each role:

```text
Level 1 .. 50
```

Free entitlement:

```text
1 .. 20
```

Premium Steam DLC:

```text
21 .. 50
```

After level 20 progression becomes significantly slower while remaining linear in overall design.

## 24.3 Upgrade kits

Each level has one upgrade kit.

Therefore the effective tree is:

```text
50 kits per role
```

A kit can contain active efficiency effect, passive/social unlock, and cosmetic unlock.

Premium levels use the same system; reward values/content may be adjusted.

## 24.4 Upgrade types

Examples:

```text
Gather/Process/Build speed
Yield
Build stages per action
Resource cost reduction
Baggage capacity
Visual effects
Emotes
Dances
Cosmetics
Social presentation effects
```

Exact values are a separate game-design balancing pass.

## 24.5 Bitset representation

Each role needs at most 50 upgrade flags.

Use one `ulong` per role:

```csharp
struct PlayerUpgradeMasks
{
    ulong GatherMask;
    ulong ProcessorMask;
    ulong BuilderMask;
}
```

Only the lower 50 bits are currently used.

---

# 25. Cosmetics

Cosmetic UX remains intentionally simple.

```text
Open list
Choose unlocked option
Click
Appearance swaps
```

No inventory slots.

No drag and drop.

No complex equipment system.

Static cosmetic definitions live in client/server content.

Persist compact selected cosmetic IDs / unlock masks.

---

# 26. Persistence Architecture

SQLite is the final production database.

The workload is intentionally small and batched.

## 26.1 SQLite configuration

Recommended baseline:

```text
WAL journal mode
prepared statements
single dedicated DB writer
batched transaction per save cycle
foreign keys enabled where useful
synchronous=NORMAL initially, benchmark before release
```

## 26.2 Save interval

Configurable.

Expected production range:

```text
20–60 minutes
```

Default proposal:

```text
30 minutes
```

The project accepts losing up to one save interval on an ungraceful crash.

## 26.3 Graceful shutdown

Controlled shutdown:

```text
Stop accepting connections
Capture final snapshot
Flush database
Close SQLite
Exit
```

## 26.4 Async snapshot strategy

Simulation never waits for SQLite writes.

At save time:

```text
Main thread / chunk workers expose immutable snapshot copies
        ↓
Persistence subsystem receives snapshot batch
        ↓
DB writer serially executes transaction
        ↓
Success acknowledgement
```

Avoid passing live mutable collections to the DB thread.

## 26.5 Disconnected player retention

On disconnect:

```text
character disappears immediately
session becomes disconnected
latest player data remains in RAM
```

After that player has been included in a successful save:

```text
if still disconnected:
    evict player state from RAM
```

Reconnect before save restores latest in-memory state, not the older SQLite state.

---

# 27. Proposed SQLite Schema

Schema names are illustrative but represent the intended normalization.

## 27.1 Players

```sql
CREATE TABLE Player (
    PlayerId INTEGER PRIMARY KEY,
    SteamId TEXT NOT NULL UNIQUE,
    InGameName TEXT NOT NULL,

    GatherLevel INTEGER NOT NULL,
    GatherXP INTEGER NOT NULL,
    GatherUpgradeMask INTEGER NOT NULL,

    ProcessorLevel INTEGER NOT NULL,
    ProcessorXP INTEGER NOT NULL,
    ProcessorUpgradeMask INTEGER NOT NULL,

    BuilderLevel INTEGER NOT NULL,
    BuilderXP INTEGER NOT NULL,
    BuilderUpgradeMask INTEGER NOT NULL,

    SelectedCosmeticBlob BLOB NOT NULL,
    CosmeticUnlockBlob BLOB NOT NULL,

    LastSavedUtc INTEGER NOT NULL
);
```

No custom friend list is persisted.

Steam provides friend priority.

## 27.2 PlayerResource

```sql
CREATE TABLE PlayerResource (
    PlayerId INTEGER NOT NULL,
    ResourceId INTEGER NOT NULL,
    Amount INTEGER NOT NULL CHECK (Amount > 0),

    PRIMARY KEY (PlayerId, ResourceId),
    FOREIGN KEY (PlayerId) REFERENCES Player(PlayerId)
);
```

Zero-value rows are omitted.

## 27.3 GlobalTotemResource

```sql
CREATE TABLE GlobalTotemResource (
    ResourceId INTEGER PRIMARY KEY,
    Amount INTEGER NOT NULL CHECK (Amount > 0)
);
```

## 27.4 ChunkState

One row per dirty/persisted chunk.

Do not create one SQL row per asset.

```sql
CREATE TABLE ChunkState (
    FloorIndex INTEGER NOT NULL,
    ChunkX INTEGER NOT NULL,
    ChunkZ INTEGER NOT NULL,

    State INTEGER NOT NULL,
    StageBlob BLOB NULL,

    LastSavedUtc INTEGER NOT NULL,

    PRIMARY KEY (FloorIndex, ChunkX, ChunkZ)
);
```

`StageBlob` contains the compact stage values in baked asset order for that chunk.

## 27.5 Completed chunk optimization

If:

```text
ChunkState == Complete
```

all construction stages are implicitly 10.

Then:

```text
StageBlob = NULL
```

is sufficient.

This reduces database size for the continually completed tower.

## 27.6 Resource nodes

No resource-node persistence table.

## 27.7 Schema migration

Normal save-schema migration should have explicit integer schema versions.

Tower topology migration is not supported once a production world is locked.

---

# 28. Chunk Runtime Persistence Format

The chunk's static asset order comes from `TowerGenerationConfig.bin`.

Therefore the BLOB does not need AssetIds.

Example:

```text
Static chunk asset order:
0 Pillar ...
1 Arch ...
2 Tile ...
3 Stair ...
...

Runtime StageBlob:
[10, 10, 4, 2, ...]
```

The static content hash guarantees both sides interpret index `N` identically.

This is significantly smaller than persisting `AssetId + Stage` for every object.

---

# 29. Resource / Progression Atomicity

Because player mutable state is main-thread owned, baggage, XP, upgrade state, and totem are mutated only on the main thread.

Chunk workers return accepted world changes.

The main thread commits player-side consequences based on the accepted result.

For interactions where player payment and chunk mutation must appear atomic:

1. reserve/validate player resources on main thread;
2. send command to owning worker;
3. worker returns accepted quantity;
4. main thread commits only accepted quantity;
5. reserved remainder is released.

The implementation can use lightweight request tokens rather than blocking.

---

# 30. Network Message Families

FishNet remains responsible for transport, connections, serialization hooks, authentication flow, and message/RPC delivery.

The game defines compact message families.

## 30.1 Client -> Server

```text
PlayerStateMessage
GatherCompleteRequest
ProcessCompleteRequest
BuildCompleteRequest
TotemDepositRequest
TotemWithdrawRequest
ChatRequest
EmoteRequest
CosmeticSelectRequest
UpgradeClaimRequest
ChunkDetailRequest (if explicit request path is used)
```

## 30.2 Server -> Client

```text
PlayerStateBatch
MovementCorrection
ObservedPlayerEnter
ObservedPlayerLeave
ChunkDynamicSnapshot
ChunkDynamicDelta
ResourceNodeSnapshot
InteractionResult
TotemStateUpdate
ChatBubbleEvent
EmoteEvent
ProgressionUpdate
CosmeticUpdate
```

## 30.3 Reliability

Recommended:

### Unreliable / sequenced

```text
PlayerStateBatch
high-frequency replaceable movement state
```

### Reliable

```text
interaction outcomes
construction state changes
resource changes
progression
chat/emote event if presentation must not silently vanish
observer enter/leave
chunk snapshots
```

Benchmark FishNet transport behavior before final channel tuning.

---

# 31. PlayerState Bandwidth Strategy

Do not send one FishNet object RPC for each visible player if a batch message can represent the observer set more efficiently.

Recommended per observer:

```text
PlayerStateBatch
{
    Sequence
    Count
    State[Count]
}
```

Maximum:

```text
Count <= 100
```

A server implementation may cache serialized states per tick and compose observer batches without reserializing identical source state fields repeatedly.

Do not assume a single chunk multicast solves all traffic because different observers can have different stable 100-player sets.

Optimization should focus on compact state, batched packet construction, observer stability, reused serialization buffers, and zero/low allocation.

---

# 32. Chunk Replication Data

Near chunk snapshot concept:

```csharp
struct ChunkDynamicHeader
{
    ChunkKey Key;
    ChunkConstructionState State;
    ushort StageCount;
}
```

Followed by:

```text
packed stage array
resource-node dynamic flags/timers if relevant
station runtime display state if relevant
```

Static asset transforms are resolved locally by the chunk key and baked asset index.

---

# 33. Project / Assembly Structure

The project uses feature-level assemblies.

Recommended root:

```text
Assets/
└── _Project/
    ├── Foundation/
    ├── Networking/
    ├── Authentication/
    ├── Tower/
    ├── Chunks/
    ├── Construction/
    ├── Resources/
    ├── Processing/
    ├── Progression/
    ├── Player/
    ├── Social/
    ├── Persistence/
    ├── Rendering/
    ├── UI/
    ├── Generation/
    └── Tests/
```

## 33.1 Foundation

Assembly:

```text
Tower.Foundation
```

Contains IDs, pure structs, math, result types, time abstractions, configuration interfaces, serialization primitives, and shared constants.

Avoid Unity dependencies where practical.

## 33.2 Suggested assemblies

```text
Tower.Foundation

Tower.Networking
Tower.Authentication

Tower.TowerData
Tower.Chunks
Tower.Construction
Tower.Resources
Tower.Processing
Tower.Progression
Tower.Player
Tower.Social
Tower.Persistence

Tower.Rendering
Tower.UI

Tower.Generation.Editor

Tower.Tests.Unit
Tower.Tests.Integration
Tower.Tests.Multiplayer
Tower.Tests.Load
```

## 33.3 Dependency direction

Target dependency graph:

```text
Foundation
   ↑
Feature domain assemblies
   ↑
Networking / Persistence adapters
   ↑
Client presentation
```

Avoid circular assembly references.

A domain feature should not depend on UI.

Persistence should depend on domain contracts, not vice versa.

---

# 34. Suggested Folder Hierarchy

```text
Assets/_Project/

Foundation/
  Runtime/
    Ids/
    Data/
    Collections/
    Serialization/
    Configuration/

Networking/
  Runtime/
    FishNet/
    Messages/
    Serialization/
    Replication/
    Interest/

Authentication/
  Runtime/
    Steam/
    Entitlements/

Tower/
  Runtime/
    Binary/
    Definitions/
    Lookup/
  Data/

Chunks/
  Runtime/
    ChunkKey.cs
    ChunkManager.cs
    ChunkWorker.cs
    ChunkWorkerPool.cs
    ChunkHasher.cs
    ChunkSnapshot.cs

Construction/
  Runtime/
    ConstructionState.cs
    ConstructionService.cs
    BuildValidation.cs
    StageReplication.cs

Resources/
  Runtime/
    ResourceDefinition.cs
    PlayerResourceState.cs
    BaggageService.cs
    TotemService.cs
  Data/
    Resources/

Processing/
  Runtime/
    RecipeDefinition.cs
    ProcessingInteraction.cs
    ProcessingStationState.cs
  Data/
    Recipes/

Progression/
  Runtime/
    RoleProgression.cs
    UpgradeMasks.cs
    UpgradeEffects.cs
    LevelCapPolicy.cs
  Data/
    UpgradeKits/

Player/
  Runtime/
    Client/
      LocalPlayerController.cs
      PlayerInput.cs
    Server/
      PlayerSession.cs
      PlayerValidation.cs
    Presentation/
      RemotePlayerView.cs
      RemotePlayerInterpolator.cs

Social/
  Runtime/
    Chat/
    Emotes/
    Moderation/
    SteamFriends/

Persistence/
  Runtime/
    SQLite/
    Schema/
    Repositories/
    Snapshot/

Rendering/
  Runtime/
    Near/
    Far/
      IFarChunkRenderer.cs
    Highlighting/
    Pools/

UI/
  Runtime/
    ServerBrowser/
    Chat/
    ActionWheel/
    Totem/
    Progression/
    Cosmetics/

Generation/
  Editor/
    TowerGenerator.cs
    TowerBakeWindow.cs
    TowerSceneScanner.cs
    TowerBinaryWriter.cs
    TowerValidation.cs
    AssetIdBaker.cs

Tests/
  Unit/
  Integration/
  Multiplayer/
  Load/
```

---

# 35. Scene Architecture

Prefer fewer persistent scenes.

Recommended:

```text
Bootstrap.unity
World.unity
```

## 35.1 Bootstrap

Client build:

```text
Game startup
Steam
server browser
FishNet client
configuration
persistent client services
```

Dedicated server build:

```text
server startup
FishNet server
region config
tower binary
SQLite
server services
```

Objects are conditionally created according to build target.

## 35.2 World

Contains client-side authored world presentation / entry points.

The dedicated headless server should not need the rendered world scene to simulate baked tower state.

The tower's authoritative static topology comes from the binary, not scene GameObjects.

---

# 36. Interaction Framework

Gather, Process, and Build share the same user-facing interaction pattern for MVP:

```text
Target
  ↓
Interact
  ↓
Lock movement
  ↓
Wait duration
  ↓
Move to cancel
  ↓
Complete request
```

Implement one reusable interaction state machine rather than three unrelated timer implementations.

Concept:

```csharp
enum InteractionState
{
    Idle,
    Waiting,
    Cancelling,
    Completing
}
```

Feature-specific validation occurs through strategy/services.

This is a game-development-specific use of composition rather than creating a deep inheritance hierarchy.

---

# 37. Client-side Visual Responsiveness

Because interaction initiation is client-authoritative:

- input reacts immediately;
- character animation starts immediately;
- progress UI starts immediately;
- movement lock is immediate.

Persistent result is applied only after the server accepts the completed interaction.

This avoids noticeable click latency without inventing predicted resource balances or predicted tower stages.

---

# 38. Server Validation Philosophy

The server is not intended to simulate every local detail.

Validate what protects shared progression/world integrity.

High-value checks:

```text
authenticated connection
valid target ID
target in player's current chunk
target within reasonable interaction distance
interaction duration plausible
resource amount available
baggage limit
stage not complete
recipe valid
cooldown valid
rate limit valid
speed / teleport exploit
upgrade entitlement valid
DLC entitlement valid
```

Do not waste server CPU reproducing purely visual client mechanics that do not affect persistent state.

---

# 39. Testing Strategy

"Full coverage" means behavior coverage, not artificial 100% line coverage.

Every feature must ship with automated behavior tests.

Tests are part of feature completion.

## 39.1 Unit tests

Examples:

```text
AssetId pack/unpack
negative chunk coordinate math
ChunkKey hashing
binary serializer
binary hash mismatch
stage advance
stage overflow clamp
resource cost calculations
baggage caps
recipe validation
XP math
DLC cap policy
upgrade bit masks
observer-set fill order
observer anchor threshold
chat rate limit
filter rules
snapshot packing
completed chunk compression
```

## 39.2 Integration tests

Examples:

```text
Steam auth adapter mocked integration
FishNet message serialization
player connect -> load -> spawn
disconnect -> retain -> snapshot -> evict
gather -> resource -> XP
process -> consume -> produce -> XP
totem deposit/withdraw
build -> worker -> accepted stage -> resource deduction -> XP
chunk structural complete -> furniture unlock
full complete -> construction rejection
near chunk promotion snapshot
far chunk refresh
SQLite save/reload
graceful shutdown final snapshot
```

## 39.3 Multiplayer simulation

Two levels are required.

### In-process / synthetic

Fast enough for regular development:

```text
10
50
100
500
1000
```

clients or simulated sessions.

### Real FishNet clients

External/headless client processes using actual transport.

Required milestone tests:

```text
50
100
500
1000
```

The 1000-client real transport test may be a dedicated pre-release/soak test rather than every local test run.

## 39.4 Bot behavior

Bots should not only idle.

Randomized legal workload:

```text
move
change chunks
gather
process
withdraw
deposit
build
chat within limits
emote
disconnect/reconnect
```

Also include adversarial tests:

```text
teleport attempt
rate-limit abuse
invalid AssetId
wrong chunk interaction
resource over-withdraw
DLC-gated upgrade request
malformed state
stale sequence
```

---

# 40. Provisional Performance Budgets

Hardware is not defined yet.

Therefore these are engineering targets to guide profiling, not final hardware guarantees.

They must be revisited after the actual US/SA/EU dedicated-server hardware is selected.

## 40.1 Server simulation

Configured tick:

```text
10 Hz
100 ms tick budget
```

Proposed targets at 1000 connected clients:

```text
Median tick work       < 25 ms
P95 tick work          < 50 ms
P99 tick work          < 75 ms
Sustained tick         never >= 100 ms
```

A single occasional spike may occur during profiling, but queues must recover immediately.

## 40.2 Worker queues

Proposed:

```text
P95 command queue latency < 1 simulation tick
No monotonically growing worker queue
No unbounded allocations
```

## 40.3 Hot-path allocations

After warmup:

```text
Player replication hot path:
target 0 B managed allocation per tick

Chunk simulation hot path:
target 0 B managed allocation per steady-state tick

Observer maintenance:
allocation allowed only on infrequent rebuild if required,
but reusable buffers are preferred.
```

## 40.4 Player bandwidth

With approximately 25-byte player state:

```text
100 states
× 10 Hz
≈ 25 KB/s raw state payload per fully populated client
```

At 1000 clients:

```text
≈ 25 MB/s raw server outbound state payload
≈ 200 Mbit/s before protocol/transport overhead
```

Real traffic will be higher after message headers, UDP/IP overhead, FishNet framing, reliable traffic, chat, chunk snapshots, and connection control.

Provisional network target:

```text
Plan for at least 300–500 Mbit/s sustained outbound headroom
for the 1000-user worst-case test.
```

This is a sizing assumption, not a final server-NIC requirement until packets are profiled.

## 40.5 Client bandwidth

Target full 100-player observer state:

```text
< 40 KB/s average movement-state downstream
```

excluding large join/promotion snapshots.

## 40.6 Construction/chunk traffic

Near chunk state:

```text
batch interval <= 1.0 s
default target 0.5 s
```

Far dynamic chunk state:

```text
default target 60 s
```

Do not resend static transforms.

## 40.7 Persistence

Proposed:

```text
Snapshot capture must not stall gameplay > 2 ms on the main thread in one frame.
DB flush may take seconds in background.
No simulation pause.
No SQLITE_BUSY retry storm.
```

If copying all dirty state exceeds the frame budget, spread snapshot copy over ticks or use versioned immutable buffers.

## 40.8 Server memory

Without final tower asset count, do not impose an arbitrary global limit.

Track these budgets independently:

```text
Static tower binary RAM
Mutable chunk stage RAM
Player/session RAM
FishNet/transport buffers
DB snapshot buffers
Rendering = none on headless server
```

Desired property:

```text
Mutable construction RAM should be O(number of baked buildable assets)
with ~1 byte/stage before container overhead.
```

No per-asset server GameObjects.

## 40.9 Client frame budget

Target:

```text
1920×1080
60 FPS
16.67 ms/frame
100 watched remote players
3×3×3 high-detail chunks
```

Provisional split:

```text
Main CPU frame       < 8 ms typical
GPU frame            < 12 ms typical
Total frame          < 16.67 ms target
```

Do not require both CPU and GPU numbers to add linearly; they overlap.

## 40.10 Client rendering stress case

Benchmark:

```text
100 visible humanoid players
active Mecanim
chat bubbles
mixed cosmetics
27 high-detail chunk slots
far tower GPU renderer visible
construction stage changes
```

The observer limit must not merely protect bandwidth; it must also enforce the client presentation budget.

## 40.11 Soak test

Pre-release target:

```text
1000 connected clients
>= 60 minutes
mixed bot workload
```

Success:

```text
no memory growth trend
no unbounded queue growth
no replication desync
no database corruption
stable 10 Hz server tick
```

Longer overnight soak testing is recommended before production.

---

# 41. Logging and Profiling

Instrumentation is required because the 1000-user requirement cannot be validated by architecture alone.

Record:

```text
tick duration
main-thread duration
worker duration
queue depth per worker
connected players
observer count distribution
bytes sent/sec
bytes received/sec
messages/sec
near/far chunk snapshot sizes
DB snapshot duration
DB transaction duration
dirty player count
dirty chunk count
GC allocations
managed heap
native memory
```

Provide development-only admin/debug views.

Avoid verbose per-player production logs at 1000 CCU.

---

# 42. Error and Abuse Handling

## Movement exploit

```text
Small violation:
ignore

Repeated / extreme:
correct position
increment violation metric

Persistent extreme abuse:
disconnect / moderation hook
```

## Invalid interaction

```text
reject
do not mutate shared state
return compact reason code if UI needs it
```

## Static binary mismatch

```text
reject login before spawn
```

## Server capacity

```text
reject
ServerFull
```

## Database write failure

```text
keep dirty in-memory state
log prominently
retry on next controlled persistence attempt
do not clear dirty flags until transaction commit
```

---

# 43. MVP Scope

MVP target:

```text
3 floors
generic resource framework
initial content equivalent to ~3 core resources
Gatherer / Processor / Builder
full progression loop
50+ live-player validation milestone
architecture capable of 1000-user validation
construction
totem
processing
local chat
emotes
simple cosmetics
Steam authentication
Steam DLC progression gate
SQLite persistence
```

Specific final resource IDs/content remain configurable.

---

# 44. Implementation Roadmap and Approval Gates

Every phase includes tests before approval.

## Phase 0 — Foundation and Tower Bake

Implement:

```text
feature assemblies
shared IDs
ChunkKey
64-bit AssetId
TowerGenerator authoring validation
scene scanner
chunk scanner
binary writer
binary reader
content hash
static lookup tables
```

Success:

```text
same scene bakes byte-identical binary repeatedly
client/server reader produces identical asset/chunk counts
AssetId round-trip tested
negative chunk coordinates tested
```

## Phase 1 — Dedicated Server and Authentication

Implement:

```text
separate client/server builds
RegionConfig
FishNet server/client
Steam auth
DLC validation
server browser entries
content hash handshake
server-full rejection
```

Success:

```text
valid client joins
invalid Steam/auth rejected
wrong tower hash rejected
DLC status read on login
```

## Phase 2 — Player Movement and Observer Replication

Implement:

```text
CharacterController
10 Hz client state
server plausibility checks
movement correction
remote interpolation
100-player observer manager
Steam friends priority
30-minute stability
10-chunk anchor trigger
```

Success:

```text
50–100 clients
no NetworkTransform
no NetworkAnimator
stable observer membership
60 FPS interpolation presentation
```

## Phase 3 — Chunk Workers and High-detail Streaming

Implement:

```text
hash partition
worker pool
pure-data chunk state
3×3×3 near promotion
near snapshot
far renderer abstraction
far snapshot
```

Success:

```text
workers never touch GameObjects
chunk ownership deterministic
moving between chunks smoothly promotes/demotes geometry
```

## Phase 4 — Generic Resources and Gathering

Implement:

```text
ResourceDefinition
baggage
resource nodes
gather interaction
cancel behavior
server validation
Gather XP
```

Success:

```text
generic resource IDs
no hardcoded resource database columns
node cooldown not persisted
```

## Phase 5 — Processing

Implement shared concurrent stations, independent timers, cancel semantics, input/output atomicity, Processor XP, and interior unlock hooks.

## Phase 6 — Global Totem

Implement deposit, withdraw, capacity validation, global main-thread ownership, and generic persistence.

## Phase 7 — Construction

Implement stage-0 highlight, stage 1–10 meshes, build wait, multi-stage upgrades, actual-stage cost, worker acceptance, near/far replication, and Builder XP.

Success:

```text
no static transform replication
AssetId+stage semantics validated
concurrent builders cannot over-deduct
```

## Phase 8 — Floor State and Furniture

Implement structural completion, furniture unlock, interior station unlock, furniture construction, full construction freeze, and completed chunk GPU batching.

## Phase 9 — Progression / Cosmetics / Social

Implement 50 kits/role, free cap 20, DLC 21–50, bitmasks, cosmetic list, emotes, chat bubble, local filter, 128-char limit, and 3-second rate limit.

## Phase 10 — Persistence

Finalize WAL, schema, dirty tracking, async snapshot, chunk BLOB, complete-chunk compression, disconnect retention, post-save eviction, and graceful shutdown.

Persistence should be prototyped earlier where needed, but this phase hardens the production format.

## Phase 11 — Scale Validation

Run:

```text
50
100
500
1000
```

with both synthetic/in-process load and real FishNet clients.

Profile against provisional budgets.

## Phase 12 — MVP Validation

Validate:

```text
3-floor complete gameplay loop
1080p/60 target
50+ live-client gameplay
regional deployment
Steam authentication
DLC entitlement
persistence restore
construction completion
```

---

# 45. Major Technical Risks

## 45.1 1000 × 100 observer replication

Worst-case logical replication is still large:

```text
100,000 remote state memberships per 10 Hz update
```

Mitigation:

```text
compact packets
batching
stable observer sets
serialization reuse
no NetworkTransform
no NetworkAnimator
profiling from Phase 2 onward
```

## 45.2 Unity main-thread ownership

Player data, FishNet, global totem, and session state deliberately remain main-thread owned.

Risk:

```text
main thread becomes bottleneck before worker threads
```

Mitigation:

```text
pure-data handlers
no LINQ in hot paths
reused buffers
batched network messages
10 Hz simulation
profile main-thread time explicitly
move only proven bottlenecks later
```

Do not preemptively complicate the ownership model.

## 45.3 Millions of tower assets

Mitigation:

```text
static binary
no server GameObjects
contiguous chunk asset ranges
byte stage arrays
complete-chunk implicit state
GPU far renderer
3×3×3 high-detail cap
```

## 45.4 SQLite snapshot spikes

Mitigation:

```text
dirty-only saves
chunk BLOBs
non-zero resource rows
completed chunk compression
single DB writer
WAL
prepared statements
async snapshots
```

## 45.5 Authoring/bake drift

Mitigation:

```text
bake validation
content hash
immutable production binary
AssetId determinism tests
client/server mismatch rejection
```

---

# 46. Explicit Rejections from Earlier Plans

The final architecture does **not** use these older proposals:

```text
JSON TowerManifest
runtime procedural tower generation
angular sector chunks
chunk-local PlayerState coordinates
friend slot cap of 30 or 50
weighted recency-based observer scoring
distance-tiered watched-player Hz
hardcoded Stone/Sand/Ink database columns
per-asset SQL construction rows
client construction resource prediction
direct build spending from totem
custom persisted friend list
single combined client/server executable build
50-node ambiguity after level 20
far chunk static transforms replicated over network
```

---

# 47. Final Architecture Summary

The intended production architecture is:

```text
                        AUTHORING
TowerGenerator + manual furniture
             ↓
         Scene review
             ↓
     Chunk-by-chunk bake
             ↓
 TowerGenerationConfig.bin
       /                 \
      /                   \
Dedicated Server         Client
(load entire binary)     (load entire binary)
      |                   |
      | static topology   | static topology
      | never replicated  |
      |
FishNet 10 Hz custom player replication
      |
100-player stable observer sets
friends first, locality fills remainder
      |
Main thread:
sessions / player data / totem / FishNet
      |
Chunk worker pool:
pure mutable chunk data
      |
Construction stage arrays
resource-node timers
processing state
      |
Async persistence snapshots
      |
SQLite WAL
```

Client rendering:

```text
3×3×3 near chunks
    ↓
GameObjects + colliders + Graphics.RenderMesh

Far chunks
    ↓
IFarChunkRenderer
    ↓
GPU batching
```

Persistent world state:

```text
Players
PlayerResource
GlobalTotemResource
ChunkState BLOBs
```

Static topology remains entirely outside the database and network.

This is the architecture baseline to implement against.
