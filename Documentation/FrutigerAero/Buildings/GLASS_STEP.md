# Glass stair steps

Requested on 2026-10-31: stair steps made of glass, white with a slight blue colour.

The first version was lightly frosted glass. The user rejected it the same day: it looked like a transparent rectangle, not glass, and the edges were nothing like real glass. The current version is clear, thick glass modelled on the references in `Frutiger Reference/INDEX.md` (glass stairs, glass panel edge, tumblers, glass ribbons).

## Look

- **Body:** nearly clear. Floors, lower steps and objects show through, refracted and very lightly tinted white-blue (`_TintColor`).
- **Edge band:** looking down through the tread, a deep aqua band appears along every edge. Its width changes with the viewing angle, and a crisp dark line with a bright line beside it marks its inner boundary. This is what thick glass panels look like.
- **Side faces and bevels:** deep aqua with uneven bright and dark streaks, strong refraction, subtle colour fringes and dark outlines at grazing angles, like a polished glass edge.
- **Reflections:** Fresnel sky and probe reflections (the shared Aero environment when it is ready) and a sharp sun glint. Reflections add light and never hide what is behind.
- **Shadows:** faint (`_ShadowDensity` 0.3).

## How it works

The shader (`TowerOfBabel/Buildings/Glass Step`) traces each pixel's view ray through the slab:

1. The ray is refracted into the glass (IOR 1.5) and Fresnel decides how much is reflected.
2. The slab frame (`_SlabCenter`, `_SlabHalfSize` from the completed mesh bounds, 0.3 m rounded corners) gives the distance to the nearest side wall, in metres, for any instance scale.
3. If the ray reaches the bottom face first, it shows the scene behind, shifted by the slab's refraction and tinted by Beer-Lambert absorption over the 0.2 m path.
4. If it hits a side wall first, it is trapped by internal reflection and travels along the slab. That pixel gets the long-path aqua colour (`_EdgeTint`, reached after `_EdgeLength` = 1 m) and a stronger distortion.
5. Side faces and bevels always use the long path, with bounce streaks between the top and bottom faces.

The scene behind comes from URP's opaque texture (enabled on both PC and Mobile assets). Premultiplied blending keeps other glass, such as the steps below, visible through the body. Colour fringes come from sampling red, green and blue at slightly different offsets (`_Dispersion`).

## Files

- Shader: `Assets/_Project/Shaders/BuildingGlassStep.shader`.
- Material: `Assets/_Project/Models/Buildings/FrutigerAero/Aero_Glass_Step_Tile.mat`. Tune `_TintColor`, `_EdgeTint`, `_EdgeLength`, `_BodyBlend`, `_Refraction`, `_Dispersion`, `_EdgeLines` and `_Reflection`.
- Prefab: only `Step_Tile.prefab`'s material changes. The GUID, all 11 construction stages, scale (1, 1, 0.35), box collider, Building settings and construction motion are unchanged. The old `Aero_WhiteMarble_Step_Tile.mat` remains as history.
- Construction: same cyan dissolve for incoming chunks. The blueprint is a faint glass outline; chunks read as glass shards.
- Authoring: **Tower of Babel → Buildings → Apply glass stair steps** (`GlassStepAuthoring.Apply()`) recreates the material, including the slab frame.
- Previews: `GlassStepPreview.Capture("flight" | "above" | "edge" | "stages")` writes to `_Screenshots-and-previews-to-delete/Buildings/GlassStep/`. The scene has floor stripes so the refraction is visible.

## Tests

`Assets/_Project/Tests/EditMode/GlassStepEditModeTests.cs` (7 tests) covers the prefab wiring, transparency and instancing, shader compilation with lighting variants, clear body vs. deep edge colours, the slab frame matching the mesh, unchanged geometry and collider, a red floor showing through almost unchanged, and edges rendering more tinted than the centre.

## Limits and open items

- Screen-space refraction cannot show things that are off screen or hidden behind the step. Refraction samples the opaque scene, so other transparent objects (including other glass steps) appear unrefracted through the blended part.
- In very close, very low-angle views, the traced edge image can draw small loop-shaped lines. The edge band and lines fade out at grazing angles to limit this.
- Planar reflection cameras may not have an opaque texture, so the glass in reflections can look less accurate.
- Steps do not write depth. Effects that read the depth texture treat them as see-through.
- Wet-weather beads are not applied to the glass yet.
- Checked in Edit Mode preview renders only. The steps have not been reviewed in the live tower, at night, or on mobile.
