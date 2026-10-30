Shader "Hidden/TowerOfBabel/River caustic cookie"
{
    Properties
    {
        _CausticsTex("Caustics (Crest net)",2D)="white"{}
        _PatternDensity("Projected pattern density",Float)=1
        _HighlightOpacity("Bright focus point opacity",Range(0,1))=.95
        _DetailOpacity("Faint flowing detail opacity",Range(0,1))=.06
        _CutoutOffset("Cutout flow offset",Vector)=(0,0,0,0)
        _CutoutWarp("Cutout flow warp",Float)=.035
        _DetailLifetime("Detail swell duration",Vector)=(6,10,0,0)
        _HighlightLifetime("Focus point lifetime",Vector)=(2,4,0,0)
        _HighlightFade("Focus point fade",Float)=.7
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Stylized Water 3/Shaders/Libraries/Caustics.hlsl"
            float _RiverClock,_FlowSpeed,_CookieSpan,_PatternDensity,_CutoutWarp,_HighlightOpacity,_DetailOpacity,_HighlightFade;
            float4 _CutoutOffset,_DetailLifetime,_HighlightLifetime;
            struct A {float4 position:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vert(A i){V o;o.position=TransformObjectToHClip(i.position.xyz);o.uv=i.uv;return o;}
            float Hash(float2 p)
            {
                p=frac(p*float2(123.34,345.45));p+=dot(p,p+34.345);
                return frac(p.x*p.y);
            }
            float Noise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                    lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            float Field(float2 p) {return Noise(p)*.64+Noise(p*2.07+17.3)*.36;}
            float2 Rotate(float2 p,float angle)
            {
                float s,c;sincos(angle,s,c);return float2(c*p.x-s*p.y,s*p.x+c*p.y);
            }
            half Peak(half3 value) {return max(value.r,max(value.g,value.b));}
            float CellLife(float2 cell,float seconds,float2 lifetime,float minimumFade,float rest)
            {
                float low=max(2,min(lifetime.x,lifetime.y)),high=max(low,max(lifetime.x,lifetime.y));
                float duration=lerp(low,high,Hash(cell+7.13));
                float period=duration+rest;
                float age=frac((seconds+Hash(cell+31.7)*period)/period)*period;
                float fade=clamp(minimumFade,.7,duration*.45);
                return smoothstep(0,fade,age)*(1-smoothstep(duration-fade,duration,age));
            }
            float LifeField(float2 p,float seconds,float2 lifetime,float fade,float rest)
            {
                // Blend neighboring lifecycles so travelling fragments never hit a
                // hard cell boundary. Highlight and detail fields use unrelated seeds.
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                float a=CellLife(cell,seconds,lifetime,fade,rest);
                float b=CellLife(cell+float2(1,0),seconds,lifetime,fade,rest);
                float c=CellLife(cell+float2(0,1),seconds,lifetime,fade,rest);
                float d=CellLife(cell+1,seconds,lifetime,fade,rest);
                return lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y);
            }
            half4 Frag(V i):SV_Target
            {
                float2 centered=i.uv-.5;
                float2 p=centered*max(1,_CookieSpan);
                float2 detail=p*max(1,_PatternDensity);
                float clock=_RiverClock*_FlowSpeed;
                float2 drift=float2(_RiverClock*.061,-_RiverClock*.047);
                // Warp the sample domain before blending unrelated rotations/scales. This
                // breaks the source texture's obvious tiling without adding new textures.
                float2 warp=float2(Field(detail*.09+drift),Field(detail*.113-drift+43.7))-.5;
                detail+=warp*2.3;
                half a=Peak(SAMPLE_TEXTURE2D(_CausticsTex,sampler_CausticsTex,Rotate(detail,.67)*.18+float2(clock,clock*.73)).rgb);
                half b=Peak(SAMPLE_TEXTURE2D(_CausticsTex,sampler_CausticsTex,Rotate(detail,-1.17)*.119-float2(clock*.61,clock)).rgb);
                half c=Peak(SAMPLE_TEXTURE2D(_CausticsTex,sampler_CausticsTex,Rotate(detail,2.31)*.073+float2(7.3-clock*.43,3.7+clock*.57)).rgb);
                half caustic=max(a,max(b*.72,c*.48));
                // Translate an unbounded cutout field through a stationary soft window.
                // Fragments leave one side as new fragments arrive on the other; no spin.
                float2 moving=centered-_CutoutOffset.xy;
                float2 flowWarp=float2(Field(moving*5.1+32.7),Field(moving*6.7-11.3))-.5;
                moving+=flowWarp*max(0,_CutoutWarp);
                float edgeNoise=Field(moving*7.1);
                float edge=length(centered*float2(1.06,.88))+(edgeNoise-.5)*.32;
                half outline=1-smoothstep(.16,.285,edge);
                half cutouts=smoothstep(.27,.56,Field(moving*9.5+19.1));
                half aperture=(1-smoothstep(.32,.42,length(centered)))*outline*cutouts;
                // Separate sparse, near-opaque focus points from the faint connected
                // branches, so scaling up coverage does not create a solid bright wash.
                // More islands than v11 so white focus points appear more often (user request 2026-10-29).
                half pointIslands=smoothstep(.42,.64,Field(moving*17.3+51.7));
                half pointCore=pow(smoothstep(.18,.55,caustic),2);
                half highlights=pointCore*pointIslands*saturate(_HighlightOpacity);
                half details=smoothstep(.025,.4,caustic)*saturate(_DetailOpacity);
                highlights*=LifeField(moving*17.3+float2(13.3,-29.1),_RiverClock,_HighlightLifetime.xy,max(.7,_HighlightFade),.45);
                details*=lerp(.35,1,LifeField(moving*3.7+float2(83.7,19.1),_RiverClock,_DetailLifetime.xy,2,0));
                // Bright points remain strong inside a cutout; the soft flowing detail
                // retains the full low-opacity mask instead of dimming both layers alike.
                half brightAperture=smoothstep(.04,.28,aperture);
                half3 value=highlights*brightAperture*half3(1,.995,.98)+details*aperture*half3(.72,.9,1);
                return half4(saturate(value),1);
            }
            ENDHLSL
        }
    }
}
