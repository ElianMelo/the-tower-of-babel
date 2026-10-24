Shader "Hidden/TowerOfBabel/Sun Shafts"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #define USE_FULL_PRECISION_BLIT_TEXTURE
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float4 _AeroShaftParams, _AeroRaysTexelSize;
        half4 _AeroSunTint;
        float _AeroShaftClock;
        TEXTURE2D_SHADOW(_AeroSunShadow); SAMPLER_CMP(sampler_AeroSunShadow);
        TEXTURE2D_X_FLOAT(_AeroFullDepth);
        float3 WorldPosition(float2 uv, float depth)
        {
            #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
            #endif
            return ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
        }
        ENDHLSL
        Pass
        {
            Name "Shadow-shaped sun shafts"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Scatter
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            half4 Scatter(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_PointClamp,input.texcoord,0).r;
                float3 position = WorldPosition(input.texcoord,rawDepth);
                float3 origin = _WorldSpaceCameraPos;
                #if UNITY_REVERSED_Z
                    if (unity_OrthoParams.w > .5) origin = WorldPosition(input.texcoord,1);
                #else
                    if (unity_OrthoParams.w > .5) origin = WorldPosition(input.texcoord,0);
                #endif
                float3 delta = position-origin;
                float travel = min(length(delta),_AeroShaftParams.x);
                float3 direction = normalize(delta);
                int count = (int)_AeroShaftParams.w;
                float stepLength = travel/count;
                // Spatially stable dither breaks shadow-step bands without a history buffer.
                float jitter = frac(52.9829189*frac(dot(input.positionCS.xy,float2(.06711056,.00583715))));
                float clock = _AeroShaftClock >= 0 ? _AeroShaftClock : _Time.y;
                half energy = 0;
                [loop] for (int i=0;i<count;i++)
                {
                    float along = (i+jitter)*stepLength;
                    float3 samplePosition = origin+direction*along;
                    float4 shadow = TransformWorldToShadowCoord(samplePosition);
                    half visibility = 1;
                    if (all(shadow.xyz >= 0) && all(shadow.xyz <= 1))
                        visibility = SAMPLE_TEXTURE2D_SHADOW(_AeroSunShadow,sampler_AeroSunShadow,shadow.xyz);
                    float mist = .85+.15*sin(dot(samplePosition,float3(.37,.19,.29))+clock*.12);
                    energy += visibility*mist*exp(-along*_AeroShaftParams.y)*_AeroShaftParams.y*stepLength;
                }
                Light sun = GetMainLight();
                // Side-lit interiors must show shafts even when the player isn't staring at the sun.
                half phase = .45+.75*pow(saturate(dot(direction,sun.direction)),4);
                half3 color = min(sun.color,half3(3,3,3))*_AeroSunTint.rgb*energy*phase*_AeroShaftParams.z;
                // Preserve blue sky beyond side openings instead of turning it into a white fog wall.
                #if UNITY_REVERSED_Z
                    if(rawDepth < .000001) color *= .15;
                #else
                    if(rawDepth > .999999) color *= .15;
                #endif
                return half4(color,min(LinearEyeDepth(rawDepth,_ZBufferParams),60000));
            }
            ENDHLSL
        }
        Pass
        {
            Name "Depth-aware sun composite"
            ZWrite Off ZTest Always Cull Off
            Blend One One ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Composite
            half4 Composite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float eye = LinearEyeDepth(SAMPLE_TEXTURE2D_X_LOD(_AeroFullDepth,sampler_PointClamp,input.texcoord,0).r,_ZBufferParams);
                float2 grid = input.texcoord*_AeroRaysTexelSize.zw-.5;
                float2 baseUV = (floor(grid)+.5)*_AeroRaysTexelSize.xy;
                half3 total = 0; float weight = 0;
                [unroll] for (int y=-1;y<=1;y++) [unroll] for (int x=-1;x<=1;x++)
                {
                    half4 ray = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_PointClamp,baseUV+float2(x,y)*_AeroRaysTexelSize.xy,0);
                    float2 offset = float2(x,y)-frac(grid);
                    float spatial = exp2(-dot(offset,offset)*.7);
                    float w = spatial*exp2(-abs(ray.a-eye)*4/max(1,eye*.05));
                    total += ray.rgb*w; weight += w;
                }
                return half4(total/max(weight,.0001),0);
            }
            ENDHLSL
        }
    }
}
