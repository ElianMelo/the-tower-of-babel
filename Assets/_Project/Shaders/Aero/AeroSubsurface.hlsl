#ifndef AERO_SUBSURFACE_INCLUDED
#define AERO_SUBSURFACE_INCLUDED
// Cheap subsurface diffuse for leaves, skin and jelly plastic. Light wraps past the
// terminator per channel, so the shadow edge turns the scatter colour instead of grey.

// Curvature from screen derivatives: 1/radius, higher on small round shapes.
half AeroCurvature(half3 normalWS, float3 positionWS)
{
    return saturate(length(fwidth(normalWS)) / max(length(fwidth(positionWS)), 1e-4) * .05);
}

// Per-channel wrapped diffuse; wrap 0 is Lambert. Energy-normalized.
half3 AeroWrapDiffuse(half NdotL, half3 wrap)
{
    return saturate((NdotL + wrap) / ((1 + wrap) * (1 + wrap)));
}

// Diffuse response with a coloured terminator. scatter is the tint seen in the wrap
// region; wrap grows with curvature so small shapes look softer.
half3 AeroSubsurfaceDiffuse(half NdotL, half3 scatterColor, half wrap, half curvature)
{
    half3 perChannel = wrap * (.6 + curvature) * lerp(half3(.35, .35, .35), half3(1, 1, 1), scatterColor);
    half3 wrapped = AeroWrapDiffuse(NdotL, perChannel);
    half lambert = saturate(NdotL);
    // Tint only the light that the wrap added.
    return lambert + max(wrapped - lambert, 0) * scatterColor * 2;
}

// Soft shadows for scattering media: light still bleeds into fully shadowed texels.
half AeroSubsurfaceShadow(half shadowAttenuation, half softness)
{
    return lerp(shadowAttenuation, 1, softness * .5);
}
#endif
