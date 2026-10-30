Shader "TowerOfBabel/Aero/Glass"
{
    // Clear Frutiger glass: refraction of the opaque scene, Beer-Lambert body tint that
    // turns cyan-green along thick edges, white-cyan Fresnel rims, optional frost,
    // dispersion, wet drops and a faint thin film.
    Properties
    {
        [Normal] _BumpMap("Normal map", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0,2)) = .3
        _BaseColor("Glass body tint", Color) = (.80,.95,1,1)
        _EdgeTint("Thick-edge tint", Color) = (.45,.86,.80,1)
        _Thickness("Optical thickness (m)", Range(0,.5)) = .03
        _Density("Tint density", Range(0,20)) = 6
        _IOR("Index of refraction", Range(1,2)) = 1.5
        _Refraction("Refraction strength", Range(0,4)) = 1
        _Dispersion("Dispersion", Range(0,.3)) = .06
        _Frost("Frost blur", Range(0,1)) = 0
        _Smoothness("Surface smoothness", Range(0,1)) = .97
        _RimColor("Fresnel rim", Color) = (.5,.85,.94,1)
        _RimStrength("Rim strength", Range(0,4)) = 1
        _RimPower("Rim falloff", Range(1,8)) = 3
        _ReflectionStrength("Reflection strength", Range(0,2)) = 1
        _FilmStrength("Thin-film sheen", Range(0,1)) = .15
        [Toggle(_AERO_WETNESS)] _WetnessOn("Rain drops on the outer face", Float) = 0
        _WetExposure("Weather exposure", Range(0,1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "AeroGlass"
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off ZTest LEqual Cull [_Cull] Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _AERO_WETNESS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BumpMap_ST;
                half4 _BaseColor, _EdgeTint, _RimColor;
                half _BumpScale, _Thickness, _Density, _IOR, _Refraction, _Dispersion, _Frost, _Smoothness;
                half _RimStrength, _RimPower, _ReflectionStrength, _FilmStrength, _WetExposure, _Cull, _WetnessOn;
            CBUFFER_END
            float _AeroDaylight;
            #include "Aero/AeroGlass.hlsl"
            #include "Aero/AeroWetness.hlsl"
            #include "Aero/AeroIridescence.hlsl"
            #include "Aero/AeroIndirect.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1;
                half4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; half fog:TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(input.uv, _BumpMap); o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half GlassSpecular(half3 n, half3 v, half3 l, half roughness)
            {
                half3 h = SafeNormalize(l + v);
                half NdotH = saturate(dot(n, h)), LdotH = saturate(dot(l, h));
                half r2 = roughness * roughness;
                half d = NdotH * NdotH * (r2 - 1) + 1.00001;
                return r2 / (d * d * max(.1, LdotH * LdotH) * (roughness * 4 + 2)) * saturate(dot(n, l));
            }
            half4 Frag(Varyings input, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 geometric = normalize(input.normalWS) * (facing > 0 ? 1 : -1);
                half3 tangent = SafeNormalize(input.tangentWS.xyz - geometric * dot(geometric, input.tangentWS.xyz));
                half3 bitangent = cross(geometric, tangent) * (input.tangentWS.w < 0 ? -1 : 1);
                half3 nts = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 n = normalize(tangent * nts.x + bitangent * nts.y + geometric * nts.z);
                half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half smoothness = _Smoothness;
                #if defined(_AERO_WETNESS)
                    half3 unusedAlbedo = 1;
                    AeroApplyWetness(unusedAlbedo, smoothness, n, input.positionWS, geometric, 0, _WetExposure * (facing > 0 ? 1 : 0));
                #endif
                half cosV = saturate(dot(n, v));
                half fresnel = AeroFresnel(cosV, _IOR);
                float path = _Thickness / max(cosV, .12);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                half roughness = PerceptualRoughnessToRoughness(1 - smoothness);
                half3 background = AeroSampleDispersed(input.positionWS, n, v, _IOR, _Thickness + .02, _Refraction, _Dispersion, _Frost * .02, uv);
                // Body tint grows with path length; grazing views and edges read cyan-green.
                half edge = saturate(path / max(_Thickness * 4, 1e-3));
                half3 tint = lerp(_BaseColor.rgb, _EdgeTint.rgb, edge);
                half3 transmission = background * AeroBodyTint(tint, path, _Density) * (1 - fresnel);

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 reflection = AeroSpecularIndirect(n, v, 1 - smoothness, input.positionWS, uv, _AeroDaylight) * fresnel * _ReflectionStrength;
                reflection += sun.color * sun.shadowAttenuation * GlassSpecular(n, v, sun.direction, max(roughness, .002)) * .25;
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS; inputData.normalizedScreenSpaceUV = uv;
                    uint count = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                        reflection += light.color * light.distanceAttenuation * light.shadowAttenuation * GlassSpecular(n, v, light.direction, max(roughness, .002)) * .25;
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(count)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                        reflection += light.color * light.distanceAttenuation * light.shadowAttenuation * GlassSpecular(n, v, light.direction, max(roughness, .002)) * .25;
                    LIGHT_LOOP_END
                #endif
                half daylight = lerp(.25, 1, saturate(_AeroDaylight));
                reflection += AeroGlassRim(n, v, _RimColor.rgb, _RimPower, _RimStrength * .35) * daylight;
                reflection += AeroThinFilm3(cosV, 380 + 160 * sin(dot(input.positionWS, half3(1.3, .7, 1.1))), 1.4, 1.5) * _FilmStrength * daylight;
                half3 color = transmission + MixFogColor(reflection, half3(0, 0, 0), input.fog);
                return half4(min(color, HALF_MAX), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
