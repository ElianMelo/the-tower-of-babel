#ifndef AERO_SUN_GLOW_INCLUDED
#define AERO_SUN_GLOW_INCLUDED

// Soft sun: a bright inner circle with a soft edge, a warm glow around it, a wide faint halo, a warm band low in the
// sky on the sun's side, and the sun's colour spread over the whole sky. Shared by the sky, the haze over distant ground and the cloud layer's far haze so all
// three agree. Mirrors AeroSunGlow.Radiance (C#). AeroDayNightCycle pushes these globals every time it applies the
// time of day, and each game camera blends them between the focused look (sun at the screen's centre) and the calm
// edge look (sun at the screen's edge) before it renders; at night they follow the moon. See Documentation/FrutigerAero/Lighting/SUN_GLOW.md.

float4 _AeroSunGlowDir;      // xyz towards the sun (or moon), w 1 once set
float4 _AeroSunGlowColor;    // rgb linear light hue (max channel 1) x visibility, a inner circle whiteness
float4 _AeroSunGlowShape;    // x core radius, y core softness (share of radius), z glow radius, w halo radius; degrees
float4 _AeroSunGlowStrength; // x core, y glow, z halo, w horizon band
float4 _AeroSunGlowTint;     // rgb halo tint, a horizon band height
float4 _AeroSunHaze;         // x haze per metre, y amount, z height falloff per metre, w cloud fade
float _AeroSunHazeBase;      // the haze thins above this height (metres); open water is hazed at this sea level
float _AeroSunGlowVeil;      // 0..1 how much the blue sky gives way to the glow at its centre
float4 _AeroSunGlowSpread;   // x sky spread brightness, y spread power, z whole-sky tint amount

struct AeroSunGlowTerms
{
    float core, glow, halo, wash, spread, above, lowered;
};

AeroSunGlowTerms AeroSunGlowEvaluate(float3 view)
{
    AeroSunGlowTerms t;
    float3 sun = _AeroSunGlowDir.xyz;
    float angle = degrees(acos(clamp(dot(view, sun), -1, 1)));
    float4 shape = _AeroSunGlowShape;
    float inner = shape.x * (1 - shape.y), outer = max(shape.x * (1 + shape.y), inner + 1e-3);
    t.core = 1 - smoothstep(inner, outer, angle);
    t.glow = exp(-(angle * angle) / max(shape.z * shape.z, 1e-4));
    t.halo = pow(1 + angle * angle / max(shape.w * shape.w, 1e-4), -1.5);
    float flatDot = view.x * sun.x + view.z * sun.z;
    float flatLengths = dot(view.xz, view.xz) * dot(sun.xz, sun.xz);
    float side = saturate(flatDot * rsqrt(max(flatLengths, 1e-8)) * .5 + .5);
    t.wash = side * side * side * exp(-max(view.y, 0) / max(_AeroSunGlowTint.a, .01));
    t.spread = pow(saturate(.5 + .5 * dot(view, sun)), max(_AeroSunGlowSpread.y, .1));
    // The sun sets behind the ground; below the horizon only a dimmer glow and halo remain.
    t.above = smoothstep(-.012, .004, view.y);
    t.lowered = lerp(.3, 1, smoothstep(-.12, 0, view.y));
    return t;
}

// Linear HDR light added looking along the unit vector view.
float3 AeroSunGlowRadiance(float3 view, bool includeCore)
{
    AeroSunGlowTerms t = AeroSunGlowEvaluate(view);
    float3 hue = _AeroSunGlowColor.rgb;
    float peak = max(hue.r, max(hue.g, hue.b));
    float3 coreColor = lerp(hue, peak.xxx, _AeroSunGlowColor.a);
    float4 s = _AeroSunGlowStrength;
    float3 color = hue * (t.glow * s.y * t.lowered) +
        hue * _AeroSunGlowTint.rgb * ((t.halo * s.z + t.wash * s.w + t.spread * _AeroSunGlowSpread.x) * t.lowered);
    if (includeCore) color += coreColor * (t.core * s.x * t.above);
    return color;
}

// 0..1: how strongly a view direction lies in the sun's glow; where haze takes on the glow's colour.
float AeroSunGlowMask(float3 view)
{
    AeroSunGlowTerms t = AeroSunGlowEvaluate(view);
    return saturate(t.glow + t.halo);
}

// The sky behind the glow is multiplied by this, so the blue gives way to the glow near a hazy low sun.
float AeroSunGlowSkyKeep(float3 view)
{
    return 1 - _AeroSunGlowVeil * AeroSunGlowMask(view);
}

// The sun's colour spread over the whole sky: the sky moves towards the sun's hue at its own brightness, most on the
// sun's side. A low golden sun warms the whole sky; a high sun leaves it clean.
float3 AeroSunGlowTintSky(float3 sky, float3 view)
{
    float3 hue = _AeroSunGlowColor.rgb * _AeroSunGlowTint.rgb;
    float peak = max(_AeroSunGlowColor.r, max(_AeroSunGlowColor.g, _AeroSunGlowColor.b));
    float broad = pow(saturate(.5 + .5 * dot(view, _AeroSunGlowDir.xyz)), 1.5);
    float amount = saturate(_AeroSunGlowSpread.z * (.35 + .65 * broad) * saturate(peak));
    const float3 lumaWeights = float3(.2126, .7152, .0722);
    float3 warm = hue * (dot(sky, lumaWeights) / max(dot(hue, lumaWeights), 1e-4));
    return lerp(sky, warm, amount);
}

#endif
