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
    public sealed class AeroLightEditModeTests
    {
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        private AeroLightRendererFeature Feature => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath)
            .rendererFeatures.OfType<AeroLightRendererFeature>().Single();

        [Test]
        public void Range_HasFullNearLightAndASmoothFiniteFade()
        {
            var s = Feature.settings;
            Assert.That(s.WeightAt(0), Is.EqualTo(1)); Assert.That(s.WeightAt(s.fadeStart), Is.EqualTo(1));
            Assert.That(s.WeightAt((s.fadeStart+s.distance)/2), Is.EqualTo(.5f).Within(.001));
            Assert.That(s.WeightAt(s.distance), Is.Zero); Assert.That(s.WeightAt(10000), Is.Zero);
            s = new AeroLightRendererFeature.LightSettings {distance=0,fadeStart=100};
            Assert.That(float.IsNaN(s.WeightAt(2)), Is.False); Assert.That(s.WeightAt(2), Is.Zero);
        }

        [Test]
        public void CameraFilter_ExcludesReflectionAndPreviewCameras()
        {
            var go=new GameObject("Camera filter fixture");
            try
            {
                var camera=go.AddComponent<Camera>();
                foreach (CameraType type in Enum.GetValues(typeof(CameraType)))
                {
                    camera.cameraType=type;
                    Assert.That(AeroLightRendererFeature.SupportsCamera(camera), Is.EqualTo(type==CameraType.Game || type==CameraType.SceneView));
                }
                Assert.That(AeroLightRendererFeature.SupportsCamera(null), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void SavedFeature_HasBuildReferencesAndACompiledShader()
        {
            Assert.That(Feature.isActive, Is.True); Assert.That(Feature.caustics, Is.Not.Null);
            Assert.That(Feature.caustics.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(Feature.lightShader, Is.Not.Null);
            Assert.That(Feature.environment, Is.Not.Null); Assert.That(Feature.environment.mipmapCount, Is.GreaterThan(1));
            Assert.That(ShaderUtil.GetShaderMessages(Feature.lightShader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), Is.Empty);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var map=new SerializedObject(renderer).FindProperty("m_RendererFeatureMap");
            Assert.That(map.arraySize, Is.EqualTo(renderer.rendererFeatures.Count));
            for (int i=0;i<renderer.rendererFeatures.Count;i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i],out string guid,out long id);
                Assert.That(map.GetArrayElementAtIndex(i).longValue, Is.EqualTo(id));
            }
        }

        [Test]
        public void Lake_UsesItsOwnReflectiveMaterialInTheSavedScene()
        {
            const string path="Assets/_Project/Environment/AeroLakeWater.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(material, Is.Not.Null);
            Assert.That(material, Is.Not.SameAs(AssetDatabase.LoadAssetAtPath<Material>("Assets/Stylized Water 3/Materials/StylizedWater3_Default.mat")));
            Assert.That(material.GetFloat("_ScreenSpaceReflectionsEnabled"), Is.EqualTo(1));
            Assert.That(material.GetFloat("_EnvironmentReflectionsOn"), Is.EqualTo(1));
            Assert.That(material.GetFloat("_CausticsBrightness"), Is.GreaterThan(0));
            var scene=SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity"); bool opened=!scene.isLoaded;
            if (opened) scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Tower.unity",OpenSceneMode.Additive);
            try
            {
                var water=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MeshRenderer>(true)).Single(x=>x.name=="StylizedWater_Desktop");
                Assert.That(water.sharedMaterial, Is.SameAs(material));
                var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MonoBehaviour>(true)).Where(x=>x!=null).ToArray();
                var terrain=all.Single(x=>x.GetType().Name=="AeroTowerTerrain");
                var basins=new SerializedObject(terrain).FindProperty("lakes");
                var regions=all.OfType<AeroWaterLightRegion>().OrderBy(x=>x.name).ToArray();
                Assert.That(regions.Length,Is.EqualTo(basins.arraySize));
                for(int i=0;i<regions.Length;i++)
                {
                    var basin=basins.GetArrayElementAtIndex(i);
                    float yaw=-basin.FindPropertyRelative("rotation").floatValue;
                    Assert.That(Quaternion.Angle(regions[i].transform.rotation,Quaternion.Euler(0,yaw,0)),Is.LessThan(.01f));
                    Assert.That(regions[i].radii,Is.EqualTo(basin.FindPropertyRelative("radii").vector2Value*1.06f));
                    Assert.That(regions[i].height,Is.LessThanOrEqualTo(3));
                }
            }
            finally {if(opened) EditorSceneManager.CloseScene(scene,true);}
        }

        // Render the actual feature through URP, with an ordinary Unlit material, not a special receiving shader.
        [Test]
        public void RenderGraph_ProjectsOnOrdinaryNearbyObjectsAndAnimates()
        {
            WithCamera((camera, wall, capture)=>
            {
                Feature.SetActive(false); var before=capture();
                Feature.SetActive(true); Feature.previewTime=1; var after=capture();
                Assert.That(Difference(before,after), Is.GreaterThan(.002), "Near objects must receive visible light.");
                Feature.previewTime=5; var later=capture();
                Assert.That(Difference(after,later), Is.GreaterThan(.001), "Caustics must move.");
            });
        }

        [Test]
        public void RenderGraph_OnlyLightsWaterFootprintsAndNeverDarkensEdges()
        {
            WithCamera((camera,wall,capture)=>
            {
                wall.transform.localScale=new Vector3(3,3,.1f); // Include silhouettes against sky.
                Feature.SetActive(false); var baseline=capture();
                Feature.SetActive(true); var lit=capture();
                for(int i=0;i<lit.Length;i++)
                {
                    Assert.That(lit[i].r,Is.GreaterThanOrEqualTo(baseline[i].r-.005f));
                    Assert.That(lit[i].g,Is.GreaterThanOrEqualTo(baseline[i].g-.005f));
                    Assert.That(lit[i].b,Is.GreaterThanOrEqualTo(baseline[i].b-.005f));
                }
                var region=wall.GetComponentInChildren<AeroWaterLightRegion>();
                region.enabled=false;
                Assert.That(Difference(baseline,capture()),Is.LessThan(.0005),"No water source means no water projection, even beside the camera.");
                region.enabled=true; region.transform.position+=Vector3.up*30;
                Assert.That(Difference(baseline,capture()),Is.LessThan(.0005),"A different tower floor must not receive lake caustics.");
                region.transform.position+=Vector3.right*50;
                Assert.That(Difference(baseline,capture()),Is.LessThan(.0005),"Dry land outside the shoreline must remain unchanged.");
            });
        }

        [Test]
        public void RenderGraph_LeavesScreenSpaceInterfacePixelsUnchanged()
        {
            WithCamera((camera,wall,capture)=>
            {
                var go=new GameObject("UI exclusion fixture",typeof(RectTransform),typeof(Canvas));
                SceneManager.MoveGameObjectToScene(go,camera.scene);
                var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=camera; canvas.planeDistance=.2f;
                var panel=new GameObject("Opaque interface panel",typeof(RectTransform),typeof(UnityEngine.UI.Image));
                panel.transform.SetParent(go.transform,false);
                panel.GetComponent<RectTransform>().sizeDelta=new Vector2(64,64);
                panel.GetComponent<UnityEngine.UI.Image>().color=new Color(.2f,.8f,.4f,1);
                Canvas.ForceUpdateCanvases();
                Feature.SetActive(false); var before=capture(); Feature.SetActive(true); var after=capture();
                for(int y=40;y<88;y++) for(int x=40;x<88;x++)
                {
                    int i=y*128+x;
                    Assert.That(after[i].g,Is.EqualTo(before[i].g).Within(.005));
                    Assert.That(after[i].r,Is.EqualTo(before[i].r).Within(.005));
                }
            });
        }

        [Test]
        public void RenderGraph_DoesNotLightDistantGeometryOrTheSky()
        {
            WithCamera((camera, wall, capture)=>
            {
                wall.transform.position=new Vector3(0,0,70); wall.transform.localScale=new Vector3(100,100,.1f);
                Feature.SetActive(false); var before=capture(); Feature.SetActive(true); var after=capture();
                Assert.That(Difference(before,after), Is.LessThan(.0005), "Distant geometry must remain unchanged.");
                wall.SetActive(false); Feature.SetActive(false); before=capture(); Feature.SetActive(true); after=capture();
                Assert.That(Difference(before,after), Is.LessThan(.0005), "Sky must remain unchanged.");
            });
        }

        [Test]
        public void RenderGraph_ZeroStrengthIsAVisibleNoOp()
        {
            var s=Feature.settings; float a=s.causticIntensity,b=s.reflectedLight;
            try
            {
                s.causticIntensity=0; s.reflectedLight=0;
                WithCamera((camera,wall,capture)=> {Feature.SetActive(false); var before=capture(); Feature.SetActive(true); Assert.That(Difference(before,capture()), Is.LessThan(.0005));});
            }
            finally {s.causticIntensity=a; s.reflectedLight=b;}
        }

        [Test]
        public void RenderGraph_WaterPatternStaysAttachedWhenTheCameraMoves()
        {
            float glints=Feature.settings.reflectedLight;
            try
            {
                Feature.settings.reflectedLight=0;
                WithCamera((camera,wall,capture)=>
                {
                    camera.orthographic=true; camera.orthographicSize=4;
                    Feature.SetActive(true); Feature.previewTime=3;
                    var before=capture(); camera.transform.position=Vector3.right;
                    var after=capture();
                    // One metre is exactly 16 pixels in this orthographic fixture.
                    float error=0; int count=0;
                    for (int y=32;y<96;y++) for (int x=48;x<96;x++)
                    { error+=Mathf.Abs(before[y*128+x].g-after[y*128+x-16].g); count++; }
                    Assert.That(error/count, Is.LessThan(.01), "Moving the view must not drag the water pattern over the surface.");
                });
            }
            finally {Feature.settings.reflectedLight=glints;}
        }

        [Test]
        public void NearbyEnvironment_ActuallyChangesOrdinaryLitSurfaceReflections()
        {
            var cube=new Cubemap(16,TextureFormat.RGBA32,true);
            var colors=Enumerable.Repeat(new Color(.05f,1,.5f),16*16).ToArray();
            for (int i=0;i<6;i++) cube.SetPixels(colors,(CubemapFace)i); cube.Apply(true);
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Metallic",1); mat.SetFloat("_Smoothness",.8f); mat.SetColor("_BaseColor",Color.white);
            var environment=Feature.environment; var s=Feature.settings;
            float caustics=s.causticIntensity, reflection=s.reflectedLight;
            try
            {
                Feature.environment=cube; s.causticIntensity=0; s.reflectedLight=1;
                WithCamera((camera,wall,capture)=>
                {
                    Feature.SetActive(false); wall.GetComponent<Renderer>().sharedMaterial=mat;
                    var before=capture(); Feature.SetActive(true); var after=capture();
                    Assert.That(Difference(before,after), Is.GreaterThan(.005), "The environment must visibly reflect on stock Lit surfaces.");
                });
            }
            finally {Feature.environment=environment; s.causticIntensity=caustics; s.reflectedLight=reflection;
                UnityEngine.Object.DestroyImmediate(mat); UnityEngine.Object.DestroyImmediate(cube);}
        }

        private void WithCamera(Action<Camera,GameObject,Func<Color[]>> check)
        {
            var scene=EditorSceneManager.NewPreviewScene(); var originalTarget=RenderTexture.active;
            var rt=new RenderTexture(128,128,24); var pixels=new Texture2D(128,128,TextureFormat.RGB24,false);
            bool active=Feature.isActive; float clock=Feature.previewTime;
            bool async=ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation=false;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")); mat.SetColor("_BaseColor",new Color(.3f,.3f,.3f));
            try
            {
                var go=new GameObject("Projection camera"); SceneManager.MoveGameObjectToScene(go,scene);
                var camera=go.AddComponent<Camera>(); camera.scene=scene; camera.targetTexture=rt;
                camera.nearClipPlane=.05f; camera.farClipPlane=150; camera.backgroundColor=new Color(.04f,.07f,.1f);
                camera.clearFlags=CameraClearFlags.SolidColor;
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(wall,scene);
                wall.transform.position=new Vector3(0,0,6); wall.transform.localScale=new Vector3(10,10,.1f);
                wall.GetComponent<Renderer>().sharedMaterial=mat;
                var source=new GameObject("Fixture water source"); source.transform.SetParent(wall.transform,false);
                var region=source.AddComponent<AeroWaterLightRegion>();
                region.radii=new Vector2(12,12); region.height=8; region.depth=8;
                Color[] Capture()
                {
                    camera.Render(); camera.Render(); RenderTexture.active=rt;
                    pixels.ReadPixels(new Rect(0,0,128,128),0,0); pixels.Apply(); return pixels.GetPixels();
                }
                check(camera,wall,Capture);
            }
            finally
            {
                Feature.SetActive(active); Feature.previewTime=clock; RenderTexture.active=originalTarget;
                ShaderUtil.allowAsyncCompilation=async; EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Object.DestroyImmediate(mat); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
        private static float Difference(Color[] a,Color[] b)
        {
            float sum=0; for (int i=0;i<a.Length;i++) sum+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);
            return sum/(a.Length*3);
        }
    }
}
