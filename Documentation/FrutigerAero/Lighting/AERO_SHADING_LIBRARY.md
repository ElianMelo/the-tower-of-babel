# Aero shading library

Added 2026-10-29. This is an experimental direction until the user accepts it. It gives the tower Frutiger Aero material families on top of the accepted white marble core: wetness, subsurface, translucent, glass, anisotropic, iridescent and clear "jelly" plastic.

The DYNAMIC Rain/Wetness package (`Assets/MoveRotateResize/DYNAMIC- RainWetness/`) was the starting point. Its master shaders are HDRP-only Shader Graphs and do not render in this URP 17.3 project. Its sub-graph logic was ported to hand-written HLSL, and its rain textures are reused in place without vendor edits.

The tower is streamed at runtime: instanced far pages, pooled near assets and changing construction stages. So nothing is lightmapped. Quality comes from heavier per-pixel shading, which the user explicitly chose ("shader cost only").

## Reference brief

The brief is drawn from all 116 images in `Assets/_Project/References/` (Buildings, UI, Grouped Pinterest board).

| Role | Colour or treatment |
| --- | --- |
| Sky | Zenith `#0A4FD8` → `#3D9BFF`, milky horizon `#BFE6FF`/`#E8F7FF` |
| Whites | Cool `#F4FAFF` |
| Shadows | Blue (`#7FA6D6`), never black |
| Water | Shallow `#3FD4F0`, deep `#0B7FD0` |
| Clear glass | Nearly invisible, with white-cyan Fresnel rims (`#7FD8F0`) and cyan-green thick edges |
| Foliage | `#7ED321`, backlit to `#E6FF7A` |

Recurring materials in the references:
- near-mirror white floors;
- inflated glossy plastic in lime and white;
- brushed stainless steel (elevator doors, drawers, rollers);
- clear tinted Y2K plastic: iMac and N64 "ice" casings, gel icons;
- glass globe lamps;
- water-filled glass tables with leaves and bubbles;
- soap bubbles with magenta/green thin film;
- CD diffraction;
- dew drops on leaves.

Lighting cues:
- a high sun with a starburst;
- wide soft bloom;
- pale blue haze, not grey;
- light lens flares;
- caustic nets near water.

## Files

All shared code is in `Assets/_Project/Shaders/Aero/`. The include files are header-only; scene-owned globals stay outside `UnityPerMaterial`.

| File | Purpose |
| --- | --- |
| `AeroWetness.hlsl` | URP port of the RainWetness sub graph: porosity darkening vs glossy film, dew/rain drops on top faces, streaks on walls, ripples, rain shelter under upper floors and distance fade. See [wetness](WETNESS.md). |
| `AeroIridescence.hlsl` | `AeroThinFilm3` (650/532/450 nm interference), `AeroBubbleFilm` (swirled 250–700 nm soap film), `AeroDiffraction` (CD grating). The accepted `AeroThinFilm` rim is unchanged. |
| `AeroAnisotropy.hlsl` | Burley anisotropic GGX in full float (thin lobes underflow in half precision). Tangents can come from the mesh, run circular around an axis, or run linear along an axis. Also provides a bent reflection normal and a Kajiya-Kay sheen line. |
| `AeroSubsurface.hlsl` | Per-channel wrapped diffuse with a coloured terminator and a curvature estimate. |
| `AeroTranslucency.hlsl` | Thin-surface back transmission. |
| `AeroGlass.hlsl` | Refracted opaque-texture lookups with the foreground depth guard, Poisson frost, RGB dispersion, Beer-Lambert body tint and Fresnel rim. |
| `AeroIndirect.hlsl` | Specular reflections from the shared Aero day/night capture, falling back to URP probes. Pooled renderers disable probes. Non-finite captures are clamped. |
| `AeroLightLoop.hlsl` | Forward/Forward+ additional-light loop shared by the custom lighting models. |

The three master shaders below follow URP/Lit property names, keep the SRP Batcher layout and support `RenderMeshInstanced`:

- **`Shaders/AeroSurface.shader`** (`TowerOfBabel/Aero/Surface`): opaque uber-lit material with clear coat.
  - Local features: `_AERO_WETNESS`, `_AERO_ANISO`, `_AERO_IRIDESCENT` (film or grating), `_AERO_SSS`, `_AERO_TRANSLUCENT`, `_EMISSION`.
- **`Shaders/AeroGlass.shader`** (`TowerOfBabel/Aero/Glass`): transparent glass.
  - Refraction with dispersion and frost, a thick-edge cyan-green tint, a Fresnel rim, a faint thin film and optional rain drops.
- **`Shaders/AeroJellyPlastic.shader`** (`TowerOfBabel/Aero/Jelly Plastic`): opaque clear plastic, so it needs no sorting.
  - A saturated core where the view crosses the most material, blurred internal structure on three parallax layers, wrapped light, back light and an inner glow.

Existing shaders gained features:

- `BuildingAquaticFinish` and `GlassWaterFloor` (marble):
  - `_AERO_WETNESS`;
  - an `_AlbedoCeiling` (0.94) that keeps lit white under the brighter post looks' bloom threshold.
- `GlassWaterFloor` also gets a polished cyan glass lip around each channel.
- `AeroBushWind`: subsurface wrap toward lime, sunlight through the leaves, sky back light and dew.
- `AeroWorldBubble`: a physical soap film and a thin dark inner ring.

## Material mapping

`Assets/_Project/Editor/AeroMaterialUpgrade.cs` is idempotent and versioned. Menu: *Tower of Babel → Rendering → Upgrade materials to Aero shading*.

| Look | Materials |
| --- | --- |
| Brushed steel (aniso, linear brushing) | `Resource_Steel`, `Workshop_Steel` |
| Candy metal (clear coat) | `Resource_BlueMetal`, `Workshop_Azure` |
| Pearl (clear coat + thin film) | `Resource_Ceramic`, `Workshop_Pearl`, `AquaLampPearl` |
| Glazed ceramic | `Resource_Glow`, `Resource_Refined` |
| Porous (darkens when wet) | `Resource_Stone`, `Workshop_Chip`, `Workshop_Sand(Light)`, `StoneMaterial` |
| Glass | `Workshop_Glass` |
| Jelly plastic | `AquaLampBlue`, `Workshop_Heat`, `AquaLampGlow` (keeps `_EMISSION`, HDR emission and GI flags) |
| Wetness enabled | four `Aero_WhiteMarble_*`, `GlassWaterFloor`, `AeroBushLeaf` |

Terrain (TerrainLit) and the FrutigirGirl player materials are unchanged.

## Review and verification

Use `Tools/AeroShading/CaptureAeroShading.cs` (`script run … --args '["label"]'`). It renders an Edit Mode preview scene with no Play Mode.

Captures go to `_Screenshots-and-previews-to-delete/aero-shading/<label>/`:
- the river from above, steep, grazing, below and below-steep;
- the material line-up when dry, with dew and in rain;
- each post look.

Automated coverage in `Tests/Rendering/EditMode`:
- `AeroShaderLibraryEditModeTests`: every feature variant compiles; iridescence hue spread; the brushed-steel highlight is a streak; backlit translucency; glass refraction; jelly core saturation; instanced rendering; upgraded materials.
- `AeroWetnessEditModeTests`, `AeroPostProfileEditModeTests`, `RiverVolumeEditModeTests`.
- `Tests/Rendering/PlayMode/AeroWetnessPlayModeTests`.
