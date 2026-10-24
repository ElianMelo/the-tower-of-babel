Shader "Hidden/TowerOfBabel/Water Projection Cookie"
{
    Properties { _Caustics("Water light pattern",2D)="white"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _Caustics;
            float _AeroCookieClock;
            fixed4 Frag(v2f_img i):SV_Target
            {
                float2 uv=i.uv;
                float clock=_AeroCookieClock*.038;
                float2 warp=sin(uv.yx*8+clock*4)*.035;
                float a=tex2D(_Caustics,uv*.7+float2(clock,clock*.61)+warp).r;
                float b=tex2D(_Caustics,float2(uv.y,-uv.x)*.83-float2(clock*.53,clock*.9)-warp).r;
                float pattern=pow(saturate(min(a,b)*2.6),1.2);
                float rim=1-smoothstep(.55,1,length(uv-.5)*2);
                float light=(.035+pattern*.965)*rim;
                return float4(light,light,light,1);
            }
            ENDHLSL
        }
    }
}
