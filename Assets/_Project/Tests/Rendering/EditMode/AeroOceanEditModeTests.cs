using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroOceanEditModeTests
    {
        [Test]
        public void Mesh_IsSeamlessUpwardFacingAndLighterThanTheOldPlane()
        {
            var mesh=AeroOceanSurface.CreateMesh();
            try
            {
                Assert.That(mesh.vertexCount,Is.LessThan(10201));
                Assert.That(mesh.subMeshCount,Is.EqualTo(1));
                var vertices=mesh.vertices; var triangles=mesh.triangles;
                var edges=new Dictionary<(int,int),int>();
                void Edge(int a,int b) {var key=a<b?(a,b):(b,a); edges.TryGetValue(key,out int n);edges[key]=n+1;}
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                    Assert.That(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).y,Is.GreaterThan(0));
                    Edge(a,b);Edge(b,c);Edge(c,a);
                }
                int boundary=0;
                foreach(var edge in edges)
                {
                    Assert.That(edge.Value,Is.InRange(1,2));
                    if(edge.Value!=1) continue;
                    boundary++;
                    foreach(int vertex in new[]{edge.Key.Item1,edge.Key.Item2})
                        Assert.That(Mathf.Max(Mathf.Abs(vertices[vertex].x),Mathf.Abs(vertices[vertex].z)),Is.EqualTo(AeroOceanSurface.OuterExtent));
                }
                Assert.That(boundary,Is.EqualTo(AeroOceanSurface.NearCells*4));
            }
            finally {Object.DestroyImmediate(mesh);}
        }

        [Test]
        public void SavedWater_UsesTheSeamlessMeshAndWorldSpaceMaterial()
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Environment/Lighting/AeroSeamlessOcean.asset");
            Assert.That(mesh,Is.Not.Null);Assert.That(mesh.bounds.extents.x,Is.GreaterThan(AeroOceanSurface.MinimumFarClip));
            var scene=SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Tower.unity",OpenSceneMode.Additive);
            try
            {
                AeroOceanSurface ocean=null;
                foreach(var root in scene.GetRootGameObjects()) if(ocean==null) ocean=root.GetComponentInChildren<AeroOceanSurface>(true);
                Assert.That(ocean,Is.Not.Null);Assert.That(ocean.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));
                Assert.That(ocean.transform.lossyScale,Is.EqualTo(Vector3.one));Assert.That(ocean.transform.position.y,Is.EqualTo(-1));
                Assert.That(ocean.GetComponent<Renderer>().sharedMaterial.GetFloat("_WorldSpaceUV"),Is.EqualTo(1));
            }
            finally {if(opened)EditorSceneManager.CloseScene(scene,true);}
        }

        [Test]
        public void RenderCoverage_HasNoSeamsOrExposedEdgeAtTowerHeightAndAfterMoving()
        {
            var scene=EditorSceneManager.NewPreviewScene();var mesh=AeroOceanSurface.CreateMesh();
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.SetColor("_BaseColor",Color.cyan);
            var rt=new RenderTexture(160,90,24);var pixels=new Texture2D(160,90,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try
            {
                var water=new GameObject("Ocean coverage fixture",typeof(MeshFilter),typeof(MeshRenderer),typeof(AeroOceanSurface));SceneManager.MoveGameObjectToScene(water,scene);
                water.GetComponent<MeshFilter>().sharedMesh=mesh;water.GetComponent<Renderer>().sharedMaterial=mat;water.transform.position=Vector3.down;
                var go=new GameObject("Ocean coverage camera",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);go.tag="MainCamera";
                var camera=go.GetComponent<Camera>();camera.scene=scene;camera.targetTexture=rt;camera.enabled=false;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.magenta;camera.transform.rotation=Quaternion.Euler(10,0,0);
                foreach(var position in new[]{new Vector3(0,2,0),new Vector3(65000,270,-65000),new Vector3(-120000,600,90000)})
                {
                    camera.transform.position=position;water.GetComponent<AeroOceanSurface>().PrepareForCamera(camera);
                    Assert.That(camera.farClipPlane,Is.GreaterThanOrEqualTo(AeroOceanSurface.MinimumFarClip));
                    camera.Render();camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,160,90),0,0);pixels.Apply();
                    for(int y=0;y<32;y++)for(int x=0;x<160;x++)
                        Assert.That(pixels.GetPixel(x,y).g,Is.GreaterThan(.5f),$"Exposed water edge/seam at {position}, pixel {x},{y}");
                }
            }
            finally
            {
                RenderTexture.active=old;EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(mesh);Object.DestroyImmediate(mat);Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);
            }
        }
    }
}
