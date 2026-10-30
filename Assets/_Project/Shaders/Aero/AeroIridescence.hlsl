#ifndef AERO_IRIDESCENCE_INCLUDED
#define AERO_IRIDESCENCE_INCLUDED
// Thin-film and diffraction colour. The accepted AeroThinFilm rim in
// AeroBuildingFinish.hlsl stays unchanged; these are for new materials.

// Two-beam interference at three wavelengths (650/532/450 nm).
// Returns absolute reflectance per channel for a film of thicknessNm.
half3 AeroThinFilm3(half cosI, float thicknessNm, half filmIor, half baseIor)
{
    cosI = saturate(cosI);
    float sinT2 = (1 - cosI * cosI) / (filmIor * filmIor);
    float cosT = sqrt(saturate(1 - sinT2));
    float opd = 2 * filmIor * thicknessNm * cosT;
    // Air->film always flips phase; film->base flips only into a denser base.
    float shift = baseIor > filmIor ? 0 : .5;
    float3 phase = 2 * PI * (opd / float3(650, 532, 450) + shift);
    float r1 = (1 - filmIor) / (1 + filmIor); r1 *= r1;
    float r2 = (filmIor - baseIor) / (filmIor + baseIor); r2 *= r2;
    // Fresnel grows toward grazing angles.
    float grazing = pow(1 - cosI, 5);
    r1 = lerp(r1, 1, grazing * .6); r2 = lerp(r2, 1, grazing * .6);
    return (half3)saturate(r1 + r2 + 2 * sqrt(r1 * r2) * cos(phase));
}

// Hue-only version normalized to unit luminance, for tinting an existing specular colour.
half3 AeroThinFilmHue(half cosI, float thicknessNm, half filmIor, half baseIor)
{
    half3 r = AeroThinFilm3(cosI, thicknessNm, filmIor, baseIor);
    return r / max(dot(r, half3(.2126, .7152, .0722)), .02);
}

float AeroFilmNoise(float3 p)
{
    float3 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
    float n = dot(i, float3(1, 57, 113));
    float4 a = frac(sin(float4(n, n + 1, n + 57, n + 58)) * 43758.5453);
    float4 b = frac(sin(float4(n + 113, n + 114, n + 170, n + 171)) * 43758.5453);
    float4 m = lerp(a, b, f.z);
    return lerp(lerp(m.x, m.y, f.x), lerp(m.z, m.w, f.x), f.y);
}

// Soap-bubble film: swirling 250-700 nm thickness, drained thinner toward the top.
half3 AeroBubbleFilm(half3 normalWS, half3 view, float3 swirlPosition, float time, half strength)
{
    float3 p = swirlPosition * 2.3 + float3(time * .11, -time * .07, time * .05);
    float swirl = AeroFilmNoise(p + AeroFilmNoise(p * 1.7) * 2.1);
    float drainage = lerp(1.25, .7, saturate(normalWS.y * .5 + .5));
    float thickness = lerp(250, 700, swirl) * drainage;
    return AeroThinFilm3(abs(dot(normalWS, view)), thickness, 1.33, 1.0) * strength;
}

half3 AeroSpectrum(float wavelengthNm)
{
    // Smooth bumps approximating the visible range; outside 400-700 fades to black.
    float x = saturate((wavelengthNm - 400) / 300);
    half3 c = saturate(half3(abs(x * 6 - 4.2) - 1.1, 2 - abs(x * 6 - 2.6), 2 - abs(x * 6 - 1.1)));
    return c * smoothstep(0, .08, x) * smoothstep(1, .9, x);
}

// CD-like grating along tangent: d * |sin(L) + sin(V)| = m * lambda.
half3 AeroDiffraction(half3 tangentWS, half3 lightDir, half3 view, float spacingNm)
{
    float u = abs(dot(tangentWS, lightDir) + dot(tangentWS, view));
    half3 color = 0;
    [unroll] for (int order = 1; order <= 3; order++)
        color += AeroSpectrum(spacingNm * u / order);
    return color / 3;
}
#endif
