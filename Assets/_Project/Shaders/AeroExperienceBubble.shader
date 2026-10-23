Shader "TowerOfBabel/UI/Experience Bubble"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _RingOnly("Pop ring",Range(0,1))=0
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
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; };
            float4 _Color, _ClipRect;
            float _RingOnly;
            v2f vert(appdata v)
            {
                v2f o; o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex);
                o.color=v.color*_Color; o.uv=v.uv; return o;
            }
            half4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-.5)*2.14;
                float r=length(p), aa=max(fwidth(r),.006);
                float inside=1-smoothstep(.98-aa,1+aa,r);
                float z=sqrt(saturate(1-r*r));
                float rim=pow(saturate(r),7);
                float angle=atan2(p.y,p.x);
                // Thin-film bands, a cyan foot and broad softbox reflections like the supplied soap bubble.
                float phase=angle*1.9+z*8.5;
                float3 film=.68+.27*cos(phase+float3(0,2.1,4.2));
                film=lerp(film,float3(.50,.91,1),saturate(-p.y)*.55);
                float softbox=exp(-dot((p-float2(-.36,.43))*float2(6,4),(p-float2(-.36,.43))*float2(6,4)));
                float shine=exp(-pow((r-.84)*24,2))*pow(saturate(dot(normalize(p+float2(.0001,.0001)),normalize(float2(-.7,.7)))),12);
                float glint=exp(-dot((p-float2(.50,-.28))*float2(10,5),(p-float2(.50,-.28))*float2(10,5)));
                float3 col=lerp(film,float3(1,1,1),saturate(softbox+shine+glint*.7));
                float alpha=inside*(.06+rim*.63+softbox*.78+shine*.8+glint*.65);
                float ring=exp(-pow((r-.86)*25,2))*inside;
                alpha=lerp(alpha,ring*.9,_RingOnly);
                col=lerp(col,float3(.72,1,1),_RingOnly);
                alpha*=i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                alpha*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha-.001);
                #endif
                return half4(col*i.color.rgb,alpha);
            }
            ENDCG
        }
    }
}
