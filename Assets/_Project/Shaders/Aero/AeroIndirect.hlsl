#ifndef AERO_INDIRECT_INCLUDED
#define AERO_INDIRECT_INCLUDED
#include "../AeroBuildingFinish.hlsl"
// Pooled tower renderers turn reflection probes off, so specular reflections use the
// shared Aero day/night capture when it exists and fall back to URP probes otherwise.
// Daylight is passed in: several existing shaders already declare _AeroDaylight.

half3 AeroSpecularIndirect(half3 normalWS, half3 view, half perceptualRoughness, float3 positionWS, float2 screenUV, float daylight)
{
    half mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
    half3 reflected;
    [branch] if (_AeroFinishEnabled > .5 && _AeroFinishEnvironmentReady > .5 && _AeroFinishCapturing < .5)
        reflected = AeroEnvironment(normalWS, view, mip, daylight);
    else
        reflected = GlossyEnvironmentReflection(reflect(-view, normalWS), positionWS, perceptualRoughness, 1, screenUV);
    // Captures can be briefly uninitialised (e.g. right after a reload); never let them poison metals.
    return any(!isfinite(reflected)) ? half3(0, 0, 0) : min(reflected, half3(16, 16, 16));
}

// Same split as URP GlobalIllumination(), with the Aero environment and an optional
// separate reflection normal (anisotropic bent normal, wet film normal).
half3 AeroGlobalIllumination(BRDFData brdfData, BRDFData brdfDataClearCoat, half clearCoatMask,
    half3 bakedGI, half occlusion, float3 positionWS, half3 normalWS, half3 reflectionNormalWS,
    half3 view, float2 screenUV, half3 specularTint, float daylight)
{
    half NoV = saturate(dot(normalWS, view));
    half fresnelTerm = Pow4(1 - NoV);
    half3 indirectDiffuse = bakedGI;
    half3 indirectSpecular = AeroSpecularIndirect(reflectionNormalWS, view, brdfData.perceptualRoughness, positionWS, screenUV, daylight) * specularTint;
    half3 color = EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);
    #if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    [branch] if (clearCoatMask > 0)
    {
        half3 coatSpecular = AeroSpecularIndirect(normalWS, view, brdfDataClearCoat.perceptualRoughness, positionWS, screenUV, daylight);
        half3 coatColor = EnvironmentBRDFClearCoat(brdfDataClearCoat, clearCoatMask, coatSpecular, fresnelTerm);
        half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * fresnelTerm;
        color = color * (1 - coatFresnel * clearCoatMask) + coatColor;
    }
    #endif
    return color * occlusion;
}
#endif
