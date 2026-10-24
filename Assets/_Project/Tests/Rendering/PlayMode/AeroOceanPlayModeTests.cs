#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroOceanPlayModeTests
    {
        private Scene runtimeScene;
        [UnityTest]
        public IEnumerator Ocean_FollowsTheViewWithoutAllocatingNewMeshesOrChangingWaterHeight()
        {
            var scene=SceneManager.CreateScene("Ocean runtime fixture");
            runtimeScene=scene;
            var mesh=AeroOceanSurface.CreateMesh();
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var target=new RenderTexture(64,64,24);
            var cameraObject=new GameObject("Ocean runtime camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
            cameraObject.tag="MainCamera";var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.targetTexture=target;
            var water=new GameObject("Ocean runtime surface",typeof(MeshFilter),typeof(MeshRenderer),typeof(AeroOceanSurface));SceneManager.MoveGameObjectToScene(water,scene);
            water.transform.position=Vector3.down;water.GetComponent<MeshFilter>().sharedMesh=mesh;water.GetComponent<Renderer>().sharedMaterial=material;
            try
            {
                foreach(var position in new[]{new Vector3(10000,240,-8000),new Vector3(-75000,350,93000)})
                {
                    camera.transform.position=position;yield return null;yield return null;
                    float grid=AeroOceanSurface.NearExtent*2/AeroOceanSurface.NearCells;
                    Assert.That(Mathf.Abs(water.transform.position.x-position.x),Is.LessThanOrEqualTo(grid));
                    Assert.That(Mathf.Abs(water.transform.position.z-position.z),Is.LessThanOrEqualTo(grid));
                    Assert.That(water.transform.position.y,Is.EqualTo(-1));
                    Assert.That(water.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));
                    Assert.That(camera.farClipPlane,Is.GreaterThanOrEqualTo(AeroOceanSurface.MinimumFarClip));
                }
            }
            finally
            {
                camera.enabled=false;camera.targetTexture=null;
                Object.Destroy(mesh);Object.Destroy(material);Object.Destroy(target);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(runtimeScene.IsValid() && runtimeScene.isLoaded) yield return SceneManager.UnloadSceneAsync(runtimeScene);
        }
    }
}
#endif
