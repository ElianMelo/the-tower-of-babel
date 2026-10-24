# Frutiger Aero UI — accepted initial version and future direction

Accepted by the user on 2026-10-21 as the initial version. Use this document as the current design brief for future UI work. The README contains implementation details and iteration history; earlier screenshots may show superseded experiments.

Primary visual reference: [accepted initial UI](Previews/Fixed-Border-Review.png). Consult the [reference index](../../Frutiger%20Reference/INDEX.md) and its relevant UI, Asset, or Character images before making visual changes.

## Visual identity to preserve

The user's [crystal-water close-up](../../Frutiger%20Reference/Asset/Crystal-Water-Iridescence-User.png) adds a material reference for future water, glazing and bubble refinement: clear aqua depth, broad liquid distortions, subtle cyan/lilac iridescence and sparse brilliant white glints. Keep the flowing forms legible, highlights selective and bloom controlled. Apply this to aquatic surfaces and effects while preserving the accepted opaque white architectural core and nearby-effect performance budgets.

The [nearby aquatic lighting](Lighting/AQUATIC_LIGHT.md) is restricted to actual water and projection-light sources. Lake caustics use the three lake footprints, four metres of shore fade and a three-metre upward limit; artificial lamps use native spotlight cookies within ten metres. Camera proximity is only a performance limit, never permission to illuminate dry areas or other tower floors. The earlier camera-wide water wash is superseded. Preserve white stone, aquatic glazing, lake reflections and lightweight terrain/grass budgets.

The [sun rays and aqua projection lamps](Lighting/SUN_RAYS_AND_PROJECTION_LAMPS.md) add low, warm sunlight entering the tower's open sides beneath covered floors. Side arches and pillars shape the beams; roof holes are not required. Use controlled HDR bloom on aquatic inlays/construction edges and local pearl/blue projection lamps. The faulty depth-reconstructed ambient-occlusion overlay is disabled; baked shading and directional shadows remain. Two exterior workshop lamps are grounded against the terrain; construction-floor visibility remains independent.

The [stone and processor models](Resources/RESOURCE_WORKSHOP.md) use a silvery faceted rock cluster and a rounded pearl-white/azure machine with visible steel rollers and cyan status lights. Generated textures supply the surface detail. The hopper and tray are empty at idle: raw stone enters during use, and processed stone appears only after confirmation. Keep the housing, interaction collider and resource rules fixed.

The [sand and glass workshop](Resources/SAND_GLASS.md) adds a granular sand mound and a distinct round pearl/azure kiln with an amber chamber, chimney, flywheel and bellows. Gathering uses capped mesh fragments, dust, stroke accents and spatial sound. Sand shrinks in its render-only presentation; destroyed stone leaves four small bricks until respawn. Kiln output appears only after a confirmed exchange. Preserve fixed gameplay colliders and bounded particle costs. The terrain now uses the lightweight resolutions and separate editor/runtime grass budgets documented in [landscape performance](TERRAIN_LANDSCAPE.md).

The [pillar](Buildings/PILLAR_CONSTRUCTION.md), [arch](Buildings/ARCH_CONSTRUCTION.md), and [floor/step construction sequences](Buildings/DECK_CONSTRUCTION.md) fill irregular chunks across each shape in a fixed random order. Incoming chunks form outside the asset through a cyan dissolve, then fly and turn into their correct positions. Existing pieces remain solid. Preserve sparse blueprints, original completed meshes, the open arch passage, full walking colliders, curved floor layout and step scale. Horizontal slices and staggered column patterns are superseded.

The [tower landscape](TERRAIN_LANDSCAPE.md) keeps the actual tower foundation level, with grass-free ground inside the boundary and rolling meadows starting just outside it. The 2026-10-23 requested revision replaces the broad 108-metre flat clearing and four crater-like ponds with a tighter 98-metre footprint, curving outer terrain, three more visible lakes with low approaches, and staggered undulating mountain ridges. Grass is populated across the dry outer terrain instead of inheriting empty source patches. Preserve gradual transitions, existing turquoise water and matte green meadow surfaces.

[Small, medium and large bushes](Foliage/BUSHES.md) add irregular green clusters to exterior tower terraces. Keep their canopies and wind motion outside the walls, on supported ledges and clear of stair routes. Plants have no colliders and remain separate from the repeating white/blue architecture.

The [floor-support repair](Buildings/TOWER_SUPPORTS.md) connects shrinking-floor pillar supports inward into the existing deck. The previous isolated pads and unnecessary outer cap row across the stairs are superseded. Preserve the incoming stair route and same-radius floor headroom. [Continuous ocean coverage](Buildings/OCEAN_AND_SETBACK_REPAIR.md) keeps water visible to the horizon from the tower and during flight, using one lightweight camera-following mesh with coarse distant rings. Foliage checks real deck sectors and embeds its roots. F3 retains God mode, instant-build toggles, next-unfinished-floor completion and ALL profession actions; see [developer controls](../CHEAT_MENU.md).

Task completion has a reusable [bubble-to-XP-bar feedback sequence](ACTION_EXPERIENCE.md): real 3D pearlescent spheres emerge from the asset, float and collect into the bottom-left glass bar. Regular gains use 35; leveling gains use 350 with wider spread. Vary sizes by 0.8–1.2 and randomize motion. Preserve the cyan ghost fill, counting XP, floating captions and level-up ripples. Align the popup's backing and glass bounds, and keep the 560 × 140 interaction card above it. Reward presentation stays local to the player and progression remains server-authoritative.

- Frutiger Aero: optimistic nature-and-technology imagery, glossy rounded glass, white highlights, blue surfaces, and green accents.
- Transparent backgrounds should let the world remain visible while preserving readable text and clear controls. Give each menu proportions and behavior appropriate to its purpose.
- Use the supplied Frutiger regular and bold faces throughout the interface, including runtime-generated labels and popups.
- Build repeated visual components from shared prefabs. Update their sources so buttons, labels, panels, resource slots, and icons remain consistent across screens.
- Keep the generated small Inventory, Upgrades, Gather, Process, and Build icon family: bold cyan outlines, broad blue/green shapes, and simple white gloss. Review at the actual 36–68 unit display size. Current imports are uncompressed 64 × 64 sprites; generated source canvases are larger.

## Color and motion decisions

World gathering uses a [slow buoyant weave](INTERACTION_WEAVE.md) instead of fast positional shaking. All four building assets use the incoming-chunk assembly described above. Animate only the presentation: keep source transforms, colliders and construction placement stable. This is separate from the fixed silhouettes of UI panels.

| Element | Accepted direction |
| --- | --- |
| Profession buttons | Preserve the original bright blue glass for Gather, Processor, and Builder. Selection uses a pale green outline. Image tint, normal, selected, and highlighted button color multipliers stay white so they do not darken the material. |
| Interactive hover | Pointer-following glass light, a soft rim, and the brighter button hover response. Disabled controls should not keep an active hover effect. |
| Background hover | Subtle light at the border. Passing over inventory, upgrade, or other background areas must not increase the interior opacity or wash out the panel. |
| Border animation | A soft cyan-white light travels around a stationary rounded outline. Keep edge positions, shape, and alpha coverage fixed. |
| Background motion | Gentle flowing refraction behind the content. Keep text, node positions, hit targets, and masks stable. |
| Interaction popup | A gentle whole-popup float plus the traveling border light. Refreshing the target must not restart the float or accumulate positional drift. |
| Click feedback | A soft press/release ripple with a buoyant icon. Button rectangles, labels and silhouettes stay fixed; only the icon compresses and rises. This supersedes the earlier whole-button shake/scale. |
| Animation timing | UI feedback continues with unscaled time when gameplay time is paused. |

Current popup float is up to 4 UI units vertically and 1.1 horizontally. Treat these values as initial tuning references; evaluate future changes at gameplay scale before increasing motion.

The [overall UI feedback pass](UI_FEEDBACK.md) adds reversible window reveals, staggered row/option fades, keyboard focus light, inventory number and delta animation, confirmed purchase pulses and quiet glass sounds. Brief window opening/dismissal is separate from hover: steady panels still keep their fixed outlines. Emotes and prompts fade without competing with their existing positioning or float.

## Screen behavior to preserve

| Screen | Layout and behavior |
| --- | --- |
| Inventory | Compact, shorter panel on the right. Resource amounts sit close to their icons. Reuse the small backpack icon and scrolling resource-row prefab. |
| Upgrades | Large masked viewport containing a draggable tree. Current tree canvas is 2600 × 2200 with 200 × 96 nodes and 260 × 158 spacing. Hide unrevealed nodes and their connections; give available upgrades a clear highlight. Hover details show the full effect, point cost, and actual unlock condition. |
| Upgrade navigation | Larger, prominent Gather, Processor, and Builder tabs. Header says “Upgrades” and uses the small growth-arrow icon. |
| Emotes | Small, quick radial submenu centered on the player’s screen center. Solid button backgrounds, no titlebar, and a center button that closes it. |
| Interaction | Action icon on the left; resource information on the right. Gather shows output, Process shows source and target, and Build shows consumed material. Quantities come from the actual gameplay and upgrade data. |
| Menus launcher | Lower-right “Menus” button. M toggles its expanding list; entries show highlighted shortcuts. Tab opens upgrades, I inventory, and R emotes. |
| Window lifecycle | Switching menus closes the previous window. Close buttons, Escape, and the same shortcut restore cursor and player control correctly. |

Stone and processed-stone icons identify the actual resources, including construction costs. Keep purchase, gathering, processing, construction, and networking rules as the source of UI information.

The supplied FrutigirGirl remains the local and remote player visual. Preserve its textures, rig, idle/walk setup, and the current use of the supplied jump clip for jump and all dance/emote choices unless a later character request changes that direction.

## Superseded experiments

Do not carry these earlier experiments forward as the default style:

- Waving, deforming, or displaced panel edges. The accepted effect is light moving along fixed borders.
- Dark teal/green selected-tab fills caused by multiplying the blue material with a green or cyan selection tint.
- Background hover that raises opacity or brightens the whole panel.
- Detailed large illustrations reduced to noisy menu icons, tiny sparkles, and intricate micro-reflections.
- Oversized upgrade nodes or trees extending outside their visible viewport.
- A uniform square-window layout for every menu, the full bottom menu bar, or the top-left “The Tower of Babel” button.
- The generated landscape artwork inside the upgrade menu.

Historical images and prompts remain in the repository for context. Prefer the accepted fixed-border review and the current small-icon family when references disagree.

## Proposed priorities for later versions

1. **Readability in the world.** Review panels over bright sky, grass, dark interiors, and busy geometry. Tune the existing glass and text contrast without sacrificing the accepted transparency or blue button colors.
2. **Layout and tree navigation.** Review compact, widescreen, and ultrawide views, tooltip placement at viewport edges, and the balance between visible tree area and readable nodes. Keep drag behavior and hidden-node progression clear.
3. **Feedback polish.** Iterate on the border light’s speed, width, and intensity; hover transitions; popup float; and click response in small, reviewable passes. Keep outlines fixed and motion restrained.
4. **State consistency.** Review selected, available, purchased, locked, disabled, loading, and empty states for clear meaning and consistent prefab usage. Extend the simple icon family as new actions or resources are introduced.
5. **Accessibility and performance.** Consider reduced-motion options, stronger focus cues, and UI scaling. Profile transparent overdraw and scene refraction before adding more shader layers or post-processing.

These are planning priorities for future work. The initial version is accepted; subsequent visual changes should be guided by feedback on a concrete preview.

## Implementation and review workflow

- Start with this brief and the reference index. Keep new references categorized and indexed inside this project.
- Work through the existing uGUI prefabs, shared glass material/shader, and authoring helpers. Preserve Frutiger fonts, serialized callbacks, gameplay data bindings, and scene dependencies.
- Use Unity CLI/MCP where available and verify the connected Editor belongs to this project. Apply prefab and scene changes through Unity.
- Compare screenshots at actual display size and inspect animation over time. A still image alone cannot validate a traveling light or a floating popup.
- Follow the repository’s current AGENTS.md for implementation, compilation, automated tests, and permitted Play Mode verification. Restore the clean Tower scene and remove temporary preview objects afterward.
- Save current review images and generation prompts in the existing documentation/reference folders. Update this brief when the user accepts a new direction; label superseded images clearly.

Initial-version validation record: [Validation.json](Validation.json), with 73 Edit Mode and 33 PlayMode checks passing, no shader errors, and the Editor restored to clean Edit Mode. This is a historical baseline, not validation of future changes.
