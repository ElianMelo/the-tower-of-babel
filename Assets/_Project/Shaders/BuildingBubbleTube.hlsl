#ifndef TOWER_BUBBLE_TUBE_INCLUDED
#define TOWER_BUBBLE_TUBE_INCLUDED
// Opaque bubble-tube interior for the instanced pillar. The glass cylinder is real geometry;
// water, the pearl spine, rising bubbles and caustics are traced in object space (Y up), so
// thousands of instances keep one opaque, instanced draw with no transparent sorting.
// Mesh contract (Tools/BuildingAero/build_bubble_tube_pillar.py): tube from y .485 to 3.515,
// glass radius .362. Each instance seeds its bubble amount and water motion from its origin.
// References: Frutiger Reference/Asset/Bubble-*-User.* (iridescent rims, varied and wobbling
// shapes, rare large bubbles, and shapes bent by the glass).

#define BT_Y0 .485
#define BT_Y1 3.515
#define BT_GLASS_R .362
#define BT_WATER_R .336
#define BT_SMALL_LAYERS 3

float BtHash(float2 p)
{
    float3 q = frac(p.xyx * float3(.1031, .1030, .0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

float BtNoise1(float x)
{
    float i = floor(x), f = frac(x);
    f = f * f * (3 - 2 * f);
    return lerp(BtHash(float2(i, 7.1)), BtHash(float2(i + 1, 7.1)), f);
}

struct BubbleTubeInstance
{
    float bubbles;   // base probability that a bubble cell is occupied
    float motion;    // water motion strength: ripples, caustic speed, swirl, bubble wobble
    float phase;     // de-synchronises neighbouring pillars
    float swirl;     // signed swirl direction
};

BubbleTubeInstance BubbleTubeVariation(float seed)
{
    BubbleTubeInstance v;
    float a = BtHash(float2(seed * 91.7, 3.1));
    float b = BtHash(float2(seed * 47.3, 8.9));
    float c = BtHash(float2(seed * 13.1, 1.7));
    // Skewed toward fewer bubbles: most pillars are calm, a few are lively.
    v.bubbles = lerp(_BubbleAmountMin, _BubbleAmountMax, lerp(.5, a * a, _InstanceVariation));
    v.motion = lerp(_WaterMotionMin, _WaterMotionMax, lerp(.5, b, _InstanceVariation));
    // Phase and swirl also follow _InstanceVariation, so 0 renders every pillar identically.
    v.phase = c * 97.0 * _InstanceVariation;
    v.swirl = lerp(.5, c, _InstanceVariation) * 2 - 1;
    return v;
}

// Emission rate when a bubble left the floor: long calm spells broken by streams of bubbles.
float BubbleTubeBurst(float emitTime, float phase)
{
    float slow = BtNoise1(emitTime * .085 + phase);
    float fast = BtNoise1(emitTime * .29 + phase * 1.7);
    float stream = saturate((slow * .7 + fast * .3 - .45) * 2.8);
    return lerp(1, lerp(.2, 2.4, stream * stream), _BubbleBurstiness);
}

float BtSpineRadius(float y)
{
    float s = sin(PI * (y - BT_Y0) / .606);
    return .065 + .028 * s * s;
}

// Ray against an infinite vertical cylinder of radius r. False when the ray misses it.
bool BtCylinder(float2 o, float2 d, float r, out float t0, out float t1)
{
    float a = max(dot(d, d), 1e-5), b = dot(o, d), c = dot(o, o) - r * r;
    float disc = b * b - a * c;
    float s = sqrt(max(disc, 0));
    t0 = (-b - s) / a; t1 = (-b + s) / a;
    return disc >= 0;
}

float BtCaustic(float2 uv, float time, float motion)
{
    float speed = .25 + 1.1 * motion;
    float2 p = uv * float2(11, 7);
    float w = sin(p.x + 1.4 * sin(p.y * 1.3 + time * speed) + time * speed * .7)
            + sin(p.y * 1.1 - 1.2 * sin(p.x * .9 - time * speed * 1.1) - time * speed * .5);
    return pow(saturate(1 - abs(w * .5)), 7);
}

// Hand-blown glass: a fixed, per-instance waviness in the wall's surface normal.
float3 BtGlassNormal(float3 p, float phase, float strength, float pattern = 0)
{
    float a = atan2(p.z, p.x);
    phase += pattern * 2.7;
    float3 radial = normalize(float3(p.x, 0, p.z));
    float3 around = float3(-radial.z, 0, radial.x);
    // Broad warps plus fine horizontal ripples, like drawn glass tubing.
    float2 wave = float2(sin(a * 5 + p.y * 3.1 + phase) + .5 * sin(a * 11 - p.y * 7.3 + phase * 2.1)
                         + .45 * sin(p.y * 19 + a * 1.5 + phase * 3.3),
                         sin(p.y * 4.3 + a * 2 + phase * .7) + .5 * sin(p.y * 9.1 - a * 7 + phase)
                         + .6 * sin(p.y * 23 - a * .8 + phase * 1.9));
    return normalize(radial + (around * wave.x + float3(0, 1, 0) * wave.y) * .12 * strength);
}

half3 BtFilm(float hue)
{
    // Thin-film palette from the references: pink, violet, cyan and gold, kept pale.
    half3 rainbow = .5 + .5 * cos(TWO_PI * (hue + float3(0, .33, .67)));
    return lerp(half3(1, 1, 1), rainbow, .85);
}

// One bubble in one cell of a rising shell. rgb: light added; a: coverage of the water behind.
float4 BtBubble(float2 cellUV, float2 id, float layer, BubbleTubeInstance v, float time,
                float density, float sizeMin, float sizeMax, float wobble)
{
    float h = BtHash(id + float2(layer * 7.3, v.phase));
    float present = step(h, density);
    float hs = BtHash(id.yx + 4.1 + layer);
    float size = lerp(sizeMin, sizeMax, hs * hs * hs);              // mostly small, rarely large
    // Keep the wobbling outline (up to ~1.3x size) inside its cell so it is never clipped.
    float2 center = .5 + (float2(BtHash(id + 1.7), BtHash(id + 9.2)) - .5) * saturate(1 - 2.7 * size);
    center.x += sin(time * 2.3 + h * 40) * .05 * v.motion;
    float2 f = cellUV - id - center;
    float angle = atan2(f.y, f.x);
    // Larger bubbles wobble and stretch more, as in the references.
    float shape = 1 + min(wobble * (.5 + size * 3), 1.8) * (.08 * sin(2 * angle + time * (1.2 + v.motion) + h * 30)
                                                        + .05 * sin(3 * angle - time * 1.9 + h * 11));
    float2 stretch = float2(1 + .2 * wobble * (hs - .5), 1);
    float rho = length(f * stretch) / (size * shape);
    float inside = 1 - smoothstep(.96, 1.03, rho);
    float rim = smoothstep(.55, .985, rho) * inside;
    float alpha = lerp(.3, .9, BtHash(id + 3.3));                 // some nearly invisible
    float hue = h * 3 + rho * .65 + .15 * sin(angle * 2 + time * .8 + h * 20);
    half3 film = BtFilm(hue);
    float2 local = f * stretch / max(size, 1e-3);
    float spot = 1 - smoothstep(0, .26, length(local - float2(-.36, .38)));
    float spot2 = 1 - smoothstep(0, .12, length(local - float2(.40, -.30)));
    float swirl = inside * (.10 + .08 * sin(hue * 8 + angle));
    float3 light = film * (rim * 1.7 + swirl) + spot * 1.1 + spot2 * .6;
    return float4(light * alpha * present * _BubbleIridescence, inside * present * alpha);
}

// Radiance seen through the tube. dOS: ray into the surface; nOS: outward surface normal.
half3 BubbleTubeInterior(float3 pOS, float3 dOS, float3 nOS, BubbleTubeInstance v, float time,
                         half detail, half3 ambient, half3 lightDirOS, half3 lightColor)
{
    float3 origin = pOS;
    float3 d;
    if (length(pOS.xz) > BT_WATER_R + .005)
    {
        // Air -> wavy glass -> water. Bending at both walls magnifies and warps the interior.
        float3 outer = BtGlassNormal(pOS, v.phase, _GlassDistortion);
        float3 inGlass = refract(dOS, outer, 1 / 1.5);
        if (dot(inGlass, inGlass) < .01) inGlass = dOS;
        float g0, g1;
        BtCylinder(pOS.xz, inGlass.xz, BT_WATER_R, g0, g1);
        origin = pOS + inGlass * max(g0, 0);
        float3 inner = BtGlassNormal(origin, v.phase, _GlassDistortion * .7, 1);
        float a = atan2(origin.z, origin.x);
        float3 ripple = float3(-sin(a), 0, cos(a)) * sin(origin.y * 9 + time * (1 + v.motion) + v.phase)
                      + float3(0, 1, 0) * cos(a * 6 - time * 1.3 + v.phase);
        inner = normalize(inner + ripple * .05 * v.motion * detail);
        d = refract(inGlass, inner, 1.5 / 1.33);
        if (dot(d, d) < .01) d = inGlass;
    }
    else
    {
        // Chunk cut faces during construction already sit inside the water.
        d = refract(dOS, nOS, 1 / 1.33);
        if (dot(d, d) < .01) d = dOS;
    }
    d = normalize(d);

    float w0, w1;
    if (!BtCylinder(origin.xz, d.xz, BT_WATER_R + .002, w0, w1))
        return _WaterDeepColor.rgb * ambient;
    float tIn = max(w0, 0);
    float tEnd = w1;
    if (d.y < -1e-4) tEnd = min(tEnd, (BT_Y0 - origin.y) / d.y);
    else if (d.y > 1e-4) tEnd = min(tEnd, (BT_Y1 - origin.y) / d.y);

    // Pearl spine: radius taken at the ray's closest approach to the axis.
    float tc = -dot(origin.xz, d.xz) / max(dot(d.xz, d.xz), 1e-5);
    float spineR = BtSpineRadius(clamp(origin.y + d.y * tc, BT_Y0, BT_Y1));
    float s0, s1;
    bool spine = BtCylinder(origin.xz, d.xz, spineR, s0, s1) && s0 > tIn && s0 < tEnd;
    if (spine) tEnd = s0;
    tEnd = max(tEnd, tIn);
    float3 pe = origin + d * tEnd;
    float path = tEnd - tIn;

    float swirlAngle = v.swirl * time * .12 * v.motion;
    float2 causticUV = float2((atan2(pe.z, pe.x) + swirlAngle) * BT_WATER_R, pe.y + v.phase);
    half caustic = BtCaustic(causticUV, time + v.phase, v.motion) * (.35 + .65 * saturate(v.motion)) * detail;
    half3 endColor;
    if (spine)
    {
        float3 ns = normalize(float3(pe.x, 0, pe.z));
        half diffuse = saturate(dot(ns, lightDirOS)) * .7 + .3;
        endColor = _SpineColor.rgb * (ambient + lightColor * diffuse * .6);
        endColor += _WaterLightColor.rgb * caustic * lightColor * .8;
    }
    else
    {
        endColor = _WaterLightColor.rgb * (ambient * .9 + lightColor * .25);
        endColor += _WaterLightColor.rgb * caustic * lightColor * .3;
    }
    half absorb = 1 - exp(-path * _WaterDensity);
    half3 water = _WaterDeepColor.rgb * (ambient + lightColor * .15);
    half3 color = lerp(endColor, water, absorb);
    // Aquarium lamps in the chrome collars light the water from both ends.
    half lamp = exp(-(pe.y - BT_Y0) * 3.5) + .55 * exp(-(BT_Y1 - pe.y) * 5);
    color += _GlowColor.rgb * _GlowStrength * .07 * lamp;

    // Rising bubbles on concentric shells; front crossings only, nearest shell composited last.
    UNITY_BRANCH
    if (detail > .01 && v.bubbles > .001)
    {
        half3 bubbleLight = ambient * .7 + lightColor * .6;
        [unroll]
        for (int k = 0; k <= BT_SMALL_LAYERS; k++)
        {
            // Layer 0 is a sparse shell of large, wobbling bubbles; 1-3 hold small ones, far to near.
            bool big = k == 0;
            float rk = big ? .19 : lerp(.09, .29, (k - 1) / (BT_SMALL_LAYERS - 1.0));
            float cell = big ? .30 : .11;
            float b0, b1;
            bool crosses = BtCylinder(origin.xz, d.xz, rk, b0, b1);
            float valid = (crosses && b0 > tIn && b0 < tEnd) ? 1 : 0;
            float3 q = origin + d * b0;
            float around = max(2, floor(TWO_PI * rk / cell));
            float u = (atan2(q.z, q.x) / TWO_PI + .5) * around + swirlAngle * around / TWO_PI;
            float column = floor(u);
            float hc = BtHash(float2(column + k * 31.7, v.phase));
            float speed = _BubbleSpeed * (big ? .55 : (.6 + .8 * hc)) * (.75 + .5 * v.motion);
            float rows = (q.y - time * speed) / cell + hc * 13;
            float2 id = float2(column, floor(rows));
            // The bubble seen at height q.y left the floor (q.y - BT_Y0) / speed seconds ago.
            float emitTime = time - (q.y - BT_Y0) / max(speed, .01) + hc * 3;
            float density = v.bubbles * BubbleTubeBurst(emitTime, v.phase) * (big ? _BigBubbleChance : 1);
            float ends = step(BT_Y0 + .03, q.y) * step(q.y, BT_Y1 - .03);
            float4 bubble = BtBubble(float2(u, rows), id, k, v, time, density,
                                     big ? .14 : .09, big ? .36 : .28, big ? 1.6 : 1);
            half depth = exp(-(b0 - tIn) * _WaterDensity * .7);
            half weight = valid * ends * depth * detail;
            color = lerp(color, color * .9 + bubbleLight * .04, bubble.a * .5 * weight);
            color += bubble.rgb * bubbleLight * weight;
        }
    }
    return color;
}
#endif
