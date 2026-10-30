using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>Water inside glass (presentation 12): colour, refraction, underside optics.</summary>
    public sealed class RiverVolumeEditModeTests
    {
        const string TileMesh = "Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset";

        sealed class Source : IRiverGlassDrawSource
        {
            public readonly List<RiverGlassDraw> draws = new();
            public void CollectRiverGlass(Camera camera, Plane[] planes, List<RiverGlassDraw> destination) => destination.AddRange(draws);
        }

        static RiverGlassRendererFeature Feature() =>
            AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset").rendererFeatures.OfType<RiverGlassRendererFeature>().Single();

        // One channel tile (slab 0..-0.2 m) with its opaque aperture, plus a receiver below.
        static void Build(AeroRenderHarness h, Source source, Material receiver, float receiverY)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(TileMesh);
            var floor = h.Own(new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Environment/Lighting/GlassWaterFloor.mat")));
            floor.SetFloat("_RiverStage", 10); floor.SetFloat("_RiverFloorIndex", 0); floor.SetFloat("_RadialFloor", 0);
            var p = new Vector3(-2, 0, 2);
            h.Spawn(mesh, floor, p);
            var draw = new RiverGlassDraw { mesh = mesh, matrices = new[] { Matrix4x4.Translate(p) }, count = 1, floor = 0, height = 0, radial = 0 };
            draw.Configure(); source.draws.Add(draw);
            h.Spawn(PrimitiveType.Cube, receiver, new Vector3(0, receiverY, 0), new Vector3(6, .2f, 6));
            RiverGlassRenderRegistry.Register(source);
        }

        [Test]
        public void FromAbove_WaterReadsAsAquaOverAWhiteBed()
        {
            using var h = new AeroRenderHarness(128);
            var feature = Feature(); float oldClock = feature.previewTime; var source = new Source();
            try
            {
                var white = h.Own(new Material(Shader.Find("Universal Render Pipeline/Unlit"))); white.color = new Color(.9f, .9f, .9f);
                Build(h, source, white, -1.5f);
                feature.previewTime = 2;
                h.Camera.orthographic = true; h.Camera.orthographicSize = 1.5f;
                h.Camera.transform.position = Vector3.up * 4; h.Camera.transform.rotation = Quaternion.Euler(90, 0, 0);
                var image = h.Render();
                Color c = h.Pixel(image, 64, 64);
                Assert.That(c.b, Is.GreaterThan(c.r + .2f), "Absorption over the real path removes red: aqua water.");
                Assert.That(c.g, Is.GreaterThan(c.r + .1f));
                Assert.That(feature.LastVolumeDraws, Is.EqualTo(1));
            }
            finally { RiverGlassRenderRegistry.Unregister(source); feature.previewTime = oldClock; }
        }

        [Test]
        public void Refraction_BendsTheFloorSeenThroughTheWater()
        {
            using var h = new AeroRenderHarness(128);
            var feature = Feature(); float oldClock = feature.previewTime; var source = new Source();
            var profile = Object.Instantiate(feature.profile); var oldProfile = feature.profile;
            try
            {
                feature.profile = profile;
                var checker = h.Own(new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat });
                checker.SetPixels(new[] { Color.white, Color.black, Color.black, Color.white }); checker.Apply();
                var receiver = h.Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
                receiver.SetTexture("_BaseMap", checker); receiver.SetTextureScale("_BaseMap", new Vector2(16, 16));
                Build(h, source, receiver, -1.5f);
                feature.previewTime = 2;
                h.Camera.fieldOfView = 50; h.Camera.transform.position = new Vector3(-1.5f, 2.6f, -1.6f); h.Camera.transform.LookAt(new Vector3(.2f, -.1f, 0));
                profile.refractionStrength = 0; var straight = h.Render();
                profile.refractionStrength = 1.5f; var bent = h.Render();
                int changed = straight.Where((c, i) => Mathf.Abs(c.grayscale - bent[i].grayscale) > .15f).Count();
                Assert.That(changed, Is.GreaterThan(60), "The checker under the channel must shift through surface and bed.");
            }
            finally { RiverGlassRenderRegistry.Unregister(source); feature.profile = oldProfile; feature.previewTime = oldClock; Object.DestroyImmediate(profile); }
        }

        [Test]
        public void FromBelow_GrazingViewsReflectTheInteriorInsteadOfShowingTheSky()
        {
            using var h = new AeroRenderHarness(128);
            var feature = Feature(); float oldClock = feature.previewTime; var source = new Source();
            try
            {
                var sky = h.Own(new Material(Shader.Find("Universal Render Pipeline/Unlit"))); sky.color = new Color(1, .2f, .1f);
                Build(h, source, sky, 2.5f); // a bright red "sky" panel above the channel
                feature.previewTime = 2;
                float RedRatio(Vector3 position)
                {
                    h.Camera.transform.position = position; h.Camera.transform.LookAt(new Vector3(0, 0, 0));
                    var image = h.Render(); Color c = h.Pixel(image, 64, 64);
                    return c.r / Mathf.Max(.01f, c.r + c.g + c.b);
                }
                h.Camera.fieldOfView = 20;
                float steep = RedRatio(new Vector3(0, -3, -.15f));
                float grazing = RedRatio(new Vector3(0, -.45f, -3.2f));
                Assert.That(steep, Is.GreaterThan(grazing + .1f),
                    "Inside Snell's window the panel above shows; at grazing angles total internal reflection shows the water instead.");
            }
            finally { RiverGlassRenderRegistry.Unregister(source); feature.previewTime = oldClock; }
        }

        [Test]
        public void Profile_IsMigratedToTheVolumeWithoutTouchingAcceptedCaustics()
        {
            var profile = Feature().profile;
            Assert.That(profile.presentationVersion, Is.GreaterThanOrEqualTo(12));
            Assert.That(profile.absorption.r, Is.GreaterThan(profile.absorption.g).And.GreaterThan(profile.absorption.b), "Water absorbs red first.");
            Assert.That(profile.waterIor, Is.EqualTo(1.33f).Within(.01f));
            Assert.That(profile.glassIor, Is.EqualTo(1.5f).Within(.01f));
            Assert.That(profile.Steps(false), Is.GreaterThan(profile.Steps(true)), "Mobile marches fewer steps.");
            // Accepted projected caustics (DESIGN_DIRECTION.md) stay exactly as approved.
            Assert.That(profile.projectionAngle, Is.EqualTo(90));
            Assert.That(profile.projectionPatternDensity, Is.EqualTo(2.5f));
            Assert.That(profile.projectionHighlightOpacity, Is.EqualTo(.95f).Within(1e-4f));
            Assert.That(profile.projectionDetailOpacity, Is.EqualTo(.06f).Within(1e-4f));
            Assert.That(profile.daylightIntensity, Is.EqualTo(96));
            Assert.That(profile.nightIntensity, Is.EqualTo(64));
            Assert.That(profile.projectionMaxActivePatches, Is.EqualTo(2));
            Assert.That(RiverGlassLighting.CookieResolution, Is.EqualTo(2048));
        }
    }
}
