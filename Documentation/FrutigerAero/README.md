# Frutiger Aero interface

The Tower scene now uses `Assets/_Project/UI/FrutigerAero/Prefabs/FrutigerInterface.prefab`. Its inventory wallet is assigned on the scene instance; the input actions, resource definitions, and internal screen references are serialized in the prefab.

## Review

- **Tab** opens upgrades; **I** opens inventory; **R** opens emotes.
- The close button, **Escape**, or the same shortcut closes a window.
- Opening another window closes the previous window and preserves the cursor/control-lock lifecycle.
- Connection, gathering, resource counts, emotes, and purchases use the existing gameplay controllers.
- The upgrade roadmap shows locked positions. Reveal rules still determine which nodes can be purchased; purchases still go through the existing network service.

## Reusable components

`Assets/_Project/UI/FrutigerAero/Prefabs/` contains the shared glass button, panel, orb, label, progress track, landscape, upgrade board, and vector icon prefabs. `Prefabs/Screens/` contains the HUD, interaction, information, server status, inventory, upgrade, and emote screen prefabs. The interface prefab composes these nested prefabs.

The existing data-driven prefab paths remain valid:

- `Assets/_Project/Prefabs/Interface/UpgradeButton.prefab`
- `Assets/_Project/Prefabs/Interface/OptionButton.prefab`
- `Assets/_Project/Prefabs/Interface/ResourceInventory.prefab`

These inherit the same shared visual components, so changing the base glass button updates navigation, shortcuts, emote choices, and upgrade nodes.

## Glass and proportions

`Assets/_Project/Shaders/AeroLiquidGlass.shader` provides rounded signed-distance edges, a bright glass rim, a glossy upper reflection, nine-sample scene frosting, and slight edge refraction. It supports uGUI stencil masks and rectangular clipping. Shared materials live under `Materials/`; per-element dimensions travel in canvas UV channels, avoiding a unique material for every rectangle.

The glass reads URP's opaque scene texture. Transparent scene effects remain visible through the panel's alpha but are not part of the frosted sample. The current URP asset and game cameras have opaque-color capture enabled. The separate `AeroAtmosphere.asset` retains the existing world effects and adds subtle bloom and color adjustments. Screen Space Overlay UI gets its gloss directly from the UI shader.

`UpgradeBoardLayout` fits seven columns and eight rows, including the level-50 capstone, inside the board. It reserves padding and gaps instead of using fixed spacing. The canvas expands from a 1920 × 1080 reference, preserving the designed minimum space on narrower and wider screens. Inventory uses a masked scrolling list.

## Rebuilding and previewing

Use **Tower of Babel → UI → Rebuild Frutiger Aero Interface** in Edit Mode with Tower open. The builder checks the project and scene, rebuilds prefab assets, replaces the old interface, retains the scene wallet binding, and saves the scene. Existing keyboard bindings and authoritative purchase logic are retained.

`FrutigerAeroPreview.Show("Upgrade")`, `Show("Inventory")`, and `Show("AnimationEmote")` populate temporary Edit Mode visual previews. `FrutigerAeroPreview.Clear()` removes them. Preview rows are marked not to save. Screenshots in `Previews/` are review artifacts, not live gameplay state.

## Validation

The Edit Mode checks cover node bounds, prefab reuse, serialized bindings, close callbacks, scroll clipping, state labels, and existing resource/upgrade behavior. PlayMode checks run only through Unity's automated test runner after implementation is complete; they cover panel switching, restored controls, all 50 nodes per profession, tab selection, and available/purchased/revealed states.

Final results are recorded in `Validation.json`. The Editor is returned to Edit Mode after verification.

## Generated artwork

Artwork was generated with the built-in image generation tool and copied into this repository. See `ArtPrompts.md` for the exact prompts. The final project assets are:

- `Assets/_Project/UI/FrutigerAero/Art/AeroLandscape.png`
- `Assets/_Project/UI/FrutigerAero/Art/Stone.png`
- `Assets/_Project/UI/FrutigerAero/Art/RefinedStone.png`

The progress pixel is a deterministic solid UI sprite authored by the builder. Pictograms are resolution-independent uGUI geometry reused through icon prefabs.
