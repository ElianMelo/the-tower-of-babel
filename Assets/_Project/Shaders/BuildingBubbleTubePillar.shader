Shader "TowerOfBabel/Buildings/Bubble Tube Pillar"
{
    // Opaque, GPU-instanced pillar: pearl base/cap, blue enamel band, chrome collars and a
    // glass tube whose water, spine and bubbles are traced in BuildingBubbleTube.hlsl.
    // UV1.x selects the region: (id + .5) / 8 with 0 pearl, 1 enamel, 2 chrome, 3 glow, 4 water.
    Properties
    {
        _MarbleMap("White marble reference", 2D) = "white" {}
        [Normal] _NormalMap("Marble relief normal map", 2D) = "bump" {}
        _NormalStrength("Marble relief strength", Range(0,2)) = .12
        _MarbleScale("Texture repeats per metre", Float) = .28
        _TextureAspect("Reference width / height", Float) = 1.5
        _VeinStrength("Gray vein strength", Range(0,1)) = .1
        _BaseColor("Pearl white", Color) = (.96,.975,.99,1)
        _EnamelColor("Blue enamel band", Color) = (.03,.36,.82,1)
        _ChromeColor("Chrome collars", Color) = (.88,.92,.97,1)
        _GlowColor("Aqua lamp glow", Color) = (.3,.9,1,1)
        _GlowStrength("Aqua lamp strength", Range(0,8)) = 2.5
        _Smoothness("Pearl polish", Range(0,1)) = .8
        _PearlCoat("Pearl clear coat", Range(0,1)) = .85
        _AlbedoCeiling("Bright-white ceiling", Range(.5,1)) = .94
        _WaterDeepColor("Deep water", Color) = (.04,.42,.72,1)
        _WaterLightColor("Lit water", Color) = (.45,.88,1,1)
        _SpineColor("Inner pearl spine", Color) = (.9,.95,1,1)
        _WaterDensity("Water density", Range(0,12)) = 3.2
        _GlassReflection("Tube glass reflection", Range(0,1)) = .8
        _BubbleAmountMin("Bubbles: fewest (per instance)", Range(0,1)) = .06
        _BubbleAmountMax("Bubbles: most (per instance)", Range(0,1)) = .45
        _BubbleBurstiness("Bubble streams vs calm spells (within an instance)", Range(0,1)) = .85
        _BigBubbleChance("Large wobbling bubbles", Range(0,1)) = .45
        _BubbleIridescence("Bubble rim and highlight strength", Range(0,2)) = 1.2
        _GlassDistortion("Hand-blown glass distortion", Range(0,3)) = 1
        _WaterMotionMin("Water motion: calmest (per instance)", Range(0,2)) = .15
        _WaterMotionMax("Water motion: liveliest (per instance)", Range(0,2)) = 1.25
        _InstanceVariation("Per-instance randomness", Range(0,1)) = 1
        _BubbleSpeed("Bubble rise speed (m/s)", Range(0,1)) = .22
        _WaterDetailDistance("Bubble and caustic distance", Float) = 85
        _MotionTime("Preview clock; -1 uses engine time", Float) = -1
        _PatternSeed("Model variation seed", Float) = 23.83
        [Toggle(_AERO_WETNESS)] _WetnessOn("Wetness (global dew and rain)", Float) = 1
        _Porosity("Stone porosity", Range(0,1)) = .12
        _WetExposure("Weather exposure", Range(0,1)) = 1
        _WetBeads("Visible drops", Range(0,1)) = 1
        [PerRendererData] _BuildMode("Incoming chunk dissolve", Float) = 0
        [PerRendererData] _BuildReveal("Construction reveal", Range(0,1)) = 1
        [PerRendererData] _BuildClock("Construction clock", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #define _CLEARCOAT 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "AeroBuildingFinish.hlsl"
        float _AeroDaylight;
        TEXTURE2D(_MarbleMap); SAMPLER(sampler_MarbleMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor,_EnamelColor,_ChromeColor,_GlowColor,_WaterDeepColor,_WaterLightColor,_SpineColor;
            half _NormalStrength,_VeinStrength,_GlowStrength,_Smoothness,_PearlCoat,_AlbedoCeiling,_GlassReflection;
            float _MarbleScale,_TextureAspect,_WaterDensity;
            float _BubbleAmountMin,_BubbleAmountMax,_WaterMotionMin,_WaterMotionMax,_InstanceVariation;
            float _BubbleSpeed,_WaterDetailDistance,_MotionTime,_PatternSeed;
            float _BubbleBurstiness,_BigBubbleChance,_BubbleIridescence,_GlassDistortion;
            half _WetnessOn,_Porosity,_WetExposure,_WetBeads,_Cull;
        CBUFFER_END
        UNITY_INSTANCING_BUFFER_START(Construction)
            UNITY_DEFINE_INSTANCED_PROP(float,_BuildMode)
            UNITY_DEFINE_INSTANCED_PROP(float,_BuildReveal)
            UNITY_DEFINE_INSTANCED_PROP(float,_BuildClock)
        UNITY_INSTANCING_BUFFER_END(Construction)
        #include "BuildingMarbleSurface.hlsl"
        #include "BuildingBubbleTube.hlsl"
        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
            float2 regionUV:TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            float3 positionOS:TEXCOORD2;
            nointerpolation float4 originSeed:TEXCOORD3;
            nointerpolation float region:TEXCOORD4;
            half fog:TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings o=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
            o.positionCS=p.positionCS;o.positionWS=p.positionWS;
            o.normalWS=TransformObjectToWorldNormal(input.normalOS);
            o.positionOS=input.positionOS.xyz;
            o.region=floor(input.regionUV.x*8);
            o.fog=ComputeFogFactor(p.positionCS.z);
            float3 origin=TransformObjectToWorld(float3(0,0,0));
            float3 q=frac(floor(origin*32+.5)*float3(.1031,.1030,.0973)+_PatternSeed*.137);
            q+=dot(q,q.yzx+33.33);
            o.originSeed=float4(origin,frac((q.x+q.y)*q.z));
            return o;
        }
        float BubbleTubeTime()
        {
            return _MotionTime<0?_Time.y:_MotionTime;
        }
        float ConstructionHash(float3 p)
        {
            p=frac(p*.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z);
        }
        float ConstructionNoise(float3 p)
        {
            float3 cell=floor(p),f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(lerp(ConstructionHash(cell),ConstructionHash(cell+float3(1,0,0)),f.x),
                             lerp(ConstructionHash(cell+float3(0,1,0)),ConstructionHash(cell+float3(1,1,0)),f.x),f.y),
                        lerp(lerp(ConstructionHash(cell+float3(0,0,1)),ConstructionHash(cell+float3(1,0,1)),f.x),
                             lerp(ConstructionHash(cell+float3(0,1,1)),ConstructionHash(cell+float3(1,1,1)),f.x),f.y),f.z);
        }
        // Same incoming-chunk dissolve as the Aquatic Finish; permanent pieces stay solid.
        half ClipConstruction(Varyings input)
        {
            AeroClipReflection(input.positionWS);
            float mode=UNITY_ACCESS_INSTANCED_PROP(Construction,_BuildMode);
            if (mode<.5) return 0;
            float reveal=UNITY_ACCESS_INSTANCED_PROP(Construction,_BuildReveal);
            float clock=UNITY_ACCESS_INSTANCED_PROP(Construction,_BuildClock);
            float noise=ConstructionNoise(input.positionOS*10.5+float3(5.2+clock*.2,13.8,2.1-clock*.15));
            float d=reveal*1.12-.06-noise; clip(d);
            return (1-smoothstep(0,.08,d))*step(.001,reveal)*step(reveal,.999);
        }
        ENDHLSL
        Pass
        {
            Name "BubbleTubeForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _AERO_WETNESS
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Aero/AeroWetness.hlsl"
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half buildEdge=ClipConstruction(input);
                half3 geometric=normalize(input.normalWS);
                half3 n=geometric;
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                float region=input.region;
                half water=step(3.5,region)*step(region,4.5);
                half enamel=step(.5,region)*step(region,1.5);
                half chrome=step(1.5,region)*step(region,2.5);
                half glow=step(2.5,region)*step(region,3.5);
                half pearl=saturate(1-water-enamel-chrome-glow);
                float3 marbleP=(input.positionWS-input.originSeed.xyz)*_MarbleScale
                    +float3(input.originSeed.w,frac(input.originSeed.w*3.71),frac(input.originSeed.w*7.13))*3;
                if(pearl>.5) n=MarbleNormal(marbleP,n);
                float4 shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                Light key=GetMainLight(shadowCoord);

                SurfaceData surface=(SurfaceData)0;
                surface.alpha=1;surface.occlusion=1;surface.normalTS=half3(0,0,1);surface.specular=half3(.04,.04,.04);
                half stone=lerp(1,Marble(marbleP,n),_VeinStrength);
                surface.albedo=_BaseColor.rgb*stone*pearl+_EnamelColor.rgb*enamel+_ChromeColor.rgb*chrome+_GlowColor.rgb*glow*.3;
                surface.metallic=chrome;
                surface.smoothness=AeroPolish(_Smoothness,input.positionWS,pearl)*pearl+.92*enamel+.94*chrome+.6*glow+.97*water;
                surface.clearCoatMask=_PearlCoat*pearl+enamel;surface.clearCoatSmoothness=.95;
                surface.emission=_GlowColor.rgb*_GlowStrength*glow+half3(.08,.65,1)*buildEdge*2.2;
                if(water>.5)
                {
                    // Opaque glass: the interior is emitted under a reflective dielectric coat.
                    float dist=distance(GetCameraPositionWS(),input.originSeed.xyz);
                    half detail=saturate((_WaterDetailDistance-dist)/max(_WaterDetailDistance*.25,1));
                    BubbleTubeInstance variation=BubbleTubeVariation(input.originSeed.w);
                    float3 dOS=normalize(TransformWorldToObjectDir(-view,false));
                    float3 nOS=normalize(TransformWorldToObjectDir(geometric,false));
                    float3 lightOS=normalize(TransformWorldToObjectDir(key.direction,false));
                    half3 ambient=SampleSH(geometric);
                    half3 lightColor=key.color*lerp(.35,1,key.shadowAttenuation);
                    half3 interior=BubbleTubeInterior(input.positionOS,dOS,nOS,variation,BubbleTubeTime(),detail,ambient,lightOS,lightColor);
                    half facing=saturate(dot(geometric,view));
                    half fresnel=.04+.96*pow(1-facing,5);
                    surface.albedo=0;surface.specular=half3(.04,.04,.04)*_GlassReflection;
                    surface.emission+=interior*(1-fresnel*_GlassReflection);
                    surface.emission+=_WaterLightColor.rgb*pow(1-facing,3)*.12;
                }
                surface.albedo=min(surface.albedo,_AlbedoCeiling.xxx);
                #if defined(_AERO_WETNESS)
                    if(water<.5) AeroApplyWetnessBeads(surface.albedo,surface.smoothness,n,input.positionWS,geometric,_Porosity,_WetExposure,_WetBeads);
                #endif
                InputData data=(InputData)0;
                data.positionWS=input.positionWS;data.positionCS=input.positionCS;
                data.normalWS=n;data.viewDirectionWS=view;
                data.shadowCoord=shadowCoord;data.bakedGI=SampleSH(n);data.shadowMask=half4(1,1,1,1);
                data.vertexLighting=VertexLighting(input.positionWS,n);
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                half4 color=UniversalFragmentPBR(data,surface);
                color.rgb=AeroFinishReflection(color.rgb,input.positionWS,n,view,0,3.2,_AeroDaylight);
                color.rgb+=AeroThinFilm(n,view,saturate(pearl*.5+water),_AeroDaylight);
                color.rgb=MixFog(color.rgb,input.fog);color.a=1;
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings o=Vert(input);
                float3 direction=_LightDirection;
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    direction=normalize(_LightPosition-o.positionWS);
                #endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,normalize(o.normalWS),direction));
                #if UNITY_REVERSED_Z
                    o.positionCS.z=min(o.positionCS.z,o.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z=max(o.positionCS.z,o.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 DepthFrag(Varyings input):SV_Target { UNITY_SETUP_INSTANCE_ID(input);ClipConstruction(input);return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(Varyings input):SV_Target { UNITY_SETUP_INSTANCE_ID(input);ClipConstruction(input);return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFrag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipConstruction(input);
                float3 n=normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct=PackNormalOctQuadEncode(n)*.5+.5;
                    return half4(PackFloat2To888(saturate(oct)),0);
                #else
                    return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
