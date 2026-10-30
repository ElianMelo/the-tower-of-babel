Shader "TowerOfBabel/Aero/Jelly Plastic"
{
    // Clear tinted Y2K plastic (iMac/N64 "ice" casings, gel icons, glowing lamp cores):
    // a deep saturated core where the view crosses the most material, a bright rim,
    // blurred internal structure via parallax layers, wrapped light and a glossy coat.
    // Opaque, so it never needs transparent sorting.
    Properties
    {
        _BaseColor("Core color", Color) = (.05,.45,.85,1)
        _RimColor("Rim color", Color) = (.65,.92,1,1)
        _CoreDepth("Core saturation", Range(0,4)) = 1.4
        _InteriorScale("Interior structure scale", Float) = 4.5
        _InteriorDepth("Interior depth (object units)", Range(0,.5)) = .08
        _InteriorStrength("Interior structure strength", Range(0,1)) = .2
        _Smoothness("Coat smoothness", Range(0,1)) = .95
        _SubsurfaceWrap("Light wrap", Range(0,1)) = .7
        _TranslucencyStrength("Back light", Range(0,4)) = 1.2
        _RimStrength("Fresnel rim", Range(0,4)) = 1
        [Toggle(_EMISSION)] _EmissionOn("Emission", Float) = 0
        [HDR] _EmissionColor("Inner glow", Color) = (0,0,0,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _RimColor, _EmissionColor;
            half _CoreDepth, _InteriorScale, _InteriorDepth, _InteriorStrength, _Smoothness;
            half _SubsurfaceWrap, _TranslucencyStrength, _RimStrength, _EmissionOn, _Cull;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings
        {
            float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1;
            float3 positionOS:TEXCOORD2; float3 viewOS:TEXCOORD3; half fog:TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            o.positionCS = p.positionCS; o.positionWS = p.positionWS;
            o.normalWS = TransformObjectToWorldNormal(input.normalOS);
            o.positionOS = input.positionOS.xyz;
            o.viewOS = TransformWorldToObject(GetCameraPositionWS()) - input.positionOS.xyz;
            o.fog = ComputeFogFactor(p.positionCS.z);
            return o;
        }
        ENDHLSL
        Pass
        {
            Name "JellyForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Aero/AeroSubsurface.hlsl"
            #include "Aero/AeroTranslucency.hlsl"
            #include "Aero/AeroIndirect.hlsl"
            float _AeroDaylight;

            // Soft cellular structure, like moulded ribs and parts seen through clear plastic.
            half Interior(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                float nearest = 8, second = 8;
                [unroll] for (int z = -1; z <= 1; z++)
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float3 o = float3(x, y, z);
                    float3 h = frac(sin(dot(cell + o, float3(127.1, 311.7, 74.7)) * float3(1, 1.37, 1.79)) * 43758.5453);
                    float d = length(o + h - f);
                    second = d < nearest ? nearest : min(second, d);
                    nearest = min(nearest, d);
                }
                // Soft moulded ribs rather than crisp cracks.
                return smoothstep(.35, 0, second - nearest) * .8;
            }

            struct AeroPixel { half3 n, v, core, scatter; half thickness; BRDFData brdf; };
            half3 AeroShadeLight(Light light, AeroPixel px)
            {
                half NdotL = dot(px.n, light.direction);
                half3 radiance = light.color * light.distanceAttenuation * lerp(light.shadowAttenuation, 1, .4);
                half3 color = px.core * AeroSubsurfaceDiffuse(NdotL, px.scatter, _SubsurfaceWrap, .3) * radiance;
                color += px.brdf.specular * DirectBRDFSpecular(px.brdf, px.n, light.direction, px.v) * saturate(NdotL) *
                    light.color * light.distanceAttenuation * light.shadowAttenuation;
                color += AeroTranslucentLight(px.n, px.v, light.direction, light.color, light.distanceAttenuation,
                    px.scatter, .3, 3, px.thickness, _TranslucencyStrength);
                return color;
            }
            #include "Aero/AeroLightLoop.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n = normalize(input.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facingRatio = saturate(dot(n, v));
                // Longer paths through the body (facing the camera) look deeper and more saturated.
                half depth = pow(facingRatio, .6);
                half3 core = lerp(_RimColor.rgb, _BaseColor.rgb, saturate(depth * _CoreDepth));
                half luminance = dot(core, half3(.2126, .7152, .0722));
                core = lerp(luminance.xxx, core, 1 + depth * .25);

                // Blurred internal structure seen at three depths behind the surface.
                float3 dirOS = -normalize(input.viewOS);
                half structure = 0, weight = 0;
                [unroll] for (int k = 1; k <= 3; k++)
                {
                    float3 p = (input.positionOS + dirOS * _InteriorDepth * k) * _InteriorScale;
                    half w = exp(-k * .7);
                    structure += Interior(p) * w; weight += w;
                }
                structure /= weight;
                core = lerp(core, _RimColor.rgb, structure * _InteriorStrength * depth);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = core; surface.metallic = 0; surface.specular = .04; surface.smoothness = _Smoothness;
                surface.occlusion = 1; surface.alpha = 1;
                AeroPixel px = (AeroPixel)0;
                InitializeBRDFData(surface, px.brdf);
                px.n = n; px.v = v; px.core = core; px.thickness = .25 + depth * .5;
                px.scatter = saturate(_BaseColor.rgb * 1.6 + .1);

                InputData data = (InputData)0;
                data.positionWS = input.positionWS; data.positionCS = input.positionCS; data.normalWS = n; data.viewDirectionWS = v;
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS); data.shadowMask = 1;
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.bakedGI = SampleSH(n) + SampleSH(-n) * px.scatter * .3;
                AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(data, surface);
                BRDFData noCoat = (BRDFData)0;
                half3 color = AeroGlobalIllumination(px.brdf, noCoat, 0, data.bakedGI, 1, input.positionWS, n, n, v,
                    data.normalizedScreenSpaceUV, 1, _AeroDaylight);
                color += AeroShadeLight(GetMainLight(data, 1, ao), px);
                color += AeroAdditionalLights(data, 1, ao, px);
                half rim = pow(1 - facingRatio, 3);
                color += _RimColor.rgb * rim * _RimStrength * .35 * lerp(.25, 1, saturate(_AeroDaylight));
                #if defined(_EMISSION)
                    color += _EmissionColor.rgb * lerp(.55, 1, depth) * (1 + structure * _InteriorStrength * .5);
                #endif
                return half4(min(MixFog(color, input.fog), HALF_MAX), 1);
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
            float3 _LightDirection; float3 _LightPosition;
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
