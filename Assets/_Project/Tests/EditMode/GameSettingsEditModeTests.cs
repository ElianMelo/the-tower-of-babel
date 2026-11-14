using System.Linq;
using NUnit.Framework;
using TMPro;
using TowerOfBabel.Rendering;
using TowerOfBabel.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ShadowQuality = TowerOfBabel.Settings.ShadowQuality;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>
    /// The settings menu (Escape): Video, Graphics, Audio, Gameplay and Controls, three graphics presets with High as
    /// the uncompromised default, remappable keys, and the mouse cursor on the old debug console key.
    /// </summary>
    public sealed class GameSettingsEditModeTests
    {
        // Editor authoring code is outside this assembly; these mirror SettingsMenuAuthoring and FrutigerFontBuilder.
        private const string PrefabPath = "Assets/_Project/UI/FrutigerAero/Prefabs/Screens/SettingsMenu.prefab";
        private const string ConsoleObjectName = "IngameDebugConsole";
        private const string RegularFont = "Assets/_Project/Fonts/Frutiger SDF.asset", BoldFont = "Assets/_Project/Fonts/Frutiger Bold SDF.asset";
        private IGameSettingsStore previousStore;
        private MemorySettingsStore store;

        [SetUp]
        public void UseMemoryStore()
        {
            previousStore = GameSettings.Store;
            GameSettings.Store = store = new MemorySettingsStore();
            GameInput.LoadOverrides("");
        }

        [TearDown]
        public void RestoreStore()
        {
            GameInput.LoadOverrides("");
            GameSettings.Store = previousStore;
            AeroRenderQuality.Reset(); AeroAudio.Reset();
        }

        [Test]
        public void Defaults_AreTheHighPresetAndTheSceneItself()
        {
            var data = new GameSettingsData();
            Assert.That(data.quality, Is.EqualTo(GraphicsQuality.High), "High is the default, to see the game as intended.");
            var scene = GraphicsBaseline.Default;
            GraphicsPresets.Apply(data, GraphicsQuality.High, scene);
            Assert.That(data.shadowQuality, Is.EqualTo(ShadowQuality.High));
            Assert.That(data.shadowDistance, Is.EqualTo(scene.shadowDistance));
            Assert.That(data.terrainPixelError, Is.EqualTo(scene.terrainPixelError));
            Assert.That(data.baseMapDistance, Is.EqualTo(scene.baseMapDistance));
            Assert.That(data.grassDistance, Is.EqualTo(scene.grassDistance));
            Assert.That(data.grassDensity, Is.EqualTo(scene.grassDensity));
            Assert.That(data.cameraFar, Is.EqualTo(scene.cameraFar));
            Assert.That(data.renderScale, Is.EqualTo(1));
            Assert.That(data.towerLodScale, Is.EqualTo(1), "High keeps the authored, sub-pixel mesh LOD distances.");
            Assert.That(data.towerDetailDistance, Is.GreaterThan(140), "High draws tower detail past the authored 140 m, about as far as whole pages used to reach.");
            Assert.That(data.occlusionCulling && data.liveReflections && data.riverRefraction, Is.True);
            Assert.That(new[] { data.sunShafts, data.clouds }, Is.All.EqualTo(AeroRenderQuality.Full));
            Assert.That(data.lampLights, Is.EqualTo(scene.lampLights));
            Assert.That(GraphicsPresets.Detect(data, scene), Is.EqualTo(GraphicsQuality.High));
        }

        [Test]
        public void Presets_GetCheaperFromHighToLowAndAreRecognised()
        {
            var scene = GraphicsBaseline.Default;
            GameSettingsData high = new(), medium = new(), low = new();
            GraphicsPresets.Apply(high, GraphicsQuality.High, scene);
            GraphicsPresets.Apply(medium, GraphicsQuality.Medium, scene);
            GraphicsPresets.Apply(low, GraphicsQuality.Low, scene);
            foreach (var (a, b) in new[] { (high, medium), (medium, low) })
            {
                Assert.That(b.shadowQuality, Is.LessThan(a.shadowQuality));
                Assert.That(b.shadowDistance, Is.LessThan(a.shadowDistance));
                Assert.That(b.terrainPixelError, Is.GreaterThan(a.terrainPixelError));
                Assert.That(b.baseMapDistance, Is.LessThan(a.baseMapDistance));
                Assert.That(b.grassDistance, Is.LessThan(a.grassDistance));
                Assert.That(b.grassDensity, Is.LessThan(a.grassDensity));
                Assert.That(b.cameraFar, Is.LessThan(a.cameraFar));
                Assert.That(b.towerDetailDistance, Is.LessThan(a.towerDetailDistance));
                Assert.That(b.towerLodScale, Is.LessThan(a.towerLodScale));
                Assert.That(b.sunShafts, Is.LessThan(a.sunShafts));
                Assert.That(b.clouds, Is.LessThan(a.clouds));
                Assert.That(b.lampLights, Is.LessThan(a.lampLights));
            }
            Assert.That(low.renderScale, Is.LessThan(1)); Assert.That(low.liveReflections || low.riverRefraction, Is.False);
            Assert.That(medium.renderScale, Is.EqualTo(1), "Medium keeps the full resolution.");
            Assert.That(GraphicsPresets.Detect(medium, scene), Is.EqualTo(GraphicsQuality.Medium));
            Assert.That(GraphicsPresets.Detect(low, scene), Is.EqualTo(GraphicsQuality.Low));
            medium.grassDistance += 10;
            Assert.That(GraphicsPresets.Detect(medium, scene), Is.EqualTo(GraphicsQuality.Custom), "One changed value makes the quality Custom.");
            // The field of view and the near plane are comfort settings: no preset touches them.
            low.cameraFov = 95; low.cameraNear = .1f;
            GraphicsPresets.Apply(low, GraphicsQuality.High, scene);
            Assert.That(low.cameraFov, Is.EqualTo(95)); Assert.That(low.cameraNear, Is.EqualTo(.1f));
        }

        [Test]
        public void Settings_SurviveASaveAndRejectBrokenValues()
        {
            GameSettingsData data = GameSettings.Current;
            data.masterVolume = .25f; data.invertY = true; data.lookSensitivity = 2.5f; data.displayMode = DisplayMode.Windowed;
            data.resolutionWidth = 1280; data.resolutionHeight = 720;
            int notified = 0;
            void Count() => notified++;
            GameSettings.Changed += Count;
            try { GameSettings.Save(); }
            finally { GameSettings.Changed -= Count; }
            Assert.That(notified, Is.EqualTo(1));
            Assert.That(store.Json, Does.Contain("masterVolume"));

            GameSettings.Store = store; // Forget the loaded copy and read the store again.
            GameSettingsData loaded = GameSettings.Current;
            Assert.That(loaded, Is.Not.SameAs(data));
            Assert.That(loaded.masterVolume, Is.EqualTo(.25f)); Assert.That(loaded.invertY, Is.True);
            Assert.That(loaded.lookSensitivity, Is.EqualTo(2.5f)); Assert.That(loaded.displayMode, Is.EqualTo(DisplayMode.Windowed));
            Assert.That(new[] { loaded.resolutionWidth, loaded.resolutionHeight }, Is.EqualTo(new[] { 1280, 720 }));

            store.Json = "{\"masterVolume\":7,\"cameraFov\":500,\"shadowDistance\":-4,\"lookSensitivity\":0,\"quality\":9,\"renderScale\":0}";
            GameSettings.Store = store;
            loaded = GameSettings.Current;
            Assert.That(loaded.masterVolume, Is.EqualTo(1)); Assert.That(loaded.cameraFov, Is.EqualTo(GameSettings.Ranges.CameraFov.y));
            Assert.That(loaded.shadowDistance, Is.EqualTo(0)); Assert.That(loaded.lookSensitivity, Is.EqualTo(GameSettings.Ranges.LookSensitivity.x));
            Assert.That(loaded.quality, Is.EqualTo(GraphicsQuality.Custom)); Assert.That(loaded.renderScale, Is.EqualTo(.5f));
            store.Json = "not json";
            GameSettings.Store = store;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try { Assert.That(GameSettings.Current.quality, Is.EqualTo(GraphicsQuality.High), "A damaged save falls back to the defaults."); }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
        }

        [Test]
        public void Controls_HaveEveryKeyAndTheCursorOnTheOldConsoleKey()
        {
            var cursor = GameInput.Find("ToggleCursor");
            Assert.That(cursor.DefaultPath, Is.EqualTo("<Keyboard>/backquote"), "The key left of 1, which opened the debug console.");
            Assert.That(cursor.Rebindable, Is.True);
            var settings = GameInput.Find("Settings");
            Assert.That(settings.DefaultPath, Is.EqualTo("<Keyboard>/escape")); Assert.That(settings.Rebindable, Is.False, "The menu key stays on Escape.");
            foreach (string id in new[] { "Move/up", "Move/down", "Move/left", "Move/right", "Jump", "Sprint", "Descend", "Interact", "Scan",
                         "Inventory", "Upgrade", "Emote", "Menus", "CheatMenu", "GraphicsPanel", "StartHost", "JoinGame" })
            {
                var binding = GameInput.Find(id);
                Assert.That(binding, Is.Not.Null, id);
                Assert.That(binding.Rebindable, Is.True, id);
                Assert.That(binding.Action, Is.Not.Null, id); Assert.That(binding.Index, Is.GreaterThanOrEqualTo(0), id);
                Assert.That(binding.Path, Is.EqualTo(binding.DefaultPath), id);
            }
            // No two controls share a default key.
            Assert.That(GameInput.Bindings.Select(b => b.DefaultPath.ToLowerInvariant()).Distinct().Count(), Is.EqualTo(GameInput.Bindings.Count));

            // The project's input asset defines every action with the same default key, so the interface scripts that
            // read the asset follow the remapped keys.
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            foreach (var binding in GameInput.Bindings)
            {
                var action = asset.FindAction(GameInput.MapName + "/" + binding.ActionName, false);
                Assert.That(action, Is.Not.Null, binding.ActionName + " is in the input asset.");
                Assert.That(action.bindings.Any(b => !b.isComposite && string.Equals(b.path, binding.DefaultPath, System.StringComparison.OrdinalIgnoreCase) &&
                                                     (binding.Part == null || string.Equals(b.name, binding.Part, System.StringComparison.OrdinalIgnoreCase))),
                    Is.True, binding.Id + " uses " + binding.DefaultPath + " in the input asset.");
            }
        }

        [Test]
        public void Controls_RemapSwapSaveAndReset()
        {
            var interact = GameInput.Find("Interact");
            var jump = GameInput.Find("Jump");
            int changes = 0;
            void Count() => changes++;
            GameInput.BindingsChanged += Count;
            try
            {
                Assert.That(GameInput.SetBinding(interact, "<Keyboard>/f"), Is.True);
                Assert.That(interact.Path, Is.EqualTo("<Keyboard>/f")); Assert.That(interact.IsDefault, Is.False);
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(GameSettings.Current.bindingOverrides, Does.Contain("<Keyboard>/f"), "Remapped keys are part of the saved settings.");
                Assert.That(store.Json, Does.Contain("Interact"));

                // A key in use swaps: nothing is left without a key.
                Assert.That(GameInput.SetBinding(jump, "<Keyboard>/f"), Is.True);
                Assert.That(jump.Path, Is.EqualTo("<Keyboard>/f"));
                Assert.That(interact.Path, Is.EqualTo("<Keyboard>/space"), "Interact took Jump's previous key.");
                Assert.That(GameInput.Conflict(jump, "<Keyboard>/space"), Is.SameAs(interact));

                // The fixed menu key can neither be remapped nor taken.
                Assert.That(GameInput.SetBinding(GameInput.Find("Settings"), "<Keyboard>/p"), Is.False);
                Assert.That(GameInput.SetBinding(jump, "<Keyboard>/escape"), Is.False);
                Assert.That(jump.Path, Is.EqualTo("<Keyboard>/f"));

                string saved = GameInput.SaveOverrides();
                GameInput.ResetBindings();
                Assert.That(GameInput.Bindings.All(b => b.IsDefault), Is.True);
                Assert.That(GameSettings.Current.bindingOverrides, Is.Empty);
                GameInput.LoadOverrides(saved);
                Assert.That(jump.Path, Is.EqualTo("<Keyboard>/f")); Assert.That(interact.Path, Is.EqualTo("<Keyboard>/space"));
                Assert.That(GameInput.DisplayName(jump), Is.Not.Empty);
            }
            finally { GameInput.BindingsChanged -= Count; }
        }

        private static SettingsMenuUI NewMenu(out GameObject root)
        {
            root = new GameObject("Settings menu test", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
            var menu = root.AddComponent<SettingsMenuUI>();
            menu.Configure(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFont), AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFont),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/UI/FrutigerAero/Materials/FrostedGlass.mat"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/FrutigerAero/Prefabs/GlassButton.prefab"), null);
            menu.BuildView();
            return menu;
        }

        [Test]
        public void Menu_HasTheFiveTabsWithEveryRequestedSetting()
        {
            var menu = NewMenu(out GameObject root);
            try
            {
                Assert.That(SettingsMenuUI.Tabs, Is.EqualTo(new[] { "Video", "Graphics", "Audio", "Gameplay", "Controls" }));
                Assert.That(menu.TabButtons.Keys, Is.EquivalentTo(SettingsMenuUI.Tabs));
                Assert.That(menu.IsVisible, Is.False, "Closed until Escape is pressed.");

                menu.SelectTab("Video");
                Assert.That(menu.ChoiceLabels, Is.EquivalentTo(new[] { "Resolution", "Display mode" }));
                Assert.That(menu.ActionButtons.ContainsKey("Apply display"), Is.True);

                menu.SelectTab("Graphics");
                Assert.That(menu.ChoiceLabels, Is.SupersetOf(new[] { "Quality", "Shadow quality" }));
                Assert.That(menu.Sliders.Keys, Is.SupersetOf(new[] { "Shadow distance", "Pixel quality", "Base map distance", "Grass draw distance",
                    "Camera far plane", "Camera near plane", "Camera field of view" }));
                // Everything the optimization pass introduced is here too.
                Assert.That(menu.Sliders.Keys, Is.SupersetOf(new[] { "Tower detail distance", "Tower mesh detail", "Render scale", "Grass density", "Lava lamp lights" }));
                Assert.That(menu.Toggles.Keys, Is.SupersetOf(new[] { "Tower occlusion culling", "Floor reflections", "River refraction" }));
                Assert.That(menu.ChoiceLabels, Is.SupersetOf(new[] { "Sun shafts", "Clouds" }));
                Assert.That(menu.ChoiceText("Quality"), Is.EqualTo("High"));

                menu.SelectTab("Audio");
                Assert.That(menu.Sliders.Keys, Is.EquivalentTo(new[] { "Master", "Music", "UI", "VFX" }));
                menu.SelectTab("Gameplay");
                Assert.That(menu.Sliders.Keys, Is.EquivalentTo(new[] { "Look sensitivity" }));
                Assert.That(menu.Toggles.Keys, Is.EquivalentTo(new[] { "Invert X axis", "Invert Y axis" }));
                menu.SelectTab("Controls");
                Assert.That(menu.BindingButtons.Keys, Is.EquivalentTo(GameInput.Bindings.Select(b => b.Id)));
                Assert.That(menu.BindingButtons["Settings"].interactable, Is.False);
                Assert.That(menu.BindingButtons["ToggleCursor"].interactable, Is.True);
                Assert.That(menu.BindingButtons["Jump"].GetComponentInChildren<TMP_Text>().text, Is.Not.Empty);
                Assert.Throws<System.ArgumentException>(() => menu.SelectTab("Cheats"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Menu_ControlsWriteTheSettingsAndTrackTheQualityPreset()
        {
            var menu = NewMenu(out GameObject root);
            try
            {
                GameSettingsData data = GameSettings.Current;
                GraphicsPresets.Apply(data, GraphicsQuality.High, GraphicsBaseline.Default);
                menu.SelectTab("Graphics");
                menu.StepChoice("Quality", -1);
                Assert.That(data.quality, Is.EqualTo(GraphicsQuality.Medium));
                Assert.That(menu.ChoiceText("Quality"), Is.EqualTo("Medium"));
                Assert.That(data.towerDetailDistance, Is.EqualTo(GraphicsPresets.MediumDetailDistance));
                menu.StepChoice("Quality", -1);
                Assert.That(data.quality, Is.EqualTo(GraphicsQuality.Low));
                Assert.That(menu.ChoiceText("Sun shafts"), Is.EqualTo("Off"), "The other rows follow the preset.");
                Assert.That(menu.Toggles["Floor reflections"].isOn, Is.False);

                // One changed value: the preset becomes Custom; stepping the quality picks a preset again.
                menu.Sliders["Shadow distance"].value = 1;
                Assert.That(data.shadowDistance, Is.EqualTo(GameSettings.Ranges.ShadowDistance.y));
                Assert.That(data.quality, Is.EqualTo(GraphicsQuality.Custom)); Assert.That(menu.ChoiceText("Quality"), Is.EqualTo("Custom"));
                menu.StepChoice("Quality", 1);
                Assert.That(data.quality, Is.EqualTo(GraphicsQuality.High)); Assert.That(data.shadowDistance, Is.EqualTo(GraphicsBaseline.Default.shadowDistance));

                menu.Toggles["Tower occlusion culling"].isOn = false;
                Assert.That(data.occlusionCulling, Is.False);
                menu.Sliders["Camera field of view"].value = 1;
                Assert.That(data.cameraFov, Is.EqualTo(GameSettings.Ranges.CameraFov.y));
                Assert.That(data.quality, Is.EqualTo(GraphicsQuality.Custom), "Occlusion was switched off; the field of view alone would not make it Custom.");

                menu.SelectTab("Audio");
                menu.Sliders["Music"].value = .5f;
                Assert.That(data.musicVolume, Is.EqualTo(.5f).Within(.006f));
                menu.SelectTab("Gameplay");
                menu.Toggles["Invert Y axis"].isOn = true;
                Assert.That(data.invertY, Is.True);
                menu.Sliders["Look sensitivity"].value = 0;
                Assert.That(data.lookSensitivity, Is.EqualTo(GameSettings.Ranges.LookSensitivity.x).Within(.001f));

                menu.ActionButtons["Restore defaults"].onClick.Invoke();
                Assert.That(data.invertY, Is.False); Assert.That(data.lookSensitivity, Is.EqualTo(1));
                menu.SelectTab("Graphics");
                menu.ActionButtons["Restore defaults"].onClick.Invoke();
                Assert.That(data.quality, Is.EqualTo(GraphicsQuality.High)); Assert.That(data.occlusionCulling, Is.True);
                Assert.That(data.cameraFov, Is.EqualTo(GraphicsBaseline.Default.cameraFov));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PipelineQuality_AppliesShadowsAndPutsTheAssetBack()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assume.That(asset, Is.Not.Null);
            float distance = asset.shadowDistance, scale = asset.renderScale;
            int resolution = asset.mainLightShadowmapResolution, cascades = asset.shadowCascadeCount;
            try
            {
                AeroPipelineQuality.Apply(AeroPipelineQuality.ShadowsHigh, distance, scale);
                Assert.That(new[] { asset.mainLightShadowmapResolution, asset.shadowCascadeCount }, Is.EqualTo(new[] { resolution, cascades }), "High is the asset as saved.");
                Assert.That(asset.shadowDistance, Is.EqualTo(distance));
                AeroPipelineQuality.Apply(AeroPipelineQuality.ShadowsLow, 35, .75f);
                Assert.That(asset.shadowDistance, Is.EqualTo(35)); Assert.That(asset.renderScale, Is.EqualTo(.75f));
                Assert.That(asset.mainLightShadowmapResolution, Is.LessThanOrEqualTo(1024)); Assert.That(asset.shadowCascadeCount, Is.EqualTo(1));
                AeroPipelineQuality.Apply(AeroPipelineQuality.ShadowsOff, 35, 1);
                Assert.That(asset.shadowDistance, Is.Zero, "Shadows off: nothing casts.");
            }
            finally { AeroPipelineQuality.Restore(); }
            Assert.That(asset.shadowDistance, Is.EqualTo(distance)); Assert.That(asset.renderScale, Is.EqualTo(scale));
            Assert.That(new[] { asset.mainLightShadowmapResolution, asset.shadowCascadeCount }, Is.EqualTo(new[] { resolution, cascades }));
        }

        [Test]
        public void RenderTiers_LowerTheFeatureCostOnlyBelowHigh()
        {
            var clouds = ScriptableObject.CreateInstance<AeroCloudQuality>();
            var rays = ScriptableObject.CreateInstance<AeroRaySettings>();
            try
            {
                clouds.downsample = 2; clouds.primarySteps = 56; clouds.lightSteps = 6;
                Assert.That(new[] { clouds.Downsample, clouds.PrimarySteps, clouds.LightSteps }, Is.EqualTo(new[] { 2, 56, 6 }), "High: as authored.");
                Assert.That(new[] { rays.Downsample, rays.Steps, rays.FillCount }, Is.EqualTo(new[] { 2, 16, 2 }));
                AeroRenderQuality.Clouds = 1; AeroRenderQuality.SunShafts = AeroRenderQuality.Low;
                Assert.That(clouds.Downsample, Is.EqualTo(3)); Assert.That(clouds.PrimarySteps, Is.LessThan(56)); Assert.That(clouds.LightSteps, Is.LessThan(6));
                Assert.That(new[] { rays.Downsample, rays.Steps, rays.FillCount }, Is.EqualTo(new[] { 4, 12, 1 }));
                AeroRenderQuality.Clouds = 0;
                Assert.That(clouds.Downsample, Is.EqualTo(4)); Assert.That(clouds.PrimarySteps, Is.GreaterThanOrEqualTo(16));
                AeroRenderQuality.Reset();
                Assert.That(clouds.PrimarySteps, Is.EqualTo(56));
            }
            finally { Object.DestroyImmediate(clouds); Object.DestroyImmediate(rays); AeroRenderQuality.Reset(); }
        }

        [Test]
        public void TowerScene_HasTheSettingsMenuAndNoDebugConsole()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "Run Tower of Babel/UI/Install settings menu.");
            var menu = prefab.GetComponent<SettingsMenuUI>();
            Assert.That(menu, Is.Not.Null); Assert.That(prefab.GetComponent<GameSettingsApplier>(), Is.Not.Null);
            var serialized = new SerializedObject(menu);
            foreach (string field in new[] { "regularFont", "boldFont", "glassMaterial", "buttonPrefab", "inputActions" })
                Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);

            // The saved scene, opened beside whatever the test runner has open.
            const string towerPath = "Assets/_Project/Scenes/Tower.unity";
            var loaded = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(towerPath);
            bool opened = !(loaded.IsValid() && loaded.isLoaded);
            var scene = opened ? UnityEditor.SceneManagement.EditorSceneManager.OpenScene(towerPath, UnityEditor.SceneManagement.OpenSceneMode.Additive) : loaded;
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.Any(r => r.name == ConsoleObjectName), Is.False, "The debug console is gone; its key toggles the mouse cursor.");
                var menus = roots.SelectMany(r => r.GetComponentsInChildren<SettingsMenuUI>(true)).ToArray();
                Assert.That(menus.Length, Is.EqualTo(1));
                Assert.That(menus[0].transform.root.name, Is.EqualTo("UI"));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Any(c => c != null && c.GetType().Name == "DebugLogManager"), Is.False);
            }
            finally { if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
