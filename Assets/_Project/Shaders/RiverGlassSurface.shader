Shader "TowerOfBabel/Buildings/Transparent river glass"
{
    // Water inside glass (presentation v12). Pass 0 traces each detailed pixel through the
    // real enclosure (lid, air gap, rippled surface, water, inner walls, glass bed) and
    // refracts the scene behind; pass 1 is the cheap, time-independent far-floor tint.
    Properties
    {
        [Normal] _RippleNormal("Stylized Water 3 ripples",2D)="bump"{}
        _CausticsTex("Stylized Water 3 caustics",2D)="white"{}
        _Environment("Aquatic reflection",Cube)=""{}
        _Absorption("Absorption per metre (RGB, linear)",Vector)=(4.5,1.1,.42,0)
        _WaterColor("Water tint",Color)=(.18,.65,.76,1)
        _ShallowColor("Shallow water",Color)=(.25,.83,.94,1)
        _DeepColor("Deep water",Color)=(.04,.5,.82,1)
        _WallColor("Inner wall (marble behind glass)",Color)=(.93,.96,1,1)
        _GlassTint("Clear glass edges",Color)=(.55,.86,.94,1)
        _EdgeTint("Thick glass edge",Color)=(.35,.82,.74,1)
        _RiverOptics("Thickness, refraction, reflection, ripples",Vector)=(.14,.003,.42,.24)
        _RiverMotion("Ripple scale, flow speed, highlights, detail distance",Vector)=(.46,.045,.7,42)
        _RiverPattern("Surface pattern scale, opacity, blur",Vector)=(.16,.22,3,0)
        _RiverShell("Rim width, joint spacing, reflection, thickness",Vector)=(.045,12,.32,.02)
        _RiverEnclosure("Water top, bottom, glass thickness, side inset",Vector)=(.04,.18,.02,.02)
        _RiverVolume("Water IOR, glass IOR, refraction strength, deep path",Vector)=(1.33,1.5,1,.6)
        _RiverBed("Bed tile size, etch opacity, caustic strength, meniscus",Vector)=(.6,.12,1,1)
        _RiverDetail("Bubble density, wall tint, meniscus width, inscatter",Vector)=(2.2,.35,.006,1)
        _RiverLayout("Layout",Vector)=(96,3,6,1.3)
        _RadialFloor("Radial floor",Float)=0
        _RadialFloorCenter("Floor center",Vector)=(0,0,0,0)
        _RiverFloorIndex("Floor identity",Float)=0
        _RiverRefractionWeight("Refraction weight",Float)=1
        _RiverDetailWeight("Near-floor detail weight",Float)=1
        _RiverSteps("Volume march steps",Float)=12
        _RiverReveal("Construction reveal",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "AeroBuildingFinish.hlsl"
        TEXTURE2D(_RippleNormal); SAMPLER(sampler_RippleNormal);
        TEXTURE2D(_CausticsTex); SAMPLER(sampler_CausticsTex);
        TEXTURECUBE(_Environment); SAMPLER(sampler_Environment);
        TEXTURE2D_X(_RiverBackground); SAMPLER(sampler_RiverBackground);
        float _RiverClock, _AeroDaylight;
        CBUFFER_START(UnityPerMaterial)
        float4 _Absorption,_WaterColor,_ShallowColor,_DeepColor,_WallColor,_GlassTint,_EdgeTint;
        float4 _RiverOptics,_RiverMotion,_RiverPattern,_RiverShell,_RiverEnclosure,_RiverVolume,_RiverBed,_RiverDetail,_RiverLayout,_RadialFloorCenter;
        float _RadialFloor,_RiverFloorIndex,_RiverRefractionWeight,_RiverReveal,_RiverDetailWeight,_RiverSteps;
        CBUFFER_END
        #include "RiverChannel.hlsl"
        #include "Aero/AeroGlass.hlsl"
        #include "Aero/AeroWetness.hlsl"
        #include "Aero/AeroCaustics.hlsl"
        #include "RiverVolume.hlsl"
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float3 floorPatch:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS:SV_POSITION; float3 ws:TEXCOORD0; half3 normal:TEXCOORD1; float fog:TEXCOORD2; float3 local:TEXCOORD3; float slab:TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
        #include "BuildingRadialFloor.hlsl"
        Varyings Vert(Attributes i)
        {
            Varyings o=(Varyings)0; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.local=_RadialFloor>1.5?i.floorPatch:i.positionOS.xyz;
            BendRadialFloor(i); o.ws=TransformObjectToWorld(i.positionOS.xyz);
            o.positionCS=TransformWorldToHClip(o.ws);o.normal=TransformObjectToWorldNormal(i.normalOS);
            // World thickness of the 20 cm template slab (tiles may be scaled vertically).
            o.slab=abs(mul((float3x3)GetObjectToWorldMatrix(),float3(0,1,0)).y);
            o.fog=ComputeFogFactor(o.positionCS.z);return o;
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
            half3 reflected;
            [branch] if(_AeroFinishEnabled>.5 && _AeroFinishEnvironmentReady>.5 && _AeroFinishCapturing<.5)
                reflected=AeroEnvironment(normal,view,lod,_AeroDaylight);
            else
                reflected=min(SAMPLE_TEXTURECUBE_LOD(_Environment,sampler_Environment,reflect(-view,normal),lod).rgb,3)*lerp(.13,1,saturate(_AeroDaylight));
            return reflected;
        }
        half Luma(half3 c){return dot(c,half3(.2126,.7152,.0722));}
        half4 Composite(Varyings i,half3 added,half3 transmit,half3 background,half weight)
        {
            // Background already carries fog; fog only the light the river adds.
            half opacity=saturate(1-Luma(transmit));
            added=MixFogColor(added,unity_FogColor.rgb*opacity,i.fog);
            half3 col=added+transmit*background*weight;
            return half4(min(col,HALF_MAX),lerp(opacity,1,weight));
        }
        half4 SimpleWater(Varyings i,half3 normal,half3 view)
        {
            float path=_RiverOptics.x/max(.15,abs(dot(view,normal)));
            half fresnel=.02+.98*pow(1-saturate(dot(normal,view)),5);
            half3 transmission=exp(-max(_Absorption.rgb,0)*path)*(1-fresnel*_RiverOptics.z);
            half alpha=saturate(1-Luma(transmission));
            // Far floors keep a saturated aqua body without texture reads or time.
            half3 tint=RiverScatterColor(path)*alpha*.75*lerp(.35,1,saturate(_AeroDaylight));
            return half4(MixFog(tint/max(alpha,.001),i.fog)*alpha,alpha);
        }
        // Thin polished frame lines: channel rims and ~12 m panel joints on the lid.
        half LidFrame(float3 ws)
        {
            float2 p=ws.xz-_RadialFloorCenter.xy;
            float edge=abs(RiverChannelDistance(ws.xz,_RiverFloorIndex));
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
            return max(rim,jointMask*.65);
        }
        half3 WallShade(float3 p,float surfaceY,Light sun,float clock,half daylight)
        {
            half3 wn=RiverWallNormal(p.xz);
            Light local=GetMainLight(TransformWorldToShadowCoord(p));
            float depthBelow=max(surfaceY-p.y,0);
            half3 sunThroughWater=exp(-_Absorption.rgb*depthBelow/max(sun.direction.y,.25));
            half3 light=max(SampleSH(wn),0)+local.color*local.shadowAttenuation*saturate(dot(wn,sun.direction))*sunThroughWater;
            half3 wall=_WallColor.rgb*light;
            // Marble behind a glass pane picks up the water colour near the glass.
            wall=lerp(wall,wall*_ShallowColor.rgb,_RiverDetail.y);
            // Caustic light dances on the inner walls below the waterline.
            half3 caustic=RiverCaustic(p.xz+p.yy*.7,clock,_RiverPattern.z+1);
            wall+=caustic*_RiverMotion.z*_RiverPattern.y*_RiverBed.z*.6*daylight*step(.002,depthBelow);
            // Bright silver meniscus where the surface meets the glass.
            half band=1-smoothstep(0,max(_RiverDetail.z,.001),abs(p.y-surfaceY));
            wall+=half3(.92,.98,1)*band*_RiverBed.w*daylight*1.4;
            return wall;
        }
        half3 BedEtch(float2 xz,half daylight)
        {
            float2 g=abs(frac(xz/max(_RiverBed.x,.05))-.5)*_RiverBed.x;
            half line_=1-smoothstep(.004,.012,min(g.x,g.y));
            return half3(.85,.96,1)*line_*_RiverBed.y*daylight;
        }
        ENDHLSL
        Pass
        {
            Name "River volume"
            ZWrite Off ZTest LEqual Cull Back Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Volume
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            half4 Volume(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);Reveal(i);
                float sdf=RiverChannelDistance(i.ws.xz,_RiverFloorIndex);
                clip(-sdf);
                clip(abs(i.normal.y)-.5); // Tile side faces must not become internal partitions.
                half3 v=GetWorldSpaceNormalizeViewDir(i.ws);
                bool above=i.normal.y>0;
                half3 up=half3(0,1,0), outward=above?up:-up;
                float metres=distance(_WorldSpaceCameraPos,i.ws);
                [branch] if(_RiverDetailWeight<.001 || metres>=_RiverMotion.w*1.5)
                {
                    clip(-sdf-_RiverEnclosure.w);
                    return SimpleWater(i,outward,v);
                }
                float detail=(1-smoothstep(_RiverMotion.w,_RiverMotion.w*1.5,metres))*_RiverDetailWeight;
                float clock=_RiverClock*_RiverMotion.y;
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                half daylight=lerp(.13,1,saturate(_AeroDaylight));
                half weight=saturate(_RiverRefractionWeight);
                RiverSlab s=RiverMakeSlab(above?i.ws.y:i.ws.y+.2*i.slab,i.slab);
                float surfaceY=s.top-s.surface, bedY=s.top-s.bed;
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.ws));
                int steps=(int)clamp(_RiverSteps,1,24);
                half3 added=0, transmit=1, background=0;

                // Thick glass wall seen end-on: a cyan-green band along every channel edge.
                [branch] if(sdf>-_RiverEnclosure.w)
                {
                    half facing=saturate(dot(outward,v));
                    half3 edgeLight=max(SampleSH(outward),0)+sun.color*sun.shadowAttenuation*saturate(dot(outward,sun.direction));
                    added=_EdgeTint.rgb*edgeLight*.55+pow(1-facing,3)*_GlassTint.rgb*.6*daylight;
                    added+=sun.color*AeroFilteredGlint(outward,v,sun.direction,320,detail)*sun.shadowAttenuation*.65;
                    transmit=_EdgeTint.rgb*.25;
                    if(weight>.001)background=SAMPLE_TEXTURE2D_X(_RiverBackground,sampler_RiverBackground,uv).rgb;
                    return Composite(i,added,transmit,background,weight);
                }

                // Outer glass face (lid from above, bed from below).
                half3 shellN=outward; half gloss=.97;
                #if 1
                [branch] if(above)
                {
                    half3 unusedAlbedo=1;
                    AeroApplyWetness(unusedAlbedo,gloss,shellN,i.ws,up,0,1);
                }
                #endif
                half shellF=AeroFresnel(saturate(dot(shellN,v)),_RiverVolume.y);
                added+=Environment(shellN,v,(1-gloss)*6)*shellF*_RiverShell.z;
                added+=sun.color*AeroFilteredGlint(shellN,v,sun.direction,320,detail)*sun.shadowAttenuation*.65;
                half frame=LidFrame(i.ws);
                added+=_GlassTint.rgb*frame*.15*daylight+AeroThinFilm(shellN,v,frame,_AeroDaylight);
                transmit*=(1-shellF)*(1-frame*.35);

                float3 rd=-v;
                half3 caustic=lerp(_WaterColor.rgb,1,.88)*_RiverMotion.z*_RiverPattern.y*_RiverBed.z*lerp(.35,.85,saturate(_AeroDaylight))*detail;
                [branch] if(above)
                {
                    // Through the lid and the air gap to the rippled surface.
                    float tS=(surfaceY-i.ws.y)/min(rd.y,-1e-3);
                    float3 pS=i.ws+rd*tS;
                    half3 nS=RiverSurfaceNormal(pS.xz,clock,_RiverOptics.w*detail);
                    half Fs=AeroFresnel(saturate(dot(nS,v)),_RiverVolume.x);
                    added+=transmit*(Environment(nS,v,.8)*Fs*_RiverOptics.z+sun.color*AeroFilteredGlint(nS,v,sun.direction,110,1)*sun.shadowAttenuation*.9*detail);
                    transmit*=1-Fs;
                    float3 rw=refract(rd,nS,1/_RiverVolume.x);
                    bool wallHit;
                    float path=RiverMarch(pS,rw,bedY,steps,wallHit);
                    float3 pE=pS+rw*path;
                    half3 T=exp(-max(_Absorption.rgb,0)*path);
                    half3 ambient=max(SampleSH(up),0)+sun.color*sun.shadowAttenuation*saturate(sun.direction.y)*.35;
                    added+=transmit*RiverScatterColor(path)*(1-T)*ambient*_RiverDetail.w;
                    [branch] if(_RiverDetail.x>.001)
                    {
                        half4 bubbles=RiverBubbles(pS,rw,surfaceY,bedY,_RiverClock)*detail;
                        added+=transmit*bubbles.rgb*daylight; transmit*=1-bubbles.a*.6;
                    }
                    [branch] if(wallHit)
                    {
                        added+=transmit*T*WallShade(pE,surfaceY,sun,clock,daylight);
                        transmit=0;
                    }
                    else
                    {
                        // Light focused by the ripples lands on the glass bed inside the volume.
                        float2 sunShift=-sun.direction.xz/max(sun.direction.y,.3)*(surfaceY-bedY)*.75;
                        added+=transmit*T*(caustic*RiverCaustic(pE.xz+sunShift,clock,_RiverPattern.z)+BedEtch(pE.xz,daylight));
                        float3 exitDir=refract(rw,up,_RiverVolume.x);
                        if(dot(exitDir,exitDir)<1e-4)exitDir=rd;
                        transmit*=T*lerp(1,_GlassTint.rgb,.15);
                        if(weight>.001)background=RiverBackgroundAlong(i.ws,float3(pE.x,s.top-.2*s.scale,pE.z),exitDir,uv,_RiverVolume.z);
                    }
                }
                else
                {
                    // Up through the glass bed into the water.
                    float tB=(bedY-i.ws.y)/max(rd.y,1e-3);
                    float3 pB=i.ws+rd*tB;
                    float3 rw=refract(rd,-up,1/_RiverVolume.x);
                    bool wallHit;
                    float path=RiverMarch(pB,rw,surfaceY,steps,wallHit);
                    float3 pE=pB+rw*path;
                    half3 T=exp(-max(_Absorption.rgb,0)*path);
                    half3 ambient=max(SampleSH(up),0)+sun.color*sun.shadowAttenuation*saturate(sun.direction.y)*.35;
                    added+=transmit*RiverScatterColor(path)*(1-T)*ambient*_RiverDetail.w;
                    [branch] if(_RiverDetail.x>.001)
                    {
                        half4 bubbles=RiverBubbles(pB,rw,surfaceY,bedY,_RiverClock)*detail;
                        added+=transmit*bubbles.rgb*daylight; transmit*=1-bubbles.a*.6;
                    }
                    [branch] if(wallHit)
                    {
                        added+=transmit*T*WallShade(pE,surfaceY,sun,clock,daylight);
                        transmit=0;
                    }
                    else
                    {
                        // The surface seen from below: Snell's window, total internal
                        // reflection outside it, and bright caustic ripples on the underside.
                        half3 nS=RiverSurfaceNormal(pE.xz,clock,_RiverOptics.w*detail);
                        float3 out_=refract(rw,-nS,_RiverVolume.x);
                        half reflectance=1;
                        if(dot(out_,out_)>1e-4)
                        {
                            half cosT=saturate(dot(out_,nS));
                            half f0=AeroDielectricF0(_RiverVolume.x);
                            reflectance=f0+(1-f0)*pow(1-cosT,5);
                        }
                        half3 interior=lerp(_DeepColor.rgb,half3(.85,.95,1),.25)*ambient;
                        added+=transmit*T*(caustic*RiverCaustic(pE.xz,clock,_RiverPattern.z)+interior*reflectance*.6);
                        transmit*=T*(1-reflectance)*lerp(1,_GlassTint.rgb,.15);
                        if(weight>.001 && reflectance<.999)
                            background=RiverBackgroundAlong(i.ws,float3(pE.x,s.top,pE.z),out_,uv,_RiverVolume.z);
                    }
                }
                return Composite(i,added,transmit,background,weight);
            }
            ENDHLSL
        }
        Pass
        {
            Name "River simple"
            ZWrite Off ZTest LEqual Cull Back Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Simple
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            half4 Simple(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);Reveal(i);
                clip(-RiverChannelDistance(i.ws.xz,_RiverFloorIndex)-_RiverEnclosure.w);
                clip(abs(i.normal.y)-.5);
                return SimpleWater(i,normalize(i.normal),GetWorldSpaceNormalizeViewDir(i.ws));
            }
            ENDHLSL
        }
    }
}
