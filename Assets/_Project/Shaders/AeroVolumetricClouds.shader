Shader "Hidden/TowerOfBabel/Volumetric Clouds"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Aero/AeroClouds.hlsl"

        float FootprintDistance(float2 uv, float3 ray)
        {
            // Trace to the farthest surface under this low-res texel so sky texels next to geometry keep their
            // clouds; the composite masks geometry that stands in front of the layer at full resolution.
            float2 o = _AeroCloudDepthTexel.xy * max(0, _AeroCloudDepthTexel.z * .5 - .5);
            float far = 0;
            [unroll] for (int i = 0; i < 4; i++)
            {
                float2 offset = float2(i & 1 ? o.x : -o.x, i & 2 ? o.y : -o.y);
                float raw = SAMPLE_TEXTURE2D_X_LOD(_AeroCloudDepth, sampler_PointClamp, uv + offset, 0).r;
                far = max(far, AeroSceneDistance(raw, ray));
            }
            return far;
        }
        ENDHLSL

        Pass
        {
            Name "Aero cloud trace"
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Trace
            float4 Trace(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float3 ray = AeroCloudRay(uv);
                return AeroTraceClouds(uv, uv * _AeroCloudTexel.zw, FootprintDistance(uv, ray));
            }
            ENDHLSL
        }

        Pass
        {
            Name "Aero cloud temporal"
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Accumulate
            float4 Accumulate(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 current = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                if (_AeroCloudTemporal.x <= 0) return current;
                float4 low = current, high = current, mean = 0, square = 0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float4 s = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + float2(x, y) * _AeroCloudTexel.xy, 0);
                    mean += s; square += s * s;
                }
                mean /= 9; square /= 9;
                float4 sigma = sqrt(max(0, square - mean * mean));
                low = mean - sigma * 1.6; high = mean + sigma * 1.6;
                // Reproject the point where this pixel's ray meets the layer (clouds are far, so this is stable).
                float3 ray = AeroCloudRay(uv);
                float2 range = AeroCloudInterval(ray);
                float distance = range.x >= 0 ? max(range.x, 200) : 8000;
                float2 previousUV = AeroCloudPrevUV(_AeroCloudCamera.xyz + ray * distance);
                if (any(previousUV < 0) || any(previousUV > 1)) return current;
                float4 history = SAMPLE_TEXTURE2D_X_LOD(_AeroCloudHistory, sampler_LinearClamp, previousUV, 0);
                history = clamp(history, low, high);
                return lerp(current, history, _AeroCloudTemporal.x);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Aero cloud composite"
            ZWrite Off ZTest Always Cull Off
            // Colour: clouds over the scene by their transmittance. Alpha: left as it was.
            Blend One SrcAlpha, Zero One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Composite
            float4 Composite(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float3 ray = AeroCloudRay(uv);
                // Each pixel is composited once: before transparents, or after them (AeroCompositeAfterTransparents).
                if (AeroCompositeAfterTransparents(ray) != (_AeroCloudComposite.x > .5)) return float4(0, 0, 0, 1);
                float4 clouds = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
                float raw = SAMPLE_TEXTURE2D_X_LOD(_AeroCloudDepth, sampler_PointClamp, uv, 0).r;
                if (!AeroIsSky(raw))
                {
                    float2 range = AeroCloudInterval(ray);
                    float scene = AeroSceneDistance(raw, ray);
                    // Geometry in front of the layer hides it; soft over 150 m to avoid stair-steps.
                    float visible = range.x < 0 ? 0 : saturate((scene - range.x) / 150);
                    clouds = float4(clouds.rgb * visible, lerp(1, clouds.a, visible));
                }
                return clouds;
            }
            ENDHLSL
        }
    }
}
