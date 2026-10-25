#ifndef TOWER_BUILDING_FINISH_INCLUDED
#define TOWER_BUILDING_FINISH_INCLUDED
// Scene-owned settings stay outside UnityPerMaterial, including on instanced draws.
float _AeroFinishEnabled, _AeroFinishEnvironmentReady, _AeroFinishCapturing;
float _AeroFinishEnvironmentShared;
float4 _AeroFinishSurface, _AeroFinishOptics, _AeroPlanarParams;
float4 _AeroFinishDayTint, _AeroFinishNightTint, _AeroReflectionVolume, _AeroReflectionOrigin;
float4x4 _AeroPlanarMatrix;
TEXTURECUBE(_AeroFinishDay); SAMPLER(sampler_AeroFinishDay);
TEXTURECUBE(_AeroFinishNight); SAMPLER(sampler_AeroFinishNight);
TEXTURE2D(_AeroPlanarTexture); SAMPLER(sampler_AeroPlanarTexture);

void AeroClipReflection(float3 positionWS)
{
    if (_AeroFinishCapturing > .5)
    {
        clip(positionWS.y - _AeroReflectionVolume.x);
        clip(_AeroReflectionVolume.y - positionWS.y);
        clip(_AeroReflectionVolume.z - distance(positionWS, _AeroReflectionOrigin.xyz));
    }
}

half AeroPolish(half existing, float3 positionWS, half stone)
{
    // Low-frequency world-space variation cannot introduce individual tile seams.
    half variation = sin(positionWS.x * .29 + positionWS.z * .17) * sin(positionWS.z * .21 - positionWS.x * .13);
    return saturate(existing + _AeroFinishEnabled * stone * (_AeroFinishSurface.y * .035 + variation * _AeroFinishSurface.z));
}

half3 AeroEnvironment(half3 normal, half3 view, float mip, float daylight)
{
    half3 direction = reflect(-view, normal);
    half3 sampleDay = SAMPLE_TEXTURECUBE_LOD(_AeroFinishDay, sampler_AeroFinishDay, direction, mip).rgb;
    if (_AeroFinishEnvironmentShared > .5)
        return min(sampleDay * lerp(_AeroFinishNightTint.rgb, _AeroFinishDayTint.rgb, saturate(daylight)), half3(3,3,3));
    half3 day = sampleDay * _AeroFinishDayTint.rgb;
    half3 night = SAMPLE_TEXTURECUBE_LOD(_AeroFinishNight, sampler_AeroFinishNight, direction, mip).rgb * _AeroFinishNightTint.rgb;
    return min(lerp(night, day, saturate(daylight)), half3(3,3,3));
}

half3 AeroFinishReflection(half3 baseColor, float3 positionWS, half3 normal, half3 view,
    half planarSurface, float mip, float daylight)
{
    if (_AeroFinishEnabled < .5 || _AeroFinishEnvironmentReady < .5 || _AeroFinishSurface.x < .001 || _AeroFinishCapturing > .5) return baseColor;
    half facing = saturate(abs(dot(normal, view)));
    half fresnel = pow(1 - facing, 5);
    half3 reflected = AeroEnvironment(normal, view, mip, daylight);
    float live = 0;
    if (_AeroPlanarParams.x > .001 && planarSurface > .5 && normal.y > .98 && abs(positionWS.y - _AeroPlanarParams.y) < .025)
    {
        float4 projected = mul(_AeroPlanarMatrix, float4(positionWS, 1));
        float2 uv = projected.xy / max(projected.w, .0001) * .5 + .5;
        #if UNITY_UV_STARTS_AT_TOP
            uv.y = 1 - uv.y;
        #endif
        float edge = saturate(min(min(uv.x, uv.y), min(1-uv.x, 1-uv.y)) * 24);
        float range = _AeroPlanarParams.z;
        live = _AeroPlanarParams.x * edge * step(.001, projected.w) *
            (1 - smoothstep(range * .75, range, distance(positionWS, GetCameraPositionWS())));
        if (live > .001)
            reflected = lerp(reflected, SAMPLE_TEXTURE2D_LOD(_AeroPlanarTexture, sampler_AeroPlanarTexture, saturate(uv), mip).rgb, live);
    }
    half strength = (.055 + .30 * fresnel) * _AeroFinishSurface.x * _AeroFinishEnabled;
    return lerp(baseColor, reflected, strength);
}

half3 AeroThinFilm(half3 normal, half3 view, half region, float daylight)
{
    half rim = pow(1 - saturate(abs(dot(normal, view))), 4);
    half phase = .5 + .5 * sin((1-rim) * 8 + dot(normal, half3(.3,.7,.2)));
    return lerp(half3(.3,.82,1), half3(.73,.57,1), phase) * rim * region *
        _AeroFinishOptics.y * _AeroFinishEnabled * _AeroFinishSurface.w * lerp(.2,1,saturate(daylight));
}

half AeroFilteredGlint(half3 normal, half3 view, half3 lightDirection, float exponent, float distanceFade)
{
    float alignment = saturate(dot(normal, SafeNormalize(view + lightDirection)));
    // Widen subpixel lobes and preserve their energy instead of adding random sparkle dots.
    float variance = max(abs(ddx(alignment)), abs(ddy(alignment)));
    float filtered = min(exponent, 1 / max(variance * 12, .001));
    return pow(alignment, filtered) * (filtered / exponent) * distanceFade;
}
#endif
