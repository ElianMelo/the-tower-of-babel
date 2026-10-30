#ifndef AERO_WETNESS_INCLUDED
#define AERO_WETNESS_INCLUDED
// URP port of the DYNAMIC RainWetness sub graph (HDRP Shader Graph in
// Assets/MoveRotateResize/DYNAMIC- RainWetness). Scene-owned globals are set by
// AeroWetnessController; materials opt in with the _AERO_WETNESS local feature.
//
// _AeroWetness   0..1 film coverage (dew by default, 1 when soaked)
// _AeroRain      0..1 active rain: moving drops, side streaks and ripples
// _AeroWetClock  seconds; a fixed preview clock in tests
// _AeroRainDropParams   x drop tile size (m), y drop speed, z dew drop weight, w normal strength
// _AeroRainStreakParams x streak width (m), y streak height (m), z fall speed, w distortion
// _AeroWetReady  1 once the textures are bound, so unbound defaults never shade
// _AeroWetnessSheltered  film level under cover (dew only, never rain)
// _AeroRainShelter   x,y tower centre XZ, z base radius, w radius decrease per two floors
// _AeroRainShelter2  x floor height, y floor count, z enabled
float _AeroWetness, _AeroRain, _AeroWetClock, _AeroWetReady, _AeroWetnessSheltered;
float4 _AeroRainDropParams, _AeroRainStreakParams, _AeroRainShelter, _AeroRainShelter2;
// One shared repeat sampler: the marble shader is already near the 16-sampler limit of ps_4_0.
TEXTURE2D(_AeroRainDrops); TEXTURE2D(_AeroRainStreaks); TEXTURE2D(_AeroRainRipples);

struct AeroWetMasks
{
    half top;      // upward faces collect a film, dew and ripples
    half side;     // vertical faces receive running streaks
    half film;     // final water coverage 0..1
    half rain;     // active rain 0..1
};

// 1 where the floor above shelters this point from rain. The tower is a stepped cone
// (WallRadius = base - decrease * (floor/2 + 1)), so outer terraces stay exposed.
half AeroRainShelter(float3 positionWS)
{
    float floorHeight = max(_AeroRainShelter2.x, .01);
    float floorIndex = floor((positionWS.y - .2) / floorHeight);
    float above = floorIndex + 1;
    float radiusAbove = max(3, _AeroRainShelter.z - _AeroRainShelter.w * (floor(max(above, 0) * .5) + 1));
    float radius = distance(positionWS.xz, _AeroRainShelter.xy);
    half covered = 1 - smoothstep(radiusAbove - 1.5, radiusAbove + .5, radius);
    // Disabled, below the ground floor, or on the top floor: open sky.
    half valid = step(.5, _AeroRainShelter2.z) * step(-1, floorIndex) * step(above, _AeroRainShelter2.y - 1);
    return covered * valid;
}

AeroWetMasks AeroWetnessMasks(float3 positionWS, half3 geometricNormal, half exposure)
{
    AeroWetMasks m;
    m.top = smoothstep(.45, .92, geometricNormal.y);
    m.side = saturate(1 - abs(geometricNormal.y) * 1.6);
    half ready = step(.5, _AeroWetReady);
    half shelter = AeroRainShelter(positionWS);
    m.film = saturate(lerp(_AeroWetness, min(_AeroWetness, _AeroWetnessSheltered), shelter) * exposure) * ready;
    m.rain = saturate(_AeroRain * exposure * (1 - shelter)) * ready;
    return m;
}

// Texture layout from the package manual: RG normal XY, B per-drop phase, A stationary drops.
half3 AeroDropNormal(float2 uv, half dewWeight, half rainWeight, out half coverage)
{
    half4 d = SAMPLE_TEXTURE2D(_AeroRainDrops, sampler_TrilinearRepeat, uv);
    half phase = frac(d.b + _AeroWetClock * _AeroRainDropParams.y);
    // A drop lands quickly, then shrinks as it spreads into the film.
    half moving = step(.004, d.b) * smoothstep(0, .06, phase) * saturate((1 - phase) * 1.4) * rainWeight;
    half resting = d.a * dewWeight;
    coverage = saturate(max(moving, resting));
    return half3((d.rg * 2 - 1) * coverage, 0);
}

// Packed ripple layout (Lagarde): R ring gradient, GB normal XY, A time offset.
half2 AeroRipple(float2 uv, half weight)
{
    half4 r = SAMPLE_TEXTURE2D(_AeroRainRipples, sampler_TrilinearRepeat, uv);
    half2 perturb = r.gb * 2 - 1;
    half dropFrac = frac(r.a + _AeroWetClock * 1.6);
    half timeFrac = dropFrac - 1 + r.r;
    half dropFactor = saturate(.2 + weight * .8 - dropFrac);
    half final = dropFactor * r.r * sin(clamp(timeFrac * 9, 0, 3) * PI);
    return perturb * final * .35;
}

half3 AeroStreakNormal(float3 positionWS, half3 geometricNormal, half weight, out half coverage)
{
    float2 size = max(_AeroRainStreakParams.xy, .05);
    float fall = _AeroWetClock * _AeroRainStreakParams.z;
    float2 uvX = float2(positionWS.z / size.x, positionWS.y / size.y + fall);
    float2 uvZ = float2(positionWS.x / size.x, positionWS.y / size.y + fall * .93 + .37);
    half4 sx = SAMPLE_TEXTURE2D(_AeroRainStreaks, sampler_TrilinearRepeat, uvX);
    half4 sz = SAMPLE_TEXTURE2D(_AeroRainStreaks, sampler_TrilinearRepeat, uvZ);
    half wx = abs(geometricNormal.x), wz = abs(geometricNormal.z);
    half total = max(wx + wz, .001);
    half4 s = (sx * wx + sz * wz) / total;
    coverage = saturate(s.b * weight);
    half2 n2 = (s.rg * 2 - 1) * coverage * _AeroRainStreakParams.w;
    // Project the streak's tangent-space bend onto the wall plane.
    half3 across = normalize(half3(-geometricNormal.z, 0, geometricNormal.x) + half3(1e-4, 0, 0));
    return across * n2.x + half3(0, 1, 0) * n2.y;
}

// Porosity 0 = polished stone, glass, plastic (gloss and drops); 1 = rough, absorbent.
// Exposure scales how much weather a surface receives (covered interiors < 1).
// beads 0..1 scales visible drops/streak beads. Walkable floors use 0: polished decks read
// as a clean glossy film with soft ripple rings, never as speckle. Props and glass use 1.
void AeroApplyWetnessBeads(inout half3 albedo, inout half smoothness, inout half3 normalWS,
    float3 positionWS, half3 geometricNormal, half porosity, half exposure, half beads)
{
    AeroWetMasks m = AeroWetnessMasks(positionWS, geometricNormal, exposure);
    [branch] if (m.film + m.rain < .001) return;

    // Porous materials darken and saturate as pores fill; sealed ones stay bright.
    half soak = m.film * porosity;
    half luminance = dot(albedo, half3(.2126, .7152, .0722));
    albedo = lerp(luminance.xxx, albedo, 1 + soak * .35) * lerp(1, .58, soak);

    // A standing film is smoother on top faces than on walls.
    half filmGloss = m.film * lerp(.55, 1, m.top);
    smoothness = lerp(smoothness, lerp(.98, .9, porosity), filmGloss);
    normalWS = normalize(lerp(normalWS, geometricNormal, filmGloss * .75));

    half3 bend = 0;
    half dew = saturate(m.film * 4) * _AeroRainDropParams.z;
    float tile = max(_AeroRainDropParams.x, .05);
    half dropCoverage = 0;
    [branch] if (m.top > .001)
    {
        [branch] if (beads > .001)
            bend += AeroDropNormal(positionWS.xz / tile, dew, m.rain, dropCoverage).xzy * m.top * beads;
        dropCoverage *= beads;
        [branch] if (m.rain > .001)
        {
            // Larger, gentler rings on floors; dense ones only where beads are wanted.
            float rippleTile = tile * lerp(3.2, 1.7, beads);
            half2 ripple = AeroRipple(positionWS.xz / rippleTile, m.rain) + AeroRipple(positionWS.xz / (rippleTile * .66) + .43, m.rain);
            bend += half3(ripple.x, 0, ripple.y) * m.top * lerp(.45, 1, beads);
        }
    }
    half streakCoverage = 0;
    [branch] if (m.rain > .001 && m.side > .001)
        bend += AeroStreakNormal(positionWS, geometricNormal, m.rain * m.side, streakCoverage) * lerp(.5, 1, beads);

    // Drops and ripples are sub-pixel far away; fade them before they alias into sparkle.
    half near = 1 - smoothstep(12, 35, distance(GetCameraPositionWS(), positionWS));
    half beaded = max(dropCoverage * m.top, streakCoverage) * near;
    smoothness = lerp(smoothness, .99, beaded);
    normalWS = normalize(normalWS + bend * _AeroRainDropParams.w * near);
}

void AeroApplyWetness(inout half3 albedo, inout half smoothness, inout half3 normalWS,
    float3 positionWS, half3 geometricNormal, half porosity, half exposure)
{
    AeroApplyWetnessBeads(albedo, smoothness, normalWS, positionWS, geometricNormal, porosity, exposure, 1);
}

// Lightweight variant for shaders that only need the gloss response.
half AeroWetGloss(half smoothness, float3 positionWS, half3 geometricNormal, half porosity, half exposure)
{
    AeroWetMasks m = AeroWetnessMasks(positionWS, geometricNormal, exposure);
    return lerp(smoothness, lerp(.98, .9, porosity), m.film * lerp(.55, 1, m.top));
}
#endif
