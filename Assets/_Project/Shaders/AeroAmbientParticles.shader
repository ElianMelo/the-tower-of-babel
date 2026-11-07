// Floating seasonal particles: soft dots (pollen, dust motes, fireflies), blossom petals, leaves and snowflakes.
// Shapes are procedural so no textures ship; colour comes from the particle (HDR for glowing motes).
Shader "TowerOfBabel/Atmosphere/Ambient particles"
{
    Properties
    {
        [Enum(Dot,0,Petal,1,Leaf,2,Flake,3)] _Shape("Shape", Float) = 0
        _NearFade("Near fade distance", Float) = .6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "AmbientParticles"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _Shape; float _NearFade;
            CBUFFER_END

            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float eye:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            V Vert(A i)
            {
                V o = (V)0; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws); o.eye = -TransformWorldToView(ws).z; o.uv = i.uv; o.color = i.color;
                return o;
            }

            // Returns coverage (a) and a shading factor (rgb multiplier) for each shape.
            float2 Shape(float2 p)
            {
                float r = length(p);
                if (_Shape < .5)          // soft dot
                    return float2(pow(saturate(1 - r), 1.6), 1);
                if (_Shape < 1.5)         // petal: rounded teardrop with a small notch at the tip
                {
                    float2 q = float2(p.x * 1.55, p.y * 1.05 + .08);
                    float body = 1 - smoothstep(.78, .9, length(q) + q.y * .18);
                    float notch = smoothstep(.12, .02, abs(p.x)) * smoothstep(.62, .82, p.y);
                    return float2(body * (1 - notch), lerp(1.06, .86, saturate(r)));
                }
                if (_Shape < 2.5)         // leaf: pointed oval with a mid vein and short stem
                {
                    float halfWidth = (1 - p.y * p.y) * .5;
                    float body = 1 - smoothstep(halfWidth - .07, halfWidth, abs(p.x));
                    body *= step(-.92, p.y) * step(p.y, .92);
                    float stem = (1 - smoothstep(.02, .045, abs(p.x))) * step(-1, p.y) * step(p.y, -.8);
                    float vein = smoothstep(.05, .0, abs(p.x)) * step(-.85, p.y);
                    float side = smoothstep(.0, .6, abs(p.x));
                    return float2(max(body, stem), (1 - vein * .3) * lerp(1.05, .85, side));
                }
                // snowflake: soft core with six faint arms
                float angle = atan2(p.y, p.x);
                float arms = smoothstep(.16, .0, abs(sin(angle * 3)) * r) * (1 - smoothstep(.55, .95, r));
                float core = pow(saturate(1 - r * 1.6), 1.4);
                return float2(saturate(max(core, arms * .75)), 1);
            }

            half4 Frag(V i) : SV_Target
            {
                float2 shape = Shape(i.uv * 2 - 1);
                float2 suv = UnityStereoTransformScreenSpaceTex(i.positionCS.xy / _ScaledScreenParams.xy);
                float scene = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float soft = saturate((scene - i.eye) * 2) * saturate((i.eye - .2) / max(.01, _NearFade));
                return half4(max(0, i.color.rgb * shape.y), saturate(i.color.a * shape.x * soft));
            }
            ENDHLSL
        }
    }
}
