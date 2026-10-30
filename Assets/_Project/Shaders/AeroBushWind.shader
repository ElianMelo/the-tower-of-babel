Shader "TowerOfBabel/Foliage/Buoyant Bush"
{
    Properties
    {
        _BaseColor("Leaf tint",Color)=(1,1,1,1)
        _Smoothness("Leaf sheen",Range(0,1))=.25
        _WindStrength("Bending strength",Range(0,.3))=.09
        _WindSpeed("Wind speed",Range(0,3))=.85
        _WindTime("Preview time; -1 uses game time",Float)=-1
        _SubsurfaceColor("Scatter color",Color)=(.75,1,.35,1)
        _SubsurfaceWrap("Scatter wrap",Range(0,1))=.45
        _TranslucencyColor("Back-lit color",Color)=(.9,1,.48,1)
        _TranslucencyStrength("Back-lit strength",Range(0,4))=1.1
        [Toggle(_AERO_WETNESS)] _WetnessOn("Dew and rain",Float)=0
        _WetExposure("Weather exposure",Range(0,1))=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor,_SubsurfaceColor,_TranslucencyColor;
            half _Smoothness,_SubsurfaceWrap,_TranslucencyStrength,_WetnessOn,_WetExposure;
            float _WindStrength,_WindSpeed,_WindTime;
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
        void Wind(Attributes input,out float3 position,out float3 normal)
        {
            float3 origin=TransformObjectToWorld(float3(0,0,0));
            float phase=frac(sin(dot(origin,float3(12.9898,4.1414,78.233)))*43758.5453)*6.28318;
            float time=(_WindTime>=0 ? _WindTime : _Time.y)*_WindSpeed;
            float2 flow=float2(sin(time*1.25+phase)+.3*sin(time*.68+phase*1.7),cos(time*.9+phase)*.5);
            float weight=saturate(input.color.a);
            float bend=max(0,input.positionOS.y)*weight*_WindStrength;
            float flutter=sin(time*3.2+dot(input.positionOS.xyz,float3(13,5,17))+phase)*.012*weight*weight;
            position=TransformObjectToWorld(input.positionOS.xyz);
            position.xz+=(flow*bend+float2(flutter,flutter*.6));
            normal=TransformObjectToWorldNormal(input.normalOS);
            normal.y-=dot(normal.xz,flow)*weight*_WindStrength*2;
            normal=normalize(normal);
        }
        Varyings Vert(Attributes input)
        {
            Varyings o=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            Wind(input,o.positionWS,o.normalWS);
            o.positionCS=TransformWorldToHClip(o.positionWS);
            float3 origin=TransformObjectToWorld(float3(0,0,0));
            float variation=frac(sin(dot(origin.xz,float2(41.17,73.23)))*43758.5);
            o.color=half4(input.color.rgb*_BaseColor.rgb*lerp(.88,1.1,variation),1);
            o.fog=ComputeFogFactor(o.positionCS.z);
            return o;
        }
        #include "Aero/AeroWetness.hlsl"
        #include "Aero/AeroSubsurface.hlsl"
        #include "Aero/AeroTranslucency.hlsl"
        // Leaves: wrapped lime terminator plus sunlight shining through thin blades.
        half3 LeafLight(Light light,half3 n,half3 v,half3 albedo)
        {
            half NdotL=dot(n,light.direction);
            half3 radiance=light.color*light.distanceAttenuation*AeroSubsurfaceShadow(light.shadowAttenuation,_SubsurfaceWrap);
            half3 wrapped=AeroSubsurfaceDiffuse(NdotL,_SubsurfaceColor.rgb,_SubsurfaceWrap,.2)-saturate(NdotL);
            half3 color=albedo*max(wrapped,0)*radiance;
            color+=AeroTranslucentLight(n,v,light.direction,light.color,light.distanceAttenuation*lerp(light.shadowAttenuation,1,.5),
                albedo*_TranslucencyColor.rgb,.4,4,.3,_TranslucencyStrength);
            return color;
        }
        half4 Frag(Varyings input):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            InputData data=(InputData)0;
            half3 albedo=input.color.rgb;
            half smoothness=_Smoothness;
            half3 n=normalize(input.normalWS);
            #if defined(_AERO_WETNESS)
                // Dew beads on leaves; the geometric normal is the only normal here.
                AeroApplyWetness(albedo,smoothness,n,input.positionWS,normalize(input.normalWS),.15,_WetExposure);
            #endif
            data.positionWS=input.positionWS; data.normalWS=n;
            data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
            data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
            data.bakedGI=SampleSH(data.normalWS); data.shadowMask=half4(1,1,1,1);
            data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
            data.vertexLighting=VertexLighting(input.positionWS,data.normalWS);
            SurfaceData surface=(SurfaceData)0;
            surface.albedo=albedo; surface.alpha=1; surface.occlusion=1;
            surface.smoothness=smoothness; surface.specular=half3(.08,.1,.07);
            surface.normalTS=half3(0,0,1);
            half4 color=UniversalFragmentPBR(data,surface);
            Light sun=GetMainLight(data.shadowCoord);
            color.rgb+=LeafLight(sun,n,data.viewDirectionWS,albedo);
            #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount=GetAdditionalLightsCount();
                for(uint lightIndex=0;lightIndex<lightCount;lightIndex++)
                    color.rgb+=LeafLight(GetAdditionalLight(lightIndex,input.positionWS,half4(1,1,1,1)),n,data.viewDirectionWS,albedo);
            #endif
            // Sky light filtering through the canopy from behind.
            color.rgb+=SampleSH(-n)*albedo*_TranslucencyColor.rgb*_TranslucencyStrength*.12;
            color.rgb=MixFog(color.rgb,input.fog);
            return color;
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
            #pragma shader_feature_local_fragment _AERO_WETNESS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings o=Vert(input);
                float3 lightDirection=_LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    lightDirection=normalize(_LightPosition-o.positionWS);
                #endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,lightDirection));
                #if UNITY_REVERSED_Z
                    o.positionCS.z=min(o.positionCS.z,o.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z=max(o.positionCS.z,o.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 DepthFrag(Varyings input):SV_Target { return 0; }
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
