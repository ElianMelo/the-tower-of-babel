#ifndef AERO_FAR_INSTANCING_INCLUDED
#define AERO_FAR_INSTANCING_INCLUDED
// GPU-resident far tower instancing (performance passes 2026-11-03 and 2026-11-12). The far renderer keeps every far
// instance's transforms in GPU buffers, uploaded only when a chunk changes, and draws with indirect commands, so no
// per-instance data is copied each frame. This procedural setup (#pragma instancing_options procedural:AeroFarSetup)
// reads the instance's transforms; shading is untouched. With _AeroFarCulled set, the draw's instances come from the
// visible list AeroFarCulling.compute wrote for this camera. In this path the per-instance construction properties
// are shader statics left at 0: far pieces never dissolve. A shader can define AERO_FAR_SETUP_EXTRA to fill other
// statics; with AERO_FAR_NEEDS_FLOOR it can read aeroFarFloor, the instance's floor (the glass floor's river index).
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
StructuredBuffer<float4x4> _AeroFarObjectToWorld;
StructuredBuffer<float4x4> _AeroFarWorldToObject;
StructuredBuffer<uint> _AeroFarVisible;
float _AeroFarBase;
float _AeroFarFloor;
float _AeroFarCulled;
#ifdef AERO_FAR_NEEDS_FLOOR
// Must match FarInstanceData in InstancedFarChunkRenderer and FarInstance in AeroFarCulling.compute.
struct AeroFarInstance { float3 center; uint batch; float3 extents; float floorIndex; };
StructuredBuffer<AeroFarInstance> _AeroFarInstances;
#endif
#endif

void AeroFarSetup()
{
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    uint index = (uint)_AeroFarBase + unity_InstanceID;
    if (_AeroFarCulled > .5) index = _AeroFarVisible[index];
    unity_ObjectToWorld = _AeroFarObjectToWorld[index];
    unity_WorldToObject = _AeroFarWorldToObject[index];
    #ifdef AERO_FAR_NEEDS_FLOOR
        // Legacy pages hold one floor and pass it per draw; culled draws mix floors and read it per instance.
        float aeroFarFloor = _AeroFarFloor;
        if (_AeroFarCulled > .5) aeroFarFloor = _AeroFarInstances[index].floorIndex;
    #endif
    #ifdef AERO_FAR_SETUP_EXTRA
        AERO_FAR_SETUP_EXTRA
    #endif
#endif
}
#endif
