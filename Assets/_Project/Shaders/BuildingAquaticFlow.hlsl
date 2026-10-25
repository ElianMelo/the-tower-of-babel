#ifndef TOWER_AQUATIC_FLOW_INCLUDED
#define TOWER_AQUATIC_FLOW_INCLUDED
// Independent implementation informed by Valve's two-phase flow technique.
// Position-derived seeds remain stable as instances move between rendering batches.
float2 RotateAquatic(float2 p,float seed)
{
    float sn,cs;sincos(seed*6.2831853,sn,cs);
    return float2(cs*p.x-sn*p.y,sn*p.x+cs*p.y);
}
float2 AquaticProjection(float3 p)
{
    return float2(p.x+p.z*.61,p.y+p.z*.43);
}
half GlassRegion(Varyings input,half3 color)
{
    // Radial deck faces are continuous marble; a synthetic border around every
    // patch reads as an open joint, especially when looking up at the ceiling.
    // Actual aqua side bands and the separate enclosed river retain their glaze.
    if(_RadialFloor>.5 && _ModelKind>.5 && _ModelKind<1.5 && abs(input.normalWS.y)>.99)return 0;
    half region=saturate((color.b-color.r)*3);
    if(_ModelKind> .5 && _ModelKind<1.5 || _ModelKind>2.5)
    {
        float depthScale=_ModelKind>2.5?.35:1;
        float2 center=float2(input.positionOS.x-2,(input.positionOS.z+2)*depthScale);
        float2 halfSize=float2(2,2*depthScale);
        float2 q=abs(center)-(halfSize-.30);
        float inside=-(length(max(q,0))+min(max(q.x,q.y),0)-.30);
        half border=1-smoothstep(_GlassBorderWidth,_GlassBorderWidth+.055,inside);
        region=max(region,border*_GlassBorderStrength);
    }
    return saturate(region);
}
struct AquaticFrame
{
    float2 uvA,uvB,dx,dy;
    half weight,fade,seed;
};
AquaticFrame BuildAquaticFrame(Varyings input)
{
    AquaticFrame f=(AquaticFrame)0;
    float seed=input.originSeed.w*_InstanceVariation;
    float scale=_WaterScale*lerp(1,.85+frac(seed*17.3)*.35,_InstanceVariation);
    float2 uv=RotateAquatic(AquaticProjection(input.positionWS-input.originSeed.xyz),seed*_InstanceVariation)*scale;
    uv+=float2(seed,frac(seed*7.13))*9;
    f.dx=ddx(uv);f.dy=ddy(uv);f.seed=seed;
    float dist=distance(GetCameraPositionWS(),input.originSeed.xyz);
    f.fade=saturate((_WaterDetailDistance-dist)/max(_WaterDetailDistance*.4,1));
    if(_AeroFinishCapturing>.5)f.fade=0;
    if(f.fade<.001)
    {
        f.uvA=uv;f.uvB=uv;f.weight=.5;
        return f;
    }
    float clock=(_MotionTime<0?_Time.y:_MotionTime)*_FlowSpeed;
    clock*=lerp(1,.84+frac(seed*11.7)*.32,_InstanceVariation);
    half4 field=SAMPLE_TEXTURE2D_GRAD(_WaterField,sampler_WaterField,uv*.27,f.dx*.27,f.dy*.27);
    float2 flow=field.rg*2-1;
    float phase=clock+field.b*.65+seed*7;
    float a=frac(phase),b=frac(phase+.5);
    f.weight=1-abs(2*a-1);
    f.uvA=uv-flow*(a-.5)*_FlowDistortion;
    f.uvB=uv-flow*(b-.5)*_FlowDistortion;
    return f;
}
half3 AquaticNormal(AquaticFrame f,half3 normal,half region)
{
    // Distant instances retain smooth glass; avoid tiny moving normal highlights.
    if(f.fade<.001 || region<.001)return normal;
    half3 a=UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_WaterRippleNormal,sampler_WaterRippleNormal,f.uvA*1.6,f.dx*1.6,f.dy*1.6));
    half3 b=UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_WaterRippleNormal,sampler_WaterRippleNormal,f.uvB*1.6,f.dx*1.6,f.dy*1.6));
    half2 slope=lerp(b.xy,a.xy,f.weight);
    // Pull the oblique texture gradient back into the world-space tangent plane.
    float sn,cs;sincos(f.seed*_InstanceVariation*6.2831853,sn,cs);
    slope=float2(cs*slope.x+sn*slope.y,-sn*slope.x+cs*slope.y);
    half3 gradient=half3(slope.x,slope.y,slope.x*.61+slope.y*.43);
    gradient-=normal*dot(normal,gradient);
    return normalize(normal+gradient*_WaterNormalStrength*region*f.fade);
}
half2 AquaticLight(AquaticFrame f,half3 n,half3 view)
{
    half2 result=half2(0,0);
    if(f.fade<.001)
    {
        result=half2(0,.5);
    }
    else
    {
    half a=SAMPLE_TEXTURE2D_GRAD(_WaterLightMap,sampler_WaterLightMap,f.uvA,f.dx,f.dy).r;
    half b=SAMPLE_TEXTURE2D_GRAD(_WaterLightMap,sampler_WaterLightMap,f.uvB,f.dx,f.dy).r;
    half moving=lerp(b,a,f.weight);
    // A shallow refracted offset creates an apparent second layer inside the glaze.
    float3 ray=refract(-view,n,1.0/1.333);
    float2 depthOffset=AquaticProjection(ray)*_InnerDepth*(1+_AeroFinishEnabled*_AeroFinishOptics.x*.7)/max(abs(dot(ray,n)),.4);
    float2 deepUV=RotateAquatic(f.uvA*.73+depthOffset,.19)+float2(.37,.71);
    float2 deepUVB=RotateAquatic(f.uvB*.73+depthOffset,.19)+float2(.37,.71);
    half deepA=SAMPLE_TEXTURE2D_GRAD(_WaterLightMap,sampler_WaterLightMap,deepUV,RotateAquatic(f.dx*.73,.19),RotateAquatic(f.dy*.73,.19)).r;
    half deepB=SAMPLE_TEXTURE2D_GRAD(_WaterLightMap,sampler_WaterLightMap,deepUVB,RotateAquatic(f.dx*.73,.19),RotateAquatic(f.dy*.73,.19)).r;
    half deep=lerp(deepB,deepA,f.weight);
    half highlight=saturate((moving*.72+deep*.28-.30)*1.7);
    result=half2(highlight*highlight*f.fade,lerp(.5,lerp(moving,deep,.4),f.fade));
    }
    return result;
}
#endif
