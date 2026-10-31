using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>Bubble Tube pillar: prefab wiring, construction stages and per-instance water shader.</summary>
    public sealed class BubbleTubePillarEditModeTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Buildings/Pillar.prefab";
        private const string ShaderName = "TowerOfBabel/Buildings/Bubble Tube Pillar";

        private static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        private static Material PillarMaterial => Prefab.GetComponent<MeshRenderer>().sharedMaterial;

        [Test]
        public void PrefabUsesInstancedBubbleTubeMaterial()
        {
            Assert.That(PillarMaterial.shader.name, Is.EqualTo(ShaderName));
            Assert.That(PillarMaterial.enableInstancing, Is.True, "Near pool and far batches render the pillar instanced.");
            Assert.That(ShaderUtil.GetShaderMessages(PillarMaterial.shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), Is.Empty);
            foreach (string property in new[] { "_BubbleAmountMin", "_BubbleAmountMax", "_WaterMotionMin", "_WaterMotionMax", "_InstanceVariation", "_BuildMode", "_BuildReveal", "_BuildClock" })
                Assert.That(PillarMaterial.HasProperty(property), property);
            Assert.That(PillarMaterial.GetFloat("_BubbleAmountMax"), Is.GreaterThan(PillarMaterial.GetFloat("_BubbleAmountMin")));
            Assert.That(PillarMaterial.GetFloat("_WaterMotionMax"), Is.GreaterThan(PillarMaterial.GetFloat("_WaterMotionMin")));
        }

        [Test]
        public void AllElevenStagesAreDistinctSingleSubmeshMeshes()
        {
            var building = Prefab.GetComponent<Building>();
            var meshes = Enumerable.Range(0, 11).Select(i => building.GetStageMesh((byte)i)).ToArray();
            Assert.That(meshes, Has.All.Not.Null);
            Assert.That(meshes.Distinct().Count(), Is.EqualTo(11));
            Assert.That(meshes.Select(m => m.subMeshCount), Has.All.EqualTo(1));
            Assert.That(meshes.Select(m => m.uv2.Length), Has.All.EqualTo(meshes[0].uv2.Length).Or.GreaterThan(0));
            Assert.That(meshes[10].name, Is.EqualTo("Pillar_BubbleTube"));
            Assert.That(Prefab.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(meshes[0]));
        }

        [Test]
        public void CompletedPillarKeepsFootprintColliderAndBudget()
        {
            Mesh complete = Prefab.GetComponent<Building>().GetStageMesh(10);
            Bounds b = complete.bounds;
            // TowerGenerator.PillarSupportRadius derives deck supports from these bounds.
            Assert.That(b.min.y, Is.EqualTo(0).Within(1e-3f)); Assert.That(b.max.y, Is.EqualTo(4).Within(1e-3f));
            Assert.That(b.extents.x, Is.EqualTo(.41f).Within(2e-3f)); Assert.That(b.extents.z, Is.EqualTo(.41f).Within(2e-3f));
            Assert.That(complete.triangles.Length / 3, Is.LessThanOrEqualTo(1000));
            var capsule = Prefab.GetComponent<CapsuleCollider>();
            Assert.That(capsule.radius, Is.EqualTo(.41f).Within(1e-3f));
            Assert.That(capsule.height, Is.EqualTo(4).Within(1e-4f));
        }

        [Test]
        public void CompletedPillarEncodesSurfaceRegions()
        {
            Mesh complete = Prefab.GetComponent<Building>().GetStageMesh(10);
            var regions = complete.uv2.Select(uv => Mathf.FloorToInt(uv.x * 8)).Distinct().OrderBy(r => r).ToArray();
            Assert.That(regions, Is.EquivalentTo(new[] { 0, 1, 2, 4 }), "pearl, enamel, chrome and water tube");
            var water = complete.vertices.Where((v, i) => Mathf.FloorToInt(complete.uv2[i].x * 8) == 4).ToArray();
            Assert.That(water.Min(v => v.y), Is.EqualTo(.485f).Within(.01f));
            Assert.That(water.Max(v => v.y), Is.EqualTo(3.515f).Within(.01f));
        }

        [Test]
        public void ChunkLibraryAddsEightPiecesPerStage()
        {
            var library = (PillarChunkLibrary)new SerializedObject(Prefab.GetComponent<PillarConstructionMotion>()).FindProperty("library").objectReferenceValue;
            Assert.That(AssetDatabase.GetAssetPath(library), Does.EndWith("BubbleTubeChunks.asset"));
            Assert.That(library.Pieces.Length, Is.EqualTo(80));
            Assert.That(library.Pieces.GroupBy(p => p.Stage).Select(g => g.Count()), Has.All.EqualTo(8));
            Assert.That(library.Pieces.Select(p => p.Stage).Distinct().OrderBy(s => s), Is.EqualTo(Enumerable.Range(1, 10)));
        }

        [Test]
        public void InstancesAtDifferentOriginsGetDifferentWaterOnlyWhenVariationIsOn()
        {
            var a = new Vector3(0, 0, 0); var b = new Vector3(37.3f, 0, -12.9f);
            float same = Difference(Render(m => m.SetFloat("_InstanceVariation", 0), a), Render(m => m.SetFloat("_InstanceVariation", 0), b));
            float varied = Difference(Render(m => m.SetFloat("_InstanceVariation", 1), a), Render(m => m.SetFloat("_InstanceVariation", 1), b));
            Assert.That(same, Is.LessThan(.004f), "With variation off, two origins render the same tube.");
            Assert.That(varied, Is.GreaterThan(same * 4 + .01f), "With variation on, each origin seeds its own bubbles and motion.");
        }

        [Test]
        public void BubbleAmountControlsHowManyBubblesAppear()
        {
            System.Action<Material> Amount(float value) => m => { m.SetFloat("_BubbleAmountMin", value); m.SetFloat("_BubbleAmountMax", value); m.SetFloat("_BubbleBurstiness", 0); };
            var none = Render(Amount(0), Vector3.zero);
            int few = ChangedPixels(none, Render(Amount(.05f), Vector3.zero));
            int many = ChangedPixels(none, Render(Amount(.9f), Vector3.zero));
            Assert.That(many, Is.GreaterThan(few * 3 + 20), $"few={few} many={many}");
        }

        [Test]
        public void WaterMovesOverTimeAndMotionAmountChangesIt()
        {
            System.Action<Material> calm = m => { m.SetFloat("_WaterMotionMin", 0); m.SetFloat("_WaterMotionMax", 0); m.SetFloat("_BubbleSpeed", 0); };
            System.Action<Material> lively = m => { m.SetFloat("_WaterMotionMin", 1.5f); m.SetFloat("_WaterMotionMax", 1.5f); m.SetFloat("_BubbleSpeed", 0); };
            float calmChange = Difference(Render(calm, Vector3.zero, 1), Render(calm, Vector3.zero, 1.6f));
            float livelyChange = Difference(Render(lively, Vector3.zero, 1), Render(lively, Vector3.zero, 1.6f));
            Assert.That(livelyChange, Is.GreaterThan(calmChange * 1.5f + .002f), $"calm={calmChange} lively={livelyChange}");
            Assert.That(Difference(Render(m => { }, Vector3.zero, 1), Render(m => { }, Vector3.zero, 2)), Is.GreaterThan(.003f), "Bubbles rise and water moves.");
        }

        [Test]
        public void BubblesComeInStreamsAndCalmSpellsWithinOneInstance()
        {
            // Same pillar over a minute: with burstiness on, the bubble count swings far more.
            int[] Counts(float burstiness)
            {
                System.Action<Material> none = m => { m.SetFloat("_BubbleAmountMin", 0); m.SetFloat("_BubbleAmountMax", 0); };
                System.Action<Material> some = m => { m.SetFloat("_BubbleAmountMin", .3f); m.SetFloat("_BubbleAmountMax", .3f); m.SetFloat("_BubbleBurstiness", burstiness); };
                return Enumerable.Range(0, 8).Select(i => ChangedPixels(Render(none, Vector3.zero, i * 8.5f), Render(some, Vector3.zero, i * 8.5f))).ToArray();
            }
            int[] steady = Counts(0), bursty = Counts(1);
            float Spread(int[] c) => (c.Max() + 1f) / (c.Min() + 1f);
            Assert.That(Spread(bursty), Is.GreaterThan(Spread(steady) * 1.5f + .5f),
                $"steady=[{string.Join(",", steady)}] bursty=[{string.Join(",", bursty)}]");
            Assert.That(bursty.Max(), Is.GreaterThan(30), "Streams must be visible.");
        }

        [Test]
        public void GlassDistortsWhatIsSeenThroughTheTube()
        {
            System.Action<Material> Glass(float amount) => m => { m.SetFloat("_GlassDistortion", amount); m.SetFloat("_WaterMotionMin", 0); m.SetFloat("_WaterMotionMax", 0); };
            float flat = Difference(Render(Glass(0), Vector3.zero), Render(Glass(0), Vector3.zero));
            float wavy = Difference(Render(Glass(0), Vector3.zero), Render(Glass(1.5f), Vector3.zero));
            Assert.That(flat, Is.LessThan(.001f));
            Assert.That(wavy, Is.GreaterThan(.01f), "Wavy glass must bend the spine, water and bubbles behind it.");
        }

        [Test]
        public void BubblesShowIridescentColour()
        {
            System.Action<Material> Bubbles(float iridescence) => m => { m.SetFloat("_BubbleAmountMin", .6f); m.SetFloat("_BubbleAmountMax", .6f); m.SetFloat("_BubbleBurstiness", 0); m.SetFloat("_BubbleIridescence", iridescence); };
            var plain = Render(Bubbles(0), Vector3.zero); var filmed = Render(Bubbles(1.5f), Vector3.zero);
            // Thin film adds warm (pink/gold) light that the blue water never has on its own.
            float Warm(Color[] c) => c.Average(p => Mathf.Max(0, p.r - p.b * .55f));
            Assert.That(Warm(filmed), Is.GreaterThan(Warm(plain) + .002f), $"plain={Warm(plain)} filmed={Warm(filmed)}");
        }

        // Renders the completed pillar's tube from a fixed relative camera and returns the centre crop.
        private static Color[] Render(System.Action<Material> configure, Vector3 origin, float time = 2)
        {
            var material = new Material(PillarMaterial) { hideFlags = HideFlags.DontSave };
            material.SetFloat("_MotionTime", time); material.SetFloat("_WaterDetailDistance", 500);
            configure(material);
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(96, 160, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(96, 160, TextureFormat.RGBA32, false);
            var old = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var pillar = new GameObject("Pillar", typeof(MeshFilter), typeof(MeshRenderer));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pillar, scene);
                pillar.transform.position = origin;
                pillar.GetComponent<MeshFilter>().sharedMesh = Prefab.GetComponent<Building>().GetStageMesh(10);
                pillar.GetComponent<MeshRenderer>().sharedMaterial = material;
                var lightObject = new GameObject("Light"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f;
                lightObject.transform.rotation = Quaternion.Euler(35, -30, 0);
                var cameraObject = new GameObject("Camera"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.fieldOfView = 40;
                camera.targetTexture = target;
                camera.transform.position = origin + new Vector3(0, 2, -2.2f); camera.transform.LookAt(origin + new Vector3(0, 2, 0));
                camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 96, 160), 0, 0); pixels.Apply();
                // Centre of the tube only: the pearl base/cap use origin-seeded marble by design.
                return pixels.GetPixels(28, 30, 40, 100);
            }
            finally
            {
                RenderTexture.active = old; ShaderUtil.allowAsyncCompilation = async;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(material);
            }
        }

        private static float Difference(Color[] a, Color[] b) =>
            a.Zip(b, (x, y) => Mathf.Abs(x.r - y.r) + Mathf.Abs(x.g - y.g) + Mathf.Abs(x.b - y.b)).Average() / 3;

        // Pixels that bubbles visibly change compared with a bubble-free render.
        private static int ChangedPixels(Color[] baseline, Color[] pixels) =>
            baseline.Zip(pixels, (x, y) => Mathf.Abs(x.grayscale - y.grayscale)).Count(d => d > .06f);
    }
}
