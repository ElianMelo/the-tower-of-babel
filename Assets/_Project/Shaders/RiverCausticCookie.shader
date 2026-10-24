Shader "Hidden/TowerOfBabel/River caustic cookie"
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
            float _RiverClock,_FlowSpeed; float4 _CookieOrigin;
            struct A {float4 position:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vert(A i){V o;o.position=TransformObjectToHClip(i.position.xyz);o.uv=i.uv;return o;}
            half4 Frag(V i):SV_Target
            {
                float2 p=(i.uv-.5)*8+_CookieOrigin.xz;
                float clock=_RiverClock*_FlowSpeed;
                half caustic=SampleCaustics(p,float2(clock,clock*.73),.42,.25).r;
                half swell=pow(saturate(.5+.5*sin(dot(p,float2(.7,.9))-clock*2)),4);
                half aperture=1-smoothstep(.3,.5,length(i.uv-.5));
                half value=(.025+caustic*.8+swell*.08)*aperture;
                return half4(value,value,value,1);
            }
            ENDHLSL
        }
    }
}
