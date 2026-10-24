#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerOfBabel.World.Tests
{
    public sealed class GlassWaterFloorPlayModeTests
    {
        [UnityTest]
        public IEnumerator ChunkProjection_AdvancesCookiesAndReleasesAfterRollbackAndDisable()
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var owner=new GameObject("Runtime river source pool");owner.SetActive(false);
            var manager=owner.AddComponent<ChunkManager>();
            var assets=new List<ChunkAssetData>();
            for(int i=0;i<4;i++)assets.Add(new ChunkAssetData(i,TowerAssetType.GlassWaterFloor,new Vector3(i*4-8,0,2),Quaternion.identity,Vector3.one,10));
            var chunk=(ChunkSceneCache)System.Activator.CreateInstance(typeof(ChunkSceneCache),flags,null,new object[]{new ChunkKey(0,0,0),assets},null);
            typeof(ChunkManager).GetField("cachedChunks",flags).SetValue(manager,new List<ChunkSceneCache>{chunk});
            var lighting=owner.AddComponent<RiverGlassLighting>();
            lighting.profile=AssetDatabase.LoadAssetAtPath<RiverGlassProfile>("Assets/_Project/Environment/Lighting/RiverGlass.asset");
            lighting.cookieShader=Shader.Find("Hidden/TowerOfBabel/River caustic cookie");
            var viewRoot=new GameObject("Runtime water camera");viewRoot.tag="MainCamera";
            var view=viewRoot.AddComponent<Camera>();view.cullingMask=0;view.transform.position=new Vector3(3,2,0);
            try
            {
                owner.SetActive(true);yield return new WaitForSeconds(.4f);
                Assert.That(lighting.ActiveLightCount,Is.EqualTo(4));Assert.That(lighting.AllocatedCookieCount,Is.EqualTo(2));
                var cookie=owner.GetComponentInChildren<Light>().cookie;uint before=cookie.updateCount;
                yield return new WaitForSeconds(.2f);
                Assert.That(cookie.updateCount,Is.GreaterThan(before),"Runtime, not just a sampled preview, must refresh the cookie.");
                for(int i=0;i<4;i++)manager.SetAssetStage(new ChunkKey(0,0,0),i,9);
                yield return null;Assert.That(lighting.ActiveLightCount,Is.Zero);
                lighting.enabled=false;yield return null;
                Assert.That(lighting.AllocatedCookieCount,Is.Zero);Assert.That(owner.GetComponentsInChildren<Light>(true),Is.Empty);
            }
            finally {Object.Destroy(owner);Object.Destroy(viewRoot);}
            yield return null;
        }

        [UnityTest]
        public IEnumerator WaterGlass_OnlyDrawsAfterCompletionAndStopsOnRollbackOrPoolRelease()
        {
            var owner=new GameObject("Glass floor owner"); owner.SetActive(false);
            var manager=owner.AddComponent<ChunkManager>(); manager.enabled=false;
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/GlassWaterFloor.prefab"));
            model.transform.position=Vector3.right*10000;
            var cameraRoot=new GameObject("River state camera"); var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;
            try
            {
                var building=model.GetComponent<Building>(); var water=model.GetComponent<GlassWaterFloorLight>();
                building.Bind(manager,new ChunkKey(0,0,0),0,9); yield return null;
                Assert.That(water.region.enabled,Is.False);
                bool Visible()=>water.TryGetDraw(camera,out _,out _,out _,out _,out _);
                Assert.That(Visible(),Is.False);
                building.ApplyStageImmediately(10); yield return null;
                Assert.That(Visible(),Is.True); Assert.That(water.region.enabled,Is.False); Assert.That(building.CanInteract,Is.False);
                building.ApplyStageImmediately(9); yield return null; Assert.That(Visible(),Is.False);
                building.ApplyStageImmediately(10); yield return null; Assert.That(Visible(),Is.True);
                building.Unbind(); yield return null; Assert.That(Visible(),Is.False);
            }
            finally {Object.Destroy(model);Object.Destroy(owner);Object.Destroy(cameraRoot);}
            yield return null;
        }
    }
}
#endif
