#ifndef AERO_CLOUDS_INCLUDED
#define AERO_CLOUDS_INCLUDED

// Volumetric cloud layer for Tower of Babel.
// Lighting model after HDRP's volumetric clouds (and its URP port by jiaozi158, MIT): energy-conserving
// integration, dual-lobe Henyey-Greenstein with multi-scattering octaves (Wrenninge), powder term (Schneider,
// Horizon Zero Dawn). Density after Schneider's Nubis talks and Sebastian Lague's Clouds (MIT): coverage map,
// height gradient, Perlin-Worley shape eroded by Worley detail. Independent implementation.
// All parameters arrive through a MaterialPropertyBlock (see AeroVolumetricCloudsRendererFeature), except the soft
// sun glow globals (AeroSunGlow.hlsl): far clouds near the sun melt into its halo instead of a flat fog colour.
#include "AeroSunGlow.hlsl"

TEXTURE3D(_AeroCloudShapeNoise);
TEXTURE3D(_AeroCloudDetailNoise);
TEXTURE2D(_AeroCloudCoverage);
TEXTURE2D(_AeroCloudCirrus);
SAMPLER(sampler_AeroCloudCirrus); // trilinear + anisotropic: the sheet is seen at grazing angles
TEXTURE2D_X_FLOAT(_AeroCloudDepth);
TEXTURE2D_X(_AeroCloudHistory);

float4 _AeroCloudLayer;      // x base altitude, y shell top (base + height variation + thickness), z planet radius, w cloud thickness
float4 _AeroCloudHeights;    // x height variation (metres), y height-noise repeats per coverage tile, z 1/cirrus tile, w cirrus map size
float4 _AeroCloudShape;      // x coverage, y extinction per metre, z 1/shape tile, w 1/detail tile
float4 _AeroCloudShape2;     // x erosion, y cloud type, z 1/coverage tile, w edge softness
float4 _AeroCloudWind;       // xy drift (x, z metres), z boiling offset (metres), w shear (metres)
float4 _AeroCloudWindDir;    // xy wind direction (x, z), z rise offset of growing towers (metres)
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
float4 _AeroCloudTone;       // x sun exposure, y sky-light exposure (keeps sunlit faces off the tonemapper shoulder)
float4 _AeroCloudForm;       // x cirrus fibres (above .5: a hair-textured sheet), y stretch along the wind, z ragged-base share, w sculpt

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
float AeroCloudFloat(float2 cloudXZ, float coverageLod)
{
    if (_AeroCloudHeights.x <= 0) return 0;
    // One lookup at 6x the map's tile repeated on an axis-aligned grid (every ~350 m on small presets). Grazing rays
    // through a thin layer crossed many repeats and summed them into straight lines fanning out from the vanishing
    // point. A second lookup, rotated off the axes at an unrelated scale, gives the sum no common period.
    float2 uv = cloudXZ * _AeroCloudShape2.z * _AeroCloudHeights.y;
    float2 turned = float2(uv.x * .819 - uv.y * .574, uv.x * .574 + uv.y * .819) * .731; // 35 degrees
    float lod = coverageLod + log2(max(_AeroCloudHeights.y, 1));
    float g = SAMPLE_TEXTURE2D_LOD(_AeroCloudCoverage, sampler_LinearRepeat, uv + float2(.37, .61), lod).g * .55
            + SAMPLE_TEXTURE2D_LOD(_AeroCloudCoverage, sampler_LinearRepeat, turned + float2(.13, .29), lod).g * .45;
    // Smooth at both ends: a clamped ramp creased every cloud base along two height contours.
    float x = smoothstep(.3, .72, g);
    // Squared: most clouds stay near the base (the top tower floors can enter them), fewer float high.
    return _AeroCloudHeights.x * x * x;
}

// Stretches a position `stretch` times along the wind (1 leaves it unchanged, so billowy presets keep their
// pattern). Cirrus ice trails downwind into long streaks, so the streaks point the way the sky drifts.
float2 AeroAlongWind(float2 xz, float stretch)
{
    float along = dot(xz, _AeroCloudWindDir.xy);
    return xz - _AeroCloudWindDir.xy * (along * (1 - 1 / stretch));
}

// How far a look's fibres go past a faint texture in the volume (veils, below .5) toward a sheet of separate
// strands (1, see AeroCirrusSheet): 0..1. The volume fades out as the sheet fades in.
float AeroStrandy() { return saturate(_AeroCloudForm.x * 2 - 1); }

// Mip of the 3D shape noise for a sample footprint of `metres` (half-texel bias keeps near clouds crisp).
float AeroFootprintLod(float metres) { return log2(max(metres * _AeroCloudShape.z * 128, 1e-4)) - 1; }

// Density before detail erosion; h = height inside this column's cloud (0 base, 1 top);
// coverage = local cloud amount for this column. `lod` is the sample footprint in shape-noise mips (unclamped).
float AeroBaseDensity(float3 world, float altitude, float lod, out float h, out float coverage)
{
    float metres = altitude - _AeroCloudLayer.x;
    h = 0; coverage = 0;
    if (metres <= 0 || altitude >= _AeroCloudLayer.y) return 0;
    float2 field = world.xz + _AeroCloudWind.xy;
    // The weather map decides where small clouds form, so it must be filtered to the sample footprint like the
    // 3D noise. Read at full resolution, long steps near the horizon skipped across its texels (aliasing) and
    // small-cloud presets showed streaks fanning out from the vanishing point.
    float coverageLod = clamp(lod + log2(2 * _AeroCloudShape2.z / _AeroCloudShape.z), 0, 8);
    metres -= AeroCloudFloat(field, coverageLod);
    h = metres / _AeroCloudLayer.w;
    if (h <= 0 || h >= 1) return 0;
    // Strand cirrus is drawn as a sheet instead (AeroCirrusSheet): the volume fades out as it fades in.
    float volume = 1 - AeroStrandy();
    if (volume <= 0) return 0;
    // Stretched skies line their cloud fields up in bands along the wind, drawn out less than the strands.
    float2 map = SAMPLE_TEXTURE2D_LOD(_AeroCloudCoverage, sampler_LinearRepeat, AeroAlongWind(field, sqrt(_AeroCloudForm.y)) * _AeroCloudShape2.z, coverageLod).rg;
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
    // Billows boil slowly and, in growing towers, rise through the column (updraft).
    float2 shapeXZ = AeroAlongWind(field, _AeroCloudForm.y);
    float3 shapePos = float3(shapeXZ.x, altitude + _AeroCloudWind.z - _AeroCloudWindDir.z, shapeXZ.y);
    shapePos.xz += _AeroCloudWindDir.xy * (h * _AeroCloudWind.w);
    float shape = SAMPLE_TEXTURE3D_LOD(_AeroCloudShapeNoise, sampler_LinearRepeat, shapePos * _AeroCloudShape.z, clamp(lod, 0, 7)).r; // 128^3: mip 7 is the tile average
    // Remap above the coverage threshold so every cloud reaches full density in its core, however low the
    // coverage. Softness lengthens the fringe; transparency comes from density, not from a weak core that
    // detail erosion would eat away.
    // Sculpt (convective towers): turbulence, not the weather map, outlines the cloud. At full local coverage the
    // map's outline was extruded up the whole kilometre-tall tower as a flat wall; a lower coverage that tapers
    // with height lets the 3D noise bulge the sides and round the tops.
    float sculpted = coverage * (1 - _AeroCloudForm.w * lerp(.25, .5, h));
    return volume * saturate((shape * profile - (1 - sculpted)) / max(sculpted * lerp(.6, 1.4, _AeroCloudShape2.w), .05));
}

// Cirrus hair, 0..1, for a point drifting with the clouds (veil fibres, below .5): the cirrus hair map, u along the
// wind, drawn out a little further by the look's stretch. `lod` is the map mip for the sample footprint.
float AeroCirrusStrands(float2 xz, float lod)
{
    float2 dir = _AeroCloudWindDir.xy;
    float2 p = float2(dot(xz, dir) / sqrt(_AeroCloudForm.y), dot(xz, float2(-dir.y, dir.x))) * _AeroCloudHeights.z;
    return SAMPLE_TEXTURE2D_LOD(_AeroCloudCirrus, sampler_AeroCloudCirrus, p, lod).r;
}

// Strand cirrus (fibres above .5): a thin sheet halfway up the layer, textured with the cirrus hair map.
// Review 2026-11-15 (third round): drawn as a 250 m deep volume, each strand stood as a wall that the view from below
// smeared into a broad band, and the weather map cut the bands off along smooth outlines: smeared paint and a ghost,
// not cloud. Real cirrus is fine hair kilometres up, thin next to its distance, so high-cloud renderers draw it as a
// textured sheet. rgb = scattered light, a = transmittance. `phases` are the march's three phase-function octaves.
float4 AeroCirrusSheet(float3 ray, float sceneDistance, float3 hazeColor, float3 phases)
{
    float strandy = AeroStrandy();
    float R = _AeroCloudLayer.z, h = _AeroCloudCamera.w;
    float altitude = _AeroCloudLayer.x + _AeroCloudLayer.w * .5;
    float2 hit = AeroSphere(h, ray.y, altitude);
    float t = h < altitude ? hit.y : hit.x;
    // Strand space (tiles, u along the wind) and its screen derivatives, in uniform control flow: they choose the
    // mip and the anisotropic footprint, which near the horizon is far longer along the view than across it.
    float2 dir = _AeroCloudWindDir.xy, across = float2(-dir.y, dir.x);
    float2 field = _AeroCloudCamera.xz + ray.xz * max(t, 0) + _AeroCloudWind.xy;
    float2 a = float2(dot(field, dir), dot(field, across)) * _AeroCloudHeights.z;
    // A second copy, larger, turned 11 degrees and drifting a little faster as the sky evolves, breaks up the tile's
    // repeats; the two slide past each other like cirrus at slightly different heights.
    float2 b = float2(a.x * .982 - a.y * .191, a.x * .191 + a.y * .982) * .73 + float2(.31 + _AeroCloudWind.z * _AeroCloudHeights.z * .5, .57);
    float2 adx = ddx(a), ady = ddy(a), bdx = ddx(b), bdy = ddy(b);
    // Looks without strand cirrus (every shipped preset since 2026-11-15) stop here, before any texture fetch.
    if (strandy <= 0 || t <= 0 || t >= min(sceneDistance, _AeroCloudHaze.w)) return float4(0, 0, 0, 1);
    if (h < altitude && AeroSphere(h, ray.y, 0).x > 0) return float4(0, 0, 0, 1); // the planet is in the way
    float hairA = SAMPLE_TEXTURE2D_GRAD(_AeroCloudCirrus, sampler_AeroCloudCirrus, a, adx, ady).r;
    float hairB = SAMPLE_TEXTURE2D_GRAD(_AeroCloudCirrus, sampler_AeroCloudCirrus, b, bdx, bdy).r;
    // The weather map spreads the wisps over the sky: each copy shows where the map clears the look's amount.
    // The threshold is soft over kilometres, so wisps thin out strand by strand instead of along an outline.
    float2 uv = field * _AeroCloudShape2.z;
    float open = 1 - saturate(_AeroCloudShape.x * 2);
    float gateA = smoothstep(open - .2, open + .2, SAMPLE_TEXTURE2D_LOD(_AeroCloudCoverage, sampler_LinearRepeat, uv + float2(.13, .71), 0).r);
    float gateB = smoothstep(open - .2, open + .2, SAMPLE_TEXTURE2D_LOD(_AeroCloudCoverage, sampler_LinearRepeat, uv * .79 + float2(.43, .17), 0).r);
    float hair = saturate(hairA * gateA + hairB * gateB * .8) * saturate(_AeroCloudShape.x * 4) * strandy;
    // Optical depth: the layer's density over a share of its thickness, longer on slanted paths (capped, or near
    // the horizon every wisp would merge into one white band).
    float3 p = float3(ray.x * t, R + h + ray.y * t, ray.z * t);
    float slant = max(abs(dot(ray, normalize(p))), .35);
    float column = hair * _AeroCloudShape.y * _AeroCloudLayer.w * .25;
    float opacity = 1 - exp(-column / slant);
    // Ice this thin barely shades itself: sunlight crosses half the sheet on its way in.
    float sunDepth = column * .5 / max(_AeroCloudSunDir.y, .1);
    float ms = _AeroCloudLighting.z, b1 = saturate(.35 + ms * .7), b2 = b1 * b1;
    float sunTerm = exp(-sunDepth) * phases.x + ms * exp(-sunDepth * b1) * phases.y + ms * ms * exp(-sunDepth * b2) * phases.z;
    float3 ambient = lerp(_AeroCloudAmbientBottom.rgb, _AeroCloudAmbientTop.rgb, .7) * lerp(1, .55, _AeroCloudLighting.w);
    float3 color = (sunTerm * 4 * PI * _AeroCloudSunColor.rgb * _AeroCloudTone.x + ambient * _AeroCloudTone.y) * opacity;
    // Aerial perspective, thicker than for the volume: the sheet stands in for cloud several times higher, whose
    // distant wisps sit behind far more air. At the volume's rate the strokes toward the horizon stayed as crisp as
    // overhead and filled it with a field of small white flecks.
    float haze = (1 - exp(-t * 2.5 / _AeroCloudHaze.x)) * _AeroCloudHaze.y;
    color = lerp(color, hazeColor * opacity, haze);
    float fade = 1 - smoothstep(_AeroCloudHaze.z, _AeroCloudHaze.w, t);
    return float4(color * fade, 1 - opacity * fade);
}

// `front` (premultiplied rgb, a transmittance) over `back`.
float4 AeroOver(float4 front, float4 back) { return float4(front.rgb + front.a * back.rgb, front.a * back.a); }

float AeroErode(float base, float3 world, float altitude, float h, float coverage, float lod, float cirrusLod)
{
    float fibres = _AeroCloudForm.x;
    float2 xz = AeroAlongWind(world.xz + _AeroCloudWind.xy * 1.6, _AeroCloudForm.y);
    float3 detailPos = float3(xz.x, altitude - _AeroCloudWind.z * 2 - _AeroCloudWindDir.z, xz.y);
    float noise = SAMPLE_TEXTURE3D_LOD(_AeroCloudDetailNoise, sampler_LinearRepeat, detailPos * _AeroCloudShape.w, lod).r;
    // The detail Worley is 1 at cell centres. Tops erode between cells (1 - detail) so round billows stay;
    // bases erode the centres for wispy, torn undersides. The reverse leaves only the cell borders: a net of lines.
    // The torn underside reaches a share of the cloud (raggedBase, a third by default). A third of a 1.1 km tower
    // tore its lower 370 m into streaky fragments, so towers keep it small and billow almost to the base.
    float detail = lerp(noise, 1 - noise, saturate(h / _AeroCloudForm.z));
    float erode = detail * _AeroCloudShape2.x * .85 * lerp(1, .65, coverage);
    float density = saturate((base - erode) / max(1 - erode, .05));
    if (fibres <= 0) return density;
    // Cirrus: keep only the strands (sheared downwind with height like the cloud), with blue sky between them.
    float strands = AeroCirrusStrands(world.xz + _AeroCloudWind.xy + _AeroCloudWindDir.xy * (h * _AeroCloudWind.w), cirrusLod);
    return lerp(density, base * strands, fibres);
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

// Optical depth of the cloud straight above a sample. Sky light comes mostly from above, so the mass overhead
// decides how much reaches a sample: deep bellies turn blue-grey while thin edges and tops stay bright.
float AeroSkyDepth(float3 relative, float lod)
{
    float reach = clamp(_AeroCloudLayer.w * .5, 60, 400);
    float depth = 0, previous = 0;
    [unroll] for (int i = 1; i <= 3; i++)
    {
        float t = reach * i * i / 9.0;
        float3 p = relative + float3(0, (t + previous) * .5, 0);
        float h, coverage;
        depth += AeroBaseDensity(p + float3(_AeroCloudCamera.x, 0, _AeroCloudCamera.z), AeroAltitude(p), lod + 1, h, coverage) * (t - previous);
        previous = t;
    }
    return depth * _AeroCloudShape.y;
}

// Per-pixel ray start offset in [0, 1). Each pixel gets an independent hashed phase, and a golden-ratio step per
// frame walks it through all phases for the temporal pass to average. Interleaved gradient noise was used before,
// but it repeats every ~3.2 rows and the per-frame step shifts every pixel alike, so the temporal blend kept a
// row-dependent remainder: a comb of horizontal teeth on thin cloud fringes. Independent phases leave fine grain.
float AeroPixelPhase(float2 pixel)
{
    uint2 q = (uint2)(int2)floor(pixel) * uint2(1597334673u, 3812015801u);
    uint n = (q.x ^ q.y) * 1597334673u;
    return frac(n * (1.0 / 4294967296.0) + _AeroCloudMarch.z * .61803398875);
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
    // The haze takes on the sun's glow in its direction, as the sky behind it does (cloud-shaded like the ground haze).
    float3 hazeColor = _AeroCloudFogColor.rgb + AeroSunGlowRadiance(ray, false) * _AeroSunHaze.w;
    float3 sunDir = _AeroCloudSunDir.xyz;
    float cosAngle = dot(ray, sunDir);
    float g = _AeroCloudLighting.y, ms = _AeroCloudLighting.z;
    float phase0 = lerp(AeroHG(cosAngle, g), AeroHG(cosAngle, -.25), .3);
    float phase1 = lerp(AeroHG(cosAngle, g * ms), AeroHG(cosAngle, -.25 * ms), .3);
    float phase2 = lerp(AeroHG(cosAngle, g * ms * ms), AeroHG(cosAngle, -.25 * ms * ms), .3);
    // Strand cirrus lies behind whatever volume the look keeps (they share the layer; only blends have both).
    float4 sheet = AeroCirrusSheet(ray, sceneDistance, hazeColor, float3(phase0, phase1, phase2));
    float4 empty = AeroOver(sheet, float4(hazeColor * wall, 1 - wall));
    if (range.x < 0 || range.x >= sceneDistance || AeroStrandy() >= 1) return empty;
    range.y = min(range.y, sceneDistance);

    int steps = (int)_AeroCloudMarch.x;
    float stepSize = clamp((range.y - range.x) / steps, 25, _AeroCloudMarch.w);
    float powderFacing = smoothstep(.5, -.5, cosAngle);
    float transmittance = 1;
    float3 sun = 0, ambient = 0;
    float meanDistance = 0, meanWeight = 0;
    // Inside a cloud (the top tower floors) steps start short and grow with distance, so the fog around the
    // camera is sampled finely; from outside the layer, range.x is far and every step is stepSize.
    // Empty air is crossed in double steps, so the start offset spans a double step: offset over a single step
    // left half the lattice positions unvisited on every frame, and thin fringes between them were missed on some
    // rows and always hit on others (a comb of horizontal teeth that the temporal pass converged to).
    float t = range.x + min(stepSize, max(8, range.x * .25)) * 2 * AeroPixelPhase(pixel);
    float start = t;
    int fine = 0; // samples left at single steps since the last cloud
    [loop] for (int i = 0; i < steps && t < range.y; i++)
    {
        float step = min(stepSize, max(8, t * .25));
        // Long steps (toward the horizon) read coarser mips so distant clouds do not sparkle.
        float shapeLod = AeroFootprintLod(step); // unclamped: the weather map needs the full footprint
        // Down to the last mip (the tile average): capped one level short, the detail tile (~110 m on small
        // presets) still repeated along grazing rays and drew streaks fanning out from the vanishing point.
        float detailLod = clamp(log2(step * _AeroCloudShape.w * 32) - 1, 0, 5);
        float3 relative = ray * t;
        float3 world = relative + float3(_AeroCloudCamera.x, 0, _AeroCloudCamera.z);
        float altitude = AeroAltitude(relative);
        float h, coverage;
        float base = AeroBaseDensity(world, altitude, shapeLod, h, coverage);
        // Empty air: double steps. Every move is a whole number of steps so all pixels keep the same jittered
        // sample lattice; a fractional skip shifted it by a different amount per pixel and drew contour lines.
        if (base <= 0) { if (fine > 0) { fine--; t += step; } else t += step * 2; continue; }
        // Entering a cloud after a double step: back up one step so its edge is not cut, then march finely.
        if (fine == 0 && t > start + step * .5) { fine = 4; t -= step; continue; }
        fine = 4;
        // Cirrus map mip for this footprint (half-texel bias, like the shape noise).
        float cirrusLod = max(log2(max(step * _AeroCloudHeights.w * _AeroCloudHeights.z, 1e-4)) - 1, 0);
        float density = AeroErode(base, world, altitude, h, coverage, detailLod, cirrusLod);
        // Thin the cloud right around the camera so a player building inside one still sees nearby blocks.
        density *= smoothstep(0, 45, t);
        if (density > .001)
        {
            float extinction = density * _AeroCloudShape.y;
            float stepTransmittance = exp(-extinction * step);
            float weight = transmittance * (1 - stepTransmittance);
            float lightDepth = AeroLightDepth(relative, sunDir, h, shapeLod);
            // Multiple-scattering octaves (Wrenninge). Extinction falls off faster than the octave energy, so
            // light still reaches deep inside, but thick cores sit clearly darker than thin, sunlit fringes.
            float b1 = saturate(.35 + ms * .7), b2 = b1 * b1;
            float sunTerm = exp(-lightDepth) * phase0 + ms * exp(-lightDepth * b1) * phase1 + ms * ms * exp(-lightDepth * b2) * phase2;
            float powder = lerp(1, saturate((1 - exp(-extinction * 60)) * 2), _AeroCloudLighting.x * powderFacing);
            sun += sunTerm * powder * 4 * PI * weight;
            float skyVisibility = exp(-AeroSkyDepth(relative, shapeLod) * .6);
            float ambientOcclusion = lerp(.3, 1, skyVisibility) * lerp(.7, 1, saturate(h * 1.4)) * lerp(1, .55, _AeroCloudLighting.w);
            ambient += lerp(_AeroCloudAmbientBottom.rgb, _AeroCloudAmbientTop.rgb, saturate(h * 1.2)) * ambientOcclusion * weight;
            meanDistance += t * weight; meanWeight += weight;
            transmittance *= stepTransmittance;
            if (transmittance < .01) { transmittance = 0; break; }
        }
        t += step;
    }
    if (meanWeight <= 0) return empty;
    meanDistance /= meanWeight;
    float3 color = sun * _AeroCloudSunColor.rgb * _AeroCloudTone.x + ambient * _AeroCloudTone.y;
    // Aerial perspective: distant clouds melt into the horizon haze, then fade out.
    float haze = (1 - exp(-meanDistance / _AeroCloudHaze.x)) * _AeroCloudHaze.y;
    color = lerp(color, hazeColor * (1 - transmittance), haze);
    float fade = 1 - smoothstep(_AeroCloudHaze.z, _AeroCloudHaze.w, meanDistance);
    return AeroOver(float4(lerp(hazeColor * wall, color, fade), lerp(1 - wall, transmittance, fade)), sheet);
}

#endif
