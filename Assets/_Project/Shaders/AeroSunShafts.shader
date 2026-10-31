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
        float _AeroShaftClock, _AeroSunHasCasters;
        float4 _AeroTowerVolume0, _AeroTowerVolume1, _AeroTowerBlend;
        float4 _AeroNoise, _AeroNoiseVelocity, _AeroFillIndices;
        // Fill scattering is capped at its ~3 m value: near the arch the 1/d^2 falloff otherwise
        // saturates into blinding orbs (user feedback 2026-10-31: interior too bright and dramatic).
        #define AERO_FILL_SCATTER_CAP .11
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
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            float Hash(float3 p) { p = frac(p * .1031); p += dot(p, p.yzx + 33.33); return frac((p.x+p.y)*p.z); }
            float Noise(float3 p)
            {
                float3 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(cell),Hash(cell+float3(1,0,0)),f.x),lerp(Hash(cell+float3(0,1,0)),Hash(cell+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash(cell+float3(0,0,1)),Hash(cell+float3(1,0,1)),f.x),lerp(Hash(cell+float3(0,1,1)),Hash(cell+float3(1,1,1)),f.x),f.y),f.z);
            }
            float2 CylinderInterval(float3 origin, float3 direction, float4 volume)
            {
                float2 offset=origin.xz-volume.xy;
                float a=dot(direction.xz,direction.xz), b=dot(offset,direction.xz), c=dot(offset,offset)-volume.z*volume.z;
                float discriminant=b*b-a*c;
                if(discriminant<0) return float2(1,0);
                float2 radial=a<.000001 ? (c<=0 ? float2(-1e6,1e6):float2(1,0)) : float2(-b-sqrt(max(0,discriminant)),-b+sqrt(max(0,discriminant)))/a;
                float2 vertical;
                if(abs(direction.y)<.000001) vertical=origin.y>=volume.w && origin.y<=volume.w+_AeroTowerBlend.x ? float2(-1e6,1e6):float2(1,0);
                else {vertical=(float2(volume.w,volume.w+_AeroTowerBlend.x)-origin.y)/direction.y;vertical=float2(min(vertical.x,vertical.y),max(vertical.x,vertical.y));}
                return float2(max(radial.x,vertical.x),min(radial.y,vertical.y));
            }
            float Interior(float3 p, float4 volume)
            {
                float edge=min(volume.z-length(p.xz-volume.xy),min(p.y-volume.w,volume.w+_AeroTowerBlend.x-p.y));
                return smoothstep(0,max(.05,_AeroTowerBlend.w),edge);
            }
            half SunVisibility(float3 position)
            {
                // URP can keep shadow variants enabled with an empty atlas after stripping
                // shadow-off variants. Its zero-strength contract must override stale cascades.
                if(_AeroSunHasCasters<.5 || GetMainLightShadowParams().x<=.0001) return 1;
                #if !defined(_MAIN_LIGHT_SHADOWS) && !defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    return 1;
                #endif
                #if defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    if(ComputeCascadeIndex(position)>=4) return 0;
                #endif
                float3 shadow=TransformWorldToShadowCoord(position).xyz;
                if(any(shadow<0)||any(shadow>1)) return 0;
                float2 offset=_MainLightShadowmapSize.xy*_AeroNoise.w;
                return .25*(SAMPLE_TEXTURE2D_SHADOW(_AeroSunShadow,sampler_AeroSunShadow,shadow+float3(offset,0))+
                    SAMPLE_TEXTURE2D_SHADOW(_AeroSunShadow,sampler_AeroSunShadow,shadow+float3(-offset,0))+
                    SAMPLE_TEXTURE2D_SHADOW(_AeroSunShadow,sampler_AeroSunShadow,shadow+float3(offset.x,-offset.y,0))+
                    SAMPLE_TEXTURE2D_SHADOW(_AeroSunShadow,sampler_AeroSunShadow,shadow+float3(-offset.x,offset.y,0)));
            }
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
                float3 direction = delta/max(length(delta),.0001);
                float2 current=CylinderInterval(origin,direction,_AeroTowerVolume0);
                float2 previous=CylinderInterval(origin,direction,_AeroTowerVolume1);
                float start=max(0,min(current.x,previous.x)), end=min(travel,max(current.y,previous.y));
                if(end<=start) return half4(0,0,0,min(LinearEyeDepth(rawDepth,_ZBufferParams),60000));
                int count = (int)_AeroShaftParams.w;
                float stepLength = max(0,end-start)/count;
                // Spatially stable dither breaks shadow-step bands without a history buffer.
                float jitter = frac(52.9829189*frac(dot(input.positionCS.xy,float2(.06711056,.00583715))));
                float clock = _AeroShaftClock >= 0 ? _AeroShaftClock : _Time.y;
                float3 energy = 0;
                Light sun = GetMainLight();
                half phase = .35+.75*pow(saturate(dot(direction,sun.direction)),4);
                [loop] for (int i=0;i<count;i++)
                {
                    float along = start+(i+jitter)*stepLength;
                    float3 samplePosition = origin+direction*along;
                    float mask=lerp(Interior(samplePosition,_AeroTowerVolume1),Interior(samplePosition,_AeroTowerVolume0),_AeroTowerBlend.y);
                    float3 advected=(samplePosition-clock*_AeroNoiseVelocity.xyz)*_AeroNoise.x;
                    float mist=lerp(1,smoothstep(.15,.85,Noise(advected)*.7+Noise(advected*2.13+7.1)*.3)*1.7,_AeroNoise.y);
                    float opening=pow(saturate(length(samplePosition.xz-_AeroTowerVolume0.xy)/max(1,_AeroTowerVolume0.z)),4);
                    float3 illumination=min(sun.color,3)*SunVisibility(samplePosition)*phase;
                    [unroll] for(int slot=0;slot<2;slot++)
                    {
                        int index=(int)_AeroFillIndices[slot];
                        if(index<0) continue;
                        Light fill=GetAdditionalPerObjectLight(index,samplePosition);
                        if(fill.distanceAttenuation>.00001)
                        {
                            half visibility=AdditionalLightRealtimeShadow(index,samplePosition,fill.direction);
                            illumination+=min(fill.color,3)*min(fill.distanceAttenuation,AERO_FILL_SCATTER_CAP)*visibility*(.4+.6*pow(saturate(dot(direction,fill.direction)),3));
                        }
                    }
                    illumination+=half3(.025,.04,.05)*_AeroNoise.z;
                    energy+=illumination*mask*mist*lerp(.4,1,opening)*exp(-along*_AeroShaftParams.y)*_AeroShaftParams.y*stepLength;
                }
                half3 color = clamp(energy*_AeroSunTint.rgb*_AeroShaftParams.z*_AeroTowerBlend.z,0,4);
                // The cylinder interval already clips sky-facing rays at the arch boundary.
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
