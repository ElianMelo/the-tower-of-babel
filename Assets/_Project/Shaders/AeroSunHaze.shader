// Aerial perspective on the sun's side: distant ground, trees and tower walls looking towards a low sun fade into the
// same glow the sky draws (Aero/AeroSunGlow.hlsl), so far silhouettes melt into the halo as in a hazy golden hour.
// Away from the sun nothing changes; URP's own fog still handles the flat haze. Sky pixels are left to the sky shader.
Shader "Hidden/TowerOfBabel/Sun Haze"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Sun-side aerial perspective"
            ZWrite Off ZTest Always Cull Off
            // Premultiplied: rgb is the glow let in, alpha how much of the scene stays.
            Blend One SrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Haze
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #define USE_FULL_PRECISION_BLIT_TEXTURE
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Aero/AeroSunGlow.hlsl"

            half4 Haze(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float depth = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0).r;
                #if UNITY_REVERSED_Z
                    bool background = depth <= 0;
                #else
                    bool background = depth >= 1;
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 world = ComputeWorldSpacePosition(input.texcoord, depth, UNITY_MATRIX_I_VP);
                float3 offset = world - _WorldSpaceCameraPos;
                float distance = length(offset);
                float3 view = offset / max(distance, 1e-4);
                if (background)
                {
                    // Nothing wrote depth: the sky above the horizon (the sky shader draws its own glow), or open water
                    // below it, which the transparent sea leaves at the far plane. Haze the water at sea level.
                    float drop = _WorldSpaceCameraPos.y - _AeroSunHazeBase;
                    if (view.y >= -1e-3 || drop <= 0) return half4(0, 0, 0, 1);
                    distance = min(drop / -view.y, 20000);
                    world = _WorldSpaceCameraPos + view * distance;
                }
                float thinning = exp(-max(world.y - _AeroSunHazeBase, 0) * _AeroSunHaze.z);
                float haze = (1 - exp(-distance * _AeroSunHaze.x)) * thinning * _AeroSunHaze.y * _AeroSunHaze.w;
                // The sun itself never shows through ground; only its glow and halo veil it.
                float3 glow = AeroSunGlowRadiance(view, false);
                float veil = haze * AeroSunGlowMask(view);
                return half4(glow * haze, 1 - veil);
            }
            ENDHLSL
        }
    }
}
