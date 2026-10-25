Shader "TowerOfBabel/Buildings/Transparent river glass"
{
    Properties
    {
        [Normal] _RippleNormal("Stylized Water 3 ripples",2D)="bump"{}
        _CausticsTex("Stylized Water 3 caustics",2D)="white"{}
        _Environment("Aquatic reflection",Cube)=""{}
        _Absorption("Absorption per metre",Color)=(.9,.22,.09,1)
        _WaterColor("Water tint",Color)=(.18,.65,.76,1)
        _GlassTint("Clear glass edges",Color)=(.55,.86,.94,1)
        _RiverOptics("Thickness, refraction, reflection, ripples",Vector)=(.14,.003,.42,.24)
        _RiverMotion("Ripple scale, flow speed, highlights, detail distance",Vector)=(.46,.045,.7,42)
        _RiverPattern("Surface pattern scale, opacity, blur",Vector)=(.16,.22,3,0)
        _RiverShell("Rim width, joint spacing, reflection, thickness",Vector)=(.045,12,.32,.02)
        _RiverEnclosure("Water top, bottom, glass thickness, side inset",Vector)=(.04,.18,.02,.02)
        _RiverLayout("Layout",Vector)=(96,3,6,1.3)
        _RadialFloor("Radial floor",Float)=0
        _RadialFloorCenter("Floor center",Vector)=(0,0,0,0)
        _RiverFloorIndex("Floor identity",Float)=0
        _RiverRefractionWeight("Refraction weight",Float)=1
        _RiverDetailWeight("Near-floor detail weight",Float)=1
        _RiverReveal("Construction reveal",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        TEXTURE2D(_RippleNormal); SAMPLER(sampler_RippleNormal);
        TEXTURE2D(_CausticsTex); SAMPLER(sampler_CausticsTex);
        TEXTURECUBE(_Environment); SAMPLER(sampler_Environment);
        TEXTURE2D_X(_RiverBackground); SAMPLER(sampler_RiverBackground);
        float _RiverClock, _AeroDaylight;
        CBUFFER_START(UnityPerMaterial)
        float4 _Absorption,_WaterColor,_GlassTint,_RiverOptics,_RiverMotion,_RiverPattern,_RiverShell,_RiverEnclosure,_RiverLayout,_RadialFloorCenter;
        float _RadialFloor,_RiverFloorIndex,_RiverRefractionWeight,_RiverReveal,_RiverDetailWeight;
        CBUFFER_END
        #include "RiverChannel.hlsl"
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float3 floorPatch:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS:SV_POSITION; float3 ws:TEXCOORD0; half3 normal:TEXCOORD1; float fog:TEXCOORD2; float3 local:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
        #include "BuildingRadialFloor.hlsl"
        Varyings Vert(Attributes i)
        {
            Varyings o=(Varyings)0; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.local=_RadialFloor>1.5?i.floorPatch:i.positionOS.xyz;
            BendRadialFloor(i); o.ws=TransformObjectToWorld(i.positionOS.xyz);
            o.positionCS=TransformWorldToHClip(o.ws);o.normal=TransformObjectToWorldNormal(i.normalOS);
            o.fog=ComputeFogFactor(o.positionCS.z);return o;
        }
        Varyings VertWater(Attributes i)
        {
            // Shift the shared 20 cm tile envelope to the inset water bounds on the GPU.
            // This keeps all 511 instances instead of allocating expanded per-batch meshes.
            i.positionOS.y=-lerp(_RiverEnclosure.x,_RiverEnclosure.y,saturate(-i.positionOS.y/.2));
            return Vert(i);
        }
        void Reveal(Varyings i)
        {
            if(_RiverReveal<.999)
            {
                float noise=frac(sin(dot(floor(i.local*18),float3(17.2,42.6,93.1)))*42731);
                clip(_RiverReveal*1.12-.06-noise);
            }
        }
        half3 Environment(half3 normal,half3 view,float lod)
        {
            return min(SAMPLE_TEXTURECUBE_LOD(_Environment,sampler_Environment,reflect(-view,normal),lod).rgb,3)*lerp(.13,1,saturate(_AeroDaylight));
        }
        half4 SimpleWater(Varyings i,half3 normal,half3 view)
        {
            float path=_RiverOptics.x/max(.15,abs(dot(view,normal)));
            half fresnel=.02+.98*pow(1-saturate(dot(normal,view)),5);
            half3 transmission=exp(-max(_Absorption.rgb,0)*path)*(1-fresnel*_RiverOptics.z);
            half alpha=saturate(1-dot(transmission,half3(.2126,.7152,.0722)));
            half3 tint=_WaterColor.rgb*alpha*.65;
            // Far layers retain clear aqua transmission without additive highlights or texture reads.
            return half4(MixFog(tint/max(alpha,.001),i.fog)*alpha,alpha);
        }
        half4 Water(Varyings i):SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);Reveal(i);
            clip(-RiverChannelDistance(i.ws.xz,_RiverFloorIndex)-_RiverEnclosure.w);
            clip(abs(i.normal.y)-.5); // Tile side faces must not become internal water partitions.
            half3 geometric=normalize(i.normal),view=GetWorldSpaceNormalizeViewDir(i.ws);
            float metres=distance(_WorldSpaceCameraPos,i.ws);
            [branch] if(_RiverDetailWeight<.001 || metres>=_RiverMotion.w*1.5)return SimpleWater(i,geometric,view);
            float t=_RiverClock*_RiverMotion.y;
            float2 p=i.ws.xz*_RiverMotion.x;
            float2 swell=float2(sin(p.y*1.4+t*2.3),cos(p.x*1.15-t*1.7))*.35;
            half3 a=UnpackNormal(SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,p+float2(t,t*.73)+swell*.12));
            half3 b=UnpackNormal(SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,p*.63-float2(t*.61,t)));
            float detail=(1-smoothstep(_RiverMotion.w,_RiverMotion.w*1.5,metres))*_RiverDetailWeight;
            half3 n=normalize(geometric+half3(a.x+b.x+swell.x,0,a.y+b.y+swell.y)*_RiverOptics.w*detail);
            float cosine=max(.15,abs(dot(view,geometric)));
            float path=_RiverOptics.x/cosine;
            half3 transmission=exp(-max(_Absorption.rgb,0)*path);
            half fresnel=.02+.98*pow(1-saturate(dot(n,view)),5);
            transmission*=1-fresnel*_RiverOptics.z;
            float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
            float2 offset=TransformWorldToViewDir(n-geometric).xy*_RiverOptics.y;
            float screenFade=saturate(min(min(uv.x,uv.y),min(1-uv.x,1-uv.y))*25);
            offset*=screenFade;
            float surfaceDepth=-TransformWorldToView(i.ws).z;
            if(_RiverRefractionWeight>.001)
            {
                float originalDepth=LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);
                float distortedDepth=LinearEyeDepth(SampleSceneDepth(saturate(uv+offset)),_ZBufferParams);
                offset*=smoothstep(0,.3,min(originalDepth,distortedDepth)-surfaceDepth);
            }
            float2 pattern=i.ws.xz*_RiverPattern.x+swell*.1-view.xz/max(abs(view.y),.25)*.09;
            // Derivative-based mips plus adjustable filtering soften the pattern with the same two reads.
            half3 first=SAMPLE_TEXTURE2D_BIAS(_CausticsTex,sampler_TrilinearRepeat,pattern+float2(t,t*.73),_RiverPattern.z).rgb;
            half3 second=SAMPLE_TEXTURE2D_BIAS(_CausticsTex,sampler_TrilinearRepeat,pattern*.6-float2(t,t*.73),_RiverPattern.z).rgb;
            half caustic=max(first.r,max(first.g,first.b))*.65+max(second.r,max(second.g,second.b))*.35;
            Light sun=GetMainLight(TransformWorldToShadowCoord(i.ws));
            half glint=pow(saturate(dot(n,SafeNormalize(view+sun.direction))),110)*sun.shadowAttenuation;
            half3 shine=(Environment(n,view,.8)*fresnel*_RiverOptics.z+sun.color*glint*.9)*detail;
            // Broad, low-contrast branches read as submerged light, rather than clipped white debris.
            half whiteCore=smoothstep(.04,.6,caustic)*_RiverMotion.z*_RiverPattern.y;
            shine+=lerp(_WaterColor.rgb,half3(1,1,1),.88)*whiteCore*lerp(.35,.85,saturate(_AeroDaylight))*detail;
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
        half4 Glass(Varyings i):SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);Reveal(i);
            clip(-RiverChannelDistance(i.ws.xz,_RiverFloorIndex));
            clip(abs(i.normal.y)-.5);
            float detail=1-smoothstep(_RiverMotion.w,_RiverMotion.w*1.5,distance(_WorldSpaceCameraPos,i.ws));
            clip(detail-.001);
            half3 n=normalize(i.normal),view=GetWorldSpaceNormalizeViewDir(i.ws);
            float2 p=i.ws.xz-_RadialFloorCenter.xy;
            float edge=abs(RiverChannelDistance(i.ws.xz,_RiverFloorIndex));
            float rim=1-smoothstep(_RiverShell.x,_RiverShell.x+max(fwidth(edge),.006),edge);
            float wall=max(3,_RiverLayout.x-_RiverLayout.y*(floor(_RiverFloorIndex*.5)+1));
            float radius=length(p),width=min(_RiverLayout.w,max(.55,wall*.014));
            float ring0=abs(radius-wall*.5),ring1=abs(radius-wall*.88);
            float ring=min(ring0,ring1),spoke=min(abs(p.x),abs(p.y));
            float along=ring<spoke ? (atan2(p.y,p.x)+PI)*(ring0<ring1?wall*.5:wall*.88) : max(abs(p.x),abs(p.y));
            float joint=abs(frac(along/max(2,_RiverShell.y)+.5)-.5)*_RiverShell.y;
            float jointMask=(1-smoothstep(.012,.025+fwidth(joint),joint))*step(width*1.8,spoke+ring)*step(wall*.17,radius);
            float2 poolJoints=abs(frac(p/max(2,_RiverShell.y))-.5)*_RiverShell.y;
            float poolJoint=min(poolJoints.x,poolJoints.y);
            jointMask=max(jointMask,(1-smoothstep(.012,.025+fwidth(poolJoint),poolJoint))*step(radius,wall*.16-width));
            float frame=max(rim,jointMask*.65);
            half fresnel=.035+.965*pow(1-saturate(dot(n,view)),5);
            half side=1-abs(n.y);
            half alpha=saturate(.012+frame*.16+side*.12+fresnel*.10);
            Light sun=GetMainLight(TransformWorldToShadowCoord(i.ws));
            half glint=pow(saturate(dot(n,SafeNormalize(view+sun.direction))),320)*sun.shadowAttenuation;
            half3 shine=Environment(n,view,.35)*fresnel*_RiverShell.z+sun.color*glint*.65;
            shine+=_GlassTint.rgb*(frame*.15+side*.05)*lerp(.2,1,saturate(_AeroDaylight));
            return half4(MixFog((shine+_GlassTint.rgb*alpha*.12)/max(alpha,.001),i.fog)*alpha,alpha)*detail;
        }
        ENDHLSL
        Pass
        {
            Name "Recessed moving water interior"
            ZWrite Off ZTest LEqual Cull Back Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertWater
            #pragma fragment Water
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "Smooth glass enclosure and polished frame"
            ZWrite Off ZTest LEqual Cull Back Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Glass
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
    }
}
