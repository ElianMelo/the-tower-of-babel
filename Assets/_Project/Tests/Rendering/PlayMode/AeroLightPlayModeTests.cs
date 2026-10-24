#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroLightPlayModeTests
    {
        private Scene scene;
        private Camera camera;
        private GameObject wall;
        private Material material;
        private RenderTexture target;
        private Texture2D pixels;
        private AeroLightRendererFeature feature;
        private bool wasActive;
        private float clock;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene=SceneManager.CreateScene("Aero runtime rendering fixture");
            var go=new GameObject("Runtime camera"); SceneManager.MoveGameObjectToScene(go,scene);
            camera=go.AddComponent<Camera>(); camera.scene=scene; camera.enabled=false;
            target=new RenderTexture(128,128,24); camera.targetTexture=target; camera.farClipPlane=150;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.03f,.05f,.08f);
            pixels=new Texture2D(128,128,TextureFormat.RGB24,false);
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(wall,scene);
            wall.transform.position=new Vector3(0,0,6); wall.transform.localScale=new Vector3(10,10,.1f);
            material=new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetColor("_BaseColor",new Color(.3f,.3f,.3f));
            wall.GetComponent<Renderer>().sharedMaterial=material;
            var region=wall.AddComponent<AeroWaterLightRegion>(); region.radii=new Vector2(12,12); region.height=8; region.depth=8;
            feature=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset").rendererFeatures.OfType<AeroLightRendererFeature>().Single();
            wasActive=feature.isActive; clock=feature.previewTime; yield return null;
        }

        [UnityTest]
        public IEnumerator Light_AnimatesWithGameTimeAndTogglesWithoutExtraCameras()
        {
            int cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;
            feature.SetActive(false); var before=Capture();
            feature.SetActive(true); feature.previewTime=-1; var first=Capture();
            Assert.That(Difference(before,first), Is.GreaterThan(.002));
            yield return new WaitForSeconds(.5f);
            Assert.That(Difference(first,Capture()), Is.GreaterThan(.001));
            feature.SetActive(false); Assert.That(Difference(before,Capture()), Is.LessThan(.0005));
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, Is.EqualTo(cameras));
        }

        [UnityTest]
        public IEnumerator Light_FadesWhenThePlayerViewMovesAway()
        {
            feature.SetActive(false); var before=Capture(); feature.SetActive(true); feature.previewTime=2;
            Assert.That(Difference(before,Capture()), Is.GreaterThan(.002));
            camera.transform.position=new Vector3(0,0,-70);
            feature.SetActive(false); before=Capture(); feature.SetActive(true);
            Assert.That(Difference(before,Capture()), Is.LessThan(.0005));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Light_DisablingTheSourceRemovesProjectionWithoutMovingTheCamera()
        {
            feature.SetActive(false); var before=Capture(); feature.SetActive(true);
            Assert.That(Difference(before,Capture()),Is.GreaterThan(.002));
            wall.GetComponent<AeroWaterLightRegion>().enabled=false;
            yield return null;
            Assert.That(Difference(before,Capture()),Is.LessThan(.0005));
        }

        private Color[] Capture()
        {
            var old=RenderTexture.active;
            try { camera.Render(); RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,128,128),0,0); pixels.Apply(); return pixels.GetPixels(); }
            finally {RenderTexture.active=old;}
        }
        private static float Difference(Color[] a,Color[] b)
        {
            float sum=0; for (int i=0;i<a.Length;i++) sum+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);
            return sum/(a.Length*3);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            feature.SetActive(wasActive); feature.previewTime=clock;
            Object.Destroy(material); Object.Destroy(target); Object.Destroy(pixels);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
