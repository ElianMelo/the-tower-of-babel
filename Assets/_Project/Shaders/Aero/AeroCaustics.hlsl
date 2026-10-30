#ifndef AERO_CAUSTICS_INCLUDED
#define AERO_CAUSTICS_INCLUDED
// Caustic sampling after the open-source Crest Ocean System (MIT, Wave Harmonic and
// contributors; see Environment/Lighting/Caustics/THIRD_PARTY_NOTICE.md). Two layers at
// scales 1 and 1.37 scroll in unrelated directions and are averaged, so the chromatic net
// keeps moving without the sparse "min of two layers" look. Values are centred on the
// texture's grey point (0.07 for CrestCaustics.png) like Crest's light modulation.

#define AERO_CAUSTICS_GREY .07

// tileMetres: world size of one texture tile (Crest default 5 m). lod: explicit mip so
// distant or out-of-focus caustics blur instead of stretching.
half3 AeroCrestCaustics(TEXTURE2D_PARAM(tex, samp), float2 positionXZ, float time, float tileMetres, float lod)
{
    float2 p = positionXZ / max(tileMetres, .01);
    float2 uv1 = p + float2(.044 * time + 17.16, -.169 * time);
    float2 uv2 = p * 1.37 + float2(.248 * time, .117 * time);
    return .5 * SAMPLE_TEXTURE2D_LOD(tex, samp, uv1, lod).rgb + .5 * SAMPLE_TEXTURE2D_LOD(tex, samp, uv2, lod).rgb;
}

// Crest's focus: sharpest at focalDepth below the surface, blurring with distance from it
// and with view distance (log2 keeps far tiles from shimmering).
float AeroCausticLod(float depthBelowSurface, float viewDistance, float focalDepth, float depthOfField, float blur)
{
    return max(0, log2(max(viewDistance, 1)) * .5 + abs(depthBelowSurface - focalDepth) / max(depthOfField, .01) + blur);
}

// Light-direction projection with Crest's 1/4 fudge: caustics shift with the sun without
// stretching on angled surfaces.
float2 AeroCausticShift(float3 lightDirection, float depthBelowSurface)
{
    return lightDirection.xz * depthBelowSurface / (4 * max(lightDirection.y, .2));
}

// Brightness above the grey point, 0 where the net is dark; 1 on bright filaments.
half AeroCausticBrightness(half3 caustic)
{
    return saturate((dot(caustic, half3(.2126, .7152, .0722)) - AERO_CAUSTICS_GREY) * 2.4);
}
#endif
