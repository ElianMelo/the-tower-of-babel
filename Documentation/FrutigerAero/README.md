# Frutiger Aero interface

All generated screenshots, comparison images, and motion previews go in the project-root `_Screenshots-and-previews-to-delete/` folder. Follow the [screenshot output policy](../SCREENSHOT_OUTPUTS.md) for future captures; keep written review findings and performance reports in this documentation.

**Accepted initial version — 2026-10-21.** Start with [Design direction and future priorities](DESIGN_DIRECTION.md) for the current visual rules. The sections below include implementation notes and historical iterations; the accepted fixed-border direction supersedes earlier edge-deformation experiments.

The Tower scene now uses `Assets/_Project/UI/FrutigerAero/Prefabs/FrutigerInterface.prefab`. Its inventory wallet is assigned on the scene instance; the input actions, resource definitions, and internal screen references are serialized in the prefab.

## Review

- **Tab** opens upgrades; **I** opens inventory; **R** opens emotes.
- **M** toggles the lower-right **Menus** launcher. Its shortcut list expands upward from the button; choosing an entry closes the list and opens that menu.
- Inventory is a compact panel on the right, with a generated glass backpack header icon and counts beside the resource icons. Emotes use solid glass buttons around the player's screen center; the center button closes the wheel. Upgrades give available nodes a deep-blue surface, white text, and a pulsing cyan outline.
- The close button, **Escape**, or the same shortcut closes a window.
- Opening another window closes the previous window and preserves the cursor/control-lock lifecycle.
- Connection, gathering, resource counts, emotes, and purchases use the existing gameplay controllers.
- Unrevealed upgrade nodes and their connections are hidden. A 2600 × 2200 tree canvas with balanced 200 × 96 nodes can be dragged inside the existing masked viewport. Hover a revealed skill for its full effect, point cost, and unlock condition. Purchases still go through the existing network service.
- Gather, Processor, and Builder use larger bold tabs with a selected outline. The upgrade landscape is removed. The header and Upgrades launcher entry reuse the newly generated glass growth-arrow icon.
- Menu shortcuts are highlighted in lime. Click rotation is limited to a 0.85-degree envelope with a 1.8% scale response, plus the aqua-green glass ripple. All selectable controls and scroll/drag areas have a pointer-following glass light, soft rim glow, and moving sheen. Hover fades use unscaled time, respect disabled controls, and share materials with the click effect.
- Interaction prompts float gently. A pickaxe, gear, or tower identifies Gather, Process, or Build on the left. The right side shows gathered output, the source-to-target recipe, or consumed construction materials with their current quantities.

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

`UpgradeBoardLayout` uses fixed readable node dimensions and spacing within a large tree canvas. The fixed viewport clips the tree while two-axis dragging and inertia let the player explore it. Tabs reset the newly selected tree to its starting skill. The interface scales from a 1920 × 1080 reference. Inventory uses a masked scrolling list.

## Rebuilding and previewing

Use **Tower of Babel → UI → Rebuild Frutiger Aero Interface** in Edit Mode with Tower open. The builder checks the project and scene, rebuilds prefab assets, replaces the old interface, retains the scene wallet binding, and saves the scene. Existing keyboard bindings and authoritative purchase logic are retained.

Use **Tower of Babel → UI → Apply Interactive Aero Revision** to update the current interaction layouts and reusable components in place. The full and compact builders also apply this revision. The organized visual reference library is indexed at `Frutiger Reference/INDEX.md`.

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

## FrutigirGirl character

Local and remote players now use `Assets/_Project/Prefabs/Player/FrutigirGirl.prefab`. This replaces the former PlayerVisuals model prefab while preserving the existing animation and networking component references.

The supplied idle FBX contains the exact matching skinned body. The complete model's hair, headphones, gloves, and ring are bound to that skeleton. Materials use the supplied textures; generated meshes, materials, avatar, clips, and controller live under `Assets/_Project/Models/FrutigirGirl/Generated/`.

Idle and walk use their supplied clips; running reuses walk at a faster rate. All six dance/emote states and the jump state use the supplied jump clip. Generic head-bone support is included for local head hiding and camera following. Original animation trigger values remain compatible with player snapshot replication.

Use **Tower of Babel → UI → Apply Compact Menu Revision** to update the compact layouts in place, or **Tower of Babel → Player → Install FrutigirGirl** to rebuild the character assets. The character builder verifies the source body's topology before binding accessories.

Earlier compact-menu review images: [inventory](Previews/Inventory-Compact.png), [emotes](Previews/Emotes-Quick.png), [menus](Previews/Menus-Expanded.png), [available upgrade highlight](Previews/Upgrade-Highlight.png), [character idle](Previews/FrutigirGirl-Idle.png), and [character jump](Previews/FrutigirGirl-Jump.png).

The compact-menu and character revision passed 60 Edit Mode checks and 22 PlayMode checks. The saved Tower scene is restored to Edit Mode with no temporary preview rows or sample progression changes.

## Frutiger fonts

All project UI and gameplay console text now uses the supplied `Assets/_Project/Fonts/Frutiger.ttf`, with bold headings rendered from `Frutiger_bold.ttf`. Shared label prefabs, data-driven inventory/emote/upgrade entries, console input fields, and the TMP default font all use Frutiger. Both interface builders preserve this font choice.

The generated regular and bold SDF assets bake Latin characters, accents, punctuation, and supported symbols at 80-point sampling with matching padding. Dynamic fallbacks use the same supplied faces and clear their generated data during builds. Use **Tower of Babel → UI → Apply Frutiger Fonts** to reapply the font configuration.

The font revision passed 63 Edit Mode checks and 23 PlayMode checks, including dynamically generated menu entries and actual bold-face rendering. Inventory, upgrade, and emote text reported no overflow at the current layout. Results are recorded in `FontValidation.json`; the Editor was returned to the clean Tower scene in Edit Mode.

## Interactive revision review

Latest images: [inventory icon](Previews/Inventory-Icon-Review.png), [draggable upgrade tree](Previews/Upgrade-Interactive-Review.png), [upgrade hover details](Previews/Upgrade-Hover-Review.png), [centered emote wheel](Previews/Emotes-Centered-Review.png), [construction resource icon](Previews/Resource-Cost-Review.png), and [highlighted shortcuts](Previews/Menus-Highlight-Review.png).

These are temporary Edit Mode previews. The upgrade preview demonstrates an available skill without changing saved player progression. The resource preview demonstrates the pillar's processed-stone cost.

The interactive revision passed 69 Edit Mode checks and 27 PlayMode checks. Coverage includes revealed-node connections, hidden skills, two-axis dragging, tooltip bounds and effect values, center-close behavior, actual resource icons, Frutiger faces, and click feedback that settles correctly even while game time is paused. The glass shader compiled without errors. Results are recorded in `Validation.json`.

All temporary previews are cleared before restoring the saved Tower scene. The indexed reference library in `Frutiger Reference/` contains the six original chat references, current and earlier UI review images, generated asset references, and character previews.

## Hover and proportion polish

The current pass uses 200 × 96 skill nodes, tighter 260 × 158 spacing, 20-point node labels, and 13-point state badges. Click rotation and scale were reduced by roughly 75%. Every selectable control and both scroll/drag areas share pointer-following cyan-green hover light, a soft rim glow, and a moving glass sheen. Legacy opaque hover overlays are disabled so they cannot cover the new VFX. Disabled controls fade out; pausing game time does not interrupt hover transitions.

The generated `UpgradesGrowth.png` replaces the earlier tower-and-leaf emblem and is reused in the Upgrades header and launcher entry. Its exact built-in image-generation prompt is in `ArtPrompts.md`; the image is indexed under `Frutiger Reference/Asset/`.

Current previews: [balanced nodes and icon](Previews/Upgrade-Balanced-Review.png), [hover lighting](Previews/Hover-Feedback-Review.png). These temporary Edit Mode previews do not alter saved progression.

Validation: 69 Edit Mode checks and 28 PlayMode checks passed, including hover enter/exit, disabled-control fades, paused-time transitions, and the reduced click-motion limits. The shader compiled without errors. Unity was restored to the clean Tower scene in Edit Mode.

## Background glass and action prompt revision

Background panels now track the pointer without changing raycast targets. Their hover response is confined to a narrow edge band; panel interior color and alpha stay unchanged. A faint reflection travels around glass borders independently of hover. Backgrounds and interactive buttons use separate fill-light settings, and each surface has one active feedback controller.

Interaction prompts now float gently by up to 4 UI units vertically and 1.1 horizontally, using unscaled time. Refreshing the current target does not reset the motion or accumulate positional drift. The left icon identifies the action: pickaxe for Gather, gear for Process, and tower for Build. The right side uses a reusable resource-slot prefab:

- Gather shows the yielded resource and its current upgraded quantity.
- Process shows source quantity and icon, a directional arrow, and target quantity and icon.
- Build shows the consumed resource and its current upgraded cost.

The prompts receive typed action and resource data directly from the interactable; names are not parsed to infer behavior. The existing purchase, gather, recipe, and construction rules remain the source of the displayed quantities.

The inventory and upgrade buttons now use InventorySmall.png and UpgradesSmall.png: newly generated bold front-view shapes with broad highlights, imported as uncompressed 64 × 64 textures for the 36–68 unit UI controls. The generator still returned large source canvases; these replacements were designed around a small visual grid rather than intricate large-icon detail. Exact built-in generation prompts are recorded in ArtPrompts.md.

Current review images: [Gather](Previews/Prompt-Gather-Review.png), [Process](Previews/Prompt-Process-Review.png), [Build](Previews/Prompt-Build-Review.png), [inventory icon](Previews/Inventory-Small-Icon-Review.png), and [upgrade icon](Previews/Upgrade-Small-Icon-Review.png). Sample preview quantities do not change saved progression.

Validation: 72 Edit Mode checks and 31 PlayMode checks passed. The shader render comparison verifies that background hover preserves interior RGB and alpha, while still detecting interactive fill lighting. Runtime checks cover all three prompt behaviors, quantities and icons, background mouse tracking, and floating without drift. The saved Tower scene is restored to clean Edit Mode after review.

## Liquid-wave borders and matching action icons — 2026-10-21

Buttons restore their previous brighter full hover response. Backgrounds keep border-only hover with stable interior opacity. The shared glass shader now animates the actual rounded outline with two slow waves, a rolling inner rim, and gentle flowing refraction. The tree well uses a 5-unit wave envelope; other decorative panels and the interaction popup use 3.5 units. Outlines move inward within their original rectangle, so hit targets, text, and tree clipping stay stable. Interactive controls retain a crisp silhouette.

Glass motion uses one shared unscaled clock and per-surface vertex data; it does not clone materials or rebuild meshes every animation frame. The popup's stronger floating motion also continues while paused and resets without drift when hidden.

GatherSmall.png, ProcessSmall.png, and BuildSmall.png were generated with the built-in image tool to match the simple inventory and upgrade icons. Each is imported as an uncompressed 64 × 64 sprite and reused through its own prefab in the profession tab and interaction prompt. Exact prompts and source paths: [ActionIconPrompts.md](ActionIconPrompts.md). Current review: [upgrade window with action icons and waving borders](Previews/Wave-Upgrade-Review.png). The three annotated user screenshots are indexed in Frutiger Reference/UI/07–09.

Validation: 73 Edit Mode checks and 32 PlayMode checks passed. Rendered pixel comparisons verify actual boundary movement, stable background interior opacity, and the restored brighter button hover. Runtime checks verify the unscaled glass clock, floating popup, typed action/resource displays, menu shortcuts, and existing gameplay behavior. Full results are in Validation.json. No manual Play Mode session was used; the Tower scene was restored to clean Edit Mode.

## Fixed glass borders and original blue tab color — 2026-10-21

This supersedes the animated-outline pass above. The selected profession tab no longer multiplies the glass by green, and Unity's selected and hovered color multipliers are white. Gather, Processor, and Builder keep the same original bright blue glass; the pale green outline identifies the active tab.

The shader no longer displaces its rounded outline. A soft cyan-white light travels around the stationary perimeter at a constant rate, while background opacity, hitboxes, and clipping remain fixed. The existing gentle background refraction and whole-popup float remain. The obsolete edgeWave setting was removed.

Rendered checks compare every pixel's alpha at two animation times to ensure the outline stays fixed, verify changing border light with an unchanged interior, and exercise all three selected tabs at runtime. See Validation.json for the final test results and Previews/Fixed-Border-Review.png for the latest review image.

Final validation for this correction: 73 Edit Mode checks and 33 PlayMode checks passed. Compilation and shader validation are clean. The Tower scene was restored to Edit Mode after automated verification.
