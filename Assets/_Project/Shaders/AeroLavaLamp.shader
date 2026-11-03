// Lava lamp vessel: the glass mesh is the outer surface, and the wax is ray marched inside it in object space.
// Wax blobs are signed distance ellipsoids joined with a smooth minimum (metaballs). Each blob runs its own heat
// cycle: it warms on the bulb, rises, lingers and cools at the top, then sinks back. The field is clipped to the
// inner glass wall, so wax presses flat against the glass and never leaves the vessel.
Shader "TowerOfBabel/Lighting/Lava Lamp Fluid"
{
    Properties
    {
        _FluidColor("Lit fluid",Color)=(.03,.3,1,1)
        _FluidDeepColor("Fluid far side",Color)=(.004,.05,.36,1)
        _WaxColor("Wax",Color)=(.16,1,.24,1)
        _WaxHotColor("Wax near the bulb",Color)=(.6,1,.32,1)
        _GlassTint("Glass edge tint",Color)=(.75,.9,1,1)
        _VesselHeight("Vessel height (object units)",Float)=.455
        _RadiusBottom("Vessel radius at the bottom",Float)=.095
        _RadiusTop("Vessel radius at the top",Float)=.055
        _GlassThickness("Glass thickness",Float)=.006
        _FlowSpeed("Flow speed",Range(0,4))=1
        _Merge("Blob merge radius",Range(.005,.08))=.034
        _Absorption("Fluid absorption",Range(0,30))=7
        _NightGlow("Glow at night",Float)=4.6
        _DayGlow("Glow during the day",Float)=1.3
        _Streak("Glass highlight streaks",Range(0,2))=.6
        _Daylight("Preview daylight; -1 follows the sky",Float)=-1
        _FlowTime("Preview time; -1 uses game time",Float)=-1
        _Steps("Ray march steps",Range(8,48))=34
        _FarSteps("Ray march steps far away",Range(4,24))=12
        _FarDistance("Fewer steps beyond (m)",Float)=45
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _FluidColor,_FluidDeepColor,_WaxColor,_WaxHotColor,_GlassTint;
            float _VesselHeight,_RadiusBottom,_RadiusTop,_GlassThickness,_FlowSpeed,_Merge,_Absorption;
            float _NightGlow,_DayGlow,_Daylight,_FlowTime,_Steps,_FarSteps,_FarDistance,_Streak;
        CBUFFER_END
        float _AeroDaylight;
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
            float3 positionOS:TEXCOORD1;
            float3 cameraOS:TEXCOORD2;
            half3 normalWS:TEXCOORD3;
            nointerpolation float seed:TEXCOORD4;
            half fog:TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings o=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
            o.positionCS=TransformWorldToHClip(o.positionWS);
            o.positionOS=input.positionOS.xyz;
            o.cameraOS=TransformWorldToObject(GetCameraPositionWS());
            o.normalWS=TransformObjectToWorldNormal(input.normalOS);
            // Every instanced lamp gets its own wax from where it hangs.
            float3 origin=TransformObjectToWorld(float3(0,0,0));
            o.seed=frac(sin(dot(origin,float3(12.9898,78.233,37.719)))*43758.5453)*97.0;
            o.fog=ComputeFogFactor(o.positionCS.z);
            return o;
        }
        ENDHLSL
        Pass
        {
            Name "LavaLampForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #define BLOBS 6
            static float3 blobCenter[BLOBS];
            static float3 blobRadius[BLOBS];
            static float3 poolRadius;

            float Hash(float n) { return frac(sin(n)*43758.5453); }
            float SmoothMin(float a,float b,float k) { float h=saturate(.5+.5*(b-a)/k); return lerp(b,a,h)-k*h*(1-h); }
            float RadiusAt(float y) { return lerp(_RadiusBottom,_RadiusTop,saturate(y/_VesselHeight)); }
            // Signed distance to the fluid volume: the tapered vessel shrunk by the glass wall.
            float Vessel(float3 p)
            {
                float slope=(_RadiusBottom-_RadiusTop)/_VesselHeight;
                float side=(length(p.xz)-RadiusAt(p.y))*rsqrt(1+slope*slope);
                return max(side,max(-p.y,p.y-_VesselHeight))+_GlassThickness;
            }
            // Bound for an ellipsoid; exact enough to march and cheap.
            float Ellipsoid(float3 p,float3 r)
            {
                float k0=length(p/r), k1=length(p/(r*r));
                return k0*(k0-1)/max(k1,1e-5);
            }
            void Animate(float time,float seed)
            {
                [unroll] for(int i=0;i<BLOBS;i++)
                {
                    float a=Hash(seed+i*17.13), b=Hash(seed*1.7+i*5.31), c=Hash(seed*2.3+i*9.77);
                    float period=lerp(15,28,a)/max(_FlowSpeed,.01);
                    float u=frac(time/period+b);
                    // Heat cycle: cosine travel lingers at the bulb and under the cap, like real wax.
                    float travel=.5-.5*cos(u*6.28318);
                    float pace=abs(sin(u*6.28318));
                    float size=lerp(.021,.04,c);
                    float y=lerp(.035+size,_VesselHeight-.02-size*.6,travel);
                    float room=max(RadiusAt(y)-_GlassThickness-size*.85,0);
                    float angle=a*6.28318+time*.04*(b-.5)+sin(time*.17+i)*.35;
                    float offset=room*lerp(.1,.9,c)*(.8+.2*sin(time*.23+i*2.1));
                    blobCenter[i]=float3(cos(angle)*offset,y,sin(angle)*offset);
                    // Moving wax stretches into a teardrop along its travel.
                    float stretch=1+pace*.6;
                    blobRadius[i]=float3(size*rsqrt(stretch),size*stretch,size*rsqrt(stretch));
                }
                float swell=.04+.012*sin(time*.37+seed);
                poolRadius=float3(_RadiusBottom*1.1,swell,_RadiusBottom*1.1);
            }
            float Wax(float3 p)
            {
                float d=Ellipsoid(p,poolRadius);
                d=SmoothMin(d,Ellipsoid(p-float3(0,_VesselHeight,0),float3(_RadiusTop*.95,.016,_RadiusTop*.95)),_Merge);
                [unroll] for(int i=0;i<BLOBS;i++) d=SmoothMin(d,Ellipsoid(p-blobCenter[i],blobRadius[i]),_Merge);
                return max(d,Vessel(p));
            }
            float3 WaxNormal(float3 p)
            {
                const float2 k=float2(1,-1); const float e=.0015;
                return normalize(k.xyy*Wax(p+k.xyy*e)+k.yyx*Wax(p+k.yyx*e)+k.yxy*Wax(p+k.yxy*e)+k.xxx*Wax(p+k.xxx*e));
            }
            half Daylight() { return _Daylight>=0 ? saturate(_Daylight) : saturate(_AeroDaylight); }

            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float time=_FlowTime>=0 ? _FlowTime : _Time.y;
                Animate(time,input.seed);
                float3 rd=normalize(input.positionOS-input.cameraOS);
                float3 ro=input.positionOS;
                float viewDistance=length(GetCameraPositionWS()-input.positionWS);
                int steps=(int)(viewDistance>_FarDistance ? _FarSteps : _Steps);
                float t=_GlassThickness*1.2, halo=0;
                bool hit=false;
                for(int s=0;s<steps;s++)
                {
                    float3 p=ro+rd*t;
                    float wall=Vessel(p);
                    if(wall>.001 && t>_GlassThickness*2) break;
                    float w=Wax(p);
                    if(w<.0012) { hit=true; break; }
                    // Soft green light scattered by the fluid around each blob.
                    halo+=exp(-w*70)*.05;
                    t+=max(min(w,max(-wall,0)+.004)*.9,.003);
                }
                float3 p=ro+rd*t;
                float height=saturate(p.y/_VesselHeight);
                // The bulb under the vessel lights the fluid from below.
                half bulb=lerp(1.55,.5,saturate(ro.y/_VesselHeight));
                half3 fluid=_FluidColor.rgb*bulb;
                half transmit=exp(-t*_Absorption);
                half3 color;
                if(hit)
                {
                    float3 n=WaxNormal(p);
                    float heat=1-height;
                    float3 toBulb=normalize(float3(0,-.07,0)-p);
                    half lit=saturate(dot(n,toBulb)*.6+.4);
                    half rim=pow(1-saturate(dot(n,-rd)),2);
                    half3 wax=lerp(_WaxColor.rgb,_WaxHotColor.rgb,heat*heat);
                    // Thick wax is deeper in the core and glows at its translucent rim; a soft sheen gives it volume.
                    half sheen=pow(saturate(dot(reflect(rd,n),normalize(float3(.35,1,-.25)))),28)*.45;
                    half3 shaded=wax*(.38+1.15*lit*lerp(.45,1.35,heat))*(.78+.22*rim)+_WaxColor.rgb*rim*.55+sheen;
                    color=shaded*transmit+fluid*(1-transmit);
                }
                else color=_FluidDeepColor.rgb*bulb*transmit+fluid*(1-transmit);
                color+=_WaxColor.rgb*saturate(halo)*.45;
                // Bright ring where the fluid meets the lit base.
                color+=_FluidColor.rgb*exp(-ro.y*40)*.9;
                half glow=lerp(_NightGlow,_DayGlow,Daylight());
                color*=glow;

                // Glass shell over the fluid: Fresnel reflection and a tight sun glint.
                half3 n=normalize(input.normalWS);
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing=saturate(dot(n,view));
                half fresnel=.04+.96*pow(1-facing,5);
                float2 screenUV=GetNormalizedScreenSpaceUV(input.positionCS);
                half3 reflected=GlossyEnvironmentReflection(reflect(-view,n),input.positionWS,.03h,1,screenUV);
                Light sun=GetMainLight();
                half3 glint=sun.color*pow(saturate(dot(n,normalize(sun.direction+view))),420)*5;
                // Near the silhouette the ray crosses only glass: show a clear, tinted edge.
                half edge=saturate(1-(t-_GlassThickness)/.012)*(1-facing);
                color=lerp(color,_GlassTint.rgb*(reflected+_FluidColor.rgb*glow*.25),edge*.6);
                // Studio-style vertical highlights that run down a glass cone, whatever the light.
                half across=dot(n,normalize(UNITY_MATRIX_V[0].xyz));
                half streak=exp(-pow((across+.55)/.07,2))*.6+exp(-pow((across-.72)/.045,2))*.3;
                half3 streakColor=lerp(reflected,half3(1,1,1),.55)*lerp(.45,1,Daylight());
                color=color*(1-fresnel)+reflected*fresnel+glint+streakColor*streak*_Streak;
                color=MixFog(color,input.fog);
                return half4(color,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(Varyings input):SV_Target { return 0; }
            ENDHLSL
        }
    }
}
