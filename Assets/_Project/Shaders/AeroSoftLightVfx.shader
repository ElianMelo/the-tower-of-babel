Shader "TowerOfBabel/Atmosphere/Soft light VFX"
{
    Properties { _Color("HDR tint",Color)=(1,1,1,1) _Beam("Beam shape",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off Cull Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color; float _Beam;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float eye:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i)
            {
                V o=(V)0; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(ws); o.eye=-TransformWorldToView(ws).z; o.uv=i.uv; o.color=i.color*_Color; return o;
            }
            half4 Frag(V i):SV_Target
            {
                float2 uv=UnityStereoTransformScreenSpaceTex(i.positionCS.xy/_ScaledScreenParams.xy);
                float raw=SampleSceneDepth(uv); float scene=LinearEyeDepth(raw,_ZBufferParams);
                float soft=saturate((scene-i.eye)*1.5)*saturate((i.eye-.3)*2);
                float roundShape=pow(saturate(1-length(i.uv*2-1)),2);
                float sides=pow(saturate(1-abs(i.uv.x*2-1)),2.6);
                float ends=smoothstep(0,.12,i.uv.y)*(1-smoothstep(.55,1,i.uv.y));
                float wisps=.78+.22*sin(i.uv.x*42+i.uv.y*8+_Time.y*.25);
                float shape=lerp(roundShape,sides*ends*wisps,_Beam);
                return half4(max(0,i.color.rgb),saturate(i.color.a*shape*soft));
            }
            ENDHLSL
        }
    }
}
