# F3 developer controls

Press **F3** to open or close the cheat menu. **Escape** closes it too. The cursor becomes available while it is open, and normal cursor and player controls return when it closes. The menu uses unscaled time and can be reopened while the simulation is paused.

Start a session with **Start local host** in Quick actions, or use the existing F1/F2 connection shortcuts. Gameplay changes travel through the existing network services and their authoritative replies update the menu and inventory. No typed project cheat commands remain in the debug console.

## Controls

- **Quick actions:** level up, gain XP, instantly build an asset or floor, increase bag capacity, start a local host.
- **Progression:** choose Gather, Process or Build; add one or ten levels, set level 50, grant XP or upgrade points, purchase individual nodes, unlock every upgrade, reset one profession or all progression. Current level, XP and points are displayed above the list.
- **Construction:** complete or advance the captured asset, reset it, complete or reset a selected floor, complete or reset the whole tower. Bulk actions update progression and renderer data by chunk. Completing a floor reveals the next; resetting it hides later floors and their bushes.
- **Inventory:** add raw stone, processed stone, sand or glass, fill or clear the bag, increase capacity, instantly gather the captured resource, instantly process at the captured machine, respawn all resources. Processing still consumes its recipe input; the glass kiln uses 2 sand for 1 glass.
- **Stats:** movement, sprint and crouch speeds, acceleration, air control, jumping, gravity, extra jumps, mouse sensitivity, camera field of view, gathering/processing/construction speeds, production, processing/construction costs, bag capacity, XP multiplier and resource respawn speed. Multipliers have direct presets; numeric fields support exact values. Reset restores defaults. Construction production controls the number of stages built per normal action. Gathering has no input-cost stat.
- **World:** teleport to a tile on the selected unlocked floor, return to the opening position, toggle flight/noclip, set local time scale, scan construction, cancel the current action. Flight uses WASD, Space to ascend, Ctrl to descend and Shift to accelerate.
- **Animations:** play each available character action and stop the current animation.

Choose an amount with the 100 / 1000 / 10000 presets or enter a value. Floor numbers start at 1. Aim at an asset, resource or processor before opening F3 to capture it; if nothing is targeted, the nearest unfinished construction within 15 metres is selected. The target and bag capacity are shown above the list. Search finds actions across categories, while the category tabs provide a shorter list.

Bag capacities are independent for each player and resource, with a maximum of 1,000,000. Reducing capacity clamps existing amounts. Stat multipliers are clamped to valid finite values, and the normal minimum resource cost is one. Cheat stat overrides reset when the session stops.

## Implementation and review

The reusable menu prefab is `Assets/_Project/UI/FrutigerAero/Prefabs/Screens/CheatMenu.prefab`; `CheatMenuAuthoring.Apply()` installs it and migrates the old console component. Runtime views reuse the project glass-button prefab, glass material and Frutiger faces. The searchable action list is masked and scrollable.

Review images: [quick actions](FrutigerAero/Previews/Cheat-Menu.png), [stats](FrutigerAero/Previews/Cheat-Menu-Stats.png), [4:3](FrutigerAero/Previews/Cheat-Menu-1280x960.png), [ultrawide](FrutigerAero/Previews/Cheat-Menu-2560x1080.png).

Final focused validation passed **58 Edit Mode checks and 13 Play Mode checks**, including a local-host run through the real scene services, F3/Escape input while paused, cursor/control restoration, XP and levels, capacity, gathering/processing, construction and resets, upgrade purchase/reset, and the camera override after Cinemachine. Source and generated terrain checks cover the foundation, lake placement and outer grass density. A scene-unload exception in the XP bubble pool was also fixed and verified during repeated host-scene loading. Unity was restored to the clean Tower scene in Edit Mode. The detailed result is [CheatTerrainValidation.json](FrutigerAero/CheatTerrainValidation.json). Earlier unrelated full-suite failures were not rerun.
