Shader "TowerOfBabel/Foliage/Clinging Vine"
{
    Properties
    {
        _BaseColor("Leaf tint",Color)=(1,1,1,1)
        _Smoothness("Leaf sheen",Range(0,1))=.25
        _WindStrength("Flutter strength",Range(0,.1))=.025
        _WindSpeed("Wind speed",Range(0,3))=.85
        _WindTime("Preview time; -1 uses game time",Float)=-1
        _SubsurfaceColor("Scatter color",Color)=(.75,1,.35,1)
        _SubsurfaceWrap("Scatter wrap",Range(0,1))=.45
        _TranslucencyColor("Back-lit color",Color)=(.9,1,.48,1)
        _TranslucencyStrength("Back-lit strength",Range(0,4))=1.1
        _DistanceLift("Light green at distance",Range(0,1))=.85
        _DistanceGlow("Soft glow at distance",Range(0,1))=.55
        _DistanceLiftStart("Lift starts (m)",Float)=12
        _DistanceLiftRange("Lift reaches full over (m)",Float)=45
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
            half _Smoothness,_SubsurfaceWrap,_TranslucencyStrength,_WetnessOn,_WetExposure,_DistanceLift,_DistanceGlow;
            float _WindStrength,_WindSpeed,_WindTime,_DistanceLiftStart,_DistanceLiftRange;
        CBUFFER_END
        #include "Aero/AeroFarInstancing.hlsl"
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
        // The vine clings to stone: only leaf rims and hanging strands move (vertex alpha), never the stems.
        void Wind(Attributes input,out float3 position,out float3 normal)
        {
            float3 origin=TransformObjectToWorld(float3(0,0,0));
            float phase=frac(sin(dot(origin,float3(12.9898,4.1414,78.233)))*43758.5453)*6.28318;
            float time=(_WindTime>=0 ? _WindTime : _Time.y)*_WindSpeed;
            float weight=saturate(input.color.a);
            float local=dot(input.positionOS.xyz,float3(11,7,13));
            float3 flutter=float3(sin(time*3.1+local+phase),sin(time*2.3+local*1.3+phase)*.5,cos(time*2.7+local*.7+phase))*_WindStrength*weight;
            position=TransformObjectToWorld(input.positionOS.xyz)+flutter;
            normal=normalize(TransformObjectToWorldNormal(input.normalOS)+flutter*4);
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
        // Distant instanced pages carry no light-probe SH when the scene has no probes, which left far vines
        // lit by the sun alone and read dark green. Fall back to the scene's trilight ambient instead.
        half3 VineAmbient(half3 n)
        {
            half3 sh=SampleSH(n);
            if(max(sh.r,max(sh.g,sh.b))>.0001h) return sh;
            return n.y>0 ? lerp(unity_AmbientEquator.rgb,unity_AmbientSky.rgb,n.y) : lerp(unity_AmbientEquator.rgb,unity_AmbientGround.rgb,-n.y);
        }
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
            // From afar, thin stems and shaded leaf sides blend into a dark mass. Lift the colour toward a bright,
            // airy light green with distance so the tower reads as covered in fresh growth.
            float distance=length(GetCameraPositionWS()-input.positionWS);
            half lift=saturate((distance-_DistanceLiftStart)/max(_DistanceLiftRange,1))*_DistanceLift;
            albedo=lerp(albedo,saturate(albedo*1.3+half3(.16,.24,.07)),lift);
            half smoothness=_Smoothness;
            half3 n=normalize(input.normalWS);
            // Double-sided leaves: light the side facing the camera. Works for mirrored instances too.
            half3 toView=GetWorldSpaceNormalizeViewDir(input.positionWS);
            n=dot(n,toView)<0 ? -n : n;
            #if defined(_AERO_WETNESS)
                // Dew beads on leaves; the geometric normal is the only normal here.
                AeroApplyWetness(albedo,smoothness,n,input.positionWS,n,.15,_WetExposure);
            #endif
            data.positionWS=input.positionWS; data.normalWS=n;
            data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
            data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
            data.bakedGI=VineAmbient(data.normalWS); data.shadowMask=half4(1,1,1,1);
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
            color.rgb+=VineAmbient(-n)*albedo*_TranslucencyColor.rgb*_TranslucencyStrength*.12;
            // A little self-light at distance keeps far foliage airy even under a dim ambient.
            color.rgb+=albedo*lift*_DistanceGlow;
            color.rgb=MixFog(color.rgb,input.fog);
            return color;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:AeroFarSetup
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
            ZWrite On ZTest LEqual ColorMask 0 Cull Off
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:AeroFarSetup
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
            ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:AeroFarSetup
            half4 DepthFrag(Varyings input):SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:AeroFarSetup
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
