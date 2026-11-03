using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>Pixel checks for the ray-marched lava lamp fluid: blue fluid, green wax that moves, brighter at night.</summary>
    public sealed class LavaLampShaderEditModeTests
    {
        private const string FluidPath = "Assets/_Project/Environment/Lighting/LavaLampFluid.mat";
        private const string GlassMeshPath = "Assets/_Project/Models/Lighting/LavaLamp_Glass.asset";
        private static Color[] empty;

        private static Color[] Render(AeroRenderHarness harness, Material material, float daylight, float time)
        {
            material.SetFloat("_Daylight", daylight); material.SetFloat("_FlowTime", time);
            return harness.Render();
        }

        private static AeroRenderHarness Harness(out Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(FluidPath);
            Assert.That(source, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(source.shader), Is.False);
            var harness = new AeroRenderHarness(96);
            material = harness.Own(new Material(source));
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GlassMeshPath);
            var vessel = harness.Spawn(mesh, material, Vector3.zero);
            harness.Camera.fieldOfView = 30;
            harness.Camera.transform.position = new Vector3(0, .23f, -.95f);
            harness.Camera.transform.LookAt(new Vector3(0, .23f, 0));
            // The first draw of a freshly loaded variant can come back empty; warm it up before measuring.
            harness.Render();
            vessel.SetActive(false); empty = harness.Render(); vessel.SetActive(true);
            return harness;
        }

        // Pixels the vessel covers: anything that differs from the same view without it.
        private static Color[] Covered(Color[] image) =>
            image.Where((c, i) => Mathf.Abs(c.r - empty[i].r) + Mathf.Abs(c.g - empty[i].g) + Mathf.Abs(c.b - empty[i].b) > .02f).ToArray();

        [Test]
        public void Fluid_IsBlue_WithGreenWaxInside()
        {
            using var harness = Harness(out var material);
            var vessel = Covered(Render(harness, material, 0, 7));
            Assert.That(vessel.Length, Is.GreaterThan(400), "The vessel fills part of the view.");
            Assert.That(vessel.Count(c => c.b > c.r * 2 && c.b > c.g), Is.GreaterThan(vessel.Length / 3), "Blue fluid fills the glass.");
            Assert.That(vessel.Count(c => c.g > c.b && c.g > c.r * 1.5f), Is.GreaterThan(20), "Green wax blobs float in it.");
        }

        [Test]
        public void Wax_RisesAndFalls_OverTime()
        {
            using var harness = Harness(out var material);
            var early = Render(harness, material, 0, 3);
            var later = Render(harness, material, 0, 11);
            int changed = early.Zip(later, (a, b) => Mathf.Abs(a.g - b.g) > .1f).Count(c => c);
            Assert.That(changed, Is.GreaterThan(40), "The wax moves between frames.");
        }

        [Test]
        public void Glow_IsBrightAtNight_AndStillVisibleByDay()
        {
            using var harness = Harness(out var material);
            float Mean(Color[] c) => c.Average(p => p.r * .2126f + p.g * .7152f + p.b * .0722f + p.b * .2f);
            float night = Mean(Covered(Render(harness, material, 0, 7)));
            float day = Mean(Covered(Render(harness, material, 1, 7)));
            Assert.That(night, Is.GreaterThan(day * 2), "Night glow is much brighter.");
            Assert.That(day, Is.GreaterThan(.15f), "The lamp still glows during the day.");
            Assert.That(Covered(Render(harness, material, 0, 7)).Max(c => c.maxColorComponent), Is.GreaterThan(1.05f), "HDR glow reaches the bloom threshold at night.");
        }
    }
}
