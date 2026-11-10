#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using FishNet;
using NUnit.Framework;
using TowerOfBabel.Cheats;
using TowerOfBabel.Networking;
using TowerOfBabel.Performance;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>
    /// Visual review: the join between glass-river floor tiles and their floor tiles, near, mid and far from the player,
    /// seen from the floor below and from the deck above (z-fighting showed there).
    /// Writes _Screenshots-and-previews-to-delete/RiverSeam/. Explicit: run on demand through the automated runner.
    /// </summary>
    [Explicit("Visual review; run on demand."), Category("Review")]
    public sealed class RiverSeamReviewPlayModeTests
    {
        const string Folder = "_Screenshots-and-previews-to-delete/RiverSeam/";

        static Texture2D Render(Camera camera)
        {
            var target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render(); camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
            camera.targetTexture = previous; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            return image;
        }

        [UnityTest]
        public IEnumerator GlassRiverJoins_NearMidAndFar_FromBelowAndAbove()
        {
            var operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Project/Scenes/Tower.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return operation;
            var tower = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
            var manager = Object.FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            var menu = Object.FindFirstObjectByType<CheatMenuUI>();
            Behaviour[] brains = new Behaviour[0];
            try
            {
                var bootstrap = Object.FindFirstObjectByType<NetworkBootstrap>();
                if (!InstanceFinder.IsClientStarted) Assert.That(bootstrap.StartHost(), Is.True);
                float deadline = Time.realtimeSinceStartup + 20;
                while (!InstanceFinder.IsClientStarted && Time.realtimeSinceStartup < deadline) yield return null;
                for (int i = 0; i < 30; i++) yield return null;
                var chunks = Object.FindFirstObjectByType<ChunkManager>();
                menu.Show(); menu.SelectCategory("Construction");
                menu.ActionButtons["Instantly build tower"].onClick.Invoke();
                deadline = Time.realtimeSinceStartup + 30;
                while (!chunks.CachedChunks.All(c => c.Assets.All(a => a.Stage == 10)) && Time.realtimeSinceStartup < deadline) yield return null;
                menu.Show(); menu.SelectCategory("World"); menu.SetFloor(15);
                menu.ActionButtons["Teleport to selected floor"].onClick.Invoke();
                menu.Hide();
                yield return TowerFrameBenchmark.Seconds(4);

                var camera = Camera.main;
                brains = camera.GetComponents<Behaviour>().Where(b => b.GetType().Name == "CinemachineBrain" && b.enabled).ToArray();
                foreach (var brain in brains) brain.enabled = false;
                var follower = camera.GetComponent<PlayerFollower>(); if (follower != null) follower.enabled = false;
                Vector3 eye = camera.transform.position;
                // The reported seam shows from below: stand where the player stands and look up at the deck above.
                var player = Object.FindFirstObjectByType<PlayerController>();
                Assert.That(player, Is.Not.Null);
                eye = player.transform.position + Vector3.up * 1.7f;
                var rivers = chunks.CachedChunks.SelectMany(c => c.Assets)
                    .Where(a => a.AssetType == TowerAssetType.GlassWaterFloor && a.Position.y > eye.y && a.Position.y < eye.y + chunks.FloorHeight + 1).ToArray();
                Assert.That(rivers.Length, Is.GreaterThan(10), $"Glass river tiles on the deck above the player at {eye}.");
                float Flat(ChunkAssetData a) => Vector2.Distance(new Vector2(a.Position.x, a.Position.z), new Vector2(eye.x, eye.z));
                rivers = rivers.OrderBy(Flat).ToArray();
                var picks = new[] { ("near", rivers.First(a => Flat(a) > 2)),
                                    ("mid", rivers.First(a => Flat(a) > 12)),
                                    ("far", rivers.First(a => Flat(a) > 30)) };
                Directory.CreateDirectory(Folder);
                foreach (var (name, tile) in picks)
                {
                    Vector3 side = new Vector3(tile.Position.x - eye.x, 0, tile.Position.z - eye.z).normalized;
                    // From below: nearly under the tile at eye height, looking up at its underside. From above: over
                    // the tile's deck, looking down at the river/floor join.
                    var views = new (string label, Vector3 position)[]
                    {
                        ("below", new Vector3(tile.Position.x, eye.y, tile.Position.z) - side * 2.5f),
                        ("above", tile.Position + Vector3.up * 1.7f - side * 2.5f),
                        // Looking up across the underside at a grazing angle (the stepped-line and slab-outline report).
                        ("up", new Vector3(tile.Position.x, eye.y + .3f, tile.Position.z) - side * .5f)
                    };
                    foreach (var view in views)
                    {
                        camera.transform.position = view.position;
                        camera.transform.LookAt(view.label == "up" ? tile.Position + side * 4f : tile.Position);
                        yield return TowerFrameBenchmark.Seconds(.6f);
                        // Render this camera directly: a background Editor stops repainting the Game view, so a screen grab can be stale.
                        var image = Render(camera);
                        File.WriteAllBytes($"{Folder}{name}-{view.label}.png", image.EncodeToPNG());
                        Object.Destroy(image);
                        if (view.label != "up") continue;
                        // Which layer draws an artefact: the far instanced tower or the near glass tiles.
                        var far = Object.FindFirstObjectByType<InstancedFarChunkRenderer>();
                        var glassTiles = Object.FindObjectsByType<TowerOfBabel.Buildings.GlassWaterFloorLight>(FindObjectsSortMode.None)
                            .Select(g => g.GetComponent<Renderer>()).Where(r => r != null && r.enabled).ToArray();
                        var toggles = new (string label, System.Action<bool> set)[]
                        {
                            ("noFar", on => far.enabled = on),
                            ("noNearGlassTiles", on => { foreach (var r in glassTiles) r.enabled = on; })
                        };
                        foreach (var toggle in toggles)
                        {
                            toggle.set(false);
                            yield return TowerFrameBenchmark.Seconds(.4f);
                            var variant = Render(camera);
                            File.WriteAllBytes($"{Folder}{name}-{view.label}-{toggle.label}.png", variant.EncodeToPNG());
                            Object.Destroy(variant);
                            toggle.set(true);
                        }
                    }
                }
            }
            finally
            {
                foreach (var brain in brains) if (brain != null) brain.enabled = true;
                if (manager != null)
                {
                    manager.ClientManager.StopConnection();
                    manager.ServerManager.StopConnection(true);
                    Object.Destroy(manager.gameObject);
                }
            }
            yield return null;
            if (tower.IsValid() && tower.isLoaded) yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(tower);
        }
    }
}
#endif
