# Dynamic ray Render Graph review

## 1. Validation summary

Reviewed the Unity 6000.3.11f1 PC Forward+ renderer, `AeroSunShaftRendererFeature` and `AeroSunShafts.shader`. The feature declares depth input, integrates into a graph-local half/quarter-resolution HDR texture and composites additively before post-processing. Render comparisons exercise the actual PC renderer. The gameplay bridge feeds a rendering-only contract.

## 2. Confirmed issues

The previous system disabled near and far architecture shadows, used fixed crossed sheets and depended on the sheet emitter to update fireflies. The old marcher had no tower volume bound and treated invalid shadow coordinates as illuminated. These are addressed by bounded real/proxy casters, native shadow scattering, independent fireflies and volume clipping.

During implementation, a repeatability check caught URP retaining a shadow variant and cascade state after switching to an empty caster set. The pass now records `CullingResults.GetShadowCasterBounds` and the shader honors both explicit caster availability and zero native shadow strength. The same view/time is reproducible after rotating the camera and after creating/removing a roof.

## 3. Likely issues or risky assumptions

Additional-light indices follow the installed URP `ForwardLights` packing: visible lights in order, excluding the main light. Direct `GetAdditionalPerObjectLight` avoids the fullscreen pass's lack of a mesh per-object light list. This mapping must be reviewed on URP upgrades. Main and additional shadow matrices/parameters are native pipeline constants; texture lifetimes are explicitly declared.

The two spotlights request 512 px in the shared native atlas; other lights can force downscaling. Large floor shadow changes upload combined geometry on the event-driven rebuild frame. The GPU benchmark is a controlled Editor fixture rather than a standalone full-game performance guarantee. No temporal history is used; very thin high-contrast occluders can retain some spatial integration noise at Low quality.

## 4. Recommended fixes

Implemented: explicit depth/main-shadow/additional-shadow dependencies; graph-local rays; bounded integration; no invalid-cascade illumination; zero-caster handling; half/quarter quality; static render delegates; explicit selected-light indices; pre-UI additive composite; safe empty/no-context camera rejection. Retained depth-aware filtering instead of a plain texture copy.

On future pipeline upgrades, rerun native fill-shadow and empty-caster repeatability comparisons before accepting changes to light index packing. Profile a standalone gameplay build when a release-level frame budget is required.

## 5. Corrected snippets

```csharp
d.sunHasCasters = frameData.Get<UniversalRenderingData>().cullResults
    .GetShadowCasterBounds(lights.mainLightIndex, out _) ? 1 : 0;
builder.UseTexture(d.depth, AccessFlags.Read);
builder.UseTexture(d.shadow, AccessFlags.Read);
if (d.additionalShadow.IsValid())
    builder.UseTexture(d.additionalShadow, AccessFlags.Read);
builder.SetRenderAttachment(rays, 0, AccessFlags.WriteAll);
```

Each static render function binds the textures and parameters carried by its own pass data before invoking `Blitter.BlitTexture`. Scattering and the composite both require additional depth/shadow resources and parameter binding; they are not copy-only passes. The composite reads the current color attachment through additive blending, with `AccessFlags.ReadWrite` and RGB-only output. No output is published globally, so no `SetGlobalTextureAfterPass`, global-state mutation or unnecessary extended resource lifetime is introduced.

```hlsl
if (_AeroSunHasCasters < .5 || GetMainLightShadowParams().x <= .0001)
    return 1;
// With actual casters, reject samples beyond valid cascade/atlas coverage.
```

## 6. Missing information

No standalone player GPU capture or long networked gameplay session is included. See `DynamicRays/Performance.json` for the available native GPU measurements and their availability flags, and `DynamicRays/Validation.json` for test failures outside the lighting scope. The 2 ms goal is verified only to the extent explicitly stated in that report.
