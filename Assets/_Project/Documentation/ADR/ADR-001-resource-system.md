# ADR-001: Server-Authoritative Resource Gathering and Inventory

- Status: Accepted
- Date: 2026-08-28
- Scope: MVP resource gathering, inventory presentation, and FishNet authority boundary
- Related architecture: `Assets/_Project/plan.md`, sections 2.3, 9, 18, 19, 30, 37, and 38

## Context

Tower of Babel requires a generic resource system that can grow beyond Stone without introducing resource-specific player fields, database columns, UI code, or network messages. Resource rewards affect persistent progression and therefore cannot be trusted to the client.

The first networking milestone runs locally as a FishNet Host, but the implementation must preserve a dedicated-server/client boundary. The target architecture supports more than 1,000 connected users and consequently must not create one FishNet `NetworkObject` per player solely to handle resource RPCs.

At the same time, gathering must feel responsive. Waiting for a server round trip before showing interaction feedback would make gathering feel delayed. The client therefore begins the presentation immediately, while the server remains authoritative over acceptance, duration, rewards, capacity, and node cooldown.

## Decision

### Generic resource definitions

Resources are authored through `ResourceDefinition` ScriptableObjects. A definition contains the resource identity and presentation/gameplay values currently needed by gathering:

- stable string resource ID;
- `ResourceType` enum value used by the current runtime protocol;
- display name and icon;
- amount gathered;
- interaction duration;
- respawn cooldown;
- visual shake settings.

All available definitions are registered explicitly in one `ResourceDefinitionCollection`. Inventory UI and future content systems consume this collection rather than scanning folders or hardcoding Stone.

Stone is the first definition. Additional resource types must be added through definitions and the central collection.

### Resource node identity

Each scene resource node has a stable serialized `ulong NodeId`. The server resolves interaction requests through this ID rather than accepting a client-provided GameObject reference.

The initial Stone node uses ID `1`. IDs must be unique and non-zero. This temporary scene-authored identity is expected to migrate to the planned baked resource-node identity system without changing the network request boundary.

### Interaction contracts

`IInteractable` defines generic local interaction presentation. `IServerAuthoritativeInteractable` adds the network lifecycle:

- request server start;
- request server cancellation;
- notify the local interaction when the server rejects it.

The raycaster remains generic and can support future non-resource interactions. Resource-specific authority is implemented by `Resource` and `NetworkResourceService`.

### Client interaction flow

The client begins gathering immediately when E is pressed:

```text
E pressed
  -> enter Gathering state and lock movement/camera
  -> begin local progress and resource visual feedback
  -> send start request with NodeId and current player position
```

The client does not add resources to its wallet and does not predict node cooldown.

If the server rejects the request, the client cancels progress immediately and returns to the appropriate control state. A rejection does not send a cancellation request back to the server.

Pressing E a second time during gathering cancels locally and sends a cancellation request. Disconnecting during gathering also cancels it and transitions the player to `Locked`.

### Server authority

One scene `NetworkObject` named `ResourceAuthority` hosts `NetworkResourceService`. It is a separate Tower scene root and must not be parented under the persistent `NetworkSystem`. Parenting it under `NetworkSystem` would move it to `DontDestroyOnLoad` before FishNet registers Tower scene objects, causing a missing SceneId error.

The global service accepts RPCs without object ownership and identifies the player by the sending FishNet `NetworkConnection`. It owns:

- active gathering request per connection;
- authoritative interaction timer;
- resource-node availability and cooldown;
- authoritative per-player resource amounts;
- validation and rejection responses;
- targeted wallet updates;
- cooldown replication to observers.

The server validates that:

- the sender is connected;
- the sender has no other active gather;
- the node ID exists;
- the node is available;
- the reported player position is within interaction distance;
- the player has capacity for that resource.

After the authoritative duration, the server grants the resource, begins node cooldown, broadcasts cooldown presentation, and sends the resulting balance only to the affected client.

### Server-side player resource storage

`ServerPlayerResourceStore` keeps an in-memory mapping:

```text
FishNet ConnectionId
  -> ResourceType
     -> Amount
```

Each resource currently has an independent capacity of 50. Additions clamp to that capacity. This is an MVP policy boundary, not the final baggage implementation.

The store is cleared for a connection when it disconnects. SQLite persistence, Steam identity, reconnect restoration, and progression-derived capacity are deferred.

### Client wallet and inventory UI

`PlayerResourceWallet` is a client-side view of authoritative server data. Network code uses `SetAuthoritativeAmount`; resource completion never calls `Add` locally.

The wallet publishes a typed amount-changed event. `InventoryUI` creates one `ResourceInventoryDisplay` from the authored prefab for every entry in `ResourceDefinitionCollection`, including definitions with an amount of zero. When a balance changes, only the corresponding row is updated.

The inventory:

- starts hidden;
- toggles with the `Player/Inventory` Input System action bound to Tab;
- remains visualization-only;
- does not block movement, camera control, or gathering;
- may be toggled while gathering.

`InterfaceManager` remains the singleton entry point for interaction, inventory, and server-status UI.

### Player control state machine

Independent boolean locks are rejected because one system could unlock controls while another system still requires them locked. `PlayerControlStateMachine` owns the effective state:

```text
Locked > Gathering > Moving
```

- `Locked`: client is not connected; movement and camera are blocked.
- `Gathering`: client is connected and gathering; movement and camera are blocked.
- `Moving`: client is connected and not gathering; controls are enabled.

Connection loss has priority over gathering, interrupts the interaction, and enters `Locked`. Reconnection enters `Moving`.

F1, F2, and Tab remain usable because their input handling is outside the player movement/camera consumers.

### Connection presentation and startup

`NetworkBootstrap` provides the MVP startup paths:

- F1 starts server and local client as Host;
- another F1 press does nothing while networking is already active;
- F2 starts a localhost client;
- a `UNITY_SERVER` build starts only the dedicated server.

`NetworkConnectionStateController` listens to FishNet client connection events and drives the control state machine and `ServerStatusUI`:

- disconnected: red `Not connected to server` at top-center;
- connecting: yellow `Connecting...`;
- connected: status hidden.

`NetworkResourceService` checks `InstanceFinder.IsClientStarted` before sending requests. It must not access `NetworkBehaviour.IsClientStarted` before its scene `NetworkObject` has been initialized.

## Consequences

### Positive

- Persistent resource outcomes are server-authoritative.
- Client interaction feedback starts without network latency.
- The client cannot grant itself resources by completing local progress.
- Resource balances are generic and isolated per player.
- One global RPC authority avoids one resource-network object per player.
- UI updates are event-driven rather than polled each frame.
- Control state transitions cannot accidentally unlock a disconnected player.
- The same service boundary can run as Host, client, or dedicated server.

### Negative and tradeoffs

- The current server identifies players by transient connection ID, so balances do not survive reconnects.
- The server currently validates distance using a client-reported position. This protects basic request consistency but is not secure against a modified client.
- Resource state is stored in scene GameObjects instead of the planned pure-data chunk workers.
- A single global service is appropriate for the MVP but its dictionaries and coroutines must be replaced or partitioned before large-scale simulation.
- `ResourceType` is currently an enum; the final protocol should move to the stable baked numeric `ResourceId` described in the architecture plan.
- Node cooldown replication currently targets observers of the global authority rather than chunk-interest recipients.

## Alternatives considered

### Grant resources locally and synchronize later

Rejected because it allows client-predicted balances to temporarily or permanently diverge and creates an exploit boundary around persistent progression.

### Wait for server approval before showing progress

Rejected because it adds visible interaction latency. The selected design begins presentation immediately and rolls it back on rejection.

### One networked player object per connected user

Rejected for this milestone. Player replication will use custom compact state messages and observer budgets; resource RPCs do not justify replicating more than 1,000 player `NetworkObject` instances.

### One network object per resource node

Rejected because the tower may contain very large numbers of resources and static world assets. Nodes are addressed by stable IDs through the global service.

### Automatic folder scanning for resource definitions

Rejected in favor of an explicit collection that provides deterministic content membership and can later participate in content hashing.

### Independent boolean control locks

Rejected because connection and interaction systems could overwrite each other's lock state.

## Validation

EditMode tests cover:

- resource definition configuration;
- central definition collection;
- wallet accumulation and change notification;
- server store player isolation and capacity enforcement;
- interaction and server-status UI behavior;
- resource visual cancellation.

PlayMode tests cover:

- FishNet Host startup and duplicate-start rejection;
- client cancellation request behavior;
- server rejection rollback;
- authoritative cooldown availability;
- no predicted wallet reward or cooldown;
- inventory row creation and event-driven updates;
- inventory toggling during gathering;
- control-state transitions and disconnect interruption.

At acceptance, 9 EditMode and 10 PlayMode tests pass.

## Deferred work

- stable baked `ushort ResourceId` protocol;
- custom 10 Hz player-state replication and server-tracked position validation;
- chunk-worker-owned resource availability timers;
- progression-derived baggage capacity;
- Steam identity and reconnect restoration;
- SQLite persistence;
- Gatherer XP;
- chunk-based interest management for cooldown updates;
- rate limiting and broader exploit telemetry;
- resource definition/content hash validation during connection.
