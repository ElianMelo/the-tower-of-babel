using System;
using System.Collections;
using System.Linq;
using FishNet;
using NUnit.Framework;
using TowerOfBabel.Cheats;
using TowerOfBabel.Networking;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Rendering;
using TowerOfBabel.Settings;
using TowerOfBabel.World.Chunks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>
    /// The settings menu in the real Tower scene with a local host: Escape opens and closes it, the old debug console
    /// key shows and hides the mouse cursor, settings reach the running game and remapped keys drive it.
    /// </summary>
    public sealed class SettingsMenuPlayModeTests
    {
        private SettingsMenuUI menu;
        private CheatMenuUI cheats;
        private Keyboard keyboard, previousKeyboard;
        private UnityEngine.SceneManagement.Scene tower;
        private FishNet.Managing.NetworkManager manager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
#if UNITY_EDITOR
            var operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Project/Scenes/Tower.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return operation;
            tower = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
#endif
            yield return null;
            menu = UnityEngine.Object.FindFirstObjectByType<SettingsMenuUI>();
            cheats = UnityEngine.Object.FindFirstObjectByType<CheatMenuUI>();
            Assert.That(menu, Is.Not.Null, "The Tower scene has the settings menu.");
            Assert.That(cheats, Is.Not.Null);
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<NetworkBootstrap>();
            manager = UnityEngine.Object.FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            if (!InstanceFinder.IsClientStarted) Assert.That(bootstrap.StartHost(), Is.True);
            yield return Wait(() => InstanceFinder.IsClientStarted && NetworkUpgradeService.Instance != null
                && NetworkUpgradeService.Instance.GetComponent<FishNet.Object.NetworkObject>().IsClientInitialized, "Host did not connect");
            var controls = UnityEngine.Object.FindFirstObjectByType<PlayerControlStateMachine>();
            yield return Wait(() => controls != null && controls.CurrentState == PlayerControlState.Moving, "The player never got control");
            previousKeyboard = Keyboard.current; keyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            GameInput.ResetBindings(false);
            if (menu != null) menu.Hide();
            if (cheats != null) cheats.Hide();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            keyboard = previousKeyboard = null;
            GameSettingsData data = GameSettings.Current;
            if (GameSettingsApplier.Instance != null)
            {
                GraphicsPresets.Apply(data, GraphicsQuality.High, GameSettingsApplier.Instance.Baseline);
                data.masterVolume = 1;
                GameSettings.NotifyChanged();
            }
            if (manager != null)
            {
                manager.ClientManager.StopConnection();
                manager.ServerManager.StopConnection(true);
                UnityEngine.Object.Destroy(manager.gameObject);
            }
            yield return null;
            if (tower.IsValid() && tower.isLoaded) yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(tower);
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        private static IEnumerator Wait(Func<bool> condition, string failure, float seconds = 20)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(condition(), Is.True, failure);
        }

        [UnityTest]
        public IEnumerator Escape_OpensAndClosesTheSettingsAndOnlyClosesOtherWindows()
        {
            var controls = UnityEngine.Object.FindFirstObjectByType<PlayerControlStateMachine>();
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            Assert.That(menu.IsVisible, Is.False);
            yield return Press(Key.Escape);
            Assert.That(menu.IsVisible, Is.True, "Escape opens the settings.");
            Assert.That(Cursor.visible, Is.True); Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(controls.CanScanConstruction, Is.False, "The player does not move while the menu is open.");
            Assert.That(menu.TabButtons.Keys, Is.EquivalentTo(SettingsMenuUI.Tabs));
            foreach (string tab in SettingsMenuUI.Tabs) { menu.TabButtons[tab].onClick.Invoke(); Assert.That(menu.CurrentTab, Is.EqualTo(tab)); yield return null; }
            yield return Press(Key.Escape);
            Assert.That(menu.IsVisible, Is.False, "Escape closes it again.");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked), "The cursor returns to how it was.");
            Assert.That(controls.CanScanConstruction, Is.True);

            // With the developer controls open, Escape closes them and does not open the settings.
            yield return Press(Key.F3);
            Assert.That(cheats.IsVisible, Is.True);
            yield return Press(Key.Escape);
            Assert.That(cheats.IsVisible, Is.False);
            Assert.That(menu.IsVisible, Is.False);
            // Opening the settings closes the developer controls.
            cheats.Show(); menu.Show();
            Assert.That(cheats.IsVisible, Is.False); Assert.That(menu.IsVisible, Is.True);
            menu.Hide();
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
        }

        [UnityTest]
        public IEnumerator ConsoleKey_ShowsAndHidesTheMouseCursor()
        {
            Assert.That(tower.GetRootGameObjects().Any(r => r.name == "IngameDebugConsole"), Is.False, "The debug console is not in the scene.");
            Assert.That(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(c => c != null && c.GetType().Name == "DebugLogManager"), Is.False);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            yield return Press(Key.Backquote);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "The key left of 1 frees the mouse.");
            Assert.That(Cursor.visible, Is.True);
            Assert.That(menu.IsVisible, Is.False);
            yield return Press(Key.Backquote);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked), "Pressed again, it captures the mouse.");
            Assert.That(Cursor.visible, Is.False);
        }

        [UnityTest]
        public IEnumerator Settings_ReachTheRunningGameAndHighIsTheSceneItself()
        {
            var applier = GameSettingsApplier.Instance;
            Assert.That(applier, Is.Not.Null);
            GraphicsBaseline scene = applier.Baseline;
            GameSettingsData data = GameSettings.Current;
            var far = UnityEngine.Object.FindFirstObjectByType<InstancedFarChunkRenderer>();
            var terrain = Terrain.activeTerrain;
            Camera view = Camera.main;
            Assert.That(data.quality, Is.EqualTo(GraphicsQuality.High), "High is the default.");
            Assert.That(AeroPipelineQuality.CurrentShadowDistance, Is.EqualTo(scene.shadowDistance));
            Assert.That(AeroPipelineQuality.CurrentRenderScale, Is.EqualTo(1));
            Assert.That(terrain.detailObjectDistance, Is.EqualTo(scene.grassDistance)); Assert.That(terrain.heightmapPixelError, Is.EqualTo(scene.terrainPixelError));
            Assert.That(far.DetailDrawDistance, Is.EqualTo(GraphicsPresets.HighDetailDistance)); Assert.That(far.LodDistanceScale, Is.EqualTo(1));
            Assert.That(far.IsGpuCulled, Is.True, "The far tower draws through the GPU-culled path.");
            Assert.That(InstancedFarChunkRenderer.OcclusionCulling, Is.True);
            Assert.That(new[] { AeroRenderQuality.SunShafts, AeroRenderQuality.Clouds }, Is.All.EqualTo(AeroRenderQuality.Full));
            Assert.That(AeroRenderQuality.LiveReflections, Is.True); Assert.That(AeroRenderQuality.ReducedRiverRefraction, Is.False);
            yield return null;
            Assert.That(view.farClipPlane, Is.EqualTo(scene.cameraFar)); Assert.That(view.fieldOfView, Is.EqualTo(scene.cameraFov).Within(.01f));
            Assert.That(Application.targetFrameRate, Is.EqualTo(-1), "No frame-rate target: the game renders as fast as it can.");

            menu.Show(); menu.SelectTab("Graphics");
            menu.StepChoice("Quality", -1); menu.StepChoice("Quality", -1);
            Assert.That(data.quality, Is.EqualTo(GraphicsQuality.Low));
            yield return null; yield return null;
            Assert.That(AeroPipelineQuality.CurrentShadowDistance, Is.EqualTo(data.shadowDistance)); Assert.That(AeroPipelineQuality.CurrentShadowDistance, Is.LessThan(scene.shadowDistance));
            Assert.That(AeroPipelineQuality.CurrentRenderScale, Is.EqualTo(.75f)); Assert.That(AeroPipelineQuality.CurrentShadowCascades, Is.EqualTo(1));
            Assert.That(terrain.detailObjectDistance, Is.EqualTo(data.grassDistance)); Assert.That(terrain.heightmapPixelError, Is.EqualTo(data.terrainPixelError));
            Assert.That(terrain.basemapDistance, Is.EqualTo(data.baseMapDistance));
            Assert.That(far.DetailDrawDistance, Is.EqualTo(GraphicsPresets.LowDetailDistance)); Assert.That(far.LodDistanceScale, Is.LessThan(.5f));
            Assert.That(AeroRenderQuality.SunShafts, Is.EqualTo(AeroRenderQuality.Off)); Assert.That(AeroRenderQuality.LiveReflections, Is.False);
            Assert.That(view.farClipPlane, Is.EqualTo(data.cameraFar));

            menu.Sliders["Camera field of view"].value = 1;
            menu.SelectTab("Audio");
            menu.Sliders["Master"].value = .3f;
            menu.Sliders["VFX"].value = .5f;
            yield return null; yield return null;
            Assert.That(view.fieldOfView, Is.EqualTo(GameSettings.Ranges.CameraFov.y).Within(.01f));
            Assert.That(AudioListener.volume, Is.EqualTo(.3f).Within(.006f)); Assert.That(AeroAudio.Vfx, Is.EqualTo(.5f).Within(.006f));

            menu.SelectTab("Graphics");
            menu.ActionButtons["Restore defaults"].onClick.Invoke();
            yield return null; yield return null;
            Assert.That(data.quality, Is.EqualTo(GraphicsQuality.High));
            Assert.That(AeroPipelineQuality.CurrentShadowDistance, Is.EqualTo(scene.shadowDistance)); Assert.That(AeroPipelineQuality.CurrentRenderScale, Is.EqualTo(1));
            Assert.That(terrain.detailObjectDistance, Is.EqualTo(scene.grassDistance));
            Assert.That(view.farClipPlane, Is.EqualTo(scene.cameraFar)); Assert.That(view.fieldOfView, Is.EqualTo(scene.cameraFov).Within(.01f));
            Assert.That(AeroRenderQuality.SunShafts, Is.EqualTo(AeroRenderQuality.Full));
            menu.Hide();
        }

        [UnityTest]
        public IEnumerator RemappedKeys_DriveTheGame()
        {
            Assert.That(GameInput.Asset, Is.Not.Null, "The game uses the project's input asset.");
            Assert.That(GameInput.SetBinding(GameInput.Find("CheatMenu"), "<Keyboard>/f9", false), Is.True);
            yield return null;
            yield return Press(Key.F3);
            Assert.That(cheats.IsVisible, Is.False, "The old key no longer opens the developer controls.");
            yield return Press(Key.F9);
            Assert.That(cheats.IsVisible, Is.True, "The new key does.");
            cheats.Hide();

            // The mouse cursor key can be moved too.
            Assert.That(GameInput.SetBinding(GameInput.Find("ToggleCursor"), "<Keyboard>/f10", false), Is.True);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            yield return Press(Key.Backquote);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            yield return Press(Key.F10);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            yield return Press(Key.F10);

            // The Controls tab shows the new key and restores the defaults.
            menu.Show(); menu.SelectTab("Controls");
            Assert.That(menu.BindingButtons["CheatMenu"].GetComponentInChildren<TMPro.TMP_Text>().text, Does.Contain("F9"));
            menu.ActionButtons["Default controls"].onClick.Invoke();
            Assert.That(GameInput.Bindings.All(b => b.IsDefault), Is.True);
            menu.Hide();
            yield return Press(Key.F3);
            Assert.That(cheats.IsVisible, Is.True);
        }
    }
}
