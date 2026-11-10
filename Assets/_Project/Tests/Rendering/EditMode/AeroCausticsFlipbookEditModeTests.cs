using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>
    /// User feedback 2026-11-03: the lava lamp projection looked low quality, the white light near water was too big,
    /// and shadows stopped a short way off. Both caustics now play looping flipbooks; the lamp cookie is sharper, split
    /// by colour and animated; the water light uses a different, finer net at half the tile size; sun shadows reach 150 m.
    /// </summary>
    public sealed class AeroCausticsFlipbookEditModeTests
    {
        const string Folder = "Assets/_Project/Environment/Lighting/Caustics/";
        const string CookiePath = "Assets/_Project/Environment/Lighting/AquaProjectionCookie.mat";

        static Texture2DArray Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2DArray>(Folder + name);

        [Test]
        public void Flipbooks_AreLoopingTiledTextureArrays()
        {
            var lamp = Load("LavaLampCaustics.png");
            Assert.That(lamp, Is.Not.Null, "Run Tower of Babel/Environment/Apply caustics flipbooks.");
            Assert.That((lamp.width, lamp.height, lamp.depth), Is.EqualTo((512, 512, 32)));
            Assert.That(System.IO.File.Exists(Folder + "PoolCausticsFlipbook.png"), Is.False, "The water light now plays the lamp flipbook.");
            foreach (var t in new[] { lamp })
            {
                Assert.That(t.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
                Assert.That(t.mipmapCount, Is.GreaterThan(1), "Distant tiles blur instead of shimmering.");
            }
            string notice = System.IO.File.ReadAllText(Folder + "THIRD_PARTY_NOTICE.md");
            StringAssert.Contains("LavaLampCaustics.png", notice);
        }

        [Test]
        public void LampCookie_IsSharp_ColourSplit_RoundAndAnimated()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(CookiePath);
            Assert.That(source.GetTexture("_CausticsArray"), Is.SameAs(Load("LavaLampCaustics.png")));
            Assert.That(source.GetFloat("_FrameCount"), Is.EqualTo(32));
            Assert.That(ShaderUtil.GetShaderMessages(source.shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), Is.Empty);
            Assert.That(AeroProjectionLamp.CookieResolution, Is.GreaterThanOrEqualTo(256), "The aqua lamps share the sharper cookie.");
            var material = new Material(source);
            var rt = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var image = new Texture2D(512, 512, TextureFormat.RGBA32, false, true);
            try
            {
                Color[] Render(float seconds)
                {
                    material.SetFloat("_AeroCookieClock", seconds);
                    var old = RenderTexture.active;
                    Graphics.Blit(null, rt, material, 0);
                    RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                    RenderTexture.active = old;
                    return image.GetPixels();
                }
                var a = Render(2); var b = Render(2.6f);
                Color At(Color[] p, int x, int y) => p[y * 512 + x];
                float edges = 0, change = 0, split = 0; int n = 0;
                for (int y = 160; y < 352; y++)
                for (int x = 160; x < 352; x++)
                {
                    edges += Mathf.Abs(At(a, x, y).g - At(a, x - 1, y).g);
                    change += Mathf.Abs(At(a, x, y).g - At(b, x, y).g);
                    split += Mathf.Abs(At(a, x, y).r - At(a, x, y).b);
                    n++;
                }
                edges /= n; change /= n; split /= n;
                float peak = a.Max(c => c.g);
                Assert.That(peak, Is.GreaterThan(.85f), "Bright focused filaments.");
                Assert.That(edges, Is.GreaterThan(.012f), $"Crisp pattern, not a soft blur (mean gradient {edges:F4}).");
                Assert.That(change, Is.GreaterThan(.02f), "The caustics move.");
                Assert.That(split, Is.GreaterThan(.004f), "Red and blue split apart at the filament edges.");
                Assert.That(At(a, 2, 2).g + At(a, 509, 509).g, Is.LessThan(.02f), "Soft round aperture: corners stay dark.");
            }
            finally { Object.DestroyImmediate(material); Object.DestroyImmediate(image); rt.Release(); Object.DestroyImmediate(rt); }
        }

        [Test]
        public void WaterLight_IsTheLampCaustics_InWhite()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            var feature = renderer.rendererFeatures.OfType<AeroLightRendererFeature>().Single();
            Assert.That(feature.caustics, Is.SameAs(Load("LavaLampCaustics.png")), "The same caustics as the lava lamp.");
            Assert.That(feature.settings.scale, Is.GreaterThan(.42f), "Smaller than the old 2.4 m Crest tiles.");
            var tint = feature.settings.tint;
            Assert.That(Mathf.Min(tint.r, tint.g, tint.b), Is.GreaterThan(.9f), "White, not blue.");
            Assert.That(ShaderUtil.GetShaderMessages(feature.lightShader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), Is.Empty);
        }

        [Test]
        public void SunShadows_ReachFarIntoTheTower()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            Assert.That(pipeline.shadowDistance, Is.InRange(100, 150), "Well past the old 50 m cut-off, within the frame budget.");
            Assert.That(pipeline.shadowCascadeCount, Is.EqualTo(3));
            Assert.That(pipeline.cascade3Split.x * pipeline.shadowDistance, Is.LessThan(12), "The nearest cascade stays tight and sharp.");
            Assert.That(pipeline.mainLightShadowmapResolution, Is.EqualTo(4096),
                "Interior sun shafts are shaped by this map; at 2048 they shimmered and lost thin arch shadows.");
            // Every shaft sample stays in the sharp first two cascades, never the coarse 120 m one.
            var rays = AssetDatabase.LoadAssetAtPath<TowerOfBabel.Rendering.AeroRaySettings>("Assets/_Project/Environment/Lighting/TowerInteriorRays.asset");
            Assert.That(pipeline.cascade3Split.y * pipeline.shadowDistance, Is.GreaterThanOrEqualTo(Mathf.Min(rays.distance, 32)),
                "The second cascade covers the full shaft march.");
            Assert.That(pipeline.additionalLightsShadowmapResolution, Is.EqualTo(1024), "Spot shadows are all 256 px; a 1024 atlas holds 16.");
        }
    }
}
