# Rain and synced weather

Added 2026-10-31. This is experimental until the user accepts it. Rain falls by day or by night, and the weather and time of day are the same for every player.

## Source package

The DYNAMIC Rain/Wetness package (`Assets/MoveRotateResize/`) builds its falling rain with VFX Graph and HDRP-only Shader Graphs. VFX Graph is not installed and those shaders do not render in this URP 17.3 project. As with the [wetness port](WETNESS.md), the rain is rebuilt in URP and reuses the package assets in place, with no vendor edits:

| Package asset | Used for |
| --- | --- |
| `DYNAMIC- Falling Rain/Rain/Textures/vxf_rain_rotated_Basecolor_01.tif` | Falling drop sprite (the one the package's falling-rain VFX graph uses) |
| `DYNAMIC- Falling Rain/Rain/Textures/rain_splash_02.tif` | Hit splashes (from the package's hit-decal graph) |
| `DYNAMIC- RainWetness/Audio/audio_rain_main.wav` | Rain loop |
| Rain drop, streak and ripple textures | Surface film, through `AeroWetnessController` |

## Falling rain (`Scripts/Rendering/AeroRainController.cs`)

The controller sits on *Aero island atmosphere* in `Scenes/Tower.unity`, next to the day/night cycle and the wetness controller. It follows the wetness controller's `RainAmount`, so falling rain, surface film and sky fade in and out together (3 s fade).

- **Drops.** A world-space particle system follows the camera: vertical billboards over a 28 × 28 m area, spawned 14 m above the camera and 6 m ahead of the view. It emits 16,000 drops per second at full rain (about 21,000 alive, capped at 24,000). Drops are 8–13 cm wide and 0.8–1.3 m long.
- **Shelter (no rain inside the tower).** Drops are emitted from script, not from a particle shape. `AeroRainController.PlanDrop` uses the tower's stepped-cone shape from `AeroTowerAtmosphere`, the same rule as the wetness shelter:
  - A drop is never spawned where a built deck above covers its column (`IsUnderRoof`, widened by the wind drift).
  - Its lifetime ends exactly on the first deck below it (`TryDeckBelow`), so drops land on outer terraces and never fall through a floor. Deck splashes are emitted where each drop lands.
  - Only built floors count. `TowerAtmosphereBridge` sets `BuiltFloors` from the floor under construction, so an unfinished top floor stays open to the sky.
  - Particle collisions still stop drops on terrain and props.
  - Earlier, drops relied on Low-quality particle collisions alone; fast drops tunnelled through thin decks, and floors away from the player had no colliders, so rain fell inside.
- **Splashes.** About 45% of drop hits spawn a short, fading splash.
- **Day and night.** Drops are grey-blue by day (so they read against a bright sky) and dim blue at night, using the cycle's daylight.
- **Sprite crop.** The package's drop fills only the middle 12% of its texture, so `AeroRainDrop.mat` crops to that column (`_BaseMap` scale 0.14 × 0.95, offset 0.43 × 0.03). Without the crop, each drop renders as a hair-thin sliver.
- **Overcast.** While it rains, the sun drops to 45%, the sky, ambient and skybox exposure dim to 78% and turn slightly grey, and fog is 2.2× denser. With no rain the cycle is unchanged.
- **Audio.** The rain loop plays at 0.8 outside and 0.3 under cover; an upward raycast checks for cover four times a second.

Tuning lives in `Environment/Lighting/AeroRain.asset` (`AeroRainProfile`), with the materials `AeroRainDrop.mat` and `AeroRainSplash.mat` (URP *Particles/Unlit*, alpha blended, soft particles). `Editor/AeroRainAuthoring.Apply()` (menu **Tools ▸ Tower of Babel ▸ Author Rain and Weather**) recreates the profile, materials and scene wiring.

## Synced weather (`Scripts/Networking/Weather/NetworkWeatherService.cs`)

The service sits on the *ResourceAuthority* network object with the other network services. The server owns:

- the time of day;
- rain on or off;
- whether time is frozen;
- whether automatic weather is on.

Every client applies the same state to its day/night cycle and wetness controller, and the rain visuals follow from that.

- **Late joiners.** State is sent as a buffered observers RPC with the server tick, so a player who joins later gets the current weather and the clock advanced by the time since it was sent.
- **Clock.** Every client runs the cycle locally. The server re-sends the state every 10 s; a client snaps to the server clock only when it is more than 1.5 s out, so normal play never jumps.
- **Automatic weather.** The server alternates clear spells of 7–15 minutes with rain spells of 2.5–6 minutes, independent of the time of day, so rain comes by day and by night. With the default 20-minute cycle that is roughly one shower per day/night cycle.
- **Offline.** With no session running, the same requests apply locally, so the F3 controls still preview everything.

The rules live in plain classes in `Scripts/Weather/`: `WeatherDirector` (requests and automatic weather), `WeatherSchedule` (spells) and `WeatherClock` (clock arithmetic).

## F3 controls (World section)

| Control | Effect, for every player |
| --- | --- |
| **Rain** | Starts or stops rain. It also turns automatic weather off, so the choice holds. |
| **Automatic weather** | Lets the server start and stop rain on its own again. |
| **Set day** / **Set night** | Jumps the clock to noon / midnight. |
| **Freeze time of day** | Stops the day/night cycle, for inspecting assets in one light. |

**Wetness preset** and **Post look** remain local previews.

## Feedback round (2026-10-31)

| Feedback | Change |
| --- | --- |
| Rain too subtle; should be heavy rain | 2,600 → 16,000 drops/s in a tighter area, wider and more opaque drops, cropped sprite, grey-blue day tint, louder loop. Collision quality is Low to pay for the count. |
| Wetness stays on with rain off | Starting or stopping rain now clears a forced *Wetness preset* (Dew/Soaked), and surfaces dry in 10 s instead of 40 s. |
| Pillar water and bubbles invisible at night | See [Bubble Tube pillar](../Buildings/BUBBLE_TUBE_PILLAR.md): a night aquarium light from the collar lamps. |
| Interior too bright; volume too dramatic | See [dynamic side rays](DYNAMIC_SIDE_RAYS.md): calmer interior grade and softer shafts. |

Review captures from the real game camera come from the explicit PlayMode test `WeatherVisualCapturePlayModeTests` (interior, a pillar and the exterior; day and night; clear and rain). It writes to `_Screenshots-and-previews-to-delete/Weather/Game/`.

## Not yet done

- **Dew.** The wetness controller's *Wetness Enabled* (dew by time of day) is still off. Rain works without it. Turning it on changes the dry look everywhere, so it is left for the user to decide.
- **Rain on the ocean.** Drops fall through the ocean surface, which has no collider, so there are no splashes or ripples on the sea.
- **Visual review.** The rain has not yet been reviewed in context by the user (day, night, inside the tower, top floor).

## Tests

- `Tests/EditMode/WeatherEditModeTests`: schedule, clock, director rules, offline service, scene wiring and package assets.
- `Tests/Rendering/EditMode/AeroRainEditModeTests`: rain follows the wetness ramp and the camera, night tint, and overcast.
- `Tests/PlayMode/CheatMenuPlayModeTests.WeatherControls_DriveSyncedRainDayNightAndFreezeOnTheHost`: the real F3 controls on a local host spawn falling rain and set day, night and freeze through the service.

Sync to a second, remote client is not covered by automated tests. It needs a two-player session (for example Multiplayer Play Mode).
