# Settings menu

Press **Escape** to open or close the game settings. While another window is open (inventory, upgrades, emotes, the menu
dock, the developer controls), Escape only closes that window. The cursor becomes available while the menu is open and
the player does not move; both return to how they were when it closes. Every change applies at once and is saved
(`PlayerPrefs`, key `TowerOfBabel.Settings`).

The key left of **1** (the one that used to open the debug console) now **shows and hides the mouse cursor**. The debug
console object is no longer in the Tower scene. Like every other key, the cursor key can be changed in the Controls tab.

## Tabs

| Tab | Settings |
| --- | --- |
| **Video** | Resolution, Display mode (Fullscreen, Borderless, Windowed). Press **Apply** to change the display. The Editor's Game view keeps its own size; these act in a built game. |
| **Graphics** | Quality (Low, Medium, High), then every value the presets set, each adjustable on its own (the quality then reads **Custom**). |
| **Audio** | Master, Music, UI, VFX. |
| **Gameplay** | Look sensitivity, Invert X axis, Invert Y axis. |
| **Controls** | Every key, remappable. Click a key, then press the new one; Escape cancels. |

**Restore defaults** (bottom right) resets the open tab.

## Graphics quality

High is the default: the scene exactly as authored, with nothing reduced. Medium trades what is hard to notice in motion.
Low goes for the highest frame rate and accepts a clearly simpler picture. Values marked *scene* are read from the scene
when the game starts, so High always follows the authored scene.

| Setting | Low | Medium | High |
| --- | --- | --- | --- |
| Shadow quality | Low: 1024 px, 1 cascade, low filter | Medium: 2048 px, 2 cascades, medium filter | High: *scene* (4096 px, 3 cascades) |
| Shadow distance | 40 m | 80 m | *scene* (120 m) |
| Pixel quality (terrain pixel error) | 30 | 14 | *scene* (8) |
| Base map distance | 120 m | 400 m | *scene* |
| Grass draw distance | 30 m | 65 m | *scene* (95 m) |
| Grass density | 30% of the scene's | 70% of the scene's | *scene* |
| Camera far plane | 1,500 m | 6,000 m | *scene* (65,536 m, so the ocean reaches the horizon) |
| Render scale | 75% | 100% | 100% |
| Tower detail distance (vines, lava lamps) | 60 m | 120 m | 170 m |
| Tower mesh detail (LOD distance) | 35% | 70% | 100% (the switch stays under a pixel) |
| Tower occlusion culling | On | On | On |
| Floor reflections (live) | Off | On | On |
| Sun shafts | Off | Low (quarter resolution) | High |
| Clouds | Low | Medium | High |
| River refraction | Reduced (one floor, half resolution) | Full | Full |
| Lava lamp lights | 12 | 40 | *scene* (96) |

**Lava lamp lights** (0 to 128) is how many lamps cast real light at once. The nearest lamps the player can see are
lit; with fewer lights than lamps in view the lit area around the player is smaller and fades out at its edge, at any
value without lights switching on or off (see [lava lamps](FrutigerAero/Lighting/LAVA_LAMPS.md)). 96 lights cost
about 1 ms more per frame than 24 where many lamps are in view.

Camera near plane and field of view are comfort settings: no preset changes them. A far plane below the scene's cuts
the ocean short of the horizon (the ocean script used to force 65,536 m every frame; it now follows this setting).
**Tower occlusion culling** changes nothing on screen (it only skips what is hidden); the switch exists to compare. The settings from "Render scale" down
were introduced with the [far tower's GPU culling](Performance/PERFORMANCE.md).

## Controls

| Control | Default key |
| --- | --- |
| Move forward / back / left / right | W / S / A / D |
| Jump / fly up | Space |
| Sprint | Left Shift |
| Fly down (god mode) | Left Ctrl |
| Interact / build / gather | E |
| Scan nearby construction | 1 |
| Show or hide the mouse cursor | the key left of 1 |
| Inventory, Upgrades, Emotes, Menu dock | I, Tab, R, M |
| Settings / back | Escape (fixed) |
| Developer controls, Developer graphics window | F3, F4 |
| Start a local host, Join the local host | F1, F2 |

A key that another control already uses is swapped: that control takes the previous key, so nothing is left unbound.
The arrow keys also move the player and are not remapped.

## Implementation

- `Scripts/Settings/GameSettings.cs`: the saved values, their ranges, and the three presets (`GraphicsPresets`).
- `Scripts/Settings/GameSettingsApplier.cs`: applies them to the running scene (pipeline shadows and render scale,
  terrain, camera, far tower culling, render feature tiers, sound levels, resolution) and puts everything back when it is
  disabled. It measures the scene's own values first; those are what High keeps.
- `Scripts/Settings/GameInput.cs`: every control in one place. The actions live in `Assets/InputSystem_Actions.inputactions`
  (map `Player`); remapped keys are stored with the settings as binding overrides. Game code reads `GameInput`, never a
  fixed key.
- `Scripts/Settings/SettingsMenuUI.cs`: the menu, built in code with the same glass panels, fonts and button prefab as
  the [developer controls](CHEAT_MENU.md).
- `Scripts/Rendering/AeroRenderQuality.cs` and `AeroPipelineQuality.cs`: the render feature tiers, sound group levels and
  the pipeline asset values. The pipeline asset is restored when the game stops.
- The prefab is `Assets/_Project/UI/FrutigerAero/Prefabs/Screens/SettingsMenu.prefab`; **Tower of Babel/UI/Install
  settings menu** (`SettingsMenuAuthoring`) builds it, installs it in the Tower scene and removes the debug console
  object. A scene without the saved menu still gets one at run time, styled from the developer controls.

The project has no music yet; the Music level is stored and exposed as `AeroAudio.Music` for the first track.

Tests: `GameSettingsEditModeTests` (presets, saving, controls, menu rows, pipeline restore) and
`SettingsMenuPlayModeTests` (Escape, the cursor key, settings reaching the running game, remapped keys). Automated tests
use an in-memory settings store and never touch the saved settings.
