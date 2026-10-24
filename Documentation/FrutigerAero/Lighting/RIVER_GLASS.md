# Transparent river glass pools

The 2026-10-24 rework replaces the opaque painted river bed with a transparent water/glass surface. Two rings, cardinal spokes, the central pool, the 20 cm slab envelope, walking collision, costs, construction stages and save identifiers are retained.

## Rendering

The original glass floor material now draws opaque marble only in completed panels. The same channel aperture is clipped in forward, depth and shadow passes. Partial construction keeps its original presentation. An explicit floor identity prevents underside pixels using the preceding floor's channel layout.

`RiverGlassRendererFeature` draws registered completed near tiles and distant instance pages with the installed Stylized Water 3 SmoothWaves normal and Caustics_1 texture. Vendor files are unchanged. Water motion uses continuous world coordinates and one shared clock, including across ring/spoke junctions. The distant renderer retains its 511-instance limit and radial deformation; river pages are separated by floor.

Absorption uses the profile's water thickness divided by the viewing-angle cosine, capped at grazing angles. The distance to lower-floor geometry does not make shallow water opaque. Refraction rejects foreground depth discontinuities and fades at screen edges. Fresnel/environment reflections, sun glints and subtle boundary tint retain a clear center.

Floors compose from far to near against accumulated scene color. At most three nearby layers copy the color attachment for refraction. More distant layers retain transparent absorption and reflections, with a smooth refraction fade. The mobile renderer reduces refraction. Regular transparent effects render after the river; their pixels are not included in its refraction background.

## Moving water light

`RiverGlassLighting` reads completed, visible tiles directly from chunk data, independently of near-pool leases. It selects two local patches, separated by five metres, within 42 metres of the camera. Each patch supplies an upward and downward spotlight: four river lights and two shared 128 px cookies maximum. Cookies advance at 8 Hz. Each light uses a 256 px shadow and a 10-metre range. Solid architecture blocks projection; completed glass apertures transmit it. Lamp budgets remain separate.

Patch changes fade out before moving their source and fade back in. Rollback invalidates emission immediately. Disable releases native lights, cookie textures and materials. The old per-tile elliptical overlay stays disabled; lake regions retain their existing overlay. Projection is disabled when the active render pipeline has no additional-light shadow support.

## Authoring and validation

Tune `Assets/_Project/Environment/Lighting/RiverGlass.asset` for tint, absorption, thickness, ripple scale/strength/speed, refraction, reflection and day/night projection. The active renderer and chunk manager share this profile.

Run **Tower of Babel → Environment → Apply transparent river glass pools** to restore renderer/profile/scene references. The tile-repair and island regeneration workflows reapply this configuration. The original completed collision mesh remains shared with ordinary floors.

`RiverGlassVisualReview.Capture()` creates disposable Edit Mode views from above, below, through stacked floors, at the ring junction and at two night animation times. It restores the active scene, lights and render settings. `RIVER_GLASS_PREVIEW.json` records water/far draw counts, refraction layers, lights, cookies and CPU render duration. CPU duration includes editor overhead and shader warmup; it is not GPU frame time or a gameplay FPS claim. GPU timing is unavailable in this capture path and is recorded as null.

Automated checks cover exact channel masks, finite optical paths, floor identities, 511-instance pages, provider cleanup, animated cookies, completion/rollback, resource release and native GPU transmission from above and below through multiple floors. Relevant PlayMode checks must run only through Unity's automated runner after implementation is complete.
