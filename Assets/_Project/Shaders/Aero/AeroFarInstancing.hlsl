#ifndef AERO_FAR_INSTANCING_INCLUDED
#define AERO_FAR_INSTANCING_INCLUDED
// GPU-resident far tower instancing (performance pass 2026-11-03). The far renderer uploads each render cell's
// transforms to GPU buffers once, when the cell changes, and draws its pages with Graphics.RenderMeshIndirect, so
// no per-instance data is copied each frame. This procedural setup (#pragma instancing_options
// procedural:AeroFarSetup) reads the instance's transforms; shading is untouched. In this path the per-instance
// construction properties are shader statics left at 0: far pieces never dissolve. A shader can define
// AERO_FAR_SETUP_EXTRA to fill other statics (the glass floor's river floor index).
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
StructuredBuffer<float4x4> _AeroFarObjectToWorld;
StructuredBuffer<float4x4> _AeroFarWorldToObject;
float _AeroFarBase;
float _AeroFarFloor;
#endif

void AeroFarSetup()
{
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    uint index = (uint)_AeroFarBase + unity_InstanceID;
    unity_ObjectToWorld = _AeroFarObjectToWorld[index];
    unity_WorldToObject = _AeroFarWorldToObject[index];
    #ifdef AERO_FAR_SETUP_EXTRA
        AERO_FAR_SETUP_EXTRA
    #endif
#endif
}
#endif
