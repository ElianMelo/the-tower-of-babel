using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class RiverGlassGpuEditModeTests
    {
        [Test]
        public void CompletedTower_RendersUnreadableSharedMeshesWithoutCpuExpansion()
        {
            var original=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewPreviewScene();
            var target=new RenderTexture(64,64,24);var oldTarget=RenderTexture.active;
            var source=new Source();
            var mesh=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset"));
            mesh.name="GPU-only shared tower river slab";mesh.UploadMeshData(true);
            var feature=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset").rendererFeatures.OfType<RiverGlassRendererFeature>().Single();
            try
            {
                var go=new GameObject("Complete tower rendering regression camera");SceneManager.MoveGameObjectToScene(go,scene);
                var camera=go.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.targetTexture=target;camera.cullingMask=1<<31;
                camera.transform.position=new Vector3(0,275,0);camera.transform.rotation=Quaternion.Euler(90,0,0);camera.farClipPlane=400;
                for(int floor=0;floor<45;floor++)
                {
                    var matrices=new Matrix4x4[425];
                    for(int i=0;i<matrices.Length;i++)matrices[i]=Matrix4x4.Translate(new Vector3((i%20-10)*4,floor*6,(i/20-10)*4));
                    var draw=new RiverGlassDraw {mesh=mesh,matrices=matrices,count=matrices.Length,floor=floor,height=floor*6};draw.Configure();source.draws.Add(draw);
                }
                RiverGlassRenderRegistry.Register(source);
                Assert.DoesNotThrow(()=>{camera.Render();camera.Render();},"Rendering must not read/duplicate mesh vertices or build per-instance geometry.");
                Assert.That(feature.LastSubmittedInstances,Is.EqualTo(19125));
                Assert.That(feature.LastDrawCalls,Is.EqualTo(47),"Only two floors receive the additional detail pass.");
                Assert.That(feature.LastDetailedFloors,Is.EqualTo(2));
                Assert.That(feature.CachedGeometryCount,Is.Zero);
                Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Any(m=>m.name.StartsWith("River glass shell — cached")),Is.False);
            }
            finally
            {
                RiverGlassRenderRegistry.Unregister(source);RenderTexture.active=oldTarget;
                EditorSceneManager.ClosePreviewScene(scene);SceneManager.SetActiveScene(original);target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(20)] [TestCase(40)]
        public void ContainedWater_MotionRemainsVisibleAtGameplayDistance(int distance)
        {
            var original=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewPreviewScene();
            var oldTarget=RenderTexture.active;var target=new RenderTexture(256,256,24,RenderTextureFormat.ARGBHalf);
            var pixels=new Texture2D(256,256,TextureFormat.RGBAFloat,false);
            var feature=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset").rendererFeatures.OfType<RiverGlassRendererFeature>().Single();
            float oldTime=feature.previewTime,oldDay=Shader.GetGlobalFloat("_AeroDaylight");var source=new Source();
            try
            {
                var root=new GameObject("Distant liquid motion camera");SceneManager.MoveGameObjectToScene(root,scene);
                var camera=root.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.targetTexture=target;
                camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.2f,.25f,.3f);
                camera.transform.position=new Vector3(0,distance*.4f,-distance*.92f);camera.transform.LookAt(Vector3.zero);
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset");
                var matrices=new List<Matrix4x4>();for(int x=-2;x<=1;x++)for(int z=-1;z<=2;z++)matrices.Add(Matrix4x4.Translate(new Vector3(x*4,0,z*4)));
                var draw=new RiverGlassDraw {mesh=mesh,count=matrices.Count,matrices=matrices.ToArray()};draw.Configure();source.draws.Add(draw);
                RiverGlassRenderRegistry.Register(source);Shader.SetGlobalFloat("_AeroDaylight",1);
                Color[] Sample(float time)
                {
                    feature.previewTime=time;camera.Render();camera.Render();RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();return pixels.GetPixels();
                }
                var before=Sample(2);var after=Sample(6);
                Assert.That(before.Where((c,i)=>Mathf.Abs(c.grayscale-after[i].grayscale)>.008f).Count(),Is.GreaterThan(100),
                    "The actual water pixels must visibly move, including in the reduced distant-refraction path.");
                Assert.That(feature.LastRefractedLayers,Is.LessThanOrEqualTo(1));
            }
            finally
            {
                RiverGlassRenderRegistry.Unregister(source);feature.previewTime=oldTime;Shader.SetGlobalFloat("_AeroDaylight",oldDay);
                RenderTexture.active=oldTarget;EditorSceneManager.ClosePreviewScene(scene);SceneManager.SetActiveScene(original);
                target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
            }
        }

        private sealed class Source : IRiverGlassDrawSource
        {
            public readonly List<RiverGlassDraw> draws = new();
            public void CollectRiverGlass(Camera camera, Plane[] planes, List<RiverGlassDraw> destination) => destination.AddRange(draws);
        }

        [TestCase(false,1)] [TestCase(true,1)] [TestCase(false,3)] [TestCase(true,3)] [TestCase(false,5)]
        public void GlassAperture_TransmitsObjectsFromEitherSideAndCompositesStackedFloors(bool underside,int layers)
        {
            var original=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewPreviewScene();
            var oldTarget=RenderTexture.active; bool oldAsync=ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation=false;
            var target=new RenderTexture(128,128,24,RenderTextureFormat.ARGBHalf);
            var pixels=new Texture2D(128,128,TextureFormat.RGBAFloat,false);
            var feature=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset").rendererFeatures.OfType<RiverGlassRendererFeature>().Single();
            float oldTime=feature.previewTime; feature.previewTime=2;
            var source=new Source(); var materials=new List<Material>();
            try
            {
                var cameraRoot=new GameObject("Glass transmission camera");var camera=cameraRoot.AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(cameraRoot,scene);
                camera.enabled=false;camera.scene=scene;camera.targetTexture=target;camera.cullingMask=1<<31;
                camera.orthographic=true;camera.orthographicSize=2.3f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.transform.position=Vector3.up*(underside?-4:4);camera.transform.rotation=Quaternion.Euler(underside?-90:90,0,0);
                var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);marker.layer=31;
                SceneManager.MoveGameObjectToScene(marker,scene);
                marker.transform.position=Vector3.up*(underside?1.5f:-1.5f);marker.transform.localScale=new Vector3(4,.2f,4);
                var markerMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));markerMaterial.color=new Color(.8f,.12f,.06f);
                marker.GetComponent<Renderer>().sharedMaterial=markerMaterial;materials.Add(markerMaterial);
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset");
                for(int i=0;i<layers;i++)
                {
                    float height=(underside?1:-1)*i*.25f;
                    var matrix=Matrix4x4.TRS(new Vector3(-2,height,2),Quaternion.identity,Vector3.one);
                    var draw=new RiverGlassDraw{mesh=mesh,matrices=new[]{matrix},count=1,floor=i,height=height,radial=0};
                    draw.Configure();source.draws.Add(draw);
                    var panel=new GameObject("Opaque surround with real channel aperture");panel.layer=31;
                    SceneManager.MoveGameObjectToScene(panel,scene);
                    panel.transform.position=new Vector3(-2,height,2);panel.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var mat=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Environment/Lighting/GlassWaterFloor.mat"));
                    mat.SetFloat("_RiverStage",10);mat.SetFloat("_RiverFloorIndex",i);mat.SetFloat("_RadialFloor",0);
                    panel.AddComponent<MeshRenderer>().sharedMaterial=mat;materials.Add(mat);
                }
                RiverGlassRenderRegistry.Register(source);
                camera.Render();camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();
                Color color=pixels.GetPixel(64,64);
                Assert.That(color.r,Is.GreaterThan(.2f),"The red marker must remain visible through actual color/depth apertures.");
                Assert.That(color.r,Is.GreaterThan(color.g*1.5f),"A painted blue bed must not replace the marker.");
                Assert.That(feature.LastDrawCalls,Is.EqualTo(layers+Mathf.Min(layers,feature.profile.detailedFloorLimit)));
                Assert.That(feature.LastRefractedLayers,Is.LessThanOrEqualTo(1));
                Assert.That(feature.LastRefractedLayers,Is.GreaterThan(0));
                Assert.That(feature.LastBackgroundWidth,Is.EqualTo(64));
                Assert.That(feature.LastBackgroundHeight,Is.EqualTo(64));
                Assert.That(ShaderUtil.GetShaderMessages(feature.surfaceShader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),Is.Empty);
            }
            finally
            {
                RiverGlassRenderRegistry.Unregister(source);feature.previewTime=oldTime;RenderTexture.active=oldTarget;
                EditorSceneManager.ClosePreviewScene(scene);SceneManager.SetActiveScene(original);
                foreach(var material in materials)Object.DestroyImmediate(material);
                target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);ShaderUtil.allowAsyncCompilation=oldAsync;
            }
        }

        [Test]
        public void CompletedChannelBank_HidesBuriedVerticalTileWalls()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var oldTarget=RenderTexture.active;var target=new RenderTexture(64,64,24);
            var pixels=new Texture2D(64,64,TextureFormat.RGB24,false);
            var material=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Environment/Lighting/GlassWaterFloor.mat"));
            try
            {
                var go=new GameObject("Internal bank wall");SceneManager.MoveGameObjectToScene(go,scene);
                go.transform.position=new Vector3(12,0,2);go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset");
                go.AddComponent<MeshRenderer>().sharedMaterial=material;
                material.SetFloat("_RadialFloor",2);material.SetFloat("_RiverFloorIndex",0);material.SetColor("_BaseColor",Color.green);
                var cameraGo=new GameObject("Buried wall camera");SceneManager.MoveGameObjectToScene(cameraGo,scene);
                var camera=cameraGo.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.targetTexture=target;
                camera.orthographic=true;camera.orthographicSize=.12f;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=Color.red;camera.transform.position=new Vector3(15.1f,-.1f,-6);
                Color Pixel()
                {
                    camera.Render();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();return pixels.GetPixel(32,32);
                }
                material.SetFloat("_RiverStage",9);Color partial=Pixel();
                material.SetFloat("_RiverStage",10);Color complete=Pixel();
                Assert.That(complete.r,Is.GreaterThan(.7f));Assert.That(complete.g,Is.LessThan(.1f));
                Assert.That(Mathf.Abs(complete.r-partial.r)+Mathf.Abs(complete.g-partial.g),Is.GreaterThan(.2f),"The control must show the internal wall before completion.");
            }
            finally
            {
                RenderTexture.active=oldTarget;EditorSceneManager.ClosePreviewScene(scene);
                target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);Object.DestroyImmediate(material);
            }
        }
    }
}
