// Outdoor light shafts: separate beams of sunlight in the air near the camera, the way light falls through hazy air
// at golden hour. Each pixel marches its view ray through world space up to the shaft range. Along the way, a
// drifting noise stretched along the sunlight makes thin beams with gaps between them, and the sun's shadow map
// (tower, trees, hills) cuts them. Lit mostly forwards, the shafts are strong looking towards the sun and faint from
// the side. The march runs at reduced resolution with an ordered dither that the depth-aware composite resolves
// (the same scheme as the tower's interior Sun Shafts). Driven by AeroSunShaftRendererFeature and AeroSunGlow.
Shader "Hidden/TowerOfBabel/Sun Rays"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #define USE_FULL_PRECISION_BLIT_TEXTURE
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Aero/AeroSunGlow.hlsl"
        float4 _AeroRaysParams;   // x strength, y gaps, z 1/width, w 1/length (per metre)
        float4 _AeroRaysVolume;   // x range (m), y height falloff (per m), z drift (m/s), w haze base height (m)
        float4 _AeroRaysMarch;    // x steps, y clock (seconds), z sun has shadow casters, w unused
        float4 _AeroRaysTexelSize;
        TEXTURE2D_SHADOW(_AeroSunShadow); SAMPLER_CMP(sampler_AeroSunShadow);
        TEXTURE2D_X_FLOAT(_AeroFullDepth);

        float3 WorldPosition(float2 uv, float depth)
        {
            #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
            #endif
            return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
        }

        // Depth-aware upsample of the reduced-resolution shafts: a sliding four-texel box (weights 1-f, 1, 1, 1, f)
        // gives every class of the scatter's 4x4 ordered dither the same weight, so it resolves without noise.
        half4 Composite(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float eye = LinearEyeDepth(SAMPLE_TEXTURE2D_X_LOD(_AeroFullDepth, sampler_PointClamp, input.texcoord, 0).r, _ZBufferParams);
            float2 grid = input.texcoord * _AeroRaysTexelSize.zw - .5;
            float2 baseUV = (floor(grid) + .5) * _AeroRaysTexelSize.xy;
            float2 f = frac(grid);
            half3 total = 0; float weight = 0;
            [unroll] for (int y = -2; y <= 2; y++) [unroll] for (int x = -2; x <= 2; x++)
            {
                half4 ray = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, baseUV + float2(x, y) * _AeroRaysTexelSize.xy, 0);
                float spatial = (x == -2 ? 1 - f.x : x == 2 ? f.x : 1) * (y == -2 ? 1 - f.y : y == 2 ? f.y : 1);
                float w = spatial * exp2(-abs(ray.a - eye) * 4 / max(1, eye * .05)) + 1e-5;
                total += ray.rgb * w; weight += w;
            }
            return half4(total / max(weight, .0001), 0);
        }
        ENDHLSL
        Pass
        {
            Name "Outdoor light shaft scatter"
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Scatter
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            float Hash(float3 p) { p = frac(p * .1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
            float Noise(float3 p)
            {
                float3 cell = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(cell), Hash(cell + float3(1, 0, 0)), f.x), lerp(Hash(cell + float3(0, 1, 0)), Hash(cell + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash(cell + float3(0, 0, 1)), Hash(cell + float3(1, 0, 1)), f.x), lerp(Hash(cell + float3(0, 1, 1)), Hash(cell + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            // Thin beams along the sunlight: noise across the light is fine (shaft width), along it long (shaft length).
            float Beams(float3 p, float3 sun, float3 u, float3 v, float clock)
            {
                float2 across = float2(dot(p, u), dot(p, v)) + clock * _AeroRaysVolume.z * float2(1, .37);
                float3 q = float3(across * _AeroRaysParams.z, dot(p, sun) * _AeroRaysParams.w);
                float n = Noise(q) * .65 + Noise(q * float3(2.03, 2.03, 1.7) + 11.3) * .35;
                return smoothstep(_AeroRaysParams.y, min(1, _AeroRaysParams.y + .22), n);
            }

            half SunVisibility(float3 position)
            {
                if (_AeroRaysMarch.z < .5 || GetMainLightShadowParams().x <= .0001) return 1;
                #if !defined(_MAIN_LIGHT_SHADOWS) && !defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    return 1;
                #endif
                #if defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    // Past the last cascade there is no shadow information: open air is lit.
                    if (ComputeCascadeIndex(position) >= 4) return 1;
                #endif
                float3 shadow = TransformWorldToShadowCoord(position).xyz;
                if (any(shadow < 0) || any(shadow > 1)) return 1;
                return SAMPLE_TEXTURE2D_SHADOW(_AeroSunShadow, sampler_AeroSunShadow, shadow);
            }

            half4 Scatter(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0).r;
                float3 position = WorldPosition(input.texcoord, rawDepth);
                float3 origin = _WorldSpaceCameraPos;
                float3 delta = position - origin;
                float distance = length(delta);
                float3 direction = delta / max(distance, .0001);
                float travel = min(distance, _AeroRaysVolume.x);
                float eye = min(LinearEyeDepth(rawDepth, _ZBufferParams), 60000);
                int count = (int)_AeroRaysMarch.x;
                float stepLength = travel / max(count, 1);
                static const float bayer[16] = {0,8,2,10, 12,4,14,6, 3,11,1,9, 15,7,13,5};
                uint2 cell = uint2(input.positionCS.xy) & 3;
                float jitter = (bayer[cell.y * 4 + cell.x] + .5) / 16;

                float3 sun = _AeroSunGlowDir.xyz;
                float3 u = normalize(cross(sun, abs(sun.y) < .99 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 v = cross(sun, u);
                float energy = 0;
                [loop] for (int i = 0; i < count; i++)
                {
                    float along = (i + jitter) * stepLength;
                    float3 p = origin + direction * along;
                    float thinning = exp(-max(p.y - _AeroRaysVolume.w, 0) * _AeroRaysVolume.y);
                    // Fade in from the camera so no beam slices through the lens.
                    float nearFade = saturate(along / 8);
                    energy += Beams(p, sun, u, v, _AeroRaysMarch.y) * thinning * nearFade * SunVisibility(p) * stepLength;
                }
                // Mostly forward scattering: strong looking towards the sun, a faint trace from the side.
                float facing = saturate(.5 + .5 * dot(direction, sun));
                float phase = .05 + pow(facing, 6);
                Light light = GetMainLight();
                half3 color = min(light.color, 3) * (energy * .03 * phase * _AeroRaysParams.x);
                // Soft shoulder: a long lit path straight at the sun settles instead of burning out.
                color /= 1 + 1.25 * max(color.r, max(color.g, color.b));
                return half4(color, eye);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Outdoor light shaft composite"
            ZWrite Off ZTest Always Cull Off
            Blend One One ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
        Pass
        {
            // Review and tests: the shafts alone.
            Name "Isolated outdoor light shaft composite"
            ZWrite Off ZTest Always Cull Off
            Blend Off ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
    }
}
