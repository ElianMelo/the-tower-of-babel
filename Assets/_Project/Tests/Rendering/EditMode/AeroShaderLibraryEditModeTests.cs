using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroShaderLibraryEditModeTests
    {
        const string Surface = "Assets/_Project/Shaders/AeroSurface.shader";
        const string Glass = "Assets/_Project/Shaders/AeroGlass.shader";
        const string Jelly = "Assets/_Project/Shaders/AeroJellyPlastic.shader";

        static void AssertCompiles(string path, params string[] keywords)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null, path);
            bool oldAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            var material = new Material(shader);
            try
            {
                foreach (var keyword in keywords) material.EnableKeyword(keyword);
                for (int pass = 0; pass < material.passCount; pass++) ShaderUtil.CompilePass(material, pass, true);
                var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error);
                Assert.That(errors.Select(e => e.file + ":" + e.line + " " + e.message), Is.Empty, path + " " + string.Join(" ", keywords));
                Assert.That(shader.isSupported, Is.True);
            }
            finally { Object.DestroyImmediate(material); ShaderUtil.allowAsyncCompilation = oldAsync; }
        }

        [Test]
        public void EveryAeroShaderAndFeatureVariantCompiles()
        {
            AssertCompiles(Surface);
            AssertCompiles(Surface, "_AERO_WETNESS", "_AERO_ANISO", "_AERO_IRIDESCENT", "_AERO_SSS", "_AERO_TRANSLUCENT", "_EMISSION");
            AssertCompiles(Surface, "_AERO_ANISO", "_ADDITIONAL_LIGHTS", "_CLUSTER_LIGHT_LOOP");
            AssertCompiles(Glass, "_AERO_WETNESS", "_ADDITIONAL_LIGHTS", "_CLUSTER_LIGHT_LOOP");
            AssertCompiles(Jelly, "_EMISSION", "_ADDITIONAL_LIGHTS");
            AssertCompiles("Assets/_Project/Shaders/BuildingAquaticFinish.shader", "_AERO_WETNESS");
            AssertCompiles("Assets/_Project/Shaders/GlassWaterFloor.shader", "_AERO_WETNESS");
            AssertCompiles("Assets/_Project/Shaders/AeroBushWind.shader", "_AERO_WETNESS", "_ADDITIONAL_LIGHTS");
            AssertCompiles("Assets/_Project/Shaders/AeroWorldBubble.shader");
            AssertCompiles("Assets/_Project/Shaders/RiverGlassSurface.shader", "_MAIN_LIGHT_SHADOWS_CASCADE");
        }

        [Test]
        public void UpgradedMaterials_UseTheAeroLibraryAndKeepTheirLookInputs()
        {
            Material Load(string path) { var m = AssetDatabase.LoadAssetAtPath<Material>(path); Assert.That(m, Is.Not.Null, path); return m; }
            const string resources = "Assets/_Project/Models/Resources/Materials/", lighting = "Assets/_Project/Environment/Lighting/";
            var steel = Load(resources + "Workshop_Steel.mat");
            Assert.That(steel.shader.name, Is.EqualTo("TowerOfBabel/Aero/Surface"));
            Assert.That(steel.IsKeywordEnabled("_AERO_ANISO"), Is.True, "Brushed steel is anisotropic.");
            var pearl = Load(resources + "Workshop_Pearl.mat");
            Assert.That(pearl.IsKeywordEnabled("_AERO_IRIDESCENT"), Is.True, "Pearl parts carry a thin film.");
            Assert.That(Load(resources + "Resource_Stone.mat").GetFloat("_Porosity"), Is.GreaterThan(.5f), "Stone soaks up rain.");
            Assert.That(Load(resources + "Workshop_Glass.mat").shader.name, Is.EqualTo("TowerOfBabel/Aero/Glass"));
            var glow = Load(lighting + "AquaLampGlow.mat");
            Assert.That(glow.shader.name, Is.EqualTo("TowerOfBabel/Aero/Jelly Plastic"));
            Assert.That(glow.IsKeywordEnabled("_EMISSION"), Is.True);
            Assert.That(glow.GetColor("_EmissionColor").maxColorComponent, Is.GreaterThan(1));
            Assert.That(Load(lighting + "AquaLampBlue.mat").shader.name, Is.EqualTo("TowerOfBabel/Aero/Jelly Plastic"));
            var ceramic = Load(resources + "Resource_Ceramic.mat");
            Assert.That(ceramic.GetTexture("_BaseMap"), Is.Not.Null, "URP/Lit textures survive the shader swap.");
            foreach (var marble in new[] { "Arch", "Floor_Tile", "Pillar", "Step_Tile" })
                Assert.That(Load("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_" + marble + ".mat").IsKeywordEnabled("_AERO_WETNESS"), Is.True);
            Assert.That(Load("Assets/_Project/Environment/Foliage/AeroBushLeaf.mat").IsKeywordEnabled("_AERO_WETNESS"), Is.True);
        }

        static Material SurfaceMaterial(AeroRenderHarness h, Color color, float metallic, float smoothness)
        {
            var m = h.Own(new Material(AssetDatabase.LoadAssetAtPath<Shader>(Surface)));
            m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        static void Toggle(Material m, string keyword, string property, bool on)
        {
            m.SetFloat(property, on ? 1 : 0);
            if (on) m.EnableKeyword(keyword); else m.DisableKeyword(keyword);
        }

        [Test]
        public void Iridescence_SpreadsHueAcrossTheSphere()
        {
            using var h = new AeroRenderHarness(128);
            var m = SurfaceMaterial(h, new Color(.9f, .92f, .95f), 0, .92f);
            h.Spawn(PrimitiveType.Sphere, m, Vector3.zero, Vector3.one * 2);
            h.Camera.transform.position = new Vector3(0, 0, -3.2f); h.Camera.transform.LookAt(Vector3.zero);
            float HueSpread(Color[] image)
            {
                var hues = image.Where(c => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) > .05f && AeroRenderHarness.Saturation(c) > .02f)
                    .Select(c => { Color.RGBToHSV(c, out float hue, out _, out _); return hue; }).ToArray();
                if (hues.Length < 10) return 0;
                float mean = hues.Average(); return Mathf.Sqrt(hues.Select(x => (x - mean) * (x - mean)).Average());
            }
            float plain = HueSpread(h.Render());
            Toggle(m, "_AERO_IRIDESCENT", "_IridescenceOn", true); m.SetFloat("_IridescenceStrength", 1);
            float film = HueSpread(h.Render());
            Assert.That(film, Is.GreaterThan(plain * 1.3f + .005f), "Thin-film interference must add a range of hues.");
        }

        [Test]
        public void Anisotropy_StretchesTheHighlightAlongTheBrush()
        {
            using var h = new AeroRenderHarness(128);
            var m = SurfaceMaterial(h, new Color(.8f, .82f, .85f), .9f, .8f);
            Toggle(m, "_AERO_ANISO", "_AnisoOn", true); m.SetFloat("_Anisotropy", .9f); m.SetFloat("_AnisoMode", 2);
            m.SetVector("_AnisoAxis", new Vector4(0, 1, 0, 0)); m.SetFloat("_SheenStrength", 0);
            h.Spawn(PrimitiveType.Sphere, m, Vector3.zero, Vector3.one * 2);
            h.Sun.transform.rotation = Quaternion.LookRotation(new Vector3(0, 0, 1));
            h.Camera.transform.position = new Vector3(0, 0, -3.2f); h.Camera.transform.LookAt(Vector3.zero);
            h.Camera.backgroundColor = Color.black;
            var image = h.Render();
            Assert.That(image.Count(c => float.IsNaN(c.r) || float.IsInfinity(c.r)), Is.Zero, "Anisotropic lighting must stay finite.");
            float peak = image.Max(c => c.grayscale);
            Assert.That(peak, Is.GreaterThan(.1f), "The brushed metal must catch the light.");
            int minX = 999, maxX = -1, minY = 999, maxY = -1;
            for (int y = 0; y < h.Size; y++) for (int x = 0; x < h.Size; x++)
                if (h.Pixel(image, x, y).grayscale > peak * .6f) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            float width = maxX - minX + 1, height = maxY - minY + 1;
            Assert.That(Mathf.Max(width, height) / Mathf.Max(1, Mathf.Min(width, height)), Is.GreaterThan(2f),
                "A brushed highlight must be a streak, not a round dot.");
        }

        [Test]
        public void SubsurfaceAndTranslucency_LightTheShadowedSide()
        {
            using var h = new AeroRenderHarness(96);
            var m = SurfaceMaterial(h, new Color(.45f, .8f, .2f), 0, .3f);
            h.Spawn(PrimitiveType.Sphere, m, Vector3.zero, Vector3.one * 2);
            h.Camera.transform.position = new Vector3(0, 0, -3.2f); h.Camera.transform.LookAt(Vector3.zero);
            // Backlit: the sun shines toward the camera.
            h.Sun.transform.rotation = Quaternion.LookRotation(new Vector3(0, -.15f, -1));
            float Mean() => h.Render().Skip(h.Size * h.Size / 4).Take(h.Size * h.Size / 2).Average(c => c.grayscale);
            float plain = Mean();
            Toggle(m, "_AERO_SSS", "_SubsurfaceOn", true); Toggle(m, "_AERO_TRANSLUCENT", "_TranslucentOn", true);
            m.SetFloat("_Thickness", .1f); m.SetFloat("_TranslucencyStrength", 1.5f);
            float scattered = Mean();
            Assert.That(scattered, Is.GreaterThan(plain * 1.15f + .005f), "Back light must pass through thin translucent material.");
        }

        [Test]
        public void Glass_RefractsTheSceneBehindIt()
        {
            using var h = new AeroRenderHarness(128);
            var checker = h.Own(new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat });
            checker.SetPixels(new[] { Color.white, Color.black, Color.black, Color.white }); checker.Apply();
            var back = h.Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
            back.SetTexture("_BaseMap", checker); back.SetTextureScale("_BaseMap", new Vector2(12, 12));
            h.Spawn(PrimitiveType.Quad, back, new Vector3(0, 0, 3), Vector3.one * 8);
            var glass = h.Own(new Material(AssetDatabase.LoadAssetAtPath<Shader>(Glass)));
            glass.SetFloat("_Refraction", 0); glass.SetFloat("_Dispersion", 0);
            h.Spawn(PrimitiveType.Sphere, glass, Vector3.zero, Vector3.one * 2);
            h.Camera.transform.position = new Vector3(0, 0, -3.2f); h.Camera.transform.LookAt(Vector3.zero);
            var straight = h.Render();
            glass.SetFloat("_Refraction", 2); glass.SetFloat("_Thickness", .3f);
            var bent = h.Render();
            int changed = straight.Where((c, i) => Mathf.Abs(c.grayscale - bent[i].grayscale) > .2f).Count();
            Assert.That(changed, Is.GreaterThan(100), "The checker behind the sphere must shift through the glass.");
        }

        [Test]
        public void JellyPlastic_CoreIsMoreSaturatedThanItsRim()
        {
            using var h = new AeroRenderHarness(128);
            var m = h.Own(new Material(AssetDatabase.LoadAssetAtPath<Shader>(Jelly)));
            m.SetColor("_BaseColor", new Color(.05f, .45f, .85f)); m.SetColor("_RimColor", new Color(.7f, .92f, 1));
            h.Spawn(PrimitiveType.Sphere, m, Vector3.zero, Vector3.one * 2);
            h.Camera.transform.position = new Vector3(0, 0, -3.2f); h.Camera.transform.LookAt(Vector3.zero);
            h.Sun.transform.rotation = Quaternion.Euler(30, 0, 0);
            var image = h.Render();
            var screenCenter = h.Camera.WorldToScreenPoint(Vector3.zero);
            var screenEdge = h.Camera.WorldToScreenPoint(new Vector3(.93f, 0, -.2f));
            Color core = h.Pixel(image, Mathf.RoundToInt(screenCenter.x), Mathf.RoundToInt(screenCenter.y));
            Color rim = h.Pixel(image, Mathf.RoundToInt(screenEdge.x), Mathf.RoundToInt(screenEdge.y));
            Assert.That(AeroRenderHarness.Saturation(core), Is.GreaterThan(AeroRenderHarness.Saturation(rim)),
                "Clear tinted plastic reads deepest where the view crosses the most material.");
        }

        [Test]
        public void InstancedAeroSurface_RendersThroughRenderMeshInstanced()
        {
            using var h = new AeroRenderHarness(64);
            var m = SurfaceMaterial(h, new Color(.2f, .6f, 1), 0, .6f); m.enableInstancing = true;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); var mesh = go.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(go);
            h.Camera.transform.position = new Vector3(0, 0, -4); h.Camera.transform.LookAt(Vector3.zero);
            h.Camera.backgroundColor = Color.black;
            var parameters = new RenderParams(m) { camera = h.Camera, layer = AeroRenderHarness.Layer };
            var matrices = new[] { Matrix4x4.Translate(new Vector3(-1, 0, 0)), Matrix4x4.Translate(new Vector3(1, 0, 0)) };
            RenderPipelineManager.beginCameraRendering += Submit;
            try
            {
                var image = h.Render();
                Assert.That(image.Count(c => c.grayscale > .05f), Is.GreaterThan(200), "Far pages draw tower assets through RenderMeshInstanced.");
            }
            finally { RenderPipelineManager.beginCameraRendering -= Submit; }
            void Submit(ScriptableRenderContext _, Camera camera) { if (camera == h.Camera) Graphics.RenderMeshInstanced(parameters, mesh, 0, matrices); }
        }
    }
}
