#ifndef AERO_RAIN_RIPPLES_INCLUDED
#define AERO_RAIN_RIPPLES_INCLUDED
// Raindrop rings on open water. Called from the Stylized Water 3 forward pass
// (Assets/Stylized Water 3/Shaders/Passes/ForwardPass.hlsl, "Aero rain ripples" hook).
// Procedural: no texture or sampler (the water shader is already sampler-heavy), two
// cell layers, and a dynamic branch that skips everything while it is dry.
//
// _AeroRain              0..1 falling rain (AeroWetnessController, same ramp as the drops)
// _AeroWetClock          seconds (AeroWetnessController; fixed in tests)
// _AeroRainRippleParams  x cell size (m), y rings per second per cell, z normal strength, w fade distance (m)
// _AeroRainRippleFoam    ring crest foam 0..1
// Both ripple globals are published by AeroRainController from AeroRainProfile.
#ifndef AERO_WETNESS_INCLUDED
float _AeroRain, _AeroWetClock;
#endif
float4 _AeroRainRippleParams;
float _AeroRainRippleFoam;

float2 AeroRippleHash2(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453);
}

// One cell layer: xy = outward slope of the ring (unit scale), z = ring crest 0..1.
float3 AeroRainRippleLayer(float2 xz, float t, float rain, float2 seed)
{
    float2 cell = floor(xz);
    float2 f = xz - cell;
    // Per-cell phase so neighbouring cells never pulse together.
    float clock = t + AeroRippleHash2(cell + seed).x;
    float cycle = floor(clock);
    float life = clock - cycle;
    float2 roll = AeroRippleHash2(cell + seed + cycle * 7.31);
    // Heavier rain fires more cells each cycle.
    float spawn = step(roll.x, rain);
    // Centre in [.3,.7] and radius <= .3 keep each ring inside its own cell (one lookup per layer).
    float2 centre = .3 + .4 * AeroRippleHash2(cell * 1.7 + seed.yx + cycle * 3.17);
    float2 d = f - centre;
    float dist = length(d) + 1e-4;
    float x = (dist - life * .3) / .055;
    float band = step(abs(x), 1);
    float fade = (1 - life) * (1 - life) * spawn * band;
    float slope = -sin(x * 3.14159265) * fade;
    float crest = (cos(x * 3.14159265) * .5 + .5) * fade;
    return float3(d / dist * slope, crest);
}

// xy = world-space XZ normal bend, z = crest foam. Zero while dry, far away or on non-upward faces.
float3 AeroRainRipples(float3 positionWS, float3 vertexNormal)
{
    float3 o = 0;
    float rain = saturate(_AeroRain);
    float4 p = _AeroRainRippleParams.x > 0 ? _AeroRainRippleParams : float4(.9, 1.2, .7, 45);
    float fade = 1 - smoothstep(p.w * .4, p.w, distance(positionWS, _WorldSpaceCameraPos));
    float weight = rain * fade * saturate(vertexNormal.y * 2 - 1);
    [branch] if (weight > .001)
    {
        float t = _AeroWetClock * p.y;
        o += AeroRainRippleLayer(positionWS.xz / p.x, t, rain, float2(0, 0));
        o += AeroRainRippleLayer(positionWS.xz / (p.x * .73) + 17.3, t * 1.13 + .5, rain, float2(41.7, 13.1));
        o.xy *= weight * p.z;
        o.z = saturate(o.z * weight * _AeroRainRippleFoam);
    }
    return o;
}
#endif
