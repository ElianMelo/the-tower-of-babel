#ifndef AERO_ANISOTROPY_INCLUDED
#define AERO_ANISOTROPY_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/BSDF.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
// Brushed steel, rollers and trims. Anisotropy > 0 stretches highlights across
// the tangent (brushing along the bitangent); < 0 the opposite.

// mode 0: mesh tangent; 1: circular brushing around an axis (discs, rollers' ends);
// 2: linear brushing along an axis (doors, panels, roller barrels).
half3 AeroAnisoTangent(int mode, half3 normalWS, half4 meshTangentWS, float3 positionWS, float3 originWS, half3 axisWS)
{
    half3 t;
    if (mode == 1)
        t = cross(axisWS, positionWS - originWS);
    else if (mode == 2)
        t = axisWS;
    else
        t = meshTangentWS.xyz;
    t = t - normalWS * dot(normalWS, t);
    half lengthSq = dot(t, t);
    // Degenerate at a disc centre or along the axis: fall back to any perpendicular.
    if (lengthSq < 1e-6) t = abs(normalWS.y) < .99 ? cross(normalWS, half3(0, 1, 0)) : cross(normalWS, half3(1, 0, 0));
    return normalize(t);
}

// Direct specular term (already multiplied by NdotL), excluding light colour.
// Burley anisotropic GGX with the height-correlated Smith visibility, evaluated in
// full float: very thin lobes underflow in half precision and turn metals black.
half3 AeroAnisoSpecular(half3 n, half3 t, half3 b, half3 v, half3 l, half perceptualRoughness, half anisotropy, half3 f0)
{
    float3 N = n, T = t, B = b, V = v, L = l;
    float NdotL = saturate(dot(N, L));
    float NdotV = max(abs(dot(N, V)), 1e-4);
    float3 H = SafeNormalize(L + V);
    float NdotH = saturate(dot(N, H));
    float LdotH = saturate(dot(L, H));
    float roughness = max(PerceptualRoughnessToRoughness(max(perceptualRoughness, .045)), .002);
    float at = max(roughness * (1 + anisotropy), .004);
    float ab = max(roughness * (1 - anisotropy), .004);
    float3 d3 = float3(ab * dot(T, H), at * dot(B, H), at * ab * NdotH);
    float w2 = at * ab / max(dot(d3, d3), 1e-12);
    float D = at * ab * w2 * w2 * INV_PI;
    float lambdaV = NdotL * length(float3(at * dot(T, V), ab * dot(B, V), NdotV));
    float lambdaL = NdotV * length(float3(at * dot(T, L), ab * dot(B, L), NdotL));
    float Vis = .5 / max(lambdaV + lambdaL, 1e-6);
    float3 fresnel = F_Schlick((float3)f0, LdotH);
    return (half3)min(fresnel * D * Vis * NdotL, 64.0);
}

// Bent normal for environment lookups so reflections streak along the grain.
half3 AeroAnisoReflectionNormal(half3 n, half3 t, half3 b, half3 v, half perceptualRoughness, half anisotropy)
{
    half3 grain = anisotropy >= 0 ? b : t;
    half stretch = abs(anisotropy) * saturate(1.5 * sqrt(perceptualRoughness));
    return GetAnisotropicModifiedNormal(grain, n, v, stretch);
}

// Secondary Kajiya-Kay lobe for polished trims: a soft sheen line along the grain.
half AeroKajiyaKay(half3 t, half3 v, half3 l, half exponent)
{
    half3 h = SafeNormalize(l + v);
    half th = dot(t, h);
    return pow(sqrt(saturate(1 - th * th)), exponent);
}
#endif
