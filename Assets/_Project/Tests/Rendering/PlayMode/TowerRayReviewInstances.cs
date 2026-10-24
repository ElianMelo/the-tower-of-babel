#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TowerOfBabel.Rendering;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.Rendering.Tests
{
    // Review geometry reuses source meshes and production radial instancing instead of saving duplicated vertex buffers.
    public sealed class TowerRayReviewInstances : MonoBehaviour
    {
        [Serializable] private sealed class Snapshot { public ChunkAssetData[] assets; }
        private sealed class Batch {public Mesh Mesh;public Material Material;public Matrix4x4[] Matrices;}
        private readonly List<Batch> batches=new();
        public bool CastShadows;
        public Camera Camera;
        public TowerAtmosphereBridge Bridge;
        public void Initialize()
        {
            var snapshot=JsonUtility.FromJson<Snapshot>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Tests/Fixtures/TowerRaySnapshot.json").text);
            var shadowRoot=new GameObject("Production bounded shadow cache");shadowRoot.layer=31;shadowRoot.transform.SetParent(transform);shadowRoot.SetActive(false);
            var manager=shadowRoot.AddComponent<ChunkManager>();manager.enabled=false;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var cache=(List<ChunkSceneCache>)typeof(ChunkManager).GetField("cachedChunks",flags).GetValue(manager);
            foreach(var chunk in snapshot.assets.GroupBy(a=>new ChunkKey(Mathf.FloorToInt((a.Position.y+.001f)/6),Mathf.FloorToInt(a.Position.x/32),Mathf.FloorToInt(a.Position.z/32))))
                cache.Add((ChunkSceneCache)Activator.CreateInstance(typeof(ChunkSceneCache),flags,null,new object[]{chunk.Key,chunk.ToList()},null));
            typeof(ChunkManager).GetField("radialFloors",flags).SetValue(manager,true);
            typeof(ChunkManager).GetMethod("RebuildChunkLookup",flags).Invoke(manager,null);
            foreach(var group in snapshot.assets.GroupBy(a=>a.AssetType))
            {
                string name=group.Key switch {TowerAssetType.Floor=>"Floor_Tile",TowerAssetType.Stair=>"Step_Tile",_=>group.Key.ToString()};
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/"+name+".prefab");
                manager.AssetPrefabs.SetPrefab(group.Key,prefab);
                var mesh=prefab.GetComponent<Building>().GetStageMesh(10);var material=new Material(prefab.GetComponent<Renderer>().sharedMaterial){enableInstancing=true};
                if(group.Key.IsFloor())TowerFloorLayout.ConfigureMaterial(material,true,Vector2.zero);
                material.SetFloat("_ReceiveShadows",1);material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
                var all=group.Select(a=>a.ObjectToWorld).ToArray();
                for(int offset=0;offset<all.Length;offset+=InstancedFarChunkRenderer.MaximumInstancesPerDraw)
                    batches.Add(new Batch {Mesh=mesh,Material=material,Matrices=all.Skip(offset).Take(InstancedFarChunkRenderer.MaximumInstancesPerDraw).ToArray()});
            }
            Bridge=shadowRoot.AddComponent<TowerAtmosphereBridge>();Bridge.chunks=manager;Bridge.atmosphere=GetComponent<AeroTowerAtmosphere>();shadowRoot.SetActive(true);Bridge.Refresh(0,0);
        }
        private void LateUpdate()
        {
            foreach(var batch in batches)
            {
                var parameters=new RenderParams(batch.Material){camera=Camera,layer=31,worldBounds=new Bounds(new Vector3(0,3,0),new Vector3(200,14,200)),shadowCastingMode=ShadowCastingMode.Off,receiveShadows=CastShadows,reflectionProbeUsage=ReflectionProbeUsage.Off,lightProbeUsage=LightProbeUsage.Off};
                Graphics.RenderMeshInstanced(parameters,batch.Mesh,0,batch.Matrices);
            }
        }
        private void OnDestroy(){foreach(var material in batches.Select(b=>b.Material).Distinct())Destroy(material);batches.Clear();}
    }
}
#endif
