#ifndef AERO_LIGHT_LOOP_INCLUDED
#define AERO_LIGHT_LOOP_INCLUDED
// Shared Forward/Forward+ additional-light loop. The including shader must define
//   struct AeroPixel { ... };
//   half3 AeroShadeLight(Light light, AeroPixel px);
// before including this file. Mirrors UniversalFragmentPBR's loop in URP 17.3.

half3 AeroAdditionalLights(InputData inputData, half4 shadowMask, AmbientOcclusionFactor aoFactor, AeroPixel px)
{
    half3 sum = 0;
    #if defined(_ADDITIONAL_LIGHTS)
    uint meshRenderingLayers = GetMeshRenderingLayer();
    uint pixelLightCount = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
            sum += AeroShadeLight(light, px);
    }
    #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
            sum += AeroShadeLight(light, px);
    LIGHT_LOOP_END
    #endif
    return sum;
}
#endif
