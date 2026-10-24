Shader "Hidden/TowerOfBabel/Water Projection Cookie"
{
    Properties { _CausticsTex("Stylized Water 3 caustics",2D)="white"{} }
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
            #include "Assets/Stylized Water 3/Shaders/Libraries/Caustics.hlsl"
            float _AeroCookieClock;
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A i) { V o; o.position=TransformObjectToHClip(i.vertex.xyz); o.uv=i.uv; return o; }
            half4 Frag(V i):SV_Target
            {
                float2 uv=i.uv;
                float clock=_AeroCookieClock*.038;
                float pattern=SampleCaustics(uv,float2(clock,clock*.61),1.4,.45).r;
                float rim=1-smoothstep(.55,1,length(uv-.5)*2);
                float light=(.035+pattern*.965)*rim;
                return float4(light,light,light,1);
            }
            ENDHLSL
        }
    }
}
