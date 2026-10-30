#ifndef TOWER_RIVER_VOLUME_INCLUDED
#define TOWER_RIVER_VOLUME_INCLUDED
// Water inside glass. Each pixel of the slab's top (seen from above) or bottom (seen
// from below) traces the view ray through the real enclosure:
//   lid glass -> air gap -> rippled water surface -> water -> glass bed -> hole below
// sphere-tracing the analytic channel (RiverChannel.hlsl) to find the inner walls.
// The scene behind is refracted through the actual path and absorbed by the actual
// water length (Beer-Lambert), so depth, walls, waterline and bed all read as volume.
//
// Requires: RiverChannel.hlsl, AeroGlass.hlsl (scene depth), the river CBUFFER and
// _RiverBackground / _CausticsTex / _RippleNormal declared by the shader.

struct RiverSlab
{
    float top;        // world Y of the lid's upper face
    float scale;      // slab thickness / 0.2 m (template height)
    float lid;        // lid glass thickness
    float surface;    // water surface depth below the top
    float bed;        // bed (water bottom) depth below the top
    float inset;      // glass wall thickness at the channel sides
};

RiverSlab RiverMakeSlab(float topY, float thicknessScale)
{
    RiverSlab s;
    s.top = topY; s.scale = thicknessScale;
    s.surface = _RiverEnclosure.x * thicknessScale;
    s.bed = _RiverEnclosure.y * thicknessScale;
    s.lid = _RiverEnclosure.z * thicknessScale;
    s.inset = _RiverEnclosure.w;
    return s;
}

// Positive inside the water, measured to the inner glass wall.
float RiverInterior(float2 xz) { return -RiverChannelDistance(xz, _RiverFloorIndex) - _RiverEnclosure.w; }

half3 RiverWallNormal(float2 xz)
{
    const float e = .01;
    float dx = RiverInterior(xz + float2(e, 0)) - RiverInterior(xz - float2(e, 0));
    float dz = RiverInterior(xz + float2(0, e)) - RiverInterior(xz - float2(0, e));
    // Points from the wall into the water.
    return normalize(half3(dx, 0, dz) + half3(1e-5, 0, 0));
}

half3 RiverSurfaceNormal(float2 xz, float t, half strength)
{
    float2 p = xz * _RiverMotion.x;
    float2 swell = float2(sin(p.y * 1.4 + t * 2.3), cos(p.x * 1.15 - t * 1.7)) * .35;
    half3 a = UnpackNormal(SAMPLE_TEXTURE2D(_RippleNormal, sampler_RippleNormal, p + float2(t, t * .73) + swell * .12));
    half3 b = UnpackNormal(SAMPLE_TEXTURE2D(_RippleNormal, sampler_RippleNormal, p * .63 - float2(t * .61, t)));
    return normalize(half3(0, 1, 0) + half3(a.x + b.x + swell.x, 0, a.y + b.y + swell.y) * strength);
}

// Marches from p along d (inside water) until the bed/surface plane at planeY or an
// inner wall. Returns travelled distance; hitWall set when a wall stopped the ray.
float RiverMarch(float3 p, float3 d, float planeY, int steps, out bool hitWall)
{
    hitWall = false;
    float tPlane = (planeY - p.y) / (abs(d.y) > 1e-4 ? d.y : (d.y < 0 ? -1e-4 : 1e-4));
    tPlane = max(tPlane, 0);
    float horizontal = max(length(d.xz), 1e-3);
    float t = 0;
    [loop] for (int i = 0; i < 24; i++)
    {
        if (i >= steps) break;
        float inside = RiverInterior(p.xz + d.xz * t);
        if (inside < .002) { hitWall = true; break; }
        t += inside / horizontal;
        if (t >= tPlane) { t = tPlane; break; }
    }
    // Unresolved grazing rays end on the plane rather than inventing a wall.
    if (!hitWall) t = min(t, tPlane);
    return t;
}

float2 RiverScreenUV(float3 positionWS)
{
    float4 cs = TransformWorldToHClip(positionWS);
    return (float2(cs.x, cs.y * _ProjectionParams.x) / max(cs.w, 1e-4)) * .5 + .5;
}

float RiverEyeDepth(float3 positionWS) { return -TransformWorldToView(positionWS).z; }

// Background seen along an exit ray leaving the slab at exitPoint. The scene distance
// comes from the unrefracted pixel; the depth guard keeps foreground silhouettes sharp.
half3 RiverBackgroundAlong(float3 surfacePoint, float3 exitPoint, float3 exitDir, float2 screenUV, half strength)
{
    float sceneEye = AeroSceneEyeDepth(screenUV);
    float exitEye = RiverEyeDepth(exitPoint);
    float3 forward = -UNITY_MATRIX_V[2].xyz;
    float along = max(sceneEye - exitEye, .05) / max(dot(exitDir, forward), .08);
    // Offset measured against this pixel's own projection, so any platform flip cancels.
    float2 offset = (RiverScreenUV(exitPoint + exitDir * along) - RiverScreenUV(surfacePoint)) * strength;
    offset = clamp(offset, -.08, .08);
    float edge = saturate(min(min(screenUV.x, screenUV.y), min(1 - screenUV.x, 1 - screenUV.y)) * 20);
    float2 uv = saturate(screenUV + offset * edge);
    uv = AeroSceneEyeDepth(uv) > exitEye - .02 ? uv : screenUV;
    return SAMPLE_TEXTURE2D_X(_RiverBackground, sampler_RiverBackground, uv).rgb;
}

// Focused light inside the volume, sampled the Crest way (Aero/AeroCaustics.hlsl): two
// averaged scales of the chromatic net above its grey point. Additive light, linear in the
// caller's opacity; blurBias raises the mip for softer, out-of-focus detail.
half3 RiverCaustic(float2 xz, float t, float blurBias)
{
    float tile = 1 / max(_RiverPattern.x * 2.5, .01);
    float lod = log2(max(distance(GetCameraPositionWS().xz, xz), 1)) * .35 + blurBias * .6;
    half3 c = AeroCrestCaustics(TEXTURE2D_ARGS(_CausticsTex, sampler_TrilinearRepeat), xz, _RiverClock * .6, tile, lod);
    return max(c - AERO_CAUSTICS_GREY, 0) * 3.4;
}
// Deep/shallow aqua by path length, so long views and wall-adjacent pixels saturate.
half3 RiverScatterColor(float path)
{
    return lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(path / max(_RiverVolume.w, .05)));
}

// Sparse micro-bubbles on three depth layers: white spec dot with a dark rim.
half4 RiverBubbles(float3 entry, float3 dir, float surfaceY, float bedY, float t)
{
    half4 sum = 0;
    [unroll] for (int k = 0; k < 3; k++)
    {
        float y = lerp(surfaceY, bedY, .25 + k * .25);
        float travel = (y - entry.y) / (abs(dir.y) > 1e-4 ? dir.y : -1e-4);
        if (travel <= 0) continue;
        float2 q = (entry.xz + dir.xz * travel) * _RiverDetail.x + float2(t * .35, t * .2) * (k + 1);
        float2 cell = floor(q), f = frac(q) - .5;
        float2 h = frac(sin(float2(dot(cell, float2(127.1, 311.7)), dot(cell, float2(269.5, 183.3))) + k * 17.1) * 43758.5453);
        float present = step(.86, h.x);
        float2 c = f - (h - .5) * .5;
        float radius = lerp(.035, .07, h.y);
        float r = length(c) / radius;
        half disc = present * saturate(1 - r);
        half rim = present * smoothstep(.55, .85, r) * (1 - smoothstep(.85, 1, r));
        half dot_ = present * (1 - smoothstep(0, .28, length(c / radius - float2(-.3, .35))));
        sum += half4(dot_.xxx * 1.4 - rim * .25, max(rim * .35, dot_)) * (1 - k * .2);
    }
    return sum;
}
#endif
