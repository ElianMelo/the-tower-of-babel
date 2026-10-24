#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroAtmospherePlayModeTests
    {
        private Scene scene;
        private Camera camera;
        private RenderTexture target;
        private Material dark, caster;
        private AeroSunShaftRendererFeature shafts;
        private AeroLightRendererFeature water;
        private bool shaftActive,waterActive;
        private AeroDayNightProfile contextProfile;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene=SceneManager.CreateScene("Atmosphere runtime fixture");
            var go=Move(new GameObject("Atmosphere test camera")); go.tag="MainCamera";
            camera=go.AddComponent<Camera>(); camera.scene=scene; camera.backgroundColor=Color.black;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.nearClipPlane=.05f; camera.farClipPlane=80;
            target=new RenderTexture(128,128,24); camera.targetTexture=target;
            dark=new Material(Shader.Find("Universal Render Pipeline/Unlit")); dark.SetColor("_BaseColor",Color.black);
            caster=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            shafts=renderer.rendererFeatures.OfType<AeroSunShaftRendererFeature>().Single(); shaftActive=shafts.isActive;
            water=renderer.rendererFeatures.OfType<AeroLightRendererFeature>().Single(); waterActive=water.isActive; water.SetActive(false);
            var root=Move(new GameObject("Runtime bounded shaft context"));
            var cycle=root.AddComponent<AeroDayNightCycle>(); contextProfile=ScriptableObject.CreateInstance<AeroDayNightProfile>(); cycle.profile=contextProfile;
            var context=root.AddComponent<AeroTowerAtmosphere>(); context.cycle=cycle; context.raySettings=shafts.raySettings; context.baseRadius=100;context.decrease=0;context.floorHeight=40;
            context.SetConstructionContext(true,System.Array.Empty<AeroArchOpening>());
            for(int i=0;i<20;i++) context.Sample(Vector3.up,i*.1f);
            yield return null;
        }
        private GameObject Move(GameObject go) {SceneManager.MoveGameObjectToScene(go,scene); return go;}

        [UnityTest]
        public IEnumerator SunShafts_ReactToMovingShadowOnlyOccludersDuringGameplay()
        {
            var sun=Move(new GameObject("Runtime sun")).AddComponent<Light>(); sun.type=LightType.Directional;
            sun.intensity=2; sun.shadows=LightShadows.Hard; sun.transform.rotation=Quaternion.Euler(20,180,0);
            var wall=Move(GameObject.CreatePrimitive(PrimitiveType.Cube)); wall.transform.position=new Vector3(0,0,16);
            wall.transform.localScale=new Vector3(30,30,.2f); wall.GetComponent<Renderer>().sharedMaterial=dark;
            wall.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            var blocker=Move(GameObject.CreatePrimitive(PrimitiveType.Cube)); blocker.transform.position=new Vector3(0,6,12);
            blocker.transform.localScale=new Vector3(50,2,50); blocker.GetComponent<Renderer>().sharedMaterial=caster;
            blocker.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;
            blocker.SetActive(false); shafts.SetActive(true);
            yield return null;
            float lit=CaptureMean(); blocker.SetActive(true); yield return null;
            float shadowed=CaptureMean(); Assert.That(shadowed,Is.LessThan(lit-.001f));
            blocker.transform.position+=Vector3.right*100; yield return null;
            Assert.That(CaptureMean(),Is.GreaterThan(shadowed+.001f));
        }

        [UnityTest]
        public IEnumerator Projection_UpdatesItsExistingCookieAndReleasesItWhenDisabled()
        {
            shafts.SetActive(false);
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Lighting/AquaProjectionLamp.prefab")); Move(go);
            go.transform.position=Vector3.zero;
            var lamp=go.GetComponent<AeroProjectionLamp>(); AeroProjectionLamp.RefreshBudget(Vector3.zero);
            var cookie=lamp.Cookie; uint count=cookie.updateCount; int cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;
            yield return new WaitForSeconds(.4f);
            Assert.That(lamp.Cookie,Is.SameAs(cookie)); Assert.That(cookie.updateCount,Is.GreaterThan(count));
            Assert.That(cookie.updateCount-count,Is.LessThanOrEqualTo(12u),"Cookie rendering must remain throttled rather than refreshing every frame.");
            Assert.That(lamp.projection.cookie,Is.SameAs(cookie));
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length,Is.EqualTo(cameras));
            lamp.enabled=false; Assert.That(lamp.projection.cookie,Is.Null); yield return null;
            Assert.That(cookie==null,Is.True); Assert.That(lamp.projection.enabled,Is.False);
        }

        private float CaptureMean()
        {
            var old=RenderTexture.active; var image=new Texture2D(128,128,TextureFormat.RGB24,false);
            try
            {
                camera.Render(); RenderTexture.active=target; image.ReadPixels(new Rect(0,0,128,128),0,0); image.Apply();
                return image.GetPixels().Average(c=>(c.r+c.g+c.b)/3);
            }
            finally {RenderTexture.active=old; Object.Destroy(image);}
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            shafts.SetActive(shaftActive); water.SetActive(waterActive); camera.enabled=false; camera.targetTexture=null;
            Object.Destroy(dark); Object.Destroy(caster); Object.Destroy(target);
            Object.Destroy(contextProfile);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
