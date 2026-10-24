using System;
using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroAtmosphereEditModeTests
    {
        private const string Prefab="Assets/_Project/Prefabs/Lighting/AquaProjectionLamp.prefab";
        private AeroSunShaftRendererFeature Shafts => AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset").rendererFeatures.OfType<AeroSunShaftRendererFeature>().Single();

        [Test]
        public void SavedAtmosphere_HasBoundedQualityAndHDRGlow()
        {
            Assert.That(Shafts.isActive,Is.True); Assert.That(Shafts.settings.samples,Is.InRange(8,24)); Assert.That(Shafts.settings.distance,Is.LessThanOrEqualTo(48));
            Assert.That(Shafts.shaftShader,Is.Not.Null);
            Assert.That(ShaderUtil.GetShaderMessages(Shafts.shaftShader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),Is.Empty);
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset");
            Assert.That(profile.TryGet<Bloom>(out var bloom),Is.True); Assert.That(bloom.intensity.overrideState,Is.True);
            Assert.That(bloom.intensity.value,Is.InRange(.3f,.7f)); Assert.That(bloom.clamp.value,Is.LessThanOrEqualTo(16));
            var core=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Environment/Lighting/AquaLampGlow.mat");
            Assert.That(core.IsKeywordEnabled("_EMISSION"),Is.True); Assert.That(core.GetColor("_EmissionColor").maxColorComponent,Is.GreaterThan(1));
            Assert.That(core.globalIlluminationFlags.HasFlag(MaterialGlobalIlluminationFlags.EmissiveIsBlack),Is.False);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            Assert.That(renderer.rendererFeatures.Single(x=>x.GetType().Name=="ScreenSpaceAmbientOcclusion").isActive,Is.False,
                "Depth-reconstructed AO caused black tile lines visible through the glass interface.");
        }

        [Test]
        public void Lamp_HasRealSpotProjectionAndAnimatedCookieAndReleasesItsTexture()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);
                var lamp=go.GetComponent<AeroProjectionLamp>(); var native=lamp.projection;
                Assert.That(native.type,Is.EqualTo(LightType.Spot)); Assert.That(native.range,Is.LessThanOrEqualTo(12));
                lamp.Sample(0); var texture=lamp.Cookie;
                Assert.That(texture.width,Is.EqualTo(128)); Assert.That(native.cookie,Is.SameAs(texture));
                var before=Read(texture); lamp.Sample(4); var after=Read(texture);
                Assert.That(Difference(before,after),Is.GreaterThan(.002));
                lamp.enabled=false; Assert.That(lamp.Cookie==null,Is.True); Assert.That(texture==null,Is.True); Assert.That(native.cookie,Is.Null);
            }
            finally {EditorSceneManager.ClosePreviewScene(scene); if(Camera.main!=null) AeroProjectionLamp.RefreshBudget(Camera.main.transform.position);}
        }

        [Test]
        public void Lamps_SelectOnlyFourNearestWithOneShadowAndReleaseDistantCookies()
        {
            var scene=EditorSceneManager.NewPreviewScene(); var lamps=new AeroProjectionLamp[6];
            try
            {
                for(int i=0;i<6;i++)
                {
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);
                    go.transform.position=new Vector3(10000+i*2,0,0); lamps[i]=go.GetComponent<AeroProjectionLamp>();
                }
                AeroProjectionLamp.RefreshBudget(new Vector3(10000,0,0));
                Assert.That(lamps.Count(x=>x.IsBudgetActive),Is.EqualTo(4));
                Assert.That(lamps.Count(x=>x.projection.shadows!=LightShadows.None),Is.EqualTo(1));
                Assert.That(lamps.Take(4).All(x=>x.Cookie!=null),Is.True); Assert.That(lamps.Skip(4).All(x=>x.Cookie==null),Is.True);
                AeroProjectionLamp.RefreshBudget(new Vector3(12000,0,0));
                Assert.That(lamps.All(x=>!x.IsBudgetActive && x.Cookie==null && !x.projection.enabled),Is.True);
            }
            finally {EditorSceneManager.ClosePreviewScene(scene); if(Camera.main!=null) AeroProjectionLamp.RefreshBudget(Camera.main.transform.position);}
        }

        [Test]
        public void SunRays_RenderVisibleScatteringAndRespondToOccluders()
        {
            WithShaftScene((camera,light,blocker,capture)=>
            {
                Shafts.SetActive(false); var before=capture(); Shafts.SetActive(true); var lit=capture();
                Assert.That(Difference(before,lit),Is.GreaterThan(.002),"The actual shaft pass must contribute light.");
                blocker.SetActive(true); var shadowed=capture();
                Assert.That(Mean(shadowed),Is.LessThan(Mean(lit)-.001),"A sunward occluder must reduce volumetric light.");
            });
        }

        [Test]
        public void SunRays_EnterOpenSidesBeneathASolidCoveredRoof()
        {
            WithShaftScene((camera,light,roof,capture)=>
            {
                camera.transform.position=new Vector3(0,1,0);
                light.transform.rotation=Quaternion.Euler(10,180,0);
                roof.transform.position=new Vector3(0,3,-10);
                roof.transform.localScale=new Vector3(40,.3f,48);
                roof.SetActive(true);
                Shafts.SetActive(true); var open=capture();
                var shutter=UnityEngine.Object.Instantiate(roof);
                SceneManager.MoveGameObjectToScene(shutter,camera.scene);
                shutter.name="Closed side aperture";
                shutter.transform.position=new Vector3(0,1.4f,14.1f);
                shutter.transform.localScale=new Vector3(40,3.1f,.3f);
                try
                {
                    var closed=capture();
                    Assert.That(Mean(open),Is.GreaterThan(Mean(closed)+.005f),
                        "Side openings must admit sunlight with the entire roof still covered.");
                }
                finally {UnityEngine.Object.DestroyImmediate(shutter);}
            });
        }

        [Test]
        public void SunRays_CoveredNoonBlocksOverheadAndWorldNoiseIsRepeatable()
        {
            WithShaftScene((camera,light,roof,capture)=>
            {
                Shafts.SetActive(true);light.transform.rotation=Quaternion.Euler(90,0,0);
                roof.transform.position=new Vector3(0,5,0);roof.transform.localScale=new Vector3(100,.4f,100);
                roof.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.On;
                roof.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor",Color.black);
                Shafts.previewTime=0;var clear=capture();roof.SetActive(true);var blocked=capture();
                Shafts.SetActive(false);var opaqueRoof=capture();Shafts.SetActive(true);
                Assert.That(Mean(blocked)-Mean(opaqueRoof),Is.LessThan(Mean(clear)*.15f),"Measure scattering separately from the visible ceiling material.");
                roof.SetActive(false);Shafts.previewTime=10;var moved=capture();
                Assert.That(Difference(clear,moved),Is.GreaterThan(.0001));
                Shafts.previewTime=0;camera.transform.Rotate(0,10,0);capture();camera.transform.rotation=Quaternion.identity;
                Assert.That(Difference(clear,capture()),Is.LessThan(.0001),"Returning to the same world view must reproduce the same shafts.");
                Assert.That(moved.All(c=>!float.IsNaN(c.r)&&!float.IsInfinity(c.r)&&c.r>=0&&c.g>=0&&c.b>=0),Is.True);
            });
            Shafts.previewTime=-1;
        }

        [Test]
        public void FillShafts_ShareNativeSpotShadowsAndRejectExteriorViews()
        {
            WithShaftScene((camera,sun,blocker,capture)=>
            {
                var fx=camera.scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<AeroTowerAtmosphere>()).Single();
                var profile=UnityEngine.Object.Instantiate(fx.raySettings);fx.raySettings=profile;profile.fillStrength=20;
                var fill=new GameObject("Native shadowed side fill",typeof(Light));SceneManager.MoveGameObjectToScene(fill,camera.scene);
                fx.bounceLights=new[]{fill.GetComponent<Light>()};
                fx.SetConstructionContext(true,new[]{new AeroArchOpening(1,0,new Vector3(0,2,13),Vector3.back)});
                sun.intensity=.001f;
                try
                {
                    for(int i=20;i<50;i++)fx.Sample(Vector3.up,i*.1f);
                    Shafts.SetActive(false);var baseline=capture();Shafts.SetActive(true);var open=capture();
                    Assert.That(Difference(baseline,open),Is.GreaterThan(.0005));
                    blocker.transform.position=new Vector3(0,2,10);blocker.transform.localScale=new Vector3(60,60,.5f);blocker.SetActive(true);
                    Assert.That(Mean(capture()),Is.LessThan(Mean(open)-.0003f),"The same native spotlight atlas must shadow the scattering.");
                    for(int i=50;i<80;i++)fx.Sample(new Vector3(150,2,0),i*.1f);
                    camera.transform.position=new Vector3(150,2,0);Shafts.SetActive(false);var exterior=capture();Shafts.SetActive(true);
                    Assert.That(Difference(exterior,capture()),Is.LessThan(.0001));
                }
                finally{fx.raySettings=Shafts.raySettings;UnityEngine.Object.DestroyImmediate(profile);}
            });
        }

        [Test]
        public void SunRays_ZeroIntensityAndMissingSunAreNoOps()
        {
            float intensity=Shafts.settings.intensity;
            try
            {
                WithShaftScene((camera,light,blocker,capture)=>
                {
                    Shafts.SetActive(false); var before=capture(); Shafts.SetActive(true); Shafts.settings.intensity=0;
                    Assert.That(Difference(before,capture()),Is.LessThan(.0005));
                    Shafts.settings.intensity=intensity; light.enabled=false;
                    Assert.That(Difference(before,capture()),Is.LessThan(.0005));
                });
            }
            finally {Shafts.settings.intensity=intensity;}
        }

        [Test]
        public void ProjectorCookie_ActuallyLightsStockSurfacesAndChangesTheRenderedPattern()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var rt=new RenderTexture(160,160,24); var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.SetColor("_BaseColor",Color.white);
            var water=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset").rendererFeatures.OfType<AeroLightRendererFeature>().Single(); bool active=water.isActive;
            bool async=ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation=false;
            GameObject Move(GameObject go) {SceneManager.MoveGameObjectToScene(go,scene); return go;}
            try
            {
                water.SetActive(false);
                var camera=Move(new GameObject("Projected pixel fixture")).AddComponent<Camera>(); camera.scene=scene;
                camera.enabled=false; camera.targetTexture=rt; camera.transform.position=new Vector3(0,1,-2); camera.farClipPlane=50;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
                var wall=Move(GameObject.CreatePrimitive(PrimitiveType.Cube)); wall.transform.position=new Vector3(0,1,4);
                wall.transform.localScale=new Vector3(12,12,.2f); wall.GetComponent<Renderer>().sharedMaterial=mat;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene); go.transform.position=new Vector3(-1.8f,0,-.7f);
                var lamp=go.GetComponent<AeroProjectionLamp>(); lamp.projection.transform.rotation=Quaternion.LookRotation(new Vector3(1,1,4)-lamp.projection.transform.position);
                lamp.Sample(0); lamp.projection.enabled=false;
                Color[] Capture() {camera.Render(); camera.Render(); return Read(rt);}
                var before=Capture(); lamp.projection.enabled=true; var lit=Capture();
                Assert.That(Difference(before,lit),Is.GreaterThan(.002),"A real spotlight must project onto stock Lit objects.");
                lamp.Sample(4); Assert.That(Difference(lit,Capture()),Is.GreaterThan(.001),"The URP cookie atlas must receive the updated animation.");
            }
            finally
            {
                water.SetActive(active); ShaderUtil.allowAsyncCompilation=async; EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Object.DestroyImmediate(mat); UnityEngine.Object.DestroyImmediate(rt);
                if(Camera.main!=null) AeroProjectionLamp.RefreshBudget(Camera.main.transform.position);
            }
        }

        private void WithShaftScene(Action<Camera,Light,GameObject,Func<Color[]>> check)
        {
            var oldSun=RenderSettings.sun;float oldClock=Shafts.previewTime;
            var sceneLights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.enabled).ToArray();foreach(var l in sceneLights)l.enabled=false;
            var scene=EditorSceneManager.NewPreviewScene();
            var oldTarget=RenderTexture.active; var rt=new RenderTexture(160,160,24); bool active=Shafts.isActive;
            var water=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset").rendererFeatures.OfType<AeroLightRendererFeature>().Single(); bool oldWater=water.isActive;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")); mat.SetColor("_BaseColor",Color.black);
            var shadowMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bool async=ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation=false;
            GameObject Move(GameObject go) {SceneManager.MoveGameObjectToScene(go,scene); return go;}
            try
            {
                water.SetActive(false);
                var camera=Move(new GameObject("Shaft pixel fixture")).AddComponent<Camera>(); camera.scene=scene;
                camera.enabled=false; camera.targetTexture=rt; camera.nearClipPlane=.05f; camera.farClipPlane=80;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
                var sun=Move(new GameObject("Shaft fixture sun")).AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=2;
                sun.shadows=LightShadows.Hard; sun.transform.rotation=Quaternion.Euler(20,180,0);
                RenderSettings.sun=sun;
                var contextRoot=Move(new GameObject("Bounded shaft fixture"));
                var cycle=contextRoot.AddComponent<AeroDayNightCycle>(); cycle.profile=ScriptableObject.CreateInstance<AeroDayNightProfile>();
                var context=contextRoot.AddComponent<AeroTowerAtmosphere>(); context.cycle=cycle; context.raySettings=Shafts.raySettings;
                context.baseRadius=100; context.decrease=0; context.floorHeight=40;
                context.SetConstructionContext(true,System.Array.Empty<AeroArchOpening>());
                for(int i=0;i<20;i++) context.Sample(Vector3.up,i*.1f);
                // A dark wall supplies opaque depth. The occluder sits above/outside the view, toward the sun.
                var wall=Move(GameObject.CreatePrimitive(PrimitiveType.Cube)); wall.transform.position=new Vector3(0,0,16);
                wall.transform.localScale=new Vector3(30,30,.2f); wall.GetComponent<Renderer>().sharedMaterial=mat; wall.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
                var blocker=Move(GameObject.CreatePrimitive(PrimitiveType.Cube)); blocker.transform.position=new Vector3(0,6,12);
                blocker.transform.localScale=new Vector3(50,2,50); blocker.GetComponent<Renderer>().sharedMaterial=shadowMaterial;
                blocker.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;
                blocker.SetActive(false);
                Color[] Capture() {camera.Render(); camera.Render(); return Read(rt);}
                check(camera,sun,blocker,Capture);
            }
            finally
            {
                foreach(var cycle in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<AeroDayNightCycle>())) UnityEngine.Object.DestroyImmediate(cycle.profile);
                Shafts.SetActive(active); water.SetActive(oldWater); RenderTexture.active=oldTarget; ShaderUtil.allowAsyncCompilation=async;
                EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(mat); UnityEngine.Object.DestroyImmediate(shadowMaterial); UnityEngine.Object.DestroyImmediate(rt);
                foreach(var l in sceneLights)if(l!=null)l.enabled=true;RenderSettings.sun=oldSun;Shafts.previewTime=oldClock;
            }
        }
        private static Color[] Read(RenderTexture rt)
        {
            var old=RenderTexture.active; var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            try {RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply(); return image.GetPixels();}
            finally {RenderTexture.active=old; UnityEngine.Object.DestroyImmediate(image);}
        }
        private static float Difference(Color[] a,Color[] b)
        {float sum=0; for(int i=0;i<a.Length;i++) sum+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b); return sum/(a.Length*3);}
        private static float Mean(Color[] pixels)=>pixels.Average(c=>(c.r+c.g+c.b)/3);
    }
}
