#ifndef TOWER_RADIAL_FLOOR_INCLUDED
#define TOWER_RADIAL_FLOOR_INCLUDED
// Same local deformation/Jacobian as TowerFloorLayout.Bend and RadialFloorMeshCache.
// Radius and cell dimensions come from the existing instanced transform.
void BendRadialFloor(inout Attributes input)
{
    if(_RadialFloor<.5 || _RadialFloor>1.5)return;
    float3 origin=TransformObjectToWorld(float3(0,0,0));
    float radius=distance(origin.xz,_RadialFloorCenter.xy);
    if(radius<.0001)return;
    float sx=length(TransformObjectToWorldDir(float3(1,0,0),false));
    float sz=length(TransformObjectToWorldDir(float3(0,0,1),false));
    float angle=-input.positionOS.z*sz/radius;
    float sn,cs;sincos(angle,sn,cs);
    float remaining=max(0,radius-input.positionOS.x*sx);
    if(remaining<.0002)remaining=0;
    float ratio=max(remaining/radius,.001);
    float3 n=input.normalOS;
    input.normalOS=normalize(float3(cs*n.x-sx/sz*sn/ratio*n.z,n.y,sz/sx*sn*n.x+cs/ratio*n.z));
    float3 t=input.tangentOS.xyz;
    input.tangentOS.xyz=normalize(float3(cs*t.x-ratio*sz/sx*sn*t.z,t.y,sx/sz*sn*t.x+ratio*cs*t.z));
    input.positionOS.x=(radius-remaining*cs)/sx;
    input.positionOS.z=-remaining*sn/sz;
}
#endif
