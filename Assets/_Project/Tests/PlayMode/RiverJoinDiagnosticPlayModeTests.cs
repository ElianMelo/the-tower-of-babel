#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using FishNet;
using NUnit.Framework;
using TowerOfBabel.Cheats;
using TowerOfBabel.Networking;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>
    /// Diagnostic (no captures): compares near glass-river tiles with the marble floor tiles beside them — height,
    /// scale, mesh and duplicates. Writes _Screenshots-and-previews-to-delete/RiverJoin/joins.txt. Explicit.
    /// </summary>
    [Explicit("Diagnostic; run on demand."), Category("Review")]
    public sealed class RiverJoinDiagnosticPlayModeTests
    {
        /// <summary>Names the outputs: which code state was measured.</summary>
        public static string Label = "matchedThickness";
        [UnityTest]
        public IEnumerator NearGlassTiles_MatchTheirMarbleNeighbours()
        {
            var operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Project/Scenes/Tower.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return operation;
            var tower = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
            var manager = Object.FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            var menu = Object.FindFirstObjectByType<CheatMenuUI>();
            var log = new System.Text.StringBuilder();
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
                yield return new WaitForSecondsRealtime(4);

                var player = Object.FindFirstObjectByType<PlayerController>();
                float eye = player.transform.position.y + 1.7f;
                var tiles = Object.FindObjectsByType<TowerAsset>(FindObjectsSortMode.None)
                    .Where(t => t.isActiveAndEnabled && t.AssetType.IsFloor())
                    .Select(t => (asset: t, renderer: t.GetComponent<MeshRenderer>(), filter: t.GetComponent<MeshFilter>()))
                    .Where(t => t.renderer != null && t.renderer.enabled && t.filter != null && t.filter.sharedMesh != null)
                    .Where(t => t.asset.transform.position.y > eye && t.asset.transform.position.y < eye + chunks.FloorHeight + 1)
                    .ToArray();
                var glass = tiles.Where(t => t.asset.AssetType == TowerAssetType.GlassWaterFloor).ToArray();
                var marble = tiles.Where(t => t.asset.AssetType == TowerAssetType.Floor).ToArray();
                log.AppendLine($"deck above y~{eye:F2}: glass={glass.Length} marble={marble.Length}");
                // Duplicates: two active tiles at the same position.
                var dupes = tiles.GroupBy(t => Vector3Int.RoundToInt(t.asset.transform.position * 100)).Where(g => g.Count() > 1).ToArray();
                log.AppendLine($"duplicate positions: {dupes.Length}");
                foreach (var g in dupes.Take(5)) log.AppendLine("  " + string.Join(" | ", g.Select(t => $"{t.asset.AssetType} {t.asset.name} mesh={t.filter.sharedMesh.name}")));
                int shown = 0;
                foreach (var g in glass.OrderBy(t => Vector3.Distance(t.asset.transform.position, player.transform.position)))
                {
                    var neighbour = marble.OrderBy(m => Vector3.Distance(m.renderer.bounds.center, g.renderer.bounds.center)).FirstOrDefault();
                    if (neighbour.asset == null) continue;
                    float gap = Vector3.Distance(neighbour.renderer.bounds.center, g.renderer.bounds.center);
                    if (gap > 6) continue;
                    string Describe((TowerAsset asset, MeshRenderer renderer, MeshFilter filter) t)
                    {
                        var tr = t.asset.transform;
                        var mat = t.renderer.sharedMaterial;
                        return $"{t.asset.AssetType} pos={tr.position:F3} rot={tr.rotation.eulerAngles:F2} scale={tr.lossyScale:F4} mesh={t.filter.sharedMesh.name} " +
                               $"bounds.y=[{t.renderer.bounds.min.y:F4},{t.renderer.bounds.max.y:F4}] mat={(mat != null ? mat.name : "-")} radial={(mat != null && mat.HasFloat("_RadialFloor") ? mat.GetFloat("_RadialFloor") : -1)}";
                    }
                    log.AppendLine(Describe(g));
                    log.AppendLine("  next to " + Describe(neighbour));
                    if (++shown >= 12) break;
                }
                // Same checks for the stage and floor index each glass tile publishes to its shader.
                var block = new MaterialPropertyBlock();
                foreach (var g in glass.Take(12))
                {
                    g.renderer.GetPropertyBlock(block);
                    var building = g.asset.GetComponent<TowerOfBabel.Buildings.Building>();
                    log.AppendLine($"glass {g.asset.transform.position:F2}: _RiverFloorIndex={block.GetFloat("_RiverFloorIndex")} _RiverStage={block.GetFloat("_RiverStage")} " +
                                   $"bound={building?.IsBound} chunkFloor={(building != null && building.IsBound ? building.ChunkKey.FloorIndex : -99)} stage={building?.CurrentStage}");
                }
                Directory.CreateDirectory("_Screenshots-and-previews-to-delete/RiverJoin/");
                File.WriteAllText($"_Screenshots-and-previews-to-delete/RiverJoin/joins-{Label}.txt", log.ToString());

                // One capture looking up across the deck above (same framing as the reported screenshots).
                var camera = Camera.main;
                foreach (var b in camera.GetComponents<Behaviour>().Where(b => b.GetType().Name == "CinemachineBrain")) b.enabled = false;
                var follower = camera.GetComponent<PlayerFollower>(); if (follower != null) follower.enabled = false;
                Vector3 eyePoint = player.transform.position + Vector3.up * 1.7f;
                var rivers = chunks.CachedChunks.SelectMany(c => c.Assets)
                    .Where(a => a.AssetType == TowerAssetType.GlassWaterFloor && a.Position.y > eyePoint.y && a.Position.y < eyePoint.y + chunks.FloorHeight + 1)
                    .OrderBy(a => Vector2.Distance(new Vector2(a.Position.x, a.Position.z), new Vector2(eyePoint.x, eyePoint.z))).ToArray();
                var tile = rivers.First(a => Vector2.Distance(new Vector2(a.Position.x, a.Position.z), new Vector2(eyePoint.x, eyePoint.z)) > 2);
                Vector3 side = new Vector3(tile.Position.x - eyePoint.x, 0, tile.Position.z - eyePoint.z).normalized;
                camera.transform.position = new Vector3(tile.Position.x, eyePoint.y + .3f, tile.Position.z) - side * .5f;
                camera.transform.LookAt(tile.Position + side * 4f);
                yield return new WaitForSecondsRealtime(1);
                var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
                var previous = camera.targetTexture; var active = RenderTexture.active;
                camera.targetTexture = target; camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target);
                File.WriteAllBytes($"_Screenshots-and-previews-to-delete/RiverJoin/up-{Label}.png", image.EncodeToPNG());
                Object.Destroy(image);
            }
            finally
            {
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
