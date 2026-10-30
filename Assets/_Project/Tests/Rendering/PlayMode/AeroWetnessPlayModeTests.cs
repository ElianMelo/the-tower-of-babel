#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroWetnessPlayModeTests
    {
        sealed class MemoryStore : IAeroSettingsStore
        {
            public readonly Dictionary<string, int> values = new();
            public int GetInt(string key, int fallback) => values.TryGetValue(key, out var v) ? v : fallback;
            public void SetInt(string key, int value) => values[key] = value;
        }

        [UnityTest]
        public IEnumerator Rain_WetsTheWorldAtRuntimeAndDriesAfterwards()
        {
            var previous = SceneManager.GetActiveScene(); var scene = SceneManager.CreateScene("Wetness runtime fixture"); SceneManager.SetActiveScene(scene);
            var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AeroWetnessProfile>("Assets/_Project/Environment/Lighting/AeroWetness.asset"));
            profile.rampIn = .5f; profile.dryOut = 1; profile.rainFade = .25f;
            var root = new GameObject("Wetness fixture"); root.SetActive(false);
            try
            {
                var controller = root.AddComponent<AeroWetnessController>(); controller.profile = profile;
                root.SetActive(true);
                yield return null;
                Assert.That(AeroWetnessController.Instance, Is.SameAs(controller));
                float dew = controller.Wetness;
                Assert.That(dew, Is.LessThan(.5f), "Dry weather settles at a light dew.");
                Assert.That(Shader.GetGlobalFloat(AeroWetnessController.ReadyId), Is.EqualTo(1), "Textures are bound for shaders.");
                controller.Raining = true;
                yield return new WaitForSeconds(.8f);
                Assert.That(controller.Wetness, Is.GreaterThan(.9f));
                Assert.That(Shader.GetGlobalFloat(AeroWetnessController.RainId), Is.GreaterThan(.9f));
                float clock = Shader.GetGlobalFloat(AeroWetnessController.ClockId);
                yield return null; yield return null;
                Assert.That(Shader.GetGlobalFloat(AeroWetnessController.ClockId), Is.GreaterThan(clock), "Drops animate with game time.");
                controller.Raining = false;
                yield return new WaitForSeconds(1.3f);
                Assert.That(controller.Wetness, Is.EqualTo(dew).Within(.05f), "Surfaces dry back to dew.");
                Assert.That(controller.RainAmount, Is.Zero);
            }
            finally { Object.Destroy(root); Object.Destroy(profile); SceneManager.SetActiveScene(previous); }
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator PostLook_CrossfadesAndRestoresTheSavedChoice()
        {
            var previous = SceneManager.GetActiveScene(); var scene = SceneManager.CreateScene("Post look runtime fixture"); SceneManager.SetActiveScene(scene);
            var store = new MemoryStore(); store.SetInt(AeroPostProfileSwitcher.PreferenceKey, (int)AeroPostLook.BrightAero);
            var root = new GameObject("Post fixture"); root.SetActive(false);
            try
            {
                var volume = root.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = -50;
                var switcher = root.AddComponent<AeroPostProfileSwitcher>();
                switcher.restrained.profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset");
                switcher.brightAero.profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Environment/Lighting/AeroPost_Bright.asset");
                switcher.y2k.profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Environment/Lighting/AeroPost_Y2K.asset");
                switcher.Store = store; switcher.crossfadeSeconds = .2f;
                root.SetActive(true);
                yield return null;
                Assert.That(switcher.Current, Is.EqualTo(AeroPostLook.BrightAero), "The saved look is restored on start.");
                Assert.That(volume.sharedProfile, Is.SameAs(switcher.brightAero.profile));
                switcher.Set(AeroPostLook.Y2K);
                var fade = root.GetComponentsInChildren<Volume>(true);
                Assert.That(fade.Length, Is.EqualTo(2), "The outgoing look fades out on a temporary volume.");
                yield return new WaitForSeconds(.35f);
                Assert.That(root.GetComponentsInChildren<Volume>(true).Length, Is.EqualTo(1), "The crossfade cleans up after itself.");
                Assert.That(store.values[AeroPostProfileSwitcher.PreferenceKey], Is.EqualTo((int)AeroPostLook.Y2K));
            }
            finally { Object.Destroy(root); SceneManager.SetActiveScene(previous); }
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
