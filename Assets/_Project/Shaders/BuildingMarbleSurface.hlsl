#ifndef TOWER_MARBLE_SURFACE_INCLUDED
#define TOWER_MARBLE_SURFACE_INCLUDED
// Shared by the marble deck and the stone surrounding the enclosed glass river.
half Marble(float3 p,half3 n)
{
    half3 w=pow(abs(n),4);w/=max(dot(w,half3(1,1,1)),.001);
    float aspect=max(_TextureAspect,.01);
    half a=SAMPLE_TEXTURE2D(_MarbleMap,sampler_MarbleMap,float2(p.y/aspect,p.z)).r;
    half b=SAMPLE_TEXTURE2D(_MarbleMap,sampler_MarbleMap,float2(p.x/aspect,p.z)).r;
    half c=SAMPLE_TEXTURE2D(_MarbleMap,sampler_MarbleMap,float2(p.x/aspect,p.y)).r;
    return dot(half3(a,b,c),w);
}
half2 ReliefGradient(float2 uv)
{
    half3 normal=UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,uv));
    half2 mirror=1-4*frac(floor(uv)*.5);
    return normal.xy/max(normal.z,.1)*mirror;
}
half3 MarbleNormal(float3 p,half3 n)
{
    half3 w=pow(abs(n),4);w/=max(dot(w,half3(1,1,1)),.001);
    float aspect=max(_TextureAspect,.01);
    half2 x=ReliefGradient(float2(p.y/aspect,p.z));
    half2 y=ReliefGradient(float2(p.x/aspect,p.z));
    half2 z=ReliefGradient(float2(p.x/aspect,p.y));
    half3 gradient=half3(0,x.x,x.y)*w.x+half3(y.x,0,y.y)*w.y+half3(z.x,z.y,0)*w.z;
    gradient-=n*dot(n,gradient);
    return normalize(n+gradient*_NormalStrength);
}
#endif
