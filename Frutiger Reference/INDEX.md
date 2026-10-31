# Frutiger reference library

- `Asset/Sun-Shafts-Cloud-User.png` — separated sunlight shafts and drifting atmospheric density, supplied with the dynamic interior ray plan.
- `Asset/Forest-Haze-User.png` — restrained haze and soft occluded shafts, supplied with the dynamic interior ray plan.

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

- `../Documentation/FrutigerAero/Previews/Cheat-Menu-Toggles.png` — God mode and instant-interaction-build toggles, sequential floor completion, and the ALL profession selector.

- `../Documentation/FrutigerAero/Previews/Cheat-Menu.png`, `Cheat-Menu-Stats.png`, `Cheat-Menu-1280x960.png` and `Cheat-Menu-2560x1080.png` — F3 developer controls using the shared glass buttons, Frutiger text, search and scrollable action/stat lists. See [menu controls](../Documentation/CHEAT_MENU.md).

- `UI/Project-Inventory-Icon-Review.png` — generated backpack header and compact resource rows.
- `UI/Project-Upgrade-Interactive-Review.png` — large skill nodes in the draggable viewport.
- `UI/Project-Upgrade-Hover-Review.png` — full effect, cost, and unlock details.
- `UI/Project-Emotes-Centered-Review.png` — solid glass choices and a center close button.
- `UI/Project-Resource-Cost-Review.png` — processed-stone icon for pillar construction.
- `UI/Project-Menus-Highlight-Review.png` — colored shortcut keys.

Other `UI/Project-*` images document earlier UI revisions. They are dated context, rather than a requirement to preserve superseded layouts. New review images belong here as the design evolves.

## Asset

### Transparent river glass pools — 2026-10-24

- `Asset/River-Surface-Flecks-User.png` — small white surface marks read as dust/dirt; replace them with softer flowing water detail and independent opacity/blur controls, while preserving the accepted projected caustics.
- `Asset/River-Wider-Caustics-User.png` — moving projections beside overhead water. Preserve the larger pattern with sparse bright points and faint flowing detail. The approved natural-timing refinement staggers appearances across at most two active sources, occasionally fades everything quiet, and gives the two layers independent lifecycles within a gently meandering current. Keep projector orientation fixed, water excluded and wall blocking intact. Four candidate patch pairs share one high-resolution texture. See the 36-second stationary and walking/turning reviews in the river lighting notes.
- `Asset/River-Glass-Refraction-User.png` — curved glass distorts the background while retaining a clear enclosure.
- `Asset/River-Suspended-Glass-Pool-User.png` — separate transparent shell and moving water interior, visible from below.
- `Asset/River-Water-Distortion-User.png` — aqua water, broad liquid distortion and bright caustic branches.
- `Asset/River-White-Caustics-User.png` — white projected light against blue water and restrained structural frames.

- `Asset/River-Projection-Motion-User.gif` — animated aqua light projected onto pillars and walls; match the existing AquaProjectionLamp's local illumination.
- `Asset/River-Glass-Aqua-Interior-User.png` and `Asset/River-Glass-Caustic-Detail-User.png` — clear cyan glass, white architecture, and branching moving caustics.
- `Asset/River-Glass-Sky-Pool-User.png` and `Asset/River-Glass-Stacked-Pools-User.png` — visible objects through water-filled glazing, including views from below and through stacked floors.
- `Asset/River-Glass-Pool-Wall-User.png` — transparent pool walls, restrained depth tint, reflected highlights and a clear water volume.

The current revision separates the recessed water interior from the smooth glass enclosure, adds thin channel rims and approximately 12-metre panel joints, and projects white moving water light above and below. These occasional pool-section joints supersede the earlier completely seamless river finish. Preserve the two-ring/spoke/pool layout and walkable slab. See [river glass implementation and performance measurements](../Documentation/FrutigerAero/Lighting/RIVER_GLASS.md).

### Continuous tower floors

- `Asset/Connected-Pillar-Ledge-User.png` — on shrinking floors, join the remaining pillar supports sideways like adjoining floor tiles; preserve the stair route and the removal of pads over arches.
- `Asset/Arch-Supported-Pillar-Pads-User.png` — remove the separate square support pads when a pillar connects directly to an arch below; keep support bridges where a setback leaves no arch beneath the pillar.
- `Asset/Tile-Gaps-And-Distance-Culling-User.png` — reported missing distant river/floor assets and visible grid joints in the covered tower interior. See [the repair and verification](../Documentation/FrutigerAero/Buildings/TILE_VISIBILITY_AND_JOINS.md) and its floor 0, 20 and 42 previews. Tile frames should not interrupt the continuous marble or enclosed river.

### Nearby water light and reflections

- `../_Screenshots-and-previews-to-delete/Documentation/FrutigerAero/Lighting/CameraTurn/Sun-Look-After.png` and `Sun-Occluded.png` — direct-sun flare with a soft core, halo, streak and cyan ghost, including depth blocking. `Particles-After.png` shows the existing floating green/cyan particles with 30% more emission and 15% more horizontal coverage. See [camera-turn optimization and atmosphere polish](../Documentation/FrutigerAero/Lighting/CAMERA_TURN_PARTICLES_AND_FLARE.md).

- `Asset/Crystal-Water-Iridescence-User.png` — supplied clear blue liquid/glass close-up: broad flowing distortions, transparent aqua depth, restrained cyan/lilac iridescence, and sparse sharp white glints. Use for future water, glazing and bubble presentation while retaining readable silhouettes and the established white architectural core.

- `Asset/Sun-Beams-Forest-User.png` — supplied golden sun shafts shaped by trees and branches.
- `Asset/Aquarium-Glow-User.png` — supplied luminous aqua aquarium, bright water and glossy highlights; the caption is reference-image content.
- `Asset/Water-Projection-Lamp-User.png` — supplied glowing cube lamp projecting moving aqua light across nearby walls.
- `../Documentation/FrutigerAero/Lighting/Previews/Sun-Rays-After.png`, `Tower-Side-After.png`, `Projection-After.png` and `Projection-Motion.gif` — sunlight entering side openings beneath solid roofs, HDR lamp cores/bloom, and local animated spotlight water cookies. The tower review uses saved cache transforms and a completed overhead floor. See [sun rays and projection lamps](../Documentation/FrutigerAero/Lighting/SUN_RAYS_AND_PROJECTION_LAMPS.md).

- `Asset/Aquatic-Light-Interior-User.png` — supplied bright aquarium interior: moving water light on white walls, ceilings and furnishings, glossy aqua reflections.
- `Asset/Aquatic-Light-Caustics-User.png` — supplied close-up of the branching cyan-white caustic pattern.
- `../Documentation/FrutigerAero/Lighting/Previews/Nearby-After.png` and `Lake-After.png` — dry kit exclusion and localized light at the actual lake. The old camera-wide caustic wash and `Nearby-Motion.gif` are superseded: caustics belong near water and artificial sources. [Lighting notes](../Documentation/FrutigerAero/Lighting/AQUATIC_LIGHT.md) describe source footprints, reflections and the distance budget.

### Stone and processor workshop

- `../Documentation/FrutigerAero/Resources/Previews/Sand-Glass-Idle.png`, `Sand-Glass-Working.png`, `Sand-Glass-Complete.png` and `Sand-Glass-Working.gif` — new Blender sand pile and round glass kiln, shrinking sand, mesh chips, rock cooldown bricks, dust and confirmed glass output. See [sand and glass notes](../Documentation/FrutigerAero/Resources/SAND_GLASS.md).

- `../Documentation/FrutigerAero/Resources/Previews/Stone-Processor-Idle.png` — textured three-rock resource and rounded white/blue processing machine, modeled in Blender.
- `../Documentation/FrutigerAero/Resources/Previews/Stone-Processor-World.png` — current prefab instances grounded in the Tower scene.
- `../Documentation/FrutigerAero/Resources/Previews/Processor-Working.gif` — sampled roller, press, feed stone, status-light and completion motion. See [asset and animation notes](../Documentation/FrutigerAero/Resources/RESOURCE_WORKSHOP.md) and [generated texture prompts](../Documentation/FrutigerAero/Resources/TexturePrompts.md).

### Exterior tower foliage

- `Asset/Tower-Setback-And-Water-Bugs-User.png` — user's annotated screenshot showing the near water boundary, disconnected pillar supports and unnecessary outer floor islands. These defects are superseded by the current repair.
- `../Documentation/FrutigerAero/Buildings/Previews/Setback-Connected.png` and `Ocean-Horizon-14m.png` / `Ocean-Horizon-270m.png` — connected shrinking-floor supports, the cleared outer stair side, and continuous water coverage. See [repair notes](../Documentation/FrutigerAero/Buildings/OCEAN_AND_SETBACK_REPAIR.md).
- `Bush-Terrace.png` and the overhead capture document earlier foliage grounding. [Current floor-support notes](../Documentation/FrutigerAero/Buildings/TOWER_SUPPORTS.md) supersede their isolated cap-pad geometry; the current scene has 164 supported shrubs.

- `../Documentation/FrutigerAero/Foliage/Previews/Bush-Sizes.png` — three green broadleaf shrub shapes in small, medium and large sizes.
- `../Documentation/FrutigerAero/Foliage/Previews/Bush-Wind.gif` — sampled slow wind bending and leaf flutter, with anchored bases.
- `../Documentation/FrutigerAero/Foliage/Previews/Bush-Terrace.png` — irregular detail clusters on the outer terrace, outside the wall and clear of the stairs. See [placement and rendering notes](../Documentation/FrutigerAero/Foliage/BUSHES.md).

### Tower terrain landscape

- `../Documentation/FrutigerAero/Previews/Terrain-Overview-Lightweight.png` and `Terrain-Lake-1-Lightweight.png` — reduced terrain and grass cost, with the rolling landscape and lakes preserved. Current settings and validation are in [landscape notes](../Documentation/FrutigerAero/TERRAIN_LANDSCAPE.md).

- `../Documentation/FrutigerAero/Previews/Terrain-Overview-RollingMeadow.png`, `Terrain-Ground-RollingMeadow.png`, `Terrain-Mountains-RollingMeadow.png` and `Terrain-Lake-1-RollingMeadow.png` through `Terrain-Lake-3-RollingMeadow.png` — requested 2026-10-23 revision: a tighter tower footprint, curved meadows, visible lakes with broad approaches, undulating ridges and grass across the outer terrain. These supersede the earlier wide clearing and four small lake placements.

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

- `Asset/Arch-Light-Ribbons-User.png`: arch direction supplied 2026-10-30. Return to glossy bright white with detail like these luminous, translucent ribbons of fine strands that twist, fold and loop. Arch Rounds 01–04 (decorated spandrels, form and material experiments, carved marble, Wave Scroll Aero variations) were rejected as art directions. The reference's green and yellow are interpreted in the building palette's cyan, aqua and blue unless the user asks otherwise.
- `../_Screenshots-and-previews-to-delete/ArchDesigns/Round05/Arch-Designs-Lineup.png`: ten glossy white ribbon arches. The user picked **10 Aurora Curtain** on 2026-10-31, asking for a rounder opening, no line along the opening edge, and a shader that mimics real aurora behaviour. It is now the prefab arch; see [Aurora Arch](../Documentation/FrutigerAero/Buildings/AURORA_ARCH.md) and its [day](../_Screenshots-and-previews-to-delete/Buildings/AuroraArch/AuroraArch-Ring.png) and [night](../_Screenshots-and-previews-to-delete/Buildings/AuroraArch/AuroraArch-Ring-Night.png) captures.
- `Asset/Aurora-Colours-User.png`: aurora reference supplied 2026-10-31 for the Aurora Arch shader. It shows soft swaying veils with fading bases, and green, teal, blue, violet and pink blending into each other. Green is allowed for the aurora only, at the user's request.

Bubble Tube pillar bubble references, supplied 2026-10-30. Bubbles should be subtle, vary per instance and over time, and appear distorted by the tube glass.

- `Asset/Bubble-Iridescent-Single-User.png`: soft bubble with pink, violet, cyan and gold thin-film colour, bright rim and window highlights. Vary colour, transparency and shape.
- `Asset/Bubble-Large-Distorted-User.webp`: large wobbling, elongated bubbles beside small round ones.
- `Asset/Bubble-Cluster-Glass-Distortion-User.webp`: bubble cluster with thin iridescent rims and dark clear bodies. Emulate the glass bending bubble shapes (the user marked this as most important).

- `../_Screenshots-and-previews-to-delete/PillarDesigns/Round03/Pillar-Designs-Lineup.png`: ten pillar proposals. The user selected **04 Bubble Tube** on 2026-10-30, now the prefab pillar; see [Bubble Tube pillar](../Documentation/FrutigerAero/Buildings/BUBBLE_TUBE_PILLAR.md) and its [stages](../_Screenshots-and-previews-to-delete/Buildings/BubbleTubePillar/BubbleTube-Stages.png) and [per-instance variation](../_Screenshots-and-previews-to-delete/Buildings/BubbleTubePillar/BubbleTube-Instances.png) captures.

- `../_Screenshots-and-previews-to-delete/Documentation/FrutigerAero/Buildings/Finish/Floor0-Day-Arch-After.png` and `Floor0-Day-Glass-After.png` — implemented pearl/aqua finish and bounded floor reflections, based on building samples 2, 3, 6, 9 and 11. [Implementation and validation limits](../Documentation/FrutigerAero/Buildings/BUILDING_FINISH.md) and [walking/turning preview](../_Screenshots-and-previews-to-delete/Documentation/FrutigerAero/Buildings/Finish/Motion.gif) accompany this pass. Pending visual acceptance; existing geometry and lighting directions remain the baseline.

- `../Documentation/FrutigerAero/Buildings/Previews/Pillar-Construction-Lineup.png` — pillar-only stages 0–10: sparse blue guides and scattered, irregular chunks filling in a seeded random order. Horizontal slices and staggered radial columns are superseded.
- `../Documentation/FrutigerAero/Buildings/Previews/Pillar-Construction-Sequence.gif` — static stage sequence.
- `../Documentation/FrutigerAero/Buildings/Previews/Pillar-Chunks-Arrival.gif` — separate chunks form with a Unity dissolve, then fly and turn into their exact pillar positions. Existing pieces remain solid. See [pillar construction notes](../Documentation/FrutigerAero/Buildings/PILLAR_CONSTRUCTION.md).
- `../Documentation/FrutigerAero/Buildings/Previews/Arch-Construction-Stages.png` — matching arch blueprint and random fill stages, preserving the doorway and recessed aqua panel.
- `../Documentation/FrutigerAero/Buildings/Previews/Arch-Chunks-Arrival.gif` — arch chunks use the same formation-and-flight sequence. See [arch construction notes](../Documentation/FrutigerAero/Buildings/ARCH_CONSTRUCTION.md).
- `../Documentation/FrutigerAero/Buildings/Previews/Deck-Chunks-Arrival.gif` — curved floor and stair step assemble from incoming chunks with the same dissolve, flight and placement sequence.
- `../Documentation/FrutigerAero/Buildings/Previews/Floor_Tile-Construction-Stages.png` and `Step_Tile-Construction-Stages.png` — floor and step blueprint/random-fill stages. Preserve full walking colliders, the radial floor layout and the step scale. See [deck construction notes](../Documentation/FrutigerAero/Buildings/DECK_CONSTRUCTION.md).

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

### Glass stair steps — 2026-10-31

The user asked for stair steps made of glass, white with a slight blue colour. See [glass stair steps](../Documentation/FrutigerAero/Buildings/GLASS_STEP.md) and the [preview renders](../_Screenshots-and-previews-to-delete/Buildings/GlassStep/).

- `Asset/Glass-Stair-Spiral-Frosted-User.webp`: frosted aqua-white treads on a curved glass stair.
- `Asset/Glass-Stair-Treads-Stone-Wall-User.webp`: pale frosted treads on steel stringers with glass balustrades.
- `Asset/Glass-Stair-Thick-Edge-User.png`: thick laminated treads; the edges show the glass's blue-green colour.

Second pass, same day: the user found the frosted version looked like a "transparent rectangle", not glass. The treads must be more transparent, and the edges must look like real glass. These references replace the frosted direction:

- `Asset/Glass-Panel-Edge-User.png`: clear glass panel. The body is almost invisible; the polished edges are aqua-green with bright and dark lines. This is the main edge reference.
- `Asset/Glass-Tumbler-Clear-User.webp` and `Asset/Glass-Tumbler-Illustrated-User.png`: thick glass shows dark outlines at grazing angles, bright rim lines and a refracted, distorted base.
- `Asset/Glass-Ribbons-Teal-User.webp` and `Asset/Glass-Ribbons-Blue-User.webp`: thick curved glass with deep teal/blue where light travels further, strong refraction and streaky internal reflections.

## Inner glass river blueprint — 2026-10-24

- `Asset/Inner-Glass-Water-Blueprint-User.png` — current floor plan: two concentric blue glass/water channels, four cardinal spokes and a central pool within the white circular deck. This replaces the provisional outer-ring-only interpretation. Composite tiles keep the ordinary floor footprint and stages, consume glass, and use a reflective glass-covered water finish. See [implementation and previews](../Documentation/FrutigerAero/Lighting/ISLAND_GLASS_AND_DAY_NIGHT.md).

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
