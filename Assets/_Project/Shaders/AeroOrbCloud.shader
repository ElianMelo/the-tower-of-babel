Shader "TowerOfBabel/Resources/Orb Cloud"
{
    // The cloud inside the Silk Turbine's glass orb (OrbCloud and the CloudMass that fills during work). The mesh is a
    // fluffy cumulus from Tools/BuildingAero/build_sky_workshop.py whose vertex colours are painted like a Frutiger
    // cloud: rgb is the albedo (lilac-blue underneath, white through the middle, warm cream on the sunlit top), alpha
    // the glow weight (the top glows most). Lighting is soft: wrapped sun diffuse, ambient, a silver rim and an inner
    // glow, so the cloud reads as lit from within behind the glass. It also billows: each vertex swells and settles
    // along its normal in three slow, crossed waves (under 0.2 Hz), so the lobes churn gently without ever jumping.
    Properties
    {
        [HDR]_GlowColor("Inner glow colour",Color)=(1,.97,.93,1)
        _Glow("Inner glow",Range(0,2))=.7
        _RimColor("Rim colour",Color)=(.92,.98,1,1)
        _Rim("Rim light (silver lining)",Range(0,2))=1
        _Wrap("Soft wrap lighting",Range(0,1))=.45
        _Billow("Billow height (m)",Range(0,.05))=.01
        _BillowSpeed("Billow speed (rad/s)",Range(0,2))=.75
        _BillowScale("Billow size (waves per m)",Range(1,60))=16
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _GlowColor,_RimColor;
            half _Glow,_Rim,_Wrap;
            float _Billow,_BillowSpeed,_BillowScale;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
            half4 color:COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            half4 color:COLOR;
            half fog:TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        /// Billow offset along the normal, in object space so the pattern turns with the cloud. Three crossed sine
        /// waves with incommensurate rates; the top (alpha) billows most and the flat base hardly at all.
        float BillowOffset(float3 p,float weight)
        {
            float t=_Time.y*_BillowSpeed;
            float b=sin(dot(p,float3(1,.7,.4))*_BillowScale+t)*.5
                   +sin(dot(p,float3(-.6,1.1,.8))*_BillowScale*1.37-t*1.21)*.3
                   +sin(dot(p,float3(.3,-.9,1.2))*_BillowScale*.71+t*.73)*.2;
            return b*_Billow*(.3+.7*weight);
        }

        Varyings Vert(Attributes input)
        {
            Varyings o=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 p=input.positionOS.xyz+input.normalOS*BillowOffset(input.positionOS.xyz,input.color.a);
            o.positionWS=TransformObjectToWorld(p);
            o.normalWS=TransformObjectToWorldNormal(input.normalOS);
            o.positionCS=TransformWorldToHClip(o.positionWS);
            o.color=input.color;
            o.fog=ComputeFogFactor(o.positionCS.z);
            return o;
        }

        half4 Frag(Varyings input):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            half3 n=normalize(input.normalWS);
            half3 v=GetWorldSpaceNormalizeViewDir(input.positionWS);
            // The painted colours are stored as sRGB in the vertex colour; light them in linear space.
            half3 albedo=input.color.rgb;
            #if !defined(UNITY_COLORSPACE_GAMMA)
                albedo=pow(max(albedo,0),2.2);
            #endif
            Light sun=GetMainLight();
            half wrap=saturate((dot(n,sun.direction)+_Wrap)/(1+_Wrap));
            half3 color=albedo*(sun.color*wrap*.85+SampleSH(n));
            half rim=pow(1-saturate(dot(n,v)),3);
            color+=_RimColor.rgb*rim*_Rim;
            color+=albedo*_GlowColor.rgb*_Glow*(.4+.6*input.color.a);
            color=MixFog(color,input.fog);
            return half4(color,1);
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma target 3.5
            #pragma multi_compile_instancing
            half4 DepthFrag(Varyings input):SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFrag(Varyings input):SV_Target
            {
                float3 normal=normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal)*.5+.5)),0);
                #else
                    return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
    }
}
