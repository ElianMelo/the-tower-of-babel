Shader "TowerOfBabel/Buildings/Aquatic Finish"
{
    Properties
    {
        _MarbleMap("White marble reference", 2D) = "white" {}
        [Normal] _NormalMap("Marble relief normal map", 2D) = "bump" {}
        _NormalStrength("Marble relief strength", Range(0,2)) = .85
        _BakedSurface("Baked pearl and blue surface", 2D) = "white" {}
        [Normal] _BakedNormal("Baked raised ornament", 2D) = "bump" {}
        _BakedAO("Baked relief occlusion", 2D) = "white" {}
        _BakedStrength("Raised ornament strength", Range(0,2)) = 1
        _AOStrength("Relief shadow strength", Range(0,1)) = .4
        _PaletteMap("Existing inlay mask", 2D) = "white" {}
        _BaseColor("White stone", Color) = (1,1,1,1)
        _DetailTint("Soft blue inlays", Color) = (.72,.83,.95,1)
        _VeinStrength("Gray vein strength", Range(0,1)) = .75
        _DetailStrength("Inlay tint strength", Range(0,1)) = .16
        _MarbleScale("Texture repeats per metre", Float) = .28
        _TextureAspect("Reference width / height", Float) = 1.5
        _Smoothness("Stone polish", Range(0,1)) = .72
        _PearlCoat("Pearl shell clear coat", Range(0,1)) = .8
        _AeroGloss("Soft sky reflection", Range(0,1)) = .5
        _UseGeometryColors("Use architectural color boundaries", Range(0,1)) = 0
        _GlazeColor("Aqua glazing", Color) = (.26,.74,.90,1)
        _WaterLightMap("Soft reflected water light", 2D) = "black" {}
        _WaterLightStrength("Reflected water light strength", Range(0,1)) = .12
        _WaterField("Flow vectors and phase noise", 2D) = "gray" {}
        [Normal] _WaterRippleNormal("Flowing water ripple normal", 2D) = "bump" {}
        _WaterScale("Water pattern size", Float) = .8
        _FlowSpeed("Water flow speed", Range(0,.5)) = .075
        _FlowDistortion("Water flow deformation", Range(0,2)) = .75
        _WaterNormalStrength("Glaze ripple strength", Range(0,.5)) = .12
        _InnerDepth("Apparent water depth", Range(0,.2)) = .055
        _InteriorStrength("Water color within glass", Range(0,1)) = .3
        _InnerCausticStrength("Caustics within glass", Range(0,2)) = .55
        _GlazeGlow("Aquatic inlay glow", Range(0,4)) = 1
        _GlassBorderWidth("Glass border width in metres", Range(0,.4)) = .11
        _GlassBorderStrength("Glass border strength", Range(0,1)) = .7
        _GlassEdgeStrength("Frosted Fresnel edge", Range(0,1)) = .12
        _WaterDeepColor("Deep aqua water", Color) = (.08,.48,.70,1)
        _WaterLightColor("Illuminated water", Color) = (.53,.88,.98,1)
        _ModelKind("Arch 0, Floor 1, Pillar 2, Step 3", Float) = 0
        _InstanceVariation("Stable per-instance variation", Range(0,1)) = 1
        _WaterDetailDistance("Water detail distance", Float) = 85
        _MotionTime("Preview clock; -1 uses engine time", Float) = -1
        _GlazeReflection("Frosted glaze reflection balance", Range(0,1)) = .25
        [Toggle(_AERO_BORDER_GLASS)] _BorderGlass("See-through glass borders (requires building glass feature)", Float) = 0
        _GlassTransmission("Glass transmission", Range(0,1)) = .9
        _GlassThickness("Glass optical thickness in metres", Range(0,.3)) = .10
        _GlassRefraction("Glass refraction strength", Range(0,2)) = .8
        _GlassIOR("Glass index of refraction", Range(1,2)) = 1.45
        _GlassTint("Glass absorption tint", Color) = (.68,.93,1,1)
        _PatternSeed("Model variation seed", Float) = 0
        [PerRendererData] _BuildMode("Incoming chunk dissolve", Float) = 0
        [PerRendererData] _BuildReveal("Construction reveal", Range(0,1)) = 1
        [PerRendererData] _BuildClock("Construction clock", Float) = 0
        [HideInInspector] _RadialFloor("Tower radial floor", Float) = 0
        [HideInInspector] _RadialFloorCenter("Tower floor center XZ", Vector) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #define _CLEARCOAT 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_MarbleMap); SAMPLER(sampler_MarbleMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_BakedSurface); SAMPLER(sampler_BakedSurface);
        TEXTURE2D(_BakedNormal); SAMPLER(sampler_BakedNormal);
        TEXTURE2D(_BakedAO); SAMPLER(sampler_BakedAO);
        TEXTURE2D(_PaletteMap); SAMPLER(sampler_PaletteMap);
        TEXTURE2D(_WaterLightMap); SAMPLER(sampler_WaterLightMap);
        TEXTURE2D(_WaterField); SAMPLER(sampler_WaterField);
        TEXTURE2D(_WaterRippleNormal); SAMPLER(sampler_WaterRippleNormal);
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _DetailTint, _GlazeColor;
            half _VeinStrength, _DetailStrength, _Smoothness, _Cull, _NormalStrength, _BakedStrength, _AOStrength, _PearlCoat, _AeroGloss;
            float _MarbleScale, _TextureAspect, _PatternSeed;
            half _UseGeometryColors, _WaterLightStrength;
            half4 _WaterDeepColor,_WaterLightColor;
            float _WaterScale,_FlowSpeed,_FlowDistortion,_InnerDepth,_WaterDetailDistance,_MotionTime,_ModelKind;
            half _WaterNormalStrength,_InteriorStrength,_InnerCausticStrength,_GlassBorderWidth,_GlassBorderStrength,_GlassEdgeStrength,_InstanceVariation;
            half _GlazeReflection;
            half _GlazeGlow;
            float _GlassThickness,_GlassRefraction,_GlassIOR;
            half _BorderGlass,_GlassTransmission;
            half4 _GlassTint;
            float _RadialFloor;
            float4 _RadialFloorCenter;
        CBUFFER_END
        UNITY_INSTANCING_BUFFER_START(Construction)
            UNITY_DEFINE_INSTANCED_PROP(float,_BuildMode)
            UNITY_DEFINE_INSTANCED_PROP(float,_BuildReveal)
            UNITY_DEFINE_INSTANCED_PROP(float,_BuildClock)
        UNITY_INSTANCING_BUFFER_END(Construction)
        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
            float4 tangentOS:TANGENT;
            float2 uv:TEXCOORD0;
            float2 paletteUV:TEXCOORD1;
            float3 floorPatch:TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            float2 uv:TEXCOORD2;
            half fog:TEXCOORD3;
            nointerpolation float4 originSeed:TEXCOORD4;
            half4 tangentWS:TEXCOORD5;
            float2 paletteUV:TEXCOORD6;
            float3 positionOS:TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        #include "BuildingRadialFloor.hlsl"
        Varyings Vert(Attributes input)
        {
            Varyings o=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            float3 unbentPosition=input.positionOS.xyz;
            BendRadialFloor(input);
            UNITY_TRANSFER_INSTANCE_ID(input,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
            o.positionCS=p.positionCS;o.positionWS=p.positionWS;
            o.normalWS=TransformObjectToWorldNormal(input.normalOS);
            o.tangentWS=half4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w*GetOddNegativeScale());
            o.paletteUV=input.paletteUV;
            o.positionOS=_RadialFloor>1.5?input.floorPatch:unbentPosition;
            o.uv=input.uv;o.fog=ComputeFogFactor(p.positionCS.z);
            float3 origin=TransformObjectToWorld(float3(0,0,0));
            float3 q=frac(floor(origin*32+.5)*float3(.1031,.1030,.0973)+_PatternSeed*.137);
            q+=dot(q,q.yzx+33.33);
            o.originSeed=float4(origin,frac((q.x+q.y)*q.z));
            return o;
        }
        #include "BuildingMarbleSurface.hlsl"
        float3 MarblePosition(Varyings input)
        {
            // One continuous floor finish across tiles, rings and near/far transitions.
            if(_ModelKind>.5 && _ModelKind<1.5)return input.positionWS*_MarbleScale;
            float seed=input.originSeed.w;
            return (input.positionWS-input.originSeed.xyz)*_MarbleScale
                 +float3(seed,frac(seed*3.71),frac(seed*7.13))*3;
        }
        half3 OrnamentNormal(Varyings input,half3 n)
        {
            // The old floor bake contains a separate bevel around every tile.
            // Finished deck faces join flush; keep their real geometric normal.
            if(_ModelKind>.5 && _ModelKind<1.5 && abs(n.y)>.99)return n;
            half3 detail=UnpackNormal(SAMPLE_TEXTURE2D(_BakedNormal,sampler_BakedNormal,input.uv));
            detail.xy*=_BakedStrength;detail=normalize(detail);
            half3 tangent=SafeNormalize(input.tangentWS.xyz-n*dot(n,input.tangentWS.xyz));
            half3 bitangent=cross(n,tangent)*input.tangentWS.w;
            return normalize(tangent*detail.x+bitangent*detail.y+n*detail.z);
        }
        #include "BuildingAquaticFlow.hlsl"
        half3 ArchitecturalColor(Varyings input)
        {
            half3 baked=SAMPLE_TEXTURE2D(_BakedSurface,sampler_BakedSurface,input.uv).rgb;
            return lerp(baked,lerp(half3(1,1,1),_GlazeColor.rgb,step(.2,input.paletteUV.x)),_UseGeometryColors);
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
        half ClipConstruction(Varyings input)
        {
            float mode=UNITY_ACCESS_INSTANCED_PROP(Construction,_BuildMode);
            if (mode<.5) return 0;
            float reveal=UNITY_ACCESS_INSTANCED_PROP(Construction,_BuildReveal);
            float clock=UNITY_ACCESS_INSTANCED_PROP(Construction,_BuildClock);
            // This branch runs only on incoming chunk renderers. Permanent
            // pillar pieces remain solid in forward, depth and shadow passes.
            float noise=ConstructionNoise(input.positionOS*10.5+float3(5.2+clock*.2,13.8,2.1-clock*.15));
            half edge=0;
            if (mode>.5)
            {
                float d=reveal*1.12-.06-noise; clip(d);
                edge=(1-smoothstep(0,.08,d))*step(.001,reveal)*step(reveal,.999);
            }
            return edge;
        }
        half ClipGlassFromCore(Varyings input)
        {
            half edge=ClipConstruction(input);
            #if defined(_AERO_BORDER_GLASS)
                // Small distant borders use the original opaque finish. Both passes use
                // the same instance-origin distance, so this cannot leave missing pixels.
                if(distance(GetCameraPositionWS(),input.originSeed.xyz)<_WaterDetailDistance)
                    clip(.35-GlassRegion(input,ArchitecturalColor(input)));
            #endif
            return edge;
        }
        ENDHLSL
        Pass
        {
            Name "MarbleForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _AERO_BORDER_GLASS
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
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half buildEdge=ClipGlassFromCore(input);
                half3 n=normalize(input.normalWS);
                float3 p=MarblePosition(input);
                half stone=lerp(1,Marble(p,n),_VeinStrength);
                n=MarbleNormal(p,OrnamentNormal(input,n));
                half3 palette=SAMPLE_TEXTURE2D(_PaletteMap,sampler_PaletteMap,input.paletteUV).rgb;
                half mask=saturate((.9-dot(palette,half3(.2126,.7152,.0722)))*1.5);
                SurfaceData surface=(SurfaceData)0;
                half3 bakedColor=SAMPLE_TEXTURE2D(_BakedSurface,sampler_BakedSurface,input.uv).rgb;
                half3 geometryColor=lerp(half3(1,1,1),_GlazeColor.rgb,step(.2,input.paletteUV.x));
                bakedColor=lerp(bakedColor,geometryColor,_UseGeometryColors);
                half glass=GlassRegion(input,bakedColor);
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                AquaticFrame flow=BuildAquaticFrame(input);
                n=AquaticNormal(flow,n,glass);
                half2 water=AquaticLight(flow,n,view);
                half3 interior=lerp(_WaterDeepColor.rgb,_WaterLightColor.rgb,saturate(water.y));
                bakedColor=lerp(bakedColor,interior,glass*_InteriorStrength);
                surface.albedo=stone*bakedColor*lerp(_BaseColor.rgb,_DetailTint.rgb,mask*_DetailStrength);
                surface.albedo=lerp(surface.albedo,half3(.04,.5,.85),buildEdge*.85);
                surface.alpha=1;surface.metallic=0;surface.specular=half3(.04,.04,.04);
                half enamel=saturate((bakedColor.b-bakedColor.r)*2);
                surface.smoothness=lerp(_Smoothness,.96,glass);surface.normalTS=half3(0,0,1);
                surface.clearCoatMask=_PearlCoat;surface.clearCoatSmoothness=.95;
                half floorFace=step(.5,_ModelKind)*step(_ModelKind,1.5)*step(.99,abs(input.normalWS.y));
                surface.occlusion=lerp(1,SAMPLE_TEXTURE2D(_BakedAO,sampler_BakedAO,input.uv).r,_AOStrength*(1-floorFace));
                InputData data=(InputData)0;
                data.positionWS=input.positionWS;data.positionCS=input.positionCS;
                data.normalWS=n;data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim=pow(1-saturate(dot(n,view)),3);
                surface.emission=half3(.42,.78,1)*water.x*_WaterLightStrength*glass*.3;
                surface.emission+=_WaterLightColor.rgb*glass*(water.x*_InnerCausticStrength+rim*_GlassEdgeStrength)*_GlazeGlow;
                surface.emission+=half3(.08,.65,1)*buildEdge*2.2;
                data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI=SampleSH(n);data.shadowMask=half4(1,1,1,1);
                data.vertexLighting=VertexLighting(input.positionWS,n);
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                half4 color=UniversalFragmentPBR(data,surface);
                // Frosted glass scatters colored light below the reflective coat.
                // Preserve the interior's aqua tint under very bright HDR probes.
                if(glass>.001)
                {
                    Light key=GetMainLight(data.shadowCoord);
                    half3 irradiance=data.bakedGI+key.color*saturate(dot(n,key.direction))*key.shadowAttenuation;
                    half3 scattered=surface.albedo*irradiance+surface.emission;
                    half reflection=lerp(_GlazeReflection,.9,rim);
                    half3 coatLight=min(max(color.rgb-scattered,0),half3(.65,.75,.85));
                    color.rgb=lerp(color.rgb,scattered+coatLight*reflection,glass);
                }
                color.rgb=MixFog(color.rgb,input.fog);color.a=1;
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "GlassBorders"
            Tags { "LightMode"="AeroGlassBorder" }
            ZWrite Off ZTest LEqual Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment GlassFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _AERO_BORDER_GLASS
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            half4 GlassFrag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if !defined(_AERO_BORDER_GLASS)
                    clip(-1);
                #endif
                clip(_WaterDetailDistance-distance(GetCameraPositionWS(),input.originSeed.xyz));
                half glass=GlassRegion(input,ArchitecturalColor(input));
                clip(glass-.35);
                half3 meshNormal=normalize(input.normalWS);
                AquaticFrame flow=BuildAquaticFrame(input);
                half3 n=AquaticNormal(flow,meshNormal,1);
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing=saturate(dot(n,view));
                half f0=(_GlassIOR-1)/(_GlassIOR+1);f0*=f0;
                half fresnel=f0+(1-f0)*pow(1-facing,5);
                float pathLength=_GlassThickness/max(facing,.22);
                float2 uv=GetNormalizedScreenSpaceUV(input.positionCS);
                float3 ray=refract(-view,n,rcp(_GlassIOR));
                float depth=-TransformWorldToView(input.positionWS).z;
                float2 offset=TransformWorldToViewDir(ray+view,false).xy*_GlassRefraction*pathLength/max(depth,.5);
                offset=clamp(offset,-.025,.025);
                float2 bentUV=saturate(uv+offset);
                float rawDepth=SampleSceneDepth(bentUV);
                float backgroundDepth=IsPerspectiveProjection()?LinearEyeDepth(rawDepth,_ZBufferParams):LinearDepthToEyeDepth(rawDepth);
                // Do not bend a foreground silhouette into a surface behind it.
                bentUV=backgroundDepth>depth+.015?bentUV:uv;
                half3 background=SampleSceneColor(bentUV);
                half3 absorption=exp(-(1-_GlassTint.rgb)*pathLength*4);
                half2 water=AquaticLight(flow,n,view);
                half3 interior=lerp(_WaterDeepColor.rgb,_WaterLightColor.rgb,water.y);
                half3 transmission=lerp(interior,background*absorption,_GlassTransmission)*(1-fresnel);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=0;surface.alpha=1;surface.specular=half3(.04,.04,.04);
                surface.smoothness=.965;surface.normalTS=half3(0,0,1);surface.occlusion=1;
                // Reflection-only dielectric lighting; scene transmission is composed separately.
                surface.clearCoatMask=0;surface.clearCoatSmoothness=1;
                InputData data=(InputData)0;
                data.positionWS=input.positionWS;data.positionCS=input.positionCS;
                data.normalWS=n;data.viewDirectionWS=view;
                data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                data.shadowMask=1;data.bakedGI=SampleSH(n);
                data.vertexLighting=VertexLighting(input.positionWS,n);
                data.normalizedScreenSpaceUV=uv;
                half3 reflection=UniversalFragmentPBR(data,surface).rgb;
                // Preserve clear transmission under the studio's very bright light cards.
                half3 reflectionLimit=lerp(half3(.22,.28,.34),half3(1.2,1.3,1.4),pow(1-facing,3));
                reflection=min(reflection,reflectionLimit);
                half3 light=_WaterLightColor.rgb*(water.x*_InnerCausticStrength*.20+pow(1-facing,3)*_GlassEdgeStrength*.18);
                // Background already contains fog; fog only the light added by the glass.
                half3 color=transmission+MixFogColor(reflection+light,half3(0,0,0),input.fog);
                return half4(color,1);
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
            #pragma shader_feature_local _AERO_BORDER_GLASS
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
            half4 DepthFrag(Varyings input):SV_Target { UNITY_SETUP_INSTANCE_ID(input);ClipGlassFromCore(input);return 0; }
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
            #pragma shader_feature_local _AERO_BORDER_GLASS
            half4 DepthFrag(Varyings input):SV_Target { UNITY_SETUP_INSTANCE_ID(input);ClipGlassFromCore(input);return input.positionCS.z; }
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
            #pragma shader_feature_local _AERO_BORDER_GLASS
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFrag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipGlassFromCore(input);
                float3 n=MarbleNormal(MarblePosition(input),OrnamentNormal(input,normalize(input.normalWS)));
                half3 color=SAMPLE_TEXTURE2D(_BakedSurface,sampler_BakedSurface,input.uv).rgb;
                color=lerp(color,lerp(half3(1,1,1),_GlazeColor.rgb,step(.2,input.paletteUV.x)),_UseGeometryColors);
                n=AquaticNormal(BuildAquaticFrame(input),n,GlassRegion(input,color));
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
