using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Utils;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>Glass stair step: frosted white-blue transparent material on the unchanged step prefab.</summary>
    public sealed class GlassStepEditModeTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Buildings/Step_Tile.prefab";
        private const string ShaderName = "TowerOfBabel/Buildings/Glass Step";

        private static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        private static Material StepMaterial => Prefab.GetComponent<MeshRenderer>().sharedMaterial;

        [Test]
        public void PrefabUsesInstancedTransparentGlassMaterial()
        {
            Assert.That(StepMaterial.shader.name, Is.EqualTo(ShaderName));
            Assert.That(StepMaterial.enableInstancing, Is.True, "Near pool and far batches render the step instanced.");
            Assert.That(StepMaterial.renderQueue, Is.EqualTo((int)RenderQueue.Transparent), "Glass must draw after opaque floors so they show through.");
            Assert.That(StepMaterial.GetTag("RenderType", false), Is.EqualTo("Transparent"));
            foreach (string property in new[] { "_BuildMode", "_BuildReveal", "_BuildClock" })
                Assert.That(StepMaterial.HasProperty(property), property + " drives the construction dissolve.");
        }

        [Test]
        public void ShaderCompilesWithAllLightingVariants()
        {
            bool asynchronous = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            var material = new Material(StepMaterial);
            try
            {
                foreach (string keyword in new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS", "_CLUSTER_LIGHT_LOOP", "_SHADOWS_SOFT" })
                    material.EnableKeyword(keyword);
                for (int pass = 0; pass < material.passCount; pass++) ShaderUtil.CompilePass(material, pass, true);
                var errors = ShaderUtil.GetShaderMessages(material.shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error);
                Assert.That(errors.Select(e => e.line + " " + e.message), Is.Empty);
                Assert.That(material.shader.isSupported, Is.True);
                Assert.That(material.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0), "Glass steps still cast a lighter shadow.");
            }
            finally { Object.DestroyImmediate(material); ShaderUtil.allowAsyncCompilation = asynchronous; }
        }

        [Test]
        public void GlassIsClearWhiteBlueWithDeepAquaEdges()
        {
            Color tint = StepMaterial.GetColor("_TintColor"), edge = StepMaterial.GetColor("_EdgeTint");
            Assert.That(tint.b, Is.GreaterThanOrEqualTo(tint.g).And.GreaterThan(tint.r), "Body leans blue.");
            Assert.That(tint.r, Is.GreaterThan(.9f), "Body stays near white.");
            // Beer-Lambert: the 0.2 m body path barely tints, the long edge path reaches the edge colour.
            float body = StepMaterial.GetFloat("_EdgeLength");
            float bodyRed = Mathf.Pow(edge.r, .2f / body);
            Assert.That(bodyRed, Is.GreaterThan(.85f), "Looking through the tread stays nearly clear.");
            Assert.That(edge.b, Is.GreaterThan(edge.r + .3f), "Thick edges read clearly aqua-blue.");
            Assert.That(StepMaterial.GetFloat("_BodyBlend"), Is.InRange(.3f, .8f));
            Assert.That(StepMaterial.GetFloat("_IOR"), Is.EqualTo(1.5f).Within(.1f), "Ordinary glass.");
        }

        [Test]
        public void SlabFrameMatchesTheCompletedStepMesh()
        {
            Bounds bounds = Prefab.GetComponent<Building>().GetStageMesh(10).bounds;
            Assert.That((Vector3)StepMaterial.GetVector("_SlabCenter"), Is.EqualTo(bounds.center).Using(Vector3EqualityComparer.Instance));
            Assert.That((Vector3)StepMaterial.GetVector("_SlabHalfSize"), Is.EqualTo(bounds.extents).Using(Vector3EqualityComparer.Instance));
            Assert.That(StepMaterial.GetFloat("_CornerRadius"), Is.InRange(.2f, .4f), "Rounded corners are about 0.3 m in the world.");
        }

        [Test]
        public void RenderedEdgeIsMoreTintedThanTheClearBody()
        {
            var output = Render(withStep: true, out var reference);
            // Camera looks straight down at the step centre; row 64 crosses the near long edge.
            Color body = output[64 * 128 + 64], bareBody = reference[64 * 128 + 64];
            float BodyShift(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
            float edgeShift = 0;
            for (int y = 0; y < 128; y++)
                edgeShift = Mathf.Max(edgeShift, BodyShift(output[y * 128 + 64], reference[y * 128 + 64]) * (Mathf.Abs(y - 64) > 20 ? 1 : 0));
            Assert.That(edgeShift, Is.GreaterThan(BodyShift(body, bareBody) + .1f), "Edges show a deeper glass colour than the centre.");
        }

        [Test]
        public void StepGeometryColliderAndConstructionAreUnchanged()
        {
            var building = Prefab.GetComponent<Building>();
            Assert.That(building.GetStageMesh(10).name, Is.EqualTo("Step_Tile"));
            Assert.That(Enumerable.Range(0, 11).Select(i => building.GetStageMesh((byte)i)), Has.All.Not.Null);
            Assert.That(Prefab.transform.localScale, Is.EqualTo(new Vector3(1, 1, .35f)));
            Assert.That(Prefab.GetComponent<BoxCollider>(), Is.Not.Null, "Players still walk on the step.");
            Assert.That(Prefab.GetComponent<PillarConstructionMotion>(), Is.Not.Null);
        }

        [Test]
        public void RenderedStepLetsTheFloorBelowShowThroughWithABlueTint()
        {
            var output = Render(withStep: true, out var reference);
            int centre = output.Length / 2 + 64;
            Color glass = output[centre], bare = reference[centre];
            // Clear glass: the red floor shows through almost unchanged, with only a faint blue shift.
            Assert.That(glass.r, Is.GreaterThan(bare.r * .7f), "Red floor shows through the glass.");
            Assert.That(glass.r - glass.b, Is.LessThanOrEqualTo(bare.r - bare.b + .01f), "The glass leans the floor slightly toward blue.");
        }

        private static Color[] Render(bool withStep, out Color[] reference)
        {
            bool asynchronous = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var red = new Material(Shader.Find("Universal Render Pipeline/Unlit")); red.SetColor("_BaseColor", new Color(.9f, .1f, .1f));
            var oldActive = RenderTexture.active;
            try
            {
                var cam = new GameObject("Camera").AddComponent<Camera>(); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cam.gameObject, scene);
                cam.enabled = false; cam.scene = scene; cam.targetTexture = target; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
                cam.transform.position = new Vector3(2, 3, -.7f); cam.transform.rotation = Quaternion.Euler(90, 0, 0);
                var light = new GameObject("Sun").AddComponent<Light>(); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(60, 20, 0);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor, scene);
                floor.transform.position = new Vector3(2, -1, 0); floor.GetComponent<Renderer>().sharedMaterial = red;
                Color[] Capture()
                {
                    cam.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply(); return pixels.GetPixels();
                }
                reference = Capture();
                if (!withStep) return reference;
                var step = new GameObject("Step", typeof(MeshFilter), typeof(MeshRenderer)); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(step, scene);
                step.transform.localScale = Prefab.transform.localScale;
                step.GetComponent<MeshFilter>().sharedMesh = Prefab.GetComponent<Building>().GetStageMesh(10);
                step.GetComponent<MeshRenderer>().sharedMaterial = StepMaterial;
                return Capture();
            }
            finally
            {
                RenderTexture.active = oldActive; ShaderUtil.allowAsyncCompilation = asynchronous;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(red);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
