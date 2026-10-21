Shader "TowerOfBabel/Buildings/White Marble"
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
        _PatternSeed("Model variation seed", Float) = 0
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
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _DetailTint, _GlazeColor;
            half _VeinStrength, _DetailStrength, _Smoothness, _Cull, _NormalStrength, _BakedStrength, _AOStrength, _PearlCoat, _AeroGloss;
            float _MarbleScale, _TextureAspect, _PatternSeed;
            half _UseGeometryColors, _WaterLightStrength;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
            float4 tangentOS:TANGENT;
            float2 uv:TEXCOORD0;
            float2 paletteUV:TEXCOORD1;
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
            o.tangentWS=half4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w*GetOddNegativeScale());
            o.paletteUV=input.paletteUV;
            o.uv=input.uv;o.fog=ComputeFogFactor(p.positionCS.z);
            float3 origin=TransformObjectToWorld(float3(0,0,0));
            float3 q=frac(floor(origin*32+.5)*float3(.1031,.1030,.0973)+_PatternSeed*.137);
            q+=dot(q,q.yzx+33.33);
            o.originSeed=float4(origin,frac((q.x+q.y)*q.z));
            return o;
        }
        half Marble(float3 p,half3 n)
        {
            half3 w=pow(abs(n),4);w/=max(dot(w,half3(1,1,1)),.001);
            float aspect=max(_TextureAspect,.01);
            half a=SAMPLE_TEXTURE2D(_MarbleMap,sampler_MarbleMap,float2(p.y/aspect,p.z)).r;
            half b=SAMPLE_TEXTURE2D(_MarbleMap,sampler_MarbleMap,float2(p.x/aspect,p.z)).r;
            half c=SAMPLE_TEXTURE2D(_MarbleMap,sampler_MarbleMap,float2(p.x/aspect,p.y)).r;
            return dot(half3(a,b,c),w);
        }
        float3 MarblePosition(Varyings input)
        {
            float seed=input.originSeed.w;
            return (input.positionWS-input.originSeed.xyz)*_MarbleScale
                 +float3(seed,frac(seed*3.71),frac(seed*7.13))*3;
        }
        half2 ReliefGradient(float2 uv)
        {
            half3 normal=UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,uv));
            // Mirrored image repeats also mirror the relief's tangent directions.
            half2 mirror=1-4*frac(floor(uv)*.5);
            return normal.xy/max(normal.z,.1)*mirror;
        }
        half3 MarbleNormal(float3 p,half3 n)
        {
            half3 w=pow(abs(n),4);w/=max(dot(w,half3(1,1,1)),.001);
            float aspect=max(_TextureAspect,.01);
            half2 x=ReliefGradient(float2(p.y/aspect,p.z));
            half2 y=ReliefGradient(float2(p.x/aspect,p.z));
            half2 z=ReliefGradient(float2(p.x/aspect,p.y));
            half3 gradient=half3(0,x.x,x.y)*w.x+half3(y.x,0,y.y)*w.y+half3(z.x,z.y,0)*w.z;
            gradient-=n*dot(n,gradient);
            // A flat normal texture leaves the actual rounded mesh normal intact.
            return normalize(n+gradient*_NormalStrength);
        }
        half3 OrnamentNormal(Varyings input,half3 n)
        {
            half3 detail=UnpackNormal(SAMPLE_TEXTURE2D(_BakedNormal,sampler_BakedNormal,input.uv));
            detail.xy*=_BakedStrength;detail=normalize(detail);
            half3 tangent=SafeNormalize(input.tangentWS.xyz-n*dot(n,input.tangentWS.xyz));
            half3 bitangent=cross(n,tangent)*input.tangentWS.w;
            return normalize(tangent*detail.x+bitangent*detail.y+n*detail.z);
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
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
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
                surface.albedo=stone*bakedColor*lerp(_BaseColor.rgb,_DetailTint.rgb,mask*_DetailStrength);
                surface.alpha=1;surface.metallic=0;surface.specular=half3(.04,.04,.04);
                half enamel=saturate((bakedColor.b-bakedColor.r)*2);
                surface.smoothness=lerp(_Smoothness,.96,enamel);surface.normalTS=half3(0,0,1);
                surface.clearCoatMask=_PearlCoat;surface.clearCoatSmoothness=.95;
                surface.occlusion=lerp(1,SAMPLE_TEXTURE2D(_BakedAO,sampler_BakedAO,input.uv).r,_AOStrength);
                InputData data=(InputData)0;
                data.positionWS=input.positionWS;data.positionCS=input.positionCS;
                data.normalWS=n;data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                // A broad sky sheen keeps the glazed accents readable without a local probe.
                half3 reflected=reflect(-data.viewDirectionWS,n);
                half rim=pow(1-saturate(dot(n,data.viewDirectionWS)),4);
                half window=pow(saturate(dot(reflected,normalize(half3(-.55,.72,-.42)))),24);
                half broadWindow=pow(saturate(dot(reflected,normalize(half3(-.35,-.12,-.93)))),48);
                half sky=smoothstep(-.15,.75,reflected.y);
                surface.emission=_AeroGloss*(half3(.52,.77,1)*rim*.24
                    +half3(.84,.94,1)*(window*.65+broadWindow*1.1)
                    +half3(.18,.35,.55)*sky*enamel*.10)*(.3+enamel*.7);
                float2 waterUV=float2(input.positionWS.x+input.positionWS.z*.45,
                    input.positionWS.y+input.positionWS.z*.35)*.48+input.originSeed.w*3;
                half waterLight=SAMPLE_TEXTURE2D(_WaterLightMap,sampler_WaterLightMap,waterUV).r;
                surface.emission+=half3(.35,.75,1)*waterLight*waterLight*_WaterLightStrength*(1-enamel*.75);
                data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI=SampleSH(n);data.shadowMask=half4(1,1,1,1);
                data.vertexLighting=VertexLighting(input.positionWS,n);
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                half4 color=UniversalFragmentPBR(data,surface);
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
            half4 DepthFrag(Varyings input):SV_Target { return 0; }
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
            half4 DepthFrag(Varyings input):SV_Target { return input.positionCS.z; }
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
                float3 n=MarbleNormal(MarblePosition(input),OrnamentNormal(input,normalize(input.normalWS)));
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
