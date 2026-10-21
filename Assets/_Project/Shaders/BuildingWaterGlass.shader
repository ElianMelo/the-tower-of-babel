Shader "TowerOfBabel/Buildings/Water Glass"
{
    Properties
    {
        _BaseMap("Original palette / detail mask", 2D) = "white" {}
        _BaseColor("Milky white glass", Color) = (0.91, 0.965, 1, 1)
        _DetailTint("Soft blue inlays", Color) = (0.48, 0.75, 1, 1)
        _DetailBlend("Inlay tint strength", Range(0,1)) = 0.32
        _WaterTint("Water inside the glass", Color) = (0.035, 0.36, 1, 1)
        _CausticsMap("Seamless water caustics", 2D) = "gray" {}
        _CausticScale("Caustic scale per metre", Range(0.05,3)) = 0.26
        _CausticIntensity("Internal water visibility", Range(0,2)) = 1.15
        _FlowSpeed("Water flow speed", Range(0,2)) = 0.7
        _WaterDepth("Internal layer depth", Range(0,0.5)) = 0.22
        _LayerCount("Stacked water layers", Range(2,6)) = 5
        _LayerSpread("Layer depth spread", Range(0.5,3)) = 1.6
        _WaterDensity("Water absorption density", Range(0,3)) = 0.9
        _RippleStrength("Internal ripple refraction", Range(0,0.3)) = 0.09
        _InstanceVariation("Instance pattern variation", Range(0,1)) = 1
        _PatternSeed("Model pattern seed", Float) = 0
        _FarLayerDistance("Distance for two-layer water", Float) = 70
        _Opacity("White glass opacity", Range(0.05,0.95)) = 0.36
        _EdgeOpacity("Edge opacity", Range(0,1)) = 0.78
        _Refraction("Gentle refraction", Range(0,0.04)) = 0.009
        _Frost("White diffusion", Range(0,1)) = 0.25
        _Smoothness("Surface smoothness", Range(0,1)) = 0.91
        _ReflectionStrength("Glass reflection", Range(0,2)) = 0.6
        [HideInInspector] _MotionTime("Preview clock; -1 uses engine time", Float) = -1
        [Toggle] _ZWrite("Write depth for instanced tower stability", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        // Keep the palette import workflow compatible without changing any mesh.
        [HideInInspector] _MetallicGlossMap("Legacy mask", 2D) = "white" {}
        [HideInInspector] _EmissionMap("Legacy emission", 2D) = "black" {}
        [HideInInspector] _EmissionColor("Legacy emission tint", Color) = (0,0,0,1)
        [HideInInspector] _Metallic("Legacy metallic", Float) = 0
        [HideInInspector] _SmoothnessTextureChannel("Legacy smoothness channel", Float) = 0
        [HideInInspector] _Surface("Surface", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "WaterGlass"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_CausticsMap); SAMPLER(sampler_CausticsMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _DetailTint, _WaterTint;
                half _DetailBlend, _CausticScale, _CausticIntensity, _FlowSpeed;
                half _WaterDepth, _Opacity, _EdgeOpacity, _Refraction, _Frost;
                half _Smoothness, _ReflectionStrength, _MotionTime;
                half _LayerCount, _LayerSpread, _WaterDensity, _RippleStrength, _InstanceVariation;
                float _PatternSeed, _FarLayerDistance;
                half _ZWrite, _Cull, _Metallic, _SmoothnessTextureChannel, _Surface;
                half4 _EmissionColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
                nointerpolation float4 originSeed : TEXCOORD4;
                nointerpolation float4 pattern : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(p.positionCS.z);
                // Use the transform, not SV_InstanceID: batch indices change as
                // the same building moves between the near pool and far pages.
                float3 origin = TransformObjectToWorld(float3(0,0,0));
                float3 q = frac(floor(origin*32+.5)*float3(.1031,.1030,.0973)+_PatternSeed*.137);
                q += dot(q,q.yzx+33.33);
                float3 random = frac((q.xxy+q.yxx)*q.zyx);
                float angle = random.y*6.2831853*_InstanceVariation;
                float sn,cs; sincos(angle,sn,cs);
                output.originSeed = float4(origin,random.x*_InstanceVariation);
                output.pattern = float4(sn,cs,lerp(1,.8+random.z*.45,_InstanceVariation),
                                        lerp(1,.78+frac(random.x*7.13)*.44,_InstanceVariation));
                return output;
            }

            float2 RotatePattern(float2 p,float2 rotation)
            {
                return float2(p.x*rotation.y-p.y*rotation.x,p.x*rotation.x+p.y*rotation.y);
            }

            half CausticLayer(float3 p, half3 weights, float2 rotation, float3 dx, float3 dy)
            {
                // Explicit derivatives keep distance/layer branches mip-safe.
                half x = SAMPLE_TEXTURE2D_GRAD(_CausticsMap,sampler_CausticsMap,
                    RotatePattern(p.yz,rotation),RotatePattern(dx.yz,rotation),RotatePattern(dy.yz,rotation)).r;
                half y = SAMPLE_TEXTURE2D_GRAD(_CausticsMap,sampler_CausticsMap,
                    RotatePattern(p.xz,rotation),RotatePattern(dx.xz,rotation),RotatePattern(dy.xz,rotation)).r;
                half z = SAMPLE_TEXTURE2D_GRAD(_CausticsMap,sampler_CausticsMap,
                    RotatePattern(p.xy,rotation),RotatePattern(dx.xy,rotation),RotatePattern(dy.xy,rotation)).r;
                return dot(half3(x,y,z), weights);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n = normalize(input.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half ndv = saturate(dot(n,v));
                half fresnel = pow(1-ndv, 3.0);
                float seed = input.originSeed.w;
                float t = (_MotionTime < 0 ? _Time.y : _MotionTime)*_FlowSpeed*input.pattern.w+seed*83.1;

                // View-dependent depth offset separates the moving water from its
                // fixed glass shell. Triplanar mapping works with the kit's palette UVs.
                half3 weights = pow(abs(n),4);
                weights /= max(dot(weights,half3(1,1,1)), .001);
                float3 localWater = input.positionWS-input.originSeed.xyz;
                float scale = _CausticScale*input.pattern.z;
                float3 patternOffset = float3(seed,frac(seed*3.71),frac(seed*7.13))*11;
                float3 waveGradient = sin(localWater.zxy*2.3+t*.63)
                                    + cos(localWater.yzx*3.1-t*.47)*.45;
                waveGradient -= n*dot(waveGradient,n);
                half3 waterNormal = normalize(n+waveGradient*_RippleStrength);
                // Snell refraction and depth-dependent absorption are informed by
                // tuxalin/water-shader; implementation is specific to this URP kit.
                float3 insideRay = refract(-v,waterNormal,1.0/1.333);
                float pathLength = _WaterDepth*_LayerSpread/max(dot(-insideRay,n),.35);
                float3 dx = ddx(localWater)*scale;
                float3 dy = ddy(localWater)*scale;
                half layerCount = clamp(round(_LayerCount),2,6);
                float distanceToPiece = distance(GetCameraPositionWS(),input.originSeed.xyz);
                layerCount = distanceToPiece > _FarLayerDistance ? 2 :
                             (distanceToPiece > _FarLayerDistance*.6 ? min(layerCount,3) : layerCount);
                half3 innerLight = 0;
                half innerTransmission = 1;
                half waterActivity = 0;
                [unroll] for(int layer=0;layer<6;layer++)
                {
                    if(layer < layerCount)
                    {
                        float depth = (layer+.5)/layerCount;
                        float layerScale = 1+layer*.19;
                        float3 waterPosition = (localWater+insideRay*pathLength*depth)*scale;
                        float direction = (layer%2==0) ? 1 : -1;
                        float3 flow = float3(.029,-.018,.024)*t*direction*(1+layer*.12);
                        float3 warp = sin(waterPosition.zxy*4.7+t*.42+layer*1.7)*.055
                                    + cos(waterPosition.yzx*3.3-t*.36-layer)*.028;
                        float3 p = waterPosition*layerScale+patternOffset+flow+warp
                                 +float3(.173,.337,.219)*layer;
                        // Deeper layers are softer, while the front lenses stay crisp.
                        float blur = layerScale*(1+depth*1.6);
                        half field = CausticLayer(p,weights,input.pattern.xy,dx*blur,dy*blur);
                        half lens = smoothstep(.018,.42,field);
                        half focus = smoothstep(.32,.94,field);
                        half coverage = saturate((.02+lens*.58)*_CausticIntensity*2/layerCount);
                        coverage *= exp(-depth*_WaterDensity*.45)*(1-fresnel*.55);
                        half3 extinction = exp(-half3(1.8,.55,.16)*depth*pathLength*_WaterDensity);
                        half3 waterColor = lerp(_WaterTint.rgb*.78,half3(.16,.76,1),focus)*extinction;
                        half3 lightCore = half3(.22,.68,1)*focus*focus*.16*_CausticIntensity/layerCount;
                        innerLight += innerTransmission*(waterColor*coverage+lightCore);
                        innerTransmission *= 1-coverage;
                        waterActivity += field/layerCount;
                    }
                }

                half3 palette = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb;
                half detail = saturate((.90-dot(palette,half3(.2126,.7152,.0722)))*1.5);
                half3 tint = lerp(_BaseColor.rgb,_DetailTint.rgb,detail*_DetailBlend);

                Light light = GetMainLight();
                half diffuse = saturate(dot(n,light.direction));
                half3 ambient = max(SampleSH(n),half3(.22,.26,.32));
                half3 litWhite = tint*(ambient*.58+light.color*(.32+.5*diffuse));
                half3 reflectedDirection = reflect(-v,n);
                half roughness = 1-_Smoothness;
                half3 environment = GlossyEnvironmentReflection(reflectedDirection,roughness,1);
                half3 h = normalize(v+light.direction);
                half specular = pow(saturate(dot(n,h)),lerp(48,220,_Smoothness));
                // Broad softbox-like white reflection gives rounded glass a soft gloss.
                half softSheen = pow(saturate(dot(reflectedDirection,normalize(half3(-.45,.8,-.35)))),20);
                half3 surface = litWhite + environment*_ReflectionStrength*(.12+fresnel*.72);
                surface += (specular*.85+softSheen*.32)*half3(.90,.97,1);
                half physicalFresnel = .04+.96*pow(1-ndv,5);
                surface += physicalFresnel*.24*half3(.8,.94,1);
                surface = lerp(surface,tint,.16*_Frost);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 lensRay = refract(-v,waterNormal,1.0/1.45);
                float2 lensOffset = TransformWorldToViewDir(lensRay+v).xy;
                float2 edge = min(screenUV,1-screenUV);
                float edgeFade = saturate(min(edge.x,edge.y)*24);
                float2 offset = (lensOffset+waveGradient.xy*.08)*_Refraction*edgeFade;
                float2 refractedUV = saturate(screenUV+offset);
                float rawDepth = SampleSceneDepth(refractedUV);
                float opaqueDepth = unity_OrthoParams.w>.5 ? LinearDepthToEyeDepth(rawDepth)
                                                          : LinearEyeDepth(rawDepth,_ZBufferParams);
                float surfaceDepth = -TransformWorldToView(input.positionWS).z;
                // Keep foreground objects from smearing into the glass silhouette.
                refractedUV = opaqueDepth < surfaceDepth ? screenUV : refractedUV;
                half3 background = SampleSceneColor(refractedUV);
                half3 original = SampleSceneColor(screenUV);
                half glassAlpha = saturate(lerp(_Opacity,_EdgeOpacity,fresnel)+detail*.035);
                half transmission = 1-glassAlpha;
                half alpha = glassAlpha+(1-innerTransmission)*transmission;
                half3 premultiplied = surface*glassAlpha
                                    +innerLight*transmission
                                    +(background-original)*(1-alpha)*(.8-_Frost*.5);
                premultiplied = MixFogColor(premultiplied,unity_FogColor.rgb*alpha,input.fog);
                return half4(premultiplied,alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
