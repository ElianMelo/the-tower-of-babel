using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
// Unity's Is extends NUnit's with allocation constraints.
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>
    /// The soft sun (Documentation/FrutigerAero/Lighting/SUN_GLOW.md): a bright inner circle that smooths out into a
    /// warm glow and a wide halo, driven by the day/night cycle, drawn by the sky and echoed by the distant haze.
    /// </summary>
    public sealed class AeroSunGlowEditModeTests
    {
        const string ProfilePath = "Assets/_Project/Environment/Lighting/AeroDayNight.asset";
        const string SkyPath = "Assets/_Project/Environment/Clouds/AeroClearSky.mat";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string SkyShader = "Tower of Babel/Sky/Sun Glow Sky";
        const string HazeShader = "Hidden/TowerOfBabel/Sun Haze";
        const string RaysShader = "Hidden/TowerOfBabel/Sun Rays";
        const string CloudShaderPath = "Assets/_Project/Shaders/AeroVolumetricClouds.shader";
        static readonly Color DaySun = new(1, .97f, .88f);

        static Vector3 Toward(float elevation, float azimuth = 30) => Quaternion.Euler(-elevation, azimuth, 0) * Vector3.forward;

        static AeroSunGlowState Sun(float elevation, float overcast = 0, float haze = 1, Color? light = null, AeroSunGlowSettings settings = null)
            => AeroSunGlow.Evaluate(settings ?? AeroSunGlowSettings.Sun(), Toward(elevation), light ?? DaySun, 1, overcast, haze, 0);

        static float Luminance(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;

        /// <summary>A view <paramref name="angle"/> degrees above the light, along the same azimuth.</summary>
        static Vector3 Above(in AeroSunGlowState state, float angle)
        {
            var axis = Vector3.Cross(state.direction, Vector3.up).normalized;
            return Quaternion.AngleAxis(-angle, axis) * state.direction;
        }

        static float Profile(in AeroSunGlowState state, float angle) => Luminance(AeroSunGlow.Radiance(state, Above(state, angle)));

        static float HalfBrightnessRadius(in AeroSunGlowState state)
        {
            float centre = Profile(state, 0);
            for (float a = 0; a < 90; a += .02f) if (Profile(state, a) < centre * .5f) return a;
            return 90;
        }

        sealed class GlobalsScope : IDisposable
        {
            readonly AeroSunGlowState saved = AeroSunGlow.Current;
            public void Dispose() => AeroSunGlow.Push(saved);
        }

        // ---- Model -------------------------------------------------------------------------------------------------

        [Test]
        public void Radiance_IsBrightestAtTheCentreAndSmoothsOutWithNoHardEdge()
        {
            foreach (float elevation in new[] { 4f, 20f, 60f })
            {
                var state = Sun(elevation);
                float centre = Profile(state, 0), previous = centre, biggestStep = 0;
                for (float a = .05f; a <= 90 - elevation; a += .05f)
                {
                    float value = Profile(state, a);
                    // acos near the exact centre carries ~0.02° of float error, hence the tiny relative tolerance.
                    Assert.That(value, Is.LessThanOrEqualTo(previous + centre * 2e-4f), $"Falls off steadily ({elevation}° sun, {a:0.00}°).");
                    biggestStep = Mathf.Max(biggestStep, previous - value);
                    previous = value;
                }
                Assert.That(biggestStep, Is.LessThan(centre * .1f), $"The inner circle's edge is soft, not a disc ({elevation}° sun).");
                float plateau = state.shape.x * (1 - state.shape.y);
                Assert.That(Profile(state, plateau * .98f), Is.GreaterThanOrEqualTo(centre * .9f), "The inner circle stays bright to its soft edge.");
                Assert.That(centre, Is.GreaterThan(2), "The inner circle is HDR, above the bloom threshold, so it blooms.");
                Assert.That(Profile(state, state.shape.x * (1 + state.shape.y) * 3), Is.InRange(.02f, centre * .35f), "Past the circle the light is a soft glow.");
                Assert.That(state.strength.x, Is.LessThan(state.strength.y * 4), "The circle's peak stays close to its glow, so it blends into the sky.");
            }
        }

        [Test]
        public void LowSun_HasAWiderBrighterAndWarmerHaloThanHighSun()
        {
            var low = Sun(4); var high = Sun(70);
            Assert.That(low.lowSun, Is.GreaterThan(.95f)); Assert.That(high.lowSun, Is.LessThan(.05f));
            Assert.That(HalfBrightnessRadius(low), Is.GreaterThan(HalfBrightnessRadius(high)));
            Assert.That(Profile(low, 20), Is.GreaterThan(Profile(high, 20) * 2), "The halo spreads much further at low sun.");
            Color lowHalo = AeroSunGlow.Radiance(low, Above(low, 15)), highHalo = AeroSunGlow.Radiance(high, Above(high, 15));
            Assert.That(lowHalo.b / lowHalo.r, Is.LessThan(highHalo.b / highHalo.r * .8f), "The low-sun halo is golden.");
            Assert.That(low.veil, Is.GreaterThan(high.veil * 3), "Near a low sun the blue sky gives way to the glow.");
            Assert.That(AeroSunGlow.SkyKeep(low, low.direction), Is.LessThan(.5f));
            Assert.That(AeroSunGlow.SkyKeep(low, -low.direction), Is.GreaterThan(.99f), "Away from the sun the sky is untouched.");
            Assert.That(low.bloomScatter, Is.GreaterThan(high.bloomScatter));
            Assert.That(low.haze.y, Is.GreaterThan(high.haze.y), "Distant ground melts into a low sun's glow more.");
            Assert.That(HalfBrightnessRadius(low), Is.InRange(1.2f, 6), "A low sun is a soft circle: not a point, not a wall of light.");
            Assert.That(low.rays.x, Is.GreaterThan(high.rays.x), "Light shafts are strongest towards a low sun.");

            // The sun's colour spreads across the skybox: tinted most on its side, still a little opposite, none at noon.
            Assert.That(AeroSunGlow.SkyTint(low, low.direction), Is.GreaterThan(.25f));
            Assert.That(AeroSunGlow.SkyTint(low, -low.direction), Is.InRange(.05f, AeroSunGlow.SkyTint(low, low.direction) * .5f));
            Assert.That(AeroSunGlow.SkyTint(high, high.direction), Is.LessThan(.05f));
            Assert.That(Profile(low, 60), Is.GreaterThan(Profile(high, 60) * 3), "A low sun's colour washes far across the sky.");

            // The 16:30 golden-hour preset has the sun about 22 degrees up: it must still read as a low sun.
            Assert.That(Sun(22.5f).lowSun, Is.GreaterThan(.5f));
        }

        [Test]
        public void Overcast_HidesTheInnerCircleButKeepsADiffuseGlow()
        {
            var clear = Sun(15); var overcast = Sun(15, overcast: 1);
            Assert.That(overcast.strength.x, Is.Zero);
            float centre = Profile(overcast, 0);
            Assert.That(centre, Is.GreaterThan(.1f)); Assert.That(centre, Is.LessThan(Profile(clear, 0) * .45f));
            Assert.That(overcast.shape.z, Is.GreaterThan(clear.shape.z), "The glow spreads under cloud.");
            Assert.That(overcast.rays.x, Is.LessThan(clear.rays.x), "Rain clouds weaken the light shafts.");
        }

        [Test]
        public void PresetHaze_WidensAndStrengthensTheHalo()
        {
            var clean = Sun(15, haze: 1); var misty = Sun(15, haze: 1.4f);
            Assert.That(misty.shape.w, Is.GreaterThan(clean.shape.w));
            Assert.That(Profile(misty, 20), Is.GreaterThan(Profile(clean, 20)));
        }

        [Test]
        public void Moon_GlowIsSmallerAndFaintAndNothingShowsWhenHidden()
        {
            var sun = Sun(30);
            var moon = AeroSunGlow.Evaluate(AeroSunGlowSettings.Moon(), Toward(30), new Color(.4f, .63f, 1), 1, 0, 1, 0);
            Assert.That(HalfBrightnessRadius(moon), Is.LessThan(HalfBrightnessRadius(sun)));
            Assert.That(Profile(moon, 10), Is.LessThan(Profile(sun, 10) * .5f));
            Assert.That(moon.haze.y, Is.Zero, "No golden haze over the ground at night.");
            var hidden = AeroSunGlow.Evaluate(AeroSunGlowSettings.Sun(), Toward(30), DaySun, 0, 0, 1, 0);
            Assert.That(Luminance(AeroSunGlow.Radiance(hidden, hidden.direction)), Is.Zero);
            Assert.That(hidden.HazeVisible, Is.False);
        }

        [Test]
        public void ProfileAsset_HoldsTheTunedSunAndMoonGlow()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroDayNightProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null);
            var sun = profile.sunGlow; var moon = profile.moonGlow;
            Assert.That(sun, Is.Not.Null); Assert.That(moon, Is.Not.Null);
            Assert.That(sun.lowSun.haloRadius, Is.GreaterThan(sun.highSun.haloRadius));
            Assert.That(sun.lowSun.coreRadius, Is.GreaterThan(sun.highSun.coreRadius));
            Assert.That(sun.lowSun.coreIntensity, Is.GreaterThan(2), "The inner circle blooms.");
            Assert.That(sun.lowSun.haloIntensity, Is.LessThan(.95f), "The halo stays below the bloom threshold, so it stays soft.");
            Assert.That(sun.lowSun.skyVeil, Is.GreaterThan(sun.highSun.skyVeil));
            Assert.That(sun.lowSunBloomScatter, Is.InRange(sun.highSunBloomScatter, 1f));
            Assert.That(sun.hazeDensity, Is.InRange(.0005f, .01f));
            Assert.That(sun.lowSun.skySpread, Is.GreaterThan(sun.highSun.skySpread));
            Assert.That(sun.lowSun.skyTint, Is.GreaterThan(sun.highSun.skyTint), "The golden-hour sun colours the whole sky; noon stays clean.");
            Assert.That(sun.lowSun.coreSoftness, Is.GreaterThanOrEqualTo(.7f), "The inner circle melts into the sky.");
            Assert.That(sun.lowSunFocused.coreSoftness, Is.GreaterThanOrEqualTo(.7f));
            Assert.That(sun.lowSun.rays, Is.GreaterThan(sun.highSun.rays));
            Assert.That(sun.lowSunFocused.haloRadius, Is.GreaterThan(sun.lowSun.haloRadius), "Looked at straight on, the low sun is bigger than at the edge.");
            Assert.That(sun.lowSunFocused.glowIntensity, Is.GreaterThan(sun.lowSun.glowIntensity));
            Assert.That(sun.shaftWidth, Is.GreaterThanOrEqualTo(6), "Outdoor shafts are broad beams.");
            Assert.That(moon.lowSun.haloRadius, Is.LessThan(sun.lowSun.haloRadius));
            Assert.That(moon.lowSunHaze + moon.highSunHaze, Is.Zero);
        }

        // ---- Day/night cycle -----------------------------------------------------------------------------------------

        [Test]
        public void Cycle_PushesAGoldenGlowInTheAfternoonFollowsTheMoonAndFadesWithClouds()
        {
            WithCycle((cycle, sun, moon) =>
            {
                cycle.ApplyTime(.5f);
                var noon = cycle.SunGlow;
                Assert.That((Vector4)Shader.GetGlobalVector(AeroSunGlow.ShapeId), Is.EqualTo(noon.shape));
                Assert.That(Vector3.Dot(noon.direction, -sun.transform.forward), Is.GreaterThan(.999f));

                cycle.ApplyTime(.6875f);
                var afternoon = cycle.SunGlow;
                Assert.That(afternoon.lowSun, Is.GreaterThan(.4f));
                Assert.That(afternoon.shape.w, Is.GreaterThan(noon.shape.w), "A wider halo at golden hour.");
                Assert.That(afternoon.strength.z, Is.GreaterThan(noon.strength.z));
                Assert.That((Vector4)Shader.GetGlobalVector(AeroSunGlow.StrengthId), Is.EqualTo(afternoon.strength));
                Assert.That((Vector4)Shader.GetGlobalVector(AeroSunGlow.SpreadId), Is.EqualTo(afternoon.spread));
                Assert.That(afternoon.spread.z, Is.GreaterThan(noon.spread.z), "The golden-hour sun tints the whole sky.");
                Assert.That(afternoon.rays.x, Is.GreaterThan(noon.rays.x));
                Assert.That(cycle.BloomScatterAt(.6875f), Is.GreaterThan(cycle.BloomScatterAt(.5f)));
                Assert.That(cycle.BloomScatterAt(.5f), Is.EqualTo(AeroAtmosphereDirector.DefaultBloomScatter).Within(.01f), "Midday keeps the accepted bloom.");

                cycle.ApplyTime(.02f);
                Assert.That(RenderSettings.sun, Is.SameAs(moon));
                Assert.That(Vector3.Dot(cycle.SunGlow.direction, -moon.transform.forward), Is.GreaterThan(.999f), "At night the glow follows the moon.");
                Assert.That(cycle.SunGlow.strength.x, Is.LessThan(afternoon.strength.x));

                // Through dusk the sun's glow fades out and the moon's fades in without a jump (a step is 0.12 s of a
                // 20-minute day).
                var energies = new System.Collections.Generic.List<float>();
                for (float t = .70f; t <= .85f; t += .0001f)
                {
                    cycle.ApplyTime(t);
                    var g = cycle.SunGlow;
                    energies.Add((g.color.r + g.color.g + g.color.b) * (g.strength.y + g.strength.z));
                }
                float peak = energies.Max();
                for (int i = 1; i < energies.Count; i++)
                    Assert.That(Mathf.Abs(energies[i] - energies[i - 1]), Is.LessThan(peak * .1f), $"No pop at t={.70f + i * .0001f:0.0000}.");

                cycle.overcast = 1; cycle.ApplyTime(.6875f);
                Assert.That(cycle.SunGlow.strength.x, Is.Zero, "Rain clouds hide the inner circle.");
                Assert.That(sun.GetComponent<LensFlareComponentSRP>().intensity, Is.Zero, "The flare is drawn over clouds, so it fades with them.");
                cycle.overcast = 0; cycle.cloudShade = 1; cycle.ApplyTime(.6875f);
                Assert.That(cycle.SunGlow.haze.w, Is.Zero, "Clouds over the sun fade the ground haze.");
                Assert.That(cycle.SunGlow.HazeVisible, Is.False);
                cycle.cloudShade = 0; cycle.ApplyTime(.6875f);
                Assert.That(sun.GetComponent<LensFlareComponentSRP>().intensity, Is.GreaterThan(0));

                float time = .6f;
                cycle.ApplyTime(time); cycle.ApplyTime(time + .001f);
                Assert.That(() => { time += .001f; cycle.ApplyTime(time); }, Is.Not.AllocatingGCMemory(), "The glow is pushed every frame without garbage.");
            });
        }

        [Test]
        public void Director_WidensBloomScatterAtLowSun()
        {
            WithCycle((cycle, sun, moon) =>
            {
                var go = new GameObject("Bloom scatter director") { hideFlags = HideFlags.HideAndDontSave };
                var library = ScriptableObject.CreateInstance<AeroAtmospherePresetLibrary>();
                var director = go.AddComponent<AeroAtmosphereDirector>();
                try
                {
                    director.library = library; director.Store = new MemoryAeroSettingsStore(); director.dayNight = cycle;
                    cycle.timeOfDay = .5f; director.Tick(0);
                    Assert.That(director.BloomScatter, Is.EqualTo(AeroAtmosphereDirector.DefaultBloomScatter).Within(.01f));
                    cycle.timeOfDay = .74f; director.Tick(0);
                    Assert.That(director.BloomScatter, Is.GreaterThan(.76f), "The low sun's glow spills further over silhouettes.");
                    Assert.That(cycle.SunGlow.haze.y, Is.GreaterThan(0), "The director's grade reaches the glow (preset haze).");
                }
                finally { director.Shutdown(); Object.DestroyImmediate(go); Object.DestroyImmediate(library); }
            });
        }

        static void WithCycle(Action<AeroDayNightCycle, Light, Light> check)
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroDayNightProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null);
            var go = new GameObject("Sun glow cycle test") { hideFlags = HideFlags.HideAndDontSave };
            var sunObject = new GameObject("Sun glow test sun") { hideFlags = HideFlags.HideAndDontSave };
            var moonObject = new GameObject("Sun glow test moon") { hideFlags = HideFlags.HideAndDontSave };
            var saved = (RenderSettings.fog, RenderSettings.fogColor, RenderSettings.fogDensity, RenderSettings.fogMode, RenderSettings.sun,
                RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
                RenderSettings.ambientProbe, RenderSettings.reflectionIntensity, Shader.GetGlobalFloat("_AeroDaylight"));
            using var globals = new GlobalsScope();
            try
            {
                var sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional;
                sunObject.AddComponent<LensFlareComponentSRP>();
                var moon = moonObject.AddComponent<Light>(); moon.type = LightType.Directional;
                var cycle = go.AddComponent<AeroDayNightCycle>(); cycle.profile = profile; cycle.sun = sun; cycle.moon = moon;
                check(cycle, sun, moon);
            }
            finally
            {
                Object.DestroyImmediate(go); Object.DestroyImmediate(sunObject); Object.DestroyImmediate(moonObject);
                (RenderSettings.fog, RenderSettings.fogColor, RenderSettings.fogDensity, RenderSettings.fogMode, RenderSettings.sun,
                    RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
                    RenderSettings.ambientProbe, RenderSettings.reflectionIntensity) =
                    (saved.Item1, saved.Item2, saved.Item3, saved.Item4, saved.Item5, saved.Item6, saved.Item7, saved.Item8, saved.Item9, saved.Item10, saved.Item11);
                Shader.SetGlobalFloat("_AeroDaylight", saved.Item12);
            }
        }

        // ---- Shaders and assets ----------------------------------------------------------------------------------------

        [Test]
        public void Shaders_CompileWithoutErrors()
        {
            foreach (var shader in new[] { Shader.Find(SkyShader), Shader.Find(HazeShader), Shader.Find(RaysShader), AssetDatabase.LoadAssetAtPath<Shader>(CloudShaderPath) })
            {
                Assert.That(shader, Is.Not.Null);
                bool oldAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
                var material = new Material(shader);
                try
                {
                    for (int pass = 0; pass < material.passCount; pass++) ShaderUtil.CompilePass(material, pass, true);
                    var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error);
                    Assert.That(errors.Select(e => e.file + ":" + e.line + " " + e.message), Is.Empty, shader.name);
                    Assert.That(shader.isSupported, Is.True, shader.name);
                }
                finally { Object.DestroyImmediate(material); ShaderUtil.allowAsyncCompilation = oldAsync; }
            }
        }

        [Test]
        public void SkyMaterialAndRenderer_UseTheSoftSun()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            Assert.That(sky.shader.name, Is.EqualTo(SkyShader), "The clear sky draws the soft sun, not the Procedural point sun.");
            var tint = sky.GetColor("_SkyTint");
            Assert.That(new[] { tint.r, tint.g, tint.b }, Is.EqualTo(new[] { .38f, .56f, .86f }).Within(1e-3f), "The accepted Frutiger blue is kept.");
            Assert.That(sky.GetFloat("_AtmosphereThickness"), Is.EqualTo(.78f).Within(1e-4f));
            Assert.That(sky.GetFloat("_Exposure"), Is.EqualTo(1.15f).Within(1e-4f));
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var haze = renderer.rendererFeatures.OfType<AeroSunHazeRendererFeature>().SingleOrDefault();
            Assert.That(haze, Is.Not.Null); Assert.That(haze.isActive, Is.True);
            Assert.That(haze.hazeShader, Is.SameAs(Shader.Find(HazeShader)));
            var shafts = renderer.rendererFeatures.OfType<AeroSunShaftRendererFeature>().Single();
            Assert.That(AssetDatabase.GetAssetPath(shafts.raysShader), Is.EqualTo(AssetDatabase.GetAssetPath(Shader.Find(RaysShader))),
                "Outdoor light shafts ride on the sun shaft feature.");
        }

        [Test]
        public void LightShafts_AreStrongestFacingTheSunAndOffBehindIt()
        {
            var go = new GameObject("Light shaft camera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = go.AddComponent<Camera>(); camera.enabled = false;
                var glow = Sun(15);
                camera.transform.rotation = Quaternion.LookRotation(glow.direction);
                float facing = AeroSunShaftRendererFeature.OutdoorStrength(camera, glow);
                // Inside the tower the interior shafts carry most of the light, so the open scene may lower this.
                Assert.That(facing, Is.InRange(glow.rays.x * .1f, glow.rays.x + 1e-3f));
                camera.transform.rotation = Quaternion.LookRotation(Quaternion.Euler(0, 90, 0) * new Vector3(glow.direction.x, 0, glow.direction.z));
                float side = AeroSunShaftRendererFeature.OutdoorStrength(camera, glow);
                Assert.That(side, Is.LessThanOrEqualTo(facing));
                camera.transform.rotation = Quaternion.LookRotation(-glow.direction);
                Assert.That(AeroSunShaftRendererFeature.OutdoorStrength(camera, glow), Is.Zero, "No shafts drawn with the sun behind the camera.");
                var night = AeroSunGlow.Evaluate(AeroSunGlowSettings.Sun(), glow.direction, DaySun, 0, 0, 1, 0);
                camera.transform.rotation = Quaternion.LookRotation(glow.direction);
                Assert.That(AeroSunShaftRendererFeature.OutdoorStrength(camera, night), Is.Zero, "No shafts from a hidden sun.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Focus_BlendsTheFullSunAtTheCentreIntoTheCalmSunAtTheEdge()
        {
            var settings = AeroSunGlowSettings.Sun();
            var edge = AeroSunGlow.Evaluate(settings, Toward(10), DaySun, 1, 0, 1, 0, 0);
            var focused = AeroSunGlow.Evaluate(settings, Toward(10), DaySun, 1, 0, 1, 0, 1);
            Assert.That(HalfBrightnessRadius(focused), Is.GreaterThan(HalfBrightnessRadius(edge) * 1.3f), "Looked at straight on, the sun is bigger.");
            Assert.That(Profile(focused, 15), Is.GreaterThan(Profile(edge, 15) * 1.5f), "Its glow reaches further.");
            Assert.That(focused.spread.z, Is.GreaterThan(edge.spread.z), "It warms the sky more.");
            var half = AeroSunGlowState.Lerp(edge, focused, .5f);
            Assert.That(half.shape.w, Is.InRange(Mathf.Min(edge.shape.w, focused.shape.w), Mathf.Max(edge.shape.w, focused.shape.w)));
            Assert.That(Profile(half, 15), Is.InRange(Profile(edge, 15), Profile(focused, 15)), "Between the centre and the edge the looks blend.");

            var go = new GameObject("Focus camera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.fieldOfView = 60; camera.aspect = 16f / 9;
                var flat = new Vector3(focused.direction.x, 0, focused.direction.z).normalized;
                camera.transform.rotation = Quaternion.LookRotation(focused.direction);
                Assert.That(AeroSunGlow.ScreenFocus(camera, focused), Is.GreaterThan(.99f), "Sun at the centre of the screen: the full look.");
                camera.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(20, Vector3.up) * flat);
                float between = AeroSunGlow.ScreenFocus(camera, focused);
                Assert.That(between, Is.InRange(.2f, .95f), "Part way to the edge: a blend.");
                camera.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(50, Vector3.up) * flat);
                Assert.That(AeroSunGlow.ScreenFocus(camera, focused), Is.LessThan(.02f), "Sun at the screen's edge: the calm look.");
                camera.transform.rotation = Quaternion.LookRotation(-focused.direction);
                Assert.That(AeroSunGlow.ScreenFocus(camera, focused), Is.Zero, "Sun behind the camera: the calm look.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- GPU --------------------------------------------------------------------------------------------------------

        [Test]
        public void Gpu_SkyDrawsTheModelsSoftCircleAndLeavesTheSkyAwayFromTheSun()
        {
            using var globals = new GlobalsScope();
            using var h = new AeroRenderHarness(128);
            var sky = h.Own(new Material(Shader.Find(SkyShader)));
            sky.SetColor("_SkyTint", new Color(.38f, .56f, .86f)); sky.SetColor("_GroundColor", new Color(.33f, .42f, .52f));
            sky.SetFloat("_AtmosphereThickness", .78f); sky.SetFloat("_Exposure", 1.15f);
            h.Spawn(PrimitiveType.Sphere, sky, Vector3.zero, Vector3.one * 200);
            h.Camera.nearClipPlane = .1f; h.Camera.farClipPlane = 1000; h.Camera.fieldOfView = 30;

            var state = Sun(20);
            state.haze.y = 0; // the sky alone, no haze pass
            // Additive light only (no veil, no whole-sky tint), so lit minus plain is exactly the model's radiance.
            var noVeil = state; noVeil.veil = 0; noVeil.spread.z = 0;
            var off = noVeil; off.strength = Vector4.zero; off.spread.x = 0;
            h.Camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(state.direction));
            AeroSunGlow.Push(off); var plain = h.Render();
            AeroSunGlow.Push(noVeil); var lit = h.Render();
            AeroSunGlow.Push(state); var veiled = h.Render();

            int c = h.Size / 2;
            Assert.That(Luminance(h.Pixel(lit, c, c)), Is.GreaterThan(2), "The centre is HDR and blooms.");
            float previous = float.MaxValue;
            for (int x = c; x < h.Size; x++)
            {
                var view = h.Camera.ViewportPointToRay(new Vector3((x + .5f) / h.Size, (c + .5f) / h.Size, 0)).direction;
                float measured = Luminance(h.Pixel(lit, x, c)) - Luminance(h.Pixel(plain, x, c));
                float expected = Luminance(AeroSunGlow.Radiance(state, view));
                Assert.That(measured, Is.EqualTo(expected).Within(expected * .06f + .02f), $"Matches the C# model at pixel {x}.");
                Assert.That(measured, Is.LessThanOrEqualTo(previous + .02f), "Smooths out from the centre.");
                previous = measured;
            }
            var centre = h.Pixel(veiled, c + 30, c); var unveiled = h.Pixel(lit, c + 30, c);
            Assert.That(centre.b / centre.r, Is.LessThan(unveiled.b / unveiled.r), "Near a low sun the blue gives way to the glow.");

            h.Camera.transform.rotation = Quaternion.LookRotation(-state.direction);
            AeroSunGlow.Push(off); var awayPlain = h.Render();
            AeroSunGlow.Push(state); var away = h.Render();
            for (int i = 0; i < away.Length; i += 97)
                Assert.That(Luminance(away[i]), Is.EqualTo(Luminance(awayPlain[i])).Within(.01f), "Away from the sun the sky keeps its brightness.");
            var farSide = h.Pixel(away, c, c); var farPlain = h.Pixel(awayPlain, c, c);
            Assert.That(farSide.r / farSide.b, Is.GreaterThan(farPlain.r / farPlain.b * 1.03f), "The sun's colour spreads even to the far side of the sky.");
        }

        [Test]
        public void Gpu_LightShaftsAreSeparateBeamsTowardsTheSun()
        {
            var shafts = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath).rendererFeatures.OfType<AeroSunShaftRendererFeature>().Single();
            using var globals = new GlobalsScope();
            using var h = new AeroRenderHarness(96);
            var oldSky = RenderSettings.skybox; float oldClock = shafts.previewTime;
            try
            {
                var sky = h.Own(new Material(Shader.Find(SkyShader)));
                sky.SetColor("_SkyTint", new Color(.38f, .56f, .86f)); sky.SetFloat("_AtmosphereThickness", .78f); sky.SetFloat("_Exposure", 1.15f);
                RenderSettings.skybox = sky; // the test runner's scene may have Unity's default sky
                h.Camera.clearFlags = CameraClearFlags.Skybox; // an outdoor view
                h.Camera.nearClipPlane = .3f; h.Camera.farClipPlane = 5000; h.Camera.fieldOfView = 60;
                shafts.previewTime = 0; shafts.isolateRays = true;
                var state = Sun(12); state.haze.y = 0;
                AeroSunGlow.Push(state);
                float[] Look(Vector3 forward)
                {
                    h.Camera.transform.SetPositionAndRotation(new Vector3(0, 2, 0), Quaternion.LookRotation(forward));
                    return h.Render().Select(Luminance).ToArray();
                }
                var towards = Look(state.direction);
                var flat = new Vector3(state.direction.x, 0, state.direction.z).normalized;
                var aside = Look(Quaternion.AngleAxis(100, Vector3.up) * flat);
                float meanTowards = towards.Average(), meanAside = aside.Average(), peak = towards.Max();
                Assert.That(meanTowards, Is.GreaterThan(.01f), "Light shafts show in the air towards the sun.");
                Assert.That(meanTowards, Is.GreaterThan(meanAside * 3), "They scatter forwards: faint seen from the side.");
                var sorted = towards.OrderBy(x => x).ToArray();
                float p20 = sorted[sorted.Length / 5], p80 = sorted[sorted.Length * 4 / 5];
                Assert.That(p80, Is.GreaterThan(p20 * 1.4f + .002f), "Separate beams with gaps, not one even wash.");
                Assert.That(towards.Count(x => x > peak * .7f), Is.LessThan(towards.Length / 3), "Bright beams cover only part of the view.");
            }
            finally { shafts.isolateRays = false; shafts.previewTime = oldClock; RenderSettings.skybox = oldSky; }
        }

        [Test]
        public void Gpu_HazeVeilsFarGroundTowardsTheSunOnly()
        {
            using var globals = new GlobalsScope();
            using var h = new AeroRenderHarness(64);
            var grey = h.Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
            grey.SetColor("_BaseColor", new Color(.2f, .25f, .2f));
            h.Camera.nearClipPlane = .3f; h.Camera.farClipPlane = 5000; h.Camera.fieldOfView = 40;
            Assert.That(AeroSunHazeRendererFeature.SupportsCamera(h.Camera), Is.False, "Solid-colour fixtures never get the outdoor haze.");
            h.Camera.clearFlags = CameraClearFlags.Skybox; // an outdoor view; the wall covers the whole frame
            var wall = h.Spawn(PrimitiveType.Quad, grey, Vector3.zero, new Vector3(6000, 6000, 1));
            var state = AeroSunGlow.Evaluate(AeroSunGlowSettings.Sun(), new Vector3(0, Mathf.Sin(8 * Mathf.Deg2Rad), Mathf.Cos(8 * Mathf.Deg2Rad)), DaySun, 1, 0, 1, 0);
            state.haze = new Vector4(.003f, 1, .0025f, 1); state.hazeBase = 0;
            var off = state; off.haze.y = 0;
            Assert.That(state.HazeVisible, Is.True); Assert.That(off.HazeVisible, Is.False);

            (Color without, Color with) Look(Vector3 wallPosition, Vector3 forward)
            {
                wall.transform.SetPositionAndRotation(wallPosition, Quaternion.LookRotation(forward));
                h.Camera.transform.SetPositionAndRotation(new Vector3(0, 50, 0), Quaternion.LookRotation(forward));
                AeroSunGlow.Push(off); var a = h.Render();
                AeroSunGlow.Push(state); var b = h.Render();
                return (h.Pixel(a, h.Size / 2, h.Size / 2), h.Pixel(b, h.Size / 2, h.Size / 2));
            }

            var (farBefore, farAfter) = Look(new Vector3(0, 50, 900), Vector3.forward);
            Assert.That(Luminance(farAfter), Is.GreaterThan(Luminance(farBefore) + .1f), "Far ground towards the sun melts into its glow.");
            var added = farAfter - farBefore;
            Assert.That(added.r, Is.GreaterThan(added.b), "The haze takes the warm glow's colour.");
            var (awayBefore, awayAfter) = Look(new Vector3(0, 50, -900), Vector3.back);
            Assert.That(Luminance(awayAfter), Is.EqualTo(Luminance(awayBefore)).Within(.01f), "Away from the sun nothing changes.");
            var (nearBefore, nearAfter) = Look(new Vector3(0, 50, 6), Vector3.forward);
            Assert.That(Luminance(nearAfter) - Luminance(nearBefore), Is.LessThan((Luminance(farAfter) - Luminance(farBefore)) * .2f),
                "Near ground keeps nearly all its colour; only distance melts it into the glow.");
        }
    }
}
