Shader "TowerOfBabel/UI/Liquid Glass"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white"{}
        _Color("Tint",Color)=(0.35,0.8,1,0.7)
        _Frost("Frost",Range(0,1))=.6
        _Blur("Blur radius",Range(0,12))=5
        _Shine("Gloss",Range(0,1))=.5
        _SceneBlur("Scene refraction",Range(0,1))=1
        [HideInInspector] _MotionTime("Motion preview clock",Float)=-1
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float4 motion:TANGENT; float2 uv:TEXCOORD0; float4 dimensions:TEXCOORD1; float4 corners:TEXCOORD2; float4 hover:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 dimensions:TEXCOORD1; float4 screen:TEXCOORD2; float4 local:TEXCOORD3; float4 corners:TEXCOORD4; float4 hover:TEXCOORD5; float2 motion:TEXCOORD6; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex, _CameraOpaqueTexture;
            float4 _CameraOpaqueTexture_TexelSize, _ClipRect;
            fixed4 _Color;
            float _Frost, _Blur, _Shine, _SceneBlur, _AeroUiTime, _MotionTime;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.screen=ComputeScreenPos(o.vertex);
                o.uv=v.uv; o.color=v.color*_Color; o.dimensions=v.dimensions; o.corners=v.corners; o.hover=v.hover; o.motion=v.motion.xy; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 size=max(i.dimensions.xy,float2(1,1));
                float radius=min(i.corners.x,min(size.x,size.y)*.5);
                float2 p=(i.uv-.5)*size;
                float t=_MotionTime>=0 ? _MotionTime : _AeroUiTime;
                float2 q=abs(p)-size*.5+radius;
                float d=length(max(q,0))+min(max(q.x,q.y),0)-radius;
                float aa=max(fwidth(d),.6);
                float mask=1-smoothstep(-aa,aa,d);
                float rim=1-smoothstep(-i.corners.y-aa,-i.corners.y+aa,d);
                rim=1-rim;
                float2 screenUV=i.screen.xy/i.screen.w;
                float2 lens=normalize(p+float2(.01,.01))*pow(saturate(1+d/14),2)*.0035;
                float2 flow=sin(float2(p.x*.008+p.y*.006+t*.6,p.y*.009-p.x*.004-t*.48));
                lens+=flow*i.motion.y*.0025;
                float2 stepUV=max(_CameraOpaqueTexture_TexelSize.xy,float2(.0005,.0005))*_Blur;
                float3 scene=tex2D(_CameraOpaqueTexture,screenUV+lens).rgb*4;
                scene+=tex2D(_CameraOpaqueTexture,screenUV+stepUV*float2(1,0)+lens).rgb*2;
                scene+=tex2D(_CameraOpaqueTexture,screenUV-stepUV*float2(1,0)+lens).rgb*2;
                scene+=tex2D(_CameraOpaqueTexture,screenUV+stepUV*float2(0,1)+lens).rgb*2;
                scene+=tex2D(_CameraOpaqueTexture,screenUV-stepUV*float2(0,1)+lens).rgb*2;
                scene+=tex2D(_CameraOpaqueTexture,screenUV+stepUV+ lens).rgb;
                scene+=tex2D(_CameraOpaqueTexture,screenUV-stepUV+ lens).rgb;
                scene+=tex2D(_CameraOpaqueTexture,screenUV+stepUV*float2(1,-1)+lens).rgb;
                scene+=tex2D(_CameraOpaqueTexture,screenUV+stepUV*float2(-1,1)+lens).rgb;
                scene/=16;
                float3 tint=i.color.rgb;
                float3 glass=lerp(scene,tint,_Frost);
                glass=lerp(tint,glass,_SceneBlur);
                glass+=flow.x*i.motion.y*.012*float3(-.3,.35,.5);
                float top=smoothstep(.46,.5,i.uv.y)*(.2+.16*i.uv.y);
                float glow=pow(saturate(1-abs(i.uv.x-.3)),4)*pow(i.uv.y,5)*.2;
                glass+=(_Shine*top+glow)*float3(.85,.95,1);
                glass=lerp(glass,float3(.91,1,1),rim*.83);
                float pulse=i.dimensions.z;
                float age=1-pulse;
                float2 rippleUV=(i.uv-i.corners.zw)*float2(size.x/max(size.y,1),1);
                float ripple=exp(-abs(length(rippleUV)-age*1.8)*22)*pulse;
                float sweep=pow(saturate(1-abs(i.uv.x+i.uv.y-age*2.8)*5),3)*pulse;
                glass=lerp(glass,float3(.78,1,.91),saturate(ripple*.7+sweep*.65));
                glass+=(ripple*.12+sweep*.22)*float3(.65,.95,1);
                float hover=i.dimensions.w;
                float2 hoverUV=(i.uv-i.hover.xy)*float2(size.x/max(size.y,1),1);
                float light=exp(-dot(hoverUV,hoverUV)*8)*hover;
                float sheen=pow(saturate(1-abs(i.uv.x*.65+i.uv.y-(sin(t*.8)*.35+.65))*4),4)*hover;
                float edge=(1-smoothstep(1,7,abs(d)))*hover;
                // Background panels keep their interior color and alpha. Hover only catches the edge.
                float fill=saturate(i.hover.z);
                float buttonGlow=(1-smoothstep(-7,-1,d))*hover;
                glass=lerp(glass,float3(.64,1,.91),(light*.14+sheen*.13+buttonGlow*.2)*fill);
                glass+=((light*.055+sheen*.065+buttonGlow*.08)*fill+edge*.015*(1-fill))*float3(.7,.95,1);
                // A highlight travels around a fixed outline. Neither edge coverage nor opacity animates.
                float2 halfSize=size*.5;
                float vertical=step(halfSize.x-abs(p.x),halfSize.y-abs(p.y));
                float across=p.y>=0 ? p.x+halfSize.x : size.x+size.y+halfSize.x-p.x;
                float along=p.x>=0 ? size.x+halfSize.y-p.y : 2*size.x+size.y+p.y+halfSize.y;
                float perimeter=lerp(across,along,vertical);
                float circumference=2*(size.x+size.y);
                float phase=t*95+size.x*.31+size.y*.17;
                float arc=abs(frac((perimeter-phase)/circumference+.5)-.5)*circumference;
                float passing=pow(saturate(1-arc/clamp(circumference*.15,30,160)),2);
                float borderBand=exp(-abs(d+1.5)*.65)*mask;
                glass+=borderBand*passing*i.hover.w*float3(.8,.95,1);
                fixed4 col=fixed4(glass,mask*i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                col.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a-.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
