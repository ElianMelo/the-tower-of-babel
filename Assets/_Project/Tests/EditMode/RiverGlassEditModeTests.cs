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
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TowerOfBabel.World.Tests
{
    public sealed class RiverGlassEditModeTests
    {
        [Test]
        public void ProjectionShadows_OnlyIncludeLocalFloorsAndStopWhenTheLightIsDisabled()
        {
            var root=new GameObject("Local river shadow budget");var view=new GameObject("River shadow camera");
            var light=root.AddComponent<Light>();light.type=LightType.Spot;light.range=8;light.shadows=LightShadows.Hard;
            var camera=view.AddComponent<Camera>();camera.enabled=false;
            try
            {
                RiverGlassRenderRegistry.RegisterProjection(light);
                Assert.That(RiverGlassRenderRegistry.NeedsProjectionShadows(new Bounds(Vector3.up*6,Vector3.one),camera,0),Is.True);
                Assert.That(RiverGlassRenderRegistry.NeedsProjectionShadows(new Bounds(Vector3.up*120,Vector3.one*4),camera,0),Is.False,
                    "Completing distant floors must not add them to a local river light's shadow workload.");
                light.enabled=false;
                Assert.That(RiverGlassRenderRegistry.NeedsProjectionShadows(new Bounds(Vector3.zero,Vector3.one),camera,0),Is.False);
            }
            finally {RiverGlassRenderRegistry.UnregisterProjection(light);Object.DestroyImmediate(root);Object.DestroyImmediate(view);}
        }

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
            camera.transform.position = new Vector3(0,2,4); camera.transform.rotation = Quaternion.Euler(0,180,0); camera.scene = scene;
            RiverGlassLighting lighting = null;
            try
            {
                var manager = root.AddComponent<ChunkManager>();
                var assets = new List<ChunkAssetData>();
                for (int i=0;i<4;i++) assets.Add(new ChunkAssetData(i,TowerAssetType.GlassWaterFloor,new Vector3(-2,0,2-i*8),Quaternion.identity,Vector3.one,10));
                var chunk = (ChunkSceneCache)Activator.CreateInstance(typeof(ChunkSceneCache),Private,null,new object[]{new ChunkKey(0,0,0),assets},null);
                typeof(ChunkManager).GetField("cachedChunks",Private).SetValue(manager,new List<ChunkSceneCache>{chunk});
                lighting=root.AddComponent<RiverGlassLighting>(); lighting.profile=Profile;
                lighting.cookieShader=Shader.Find("Hidden/TowerOfBabel/River caustic cookie"); root.SetActive(true);
                lighting.Sample(camera,0,1); lighting.Sample(camera,.1f,1);
                Assert.That(lighting.ActiveLightCount,Is.Zero,"New patches must begin in their randomized quiet interval.");
                float sampleTime=.1f;
                for(int frame=0;frame<240&&lighting.ActiveLightCount==0;frame++)
                {sampleTime+=1f/30;lighting.Sample(camera,sampleTime,1f/30);}
                Assert.That(lighting.ActiveLightCount,Is.InRange(2,RiverGlassLighting.MaximumActivePatches*2)); Assert.That(lighting.AllocatedCookieCount,Is.EqualTo(1));
                var lights=root.GetComponentsInChildren<Light>();
                Assert.That(lights.All(l=>l.shadows==LightShadows.Hard&&l.shadowCustomResolution==512),Is.True);
                Assert.That(lights.All(l=>l.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier==UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom),Is.True,
                    "URP must honor the custom resolution instead of silently using its 256px low tier.");
                Assert.That(lights.Length,Is.EqualTo(RiverGlassLighting.MaximumLights));
                Assert.That(lights.All(l=>l.spotAngle>=80&&l.spotAngle<=90&&l.range==14&&(l.cullingMask&RiverGlassLighting.WaterLayerMask)==0),Is.True);
                var cookie=lights[0].cookie as RenderTexture; var before=CookiePixels(cookie);
                Assert.That(cookie.width,Is.EqualTo(2048));Assert.That(cookie.height,Is.EqualTo(2048));Assert.That(lights.All(l=>l.cookie==cookie),Is.True);
                uint version=cookie.updateCount;lighting.Sample(camera,sampleTime+.001f,0);
                Assert.That(cookie.updateCount,Is.EqualTo(version),"Higher resolution must retain the 8Hz refresh budget.");
                var rotations=lights.Select(l=>l.transform.rotation).ToArray();
                int starts=lighting.WindowStartCount;float lastStart=lighting.LastWindowStartedAt;int quietFrames=0;
                // Long enough to include one complete quiet interval (30-45 s apart since presentation 13).
                int frames=Mathf.CeilToInt((Profile.projectionGlobalQuietInterval.y+Profile.projectionGlobalQuietDuration.y+2)*30);
                for(int frame=1;frame<=frames;frame++)
                {
                    lighting.Sample(camera,sampleTime+frame/30f,1f/30);
                    Assert.That(lighting.RunningWindowCount,Is.InRange(0,2));
                    Assert.That(lighting.ActiveLightCount,Is.InRange(0,4));
                    if(lighting.GlobalVisibility==0){quietFrames++;Assert.That(lighting.ActiveLightCount,Is.Zero);}
                    if(lighting.WindowStartCount!=starts)
                    {
                        Assert.That(lighting.WindowStartsAllowed,Is.True);
                        Assert.That(lighting.LastWindowStartedAt-lastStart,Is.GreaterThanOrEqualTo(Profile.projectionStartSpacing.x-.001f));
                        starts=lighting.WindowStartCount;lastStart=lighting.LastWindowStartedAt;
                    }
                }
                Assert.That(quietFrames,Is.GreaterThanOrEqualTo(30),"The sequence must contain a complete gentle quiet interval.");
                var after=CookiePixels(cookie);
                Assert.That(lights.Select((l,i)=>Quaternion.Angle(l.transform.rotation,rotations[i])).Max(),Is.LessThan(.001f),
                    "Projectors stay fixed while cutouts stream across their footprint.");
                Assert.That(before.Where((v,i)=>!v.Equals(after[i])).Count(),Is.GreaterThan(50),"The cookie must advance, not just the CPU clock.");
                for(int i=0;i<4;i++)manager.SetAssetStage(new ChunkKey(0,0,0),i,9);
                lighting.Sample(camera,sampleTime+frames/30f+.2f,1); Assert.That(lighting.ActiveLightCount,Is.Zero);
                lighting.enabled=false; Assert.That(lighting.AllocatedCookieCount,Is.Zero);
                Assert.That(root.GetComponentsInChildren<Light>(true),Is.Empty);
            }
            finally { if(lighting!=null)lighting.Release(); EditorSceneManager.ClosePreviewScene(scene);SceneManager.SetActiveScene(original); }
        }

        [TestCase(false,.8f,3f,false)] [TestCase(true,.8f,3f,false)]
        [TestCase(false,1.2f,5f,false)] [TestCase(true,1.2f,5f,false)]
        [TestCase(false,2.5f,9f,false)] [TestCase(true,2.5f,9f,false)]
        [TestCase(false,2.5f,9f,true)] [TestCase(true,2.5f,9f,true)]
        public void ProjectionNativeLight_IlluminatesReceiversAndSolidWallsBlockIt(bool below,float reach,float height,bool horizontal)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var root=new GameObject("Native river light fixture");root.SetActive(false);root.layer=31;SceneManager.MoveGameObjectToScene(root,scene);
            var cameraRoot=new GameObject("Native caustic camera");SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.cullingMask=1<<31;
            float y=below?-height:height;
            camera.transform.position=new Vector3(4,y+(horizontal?(below?3:-3):0),-1);camera.transform.LookAt(new Vector3(0,y,reach));
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
                receiver.transform.position=new Vector3(0,y,reach);receiver.transform.localScale=horizontal?new Vector3(5,.2f,5):new Vector3(5,4,.2f);receiver.GetComponent<Renderer>().sharedMaterial=material;
                float Brightness()
                {
                    camera.Render();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();
                    return pixels.GetPixels(120,120,16,16).Average(c=>c.grayscale);
                }
                float dark=Brightness();lighting.Sample(camera,0,1);float clock=0;
                while(lighting.WindowStartCount==0&&clock<8){clock+=1f/30;lighting.Sample(camera,clock,1f/30);}
                float litTime=lighting.LastWindowStartedAt+lighting.LastWindowDuration*.5f;
                while(clock<litTime){float next=Mathf.Min(litTime,clock+1f/30);lighting.Sample(camera,next,next-clock);clock=next;}
                float lit=Brightness();
                var review=new Texture2D(256,256,TextureFormat.RGB24,false);review.SetPixels(pixels.GetPixels());review.Apply();
                System.IO.File.WriteAllBytes($"Documentation/FrutigerAero/Lighting/Previews/River-Receiver-{(below?"Below":"Above")}-{reach:0}{(horizontal?"-Floor":"")}.png",review.EncodeToPNG());Object.DestroyImmediate(review);
                Assert.That(lit-dark,Is.GreaterThan(.01f),"The pooled cookie must actually illuminate a native URP receiver.");
                if(reach*reach+height*height>64)
                {
                    foreach(var light in root.GetComponentsInChildren<Light>())light.range=8;
                    float previousReach=Brightness();
                    Assert.That(previousReach-dark,Is.LessThan((lit-dark)*.25f),"The newly affected receiver must lie beyond the previous light range.");
                    lighting.Sample(camera,litTime+.1f,0);Brightness();
                }
                var before=pixels.GetPixels();lighting.Sample(camera,litTime+.8f,.7f);Brightness();var after=pixels.GetPixels();
                Assert.That(before.Where((v,i)=>Mathf.Abs(v.grayscale-after[i].grayscale)>.005f).Count(),Is.GreaterThan(20),
                    "Moving light must reach rendered receiver pixels, not merely update a cookie texture.");
                float unblocked=Brightness();
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(blocker,scene);blocker.layer=31;
                blocker.transform.position=new Vector3(0,y*.5f,reach*.5f);blocker.transform.localScale=horizontal?new Vector3(4,.3f,5):new Vector3(1.3f,4,.3f);blocker.GetComponent<Renderer>().sharedMaterial=material;
                float blocked=Brightness();
                Assert.That(blocked-dark,Is.LessThan((unblocked-dark)*.65f),"A solid wall between source and receiver must stop projection.");
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
