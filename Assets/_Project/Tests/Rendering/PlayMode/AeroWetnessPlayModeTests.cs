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
                Assert.That(controller.Wetness, Is.Zero, "Wetness is off by default until rain is implemented.");
                Assert.That(Shader.GetGlobalFloat(AeroWetnessController.ReadyId), Is.Zero, "Shaders skip the wet path while it is off.");
                controller.WetnessEnabled = true;
                yield return new WaitForSeconds(.8f); // dew ramps in over rampIn (0.5 s)
                float dew = controller.Wetness;
                Assert.That(dew, Is.GreaterThan(0), "Once enabled, dew returns.");
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
    }
}
#endif
