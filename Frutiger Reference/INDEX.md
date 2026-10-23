# Frutiger reference library

The user accepted the initial UI version on 2026-10-21. Read [Design direction and future priorities](../Documentation/FrutigerAero/DESIGN_DIRECTION.md) for the consolidated brief. `UI/Project-Fixed-Border-Review.png` is the accepted initial review; earlier images preserve iteration history.

Consult this folder before creating a visual guide or changing this project's visual design. The palette is white, blue, and green, with glossy glass, soft highlights, nature imagery, and readable Frutiger typography. Start with the **Fixed borders and bright blue tabs** section below; earlier images are retained as history.

## UI

The six original user-supplied images from this chat are preserved in attachment order:

1. `UI/01-Chat-Reference.png` — translucent desktop launcher, glass buttons and panels.
2. `UI/02-Chat-Reference.png` — Frutiger Paradise music player, white glass over blue and green scenery.
3. `UI/03-Chat-Reference.png` — desktop music application with distinct sidebar and content areas.
4. `UI/04-Chat-Reference.png` — glossy water-and-globe desktop icon.
5. `UI/05-Chat-Reference.png` — nature dashboard with rounded blue and green controls.
6. `UI/06-Chat-Reference.png` — close-up of the translucent music-player treatment.

Current interactive revision images:

- `UI/Project-Inventory-Icon-Review.png` — generated backpack header and compact resource rows.
- `UI/Project-Upgrade-Interactive-Review.png` — large skill nodes in the draggable viewport.
- `UI/Project-Upgrade-Hover-Review.png` — full effect, cost, and unlock details.
- `UI/Project-Emotes-Centered-Review.png` — solid glass choices and a center close button.
- `UI/Project-Resource-Cost-Review.png` — processed-stone icon for pillar construction.
- `UI/Project-Menus-Highlight-Review.png` — colored shortcut keys.

Other `UI/Project-*` images document earlier UI revisions. They are dated context, rather than a requirement to preserve superseded layouts. New review images belong here as the design evolves.

## Asset

### Stone and processor workshop

- `../Documentation/FrutigerAero/Resources/Previews/Stone-Processor-Idle.png` — textured three-rock resource and rounded white/blue processing machine, modeled in Blender.
- `../Documentation/FrutigerAero/Resources/Previews/Stone-Processor-World.png` — current prefab instances grounded in the Tower scene.
- `../Documentation/FrutigerAero/Resources/Previews/Processor-Working.gif` — sampled roller, press, feed stone, status-light and completion motion. See [asset and animation notes](../Documentation/FrutigerAero/Resources/RESOURCE_WORKSHOP.md) and [generated texture prompts](../Documentation/FrutigerAero/Resources/TexturePrompts.md).

### Exterior tower foliage

- `../Documentation/FrutigerAero/Foliage/Previews/Bush-Sizes.png` — three green broadleaf shrub shapes in small, medium and large sizes.
- `../Documentation/FrutigerAero/Foliage/Previews/Bush-Wind.gif` — sampled slow wind bending and leaf flutter, with anchored bases.
- `../Documentation/FrutigerAero/Foliage/Previews/Bush-Terrace.png` — irregular detail clusters on the outer terrace, outside the wall and clear of the stairs. See [placement and rendering notes](../Documentation/FrutigerAero/Foliage/BUSHES.md).

### Tower terrain landscape

- `../Assets/_Project/UI/FrutigerAero/Art/AeroLandscape.png` — existing nature reference for optimistic green hills, clear blue skies and soft transitions; its historical menu placement is still superseded.
- `../Documentation/FrutigerAero/Previews/Terrain-Overview.png` — current green circular tower clearing, small lakes and flowing green mountains, based on the four boundary pillars.
- `../Documentation/FrutigerAero/Previews/Terrain-Clearing.png` — current flat 108-metre-radius grass-free footprint.
- `../Documentation/FrutigerAero/Previews/Terrain-Ground.png` and `../Documentation/FrutigerAero/Previews/Terrain-Mountains.png` — ground-level rounded ridgeline reviews.
- `../Documentation/FrutigerAero/Previews/Terrain-Lake-1.png` through `Terrain-Lake-4.png` — four small basins exposing the existing turquoise ocean, with rounded green banks. The clearing stays green without grass blades; this supersedes its pearl ground color.

These are Edit Mode captures of the actual generated landscape. See [terrain direction and generator settings](../Documentation/FrutigerAero/TERRAIN_LANDSCAPE.md).

### Interaction motion

- `../Documentation/FrutigerAero/Previews/Interaction-Weave.gif` — current gentle figure-eight sway, buoyant rise and slight tilt on the stone and pillar; sampled in a disposable Edit Mode scene. See [motion notes](../Documentation/FrutigerAero/INTERACTION_WEAVE.md). This replaces the fast gathering/construction shake while keeping colliders and placement fixed.

### Action experience feedback

- `Asset/ExperienceBubble-User.png` — supplied soap bubble reference: translucent center, pastel cyan/pink thin-film rim and broad white reflections. Used for the procedural XP bubble shader.
- `../Documentation/FrutigerAero/Previews/Experience-Float.png` — actual 3D sphere bubbles emerging from the resource, with randomized sizes and motion before collection.
- `../Documentation/FrutigerAero/Previews/Experience-Flight.png` — bubbles curving toward the bottom XP bar.
- `../Documentation/FrutigerAero/Previews/Experience-Arrival.png` — current bottom-left reward popup with aligned backing/glass, collection ripple, animated count and floating action text; the compact interaction prompt remains separately visible.
- `../Documentation/FrutigerAero/Previews/Experience-LevelUp.png` — level transition and upgrade-point celebration.
- `../Documentation/FrutigerAero/Previews/Experience-LevelUpFloat.png` — wider 350-bubble burst for XP that levels up; regular XP uses 35 bubbles at every level. This supersedes the 175-bubble canvas burst.
- `../Documentation/FrutigerAero/Previews/Experience-LevelUp-1280x960.png` and `../Documentation/FrutigerAero/Previews/Experience-LevelUp-2560x1080.png` — current layout at 4:3 and ultrawide aspect ratios.

These are sampled Edit Mode visual previews, not live gameplay captures. See [feedback and tuning notes](../Documentation/FrutigerAero/ACTION_EXPERIENCE.md).

### Building kit — white and blue only

- `../Documentation/FrutigerAero/Buildings/Previews/Pillar-Construction-Lineup.png` — pillar-only stages 0–10: sparse blue guides and scattered, irregular chunks filling in a seeded random order. Horizontal slices and staggered radial columns are superseded.
- `../Documentation/FrutigerAero/Buildings/Previews/Pillar-Construction-Sequence.gif` — static stage sequence.
- `../Documentation/FrutigerAero/Buildings/Previews/Pillar-Chunks-Arrival.gif` — separate chunks form with a Unity dissolve, then fly and turn into their exact pillar positions. Existing pieces remain solid. See [pillar construction notes](../Documentation/FrutigerAero/Buildings/PILLAR_CONSTRUCTION.md). Other building prefabs retain their stage setup.

The 2026-10-21 building brief explicitly excludes green from building assets. It overrides the older blue/green UI palette for this kit.

- `Asset/Building-Arch-Full-Spandrel-User.png` — user annotation: fill the area above the arch opening and provide a flat top connecting to the upper floor.
- `Asset/Building-White-Marble-User.png` — white base with gray-veined marble. The latest shader request retains this white core and reintroduces controlled moving water in aqua glazing and borders; the earlier full-glass experiments remain historical.
- `Asset/Building-Classical-Column-User.png` — user-supplied fluted shaft, stepped base, paired scrolls and ornate capital reference.
- `Asset/Building-Classical-Capital-User.png` — user-supplied close-up of raised classical capital ornament. Adapt its shapes to white pearl, soft blue enamel and marine motifs; retain a low-poly silhouette with baked relief.
- `../Assets/_Project/References/Buildings/frutiger-building-sample-2.jpg` — **primary user-selected target**: continuous rounded white architecture, broad recessed aqua glazing, polished surfaces and reflected water light. Exclude its green vegetation from building colors.
- `../Documentation/FrutigerAero/Buildings/Previews/AeroFlow-Kit.png` — current restored opaque aquatic finish. The user rejected the subsequent see-through borders and requested this solution again.
- `../Documentation/FrutigerAero/Buildings/Previews/BorderGlass-Kit.png` — historical rejected see-through aqua recesses and refracting borders.
- `../Documentation/FrutigerAero/Buildings/Previews/BorderGlass-Floor.png` — historical rejected clear slab rim.
- `../Documentation/FrutigerAero/Buildings/BORDER_GLASS.md` — rejected glass pass; disabled and removed from active renderers.
- `../Documentation/FrutigerAero/Buildings/Previews/AeroFlow-Motion.gif` — current two-layer moving caustic and ripple preview.
- `../Documentation/FrutigerAero/Buildings/AERO_FLOW_SHADER.md` — current restored water-flow shader research, material settings and verification.
- `../Documentation/FrutigerAero/Buildings/Previews/AquaticArchitecture-Kit.png` — current geometry with the earlier static finish.
- `../Documentation/FrutigerAero/Buildings/AQUATIC_ARCHITECTURE.md` — current geometry source and earlier finish verification.
- `../Documentation/FrutigerAero/Buildings/Previews/AeroShell-Kit.png` — historical rejected pearl shell version.
- `../Documentation/FrutigerAero/Buildings/Previews/AeroShell-Pillar.png` — historical rejected rounded capital and glazed strips.
- `../Documentation/FrutigerAero/Buildings/AERO_SHELLS.md` — historical rejected pearl shell direction.
- `../Documentation/FrutigerAero/Buildings/Previews/LowPolyAero-Kit.png` — historical low-poly classical kit.
- `../Documentation/FrutigerAero/Buildings/Previews/LowPolyAero-Capital.png` — historical pearl petals and soft blue capital scrolls baked into color/normal/AO maps.
- `../Documentation/FrutigerAero/Buildings/LOW_POLY_AERO.md` — historical classical ornament settings and previous triangle budget.
- `../Documentation/FrutigerAero/Buildings/Previews/RoundedMarble-Kit.png` — historical rounded white marble kit, superseded by the low-poly version.
- `../Documentation/FrutigerAero/Buildings/Previews/RoundedMarble-Arch.png` — historical rounded corners, moulding and marble relief.
- `../Documentation/FrutigerAero/Buildings/MARBLE.md` — historical marble material and rounding settings.
- `Asset/Building-Water-Glass-Collage-User.png` — superseded material brief: translucent white glass with blue water caustics.
- `Asset/Building-Glass-House-User.png` — glass architecture, soft white reflections and blue water accents. Green in this image does not change the building palette restriction.
- `../Assets/_Project/References/Buildings/frutiger-building-sample-*.jpg` — ten supplied architecture references: glossy white shells, rounded edges, blue glass and luminous trim. Vegetation and green furnishings are not part of the building palette.
- `../Documentation/FrutigerAero/Buildings/Previews/Unity-PlayMode-Kit.png` — implemented four-piece kit and assembled bay in Unity Play Mode.
- `../Documentation/FrutigerAero/Buildings/Previews/Blender-Kit.png` — Blender source render.
- `../Documentation/FrutigerAero/Buildings/Previews/LayeredWater-Kit.png` — historical glass revision: model-specific transparency and 4–6 stacked water layers.
- `../Documentation/FrutigerAero/Buildings/Previews/LayeredWater-InstanceVariation.png` — identical arches with one shared material and differing per-instance patterns.
- `../Documentation/FrutigerAero/Buildings/Previews/LayeredWater-Motion.gif` — historical water close-up, superseded by marble.
- `../Documentation/FrutigerAero/Buildings/LAYERED_WATER.md` — historical water material presets, shader references and validation.

See [building kit notes](../Documentation/FrutigerAero/Buildings/README.md) for dimensions, prefab compatibility, editable sources and inspection results. This is an initial implementation awaiting user review.

- `InventoryBackpack.png` — earlier detailed backpack, retained as a historical reference; superseded by InventorySmall.png.
- `Stone.png` — generated raw stone resource icon.
- `RefinedStone.png` — generated processed stone icon, used for construction costs.
- `AeroLandscape.png` — earlier generated UI landscape; removed from the upgrade menu by user request.
- `UpgradesGrowth.png` — earlier detailed glass steps and growth arrow, retained as history; superseded by UpgradesSmall.png.

The game consumes the originals in `Assets/_Project/UI/FrutigerAero/Art/`. Generation prompts are recorded in `Documentation/FrutigerAero/ArtPrompts.md`. The reusable UpgradesIcon prefab now uses the generated growth-arrow image; the earlier tower-and-leaf emblem is superseded.

## Character

- `FrutigirGirl-Idle.png` — implemented supplied character, idle pose.
- `FrutigirGirl-Jump.png` — supplied jump animation used for jump and all emotes.

Character source models, textures, and animations are in `Assets/_Project/Models/FrutigirGirl/`.

## Latest hover and proportion pass

- `UI/Project-Upgrade-Balanced-Review.png` — current 200 × 96 nodes and generated growth-arrow icon.
- `UI/Project-Hover-Feedback-Review.png` — current pointer-light and glass-rim treatment for interactable UI.

These previews supersede the larger-node proportions shown in the earlier interactive revision images.

## Current background, prompt, and small-icon direction

Use these as the latest visual guide; earlier dense inventory and upgrade icons are historical references.

- `Asset/InventorySmall.png` — simple blue backpack with one green pocket and broad white highlight.
- `Asset/UpgradesSmall.png` — bold green upward arrow on a blue step.
- `UI/Project-Inventory-Small-Icon-Review.png` — current small inventory icon in its actual header.
- `UI/Project-Upgrade-Small-Icon-Review.png` — current small upgrade icon and border-only background hover.
- `UI/Project-Prompt-Gather-Review.png` — pickaxe on the left; gathered material and quantity on the right.
- `UI/Project-Prompt-Process-Review.png` — gear on the left; source-to-target recipe on the right.
- `UI/Project-Prompt-Build-Review.png` — tower on the left; consumed building material on the right.

New icon source canvases come from the built-in image tool, with the small visual-grid constraint recorded in Documentation/FrutigerAero/ArtPrompts.md. Unity imports the simple icon designs as uncompressed 64 × 64 textures. Avoid micro-reflections, elaborate perspective, small sparkles, and other details that become noise in a 36–68 unit control.

## Liquid-wave and action-icon direction

- `UI/07-User-Viewport-Motion.png` — user annotation requesting motion inside the tree background.
- `UI/08-User-Window-Wave.png` — user annotation requesting waving motion along the window edges.
- `UI/09-User-Popup-Wave.png` — user annotation requesting floating motion and waving edges on the interaction prompt.
- `Asset/GatherSmall.png` — simple glossy cyan pickaxe with a green handle.
- `Asset/ProcessSmall.png` — simple six-tooth cyan gear with a green hub.
- `Asset/BuildSmall.png` — three broad cyan/green building blocks.
- `UI/Project-Wave-Upgrade-Review.png` — implemented action icons, brighter buttons, and waving panel borders.

These three action icons match InventorySmall and UpgradesSmall and are shared by the profession tabs and interaction prompt. Buttons restore the brighter full hover response. Background hover remains confined to the border; the background itself has gentle flowing refraction and an animated rounded outline. Exact icon prompts are in `Documentation/FrutigerAero/ActionIconPrompts.md`.

## Fixed borders and bright blue tabs

- `UI/10-User-Button-Color-And-Fixed-Borders.png` — latest user feedback: remove the dark teal selected-tab tint; use the lighter blue treatment shown on Processor and Builder. Animate a light traveling along the border while the outline stays still.
- `UI/Project-Fixed-Border-Review.png` — corrected blue tabs and stationary glass outlines.

This supersedes the animated-outline direction above. Selected tabs retain their original blue glass and use the pale green outline to indicate selection. Glass silhouettes and alpha coverage stay fixed. A soft cyan-white reflection travels around the perimeter; gentle interior flow and popup floating remain.

## Overall UI feedback review

- `../Documentation/FrutigerAero/Previews/UI-Feedback.gif` — sampled window reveals, buoyant icon/ripple feedback, changing inventory amounts and radial emotes.
- `../Documentation/FrutigerAero/UI_FEEDBACK.md` — current motion timings and feedback behavior. Whole-button shaking is superseded by icon movement within stationary controls.
