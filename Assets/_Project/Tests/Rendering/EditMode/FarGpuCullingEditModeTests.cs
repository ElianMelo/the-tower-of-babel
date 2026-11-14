using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>
    /// Performance pass (2026-11-12): the far tower is culled per instance on the GPU (frustum, draw distance, deck
    /// occlusion, mesh level) and drawn with one indirect command per mesh level. It must show exactly what the
    /// per-page paths show: culling may only remove what cannot be seen.
    /// </summary>
    public sealed class FarGpuCullingEditModeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const int Layer = 31;

        // One far renderer drawing into a private layer of the open scene (RenderMesh* does not reach preview scenes).
        private sealed class Fixture : IDisposable
        {
            public readonly Camera Camera;
            public readonly ChunkManager Manager;
            public InstancedFarChunkRenderer Far;
            public readonly int Size;
            private readonly GameObject root, cameraObject;
            private readonly RenderTexture target;
            private readonly Texture2D pixels;
            private readonly bool culling, resident, lod, occlusion, asyncCompile;

            public Fixture(int size = 128)
            {
                Size = size;
                culling = InstancedFarChunkRenderer.GpuCulling; resident = InstancedFarChunkRenderer.GpuResidentInstancing;
                lod = InstancedFarChunkRenderer.MeshLodEnabled; occlusion = InstancedFarChunkRenderer.OcclusionCulling;
                asyncCompile = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
                cameraObject = new GameObject("Far culling camera") { hideFlags = HideFlags.DontSave };
                Camera = cameraObject.AddComponent<Camera>();
                Camera.enabled = false; Camera.cullingMask = 1 << Layer; Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = new Color(.2f, .25f, .3f); Camera.farClipPlane = 2000;
                // The open scene's baked occlusion data must not hide the reference objects of these tests.
                Camera.useOcclusionCulling = false;
                target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGBHalf); Camera.targetTexture = target;
                pixels = new Texture2D(size, size, TextureFormat.RGBAFloat, false);
                root = new GameObject("Far culling test") { layer = Layer };
                root.SetActive(false);
                Manager = root.AddComponent<ChunkManager>(); Manager.enabled = false;
            }

            public GameObject Prefab(TowerAssetType type, string name)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Buildings/{name}.prefab");
                Assert.That(prefab, Is.Not.Null, name);
                Manager.AssetPrefabs.SetPrefab(type, prefab);
                return prefab;
            }

            public void MakeRadial(Vector2 center)
            {
                typeof(ChunkManager).GetField("radialFloors", Private).SetValue(Manager, true);
                typeof(ChunkManager).GetField("floorCenter", Private).SetValue(Manager, center);
            }

            /// <summary>Call after the prefabs (and the floor layout) are set.</summary>
            public InstancedFarChunkRenderer CreateRenderer()
            {
                Far = root.AddComponent<InstancedFarChunkRenderer>();
                typeof(InstancedFarChunkRenderer).GetField("renderCamera", Private).SetValue(Far, Camera);
                typeof(InstancedFarChunkRenderer).GetField("useLightProbes", Private).SetValue(Far, false);
                typeof(InstancedFarChunkRenderer).GetField("chunkManager", Private).SetValue(Far, Manager);
                return Far;
            }

            public void Submit() => typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate", Private).Invoke(Far, null);

            public Color[] Render()
            {
                // RenderMesh* submissions last one camera render; submit before each of the two renders.
                Submit(); Camera.Render();
                Submit(); Camera.Render();
                var old = RenderTexture.active; RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); pixels.Apply();
                RenderTexture.active = old;
                return pixels.GetPixels();
            }

            public int Covered(Color[] image)
            {
                var background = Camera.backgroundColor.linear;
                return image.Count(c => Mathf.Abs(c.r - background.r) + Mathf.Abs(c.g - background.g) + Mathf.Abs(c.b - background.b) > .02f);
            }

            public void Dispose()
            {
                InstancedFarChunkRenderer.GpuCulling = culling; InstancedFarChunkRenderer.GpuResidentInstancing = resident;
                InstancedFarChunkRenderer.MeshLodEnabled = lod; InstancedFarChunkRenderer.OcclusionCulling = occlusion;
                ShaderUtil.allowAsyncCompilation = asyncCompile;
                Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        private static float MeanDifference(Color[] a, Color[] b, int covered)
        {
            float difference = 0;
            for (int i = 0; i < a.Length; i++)
                difference += Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
            return difference / (Mathf.Max(1, covered) * 3);
        }

        [TestCase(TowerAssetType.Pillar, "Pillar")]
        [TestCase(TowerAssetType.Arch, "Arch")]
        [TestCase(TowerAssetType.Floor, "Floor_Tile")]
        [TestCase(TowerAssetType.GlassWaterFloor, "GlassWaterFloor")]
        [TestCase(TowerAssetType.Stair, "Step_Tile")]
        [TestCase(TowerAssetType.Vine3, "Detail_Vine_3")]
        [TestCase(TowerAssetType.LavaLamp, "Detail_LavaLamp")]
        public void CulledDraw_MatchesTheInstancedDraw(TowerAssetType type, string prefabName)
        {
            Assume.That(InstancedFarChunkRenderer.GpuResidentSupported, "Needs compute buffers and indirect draws.");
            using var fixture = new Fixture();
            var prefab = fixture.Prefab(type, prefabName);
            var far = fixture.CreateRenderer();
            float size = prefab.GetComponent<MeshFilter>().sharedMesh.bounds.size.magnitude;
            var assets = new List<ChunkAssetData>();
            for (int i = 0; i < 3; i++)
                assets.Add(new ChunkAssetData(i, type, new Vector3((i - 1) * size * .8f, 0, 0),
                    Quaternion.Euler(0, i * 40, 0), Vector3.one, ChunkAssetData.CompletedStage));
            far.LoadChunk(new FarChunkSnapshot(new ChunkKey(0, 0, 0), assets));
            fixture.Camera.transform.position = new Vector3(0, size * .6f, -size * 2.6f);
            fixture.Camera.transform.LookAt(new Vector3(0, size * .25f, 0));

            InstancedFarChunkRenderer.GpuResidentInstancing = false;
            var instanced = fixture.Render();
            Assert.That(far.IsGpuCulled, Is.False);
            Assert.That(far.ResidentDrawCount, Is.Zero);
            InstancedFarChunkRenderer.GpuResidentInstancing = true; InstancedFarChunkRenderer.GpuCulling = true;
            var culled = fixture.Render();
            Assert.That(far.IsGpuCulled, Is.True, "The GPU-culled path was taken.");
            Assert.That(far.DrawCallCount, Is.EqualTo(1), "Three instances of one piece are one indirect draw.");
            Assert.That(far.ReadVisibleInstanceCounts(out _, out _, out _), Is.EqualTo(3), "All three are in view.");
            Assert.That(far.LoadedInstanceCount, Is.EqualTo(3));

            int a = fixture.Covered(instanced), b = fixture.Covered(culled);
            Assert.That(a, Is.GreaterThan(40), "The instanced reference draws the pieces.");
            Assert.That(b, Is.InRange(a * .97f, a * 1.03f), $"Same silhouette ({b} vs {a} pixels).");
            // Animated water, bubbles and leaves move a little between the two renders.
            Assert.That(MeanDifference(instanced, culled, a), Is.LessThan(.03f), "Same shading.");

            far.RemoveChunk(new ChunkKey(0, 0, 0));
            var empty = fixture.Render();
            Assert.That(far.LoadedInstanceCount, Is.Zero);
            Assert.That(fixture.Covered(empty), Is.Zero, "A removed chunk stops drawing at once.");
        }

        [Test]
        public void LampChrome_FarLitMatchesTheLitMaterial()
        {
            Assume.That(InstancedFarChunkRenderer.GpuResidentSupported, "Needs compute buffers and indirect draws.");
            using var fixture = new Fixture();
            var prefab = fixture.Prefab(TowerAssetType.LavaLamp, "Detail_LavaLamp");
            var source = prefab.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(source.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), "This test covers the Lit to Far Lit swap.");
            var far = fixture.CreateRenderer();
            // The far tower draws the finished stage.
            var finished = prefab.GetComponent<TowerOfBabel.Buildings.Building>().GetStageMesh(ChunkAssetData.CompletedStage);
            var bounds = finished.bounds;
            float size = bounds.size.magnitude;
            fixture.Camera.transform.position = bounds.center + new Vector3(size * .5f, size * .2f, -size * 1.4f);
            fixture.Camera.transform.LookAt(bounds.center);

            // Reference: the prefab's own mesh and Lit material on an ordinary renderer.
            var reference = new GameObject("Lamp reference") { layer = Layer, hideFlags = HideFlags.DontSave };
            Color[] expected;
            try
            {
                reference.AddComponent<MeshFilter>().sharedMesh = finished;
                var renderer = reference.AddComponent<MeshRenderer>(); renderer.sharedMaterial = source;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                expected = fixture.Render();
            }
            finally { Object.DestroyImmediate(reference); }

            far.LoadChunk(new FarChunkSnapshot(new ChunkKey(0, 0, 0), new List<ChunkAssetData>
                { new(0, TowerAssetType.LavaLamp, Vector3.zero, Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage) }));
            var actual = fixture.Render();
            Assert.That(far.IsGpuCulled, Is.True);
            Assert.That(far.ReadVisibleInstanceCounts(out _, out _, out _), Is.EqualTo(1), "The lamp chrome draws through the culled path.");
            int a = fixture.Covered(expected), b = fixture.Covered(actual);
            Assert.That(a, Is.GreaterThan(40), "The reference lamp is on screen.");
            Assert.That(b, Is.InRange(a * .98f, a * 1.02f), $"Same silhouette ({b} vs {a} pixels).");
            Assert.That(MeanDifference(expected, actual, a), Is.LessThan(.02f), "The far Lit shader shades like URP Lit.");
        }

        [Test]
        public void Culling_TestsEveryInstanceForFrustumDistanceAndMeshLevel()
        {
            Assume.That(InstancedFarChunkRenderer.GpuResidentSupported, "Needs compute buffers and indirect draws.");
            using var fixture = new Fixture(64);
            fixture.Prefab(TowerAssetType.Pillar, "Pillar"); fixture.Prefab(TowerAssetType.Vine3, "Detail_Vine_3");
            var far = fixture.CreateRenderer();
            var lods = new Mesh[InstancedFarChunkRenderer.LodSlots];
            for (int level = 1; level <= 2; level++)
                lods[InstancedFarChunkRenderer.LodSlot(TowerAssetType.Pillar, level)] = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/_Project/Models/Buildings/MeshLod/Pillar_LOD{level}.asset");
            far.ConfigureMeshLod(lods, 70, 140);
            fixture.Camera.transform.SetPositionAndRotation(new Vector3(0, 3, 0), Quaternion.identity);

            // One chunk holds pillars at every distance, and one behind the camera: a page would treat them as one.
            var assets = new List<ChunkAssetData>();
            float[] distances = { 30, 100, 200, -60 };
            for (int i = 0; i < distances.Length; i++)
                assets.Add(new ChunkAssetData(i, TowerAssetType.Pillar, new Vector3(0, 0, distances[i]), Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage));
            assets.Add(new ChunkAssetData(4, TowerAssetType.Vine3, new Vector3(4, 0, 50), Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage));
            assets.Add(new ChunkAssetData(5, TowerAssetType.Vine3, new Vector3(4, 0, 220), Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage));
            far.LoadChunk(new FarChunkSnapshot(new ChunkKey(0, 0, 0), assets));
            far.DetailDrawDistance = 140;

            fixture.Render();
            Assert.That(far.IsGpuCulled, Is.True);
            int total = far.ReadVisibleInstanceCounts(out int full, out int lod1, out int lod2);
            Assert.That(lod1, Is.EqualTo(1), "The pillar 100 m away draws LOD1.");
            Assert.That(lod2, Is.EqualTo(1), "The pillar 200 m away draws LOD2.");
            Assert.That(full, Is.EqualTo(2), "The pillar 30 m away and the vine 50 m away draw their full mesh.");
            Assert.That(total, Is.EqualTo(4), "The pillar behind the camera and the vine past the detail distance are not drawn.");

            InstancedFarChunkRenderer.MeshLodEnabled = false;
            fixture.Render();
            far.ReadVisibleInstanceCounts(out full, out lod1, out lod2);
            Assert.That(new[] { full, lod1, lod2 }, Is.EqualTo(new[] { 4, 0, 0 }), "Without mesh LOD every piece draws its full mesh.");
            InstancedFarChunkRenderer.MeshLodEnabled = true;

            far.DetailDrawDistance = 0;
            fixture.Render();
            Assert.That(far.ReadVisibleInstanceCounts(out _, out _, out _), Is.EqualTo(5), "No detail limit: the far vine draws too.");
            far.MaximumDrawDistance = 150;
            fixture.Render();
            Assert.That(far.ReadVisibleInstanceCounts(out _, out _, out _), Is.EqualTo(3), "The 200 m pillar and the 220 m vine are past the maximum distance.");
            far.MaximumDrawDistance = 0;
            far.LodDistanceScale = .25f;
            fixture.Render();
            far.ReadVisibleInstanceCounts(out full, out lod1, out lod2);
            Assert.That(lod2, Is.EqualTo(2), "A quarter of the LOD distances: 100 m and 200 m are both LOD2.");
            Assert.That(lod1, Is.EqualTo(1), "30 m is past the scaled LOD1 distance (17.5 m).");
        }

        // Glass steps blend, so two that overlap on screen show the order they were drawn in. The culled lists
        // were filled by racing GPU threads: the order changed from frame to frame and distant stairs flickered.
        [Test]
        public void GlassSteps_DrawInTheSameOrderEveryFrame_ChunkByChunk()
        {
            Assume.That(InstancedFarChunkRenderer.GpuResidentSupported, "Needs compute buffers and indirect draws.");
            using var fixture = new Fixture(64);
            fixture.Prefab(TowerAssetType.Stair, "Step_Tile"); fixture.Prefab(TowerAssetType.Pillar, "Pillar");
            var far = fixture.CreateRenderer();
            var lods = new Mesh[InstancedFarChunkRenderer.LodSlots];
            for (int level = 1; level <= 2; level++)
                lods[InstancedFarChunkRenderer.LodSlot(TowerAssetType.Stair, level)] = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/_Project/Models/Buildings/MeshLod/Stair_LOD{level}.asset");
            Assert.That(lods[InstancedFarChunkRenderer.LodSlot(TowerAssetType.Stair, 1)], Is.Not.Null);
            far.ConfigureMeshLod(lods, 70, 140);
            fixture.Camera.transform.SetPositionAndRotation(new Vector3(0, 6, -30), Quaternion.identity);

            // Steps on five floors, a near group and one 100 m further, with pillars sharing the instance buffers.
            const int Near = 24, Far = 6, Pillars = 10;
            List<ChunkAssetData> Floor(int floor)
            {
                var assets = new List<ChunkAssetData>();
                for (int i = 0; i < Near; i++)
                    assets.Add(new ChunkAssetData(assets.Count, TowerAssetType.Stair, new Vector3((i % 6 - 3) * 2.5f, floor * 2 + .5f, i / 6 * 2f), Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage));
                for (int i = 0; i < Pillars; i++)
                    assets.Add(new ChunkAssetData(assets.Count, TowerAssetType.Pillar, new Vector3((i - 5) * 2f, floor * 2, 12), Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage));
                for (int i = 0; i < Far; i++)
                    assets.Add(new ChunkAssetData(assets.Count, TowerAssetType.Stair, new Vector3((i - 3) * 2.5f, floor * 2 + .5f, 75), Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage));
                return assets;
            }
            // Chunks arrive out of order, as they do while the player moves.
            foreach (int floor in new[] { 3, 0, 4, 1, 2 })
                far.LoadChunk(new FarChunkSnapshot(new ChunkKey(floor, 0, 0), Floor(floor)));
            Assert.That(far.DrawsInFixedOrder(TowerAssetType.Stair), Is.True, "Glass steps blend.");
            Assert.That(far.DrawsInFixedOrder(TowerAssetType.Pillar), Is.False, "Opaque pieces are sorted by the depth buffer.");

            void AssertFloorByFloor(List<Vector3> centers, string what)
            {
                for (int i = 1; i < centers.Count; i++)
                    Assert.That(centers[i].y, Is.GreaterThanOrEqualTo(centers[i - 1].y - .001f), $"{what}: step {i} draws before a lower floor's step.");
            }

            fixture.Render();
            Assert.That(far.IsGpuCulled, Is.True);
            var full = far.ReadVisibleCenters(TowerAssetType.Stair);
            var simplified = far.ReadVisibleCenters(TowerAssetType.Stair, 1);
            Assert.That(full.Count, Is.EqualTo(5 * Near), "Every near step draws its full mesh.");
            Assert.That(simplified.Count, Is.EqualTo(5 * Far), "Every step 100 m away draws LOD1.");
            AssertFloorByFloor(full, "Full mesh"); AssertFloorByFloor(simplified, "LOD1");
            far.ReadVisibleInstanceCounts(out int fullCount, out int lod1Count, out _);
            Assert.That(fullCount, Is.EqualTo(5 * (Near + Pillars)), "The pillars draw as before.");
            Assert.That(lod1Count, Is.EqualTo(5 * Far));

            for (int frame = 0; frame < 4; frame++)
            {
                fixture.Render();
                Assert.That(far.ReadVisibleCenters(TowerAssetType.Stair), Is.EqualTo(full), "The same order every frame.");
                Assert.That(far.ReadVisibleCenters(TowerAssetType.Stair, 1), Is.EqualTo(simplified));
            }

            // A chunk that leaves and comes back (near and far hand-off) takes other buffer slots, not another place.
            far.RemoveChunk(new ChunkKey(1, 0, 0));
            fixture.Render();
            Assert.That(far.ReadVisibleCenters(TowerAssetType.Stair).Count, Is.EqualTo(4 * Near));
            far.LoadChunk(new FarChunkSnapshot(new ChunkKey(1, 0, 0), Floor(1)));
            fixture.Render();
            Assert.That(far.ReadVisibleCenters(TowerAssetType.Stair), Is.EqualTo(full));

            // Culling still works per step: past the near group only the far steps are in view.
            fixture.Camera.transform.position = new Vector3(0, 6, 40);
            fixture.Render();
            Assert.That(far.ReadVisibleCenters(TowerAssetType.Stair).Count, Is.EqualTo(5 * Far), "35 m away the far steps draw their full mesh.");
            Assert.That(far.ReadVisibleCenters(TowerAssetType.Stair, 1), Is.Empty);
            AssertFloorByFloor(far.ReadVisibleCenters(TowerAssetType.Stair), "Full mesh");
            fixture.Camera.transform.rotation = Quaternion.LookRotation(Vector3.left);
            fixture.Render();
            Assert.That(far.ReadVisibleCenters(TowerAssetType.Stair), Is.Empty, "Nothing is drawn when no step is in view.");
        }

        // The lamp lights use the deck maps on the CPU: the same test as the culling pass, on coarser blocks.
        [Test]
        public void DeckTestOnTheCpu_HidesOnlyWhatLiesBehindSolidDeck()
        {
            var set = new FarOccluderSet();
            set.Configure(true, Vector2.zero);
            set.SetChunk(new ChunkKey(2, 0, 0), Deck(Vector2.zero, 12, 24), 1, 0, false);
            set.Update(0);
            var onDeck = new Bounds(new Vector3(0, 15, 0), new Vector3(2, 4, 2));
            var underDeck = new Bounds(new Vector3(-2, 4, 6), new Vector3(2, 4, 2));
            Vector3 below = new(4, 0, -14), above = new(4, 44, -14);
            Assert.That(set.Hides(below, onDeck), Is.True, "From below, the deck hides what stands on it.");
            Assert.That(set.Hides(below, underDeck), Is.False);
            Assert.That(set.Hides(above, underDeck), Is.True, "From above, the deck hides what lies under it.");
            Assert.That(set.Hides(above, onDeck), Is.False);
            Assert.That(set.Hides(below, new Bounds(new Vector3(0, 12, 0), new Vector3(2, 4, 2))), Is.False, "A box that crosses the deck shows on this side.");
            Assert.That(set.Hides(new Vector3(60, 0, 0), new Bounds(new Vector3(22, 15, 0), new Vector3(2, 4, 2))), Is.False, "Seen past the rim of the deck.");
            Assert.That(set.Hides(new Vector3(0, 11.5f, 0), onDeck), Is.False, "A deck this close to the eye is not trusted.");

            // A stair hole: one tile missing. What shows through it is never hidden.
            var holed = Deck(Vector2.zero, 12, 24);
            int removed = holed.FindIndex(a => new Vector2(a.Position.x, a.Position.z).magnitude > 8 && new Vector2(a.Position.x, a.Position.z).magnitude < 14);
            // The middle of the missing tile: half its depth in from its outer edge, half its sweep along.
            var tile = holed[removed];
            float outer = new Vector2(tile.Position.x, tile.Position.z).magnitude, start = Mathf.Atan2(tile.Position.z, tile.Position.x);
            float middle = outer - tile.Scale.x * TowerFloorLayout.TemplateSize * .5f, turn = start + tile.Scale.z * TowerFloorLayout.TemplateSize / outer * .5f;
            var hole = new Vector3(Mathf.Cos(turn) * middle, 0, Mathf.Sin(turn) * middle);
            holed.RemoveAt(removed);
            set.SetChunk(new ChunkKey(2, 0, 0), holed, 2, 0, false);
            set.Update(0);
            Assert.That(set.Hides(new Vector3(hole.x, 0, hole.z), new Bounds(new Vector3(hole.x, 15, hole.z), new Vector3(1, 4, 1))), Is.False, "Seen through the hole.");
            Assert.That(set.Hides(below, onDeck), Is.True, "The rest of the deck still hides.");

            // Glass floors hide nothing.
            var glass = new FarOccluderSet();
            glass.Configure(true, Vector2.zero);
            glass.SetChunk(new ChunkKey(2, 0, 0), Deck(Vector2.zero, 12, 24, sector => sector.Radius < 14), 1, 0, false);
            glass.Update(0);
            Assert.That(glass.Hides(new Vector3(0, 1, -1), onDeck), Is.False);
        }

        // A deck of radial floor tiles 12 m up. From below, a pillar standing on it is hidden and one under it is
        // not; from above it is the other way round.
        private static List<ChunkAssetData> Deck(Vector2 center, float height, float radius, Predicate<TowerFloorLayout.Sector> glass = null)
        {
            var assets = new List<ChunkAssetData>();
            foreach (var sector in TowerFloorLayout.Build(center, height, radius, 3.9f, 3.351032f))
                assets.Add(new ChunkAssetData(assets.Count, glass != null && glass(sector) ? TowerAssetType.GlassWaterFloor : TowerAssetType.Floor,
                    sector.Position, sector.Rotation, sector.Scale, ChunkAssetData.CompletedStage));
            return assets;
        }

        [Test]
        public void Occlusion_RemovesOnlyWhatTheDeckHides()
        {
            Assume.That(InstancedFarChunkRenderer.GpuResidentSupported, "Needs compute buffers and indirect draws.");
            using var fixture = new Fixture(256);
            fixture.Prefab(TowerAssetType.Floor, "Floor_Tile"); fixture.Prefab(TowerAssetType.Pillar, "Pillar");
            fixture.MakeRadial(Vector2.zero);
            var far = fixture.CreateRenderer();
            var deck = Deck(Vector2.zero, 12, 24);
            var key = new ChunkKey(2, 0, 0);
            far.LoadChunk(new FarChunkSnapshot(key, deck));
            far.SetOccluderChunk(key, deck, 1);
            var pillars = new List<ChunkAssetData>
            {
                new(0, TowerAssetType.Pillar, new Vector3(0, 12, 0), Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage),   // On the deck.
                new(1, TowerAssetType.Pillar, new Vector3(-2, 2, 6), Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage)    // Under it.
            };
            far.LoadChunk(new FarChunkSnapshot(new ChunkKey(2, 5, 0), pillars));
            fixture.Camera.fieldOfView = 70;

            // Counts and pictures with and without occlusion from one viewpoint.
            (int hidden, int covered, int differing) Compare(Vector3 eye, Vector3 target)
            {
                fixture.Camera.transform.position = eye; fixture.Camera.transform.LookAt(target);
                InstancedFarChunkRenderer.OcclusionCulling = false;
                var open = fixture.Render();
                int everything = far.ReadVisibleInstanceCounts(out _, out _, out _);
                InstancedFarChunkRenderer.OcclusionCulling = true;
                var occluded = fixture.Render();
                int kept = far.ReadVisibleInstanceCounts(out _, out _, out _);
                int changed = 0;
                for (int i = 0; i < open.Length; i++)
                    if (Mathf.Abs(open[i].r - occluded[i].r) + Mathf.Abs(open[i].g - occluded[i].g) + Mathf.Abs(open[i].b - occluded[i].b) > .06f) changed++;
                return (everything - kept, fixture.Covered(open), changed);
            }

            var below = Compare(new Vector3(4, 0, -14), new Vector3(0, 14, 2));
            Assert.That(far.OccluderSolidTexels, Is.GreaterThan(10000), "The deck is mapped as solid.");
            Assert.That(below.hidden, Is.EqualTo(1), "From below, only the pillar standing on the deck is hidden.");
            Assert.That(below.covered, Is.GreaterThan(2000), "The deck fills the view.");
            Assert.That(below.differing, Is.LessThanOrEqualTo(below.covered / 200), $"Occlusion culling does not change the picture ({below.differing} of {below.covered} pixels).");

            var above = Compare(new Vector3(4, 44, -14), new Vector3(0, 12, 2));
            Assert.That(above.hidden, Is.EqualTo(1), "From above, only the pillar under the deck is hidden.");
            Assert.That(above.differing, Is.LessThanOrEqualTo(above.covered / 200), $"Occlusion culling does not change the picture ({above.differing} of {above.covered} pixels).");
        }

        [Test]
        public void Occlusion_NeverHidesWhatShowsThroughGlassOrAnUnbuiltTile()
        {
            Assume.That(InstancedFarChunkRenderer.GpuResidentSupported, "Needs compute buffers and indirect draws.");
            using var fixture = new Fixture(64);
            fixture.Prefab(TowerAssetType.Floor, "Floor_Tile"); fixture.Prefab(TowerAssetType.GlassWaterFloor, "GlassWaterFloor"); fixture.Prefab(TowerAssetType.Pillar, "Pillar");
            fixture.MakeRadial(Vector2.zero);
            var far = fixture.CreateRenderer();
            // The whole middle of the deck is river glass: the pillar on it shows through.
            var deck = Deck(Vector2.zero, 12, 24, sector => sector.Radius < 14);
            var key = new ChunkKey(2, 0, 0);
            far.LoadChunk(new FarChunkSnapshot(key, deck));
            far.SetOccluderChunk(key, deck, 1);
            far.LoadChunk(new FarChunkSnapshot(new ChunkKey(2, 5, 0), new List<ChunkAssetData>
                { new(0, TowerAssetType.Pillar, new Vector3(0, 12, 0), Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage) }));
            // Straight up from under the glass.
            fixture.Camera.transform.position = new Vector3(0, 1, -1);
            fixture.Camera.transform.LookAt(new Vector3(0, 15, 0));

            InstancedFarChunkRenderer.OcclusionCulling = false;
            fixture.Render();
            int everything = far.ReadVisibleInstanceCounts(out _, out _, out _);
            InstancedFarChunkRenderer.OcclusionCulling = true;
            fixture.Render();
            Assert.That(far.ReadVisibleInstanceCounts(out _, out _, out _), Is.EqualTo(everything), "Glass floors hide nothing.");

            // A hidden floor (null assets) stops occluding at once.
            far.SetOccluderChunk(key, null, 2);
            fixture.Submit();
            Assert.That(far.OccluderSolidTexels, Is.Zero);
        }

        [Test]
        public void DeckMaps_AreSolidAcrossTileJoinsAndNeverReachAHole()
        {
            var center = new Vector2(3, -2);
            const float radius = 30;
            // One ring of glass (not an occluder without a river layout) and a missing wedge (a stair opening).
            bool River(TowerFloorLayout.Sector s) => s.Radius > 17 && s.Radius < 21;
            bool Opening(TowerFloorLayout.Sector s) => s.Radius > 25 && s.Angle > 1f && s.Angle < 1.4f;
            var sectors = TowerFloorLayout.Build(center, 6, radius, 3.9f, 3.351032f).ToList();
            var assets = new List<ChunkAssetData>();
            foreach (var sector in sectors)
            {
                if (Opening(sector)) continue;
                assets.Add(new ChunkAssetData(assets.Count, River(sector) ? TowerAssetType.GlassWaterFloor : TowerAssetType.Floor,
                    sector.Position, sector.Rotation, sector.Scale, ChunkAssetData.CompletedStage));
            }
            var set = new FarOccluderSet();
            set.Configure(true, center);
            set.SetChunk(new ChunkKey(1, 0, 0), assets, 1, 0, false);
            Assert.That(set.Update(0), Is.True);
            Assert.That(set.TryGetFloor(1, out var map), Is.True);
            Assert.That(map.HasSolid, Is.True);
            Assert.That(map.PlaneY, Is.EqualTo(6 - FarOccluderSet.PlaneDepth).Within(.001f), "The map plane lies inside the 20 cm slab.");

            var solid = sectors.Where(s => !River(s) && !Opening(s)).ToList();
            bool InSolidTile(Vector2 point) => solid.Any(s => s.Contains(point, center));
            // Everything within this distance of the point is solid tile.
            bool DeepSolid(Vector2 point, float distance)
            {
                if (!InSolidTile(point)) return false;
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI / 8;
                    if (!InSolidTile(point + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * distance)) return false;
                    if (!InSolidTile(point + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * distance * .5f)) return false;
                }
                return true;
            }
            var random = new System.Random(7);
            int deepSamples = 0, missed = 0, holeSamples = 0, leaked = 0;
            float clearance = FarOccluderSet.SampleSpacing + map.TexelSize * 2 + FarOccluderSet.Margin + .1f;
            for (int i = 0; i < 20000; i++)
            {
                float r = Mathf.Sqrt((float)random.NextDouble()) * (radius + 2), angle = (float)random.NextDouble() * Mathf.PI * 2;
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                bool mapped = set.IsSolid(1, point);
                if (!InSolidTile(point)) { holeSamples++; if (mapped) leaked++; }
                else if (DeepSolid(point, clearance)) { deepSamples++; if (!mapped) missed++; }
            }
            Assert.That(deepSamples, Is.GreaterThan(5000)); Assert.That(holeSamples, Is.GreaterThan(1500));
            Assert.That(leaked, Is.Zero, "No solid texel holds a point of the glass ring, the stair opening or beyond the rim.");
            Assert.That(missed, Is.Zero, "Solid deck away from holes is mapped solid across every tile join.");

            // The summed-area table the GPU reads counts exactly the solid texels.
            var table = new float[FarOccluderSet.MapSize * FarOccluderSet.MapSize];
            set.FillSummedArea(map, table);
            Assert.That(table[table.Length - 1], Is.EqualTo(map.SolidTexels));
        }

        [Test]
        public void DeckMaps_CarveTheRiverChannelOutOfGlassTiles()
        {
            var layout = new Vector4(96, 3, 6, 1.3f);
            const int floor = 4;
            float wall = Mathf.Max(3, layout.x - layout.y * (Mathf.Floor(floor * .5f) + 1));
            var sectors = TowerFloorLayout.Build(Vector2.zero, floor * 6, wall, 3.9f, 3.351032f).ToList();
            var assets = new List<ChunkAssetData>();
            foreach (var sector in sectors)
            {
                // As in the tower: tiles the channel design touches are glass, the rest are stone.
                var middle = new Vector2(Mathf.Cos(sector.Angle + sector.Sweep * .5f), Mathf.Sin(sector.Angle + sector.Sweep * .5f)) * (sector.Radius - sector.Depth * .5f);
                bool glass = GlassWaterFloorLayout.IsChannel(middle, wall, 3.9f);
                assets.Add(new ChunkAssetData(assets.Count, glass ? TowerAssetType.GlassWaterFloor : TowerAssetType.Floor,
                    sector.Position, sector.Rotation, sector.Scale, ChunkAssetData.CompletedStage));
            }
            Assert.That(assets.Count(a => a.AssetType == TowerAssetType.GlassWaterFloor), Is.GreaterThan(50));
            var set = new FarOccluderSet();
            set.Configure(true, Vector2.zero, layout);
            set.SetChunk(new ChunkKey(floor, 0, 0), assets, 1, 0, false);
            set.Update(0);
            Assert.That(set.TryGetFloor(floor, out var map), Is.True);

            var random = new System.Random(11);
            int water = 0, leaked = 0, marble = 0, mapped = 0;
            for (int i = 0; i < 20000; i++)
            {
                float r = Mathf.Sqrt((float)random.NextDouble()) * wall, angle = (float)random.NextDouble() * Mathf.PI * 2;
                var point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                float channel = GlassWaterFloorLayout.ChannelDistance(point, floor, layout);
                bool isSolid = set.IsSolid(floor, point);
                if (channel < 0) { water++; if (isSolid) leaked++; }
                else if (channel > 1.5f && r < wall - 2) { marble++; if (isSolid) mapped++; }
            }
            Assert.That(water, Is.GreaterThan(1000)); Assert.That(marble, Is.GreaterThan(5000));
            Assert.That(leaked, Is.Zero, "The water channel (seen through glass) is never solid.");
            Assert.That(mapped, Is.EqualTo(marble), "Marble away from the channel is solid, on stone and glass tiles alike.");

            // Without the river layout the glass tiles are not occluders at all.
            set.Configure(true, Vector2.zero);
            set.SetChunk(new ChunkKey(floor, 0, 0), assets, 1, 0, false);
            set.Update(0);
            var onGlass = assets.First(a => a.AssetType == TowerAssetType.GlassWaterFloor);
            Assert.That(sectors.Where((s, i) => assets[i].AssetType == TowerAssetType.GlassWaterFloor)
                .All(s => !set.IsSolid(floor, new Vector2(Mathf.Cos(s.Angle + s.Sweep * .5f), Mathf.Sin(s.Angle + s.Sweep * .5f)) * (s.Radius - s.Depth * .5f))), Is.True);
            Assert.That(onGlass.Stage, Is.EqualTo(ChunkAssetData.CompletedStage));
        }

        [Test]
        public void DeckMaps_WaitWhileATileAssemblesAndWithdrawAtOnce()
        {
            var sectors = TowerFloorLayout.Build(Vector2.zero, 0, 12, 3.9f, 3.351032f).ToList();
            int last = 0; // An outer-ring tile.
            Vector2 lastMiddle = new Vector2(Mathf.Cos(sectors[last].Angle + sectors[last].Sweep * .5f), Mathf.Sin(sectors[last].Angle + sectors[last].Sweep * .5f)) * (sectors[last].Radius - sectors[last].Depth * .5f);
            List<ChunkAssetData> Assets(byte stage)
            {
                var list = new List<ChunkAssetData>();
                for (int i = 0; i < sectors.Count; i++)
                    list.Add(new ChunkAssetData(i, TowerAssetType.Floor, sectors[i].Position, sectors[i].Rotation, sectors[i].Scale,
                        i == last ? stage : ChunkAssetData.CompletedStage));
                return list;
            }
            var key = new ChunkKey(0, 0, 0);
            var set = new FarOccluderSet();
            set.Configure(true, Vector2.zero);
            // First sight of the chunk: everything already built occludes at once.
            set.SetChunk(key, Assets(9), 1, 100, true);
            set.Update(100);
            Assert.That(set.IsSolid(0, Vector2.zero + new Vector2(.5f, .5f)), Is.True);
            Assert.That(set.IsSolid(0, lastMiddle), Is.False, "A tile under construction is a hole.");
            int before = set.SolidTexelCount;

            // The tile reaches its final stage: it is still assembling on screen.
            set.SetChunk(key, Assets(10), 2, 100, true);
            set.Update(100.5f);
            Assert.That(set.IsSolid(0, lastMiddle), Is.False, "A tile that just completed does not occlude yet.");
            Assert.That(set.SolidTexelCount, Is.EqualTo(before));
            set.Update(100 + FarOccluderSet.ActivationDelay + .1f);
            Assert.That(set.IsSolid(0, lastMiddle), Is.True, "It occludes once its assembly has played.");

            // Rolled back: withdrawn in the same update, whatever the budget.
            set.SetChunk(key, Assets(3), 3, 200, true);
            set.Update(200, 0);
            Assert.That(set.IsSolid(0, lastMiddle), Is.False);
            set.SetChunk(key, null, 4, 200, true);
            set.Update(200, 0);
            Assert.That(set.SolidTexelCount, Is.Zero, "A hidden floor occludes nothing.");
        }

        [Test]
        public void TowerScene_DrawsEveryFarPieceThroughTheCulledPath()
        {
            Assume.That(InstancedFarChunkRenderer.GpuResidentSupported, "Needs compute buffers and indirect draws.");
            Assert.That(UnityEngine.Resources.Load<ComputeShader>(InstancedFarChunkRenderer.CullingShaderResource), Is.Not.Null);
            var farLit = UnityEngine.Resources.Load<Shader>(InstancedFarChunkRenderer.FarLitShaderResource);
            Assert.That(farLit, Is.Not.Null);
            Assert.That(farLit.keywordSpace.FindKeyword("PROCEDURAL_INSTANCING_ON").isValid, Is.True);
            // Every tower prefab's shader must have the procedural variant, or its pieces fall back to CPU pages.
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Buildings" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab.GetComponent<TowerAsset>() == null && prefab.GetComponent<TowerOfBabel.Buildings.Building>() == null) continue;
                var material = prefab.GetComponent<MeshRenderer>()?.sharedMaterial;
                if (material == null) continue;
                bool procedural = material.shader.keywordSpace.FindKeyword("PROCEDURAL_INSTANCING_ON").isValid;
                bool plainLit = material.shader.name == "Universal Render Pipeline/Lit" && material.shaderKeywords.Length == 0;
                Assert.That(procedural || plainLit, Is.True, $"{prefab.name} ({material.shader.name}) cannot draw through the GPU-culled far path.");
            }
        }
    }
}
