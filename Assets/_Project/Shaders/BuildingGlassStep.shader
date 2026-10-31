Shader "TowerOfBabel/Buildings/Glass Step"
{
    // Clear, thick glass tread (white with a slight blue). Per pixel the view ray is refracted
    // into the 0.2 m slab and traced against its bottom face and its rounded-rectangle side walls:
    //  - Rays that leave through the bottom show the refracted scene, lightly tinted (Beer-Lambert).
    //  - Rays that reach a side wall first are trapped by internal reflection and travel along the
    //    slab, so a deep aqua band with a sharp dark/bright line appears along every edge, and its
    //    width changes with the viewing angle, as in real thick glass.
    //  - Side faces and bevels show a long light path: deep aqua, streaked by repeated reflections
    //    between the top and bottom faces, with dispersion and strong Fresnel reflections.
    // The scene behind comes from URP's opaque texture; blending keeps other glass visible.
    Properties
    {
        _TintColor("Clear glass tint (white, slight blue)", Color) = (.95,.98,1,1)
        _EdgeTint("Colour after the edge light path", Color) = (.5,.85,.94,1)
        _EdgeLength("Edge light path (m)", Range(.1,4)) = 1
        _IOR("Index of refraction", Range(1,2)) = 1.5
        _Refraction("Refraction strength", Range(0,3)) = 1
        _Dispersion("Chromatic dispersion", Range(0,.2)) = .02
        _BodyBlend("Clear body coverage", Range(0,1)) = .6
        _Reflection("Reflections", Range(0,2)) = 1
        _Specular("Sun glint", Range(0,6)) = 2.5
        _EdgeLines("Edge line strength", Range(0,2)) = 1
        _ShadowDensity("Shadow density", Range(0,1)) = .3
        [HideInInspector] _SlabCenter("Slab centre (object)", Vector) = (2,-.1,-2,0)
        [HideInInspector] _SlabHalfSize("Slab half size (object)", Vector) = (2,.1,2,0)
        [HideInInspector] _CornerRadius("Corner radius (m)", Float) = .3
        [PerRendererData] _BuildMode("Incoming chunk dissolve", Float) = 0
        [PerRendererData] _BuildReveal("Construction reveal", Range(0,1)) = 1
        [PerRendererData] _BuildClock("Construction clock", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "AeroBuildingFinish.hlsl"
        float _AeroDaylight;
        CBUFFER_START(UnityPerMaterial)
            half4 _TintColor,_EdgeTint;
            half _EdgeLength,_IOR,_Refraction,_Dispersion,_BodyBlend,_Reflection,_Specular,_EdgeLines,_ShadowDensity,_Cull;
            float4 _SlabCenter,_SlabHalfSize;
            float _CornerRadius;
        CBUFFER_END
        UNITY_INSTANCING_BUFFER_START(Construction)
            UNITY_DEFINE_INSTANCED_PROP(float,_BuildMode)
            UNITY_DEFINE_INSTANCED_PROP(float,_BuildReveal)
            UNITY_DEFINE_INSTANCED_PROP(float,_BuildClock)
        UNITY_INSTANCING_BUFFER_END(Construction)
        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            float3 positionOS:TEXCOORD2;
            half3 normalOS:TEXCOORD3;
            half fog:TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings o=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
            o.positionCS=p.positionCS;o.positionWS=p.positionWS;
            o.normalWS=TransformObjectToWorldNormal(input.normalOS);
            o.positionOS=input.positionOS.xyz;o.normalOS=input.normalOS;
            o.fog=ComputeFogFactor(p.positionCS.z);
            return o;
        }
        float ConstructionHash(float3 p)
        {
            p=frac(p*.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z);
        }
        float ConstructionNoise(float3 p)
        {
            float3 cell=floor(p),f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(lerp(ConstructionHash(cell),ConstructionHash(cell+float3(1,0,0)),f.x),
                             lerp(ConstructionHash(cell+float3(0,1,0)),ConstructionHash(cell+float3(1,1,0)),f.x),f.y),
                        lerp(lerp(ConstructionHash(cell+float3(0,0,1)),ConstructionHash(cell+float3(1,0,1)),f.x),
                             lerp(ConstructionHash(cell+float3(0,1,1)),ConstructionHash(cell+float3(1,1,1)),f.x),f.y),f.z);
        }
        // Same incoming-chunk dissolve as the other building shaders; permanent pieces stay whole.
        half ClipConstruction(Varyings input)
        {
            AeroClipReflection(input.positionWS);
            float mode=UNITY_ACCESS_INSTANCED_PROP(Construction,_BuildMode);
            if (mode<.5) return 0;
            float reveal=UNITY_ACCESS_INSTANCED_PROP(Construction,_BuildReveal);
            float clock=UNITY_ACCESS_INSTANCED_PROP(Construction,_BuildClock);
            float noise=ConstructionNoise(input.positionOS*10.5+float3(5.2+clock*.2,13.8,2.1-clock*.15));
            float d=reveal*1.12-.06-noise; clip(d);
            return (1-smoothstep(0,.08,d))*step(.001,reveal)*step(reveal,.999);
        }
        ENDHLSL
        Pass
        {
            Name "GlassStepForward"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float EyeDepthAt(float2 uv)
            {
                float raw=SampleSceneDepth(uv);
                return IsPerspectiveProjection()?LinearEyeDepth(raw,_ZBufferParams):LinearDepthToEyeDepth(raw);
            }
            // Scene behind the glass, displaced by a world-space shift and split into RGB.
            half3 Behind(float2 uv,float3 shiftWS,float depth,half dispersion)
            {
                float3 shiftVS=TransformWorldToViewDir(shiftWS,false);
                float2 offset=float2(shiftVS.x*UNITY_MATRIX_P._m00,shiftVS.y*UNITY_MATRIX_P._m11)*.5/max(depth,.3);
                offset=clamp(offset,-.05,.05);
                // Never pull a foreground silhouette into the glass.
                if(EyeDepthAt(saturate(uv+offset))<depth) offset=0;
                half3 c;
                c.r=SampleSceneColor(saturate(uv+offset*(1-dispersion))).r;
                c.g=SampleSceneColor(saturate(uv+offset)).g;
                c.b=SampleSceneColor(saturate(uv+offset*(1+dispersion))).b;
                return c;
            }
            half3 Absorb(float path)
            {
                return _TintColor.rgb*pow(max(_EdgeTint.rgb,.001),path/max(_EdgeLength,.01));
            }
            half Line(float x,float width)
            {
                float w=max(width,fwidth(x)*1.5);
                return exp(-(x*x)/(w*w));
            }

            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half buildEdge=ClipConstruction(input);
                float3 positionWS=input.positionWS;
                half3 n=normalize(input.normalWS);
                half3 view=GetWorldSpaceNormalizeViewDir(positionWS);
                half3 nOS=normalize(input.normalOS);
                float2 uv=GetNormalizedScreenSpaceUV(input.positionCS);
                float depth=-TransformWorldToView(positionWS).z;

                // Slab frame in metres (works for every instance scale).
                float3 scale=float3(length(UNITY_MATRIX_M._m00_m10_m20),length(UNITY_MATRIX_M._m01_m11_m21),length(UNITY_MATRIX_M._m02_m12_m22));
                float2 p=(input.positionOS.xz-_SlabCenter.xz)*scale.xz;
                float2 halfSize=_SlabHalfSize.xz*scale.xz;
                float2 q=abs(p)-(halfSize-_CornerRadius);
                float edgeDist=max(-(length(max(q,0))+min(max(q.x,q.y),0)-_CornerRadius),0);
                float2 outward=(q.x>0&&q.y>0)?normalize(q):(q.x>q.y?float2(1,0):float2(0,1));
                outward*=float2(p.x<0?-1:1,p.y<0?-1:1);
                float3 outWS=normalize(TransformObjectToWorldDir(float3(outward.x/scale.x,0,outward.y/scale.z),false));
                float3 upWS=normalize(TransformObjectToWorldDir(float3(0,1,0),false));
                float thickness=2*_SlabHalfSize.y*scale.y;

                half facing=saturate(dot(n,view));
                half f0=(_IOR-1)/(_IOR+1);f0*=f0;
                half fresnel=f0+(1-f0)*pow(1-facing,5);
                float3 ray=refract(-view,n,rcp(_IOR));
                if(dot(ray,ray)<1e-4) ray=-view;

                half flat=step(.9,abs(nOS.y));
                float3 inward=-upWS*(nOS.y<0?-1:1);
                float down=max(dot(ray,inward),.06);
                float toFar=thickness/down;
                float across=dot(ray,outWS);
                // Signed metres between where the ray meets the far face and the side wall:
                // positive = clear exit, negative = trapped against the wall.
                float clearance=edgeDist-toFar*max(across,0);
                // At grazing views the trace becomes unstable, so the band fades to clear glass.
                half trapped=flat*step(clearance,0)*saturate((facing-.12)*5);
                half deepness=saturate(-clearance/.12);

                half3 transmitted; half alpha; float path;
                if(flat>.5)
                {
                    // Lateral displacement through the slab, much stronger once reflected by a wall.
                    float3 shift=(ray+view)*toFar*_Refraction;
                    shift=lerp(shift,(-outWS*2+(ray+view))*thickness*(1.5+deepness*2)*_Refraction,trapped);
                    path=toFar+trapped*lerp(.25,1,deepness)*_EdgeLength;
                    transmitted=Behind(uv,shift,depth,_Dispersion*(1+trapped))*Absorb(path);
                    alpha=lerp(_BodyBlend,.96,trapped);
                    // Image of the far edge: a crisp dark line with a bright line on its clear side.
                    // Faded at grazing views, where the traced edge image becomes unstable.
                    half lines=_EdgeLines*saturate((facing-.18)*4);
                    half dark=Line(clearance,.006)*lines;
                    half bright=Line(clearance-.012,.005)*lines;
                    transmitted=transmitted*(1-.75*dark)+bright*half3(.85,.97,1)*.8;
                    alpha=max(alpha,saturate((dark+bright)*.9));
                }
                else
                {
                    // Sides and bevels: light runs along the slab, bouncing between top and bottom.
                    float sideY=(input.positionOS.y-_SlabCenter.y)/max(_SlabHalfSize.y,1e-4);
                    // Irregular along the edge, like the uneven reflections in a real polished edge.
                    float wander=ConstructionNoise(float3(positionWS.xz*1.7,sideY))*1.4;
                    float bounce=sideY*1.3+dot(view,upWS)*3+wander;
                    half streak=.5+.5*cos(bounce*PI*2);
                    half stripes=lerp(.75,1.18,streak*streak);
                    path=_EdgeLength*(.55+.45*(1-facing));
                    float3 shift=(ray+view)*thickness*4*_Refraction+upWS*(streak-.5)*thickness*2;
                    transmitted=Behind(uv,shift,depth,_Dispersion*1.5)*Absorb(path)*stripes;
                    // Grazing contours read as dark outlines, like the rims of a tumbler.
                    transmitted*=lerp(.5,1,smoothstep(.0,.35,facing));
                    alpha=.97;
                }
                transmitted*=1-fresnel;

                // Reflections add light on top and never hide the scene behind.
                Light key=GetMainLight(TransformWorldToShadowCoord(positionWS));
                half3 reflected=GlossyEnvironmentReflection(reflect(-view,n),positionWS,.02h,1,uv);
                if(_AeroFinishEnabled>.5&&_AeroFinishEnvironmentReady>.5&&_AeroFinishCapturing<.5)
                    reflected=lerp(reflected,AeroEnvironment(n,view,.5,_AeroDaylight),.6);
                half3 h=SafeNormalize(key.direction+view);
                half nh=saturate(dot(n,h));
                half3 glint=key.color*key.shadowAttenuation*(pow(nh,900)*_Specular+pow(nh,90)*_Specular*.08*(1-flat));
                half3 added=reflected*fresnel*_Reflection+glint;
                added+=_TintColor.rgb*SampleSH(n)*.02;
                added+=half3(.08,.65,1)*buildEdge*2.2;
                alpha=saturate(alpha+buildEdge*.5);

                half3 color=transmitted*alpha+MixFogColor(added,half3(0,0,0),input.fog);
                return half4(color,alpha);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings o=Vert(input);
                float3 direction=_LightDirection;
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    direction=normalize(_LightPosition-o.positionWS);
                #endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,normalize(o.normalWS),direction));
                #if UNITY_REVERSED_Z
                    o.positionCS.z=min(o.positionCS.z,o.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z=max(o.positionCS.z,o.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            // Clear glass lets most light through: an ordered dither in shadow-map texels,
            // smoothed by soft shadow filtering, gives a faint shadow.
            half4 ShadowFrag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ClipConstruction(input);
                uint2 texel=uint2(input.positionCS.xy)&3;
                const float bayer[16]={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
                clip(_ShadowDensity-(bayer[texel.y*4+texel.x]+.5)/16);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
