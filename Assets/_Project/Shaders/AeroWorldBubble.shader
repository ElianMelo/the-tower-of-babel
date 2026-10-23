Shader "TowerOfBabel/World/Experience Bubble"
{
    Properties
    {
        _Opacity("Opacity",Range(0,1))=1
        _Phase("Thin film phase",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            UNITY_INSTANCING_BUFFER_START(Bubbles)
                UNITY_DEFINE_INSTANCED_PROP(float, _Opacity)
                UNITY_DEFINE_INSTANCED_PROP(float, _Phase)
            UNITY_INSTANCING_BUFFER_END(Bubbles)
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normalWS),v=GetWorldSpaceNormalizeViewDir(i.positionWS);
                float facing=saturate(dot(n,v));
                float rim=pow(1-facing,2.6);
                float phase=UNITY_ACCESS_INSTANCED_PROP(Bubbles,_Phase);
                float3 film=.68+.28*cos(facing*11+n.y*2.5+phase+float3(0,2.1,4.2));
                film=lerp(film,float3(.42,.88,1),saturate(-n.y)*.45);
                float3 reflected=reflect(-v,n);
                float shine=pow(saturate(dot(reflected,normalize(float3(-.5,.85,-.3)))),28);
                float secondary=pow(saturate(dot(reflected,normalize(float3(.7,.3,.6)))),48)*.6;
                float alpha=(.045+rim*.72+shine*.85+secondary*.65)*UNITY_ACCESS_INSTANCED_PROP(Bubbles,_Opacity);
                return half4(lerp(film,1,saturate(shine+secondary)),saturate(alpha));
            }
            ENDHLSL
        }
    }
}
