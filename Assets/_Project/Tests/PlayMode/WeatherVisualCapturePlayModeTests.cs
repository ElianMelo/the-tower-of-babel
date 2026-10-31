using System;
using System.Collections;
using System.IO;
using System.Linq;
using FishNet;
using NUnit.Framework;
using TowerOfBabel.Cheats;
using TowerOfBabel.Networking;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Networking.Weather;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>
    /// Visual review captures of the real game camera: interior, a Bubble Tube pillar and the tower from outside,
    /// by day and night, clear and raining. Explicit: run by name; images go to the disposable screenshot folder.
    /// </summary>
    [Explicit("Visual review capture; run by name.")]
    public sealed class WeatherVisualCapturePlayModeTests
    {
        const string Output = "_Screenshots-and-previews-to-delete/Weather/Game/";
        private UnityEngine.SceneManagement.Scene tower;
        private FishNet.Managing.NetworkManager manager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Project/Scenes/Tower.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            tower = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
#endif
            yield return null;
            manager = UnityEngine.Object.FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            if (!InstanceFinder.IsClientStarted) Assert.That(UnityEngine.Object.FindFirstObjectByType<NetworkBootstrap>().StartHost(), Is.True);
            yield return Wait(() => InstanceFinder.IsClientStarted && NetworkUpgradeService.Instance != null && NetworkWeatherService.Instance != null
                && NetworkUpgradeService.Instance.GetComponent<FishNet.Object.NetworkObject>().IsClientInitialized, "Host did not connect", 15);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null)
            {
                manager.ClientManager.StopConnection(); manager.ServerManager.StopConnection(true);
                UnityEngine.Object.Destroy(manager.gameObject);
            }
            yield return null;
            if (tower.IsValid() && tower.isLoaded) yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(tower);
        }

        [UnityTest]
        public IEnumerator Capture_InteriorPillarAndExterior_DayNightClearRain()
        {
            Directory.CreateDirectory(Output);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(tower); // the day/night cycle (and overcast) only runs in the active scene
            var chunks = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
            var upgrades = NetworkUpgradeService.Instance;
            var weather = NetworkWeatherService.Instance;
            var fx = UnityEngine.Object.FindFirstObjectByType<AeroTowerAtmosphere>();
            var player = UnityEngine.Object.FindFirstObjectByType<global::PlayerController>();
            var camera = Camera.main;
            Assert.That(chunks != null && fx != null && player != null && camera != null, Is.True);
            Assert.That(upgrades.RequestCheatCommand(CheatCommand.BuildTower), Is.True);
            yield return Wait(() => chunks.CachedChunks.All(c => c.Assets.All(a => a.Stage == 10)), "Tower did not build", 30);

            foreach (var behaviour in camera.GetComponents<Behaviour>()) if (behaviour.GetType().Name == "CinemachineBrain") behaviour.enabled = false;
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var log = new System.Text.StringBuilder();
            try
            {
                Vector3 center = new(fx.center.x, 0, fx.center.y);
                Vector3 radial = (new Vector3(player.transform.position.x, 0, player.transform.position.z) - center).normalized;
                if (radial.sqrMagnitude < .5f) radial = Vector3.back;
                Vector3 tangent = Vector3.Cross(Vector3.up, radial);
                float wall = fx.WallRadius(0);
                Vector3 inside = center + radial * (wall - 9) + Vector3.up * 1.1f;
                var pillar = chunks.CachedChunks.Where(c => c.Key.FloorIndex == 0).SelectMany(c => c.Assets)
                    .Where(a => a.AssetType == TowerAssetType.Pillar).OrderBy(a => (a.Position - inside).sqrMagnitude).First().Position;
                Vector3 pillarRadial = (new Vector3(pillar.x, 0, pillar.z) - center).normalized;

                var views = new (string name, Vector3 standAt, Vector3 eye, Vector3 look)[]
                {
                    ("Interior", inside, inside + Vector3.up * .95f, inside + tangent * 20 - radial * 4 + Vector3.up * 1.3f),
                    ("Pillar", pillar - pillarRadial * 2.6f + Vector3.up * 1.1f, pillar - pillarRadial * 2.4f + Vector3.up * 2f + tangent * .8f, pillar + Vector3.up * 2f),
                    ("Exterior", center + radial * (wall + 16) + Vector3.up * 1.1f, center + radial * (wall + 16) + Vector3.up * 2.2f, center + radial * wall + Vector3.up * 5),
                };
                foreach (var (time, timeName) in new[] { (.5f, "Day"), (.02f, "Night") })
                foreach (bool rain in new[] { false, true })
                {
                    weather.RequestTimeOfDay(time); weather.RequestFreezeTime(true); weather.RequestRain(rain);
                    foreach (var view in views)
                    {
                        Teleport(player, view.standAt);
                        float until = Time.time + (rain ? 3.6f : 1.4f);
                        while (Time.time < until) { camera.transform.position = view.eye; camera.transform.LookAt(view.look); yield return null; }
                        camera.transform.position = view.eye; camera.transform.LookAt(view.look);
                        camera.targetTexture = target; camera.Render(); camera.targetTexture = null;
                        var previous = RenderTexture.active; RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); RenderTexture.active = previous;
                        string name = $"{view.name}-{timeName}-{(rain ? "Rain" : "Clear")}";
                        File.WriteAllBytes(Output + name + ".png", image.EncodeToPNG());
                        float lum = image.GetPixels32().Where((_, i) => i % 41 == 0).Average(c => (c.r * .2126f + c.g * .7152f + c.b * .0722f) / 255f);
                        var rainController = AeroRainController.Instance;
                        log.AppendLine($"{name}: mean luminance {lum:F3}, drops {(rainController != null && rainController.Drops != null ? rainController.Drops.particleCount : 0)}, emitted/rejected last frame {rainController?.EmittedLastTick}/{rainController?.RejectedLastTick}");
                    }
                }
                TestContext.WriteLine(log.ToString());
                File.WriteAllText(Output + "Captures.txt", log.ToString());
            }
            finally
            {
                weather.RequestRain(false); weather.RequestFreezeTime(false); weather.RequestAutomaticWeather(true);
                UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image);
                upgrades.RequestCheatCommand(CheatCommand.ResetTower);
            }
            yield return Wait(() => chunks.CachedChunks.All(c => c.Assets.All(a => a.Stage == 0)), "Tower did not reset", 30);
        }

        private static void Teleport(Component player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = position;
            if (controller != null) controller.enabled = true;
        }

        private static IEnumerator Wait(Func<bool> condition, string message, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
