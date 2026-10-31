using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>Aurora Arch: fit between pillars, round opening, construction stages, ring closure and animated aurora shader.</summary>
    public sealed class AuroraArchEditModeTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Buildings/Arch.prefab";
        private const string ShaderName = "TowerOfBabel/Buildings/Aurora Arch";
        private const float Span = 2.0944f, Radius = .7472f, Spring = .14f, Rise = .62f, Crown = Spring + Rise;

        private static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        private static Material ArchMaterial => Prefab.GetComponent<MeshRenderer>().sharedMaterial;
        private static Mesh Complete => Prefab.GetComponent<Building>().GetStageMesh(10);

        [Test]
        public void PrefabUsesInstancedAuroraMaterial()
        {
            Assert.That(ArchMaterial.shader.name, Is.EqualTo(ShaderName));
            Assert.That(ArchMaterial.enableInstancing, Is.True, "Near pool and far batches render the arch instanced.");
            Assert.That(ShaderUtil.GetShaderMessages(ArchMaterial.shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), Is.Empty);
            foreach (string property in new[] { "_AuroraStrength", "_AuroraRayDensity", "_AuroraRayDrift", "_AuroraFlicker", "_AuroraFolds",
                         "_AuroraSurge", "_AuroraBase", "_TowerCenter", "_MotionTime", "_BuildMode", "_BuildReveal", "_BuildClock" })
                Assert.That(ArchMaterial.HasProperty(property), property);
        }

        [Test]
        public void AllElevenStagesAreDistinctAndColliderUsesCompletedArch()
        {
            var building = Prefab.GetComponent<Building>();
            var meshes = Enumerable.Range(0, 11).Select(i => building.GetStageMesh((byte)i)).ToArray();
            Assert.That(meshes, Has.All.Not.Null);
            Assert.That(meshes.Distinct().Count(), Is.EqualTo(11));
            Assert.That(meshes.Select(m => m.subMeshCount), Has.All.EqualTo(1));
            Assert.That(meshes.Select(m => m.uv2.Length), Has.All.GreaterThan(0));
            Assert.That(meshes[10].name, Is.EqualTo("Arch_AuroraCurtain"));
            Assert.That(Prefab.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(meshes[0]));
            var collider = Prefab.GetComponent<MeshCollider>();
            Assert.That(collider.sharedMesh, Is.SameAs(meshes[10]));
            Assert.That(collider.convex, Is.False, "A convex hull would close the opening.");
        }

        [Test]
        public void ArchSpansExactlyPillarAxisToPillarAxis()
        {
            Bounds b = Complete.bounds;
            Assert.That(b.max.x, Is.EqualTo(Span).Within(1e-3f), "Ends on the next pillar's axis; it no longer overlaps the next arch.");
            Assert.That(b.min.x, Is.EqualTo(-.2f).Within(1e-3f), "Only the joint cap sits behind the start axis.");
            Assert.That(b.min.y, Is.EqualTo(.14f).Within(1e-3f)); Assert.That(b.max.y, Is.EqualTo(2.15f).Within(1e-3f));
            Assert.That(b.extents.z, Is.EqualTo(.2f).Within(1e-3f));
            foreach (var v in Complete.vertices.Where(v => v.x < -1e-4f))
                Assert.That(new Vector2(v.x, v.z).magnitude, Is.LessThanOrEqualTo(.2f + 1e-3f), "Cap must stay within the pillar joint.");
            Assert.That(Complete.triangles.Length / 3, Is.LessThanOrEqualTo(1200), "Stays below the previous 1,680-triangle arch.");
        }

        [Test]
        public void OpeningIsALowRoundCurveRisingFromThePillarTops()
        {
            // 2026-10-31 sketch: no straight legs; a half-ellipse (0.747 m half-width, 0.62 m rise) starts on the pillar tops.
            var verts = Complete.vertices;
            Assert.That(verts.Select(Opening), Has.All.GreaterThan(-1e-3f), "No geometry inside the opening.");
            var intrados = verts.Where(v => Mathf.Abs(Opening(v)) < 1e-3f).ToArray();
            Assert.That(intrados.Length, Is.GreaterThan(40), "The curve is finely segmented and lies on the half-ellipse.");
            Assert.That(intrados.Max(v => v.y), Is.EqualTo(Crown).Within(1e-3f));
            Assert.That(intrados.Min(v => v.y), Is.EqualTo(Spring).Within(1e-3f), "The curve starts at the feet, without straight legs.");
            Assert.That(intrados.Where(v => v.y > Spring + .05f).Min(v => Mathf.Abs(v.x - Span / 2)), Is.LessThan(Radius), "The sides curve inward right away.");
            Assert.That(intrados.Min(v => v.x) + intrados.Max(v => v.x), Is.EqualTo(Span).Within(1e-3f), "Opening is centred between the pillars.");
        }

        [Test]
        public void InnerRimIsRoundedIntoBothFaces()
        {
            // At the crown the rim curls from the front face (z -0.2, 0.10 above the opening) down to the opening and back out.
            var crown = Complete.vertices.Where(v => Mathf.Abs(v.x - Span / 2) < 1e-3f && v.y < 1.45f && v.y > Crown - 1e-3f).ToArray();
            Assert.That(crown.Length, Is.GreaterThanOrEqualTo(10), "The rim profile has several steps.");
            foreach (var v in crown)
            {
                // Quarter-round: distance from the fillet centre line (0.1 above the opening, 0.1 inside the face) is the radius.
                float side = Mathf.Abs(v.z) > .1f ? Mathf.Abs(v.z) - .1f : 0;
                float lift = v.y - Crown;
                if (side > 0) Assert.That(new Vector2(side, .1f - lift).magnitude, Is.EqualTo(.1f).Within(2e-3f), $"Rim vertex {v} is off the round.");
                else Assert.That(lift, Is.EqualTo(0).Within(1e-3f));
            }
            Assert.That(crown.Any(v => Mathf.Abs(v.z) > .19f && Mathf.Abs(v.y - (Crown + .1f)) < 1e-3f), Is.True, "Rim meets the face tangentially.");
        }

        [Test]
        public void FrontAndBackFacesCarryTheAuroraRegion()
        {
            Mesh mesh = Complete; var normals = mesh.normals; var regions = mesh.uv2; var tris = mesh.triangles;
            int front = 0;
            for (int t = 0; t < tris.Length; t += 3)
            {
                int i = tris[t];
                int region = Mathf.FloorToInt(regions[i].x * 8);
                Vector3 n = (normals[tris[t]] + normals[tris[t + 1]] + normals[tris[t + 2]]).normalized;
                if (Mathf.Abs(n.z) > .9999f && mesh.vertices[i].x > 0) { Assert.That(region, Is.EqualTo(1)); front++; }
                else if (Mathf.Abs(n.y) > .99f) Assert.That(region, Is.EqualTo(0), "Top and feet are plain glossy white.");
            }
            Assert.That(front, Is.GreaterThan(20));
        }

        [Test]
        public void ChunkLibraryAddsEightPiecesPerStage()
        {
            var library = (PillarChunkLibrary)new SerializedObject(Prefab.GetComponent<PillarConstructionMotion>()).FindProperty("library").objectReferenceValue;
            Assert.That(AssetDatabase.GetAssetPath(library), Does.EndWith("AuroraArchChunks.asset"));
            Assert.That(library.Pieces.Length, Is.EqualTo(80));
            Assert.That(library.Pieces.GroupBy(p => p.Stage).Select(g => g.Count()), Has.All.EqualTo(8));
            Assert.That(library.Pieces.Select(p => p.Stage).Distinct().OrderBy(s => s), Is.EqualTo(Enumerable.Range(1, 10)));
        }

        [Test]
        public void EveryTowerRingClosesWithScaledArchesEndingOnTheNextPillar()
        {
            // Tower.unity settings: radius 96, pillar angle 1.25, 45 levels, radius shrinks by 3 every second level.
            float radius = 96, current = radius;
            for (int level = 0; level < 45; level++)
            {
                current -= level % 2 == 0 ? 3 : 0;
                float requested = 1.25f / (current / radius);
                int bays = TowerGenerator.PillarsPerRing(requested);
                float step = 2 * Mathf.PI / bays;
                Assert.That(Mathf.Abs(step * Mathf.Rad2Deg - requested) / requested, Is.LessThan(.01f), $"Level {level} keeps its spacing.");
                float scale = TowerGenerator.ArchChordScale(current, bays, radius, 1.25f);
                Assert.That(scale, Is.EqualTo(1).Within(.01f), $"Level {level} arch barely stretches.");
                Vector2 a = new Vector2(current, 0), b = new Vector2(Mathf.Cos(step), Mathf.Sin(step)) * current;
                Assert.That(Span * scale, Is.EqualTo(Vector2.Distance(a, b)).Within(1e-3f), $"Level {level} arch ends on the next pillar axis.");
            }
            Assert.That(TowerGenerator.PillarsPerRing(1.25f), Is.EqualTo(288));
        }

        [Test]
        public void SavedTowerHasNoSeamDuplicatesAndArchesMeetTheNextPillar()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Tower.unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var g = roots.SelectMany(r => r.GetComponentsInChildren<TowerGenerator>(true)).Single();
                var manager = roots.SelectMany(r => r.GetComponentsInChildren<TowerOfBabel.World.Chunks.ChunkManager>(true)).Single();
                var all = manager.CachedChunks.SelectMany(c => c.Assets).ToArray();
                for (int level = 0; level < g.levels; level++)
                {
                    var layout = g.RingLayout(level).ToArray();
                    var pillars = all.Where(a => a.AssetType == TowerOfBabel.World.Tower.TowerAssetType.Pillar && Mathf.Abs(a.Position.y - level * g.heighIncrease) < .01f).ToArray();
                    var arches = all.Where(a => a.AssetType == TowerOfBabel.World.Tower.TowerAssetType.Arch && Mathf.Abs(a.Position.y - level * g.heighIncrease - g.yOffset) < .01f).ToArray();
                    Assert.That(pillars.Length, Is.EqualTo(layout.Length), $"Level {level} pillars");
                    Assert.That(arches.Length, Is.EqualTo(layout.Length), $"Level {level} arches");
                    // Each arch's far end lands on another pillar's axis: no overlap, no gap, no duplicate at the seam.
                    foreach (var arch in arches)
                    {
                        Vector3 end = arch.ObjectToWorld.MultiplyPoint3x4(new Vector3(Span, 0, 0));
                        float nearest = pillars.Min(p => Vector2.Distance(new Vector2(p.Position.x, p.Position.z), new Vector2(end.x, end.z)));
                        Assert.That(nearest, Is.LessThan(.005f), $"Level {level} arch at {arch.Position} ends off a pillar axis.");
                    }
                    float closest = pillars.SelectMany((p, i) => pillars.Skip(i + 1).Select(q => Vector3.Distance(p.Position, q.Position))).DefaultIfEmpty(9).Min();
                    Assert.That(closest, Is.GreaterThan(1.5f), $"Level {level} has overlapping pillars.");
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void AuroraMovesOverTime()
        {
            // Use the busiest of several moments: the curtain is patchy, so one spot can sit in a blank stretch.
            float time = Enumerable.Range(0, 10).Select(k => k * 45f).OrderByDescending(k => Render(k, Band, night: true).Average(c => c.grayscale)).First();
            var early = Render(time, Band, night: true); var later = Render(time + 2.5f, Band, night: true);
            Assert.That(Difference(early, later), Is.GreaterThan(.01f), "Rays drift, flicker and the edge wanders.");
            Assert.That(Difference(Render(time, Band, night: true), early), Is.LessThan(.002f), "A fixed clock renders identically.");
        }

        [Test]
        public void CurtainGlowsAboveTheOpeningAndLeavesTheLegsWhite()
        {
            float time = Enumerable.Range(0, 10).Select(k => k * 45f).OrderByDescending(k => Render(k, Band, night: true).Average(c => c.grayscale)).First();
            var band = Render(time, Band, night: true); var leg = Render(time, Leg, night: true);
            float bandLight = band.Average(c => c.grayscale), legLight = leg.Average(c => c.grayscale);
            Assert.That(bandLight, Is.GreaterThan(legLight + .05f), "The curtain glows at night where it is present.");
            Assert.That(band.Average(c => Mathf.Max(c.g, c.b) - c.r), Is.GreaterThan(.05f), "Aurora colours are cool: green, teal and blue.");
            Assert.That(Difference(Render(2f, Leg), Render(5f, Leg)), Is.LessThan(.004f), "No aurora on the legs below the curtain.");
        }

        [Test]
        public void CurtainLeavesBlankWhiteStretches()
        {
            // The same spot over a long span of time sees the patches drift past: sometimes aurora, sometimes plain white.
            float leg = Render(0, Leg, night: true).Average(c => c.grayscale);
            var light = Enumerable.Range(0, 24).Select(k => Render(k * 40f, Band, night: true).Average(c => c.grayscale) - leg).ToArray();
            Assert.That(light.Count(v => v < .02f), Is.GreaterThanOrEqualTo(3), "Some of the time the wall is plain white.");
            Assert.That(light.Count(v => v > .05f), Is.GreaterThanOrEqualTo(3), "Some of the time the aurora is there.");
        }

        [Test]
        public void CurtainBlendsGreenTealAndVioletAlongTheRing()
        {
            // Sample the curtain at several places and moments at night (it is patchy, so many samples are blank).
            var pixels = Enumerable.Range(0, 16).SelectMany(k => Render(2f + k * 53f, new Vector2(Span * (.1f + .05f * k), 1.68f), night: true)).ToArray();
            int green = pixels.Count(c => c.g > c.b + .04f && c.g > c.r + .1f);

            Assert.That(green, Is.GreaterThan(pixels.Length / 200), "Green from the user's aurora reference appears.");
            Assert.That(pixels.Count(c => c.b > c.g + .02f), Is.GreaterThan(0), "Blue or violet also appears.");
        }

        [Test]
        public void RaysHangBelowTheBaseToDifferentLengths()
        {
            // Lit columns end at different rows: a ragged fringe of ray tips, not a base line.
            var tips = BusyColumns().SelectMany(c => c.Select(col => (float)col.First())).ToArray();
            Assert.That(tips.Length, Is.GreaterThan(8), "Enough lit rays to compare.");
            Assert.That(Spread(tips), Is.GreaterThan(3f), "Ray tips end at varied heights below the curtain.");
        }

        [Test]
        public void RayLengthsAreRandom()
        {
            // Each lit column's streak length (number of lit rows) varies widely: short stubs beside long streaks.
            var lengths = BusyColumns().SelectMany(c => c.Select(col => (float)col.Length)).ToArray();
            Assert.That(lengths.Length, Is.GreaterThan(8), "Enough lit rays to compare.");
            Assert.That(Spread(lengths) / lengths.Average(), Is.GreaterThan(.3f), "Rays range from short stubs to long streaks.");
        }

        // The three busiest of sixteen moments, viewed 0.85 m tall around the curtain; per frame, the lit rows of each lit column.
        private static int[][][] BusyColumns()
        {
            var focus = new Vector2(Span * .5f, 1.8f);
            return Enumerable.Range(0, 16).Select(k => k * 37f)
                .Select(t => Render(t, focus, night: true, fieldOfView: 22))
                .OrderByDescending(p => p.Average(c => c.grayscale)).Take(3)
                .Select(p =>
                {
                    // Rows 0-78 are wall (the view's top shows sky); the plain wall is the 20th-percentile brightness.
                    var wall = Enumerable.Range(0, 79).SelectMany(y => Enumerable.Range(0, 96).Select(x => p[y * 96 + x].grayscale)).OrderBy(v => v).ToArray();
                    float floor = wall[wall.Length / 5], lit = floor + (wall[wall.Length - 1] - floor) * .3f;
                    return Enumerable.Range(0, 48).Select(i => i * 2)
                        .Select(x => Enumerable.Range(0, 79).Where(y => p[y * 96 + x].grayscale >= lit).ToArray())
                        .Where(rows => rows.Length > 2).ToArray();
                }).ToArray();
        }

        private static float Spread(float[] values)
        {
            float mean = values.Average();
            return Mathf.Sqrt(values.Average(v => (v - mean) * (v - mean)));
        }

        [Test]
        public void LowerEdgeIsWavyAndSoft()
        {
            // Brightness profile up one column: no step sharper than the soft fade, and the edge height changes along the ring.
            var column = Render(3f, new Vector2(Span * .5f, 1.6f), night: true);
            var rows = Enumerable.Range(0, 96).Select(y => Enumerable.Range(40, 16).Average(x => column[y * 96 + x].grayscale)).ToArray();
            float maxStep = Enumerable.Range(1, 95).Max(y => Mathf.Abs(rows[y] - rows[y - 1]));
            Assert.That(maxStep, Is.LessThan(.12f), "The base fades in instead of a hard line.");
            // A 0.85 m tall view (rows bottom to top) covers the whole edge range; the edge is where the column first reaches half its peak.
            float EdgeRow(Color[] p)
            {
                var r = Enumerable.Range(0, 96).Select(y => Enumerable.Range(44, 8).Average(x => p[y * 96 + x].grayscale)).ToArray();
                float half = r.Min() + (r.Max() - r.Min()) * .5f;
                return Enumerable.Range(0, 96).First(y => r[y] >= half);
            }
            var heights = Enumerable.Range(0, 8).Select(k => EdgeRow(Render(3f, new Vector2(Span * (.1f + .11f * k), 1.6f), night: true, fieldOfView: 22))).ToArray();
            Assert.That(heights.Max() - heights.Min(), Is.GreaterThan(4), "The lower edge rises and falls along the curtain.");
        }

        [Test]
        public void CurtainIsContinuousThroughThePillarJoint()
        {
            // Two neighbouring arches on the ring; the strip just left and right of their joint should match.
            var pixels = Render(2f, Joint, night: true, pair: true);
            float left = Column(pixels, 46), right = Column(pixels, 50);
            Assert.That(Mathf.Abs(left - right), Is.LessThan(.12f));
            Assert.That(Mathf.Min(left, right), Is.GreaterThan(.15f), "The joint cap carries the curtain too.");
        }

        private static readonly Vector2 Band = new(Span * .5f, 1.72f), Leg = new(.15f, .45f), Joint = new(0, 1.62f);

        private static float Opening(Vector3 v)
        {
            // Ellipse level (0 on the curve, negative inside the opening); below the springing it is the straight side.
            Vector2 q = new(Mathf.Abs(v.x - Span / 2), v.y - Spring);
            return q.y < 0 ? q.x - Radius : new Vector2(q.x / Radius, q.y / Rise).magnitude - 1;
        }

        // Renders a 96x96 view centred on `focus` (arch object space) on a ring of radius 96 around the tower centre.
        private static Color[] Render(float time, Vector2 focus, bool night = false, bool pair = false, float fieldOfView = 8)
        {
            var material = new Material(ArchMaterial) { hideFlags = HideFlags.DontSave };
            material.SetFloat("_MotionTime", time); material.SetVector("_TowerCenter", Vector4.zero);
            material.DisableKeyword("_AERO_WETNESS");
            float daylight = Shader.GetGlobalFloat("_AeroDaylight"); Shader.SetGlobalFloat("_AeroDaylight", night ? 0 : 1);
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            var old = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            try
            {
                float step = 1.25f * Mathf.Deg2Rad;
                Transform first = null;
                for (int k = pair ? -1 : 0; k <= 0; k++)
                {
                    float angle = -Mathf.PI / 2 + k * step;
                    Vector3 a = new(Mathf.Cos(angle) * 96, 3.85f, Mathf.Sin(angle) * 96);
                    Vector3 b = new(Mathf.Cos(angle + step) * 96, 3.85f, Mathf.Sin(angle + step) * 96);
                    var arch = new GameObject("Arch", typeof(MeshFilter), typeof(MeshRenderer));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(arch, scene);
                    Vector3 dir = (b - a).normalized;
                    arch.transform.SetPositionAndRotation(a, Quaternion.Euler(0, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - 90, 0));
                    arch.GetComponent<MeshFilter>().sharedMesh = Complete;
                    arch.GetComponent<MeshRenderer>().sharedMaterial = material;
                    if (k == 0) first = arch.transform;
                }
                var lightObject = new GameObject("Light"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = night ? .1f : 1.2f;
                lightObject.transform.rotation = Quaternion.Euler(35, -30, 0);
                var cameraObject = new GameObject("Camera"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.fieldOfView = fieldOfView;
                camera.targetTexture = target;
                Vector3 point = first.TransformPoint(new Vector3(focus.x, focus.y, 0));
                camera.transform.position = point - first.forward * 2.2f;     // outside, facing the front (-Z) face
                camera.transform.LookAt(point);
                camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 96, 96), 0, 0); pixels.Apply();
                return pixels.GetPixels();
            }
            finally
            {
                Shader.SetGlobalFloat("_AeroDaylight", daylight);
                RenderTexture.active = old; ShaderUtil.allowAsyncCompilation = async;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(material);
            }
        }

        private static float Column(Color[] pixels, int x) =>
            Enumerable.Range(20, 56).Average(y => pixels[y * 96 + x].grayscale);

        private static float Difference(Color[] a, Color[] b) =>
            a.Zip(b, (x, y) => Mathf.Abs(x.r - y.r) + Mathf.Abs(x.g - y.g) + Mathf.Abs(x.b - y.b)).Average() / 3;
    }
}
