#ifndef AERO_CLOUDS_INCLUDED
#define AERO_CLOUDS_INCLUDED

// Volumetric cloud layer for Tower of Babel.
// Lighting model after HDRP's volumetric clouds (and its URP port by jiaozi158, MIT): energy-conserving
// integration, dual-lobe Henyey-Greenstein with multi-scattering octaves (Wrenninge), powder term (Schneider,
// Horizon Zero Dawn). Density after Schneider's Nubis talks and Sebastian Lague's Clouds (MIT): coverage map,
// height gradient, Perlin-Worley shape eroded by Worley detail. Independent implementation.
// All parameters arrive through a MaterialPropertyBlock (see AeroVolumetricCloudsRendererFeature).

TEXTURE3D(_AeroCloudShapeNoise);
TEXTURE3D(_AeroCloudDetailNoise);
TEXTURE2D(_AeroCloudCoverage);
TEXTURE2D_X_FLOAT(_AeroCloudDepth);
TEXTURE2D_X(_AeroCloudHistory);

float4 _AeroCloudLayer;      // x base altitude, y shell top (base + height variation + thickness), z planet radius, w cloud thickness
float4 _AeroCloudHeights;    // x height variation (metres), y height-noise repeats per coverage tile
float4 _AeroCloudShape;      // x coverage, y extinction per metre, z 1/shape tile, w 1/detail tile
float4 _AeroCloudShape2;     // x erosion, y cloud type, z 1/coverage tile, w edge softness
float4 _AeroCloudWind;       // xy drift (x, z metres), z boiling offset (metres), w shear (metres)
float4 _AeroCloudWindDir;    // xy wind direction (x, z)
float4 _AeroCloudLighting;   // x powder, y forward eccentricity, z multi-scatter, w darkness
float4 _AeroCloudMarch;      // x primary steps, y light steps, z frame noise offset, w max step
float4 _AeroCloudHaze;       // x haze distance, y haze strength, z fade start, w fade end
float4 _AeroCloudSunDir;
float4 _AeroCloudSunColor;
float4 _AeroCloudAmbientTop;
float4 _AeroCloudAmbientBottom;
float4 _AeroCloudFogColor;
float4 _AeroCloudCamera;     // xyz camera world position, w altitude used for the planet
float4 _AeroCloudForward;    // xyz camera forward
float4 _AeroCloudTexel;      // xy cloud texel size, zw cloud size
float4 _AeroCloudDepthTexel; // xy full-resolution texel size, z downsample
float4 _AeroCloudTemporal;   // x history weight (0 = reset)
float4x4 _AeroCloudPrevView; // last frame's world-to-camera matrix
float4 _AeroCloudComposite;  // x: 0 composite before transparents, 1 after (downward rays from inside or above)

#define AERO_CLOUD_NO_HIT 1e7

float3 AeroCloudRay(float2 uv)
{
    float3 world = ComputeWorldSpacePosition(uv, .5, UNITY_MATRIX_I_VP);
    return normalize(world - _WorldSpaceCameraPos);
}

float2 AeroCloudPrevUV(float3 worldPosition)
{
    float4 clip = mul(UNITY_MATRIX_P, mul(_AeroCloudPrevView, float4(worldPosition, 1)));
    float2 uv = clip.xy / max(clip.w, 1e-5) * .5 + .5;
    #if UNITY_UV_STARTS_AT_TOP
        uv.y = 1 - uv.y;
    #endif
    return clip.w > 0 ? uv : float2(-1, -1);
}

bool AeroIsSky(float rawDepth)
{
    #if UNITY_REVERSED_Z
        return rawDepth <= 1e-7;
    #else
        return rawDepth >= 1 - 1e-7;
    #endif
}

float AeroSceneDistance(float rawDepth, float3 ray)
{
    if (AeroIsSky(rawDepth)) return AERO_CLOUD_NO_HIT;
    // Distance along the ray: eye depth / cos(angle to the view axis).
    float eye = LinearEyeDepth(rawDepth, _ZBufferParams);
    return eye / max(dot(ray, _AeroCloudForward.xyz), 1e-3);
}

// Ray against a sphere centred on the planet, origin height h above sea and sphere altitude a (both metres).
// Written as (h - a) * (2R + h + a) so the huge radius never cancels in float precision.
float2 AeroSphere(float originHeight, float rayUp, float altitude)
{
    float R = _AeroCloudLayer.z;
    float ro = R + originHeight;
    float b = ro * rayUp;
    float c = (originHeight - altitude) * (2 * R + originHeight + altitude);
    float h = b * b - c;
    if (h < 0) return float2(-1, -1);
    h = sqrt(h);
    float q = -(b + (b >= 0 ? h : -h));
    float t0 = q, t1 = abs(q) > 1e-6 ? c / q : 0;
    return float2(min(t0, t1), max(t0, t1));
}

// Distances (start, end) of the ray inside the cloud layer; start < 0 when it misses.
float2 AeroCloudInterval(float3 ray)
{
    float h = _AeroCloudCamera.w, up = ray.y;
    float2 ground = AeroSphere(h, up, 0);
    float2 bottom = AeroSphere(h, up, _AeroCloudLayer.x);
    float2 top = AeroSphere(h, up, _AeroCloudLayer.y);
    float2 range;
    if (h < _AeroCloudLayer.x)
    {
        if (ground.x > 0) return float2(-1, -1); // the planet blocks the view
        range = float2(bottom.y, top.y);
    }
    else if (h < _AeroCloudLayer.y)
        range = float2(0, bottom.x > 0 ? bottom.x : top.y);
    else
    {
        if (top.x < 0) return float2(-1, -1);
        range = float2(top.x, bottom.x > 0 ? bottom.x : top.y);
    }
    range.y = min(range.y, _AeroCloudHaze.w);
    return range.x < range.y ? range : float2(-1, -1);
}

float AeroAltitude(float3 relative)
{
    // Height above sea of camera + relative, measured along the planet's radius.
    float R = _AeroCloudLayer.z;
    float3 p = float3(relative.x, R + _AeroCloudCamera.w + relative.y, relative.z);
    return length(p) - R;
}

// Rounded underside, in metres so a cloud's base stays near the preset's base altitude whatever the layer's
// thickness. The ramp starts `lift` metres up, so each column's belly sits at its own height and the noise shapes
// where the underside begins, instead of every cloud sharing one flat floor.
float AeroBaseRamp(float metresAboveBase, float thickness, float type, float lift)
{
    float ramp = min(lerp(50, 90, type), thickness * .3);
    float b = saturate((metresAboveBase - min(lift, thickness * .2)) / ramp);
    return 1 - (1 - b) * (1 - b);
}

float AeroHeightProfile(float h, float type, float baseRamp)
{
    // 0 stratus: thin sheet low in the layer; .5 cumulus: rounded bases, domed tops; 1 cumulonimbus: full height.
    float topStart = lerp(.2, .7, type);
    float topEnd = lerp(.45, 1, type);
    return baseRamp * (1 - smoothstep(topStart, topEnd, h));
}

// Metres this column's cloud floats above the layer base. The map's height channel, sampled at ~1 km patches,
// gives each cloud its own base, so clouds sit at different heights instead of on one shared floor.
float AeroCloudFloat(float2 cloudXZ)
{
    if (_AeroCloudHeights.x <= 0) return 0;
    float g = SAMPLE_TEXTURE2D_LOD(_AeroCloudCoverage, sampler_LinearRepeat, cloudXZ * _AeroCloudShape2.z * _AeroCloudHeights.y + float2(.37, .61), 0).g;
    float x = saturate((g - .25) / .55); // the channel spans ~.25-.8; spread it over 0..1
    // Squared: most clouds stay near the base (the top tower floors can enter them), fewer float high.
    return _AeroCloudHeights.x * x * x;
}

// Density before detail erosion; h = height inside this column's cloud (0 base, 1 top);
// coverage = local cloud amount for this column.
float AeroBaseDensity(float3 world, float altitude, float lod, out float h, out float coverage)
{
    float metres = altitude - _AeroCloudLayer.x;
    h = 0; coverage = 0;
    if (metres <= 0 || altitude >= _AeroCloudLayer.y) return 0;
    float2 drift = _AeroCloudWind.xy;
    metres -= AeroCloudFloat(world.xz + drift);
    h = metres / _AeroCloudLayer.w;
    if (h <= 0 || h >= 1) return 0;
    float2 map = SAMPLE_TEXTURE2D_LOD(_AeroCloudCoverage, sampler_LinearRepeat, (world.xz + drift) * _AeroCloudShape2.z, 0).rg;
    float amount = _AeroCloudShape.x;
    // The equalized map decides where clouds form (a share `amount` of the sky); inside those regions local
    // coverage rises fast, so the shape noise only breaks up edges instead of thresholding the area a second time.
    coverage = saturate((map.r - (1 - amount)) / lerp(.08, .3, _AeroCloudShape2.w) + .35);
    coverage = min(coverage, saturate(amount * 4)); // a near-zero amount leaves an empty sky
    if (coverage <= 0) return 0;
    // Denser columns reach higher; the map's second channel varies the tops.
    float topScale = lerp(.55, 1, saturate(coverage * .7 + map.g * .5));
    // Thinner columns start higher (up to ~55 m), so cloud edges lift away from the base.
    float lift = (1 - map.g) * 30 + (1 - coverage) * 25;
    float baseRamp = AeroBaseRamp(metres, _AeroCloudLayer.w, _AeroCloudShape2.y, lift);
    float profile = AeroHeightProfile(h / topScale, _AeroCloudShape2.y, baseRamp);
    if (profile <= 0) return 0;
    float3 shapePos = float3(world.x + drift.x, altitude + _AeroCloudWind.z, world.z + drift.y);
    shapePos.xz += _AeroCloudWindDir.xy * (h * _AeroCloudWind.w);
    float shape = SAMPLE_TEXTURE3D_LOD(_AeroCloudShapeNoise, sampler_LinearRepeat, shapePos * _AeroCloudShape.z, lod).r;
    // Remap above the coverage threshold so every cloud reaches full density in its core, however low the
    // coverage. Softness lengthens the fringe; transparency comes from density, not from a weak core that
    // detail erosion would eat away.
    return saturate((shape * profile - (1 - coverage)) / max(coverage * lerp(.6, 1.4, _AeroCloudShape2.w), .05));
}

float AeroErode(float base, float3 world, float altitude, float h, float coverage, float lod)
{
    float3 detailPos = float3(world.x + _AeroCloudWind.x * 1.6, altitude - _AeroCloudWind.z * 2, world.z + _AeroCloudWind.y * 1.6);
    float detail = SAMPLE_TEXTURE3D_LOD(_AeroCloudDetailNoise, sampler_LinearRepeat, detailPos * _AeroCloudShape.w, lod).r;
    // Wispy, torn bases; billowy tops.
    detail = lerp(1 - detail, detail, saturate(h * 3));
    float erode = detail * _AeroCloudShape2.x * .85 * lerp(1, .65, coverage);
    return saturate((base - erode) / max(1 - erode, .05));
}

float AeroHG(float cosAngle, float g)
{
    float g2 = g * g;
    return (1 - g2) / (4 * PI * pow(max(1 + g2 - 2 * g * cosAngle, 1e-4), 1.5));
}

float AeroLightDepth(float3 relative, float3 sunDir, float baseH, float lod)
{
    int steps = (int)_AeroCloudMarch.y;
    float reach = min(_AeroCloudLayer.w * 1.2, 2400) * lerp(1, .6, baseH);
    float depth = 0, previous = 0;
    [loop] for (int i = 0; i < steps; i++)
    {
        float s = (i + 1.0) / steps;
        float t = reach * s * s;
        float3 p = relative + sunDir * (t + previous) * .5;
        float h, coverage;
        float altitude = AeroAltitude(p);
        float d = AeroBaseDensity(p + float3(_AeroCloudCamera.x, 0, _AeroCloudCamera.z), altitude, lod + s * 2, h, coverage);
        depth += d * (t - previous);
        previous = t;
    }
    return depth * _AeroCloudShape.y * (1 + _AeroCloudLighting.w * 2.5);
}

float AeroInterleavedNoise(float2 pixel)
{
    pixel += _AeroCloudMarch.z * float2(47, 17) * .695;
    return frac(52.9829189 * frac(dot(pixel, float2(.06711056, .00583715))));
}

// From inside or above the layer, rays that look down cross the cloud before reaching water; those pixels are
// composited after transparents so the ocean does not paint over the cloud. All others go before transparents.
bool AeroCompositeAfterTransparents(float3 ray) { return ray.y < 0 && _AeroCloudCamera.w > _AeroCloudLayer.x; }

bool AeroAboveHorizon(float3 ray) { return AeroSphere(_AeroCloudCamera.w, ray.y, 0).x <= 0; }

// Overcast skies keep a grey haze down to the horizon, where the deck lies beyond the fade distance.
float AeroHazeWall(float3 ray, float sceneDistance)
{
    if (sceneDistance < _AeroCloudHaze.w || !AeroAboveHorizon(ray)) return 0;
    float entry = 1e9;
    if (_AeroCloudCamera.w < _AeroCloudLayer.x) entry = AeroSphere(_AeroCloudCamera.w, ray.y, _AeroCloudLayer.x).y;
    return smoothstep(.55, .95, _AeroCloudShape.x) * _AeroCloudHaze.y * smoothstep(_AeroCloudHaze.z, _AeroCloudHaze.w, entry);
}

// rgb = scattered light, a = transmittance (premultiplied for Blend One SrcAlpha).
float4 AeroTraceClouds(float2 uv, float2 pixel, float sceneDistance)
{
    float3 ray = AeroCloudRay(uv);
    float2 range = AeroCloudInterval(ray);
    float wall = AeroHazeWall(ray, sceneDistance);
    if (range.x < 0 || range.x >= sceneDistance) return float4(_AeroCloudFogColor.rgb * wall, 1 - wall);
    range.y = min(range.y, sceneDistance);

    int steps = (int)_AeroCloudMarch.x;
    float stepSize = clamp((range.y - range.x) / steps, 25, _AeroCloudMarch.w);
    float3 sunDir = _AeroCloudSunDir.xyz;
    float cosAngle = dot(ray, sunDir);
    float g = _AeroCloudLighting.y, ms = _AeroCloudLighting.z;
    float phase0 = lerp(AeroHG(cosAngle, g), AeroHG(cosAngle, -.25), .3);
    float phase1 = lerp(AeroHG(cosAngle, g * ms), AeroHG(cosAngle, -.25 * ms), .3);
    float phase2 = lerp(AeroHG(cosAngle, g * ms * ms), AeroHG(cosAngle, -.25 * ms * ms), .3);
    float powderFacing = smoothstep(.5, -.5, cosAngle);
    float transmittance = 1;
    float3 sun = 0, ambient = 0;
    float meanDistance = 0, meanWeight = 0;
    // Inside a cloud (the top tower floors) steps start short and grow with distance, so the fog around the
    // camera is sampled finely; from outside the layer, range.x is far and every step is stepSize.
    float t = range.x + min(stepSize, max(8, range.x * .25)) * AeroInterleavedNoise(pixel);
    [loop] for (int i = 0; i < steps && t < range.y; i++)
    {
        float step = min(stepSize, max(8, t * .25));
        // Long steps (toward the horizon) read coarser mips so distant clouds do not sparkle.
        float shapeLod = clamp(log2(step * _AeroCloudShape.z * 128) - 1, 0, 5);
        float detailLod = clamp(log2(step * _AeroCloudShape.w * 32) - 1, 0, 4);
        float3 relative = ray * t;
        float3 world = relative + float3(_AeroCloudCamera.x, 0, _AeroCloudCamera.z);
        float altitude = AeroAltitude(relative);
        float h, coverage;
        float base = AeroBaseDensity(world, altitude, shapeLod, h, coverage);
        if (base <= 0) { t += step * 1.6; continue; }
        float density = AeroErode(base, world, altitude, h, coverage, detailLod);
        // Thin the cloud right around the camera so a player building inside one still sees nearby blocks.
        density *= smoothstep(0, 45, t);
        if (density > .001)
        {
            float extinction = density * _AeroCloudShape.y;
            float stepTransmittance = exp(-extinction * step);
            float weight = transmittance * (1 - stepTransmittance);
            float lightDepth = AeroLightDepth(relative, sunDir, h, shapeLod);
            float sunTerm = exp(-lightDepth) * phase0 + ms * exp(-lightDepth * ms) * phase1 + ms * ms * exp(-lightDepth * ms * ms) * phase2;
            float powder = lerp(1, saturate((1 - exp(-extinction * 60)) * 2), _AeroCloudLighting.x * powderFacing);
            sun += sunTerm * powder * 4 * PI * weight;
            float ambientOcclusion = lerp(.45, 1, saturate(h * 1.4)) * lerp(1, .55, _AeroCloudLighting.w);
            ambient += lerp(_AeroCloudAmbientBottom.rgb, _AeroCloudAmbientTop.rgb, saturate(h * 1.2)) * ambientOcclusion * weight;
            meanDistance += t * weight; meanWeight += weight;
            transmittance *= stepTransmittance;
            if (transmittance < .01) { transmittance = 0; break; }
        }
        t += step;
    }
    if (meanWeight <= 0) return float4(_AeroCloudFogColor.rgb * wall, 1 - wall);
    meanDistance /= meanWeight;
    float3 color = sun * _AeroCloudSunColor.rgb + ambient;
    // Aerial perspective: distant clouds melt into the horizon haze, then fade out.
    float haze = (1 - exp(-meanDistance / _AeroCloudHaze.x)) * _AeroCloudHaze.y;
    color = lerp(color, _AeroCloudFogColor.rgb * (1 - transmittance), haze);
    float fade = 1 - smoothstep(_AeroCloudHaze.z, _AeroCloudHaze.w, meanDistance);
    return float4(lerp(_AeroCloudFogColor.rgb * wall, color, fade), lerp(1 - wall, transmittance, fade));
}

#endif
