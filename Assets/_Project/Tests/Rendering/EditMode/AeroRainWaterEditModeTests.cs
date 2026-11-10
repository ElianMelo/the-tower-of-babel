using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>Rain against the lake water (Stylized Water 3): draw order and raindrop ripples.</summary>
    public sealed class AeroRainWaterEditModeTests
    {
        const string RainProfilePath = "Assets/_Project/Environment/Lighting/AeroRain.asset";
        const string LakePath = "Assets/_Project/Environment/AeroLakeWater.mat";
        const string ForwardPassPath = "Assets/Stylized Water 3/Shaders/Passes/ForwardPass.hlsl";
        static readonly string[] Globals = { "_AeroRain", "_AeroWetClock", "_CustomTime", "_AeroRainRippleFoam" };

        [Test]
        public void Rain_DrawsAfterTheLakeWaterWhichWritesDepth()
        {
            var lake = AssetDatabase.LoadAssetAtPath<Material>(LakePath);
            var profile = AssetDatabase.LoadAssetAtPath<AeroRainProfile>(RainProfilePath);
            Assert.That(lake.GetFloat("_ZWrite"), Is.EqualTo(1), "The water writes depth, so rain behind it is depth-tested away.");
            foreach (var material in new[] { profile.dropMaterial, profile.splashMaterial })
            {
                Assert.That(material.renderQueue, Is.GreaterThan(lake.renderQueue), material.name + " draws after the water, so water never paints over rain in front of it.");
                Assert.That(material.renderQueue, Is.LessThan(3500), "...but stays in the transparent range.");
                Assert.That(material.GetFloat("_ZWrite"), Is.Zero, "Drops never occlude each other.");
                Assert.That((int)RenderQueueOf(material), Is.EqualTo(material.renderQueue), "URP's own queue rule keeps this order when it revalidates the material.");
            }
        }

        [Test]
        public void WaterPackage_KeepsTheRainRippleHook()
        {
            string pass = File.ReadAllText(ForwardPassPath);
            Assert.That(pass, Does.Contain("Assets/_Project/Shaders/Aero/AeroRainRipples.hlsl"),
                "Re-add the Tower of Babel hook to the water forward pass (lost on a Stylized Water update?).");
            Assert.That(pass, Does.Contain("AeroRainRipples(water.positionWS, water.vertexNormal)"));
            Assert.That(pass, Does.Contain("aeroRainRipple.z"));
        }

        static float RenderQueueOf(Material m) => (int)UnityEngine.Rendering.RenderQueue.Transparent + m.GetFloat("_QueueOffset");

        static float Magenta(Color c) => Mathf.Min(c.r, c.b) - c.g;

        [Test]
        public void Rain_InFrontOfWaterStaysVisibleAndRainBehindItIsHidden()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroRainProfile>(RainProfilePath);
            var lake = AssetDatabase.LoadAssetAtPath<Material>(LakePath);
            using var h = new AeroRenderHarness(128);
            var saved = Save();
            try
            {
                Shader.SetGlobalFloat("_AeroRain", 0); Shader.SetGlobalFloat("_CustomTime", 3);
                // Opaque magenta drop: the real drop material and queue, only the streak sprite is replaced.
                var drop = h.Own(new Material(profile.dropMaterial));
                drop.SetTexture("_BaseMap", Texture2D.whiteTexture); drop.SetTextureScale("_BaseMap", Vector2.one);
                drop.SetColor("_BaseColor", new Color(1, 0, 1, 1));
                drop.renderQueue = profile.dropMaterial.renderQueue; // a material copy falls back to the shader's queue
                h.Camera.transform.SetPositionAndRotation(new Vector3(0, 10, 0), Quaternion.Euler(90, 0, 0));
                h.Camera.fieldOfView = 60; h.Camera.nearClipPlane = .1f; h.Camera.farClipPlane = 100;
                h.Spawn(PrimitiveType.Plane, lake, Vector3.zero, Vector3.one * 4);
                var front = h.Spawn(PrimitiveType.Quad, drop, new Vector3(-2.5f, 4, 0), Vector3.one * 2);
                var behind = h.Spawn(PrimitiveType.Quad, drop, new Vector3(2.5f, -1, 0), Vector3.one * 2.5f);
                front.transform.rotation = behind.transform.rotation = Quaternion.Euler(90, 0, 0);

                var image = h.Render();
                Color Sample(Vector3 world)
                {
                    var v = h.Camera.WorldToViewportPoint(world);
                    return h.Pixel(image, (int)(v.x * h.Size), (int)(v.y * h.Size));
                }
                Color inFront = Sample(front.transform.position), underWater = Sample(behind.transform.position), water = Sample(new Vector3(0, 0, 3));
                Assert.That(Magenta(inFront), Is.GreaterThan(.6f), $"Rain in front of the water is drawn over it ({inFront}).");
                Assert.That(Magenta(underWater), Is.LessThan(.15f), $"Rain behind the water surface is hidden ({underWater}).");
                Assert.That(Magenta(water), Is.LessThan(.15f), "Sanity: plain water is not magenta.");
            }
            finally { Restore(saved); }
        }

        [Test]
        public void Water_RipplesOnlyWhileItRains()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroRainProfile>(RainProfilePath);
            var lake = AssetDatabase.LoadAssetAtPath<Material>(LakePath);
            using var h = new AeroRenderHarness(128);
            var saved = Save();
            var savedParams = Shader.GetGlobalVector(AeroRainController.RippleParamsId);
            try
            {
                Shader.SetGlobalVector(AeroRainController.RippleParamsId, profile.RippleParams);
                Shader.SetGlobalFloat(AeroRainController.RippleFoamId, profile.rippleFoam);
                Shader.SetGlobalFloat("_CustomTime", 3); Shader.SetGlobalFloat("_AeroWetClock", 11.3f);
                h.Camera.transform.SetPositionAndRotation(new Vector3(0, 6, -6), Quaternion.Euler(45, 0, 0));
                h.Spawn(PrimitiveType.Plane, lake, Vector3.zero, Vector3.one * 3);

                Shader.SetGlobalFloat("_AeroRain", 0);
                var dry = h.Render(); var dryAgain = h.Render();
                Shader.SetGlobalFloat("_AeroRain", 1);
                var wet = h.Render();
                Shader.SetGlobalFloat("_AeroRain", .25f);
                var drizzle = h.Render();

                float baseline = Difference(dry, dryAgain), heavy = Difference(dry, wet), light = Difference(dry, drizzle);
                Assert.That(baseline, Is.LessThan(.002f), "Frozen water renders the same twice (test sanity).");
                Assert.That(heavy, Is.GreaterThan(.004f), $"Rain visibly ripples the water (mean diff {heavy}).");
                Assert.That(light, Is.GreaterThan(baseline).And.LessThan(heavy), $"Ripples scale with rain intensity ({light} vs {heavy}).");
            }
            finally { Restore(saved); Shader.SetGlobalVector(AeroRainController.RippleParamsId, savedParams); }
        }

        static float Difference(Color[] a, Color[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++)
                sum += Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
            return (float)(sum / (a.Length * 3));
        }

        static float[] Save()
        {
            var values = new float[Globals.Length];
            for (int i = 0; i < Globals.Length; i++) values[i] = Shader.GetGlobalFloat(Globals[i]);
            return values;
        }

        static void Restore(float[] values)
        {
            for (int i = 0; i < Globals.Length; i++) Shader.SetGlobalFloat(Globals[i], values[i]);
        }
    }
}
