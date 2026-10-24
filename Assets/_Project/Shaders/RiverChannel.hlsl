#ifndef TOWER_RIVER_CHANNEL_INCLUDED
#define TOWER_RIVER_CHANNEL_INCLUDED
// Keep aligned with GlassWaterFloorLayout.ChannelDistance. Floor identity is supplied
// by the draw, never inferred from a shaded underside's world height.
float RiverChannelDistance(float2 worldXZ, float floorIndex)
{
    float wall = max(3, _RiverLayout.x - _RiverLayout.y * (floor(floorIndex * .5) + 1));
    float2 p = worldXZ - _RadialFloorCenter.xy;
    float radius = length(p);
    float width = min(_RiverLayout.w, max(.55, wall * .014));
    float ring = min(abs(radius - wall * .5), abs(radius - wall * .88));
    float spoke = min(abs(p.x), abs(p.y));
    return max(min(min(ring, spoke) - width, radius - wall * .16), radius - wall * .96);
}
#endif
