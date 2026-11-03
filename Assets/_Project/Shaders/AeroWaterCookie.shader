Shader "Hidden/TowerOfBabel/Water Projection Cookie"
{
    // Animated spotlight cookie for the lava and aqua lamps. Two layers of a 32-frame refraction-baked caustics
    // flipbook, blended with min (sharp crossings), split slightly per colour channel like light through water,
    // inside a soft round aperture.
    Properties
    {
        _CausticsArray("Caustics flipbook", 2DArray) = "" {}
        _FrameCount("Frames", Float) = 32
        _LoopSeconds("Loop seconds", Float) = 9
        _Dispersion("Colour split", Range(0, .05)) = .018
        _Tiling("Pattern tiling", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D_ARRAY(_CausticsArray); SAMPLER(sampler_CausticsArray);
            #include "Aero/AeroCaustics.hlsl"
            float _AeroCookieClock, _FrameCount, _LoopSeconds, _Dispersion, _Tiling;
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A i) { V o; o.position=TransformObjectToHClip(i.vertex.xyz); o.uv=i.uv; return o; }

            half Channel(float2 c, float k, float t, float frame)
            {
                float2 uvA = c * k * _Tiling + float2(.013, -.009) * t;
                float2 r = float2(.799 * c.x - .602 * c.y, .602 * c.x + .799 * c.y);
                float2 uvB = r * k * _Tiling * 1.27 + float2(-.007, .011) * t + float2(.31, .17);
                return AeroLayeredFlipbook(TEXTURE2D_ARRAY_ARGS(_CausticsArray, sampler_CausticsArray), uvA, uvB, frame, _FrameCount, 0);
            }

            half4 Frag(V i):SV_Target
            {
                float2 c = i.uv - .5;
                float t = _AeroCookieClock;
                float frame = t / max(_LoopSeconds, .1) * _FrameCount;
                half3 pattern = half3(Channel(c, 1 + _Dispersion, t, frame), Channel(c, 1, t, frame), Channel(c, 1 - _Dispersion, t, frame));
                half rim = 1 - smoothstep(.6, 1, length(c) * 2);
                return half4((.04 + pattern * .96) * rim, 1);
            }
            ENDHLSL
        }
    }
}
