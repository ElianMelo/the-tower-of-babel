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
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 dimensions:TEXCOORD1; float4 corners:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 dimensions:TEXCOORD1; float4 screen:TEXCOORD2; float4 local:TEXCOORD3; float2 corners:TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex, _CameraOpaqueTexture;
            float4 _CameraOpaqueTexture_TexelSize, _ClipRect;
            fixed4 _Color;
            float _Frost, _Blur, _Shine, _SceneBlur;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.screen=ComputeScreenPos(o.vertex);
                o.uv=v.uv; o.color=v.color*_Color; o.dimensions=v.dimensions; o.corners=v.corners.xy; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 size=max(i.dimensions.xy,float2(1,1));
                float radius=min(i.corners.x,min(size.x,size.y)*.5);
                float2 p=(i.uv-.5)*size;
                float2 q=abs(p)-size*.5+radius;
                float d=length(max(q,0))+min(max(q.x,q.y),0)-radius;
                float aa=max(fwidth(d),.6);
                float mask=1-smoothstep(-aa,aa,d);
                float rim=1-smoothstep(-i.corners.y-aa,-i.corners.y+aa,d);
                rim=1-rim;
                float2 screenUV=i.screen.xy/i.screen.w;
                float2 lens=normalize(p+float2(.01,.01))*pow(saturate(1+d/14),2)*.0035;
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
                float top=smoothstep(.46,.5,i.uv.y)*(.2+.16*i.uv.y);
                float glow=pow(saturate(1-abs(i.uv.x-.3)),4)*pow(i.uv.y,5)*.2;
                glass+=(_Shine*top+glow)*float3(.85,.95,1);
                glass=lerp(glass,float3(.91,1,1),rim*.83);
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
