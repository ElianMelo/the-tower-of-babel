#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerOfBabel.World.Tests
{
    public sealed class GlassWaterFloorPlayModeTests
    {
        [UnityTest]
        public IEnumerator WaterProjection_OnlyEmitsAfterCompletionAndStopsOnRollbackOrPoolRelease()
        {
            var owner=new GameObject("Glass floor owner"); owner.SetActive(false);
            var manager=owner.AddComponent<ChunkManager>(); manager.enabled=false;
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/GlassWaterFloor.prefab"));
            model.transform.position=Vector3.right*10000;
            try
            {
                var building=model.GetComponent<Building>(); var water=model.GetComponent<GlassWaterFloorLight>();
                building.Bind(manager,new ChunkKey(0,0,0),0,9); yield return null;
                Assert.That(water.region.enabled,Is.False);
                building.ApplyStageImmediately(10); yield return null;
                Assert.That(water.region.enabled,Is.True); Assert.That(building.CanInteract,Is.False);
                building.ApplyStageImmediately(9); yield return null; Assert.That(water.region.enabled,Is.False);
                building.ApplyStageImmediately(10); yield return null; Assert.That(water.region.enabled,Is.True);
                building.Unbind(); yield return null; Assert.That(water.region.enabled,Is.False);
            }
            finally {Object.Destroy(model);Object.Destroy(owner);}
            yield return null;
        }
    }
}
#endif
