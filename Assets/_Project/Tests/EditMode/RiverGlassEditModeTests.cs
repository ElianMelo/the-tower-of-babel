using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TowerOfBabel.World.Tests
{
    public sealed class RiverGlassEditModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static RiverGlassProfile Profile => AssetDatabase.LoadAssetAtPath<RiverGlassProfile>("Assets/_Project/Environment/Lighting/RiverGlass.asset");
        private static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/GlassWaterFloor.prefab");

        [TestCase(0)] [TestCase(1)] [TestCase(20)] [TestCase(42)]
        public void ExactChannelMask_FollowsBothRingsSpokesAndPoolAtEveryFloor(int floor)
        {
            Vector4 layout = Profile.layout;
            float wall = Mathf.Max(3, layout.x - layout.y * (Mathf.Floor(floor * .5f) + 1));
            foreach (float ring in new[] { .5f, .88f }) for (int i = 0; i < 180; i++)
            {
                float angle = i * Mathf.PI / 90;
                var p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * wall * ring;
                Assert.That(GlassWaterFloorLayout.ChannelDistance(p, floor, layout), Is.LessThan(0));
            }
            Assert.That(GlassWaterFloorLayout.ChannelDistance(Vector2.zero, floor, layout), Is.LessThan(0));
            Assert.That(GlassWaterFloorLayout.ChannelDistance(new Vector2(wall*.7f, 0), floor, layout), Is.LessThan(0));
            Assert.That(GlassWaterFloorLayout.ChannelDistance(Vector2.one * wall*.34f, floor, layout), Is.GreaterThan(0));
        }

        [Test]
        public void OpticalPath_DependsOnWaterThicknessAndAngleAndRemainsFiniteAtGrazingAngles()
        {
            Assert.That(RiverGlassProfile.WaterPath(.2f, 1), Is.EqualTo(.2f));
            Assert.That(RiverGlassProfile.WaterPath(.2f, -.5f), Is.EqualTo(.4f));
            Assert.That(RiverGlassProfile.WaterPath(.2f, 0), Is.LessThan(1.34f));
            Assert.That(RiverGlassRendererFeature.RefractionWeight(0, 100, 6), Is.EqualTo(1));
            Assert.That(RiverGlassRendererFeature.RefractionWeight(42, 100, 6), Is.Zero);
            Assert.That(RiverGlassRendererFeature.RefractionWeight(10, 10, 6), Is.Zero);
            Assert.That(RiverGlassRendererFeature.RefractionWeight(30, 100, 6), Is.InRange(0f, 1f));
        }

        [Test]
        public void FarSubmissions_KeepFloorIdentityBatchLimitAndCompletionState()
        {
            var root = new GameObject("River submission test"); root.SetActive(false);
            var cameraRoot = new GameObject("River submission camera"); var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false;
            try
            {
                var manager = root.AddComponent<ChunkManager>(); manager.enabled = false;
                manager.AssetPrefabs.SetPrefab(TowerAssetType.GlassWaterFloor, Prefab);
                var far = root.AddComponent<InstancedFarChunkRenderer>();
                foreach (int floor in new[] { 0, 1 })
                {
                    var assets = new List<ChunkAssetData>();
                    for (int i = 0; i < 1024; i++) assets.Add(new ChunkAssetData(i, TowerAssetType.GlassWaterFloor,
                        new Vector3(i%32, floor*6, i/32), Quaternion.identity, Vector3.one, i==0?(byte)9:(byte)10));
                    far.LoadChunk(new FarChunkSnapshot(new ChunkKey(floor,0,0), assets));
                }
                var draws = new List<RiverGlassDraw>(); far.CollectRiverGlass(camera, null, draws);
                Assert.That(draws.Sum(d=>d.count), Is.EqualTo(2046));
                Assert.That(draws.All(d=>d.count <= 511), Is.True);
                Assert.That(draws.Select(d=>d.floor).Distinct().Count(), Is.EqualTo(2));
                foreach (var draw in draws) Assert.That(draw.matrices.All(m=>Mathf.Abs(m.m13-draw.floor*6)<.001f), Is.True);
                far.RemoveChunk(new ChunkKey(0,0,0)); draws.Clear(); far.CollectRiverGlass(camera,null,draws);
                Assert.That(draws.Sum(d=>d.count), Is.EqualTo(1023));
                far.SetVisible(false); draws.Clear(); far.CollectRiverGlass(camera,null,draws); Assert.That(draws, Is.Empty);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraRoot); }
        }

        [Test]
        public void DestroyedDrawProvider_IsPrunedBeforeTheNextCamera()
        {
            var root = new GameObject("Destroyed provider"); root.SetActive(false);
            var cameraRoot = new GameObject("Provider cleanup camera"); var camera = cameraRoot.AddComponent<Camera>(); camera.enabled=false;
            var far = root.AddComponent<InstancedFarChunkRenderer>(); RiverGlassRenderRegistry.Register(far);
            Object.DestroyImmediate(root);
            try { Assert.DoesNotThrow(()=>RiverGlassRenderRegistry.Collect(camera,null,new List<RiverGlassDraw>())); }
            finally { Object.DestroyImmediate(cameraRoot); }
        }

        [Test]
        public void ProjectionPool_IsBoundedAnimatedAndRemovedOnRollbackAndDisable()
        {
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Projection pool fixture"); root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root,scene);
            var cameraRoot = new GameObject("Projection camera"); var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false;
            SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            camera.transform.position = new Vector3(3,2,0); camera.scene = scene;
            RiverGlassLighting lighting = null;
            try
            {
                var manager = root.AddComponent<ChunkManager>();
                var assets = new List<ChunkAssetData>();
                for (int i=0;i<4;i++) assets.Add(new ChunkAssetData(i,TowerAssetType.GlassWaterFloor,new Vector3(i*4-8,0,2),Quaternion.identity,Vector3.one,10));
                var chunk = (ChunkSceneCache)Activator.CreateInstance(typeof(ChunkSceneCache),Private,null,new object[]{new ChunkKey(0,0,0),assets},null);
                typeof(ChunkManager).GetField("cachedChunks",Private).SetValue(manager,new List<ChunkSceneCache>{chunk});
                lighting=root.AddComponent<RiverGlassLighting>(); lighting.profile=Profile;
                lighting.cookieShader=Shader.Find("Hidden/TowerOfBabel/River caustic cookie"); root.SetActive(true);
                lighting.Sample(camera,0,1); lighting.Sample(camera,.3f,1);
                Assert.That(lighting.ActiveLightCount,Is.EqualTo(4)); Assert.That(lighting.AllocatedCookieCount,Is.EqualTo(2));
                var lights=root.GetComponentsInChildren<Light>();
                Assert.That(lights.All(l=>l.shadows==LightShadows.Hard&&l.shadowCustomResolution==256),Is.True);
                var cookie=lights[0].cookie as RenderTexture; var before=CookiePixels(cookie);
                lighting.Sample(camera,4,1); var after=CookiePixels(cookie);
                Assert.That(before.Where((v,i)=>!v.Equals(after[i])).Count(),Is.GreaterThan(50),"The cookie must advance, not just the CPU clock.");
                for(int i=0;i<4;i++)manager.SetAssetStage(new ChunkKey(0,0,0),i,9);
                lighting.Sample(camera,4.3f,1); Assert.That(lighting.ActiveLightCount,Is.Zero);
                lighting.enabled=false; Assert.That(lighting.AllocatedCookieCount,Is.Zero);
                Assert.That(root.GetComponentsInChildren<Light>(true),Is.Empty);
            }
            finally { if(lighting!=null)lighting.Release(); EditorSceneManager.ClosePreviewScene(scene);SceneManager.SetActiveScene(original); }
        }

        [TestCase(false)] [TestCase(true)]
        public void ProjectionNativeLight_IlluminatesReceiversAndSolidWallsBlockIt(bool below)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var root=new GameObject("Native river light fixture");root.SetActive(false);root.layer=31;SceneManager.MoveGameObjectToScene(root,scene);
            var cameraRoot=new GameObject("Native caustic camera");SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.cullingMask=1<<31;
            camera.transform.position=new Vector3(4,below?-2:2,-1);camera.transform.LookAt(new Vector3(0,below?-2:2,3));
            var target=new RenderTexture(256,256,24,RenderTextureFormat.ARGBHalf);camera.targetTexture=target;
            var pixels=new Texture2D(256,256,TextureFormat.RGBAFloat,false);var oldTarget=RenderTexture.active;
            var oldLights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.enabled).ToArray();
            foreach(var light in oldLights)light.enabled=false;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color=Color.white;
            RiverGlassLighting lighting=null;
            try
            {
                var manager=root.AddComponent<ChunkManager>();
                var assets=new List<ChunkAssetData>{new ChunkAssetData(0,TowerAssetType.GlassWaterFloor,new Vector3(-2,0,2),Quaternion.identity,Vector3.one,10)};
                var chunk=(ChunkSceneCache)Activator.CreateInstance(typeof(ChunkSceneCache),Private,null,new object[]{new ChunkKey(0,0,0),assets},null);
                typeof(ChunkManager).GetField("cachedChunks",Private).SetValue(manager,new List<ChunkSceneCache>{chunk});
                lighting=root.AddComponent<RiverGlassLighting>();lighting.profile=Profile;lighting.cookieShader=Shader.Find("Hidden/TowerOfBabel/River caustic cookie");root.SetActive(true);
                var receiver=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(receiver,scene);receiver.layer=31;
                receiver.transform.position=new Vector3(0,below?-2:2,3);receiver.transform.localScale=new Vector3(5,4,.2f);receiver.GetComponent<Renderer>().sharedMaterial=material;
                float Brightness()
                {
                    camera.Render();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();
                    return pixels.GetPixels(120,120,16,16).Average(c=>c.grayscale);
                }
                float dark=Brightness();lighting.Sample(camera,1,1);lighting.Sample(camera,1.3f,1);float lit=Brightness();
                Assert.That(lit-dark,Is.GreaterThan(.01f),"The pooled cookie must actually illuminate a native URP receiver.");
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(blocker,scene);blocker.layer=31;
                blocker.transform.position=new Vector3(0,below?-1:1,1.5f);blocker.transform.localScale=new Vector3(1.3f,4,.3f);blocker.GetComponent<Renderer>().sharedMaterial=material;
                float blocked=Brightness();
                Assert.That(blocked-dark,Is.LessThan((lit-dark)*.65f),"A solid wall between source and receiver must stop projection.");
            }
            finally
            {
                if(lighting!=null)lighting.Release();RenderTexture.active=oldTarget;EditorSceneManager.ClosePreviewScene(scene);
                foreach(var light in oldLights)if(light!=null)light.enabled=true;
                target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);Object.DestroyImmediate(material);
            }
        }

        private static Color32[] CookiePixels(RenderTexture cookie)
        {
            var old=RenderTexture.active; var pixels=new Texture2D(cookie.width,cookie.height,TextureFormat.RGBA32,false);
            try {RenderTexture.active=cookie;pixels.ReadPixels(new Rect(0,0,cookie.width,cookie.height),0,0);pixels.Apply();return pixels.GetPixels32();}
            finally {RenderTexture.active=old;Object.DestroyImmediate(pixels);}
        }
    }
}
