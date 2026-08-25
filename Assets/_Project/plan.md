# Tower of Babel - Technical Project Plan

## 1. Project Overview

Tower of Babel is a first-person social MMO incremental game where all players collaborate to construct a massive procedurally-defined tower. Players specialize through activity rather than class selection:

- Gatherer
- Processor
- Builder

Progression is activity-driven and shared within a persistent world hosted per region.

Target platform:

- Unity 6.3 LTS (6000.3.11f1)
- URP
- FishNet 4.7.2R
- SQLite
- Dedicated Headless Servers
- Steam Distribution
- Premium progression through DLC

Primary technical objective:

- Support 1000+ connected users per regional server.
- Maintain 20-100 visible prioritized players.
- Keep server authoritative.
- Minimize network and database traffic.

---

# 2. MVP Scope

## Floors

- 3 tower floors
- Circular tower layout
- Pre-generated construction positions

## Resources

- Stone
- Glass
- Ink

## Roles

- Gatherer
- Processor
- Builder

## Social

- Local chat
- Emotes
- Cosmetic customization
- Friend priority system

## Progression

- Leveling
- Upgrade trees
- Cosmetic unlocks

## Server

- 50+ player validation target initially
- Architecture designed for 1000+ concurrent users

---

# 3. World Architecture

## Regional Deployment

Single executable:

```text
TowerServer.exe
```

Configuration:

```text
RegionConfig.json
```

Regions:

```text
US
SA
EU
```

Each region owns:

```text
World
Tower Progress
Player Data
Chunk State
```

No cross-region synchronization.

---

# 4. Tower Architecture

## Construction Model

Construction locations are predefined.

Players never place structures freely.

Structure types:

```text
Floor Tile
Pillar
Arch
Furniture
```

Furniture is hand-authored.

Other elements are generated before launch.

## Construction Stages

Each constructable piece contains:

```text
0/10
1/10
2/10
...
10/10
```

Every stage maps to:

```text
Different Mesh
Different LOD
Different Visual State
```

## Generation Pipeline

Based on CircleEdgeGenerator prototype.

The tower is generated before gameplay.

Generated data includes:

```text
Chunk ID
Floor Index
Structure Type
Position
Rotation
Construction State
Required Resources
```

Runtime generation is not required.

---

# 5. Chunk System

## Chunk Dimensions

Default:

```text
32m x 32m x FloorHeight
```

Configurable through settings.

## Chunk Structure

Each chunk contains:

```text
ChunkId
FloorIndex
PlayerList
ConstructionObjects
ResourceObjects
InteractionObjects
```

## Threading Model

Recommended:

```text
SimulationThread
 ├─ Chunk
 ├─ Chunk
 ├─ Chunk
```

Threads own many chunks.

Chunks never migrate during execution unless manually rebalanced.

## Ownership Rule

All operations for a chunk execute on the same thread.

This guarantees:

```text
No race conditions
No locking
No synchronization complexity
```

---

# 6. Interest Management

This is the most critical scalability system.

## Visibility Budget

Maximum visible players:

```text
100
```

Priority order:

1. Current chunk
2. Neighbor chunks
3. Friend priority players
4. Extended chunks

## Selection Algorithm

Score =

```text
ChunkDistance
+ FriendPriority
+ RecentInteraction
```

Top 100 become observed players.

All others are ignored visually.

## Replication Groups

Instead of:

```text
1000 x 1000 replication
```

Server builds observer lists.

Only observed players receive updates.

---

# 7. Networking Architecture

## Avoid

```text
NetworkTransform
NetworkAnimator
```

## Core Network Packet

PlayerState

```text
struct PlayerState
{
    Position
    Rotation
    MovementFlags
    AnimationState
}
```

Sent:

```text
10 Hz
```

## Interaction Requests

Client → Server

```text
GatherRequest
ProcessRequest
BuildRequest
ChatRequest
EmoteRequest
CustomizationRequest
```

## Replication

Server → Client

```text
PlayerState
ConstructionUpdate
ResourceUpdate
ChatBubble
EmoteEvent
```

Server remains authoritative.

---

# 8. Player Systems

## Character Controller

Unity CharacterController

Features:

```text
Move
Jump
Interact
```

## Perspective

```text
First Person
```

## Animation

Humanoid Mecanim

Recommended:

```text
Idle
Walk
Run
Jump
Emote
```

Keep animation state compact.

---

# 9. Resource System

## Resource Definition

Data-driven.

Recommended:

```text
ResourceDefinition
```

Stored as ScriptableObject.

Examples:

```text
Stone
Glass
Ink
```

Future resources require no code changes.

## Storage

No inventory UI.

Player data:

```text
ResourceId
Amount
```

Only integer counts.

## Baggage Capacity

Required to prevent abuse.

Players:

```text
Gather
Carry
Deposit
Build
```

Cannot carry unlimited resources.

---

# 10. Gathering System

## Resource Nodes

Located outside tower.

Behavior:

```text
Regenerating
Shared
Persistent
```

Flow:

```text
Interact
→ Gather Timer
→ Resource Reward
```

Server validates all rewards.

---

# 11. Processing System

Stations:

```text
Shared
Multiplayer
Concurrent Usage
```

Flow:

```text
Input Resource
Processing Time
Output Resource
```

Example:

```text
Sand -> Glass
Ink -> Paint
Stone -> Processed Stone
```

---

# 12. Construction System

## Resource Contribution

Players contribute resources.

Progress requires:

```text
Required Resources
+ Build Action
```

## Construction Object

Recommended structure:

```text
ConstructionState
CurrentStage
RequiredResources
StoredResources
CompletionPercent
```

## Frozen Chunks

When complete:

```text
Construction Disabled
```

Still allow:

```text
Movement
Chat
Emotes
Interaction
```

---

# 13. Progression System

## Roles

Experience is activity based.

```text
Gather XP
Process XP
Build XP
```

## Upgrade Trees

Per role:

```text
20 Nodes
Linear
```

Each level grants:

```text
Active Upgrade
Passive Upgrade
Cosmetic Unlock
```

## Premium

Base:

```text
Level 20
```

Steam DLC:

```text
Level 50
```

Progression remains identical.

Only cap changes.

---

# 14. Social Systems

## Chat

Local proximity chat only.

Requirements:

```text
Chat Bubble
Profanity Filter
Spam Protection
Rate Limiting
```

Messages are not persisted.

## Friends

Only used for priority visibility.

No:

```text
Guilds
Parties
Mail
Trading
```

MVP intentionally minimal.

---

# 15. Persistence Architecture

## Database

SQLite

## Save Policy

Configurable.

Examples:

```text
10 Minutes
20 Minutes
30 Minutes
60 Minutes
```

## Stored Data

Players

```text
Resources
XP
Levels
Unlocks
Cosmetics
Friends
```

Chunks

```text
Construction State
Stored Resources
Completion State
```

## Tradeoff

Server crash may lose unsaved progress.

Accepted by project requirements.

---

# 16. Folder Structure

```text
Assets
│
├── Art
├── Audio
├── Prefabs
├── Scenes
├── Tests
│
├── Game
│   ├── Core
│   ├── Networking
│   ├── Chunks
│   ├── Tower
│   ├── Construction
│   ├── Resources
│   ├── Gathering
│   ├── Processing
│   ├── Progression
│   ├── Social
│   ├── Persistence
│   ├── UI
│   └── Player
│
├── ScriptableObjects
├── Addressables
└── Generated
```

---

# 17. Assembly Definitions

```text
Tower.Core
Tower.Networking
Tower.Chunks
Tower.Player
Tower.Resources
Tower.Construction
Tower.Progression
Tower.Persistence
Tower.UI
Tower.Tests
```

Keep dependencies one-directional.

---

# 18. Testing Strategy

Every feature ships with tests.

## Unit Tests

```text
Serialization
XP Calculations
Resource Logic
Chunk Logic
Construction Logic
```

## Integration Tests

```text
Networking
Persistence
Progression
Chunk Loading
```

## Multiplayer Simulation

Automated:

```text
10 Players
50 Players
100 Players
500 Players
1000 Players
```

Validate:

```text
Bandwidth
CPU
Memory
Replication
```

---

# 19. Major Technical Risks

## Risk 1

1000 concurrent users.

Mitigation:

```text
Interest Management
Custom Replication
10 Hz Updates
Player Priority System
```

## Risk 2

Large tower rendering.

Mitigation:

```text
Chunk Streaming
LOD
GPU Rendering
Frozen Construction Chunks
```

## Risk 3

Network traffic.

Mitigation:

```text
Compact Structs
Bit Packing
Custom Serialization
```

## Risk 4

Solo development complexity.

Mitigation:

```text
Strict modular architecture
Automated tests
Feature approval before implementation
```

---

# 20. Recommended Architecture Principles

1. Server authoritative always.
2. All gameplay requests validated server-side.
3. Data-oriented structs for network state.
4. No NetworkTransform.
5. No NetworkAnimator.
6. Chunk ownership never shared across threads.
7. Interest management first-class system.
8. Every feature must include automated tests.
9. Data-driven resources and upgrades.
10. Optimize architecture before content production.

This structure is designed specifically for a solo developer building a scalable social MMO in Unity/FishNet while preserving a path toward 1000+ concurrent connected users per regional server.
