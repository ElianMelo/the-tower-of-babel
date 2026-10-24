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
                // At a typical 3m receiver distance this spans about ten world metres.
                float2 p=(i.uv-.5)*10+_CookieOrigin.xz;
                float clock=_RiverClock*_FlowSpeed;
                half3 first=SAMPLE_TEXTURE2D(_CausticsTex,sampler_CausticsTex,p*.18+float2(clock,clock*.73)).rgb;
                half3 second=SAMPLE_TEXTURE2D(_CausticsTex,sampler_CausticsTex,p*.12-float2(clock*.61,clock)).rgb;
                // Preserve complete branching ridges. Intersecting two RGB patterns and taking
                // only red reduced the projection to isolated pinpricks instead of water light.
                half caustic=max(max(first.r,max(first.g,first.b)),max(second.r,max(second.g,second.b))*.65);
                half swell=pow(saturate(.5+.5*sin(dot(p,float2(.7,.9))-clock*2)),4);
                half aperture=1-smoothstep(.42,.5,length(i.uv-.5));
                half core=smoothstep(.025,.4,caustic);
                half value=(.025+core*1.5+swell*.025)*aperture;
                return half4(value*(.92+.08*core),value*(.98+.02*core),value,1);
            }
            ENDHLSL
        }
    }
}
