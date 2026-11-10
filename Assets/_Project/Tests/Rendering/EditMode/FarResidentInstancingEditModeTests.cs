using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>
    /// Performance pass (2026-11-03): the far tower draws from GPU-resident transforms with RenderMeshIndirect instead of
    /// re-sending every instance each frame. It must look exactly like the instanced path for every tower piece.
    /// </summary>
    public sealed class FarResidentInstancingEditModeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(TowerAssetType.Pillar, "Pillar")]
        [TestCase(TowerAssetType.Arch, "Arch")]
        [TestCase(TowerAssetType.Floor, "Floor_Tile")]
        [TestCase(TowerAssetType.GlassWaterFloor, "GlassWaterFloor")]
        [TestCase(TowerAssetType.Stair, "Step_Tile")]
        [TestCase(TowerAssetType.Vine3, "Detail_Vine_3")]
        public void ResidentDraw_MatchesTheInstancedDraw(TowerAssetType type, string prefabName)
        {
            bool previous = InstancedFarChunkRenderer.GpuResidentInstancing;
            Assume.That(InstancedFarChunkRenderer.GpuResidentSupported, "Needs compute buffers and indirect draws.");
            // RenderMesh* draws do not reach preview-scene cameras, so this camera lives in the open scene and sees one
            // private layer only. Both paths render under the same lights; only their difference matters.
            const int Layer = 31, Size = 128;
            var cameraObject = new GameObject("Far resident camera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.cullingMask = 1 << Layer; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.2f, .25f, .3f);
            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGBHalf); camera.targetTexture = target;
            var pixels = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false);
            bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            var root = new GameObject("Far resident test") { layer = Layer };
            root.SetActive(false);
            try
            {
                var manager = root.AddComponent<ChunkManager>(); manager.enabled = false;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Buildings/{prefabName}.prefab");
                manager.AssetPrefabs.SetPrefab(type, prefab);
                var far = root.AddComponent<InstancedFarChunkRenderer>();
                typeof(InstancedFarChunkRenderer).GetField("renderCamera", Private).SetValue(far, camera);
                typeof(InstancedFarChunkRenderer).GetField("useLightProbes", Private).SetValue(far, false);
                var size = prefab.GetComponent<MeshFilter>().sharedMesh.bounds.size.magnitude;
                var assets = new List<ChunkAssetData>();
                for (int i = 0; i < 3; i++)
                    assets.Add(new ChunkAssetData(i, type, new Vector3((i - 1) * size * .8f, 0, 0),
                        Quaternion.Euler(0, i * 40, 0), Vector3.one, ChunkAssetData.CompletedStage));
                far.LoadChunk(new FarChunkSnapshot(new ChunkKey(0, 0, 0), assets));
                camera.transform.position = new Vector3(0, size * .6f, -size * 2.6f);
                camera.transform.LookAt(new Vector3(0, size * .25f, 0));

                Color[] Draw(bool resident)
                {
                    InstancedFarChunkRenderer.GpuResidentInstancing = resident;
                    // RenderMesh* submissions last one camera render; submit before each of the harness's two renders.
                    typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate", Private).Invoke(far, null);
                    camera.Render();
                    typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate", Private).Invoke(far, null);
                    camera.Render();
                    var old = RenderTexture.active; RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); pixels.Apply();
                    RenderTexture.active = old;
                    return pixels.GetPixels();
                }
                var instanced = Draw(false);
                Assert.That(far.ResidentDrawCount, Is.Zero);
                var resident = Draw(true);
                Assert.That(far.ResidentDrawCount, Is.GreaterThan(0), "The resident path was taken.");

                var background = camera.backgroundColor.linear;
                bool Covered(Color c) => Mathf.Abs(c.r - background.r) + Mathf.Abs(c.g - background.g) + Mathf.Abs(c.b - background.b) > .02f;
                int a = instanced.Count(Covered), b = resident.Count(Covered);
                Assert.That(a, Is.GreaterThan(40), "The instanced reference draws the pieces.");
                Assert.That(b, Is.InRange(a * .97f, a * 1.03f), $"Same silhouette ({b} vs {a} pixels).");
                float difference = 0;
                for (int i = 0; i < instanced.Length; i++)
                    difference += Mathf.Abs(instanced[i].r - resident[i].r) + Mathf.Abs(instanced[i].g - resident[i].g) + Mathf.Abs(instanced[i].b - resident[i].b);
                difference /= Mathf.Max(1, a) * 3;
                // Animated water, bubbles and leaves move a little between the two renders.
                Assert.That(difference, Is.LessThan(.03f), $"Same shading (mean difference {difference:F4} per covered channel).");
            }
            finally
            {
                InstancedFarChunkRenderer.GpuResidentInstancing = previous;
                ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [Test]
        public void ResidentRuns_MergeNeighbouringPagesIntoOneDrawAndLookTheSame()
        {
            bool previous = InstancedFarChunkRenderer.GpuResidentInstancing;
            Assume.That(InstancedFarChunkRenderer.GpuResidentSupported, "Needs compute buffers and indirect draws.");
            const int Layer = 31, Size = 256, Count = 1200;
            var cameraObject = new GameObject("Far run camera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.cullingMask = 1 << Layer; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.2f, .25f, .3f); camera.farClipPlane = 2000;
            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGBHalf); camera.targetTexture = target;
            var pixels = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false);
            bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            var root = new GameObject("Far run test") { layer = Layer };
            root.SetActive(false);
            try
            {
                var manager = root.AddComponent<ChunkManager>(); manager.enabled = false;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Pillar.prefab");
                manager.AssetPrefabs.SetPrefab(TowerAssetType.Pillar, prefab);
                var far = root.AddComponent<InstancedFarChunkRenderer>();
                typeof(InstancedFarChunkRenderer).GetField("renderCamera", Private).SetValue(far, camera);
                typeof(InstancedFarChunkRenderer).GetField("useLightProbes", Private).SetValue(far, false);
                float size = prefab.GetComponent<MeshFilter>().sharedMesh.bounds.size.magnitude;
                var assets = new List<ChunkAssetData>();
                for (int i = 0; i < Count; i++)
                    assets.Add(new ChunkAssetData(i, TowerAssetType.Pillar, new Vector3((i % 40 - 20) * size * 1.1f, 0, (i / 40) * size * 1.1f),
                        Quaternion.Euler(0, i * 17, 0), Vector3.one, ChunkAssetData.CompletedStage));
                far.LoadChunk(new FarChunkSnapshot(new ChunkKey(0, 0, 0), assets));
                camera.transform.position = new Vector3(0, size * 14, -size * 22);
                camera.transform.LookAt(new Vector3(0, 0, size * 16));

                Color[] Draw(bool resident)
                {
                    InstancedFarChunkRenderer.GpuResidentInstancing = resident;
                    typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate", Private).Invoke(far, null);
                    camera.Render();
                    typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate", Private).Invoke(far, null);
                    camera.Render();
                    var old = RenderTexture.active; RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); pixels.Apply();
                    RenderTexture.active = old;
                    return pixels.GetPixels();
                }
                var instanced = Draw(false);
                int pages = far.VisiblePageCount;
                Assert.That(pages, Is.EqualTo(3), "1200 pieces fill three 511-instance pages.");
                Assert.That(far.DrawCallCount, Is.EqualTo(3), "The instanced path draws one call per page.");
                var resident = Draw(true);
                Assert.That(far.VisiblePageCount, Is.EqualTo(pages));
                Assert.That(far.ResidentDrawCount, Is.EqualTo(1), "Neighbouring visible pages merge into one indirect draw.");
                Assert.That(far.DrawCallCount, Is.EqualTo(1));

                var background = camera.backgroundColor.linear;
                bool Covered(Color c) => Mathf.Abs(c.r - background.r) + Mathf.Abs(c.g - background.g) + Mathf.Abs(c.b - background.b) > .02f;
                int a = instanced.Count(Covered), b = resident.Count(Covered);
                Assert.That(a, Is.GreaterThan(500), "The instanced reference draws the pieces.");
                Assert.That(b, Is.InRange(a * .98f, a * 1.02f), $"Every page is drawn by the merged run ({b} vs {a} pixels).");
                float difference = 0;
                for (int i = 0; i < instanced.Length; i++)
                    difference += Mathf.Abs(instanced[i].r - resident[i].r) + Mathf.Abs(instanced[i].g - resident[i].g) + Mathf.Abs(instanced[i].b - resident[i].b);
                difference /= Mathf.Max(1, a) * 3;
                Assert.That(difference, Is.LessThan(.03f), $"Same shading (mean difference {difference:F4} per covered channel).");
            }
            finally
            {
                InstancedFarChunkRenderer.GpuResidentInstancing = previous;
                ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [TestCase(TowerAssetType.Pillar, "Pillar", 100f)]
        [TestCase(TowerAssetType.Pillar, "Pillar", 170f)]
        [TestCase(TowerAssetType.Arch, "Arch", 100f)]
        [TestCase(TowerAssetType.Arch, "Arch", 170f)]
        [TestCase(TowerAssetType.Floor, "Floor_Tile", 170f)]
        [TestCase(TowerAssetType.Stair, "Step_Tile", 170f)]
        public void MeshLod_IsInvisibleAtItsDistance(TowerAssetType type, string prefabName, float distance)
        {
            // Same pixel density as the game: 1080 px over a 60 degree view is 512 px over about 28 degrees.
            const int Layer = 31, Size = 512;
            bool previous = InstancedFarChunkRenderer.MeshLodEnabled;
            var cameraObject = new GameObject("Far LOD camera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.cullingMask = 1 << Layer; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.2f, .25f, .3f); camera.fieldOfView = 28.4f; camera.farClipPlane = 1000;
            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGBHalf); camera.targetTexture = target;
            var pixels = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false);
            bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            var root = new GameObject("Far LOD test") { layer = Layer };
            root.SetActive(false);
            try
            {
                var manager = root.AddComponent<ChunkManager>(); manager.enabled = false;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Buildings/{prefabName}.prefab");
                manager.AssetPrefabs.SetPrefab(type, prefab);
                var far = root.AddComponent<InstancedFarChunkRenderer>();
                typeof(InstancedFarChunkRenderer).GetField("renderCamera", Private).SetValue(far, camera);
                typeof(InstancedFarChunkRenderer).GetField("useLightProbes", Private).SetValue(far, false);
                var lods = new Mesh[InstancedFarChunkRenderer.LodSlots];
                for (int level = 1; level <= 2; level++)
                {
                    lods[InstancedFarChunkRenderer.LodSlot(type, level)] = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/_Project/Models/Buildings/MeshLod/{type}_LOD{level}.asset");
                    Assert.That(lods[InstancedFarChunkRenderer.LodSlot(type, level)], Is.Not.Null, "Run Tower of Babel/Performance/Build far mesh LODs.");
                }
                far.ConfigureMeshLod(lods, 70, 140);
                var bounds = prefab.GetComponent<MeshFilter>().sharedMesh.bounds;
                float size = bounds.size.magnitude;
                var assets = new List<ChunkAssetData>();
                for (int i = 0; i < 9; i++)
                    assets.Add(new ChunkAssetData(i, type, new Vector3((i % 3 - 1) * size * .9f, (i / 3) * bounds.size.y * 1.1f, distance),
                        Quaternion.Euler(0, i * 37, 0), Vector3.one, ChunkAssetData.CompletedStage));
                far.LoadChunk(new FarChunkSnapshot(new ChunkKey(0, 0, 0), assets));
                camera.transform.position = new Vector3(0, bounds.size.y * 1.6f, 0);
                camera.transform.LookAt(new Vector3(0, bounds.size.y * 1.1f, distance));

                Color[] Draw(bool lod)
                {
                    InstancedFarChunkRenderer.MeshLodEnabled = lod;
                    typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate", Private).Invoke(far, null);
                    camera.Render();
                    typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate", Private).Invoke(far, null);
                    camera.Render();
                    var old = RenderTexture.active; RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); pixels.Apply();
                    RenderTexture.active = old;
                    return pixels.GetPixels();
                }
                var full = Draw(false);
                Assert.That(far.LodPageCount, Is.Zero);
                var lodImage = Draw(true);
                Assert.That(far.LodPageCount, Is.GreaterThan(0), $"The page {distance} m away uses a simplified mesh.");

                var background = camera.backgroundColor.linear;
                bool Covered(Color c) => Mathf.Abs(c.r - background.r) + Mathf.Abs(c.g - background.g) + Mathf.Abs(c.b - background.b) > .02f;
                int a = full.Count(Covered), b = lodImage.Count(Covered);
                Assert.That(a, Is.GreaterThan(30), "The full meshes are on screen.");
                Assert.That(Mathf.Abs(b - a), Is.LessThanOrEqualTo(Mathf.Max(6, a * .04f)), $"Same silhouette within a few pixels ({b} vs {a}).");
                int changed = 0;
                for (int i = 0; i < full.Length; i++)
                    if (Mathf.Abs(full[i].grayscale - lodImage[i].grayscale) > .12f) changed++;
                Assert.That(changed, Is.LessThanOrEqualTo(Mathf.Max(6, a * .06f)), $"Only edge pixels may differ ({changed} of {a}).");
            }
            finally
            {
                InstancedFarChunkRenderer.MeshLodEnabled = previous;
                ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [Test]
        public void MeshLod_SwitchesOnlyPastItsDistances()
        {
            var root = new GameObject("Far LOD distances"); root.SetActive(false);
            var cameraObject = new GameObject("Far LOD distance camera") { hideFlags = HideFlags.DontSave };
            try
            {
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.farClipPlane = 1000;
                var manager = root.AddComponent<ChunkManager>(); manager.enabled = false;
                manager.AssetPrefabs.SetPrefab(TowerAssetType.Pillar, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Pillar.prefab"));
                var far = root.AddComponent<InstancedFarChunkRenderer>();
                typeof(InstancedFarChunkRenderer).GetField("renderCamera", Private).SetValue(far, camera);
                typeof(InstancedFarChunkRenderer).GetField("useLightProbes", Private).SetValue(far, false);
                var lods = new Mesh[InstancedFarChunkRenderer.LodSlots];
                for (int level = 1; level <= 2; level++)
                    lods[InstancedFarChunkRenderer.LodSlot(TowerAssetType.Pillar, level)] = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/_Project/Models/Buildings/MeshLod/Pillar_LOD{level}.asset");
                far.ConfigureMeshLod(lods, 70, 140);
                int key = 0;
                foreach (float z in new[] { 30f, 100f, 200f })
                    far.LoadChunk(new FarChunkSnapshot(new ChunkKey(0, 0, 10 + key++ * 10),
                        new List<ChunkAssetData> { new(0, TowerAssetType.Pillar, new Vector3(0, 0, z), Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage) }));
                typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate", Private).Invoke(far, null);
                Assert.That(far.VisiblePageCount, Is.EqualTo(3));
                Assert.That(far.LodPageCount, Is.EqualTo(2), "30 m keeps the full mesh; 100 m and 200 m are simplified.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void Rebuilds_AreSpreadOverFramesNewestAndNearestFirst()
        {
            var root = new GameObject("Far rebuild budget"); root.SetActive(false);
            var cameraObject = new GameObject("Far rebuild camera") { hideFlags = HideFlags.DontSave };
            try
            {
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.farClipPlane = 2000;
                var manager = root.AddComponent<ChunkManager>(); manager.enabled = false;
                manager.AssetPrefabs.SetPrefab(TowerAssetType.Pillar, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Pillar.prefab"));
                var far = root.AddComponent<InstancedFarChunkRenderer>();
                typeof(InstancedFarChunkRenderer).GetField("renderCamera", Private).SetValue(far, camera);
                typeof(InstancedFarChunkRenderer).GetField("useLightProbes", Private).SetValue(far, false);
                var lateUpdate = typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate", Private);
                ChunkKey Key(int i) => new ChunkKey(0, 0, i * 10);
                void Load(int i, float z) => far.LoadChunk(new FarChunkSnapshot(Key(i),
                    new List<ChunkAssetData> { new(0, TowerAssetType.Pillar, new Vector3(0, 0, z), Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage) }));

                // Four cells, all new: with no budget every cell builds in the first frame.
                far.RebuildBudgetMs = 0;
                for (int i = 0; i < 4; i++) Load(i, 40 + i * 60);
                lateUpdate.Invoke(far, null);
                Assert.That(far.RebuiltCellCount, Is.EqualTo(4)); Assert.That(far.PendingCellCount, Is.Zero);
                Assert.That(far.VisiblePageCount, Is.EqualTo(4));

                // Change all four with a budget too small for more than one rebuild per frame.
                far.RebuildBudgetMs = .0001f;
                for (int i = 0; i < 4; i++) Load(i, 40 + i * 60);
                lateUpdate.Invoke(far, null);
                Assert.That(far.RebuiltCellCount, Is.EqualTo(1), "At least one, but only one, rebuild fits the budget.");
                Assert.That(far.PendingCellCount, Is.EqualTo(3));
                Assert.That(far.VisiblePageCount, Is.EqualTo(4), "Cells waiting for a rebuild keep drawing their previous state.");
                for (int frame = 0; frame < 3; frame++) lateUpdate.Invoke(far, null);
                Assert.That(far.PendingCellCount, Is.Zero, "Every changed cell catches up within a few frames.");

                // A brand-new cell is rebuilt before an older, nearer one that only changed.
                Load(0, 40); Load(9, 400);
                lateUpdate.Invoke(far, null);
                Assert.That(far.RebuiltCellCount, Is.EqualTo(1));
                Assert.That(far.VisiblePageCount, Is.EqualTo(5), "The new, never-drawn cell went first.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void TowerScene_HasMeshLodsWired()
        {
            const string path = "Assets/_Project/Scenes/Tower.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var far = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InstancedFarChunkRenderer>(true)).Single();
                var meshes = (Mesh[])typeof(InstancedFarChunkRenderer).GetField("meshLods", Private).GetValue(far);
                Assert.That(meshes.Length, Is.EqualTo(InstancedFarChunkRenderer.LodSlots));
                Assert.That(meshes.All(m => m != null), "Two simplified meshes for each of the five structure types.");
                Assert.That(far.Lod1Distance, Is.GreaterThanOrEqualTo(60), "Close enough to see detail stays full mesh.");
                Assert.That(far.Lod2Distance, Is.GreaterThan(far.Lod1Distance));
            }
            finally { if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
