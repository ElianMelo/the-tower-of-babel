Shader "TowerOfBabel/Buildings/Glass water floor"
{
    Properties
    {
        _CausticsTex("Stylized Water 3 caustics",2D)="white"{}
        _Environment("Aquatic reflection",Cube)=""{}
        _WaterColor("Water absorption",Color)=(.035,.47,.65,1)
        _GlassTint("Glass edge",Color)=(.45,.84,.98,1)
        _Smoothness("Polish",Range(0,1))=.96
        _MarbleMap("Surrounding marble",2D)="white"{}
        [Normal] _NormalMap("Marble relief",2D)="bump"{}
        _BaseColor("Marble color",Color)=(1,1,1,1)
        _VeinStrength("Marble veins",Range(0,1))=.1
        _NormalStrength("Marble relief strength",Float)=.12
        _MarbleScale("Marble repeats per metre",Float)=.28
        _TextureAspect("Marble texture aspect",Float)=1.5
        _MarbleSmoothness("Marble polish",Range(0,1))=.88
        _PearlCoat("Marble clear coat",Range(0,1))=.55
        _FlowSpeed("Flow speed",Float)=.045
        _Refraction("Refraction",Range(0,.03))=.007
        _RiverLayout("Base radius, setback, floor height, channel half width",Vector)=(96,3,6,1.3)
        _RadialFloor("Radial floor",Float)=0
        _RadialFloorCenter("Floor center",Vector)=(0,0,0,0)
        [PerRendererData] _BuildMode("Build mode",Float)=0
        [PerRendererData] _BuildReveal("Build reveal",Float)=1
        [PerRendererData] _BuildClock("Build clock",Float)=0
    }
    SubShader
    {
        // The sealed water bed is opaque, so stacked floors occlude correctly. The glass top
        // optically blends the lit water with the environment through Fresnel reflection.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #define _CLEARCOAT 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Stylized Water 3/Shaders/Libraries/Caustics.hlsl"
        TEXTURECUBE(_Environment); SAMPLER(sampler_Environment);
        TEXTURE2D(_MarbleMap); SAMPLER(sampler_MarbleMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        float _AeroDaylight;
        CBUFFER_START(UnityPerMaterial)
        half4 _WaterColor,_GlassTint; float _Smoothness,_FlowSpeed,_Refraction,_RadialFloor; float4 _RadialFloorCenter,_RiverLayout;
        half4 _BaseColor; half _VeinStrength,_NormalStrength,_MarbleSmoothness,_PearlCoat;
        float _MarbleScale,_TextureAspect;
        CBUFFER_END
        #include "BuildingMarbleSurface.hlsl"
        UNITY_INSTANCING_BUFFER_START(Construction)
        UNITY_DEFINE_INSTANCED_PROP(float,_BuildMode)
        UNITY_DEFINE_INSTANCED_PROP(float,_BuildReveal)
        UNITY_DEFINE_INSTANCED_PROP(float,_BuildClock)
        UNITY_INSTANCING_BUFFER_END(Construction)
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; float3 floorPatch:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS:SV_POSITION; float3 ws:TEXCOORD0; float3 normal:TEXCOORD1; float3 local:TEXCOORD2; float fog:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
        #include "BuildingRadialFloor.hlsl"
        Varyings Vert(Attributes i)
        {
            Varyings o=(Varyings)0; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.local=_RadialFloor>1.5?i.floorPatch:i.positionOS.xyz; BendRadialFloor(i);
            o.ws=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.ws); o.normal=TransformObjectToWorldNormal(i.normalOS); o.fog=ComputeFogFactor(o.positionCS.z); return o;
        }
        half Reveal(Varyings i)
        {
            float mode=UNITY_ACCESS_INSTANCED_PROP(Construction,_BuildMode);
            if(mode<.5)return 0;
            float reveal=UNITY_ACCESS_INSTANCED_PROP(Construction,_BuildReveal);
            float noise=frac(sin(dot(floor(i.local*18),float3(17.2,42.6,93.1)))*42731);
            float d=reveal*1.12-.06-noise; clip(d); return 1-smoothstep(0,.07,d);
        }
        ENDHLSL
        Pass
        {
            Name "Glass over flowing water" Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); half build=Reveal(i);
                float2 p=i.ws.xz; float t=_Time.y*_FlowSpeed;
                // A continuous world-space field crosses tile boundaries without per-tile resets.
                float2 warp=float2(sin(p.y*.64+t*3)+sin(p.x*.4-t*2),cos(p.x*.55+t*2.5))*.035;
                half3 n=normalize(i.normal+float3(warp.x,0,warp.y)*saturate(i.normal.y)*.85);
                half3 view=GetWorldSpaceNormalizeViewDir(i.ws);
                float2 causticUV=p+warp-view.xz/max(abs(view.y),.25)*(_Refraction*12);
                half3 focused=SampleCaustics(causticUV,float2(t,t*.73),.42,.45);
                half fresnel=.035+.965*pow(1-saturate(dot(n,view)),5);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.ws));
                half3 reflected=min(SAMPLE_TEXTURECUBE_LOD(_Environment,sampler_Environment,reflect(-view,n),lerp(4,.5,_Smoothness)).rgb,3);
                reflected*=lerp(.08,1,saturate(_AeroDaylight));
                half3 halfway=SafeNormalize(view+sun.direction);
                half spec=pow(saturate(dot(n,halfway)),380)*sun.shadowAttenuation;
                half3 bed=lerp(_WaterColor.rgb,_GlassTint.rgb,focused.r*.32)+focused*.32;
                half3 light=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation*.65;
                half3 col=lerp(bed*(light+.25),reflected,fresnel*.7)+sun.color*spec*1.7;
                col+=focused*.055+build*half3(.1,2,3);
                float floorIndex=max(0,floor((i.ws.y+.015)/_RiverLayout.z));
                float wall=max(3,_RiverLayout.x-_RiverLayout.y*(floor(floorIndex*.5)+1));
                float2 relative=p-_RadialFloorCenter.xy;
                float radius=length(relative); float width=min(_RiverLayout.w,max(.55,wall*.014));
                float ringDistance=min(abs(radius-wall*.5),abs(radius-wall*.88));
                float spokeDistance=min(abs(relative.x),abs(relative.y));
                float distanceToChannel=min(ringDistance,spokeDistance);
                float channelEdge=max(min(distanceToChannel-width,radius-wall*.16),radius-wall*.96);
                half channel=1-smoothstep(-.025,.025,channelEdge);
                half rim=1-smoothstep(.01,.035,abs(channelEdge));
                col=lerp(col,_GlassTint.rgb*(light+.22),rim*.25);
                if(channel<.999)
                {
                    // The white remainder is the same world-space marble and PBR lighting
                    // as the neighbouring floor, rather than a bright per-tile frame.
                    half3 normal=normalize(i.normal); float3 marblePosition=i.ws*_MarbleScale;
                    half stone=lerp(1,Marble(marblePosition,normal),_VeinStrength);
                    normal=MarbleNormal(marblePosition,normal);
                    SurfaceData surface=(SurfaceData)0;
                    surface.albedo=stone*_BaseColor.rgb;surface.alpha=1;surface.specular=half3(.04,.04,.04);
                    surface.smoothness=_MarbleSmoothness;surface.normalTS=half3(0,0,1);surface.occlusion=1;
                    surface.clearCoatMask=_PearlCoat;surface.clearCoatSmoothness=.95;
                    surface.emission=build*half3(.1,2,3);
                    InputData data=(InputData)0;
                    data.positionWS=i.ws;data.positionCS=i.positionCS;data.normalWS=normal;data.viewDirectionWS=view;
                    data.shadowCoord=TransformWorldToShadowCoord(i.ws);data.bakedGI=SampleSH(normal);data.shadowMask=half4(1,1,1,1);
                    data.vertexLighting=VertexLighting(i.ws,normal);data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                    col=lerp(UniversalFragmentPBR(data,surface).rgb,col,channel);
                }
                return half4(MixFog(max(col,0),i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" } ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            half4 Depth(Varyings i):SV_Target { UNITY_SETUP_INSTANCE_ID(i); Reveal(i); return i.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" } ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            half4 Depth(Varyings i):SV_Target { UNITY_SETUP_INSTANCE_ID(i); Reveal(i); return 0; }
            ENDHLSL
        }
    }
}
