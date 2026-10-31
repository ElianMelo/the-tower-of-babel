Shader "TowerOfBabel/Buildings/Aurora Arch"
{
    // Opaque, GPU-instanced glossy white arch with an animated aurora curtain on its front and back
    // faces (BuildingAuroraCurtain.hlsl). The curtain follows the angle around the tower, so it runs
    // continuously through every arch on a floor.
    // UV1.x selects the region: (id + .5) / 8 with 0 glossy white, 1 aurora face, 3 blueprint glow.
    Properties
    {
        _BaseColor("Glossy white", Color) = (.965,.975,.99,1)
        _Smoothness("Polish", Range(0,1)) = .9
        _ClearCoat("Clear coat", Range(0,1)) = 1
        _AlbedoCeiling("Bright-white ceiling", Range(.5,1)) = .95
        _GlowColor("Blueprint glow", Color) = (.3,.9,1,1)
        _GlowStrength("Blueprint glow strength", Range(0,8)) = 2.5
        [HDR] _AuroraLowColor("Aurora base (green)", Color) = (.22,1,.42,1)
        [HDR] _AuroraMidColor("Aurora body (teal)", Color) = (.05,.85,.82,1)
        [HDR] _AuroraHighColor("Aurora upper (blue)", Color) = (.2,.36,1,1)
        [HDR] _AuroraFringeColor("Aurora top and pink tinges (violet-pink)", Color) = (.9,.32,.95,1)
        _AuroraStrength("Aurora brightness", Range(0,12)) = 1.3
        _AuroraNightBoost("Extra brightness at night", Range(1,4)) = 1.25
        _AuroraTint("Aurora colour in the white glaze (daylight)", Range(0,1)) = .6
        _AuroraBase("Lower edge height (m, arch object space)", Range(1.3,2)) = 1.7
        _AuroraEdgeWander("Lower edge waviness (m)", Range(0,.3)) = .12
        _AuroraRayDensity("Ray density", Range(.25,3)) = 1
        _AuroraRayDrift("Ray drift along the curtain (m/s)", Range(0,1)) = .16
        _AuroraFlicker("Ray flicker rate", Range(0,4)) = 1.1
        _AuroraFolds("Curtain folds", Range(0,2)) = 1
        _AuroraSurge("Travelling surges", Range(0,2)) = 1
        _AuroraCoverage("Share of the wall the curtain covers", Range(0,1)) = .3
        _AuroraHeight("Curtain size (1 = full upper wall)", Range(.2,1)) = .5
        _AuroraActivityMin("Calmest spell", Range(0,2)) = .45
        _AuroraActivityMax("Most active spell", Range(0,3)) = 1.35
        _AuroraSpeed("Overall speed", Range(0,4)) = .5
        _TowerCenter("Tower centre (world XZ)", Vector) = (0,0,0,0)
        _FloorHeight("Floor height (m)", Float) = 6
        _MotionTime("Preview clock; -1 uses engine time", Float) = -1
        [Toggle(_AERO_WETNESS)] _WetnessOn("Wetness (global dew and rain)", Float) = 1
        _Porosity("Glaze porosity", Range(0,1)) = .08
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
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor,_GlowColor,_AuroraLowColor,_AuroraMidColor,_AuroraHighColor,_AuroraFringeColor;
            half _Smoothness,_ClearCoat,_AlbedoCeiling,_GlowStrength,_AuroraStrength,_AuroraNightBoost,_AuroraTint;
            float _AuroraBase,_AuroraEdgeWander,_AuroraRayDensity,_AuroraRayDrift,_AuroraFlicker,_AuroraFolds,_AuroraSurge;
            float _AuroraActivityMin,_AuroraActivityMax,_AuroraSpeed,_AuroraCoverage,_AuroraHeight;
            float4 _TowerCenter;
            float _FloorHeight,_MotionTime;
            half _WetnessOn,_Porosity,_WetExposure,_WetBeads,_Cull;
        CBUFFER_END
        UNITY_INSTANCING_BUFFER_START(Construction)
            UNITY_DEFINE_INSTANCED_PROP(float,_BuildMode)
            UNITY_DEFINE_INSTANCED_PROP(float,_BuildReveal)
            UNITY_DEFINE_INSTANCED_PROP(float,_BuildClock)
        UNITY_INSTANCING_BUFFER_END(Construction)
        #include "BuildingAuroraCurtain.hlsl"
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
            nointerpolation float3 originWS:TEXCOORD3;
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
            o.originWS=TransformObjectToWorld(float3(0,0,0));
            return o;
        }
        float AuroraArchTime()
        {
            return (_MotionTime<0?_Time.y:_MotionTime)*_AuroraSpeed;
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
        // Same incoming-chunk dissolve as the other buildings; permanent pieces stay solid.
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
        AuroraSettings AuroraArchSettings(float footprint)
        {
            AuroraSettings s;
            s.time=AuroraArchTime();
            s.base=_AuroraBase;s.edgeWander=_AuroraEdgeWander;
            s.rayDensity=_AuroraRayDensity;s.rayDrift=_AuroraRayDrift;s.flicker=_AuroraFlicker;
            s.foldAmount=_AuroraFolds;s.surge=_AuroraSurge;
            s.activityMin=_AuroraActivityMin;s.activityMax=_AuroraActivityMax;s.coverage=_AuroraCoverage;s.heightScale=_AuroraHeight;
            s.footprint=footprint;
            s.lowColor=_AuroraLowColor.rgb;s.midColor=_AuroraMidColor.rgb;s.highColor=_AuroraHighColor.rgb;s.fringeColor=_AuroraFringeColor.rgb;
            return s;
        }
        ENDHLSL
        Pass
        {
            Name "AuroraArchForward"
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
                half auroraFace=step(.5,region)*step(region,1.5);
                half glow=step(2.5,region)*step(region,3.5);
                half white=1-glow;

                half3 aurora=0;half auroraAmount=0;
                if(auroraFace>.5)
                {
                    float footprint=max(length(fwidth(input.positionWS)),1e-4);
                    AuroraRing ring=AuroraRingAt(input.positionWS,input.originWS,_TowerCenter.xz,_FloorHeight);
                    AuroraLight light=AuroraCurtain(ring,input.positionOS.y,AuroraArchSettings(footprint));
                    auroraAmount=light.amount*AuroraMask(input.positionOS.xy);
                    aurora=light.color*auroraAmount;
                }

                float4 shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                SurfaceData surface=(SurfaceData)0;
                surface.alpha=1;surface.occlusion=1;surface.normalTS=half3(0,0,1);surface.specular=half3(.04,.04,.04);
                // The curtain reads as light inside the glaze: it tints the white and glows through it.
                half3 hue=aurora/max(auroraAmount,1e-3);
                half daylight=saturate(_AeroDaylight);
                // By day the curtain shows as colour in the glaze; at night it mostly glows.
                half3 tint=lerp(_BaseColor.rgb,hue*hue*.9,saturate(auroraAmount*_AuroraTint*lerp(1,2.6,daylight)));
                surface.albedo=tint*white+_GlowColor.rgb*glow*.3;
                surface.smoothness=AeroPolish(_Smoothness,input.positionWS,white)*white+.6*glow;
                surface.clearCoatMask=_ClearCoat*white;surface.clearCoatSmoothness=.96;
                half night=lerp(_AuroraNightBoost,.45,daylight);
                surface.emission=aurora*_AuroraStrength*night+_GlowColor.rgb*_GlowStrength*glow+half3(.08,.65,1)*buildEdge*2.2;
                surface.albedo=min(surface.albedo,_AlbedoCeiling.xxx);
                #if defined(_AERO_WETNESS)
                    AeroApplyWetnessBeads(surface.albedo,surface.smoothness,n,input.positionWS,geometric,_Porosity,_WetExposure,_WetBeads);
                #endif
                InputData data=(InputData)0;
                data.positionWS=input.positionWS;data.positionCS=input.positionCS;
                data.normalWS=n;data.viewDirectionWS=view;
                data.shadowCoord=shadowCoord;data.bakedGI=SampleSH(n);data.shadowMask=half4(1,1,1,1);
                data.vertexLighting=VertexLighting(input.positionWS,n);
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                half4 color=UniversalFragmentPBR(data,surface);
                color.rgb=AeroFinishReflection(color.rgb,input.positionWS,n,view,0,3.2,_AeroDaylight);
                color.rgb+=AeroThinFilm(n,view,white*.5,_AeroDaylight);
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
