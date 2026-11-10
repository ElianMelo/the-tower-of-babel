#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using FishNet;
using NUnit.Framework;
using TowerOfBabel.Networking;
using TowerOfBabel.Performance;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>
    /// Visual review of a fresh start: where the player and camera are, the ground under them and whether the terrain
    /// draws. Writes _Screenshots-and-previews-to-delete/StartView/. Explicit: run on demand through the automated runner.
    /// </summary>
    [Explicit("Visual review; run on demand."), Category("Review")]
    public sealed class StartViewReviewPlayModeTests
    {
        const string Folder = "_Screenshots-and-previews-to-delete/StartView/";

        [UnityTest]
        public IEnumerator FreshStart_PlayerStandsOnVisibleGround()
        {
            var operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Project/Scenes/Tower.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return operation;
            var tower = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
            var manager = Object.FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            var log = new System.Text.StringBuilder();
            try
            {
                var bootstrap = Object.FindFirstObjectByType<NetworkBootstrap>();
                if (!InstanceFinder.IsClientStarted) Assert.That(bootstrap.StartHost(), Is.True);
                float deadline = Time.realtimeSinceStartup + 20;
                while (!InstanceFinder.IsClientStarted && Time.realtimeSinceStartup < deadline) yield return null;
                Directory.CreateDirectory(Folder);
                var terrain = Terrain.activeTerrains.FirstOrDefault(t => t.terrainData != null);
                foreach (float at in new[] { 0.5f, 3f, 6f })
                {
                    yield return TowerFrameBenchmark.Seconds(at < 1 ? at : 3f);
                    var player = Object.FindFirstObjectByType<PlayerController>();
                    var camera = Camera.main;
                    Vector3 p = player != null ? player.transform.position : Vector3.zero;
                    float ground = terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : float.NaN;
                    log.AppendLine($"t={Time.timeSinceLevelLoad:F1} player={(player != null ? p.ToString("F2") : "none")} ground={ground:F2} " +
                                   $"camera={(camera != null ? camera.transform.position.ToString("F2") + " fwd " + camera.transform.forward.ToString("F2") + " far " + camera.farClipPlane + " mask " + camera.cullingMask : "none")}");
                    foreach (var t in Terrain.activeTerrains)
                        log.AppendLine($"  terrain {t.name} enabled={t.enabled} active={t.gameObject.activeInHierarchy} heightmap={t.drawHeightmap} data={(t.terrainData != null)} layer={t.gameObject.layer} visibleToCamera={(camera != null && (camera.cullingMask & (1 << t.gameObject.layer)) != 0)}");
                    foreach (var c in Camera.allCameras)
                        log.AppendLine($"  camera {c.name} enabled={c.enabled} depth={c.depth} target={(c.targetTexture != null ? c.targetTexture.name : "SCREEN")} display={c.targetDisplay} " +
                                       $"pos={c.transform.position:F2} fwd={c.transform.forward:F2} mask={c.cullingMask} main={c == camera}");
                    if (camera != null)
                    {
                        var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
                        var previous = camera.targetTexture; var active = RenderTexture.active;
                        camera.targetTexture = target; camera.Render(); camera.Render();
                        RenderTexture.active = target;
                        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                        camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target);
                        File.WriteAllBytes($"{Folder}start-{at:F1}.png", image.EncodeToPNG());
                        Object.Destroy(image);
                        // The on-screen path (Game view backbuffer) as the player sees it.
                        yield return new WaitForEndOfFrame();
                        var screen = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes($"{Folder}screen-{at:F1}.png", screen.EncodeToPNG());
                        Object.Destroy(screen);
                    }
                }
                File.WriteAllText($"{Folder}start.txt", log.ToString());
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
