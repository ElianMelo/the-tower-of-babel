Shader "TowerOfBabel/Foliage/Dandelion Seeds"
{
    // The seed clocks of the Cloud Dandelions (SkyDandelions.fbx, Seeds_<flower>). Every seed carries its umbrella
    // centre in TEXCOORD1.xy + TEXCOORD2.x (Blender object space) and a random value in TEXCOORD2.y. _Release is a
    // per-renderer value set by EssenceHarvestFeedback: 0 keeps every seed on the head; as it rises, seeds leave in a
    // wave from the downwind side, turn pappus-up, rise, drift downwind on a gentle arc and shrink away. Every seed's
    // path is one smooth curve of its own progress, so nothing oscillates or jumps. Vertex colour: rgb albedo,
    // alpha emission weight (the silky pappus glows a little, the achenes do not).
    Properties
    {
        _Smoothness("Sheen",Range(0,1))=.35
        [HDR]_EmissionColor("Glow (set per renderer by the harvest feedback)",Color)=(.38,.4,.5,1)
        _Release("Seed release: 0 on the head, 1 = the last seed leaves",Float)=0
        _Flight("Flight length, in release units",Range(.05,3))=.9
        _SeedWind("Wind direction (world) and drift distance",Vector)=(1,0,-.25,1.4)
        _Lift("Rise while flying",Range(0,2))=.5
        _Shrink("Flight fraction where seeds start to shrink away",Range(0,1))=.62
        _Backlight("Light through the pappus",Range(0,1))=.3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _EmissionColor;
            float4 _SeedWind;
            half _Smoothness,_Backlight;
            float _Release,_Flight,_Lift,_Shrink;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
            half4 color:COLOR;
            float2 pivotXY:TEXCOORD1;
            float2 info:TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            half4 color:COLOR;
            half fog:TEXCOORD2;
            half flight:TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float3 Rotate(float3 v,float3 axis,float angle)
        {
            float s=sin(angle),c=cos(angle);
            return v*c+cross(axis,v)*s+axis*dot(axis,v)*(1-c);
        }

        /// How far one seed is along its flight (0 on the head, 1 gone). Exposed for the C# mirror in the tests.
        float SeedFlight(float3 directionWS,float random,float3 wind)
        {
            // Downwind and upper seeds go first, with some randomness so the wave is not a hard line.
            float order=saturate(.5-.42*dot(directionWS,wind)-.12*directionWS.y+(random-.5)*.45);
            return saturate((_Release-order)/max(_Flight,1e-3));
        }

        void Fly(Attributes input,out float3 positionWS,out float3 normalWS,out float flight)
        {
            // Blender (x, y, z) arrives in Unity object space as (x, y, -z).
            float3 pivot=float3(input.pivotXY.x,input.pivotXY.y,-input.info.x);
            float random=input.info.y;
            float len=length(pivot);
            float3 position=input.positionOS.xyz,normal=input.normalOS;
            positionWS=TransformObjectToWorld(position);
            normalWS=TransformObjectToWorldNormal(normal);
            flight=0;
            if(len<1e-4 || _Release<=0) return;                       // loose seeds and resting heads stay exact
            float3 direction=pivot/len;
            float3 directionWS=normalize(TransformObjectToWorldDir(direction));
            float3 wind=normalize(_SeedWind.xyz+float3(0,1e-5,0));
            float r=SeedFlight(directionWS,random,wind);
            flight=r;
            if(r<=0) return;
            // Turn pappus-up about the umbrella centre, then shrink away near the end of the flight.
            float3 up=normalize(TransformWorldToObjectDir(float3(0,1,0)));
            float3 axis=cross(direction,up); float axisLength=length(axis);
            float turn=atan2(axisLength,dot(direction,up))*smoothstep(0,.55,r);
            float3 local=position-pivot;
            if(axisLength>1e-4)
            {
                axis/=axisLength;
                local=Rotate(local,axis,turn); normal=Rotate(normal,axis,turn);
            }
            local*=1-smoothstep(_Shrink,1,r);
            positionWS=TransformObjectToWorld(pivot+local);
            normalWS=TransformObjectToWorldNormal(normal);
            // Pop out a little, rise, then drift downwind faster and faster on a one-sided arc.
            float eased=r*sqrt(r);
            float3 side=normalize(cross(wind,float3(0,1,0))+float3(1e-5,0,0));
            positionWS+=directionWS*.07*smoothstep(0,.3,r);
            positionWS+=wind*_SeedWind.w*eased*(.75+.5*random);
            positionWS+=float3(0,1,0)*_Lift*(r*.8+eased*.2)*(.7+.6*frac(random*7.31));
            positionWS+=side*sin(r*PI)*.12*(frac(random*13.7)-.5)*2;
        }

        Varyings Vert(Attributes input)
        {
            Varyings o=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float flight; Fly(input,o.positionWS,o.normalWS,flight); o.flight=flight;
            o.positionCS=TransformWorldToHClip(o.positionWS);
            o.color=input.color;
            o.fog=ComputeFogFactor(o.positionCS.z);
            return o;
        }

        half4 Frag(Varyings input,bool front:SV_IsFrontFace):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            half3 n=normalize(input.normalWS)*(front ? 1 : -1);
            half3 albedo=input.color.rgb;
            InputData data=(InputData)0;
            data.positionWS=input.positionWS; data.normalWS=n;
            data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
            data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
            data.bakedGI=SampleSH(n); data.shadowMask=half4(1,1,1,1);
            data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
            data.vertexLighting=VertexLighting(input.positionWS,n);
            SurfaceData surface=(SurfaceData)0;
            surface.albedo=albedo; surface.alpha=1; surface.occlusion=1;
            surface.smoothness=_Smoothness; surface.specular=half3(.06,.06,.06);
            surface.normalTS=half3(0,0,1);
            // Airborne silk catches the light: it glows up to twice as bright while the seed flies.
            surface.emission=albedo*_EmissionColor.rgb*input.color.a*(1+input.flight*smoothstep(0,.25,input.flight)*1.2);
            half4 color=UniversalFragmentPBR(data,surface);
            // Thin pappus lets sunlight through: a soft lift on the silk, none on the achenes.
            Light sun=GetMainLight(data.shadowCoord);
            color.rgb+=albedo*sun.color*lerp(sun.shadowAttenuation,1,.5)*_Backlight*input.color.a*(.5+.5*saturate(dot(-n,sun.direction)));
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
