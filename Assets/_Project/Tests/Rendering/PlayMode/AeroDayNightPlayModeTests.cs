#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroDayNightPlayModeTests
    {
        [UnityTest]
        public IEnumerator Cycle_AdvancesAndSwitchesSunMoonAndFlareWithoutAllocatingNewSkyMaterials()
        {
            var previous=SceneManager.GetActiveScene(); var scene=SceneManager.CreateScene("Day night runtime fixture"); SceneManager.SetActiveScene(scene);
            var root=new GameObject("Cycle fixture"); var profile=ScriptableObject.CreateInstance<AeroDayNightProfile>();
            profile.cycleSeconds=60; profile.startTime=.32f;
            try
            {
                var cycle=root.AddComponent<AeroDayNightCycle>(); cycle.profile=profile;
                var sun=new GameObject("Fixture sun",typeof(Light)); sun.transform.SetParent(root.transform); cycle.sun=sun.GetComponent<Light>();cycle.sun.type=LightType.Directional;
                var moon=new GameObject("Fixture moon",typeof(Light));moon.transform.SetParent(root.transform);cycle.moon=moon.GetComponent<Light>();cycle.moon.type=LightType.Directional;
                var flare=sun.AddComponent<UnityEngine.Rendering.LensFlareComponentSRP>();
                yield return null; float before=cycle.timeOfDay; var sky=RenderSettings.skybox;
                yield return new WaitForSeconds(.25f);
                Assert.That(cycle.timeOfDay,Is.GreaterThan(before)); Assert.That(RenderSettings.skybox,Is.SameAs(sky));
                cycle.ApplyTime(.5f); Assert.That(cycle.sun.intensity,Is.GreaterThan(2)); Assert.That(cycle.moon.enabled,Is.False); Assert.That(flare.intensity,Is.GreaterThan(0));
                cycle.ApplyTime(0); Assert.That(cycle.sun.enabled,Is.False); Assert.That(cycle.moon.enabled,Is.True); Assert.That(flare.intensity,Is.Zero);
            }
            finally
            {
                Object.Destroy(root); Object.Destroy(profile); SceneManager.SetActiveScene(previous);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Fireflies_HaveBoundedPopulationAndContinuousMotion()
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Lighting/AeroFireflies.prefab"); Assert.That(source,Is.Not.Null);
            var root=new GameObject("Firefly runtime fixture");
            var ps=Object.Instantiate(source,root.transform).GetComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.position=new Vector3(10000,2,10000); ps.useAutoRandomSeed=false; ps.randomSeed=4281;
            try
            {
                ps.Simulate(10,true,true,false); var a=new ParticleSystem.Particle[96]; int count=ps.GetParticles(a);
                Assert.That(count,Is.InRange(4,96)); ps.Simulate(.1f,true,false,false);
                var b=new ParticleSystem.Particle[96]; int after=ps.GetParticles(b);
                var lookup=new System.Collections.Generic.Dictionary<uint,Vector3>(); for(int i=0;i<count;i++)lookup[a[i].randomSeed]=a[i].position;
                int moving=0;
                for(int i=0;i<after;i++) if(lookup.TryGetValue(b[i].randomSeed,out var before))
                {
                    float distance=Vector3.Distance(before,b[i].position); Assert.That(distance,Is.LessThan(.4f)); if(distance>.0001f)moving++;
                    Assert.That(b[i].startSize,Is.InRange(.018f,.0551f));
                }
                Assert.That(moving,Is.GreaterThan(0));
            }
            finally {Object.Destroy(root);}
            yield return null;
        }
    }
}
#endif
