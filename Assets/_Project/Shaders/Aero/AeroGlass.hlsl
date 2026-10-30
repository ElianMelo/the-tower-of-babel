#ifndef AERO_GLASS_INCLUDED
#define AERO_GLASS_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
// Screen-space glass helpers built on the camera opaque texture. The depth guard is the
// pattern from BuildingAquaticFinish's GlassBorders pass: never bend a foreground
// silhouette into a surface behind the glass.

half AeroDielectricF0(half ior)
{
    half f0 = (ior - 1) / (ior + 1);
    return f0 * f0;
}

half AeroFresnel(half cosTheta, half ior)
{
    half f0 = AeroDielectricF0(ior);
    return f0 + (1 - f0) * pow(1 - saturate(cosTheta), 5);
}

float AeroSceneEyeDepth(float2 uv)
{
    float raw = SampleSceneDepth(uv);
    return IsPerspectiveProjection() ? LinearEyeDepth(raw, _ZBufferParams) : LinearDepthToEyeDepth(raw);
}

// Screen UV of the background seen through a slab of thickness metres.
float2 AeroRefractUV(float3 positionWS, half3 normalWS, half3 view, half ior, float thickness, half strength, float2 screenUV)
{
    float3 ray = refract(-view, normalWS, rcp(max(ior, 1.0001)));
    float depth = -TransformWorldToView(positionWS).z;
    half facing = max(saturate(dot(normalWS, view)), .2);
    float path = thickness / facing;
    float2 offset = TransformWorldToViewDir(ray + view, false).xy * strength * path / max(depth, .5);
    offset = clamp(offset, -.06, .06);
    float edge = saturate(min(min(screenUV.x, screenUV.y), min(1 - screenUV.x, 1 - screenUV.y)) * 20);
    float2 bent = saturate(screenUV + offset * edge);
    return AeroSceneEyeDepth(bent) > depth + .015 ? bent : screenUV;
}

// The opaque texture has no mips; frosted glass blurs with a small rotated Poisson disc.
half3 AeroSampleBlurred(float2 uv, half radius)
{
    static const float2 taps[6] = { float2(0, 0), float2(.53, .85), float2(-.92, .4), float2(-.36, -.93), float2(.88, -.47), float2(.1, .99) };
    float aspect = _ScreenParams.y / _ScreenParams.x;
    int count = radius < 1e-4 ? 1 : 6;
    half3 sum = 0;
    [loop] for (int i = 0; i < count; i++)
        sum += SampleSceneColor(saturate(uv + taps[i] * float2(aspect, 1) * radius));
    return sum / count;
}

// Chromatic dispersion: three refracted lookups at slightly different strengths.
half3 AeroSampleDispersed(float3 positionWS, half3 normalWS, half3 view, half ior, float thickness, half strength,
    half dispersion, half blur, float2 screenUV)
{
    half3 c = AeroSampleBlurred(AeroRefractUV(positionWS, normalWS, view, ior, thickness, strength, screenUV), blur);
    [branch] if (dispersion >= 1e-3)
    {
        c.r = AeroSampleBlurred(AeroRefractUV(positionWS, normalWS, view, ior, thickness, strength * (1 - dispersion), screenUV), blur).r;
        c.b = AeroSampleBlurred(AeroRefractUV(positionWS, normalWS, view, ior, thickness, strength * (1 + dispersion), screenUV), blur).b;
    }
    return c;
}

// Beer-Lambert body colour. tint is the colour after one metre-equivalent "depth"
// of glass; longer paths (thick edges, grazing views) saturate toward it.
half3 AeroBodyTint(half3 tint, float pathLength, half density)
{
    return exp(-max(1 - tint, 0) * pathLength * density);
}

// White-cyan Fresnel rim (#7FD8F0 by default) that reads clear glass silhouettes.
half3 AeroGlassRim(half3 normalWS, half3 view, half3 rimColor, half power, half strength)
{
    return rimColor * pow(1 - saturate(abs(dot(normalWS, view))), power) * strength;
}
#endif
