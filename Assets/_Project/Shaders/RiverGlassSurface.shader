Shader "TowerOfBabel/Buildings/Transparent river glass"
{
    Properties
    {
        [Normal] _RippleNormal("Stylized Water 3 ripples",2D)="bump"{}
        _CausticsTex("Stylized Water 3 caustics",2D)="white"{}
        _Environment("Aquatic reflection",Cube)=""{}
        _Absorption("Absorption per metre",Color)=(.9,.22,.09,1)
        _WaterColor("Water tint",Color)=(.18,.65,.76,1)
        _RiverOptics("Thickness, refraction, reflection, ripples",Vector)=(.2,.003,.42,.12)
        _RiverMotion("Ripple scale, flow speed",Vector)=(.46,.045,0,0)
        _RiverLayout("Layout",Vector)=(96,3,6,1.3)
        _RadialFloor("Radial floor",Float)=0
        _RadialFloorCenter("Floor center",Vector)=(0,0,0,0)
        _RiverFloorIndex("Floor identity",Float)=0
        _RiverRefractionWeight("Refraction weight",Float)=1
        _RiverReveal("Construction reveal",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Sealed glass pool"
            ZWrite Off ZTest LEqual Cull Back
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Stylized Water 3/Shaders/Libraries/Caustics.hlsl"
            TEXTURE2D(_RippleNormal); SAMPLER(sampler_RippleNormal);
            TEXTURECUBE(_Environment); SAMPLER(sampler_Environment);
            TEXTURE2D_X(_RiverBackground); SAMPLER(sampler_RiverBackground);
            float _RiverClock, _AeroDaylight;
            CBUFFER_START(UnityPerMaterial)
            float4 _Absorption,_WaterColor,_RiverOptics,_RiverMotion,_RiverLayout,_RadialFloorCenter;
            float _RadialFloor,_RiverFloorIndex,_RiverRefractionWeight,_RiverReveal;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float3 floorPatch:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 ws:TEXCOORD0; half3 normal:TEXCOORD1; float fog:TEXCOORD2; float3 local:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            #include "BuildingRadialFloor.hlsl"
            #include "RiverChannel.hlsl"
            Varyings Vert(Attributes i)
            {
                Varyings o=(Varyings)0; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.local=_RadialFloor>1.5?i.floorPatch:i.positionOS.xyz;
                BendRadialFloor(i); o.ws=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.ws);o.normal=TransformObjectToWorldNormal(i.normalOS);
                o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if (_RiverReveal < .999)
                {
                    float noise=frac(sin(dot(floor(i.local*18),float3(17.2,42.6,93.1)))*42731);
                    clip(_RiverReveal*1.12-.06-noise);
                }
                float edge=RiverChannelDistance(i.ws.xz,_RiverFloorIndex);clip(-edge);
                // Adjacent tiles contain no visible partition: only the horizontal outer faces
                // shade water. The opaque marble supplies the real channel side walls.
                clip(abs(i.normal.y)-.5);
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float t=_RiverClock*_RiverMotion.y;float2 p=i.ws.xz*_RiverMotion.x;
                half3 a=UnpackNormal(SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,p+float2(t,t*.73)));
                half3 b=UnpackNormal(SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,p*.63-float2(t*.61,t)));
                half3 n=normalize(i.normal+half3(a.x+b.x,0,a.y+b.y)*_RiverOptics.w);
                half3 view=GetWorldSpaceNormalizeViewDir(i.ws);
                float cosine=max(.15,abs(dot(view,normalize(i.normal))));
                float path=_RiverOptics.x/cosine;
                half3 transmission=exp(-max(_Absorption.rgb,0)*path);
                half fresnel=.025+.975*pow(1-saturate(dot(n,view)),5);
                transmission*=1-fresnel*_RiverOptics.z;
                float2 offset=TransformWorldToViewDir(n).xy*_RiverOptics.y;
                float screenFade=saturate(min(min(uv.x,uv.y),min(1-uv.x,1-uv.y))*25);
                offset*=screenFade;
                // Reject foreground silhouettes rather than pulling their color into the pool.
                float sceneDepth=LinearEyeDepth(SampleSceneDepth(saturate(uv+offset)),_ZBufferParams);
                float surfaceDepth=-TransformWorldToView(i.ws).z;
                offset*=smoothstep(0,.2,sceneDepth-surfaceDepth);
                half daylight=lerp(.13,1,saturate(_AeroDaylight));
                half3 reflection=min(SAMPLE_TEXTURECUBE_LOD(_Environment,sampler_Environment,reflect(-view,n),1.2).rgb,3)*daylight;
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.ws));
                half glint=pow(saturate(dot(n,SafeNormalize(view+sun.direction))),220)*sun.shadowAttenuation;
                half3 caustics=SampleCaustics(i.ws.xz,float2(t,t*.73),.42,.25);
                half rim=(1-smoothstep(0,.075,-edge))*.07;
                half3 shine=reflection*fresnel*_RiverOptics.z+sun.color*glint*.45;
                shine+=_WaterColor.rgb*(rim+caustics*.012)*daylight;
                half opacity=saturate(1-dot(transmission,half3(.2126,.7152,.0722)));
                half3 tint=_WaterColor.rgb*opacity*.65;
                half weight=saturate(_RiverRefractionWeight);
                half3 col=tint+shine;
                if(weight>.001)
                {
                    half3 background=SAMPLE_TEXTURE2D_X(_RiverBackground,sampler_RiverBackground,saturate(uv+offset)).rgb;
                    col=lerp(col,background*transmission+tint+shine,weight);
                }
                half alpha=lerp(opacity,1,weight);
                return half4(MixFog(col/max(alpha,.001),i.fog)*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
