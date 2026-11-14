// Procedural sky with a soft sun: the atmosphere of Unity's built-in Skybox/Procedural, with its sun disk replaced by
// the shared glow model (Aero/AeroSunGlow.hlsl): a bright inner circle melting into a warm glow and a wide halo.
// The Rayleigh/Mie atmosphere below is ported from Unity's built-in shader source (Skybox-Procedural.shader,
// Copyright (c) 2016 Unity Technologies, MIT licence) with the same constants and property names, so the existing
// sky material keeps its colours. The sun direction comes from _AeroSunGlowDir (AeroDayNightCycle), falling back to
// URP's main light when no cycle has run.
Shader "Tower of Babel/Sky/Sun Glow Sky"
{
    Properties
    {
        _SkyTint ("Sky Tint", Color) = (.5, .5, .5, 1)
        _GroundColor ("Ground", Color) = (.369, .349, .341, 1)
        _AtmosphereThickness ("Atmosphere Thickness", Range(0, 5)) = 1.0
        _Exposure ("Exposure", Range(0, 8)) = 1.3
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            Name "Sun glow sky"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Aero/AeroSunGlow.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half3 _SkyTint;
                half3 _GroundColor;
                half _AtmosphereThickness;
                half _Exposure;
            CBUFFER_END

            // The project renders in linear colour space: colours arrive linear; the tint is read in gamma.
            #define AERO_GAMMA 2.2
            static const float3 kDefaultScatteringWavelength = float3(.65, .57, .475);
            static const float3 kVariableRangeForScatteringWavelength = float3(.15, .15, .15);
            #define OUTER_RADIUS 1.025
            static const float kOuterRadius = OUTER_RADIUS;
            static const float kOuterRadius2 = OUTER_RADIUS * OUTER_RADIUS;
            static const float kInnerRadius = 1.0;
            static const float kInnerRadius2 = 1.0;
            static const float kCameraHeight = 0.0001;
            #define kRAYLEIGH (lerp(0.0, 0.0025, pow(max(_AtmosphereThickness, 0), 2.5)))
            #define kMIE 0.0010
            #define kSUN_BRIGHTNESS 20.0
            #define kMAX_SCATTER 50.0
            static const float kKmESun = kMIE * kSUN_BRIGHTNESS;
            static const float kKm4PI = kMIE * 4.0 * 3.14159265;
            static const float kScale = 1.0 / (OUTER_RADIUS - 1.0);
            static const float kScaleDepth = 0.25;
            static const float kScaleOverScaleDepth = (1.0 / (OUTER_RADIUS - 1.0)) / 0.25;
            #define SKY_GROUND_THRESHOLD 0.02

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 eyeRay : TEXCOORD0;
                half3 groundColor : TEXCOORD1;
                half3 skyColor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 SunDirection()
            {
                return _AeroSunGlowDir.w > .5 ? _AeroSunGlowDir.xyz : _MainLightPosition.xyz;
            }

            half RayleighPhase(half3 light, half3 ray)
            {
                half eyeCos = dot(light, ray);
                return 0.75 + 0.75 * eyeCos * eyeCos;
            }

            float Scale(float inCos)
            {
                float x = 1.0 - inCos;
                return 0.25 * exp(-0.00287 + x * (0.459 + x * (3.83 + x * (-6.80 + x * 5.25))));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 lightDir = SunDirection();

                float3 skyTintInGammaSpace = pow(max(_SkyTint, 0), 1.0 / AERO_GAMMA);
                float3 scatteringWavelength = lerp(kDefaultScatteringWavelength - kVariableRangeForScatteringWavelength,
                    kDefaultScatteringWavelength + kVariableRangeForScatteringWavelength, half3(1, 1, 1) - skyTintInGammaSpace);
                float3 invWavelength = 1.0 / pow(max(scatteringWavelength, 1e-3), 4);
                float krESun = kRAYLEIGH * kSUN_BRIGHTNESS;
                float kr4PI = kRAYLEIGH * 4.0 * 3.14159265;
                float3 cameraPos = float3(0, kInnerRadius + kCameraHeight, 0);
                float3 eyeRay = normalize(TransformObjectToWorldDir(input.positionOS.xyz, false));
                half3 cIn, cOut;

                if (eyeRay.y >= 0.0)
                {
                    // Sky
                    float far = sqrt(kOuterRadius2 + kInnerRadius2 * eyeRay.y * eyeRay.y - kInnerRadius2) - kInnerRadius * eyeRay.y;
                    float height = kInnerRadius + kCameraHeight;
                    float depth = exp(kScaleOverScaleDepth * (-kCameraHeight));
                    float startAngle = dot(eyeRay, cameraPos) / height;
                    float startOffset = depth * Scale(startAngle);
                    float sampleLength = far / 2.0;
                    float scaledLength = sampleLength * kScale;
                    float3 sampleRay = eyeRay * sampleLength;
                    float3 samplePoint = cameraPos + sampleRay * 0.5;
                    float3 frontColor = 0;
                    [unroll] for (int i = 0; i < 2; i++)
                    {
                        float sampleHeight = length(samplePoint);
                        float sampleDepth = exp(kScaleOverScaleDepth * (kInnerRadius - sampleHeight));
                        float lightAngle = dot(lightDir, samplePoint) / sampleHeight;
                        float cameraAngle = dot(eyeRay, samplePoint) / sampleHeight;
                        float scatter = startOffset + sampleDepth * (Scale(lightAngle) - Scale(cameraAngle));
                        float3 attenuate = exp(-clamp(scatter, 0.0, kMAX_SCATTER) * (invWavelength * kr4PI + kKm4PI));
                        frontColor += attenuate * (sampleDepth * scaledLength);
                        samplePoint += sampleRay;
                    }
                    cIn = frontColor * (invWavelength * krESun);
                    cOut = frontColor * kKmESun;
                }
                else
                {
                    // Ground
                    float far = (-kCameraHeight) / (min(-0.001, eyeRay.y));
                    float3 pos = cameraPos + far * eyeRay;
                    float depth = exp((-kCameraHeight) * (1.0 / kScaleDepth));
                    float cameraAngle = dot(-eyeRay, pos);
                    float lightAngle = dot(lightDir, pos);
                    float cameraScale = Scale(cameraAngle);
                    float lightScale = Scale(lightAngle);
                    float cameraOffset = depth * cameraScale;
                    float temp = lightScale + cameraScale;
                    float sampleLength = far / 2.0;
                    float scaledLength = sampleLength * kScale;
                    float3 sampleRay = eyeRay * sampleLength;
                    float3 samplePoint = cameraPos + sampleRay * 0.5;
                    float sampleHeight = length(samplePoint);
                    float sampleDepth = exp(kScaleOverScaleDepth * (kInnerRadius - sampleHeight));
                    float scatter = sampleDepth * temp - cameraOffset;
                    float3 attenuate = exp(-clamp(scatter, 0.0, kMAX_SCATTER) * (invWavelength * kr4PI + kKm4PI));
                    float3 frontColor = attenuate * (sampleDepth * scaledLength);
                    cIn = frontColor * (invWavelength * krESun + kKmESun);
                    cOut = clamp(attenuate, 0.0, 1.0);
                }

                output.eyeRay = eyeRay;
                output.groundColor = _Exposure * (cIn + _GroundColor * cOut);
                output.skyColor = _Exposure * (cIn * RayleighPhase(lightDir, -eyeRay));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 ray = normalize(input.eyeRay);
                // Above the horizon is sky, a thin band blends into the ground colour below it.
                half y = -ray.y / SKY_GROUND_THRESHOLD;
                half3 color = lerp(input.skyColor, input.groundColor, saturate(y));
                // The soft sun, evaluated per pixel so its edge and halo never band across the sky mesh. Near a hazy low
                // sun the blue gives way to the glow, as in a golden-hour sky.
                // The sun's colour spreads over the whole skybox, most on its side.
                color = AeroSunGlowTintSky(color, ray) * AeroSunGlowSkyKeep(ray) + AeroSunGlowRadiance(ray, true);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
