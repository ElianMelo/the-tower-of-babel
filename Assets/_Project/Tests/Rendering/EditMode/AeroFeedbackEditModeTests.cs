using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>User feedback 2026-10-29: floor wetness, Crest caustics, frequent white light, particles.</summary>
    public sealed class AeroFeedbackEditModeTests
    {
        const string Crest = "Assets/_Project/Environment/Lighting/Caustics/CrestCaustics.png";

        [Test]
        public void WalkableFloors_StayCleanWhenDewyInsteadOfSpeckled()
        {
            foreach (var path in new[] { "Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Floor_Tile.mat",
                "Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Step_Tile.mat", "Assets/_Project/Environment/Lighting/GlassWaterFloor.mat" })
                Assert.That(AssetDatabase.LoadAssetAtPath<Material>(path).GetFloat("_WetBeads"), Is.Zero, path);

            var profile = AssetDatabase.LoadAssetAtPath<AeroWetnessProfile>("Assets/_Project/Environment/Lighting/AeroWetness.asset");
            using var h = new AeroRenderHarness(160);
            var material = h.Own(new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Floor_Tile.mat")));
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset");
            for (int x = -1; x <= 1; x++) for (int z = 0; z <= 2; z++) h.Spawn(mesh, material, new Vector3(x * 4, 0, z * 4));
            h.Camera.transform.position = new Vector3(0, 1.7f, 0); h.Camera.transform.LookAt(new Vector3(0, 0, 2.5f));
            var go = new GameObject("Floor wetness test") { hideFlags = HideFlags.HideAndDontSave }; go.SetActive(false);
            var controller = go.AddComponent<AeroWetnessController>(); controller.profile = profile; controller.previewTime = 3; go.SetActive(true);
            try
            {
                float Detail(Color[] image)
                {
                    float sum = 0; int n = h.Size;
                    for (int y = 1; y < n; y++) for (int x = 1; x < n; x++)
                        sum += Mathf.Abs(image[y * n + x].grayscale - image[y * n + x - 1].grayscale) + Mathf.Abs(image[y * n + x].grayscale - image[(y - 1) * n + x].grayscale);
                    return sum / (n * n);
                }
                controller.OverrideWetness(0); controller.Tick(0); float dry = Detail(h.Render());
                controller.OverrideWetness(.25f); controller.Tick(0); float dew = Detail(h.Render());
                controller.OverrideWetness(1); controller.Raining = true; for (int i = 0; i < 20; i++) controller.Tick(.5f);
                float rain = Detail(h.Render());
                Assert.That(dew, Is.LessThan(dry * 1.15f + .002f), "Dew must not cover the deck in speckle.");
                Assert.That(rain, Is.LessThan(dry * 2f + .01f), "Rain on a deck is a glossy film with soft rings, not sandpaper.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                var scene = Object.FindObjectsByType<AeroWetnessController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
                if (scene != null) { scene.enabled = false; scene.enabled = true; }
            }
        }

        [Test]
        public void EveryWaterLight_SamplesTheCrestCausticNet()
        {
            var crest = AssetDatabase.LoadAssetAtPath<Texture2D>(Crest);
            Assert.That(crest, Is.Not.Null);
            Assert.That(System.IO.File.Exists("Assets/_Project/Environment/Lighting/Caustics/THIRD_PARTY_NOTICE.md"), Is.True, "MIT attribution ships with the texture.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(Crest);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat)); Assert.That(importer.mipmapEnabled, Is.True);
            var river = AssetDatabase.LoadAssetAtPath<RiverGlassProfile>("Assets/_Project/Environment/Lighting/RiverGlass.asset");
            Assert.That(river.caustics, Is.SameAs(crest)); Assert.That(river.surfaceCaustics, Is.SameAs(crest));
            foreach (var path in new[] { "Assets/_Project/Environment/AeroLakeWater.mat", "Assets/_Project/Environment/Lighting/AquaProjectionCookie.mat", "Assets/_Project/Environment/Lighting/GlassWaterFloor.mat" })
                Assert.That(AssetDatabase.LoadAssetAtPath<Material>(path).GetTexture("_CausticsTex"), Is.SameAs(crest), path);
            var feature = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset").rendererFeatures.OfType<AeroLightRendererFeature>().Single();
            Assert.That(feature.caustics, Is.SameAs(crest));
        }

        [Test]
        public void WhiteProjectedLight_AppearsMoreOftenWithoutChangingItsLook()
        {
            var p = AssetDatabase.LoadAssetAtPath<RiverGlassProfile>("Assets/_Project/Environment/Lighting/RiverGlass.asset");
            Assert.That(p.presentationVersion, Is.GreaterThanOrEqualTo(13));
            Assert.That(Mathf.Max(p.projectionQuietDuration.x, p.projectionQuietDuration.y), Is.LessThanOrEqualTo(1.5f), "Short gaps between appearances.");
            Assert.That(p.projectionBurstDuration.x, Is.GreaterThanOrEqualTo(7), "Longer appearances.");
            Assert.That(p.projectionGlobalQuietInterval.x, Is.GreaterThanOrEqualTo(30), "Complete quiet moments are rarer.");
            // Share of time a source is lit, before global quiet: burst / (burst + quiet) > 80%.
            float duty = (p.projectionBurstDuration.x + p.projectionBurstDuration.y) /
                (p.projectionBurstDuration.x + p.projectionBurstDuration.y + p.projectionQuietDuration.x + p.projectionQuietDuration.y);
            Assert.That(duty, Is.GreaterThan(.8f));
        }

        [Test]
        public void Fireflies_SpawnAtLeastTwiceAsOftenAsBefore()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroDayNightProfile>("Assets/_Project/Environment/Lighting/AeroDayNight.asset");
            float before = Mathf.Lerp(3, 7, 0) * .65f * 1.3f; // previous daytime rate
            Assert.That(AeroTowerAtmosphere.FireflyRate(1, profile, 1), Is.GreaterThanOrEqualTo(before * 2));
            Assert.That(AeroTowerAtmosphere.FireflyRate(0, profile, 1), Is.GreaterThan(AeroTowerAtmosphere.FireflyRate(1, profile, 1)), "Night stays livelier.");
            Assert.That(AeroTowerAtmosphere.FireflyRate(1, profile, 0), Is.Zero, "Only inside the tower.");
        }
    }
}
