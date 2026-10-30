Shader "TowerOfBabel/Aero/Surface"
{
    // Opaque uber-lit material for the Frutiger Aero look. Property names follow URP/Lit
    // so existing materials upgrade by swapping the shader (see Editor/AeroMaterialUpgrade).
    Properties
    {
        _BaseMap("Base map", 2D) = "white" {}
        _BaseColor("Base color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal map", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0,2)) = 1
        _MetallicGlossMap("Metallic (R) smoothness (A)", 2D) = "white" {}
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = .7
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion strength", Range(0,1)) = 1
        [Toggle(_EMISSION)] _EmissionOn("Emission", Float) = 0
        _EmissionMap("Emission map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission color", Color) = (0,0,0,1)

        _ClearCoatMask("Clear coat", Range(0,1)) = 0
        _ClearCoatSmoothness("Clear coat smoothness", Range(0,1)) = .95

        [Toggle(_AERO_WETNESS)] _WetnessOn("Wetness (global rain/dew)", Float) = 0
        _Porosity("Porosity (0 sealed, 1 absorbent)", Range(0,1)) = .2
        _WetExposure("Weather exposure", Range(0,1)) = 1

        [Toggle(_AERO_ANISO)] _AnisoOn("Anisotropic (brushed metal)", Float) = 0
        _Anisotropy("Anisotropy", Range(-1,1)) = .75
        [Enum(Mesh tangent,0,Circular,1,Linear,2)] _AnisoMode("Brush direction", Float) = 2
        _AnisoAxis("Brush axis (object space)", Vector) = (0,1,0,0)
        _SheenStrength("Grain sheen line", Range(0,2)) = .35

        [Toggle(_AERO_IRIDESCENT)] _IridescenceOn("Iridescent", Float) = 0
        [Enum(Thin film,0,Diffraction grating,1)] _IridescenceMode("Iridescence type", Float) = 0
        _IridescenceStrength("Iridescence strength", Range(0,1)) = .6
        _FilmThickness("Film thickness (nm)", Range(100,1200)) = 420
        _FilmThicknessRange("Film thickness variation (nm)", Range(0,600)) = 220
        _FilmIOR("Film IOR", Range(1,2.4)) = 1.45
        _DiffractionSpacing("Grating spacing (nm)", Range(500,3000)) = 1600

        [Toggle(_AERO_SSS)] _SubsurfaceOn("Subsurface scattering", Float) = 0
        _SubsurfaceColor("Scatter color", Color) = (1,.45,.3,1)
        _SubsurfaceWrap("Scatter wrap", Range(0,1)) = .5

        [Toggle(_AERO_TRANSLUCENT)] _TranslucentOn("Translucent (thin)", Float) = 0
        _TranslucencyColor("Transmitted color", Color) = (.8,1,.45,1)
        _TranslucencyStrength("Transmission strength", Range(0,4)) = 1
        _TranslucencyDistortion("Transmission distortion", Range(0,1)) = .35
        _TranslucencyPower("Transmission focus", Range(1,16)) = 4
        _Thickness("Thickness", Range(0,1)) = .3
        [Toggle] _ThicknessFromVertex("Thickness from vertex alpha", Float) = 0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #define _CLEARCOAT 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _EmissionColor, _SubsurfaceColor, _TranslucencyColor;
            float4 _AnisoAxis;
            half _BumpScale, _Metallic, _Smoothness, _OcclusionStrength;
            half _ClearCoatMask, _ClearCoatSmoothness;
            half _Porosity, _WetExposure;
            half _Anisotropy, _AnisoMode, _SheenStrength;
            half _IridescenceMode, _IridescenceStrength, _FilmThickness, _FilmThicknessRange, _FilmIOR, _DiffractionSpacing;
            half _SubsurfaceWrap;
            half _TranslucencyStrength, _TranslucencyDistortion, _TranslucencyPower, _Thickness, _ThicknessFromVertex;
            half _Cull, _EmissionOn, _WetnessOn, _AnisoOn, _IridescenceOn, _SubsurfaceOn, _TranslucentOn;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
            float4 tangentOS:TANGENT;
            float2 uv:TEXCOORD0;
            half4 color:COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            half4 tangentWS:TEXCOORD2;
            float2 uv:TEXCOORD3;
            half fog:TEXCOORD4;
            half4 color:TEXCOORD5;
            float3 originWS:TEXCOORD6;
            half3 axisWS:TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            o.positionCS = p.positionCS; o.positionWS = p.positionWS;
            o.normalWS = TransformObjectToWorldNormal(input.normalOS);
            o.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
            o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            o.fog = ComputeFogFactor(p.positionCS.z);
            o.color = input.color;
            o.originWS = TransformObjectToWorld(float3(0, 0, 0));
            o.axisWS = SafeNormalize(TransformObjectToWorldDir(_AnisoAxis.xyz + float3(0, 1e-5, 0)));
            return o;
        }
        ENDHLSL
        Pass
        {
            Name "AeroForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _AERO_WETNESS
            #pragma shader_feature_local_fragment _AERO_ANISO
            #pragma shader_feature_local_fragment _AERO_IRIDESCENT
            #pragma shader_feature_local_fragment _AERO_SSS
            #pragma shader_feature_local_fragment _AERO_TRANSLUCENT
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Aero/AeroWetness.hlsl"
            #include "Aero/AeroIridescence.hlsl"
            #include "Aero/AeroAnisotropy.hlsl"
            #include "Aero/AeroSubsurface.hlsl"
            #include "Aero/AeroTranslucency.hlsl"
            #include "Aero/AeroIndirect.hlsl"
            float _AeroDaylight;

            struct AeroPixel
            {
                BRDFData brdf;
                BRDFData coat;
                half coatMask;
                half3 n, v, t, b;
                half3 albedo;
                half3 specularTint;
                half curvature;
                half thickness;
            };

            half3 AeroShadeLight(Light light, AeroPixel px)
            {
                half3 l = light.direction;
                half NdotL = dot(px.n, l);
                half shadow = light.shadowAttenuation;
                #if defined(_AERO_SSS)
                    shadow = AeroSubsurfaceShadow(shadow, _SubsurfaceWrap);
                    half3 diffuseTerm = AeroSubsurfaceDiffuse(NdotL, _SubsurfaceColor.rgb, _SubsurfaceWrap, px.curvature);
                #else
                    half3 diffuseTerm = saturate(NdotL).xxx;
                #endif
                half3 radiance = light.color * light.distanceAttenuation * shadow;
                half3 color = px.brdf.diffuse * diffuseTerm * radiance;

                half NdotLs = saturate(NdotL);
                #if defined(_AERO_ANISO)
                    half3 specular = AeroAnisoSpecular(px.n, px.t, px.b, px.v, l, px.brdf.perceptualRoughness, _Anisotropy, px.brdf.specular)
                        + px.brdf.specular * AeroKajiyaKay(_Anisotropy >= 0 ? px.b : px.t, px.v, l, 180) * _SheenStrength * NdotLs;
                #else
                    half3 specular = px.brdf.specular * DirectBRDFSpecular(px.brdf, px.n, l, px.v) * NdotLs;
                #endif
                #if defined(_AERO_IRIDESCENT)
                    [branch] if (_IridescenceMode > .5)
                        specular += AeroDiffraction(px.t, l, px.v, _DiffractionSpacing) * DirectBRDFSpecular(px.brdf, px.n, l, px.v) * NdotLs * _IridescenceStrength;
                #endif
                specular *= px.specularTint;
                color += specular * light.color * light.distanceAttenuation * light.shadowAttenuation;

                // Clear coat on top, as in URP's LightingPhysicallyBased.
                [branch] if (px.coatMask > 0)
                {
                    half coatTerm = kDielectricSpec.r * DirectBRDFSpecular(px.coat, px.n, l, px.v);
                    half NoV = saturate(dot(px.n, px.v));
                    half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * Pow4(1 - NoV);
                    color = color * (1 - px.coatMask * coatFresnel) + coatTerm * px.coatMask * NdotLs * radiance;
                }
                #if defined(_AERO_TRANSLUCENT)
                    color += AeroTranslucentLight(px.n, px.v, l, light.color, light.distanceAttenuation * lerp(light.shadowAttenuation, 1, .6),
                        _TranslucencyColor.rgb * px.albedo, _TranslucencyDistortion, _TranslucencyPower, px.thickness, _TranslucencyStrength);
                #endif
                return color;
            }
            #include "Aero/AeroLightLoop.hlsl"

            half4 Frag(Varyings input, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 geometric = normalize(input.normalWS) * (facing > 0 ? 1 : -1);
                half3 tangent = SafeNormalize(input.tangentWS.xyz - geometric * dot(geometric, input.tangentWS.xyz));
                half3 bitangent = cross(geometric, tangent) * (input.tangentWS.w < 0 ? -1 : 1);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 n = normalize(tangent * normalTS.x + bitangent * normalTS.y + geometric * normalTS.z);
                half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half4 metalGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half3 albedo = baseSample.rgb;
                half metallic = metalGloss.r * _Metallic;
                half smoothness = metalGloss.a * _Smoothness;
                half occlusion = lerp(1, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g, _OcclusionStrength);

                #if defined(_AERO_WETNESS)
                    AeroApplyWetness(albedo, smoothness, n, input.positionWS, geometric, _Porosity, _WetExposure);
                #endif

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo; surface.metallic = metallic; surface.specular = half3(.04, .04, .04);
                surface.smoothness = smoothness; surface.occlusion = occlusion; surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);
                surface.clearCoatMask = _ClearCoatMask; surface.clearCoatSmoothness = _ClearCoatSmoothness;

                AeroPixel px = (AeroPixel)0;
                InitializeBRDFData(surface, px.brdf);
                px.coat = CreateClearCoatBRDFData(surface, px.brdf);
                px.coatMask = _ClearCoatMask;
                px.n = n; px.v = v; px.albedo = albedo;
                px.specularTint = 1;
                px.thickness = _ThicknessFromVertex > .5 ? input.color.a : _Thickness;
                #if defined(_AERO_SSS)
                    px.curvature = AeroCurvature(n, input.positionWS);
                #endif
                #if defined(_AERO_ANISO) || defined(_AERO_IRIDESCENT)
                    half4 meshTangent = half4(tangent, input.tangentWS.w);
                    px.t = AeroAnisoTangent((int)_AnisoMode, n, meshTangent, input.positionWS, input.originWS, input.axisWS);
                    px.b = cross(n, px.t);
                #endif
                float filmThickness = 0;
                #if defined(_AERO_IRIDESCENT)
                    [branch] if (_IridescenceMode < .5)
                    {
                        filmThickness = _FilmThickness + (AeroFilmNoise(input.positionWS * 3.1) - .5) * _FilmThicknessRange;
                        half3 film = AeroThinFilmHue(dot(n, v), filmThickness, _FilmIOR, lerp(1.5, 2.5, metallic));
                        px.specularTint = lerp(1, film, _IridescenceStrength);
                    }
                #endif

                InputData data = (InputData)0;
                data.positionWS = input.positionWS; data.positionCS = input.positionCS;
                data.normalWS = n; data.viewDirectionWS = v;
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI = SampleSH(n); data.shadowMask = half4(1, 1, 1, 1);
                data.vertexLighting = VertexLighting(input.positionWS, n);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half4 shadowMask = CalculateShadowMask(data);
                AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(data, surface);

                half3 reflectionNormal = n;
                #if defined(_AERO_ANISO)
                    reflectionNormal = AeroAnisoReflectionNormal(n, px.t, px.b, v, px.brdf.perceptualRoughness, _Anisotropy);
                #endif
                #if defined(_AERO_SSS)
                    // Ambient light also wraps into the scatter colour on the shadow side.
                    data.bakedGI += SampleSH(-n) * _SubsurfaceColor.rgb * _SubsurfaceWrap * .25;
                #endif
                half3 color = AeroGlobalIllumination(px.brdf, px.coat, px.coatMask, data.bakedGI, ao.indirectAmbientOcclusion,
                    input.positionWS, n, reflectionNormal, v, data.normalizedScreenSpaceUV, px.specularTint, _AeroDaylight);

                Light mainLight = GetMainLight(data, shadowMask, ao);
                color += AeroShadeLight(mainLight, px);
                color += AeroAdditionalLights(data, shadowMask, ao, px);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    color += data.vertexLighting * px.brdf.diffuse;
                #endif
                #if defined(_AERO_TRANSLUCENT)
                    // Sky light through thin geometry.
                    color += SampleSH(-n) * _TranslucencyColor.rgb * albedo * (1 - px.thickness) * _TranslucencyStrength * .2;
                #endif
                #if defined(_AERO_IRIDESCENT)
                    // Pearlescent sheen: the film's own coloured reflection of sky and sun across
                    // the whole surface, not only inside the specular highlight.
                    [branch] if (_IridescenceMode < .5)
                    {
                        half3 filmReflectance = AeroThinFilm3(saturate(dot(n, v)), filmThickness, _FilmIOR, lerp(1.5, 2.5, metallic));
                        half3 sky = max(SampleSH(reflect(-v, n)), 0) + mainLight.color * mainLight.shadowAttenuation * saturate(dot(n, mainLight.direction)) * .5;
                        color += filmReflectance * sky * _IridescenceStrength * 3.5;
                    }
                #endif
                #if defined(_EMISSION)
                    color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #endif
                color = MixFog(color, input.fog);
                return half4(min(color, HALF_MAX), 1);
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
                Varyings o = Vert(input);
                float3 direction = _LightDirection;
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    direction = normalize(_LightPosition - o.positionWS);
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(o.positionWS, normalize(o.normalWS), direction));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 DepthFrag(Varyings input) : SV_Target { return 0; }
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
            half4 DepthFrag(Varyings input) : SV_Target { return input.positionCS.z; }
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
            half4 NormalFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 n = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n) * .5 + .5;
                    return half4(PackFloat2To888(saturate(oct)), 0);
                #else
                    return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
