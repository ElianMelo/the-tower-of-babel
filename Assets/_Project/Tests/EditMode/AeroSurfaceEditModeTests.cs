using NUnit.Framework;
using TowerOfBabel.Interface;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class AeroSurfaceEditModeTests
    {
        [Test]
        public void BackgroundsUseBorderOnlyHoverAndSmallIconsUse64PixelTextures()
        {
            var ui=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/FrutigerAero/Prefabs/FrutigerInterface.prefab");
            foreach(string path in new[]{"Inventory/Visuals/WindowGlass","Upgrade/Visuals/WindowGlass","Upgrade/Visuals/SkillRail","Upgrade/Visuals/BoardWell","Interaction/Visuals"})
            {
                var surface=ui.transform.Find(path).GetComponent<AeroGlassGeometry>();
                var hover=surface.GetComponent<AeroHoverFeedback>();
                Assert.That(surface.HoverFill,Is.Zero,path);
                Assert.That(surface.SurfaceFlow,Is.GreaterThan(.5f),path);
                Assert.That(hover.enabled && hover.TracksBackground,Is.True,path);
                Assert.That(surface.BorderMotion,Is.InRange(.12f,.25f));
            }
            foreach(string path in new[]{"Inventory/Visuals/Titlebar/InventoryIcon","Upgrade/Visuals/Titlebar/UpgradesIcon"})
                Assert.That(ui.transform.Find(path).GetComponent<UnityEngine.UI.Image>().sprite.texture.width,Is.EqualTo(64));
            var prompt=ui.transform.Find("Interaction/Visuals");
            Assert.That(prompt.GetComponent<AeroFloat>(),Is.Not.Null);
            Assert.That(prompt.Find("ActionIcon/ProcessIcon").GetComponent<AeroIcon>().Kind,Is.EqualTo(AeroIconKind.Gear));
            Assert.That(prompt.Find("ActionIcon/BuildIcon").GetComponent<AeroIcon>().Kind,Is.EqualTo(AeroIconKind.Tower));
            Assert.That(prompt.Find("ResourceSummary/Source/Icon"),Is.Not.Null);
            Assert.That(prompt.Find("ResourceSummary/Result/Icon"),Is.Not.Null);
            Assert.That(prompt.Find("ResourceSummary").gameObject.activeSelf,Is.False);
            string[] actions={"Gather","Process","Build"};
            string[] popupPaths={"PickaxeIcon","ProcessIcon","BuildIcon"};
            string[] tabPaths={"GatherButton/LeafIcon","ProcessorButton/CubeIcon","BuilderButton/TowerIcon"};
            for(int i=0;i<actions.Length;i++)
            {
                var popup=prompt.Find("ActionIcon/"+popupPaths[i]+"/Artwork").GetComponent<UnityEngine.UI.Image>();
                var tab=ui.transform.Find("Upgrade/Visuals/SkillRail/"+tabPaths[i]+"/Artwork").GetComponent<UnityEngine.UI.Image>();
                Assert.That(popup.sprite.name,Is.EqualTo(actions[i]+"Small"));
                Assert.That(popup.sprite.texture.width,Is.EqualTo(64));
                Assert.That(tab.sprite,Is.SameAs(popup.sprite));
                Assert.That(popup.raycastTarget || tab.raycastTarget,Is.False);
                Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(popup)),Does.EndWith(actions[i]+"ActionIcon.prefab"));
            }
        }

        [Test]
        public void GameplayMetadataKeepsGatherRecipeAndConstructionDistinct()
        {
            var stone=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Resources/Stone.prefab").GetComponent<Resource>();
            var process=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Processor/StoneProcessor.prefab").GetComponent<ResourceProcessor>();
            var build=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Pillar.prefab").GetComponent<TowerOfBabel.Buildings.Building>();
            Assert.That(stone.InteractionResources.Action,Is.EqualTo(InteractionActionKind.Gather));
            Assert.That(stone.InteractionResources.Target,Is.EqualTo(stone.Definition.ResourceType));
            Assert.That(process.InteractionResources.Action,Is.EqualTo(InteractionActionKind.Process));
            Assert.That(process.InteractionResources.SourceAmount,Is.EqualTo(process.EffectiveSourceAmount));
            Assert.That(process.InteractionResources.Target,Is.EqualTo(process.TargetResource.ResourceType));
            Assert.That(build.InteractionResources.Action,Is.EqualTo(InteractionActionKind.Build));
            Assert.That(build.InteractionResources.SourceAmount,Is.EqualTo(build.EffectiveResourceCost));
        }

        [Test]
        public void BackgroundHoverDoesNotChangeRenderedInteriorColorOrAlpha()
        {
            Color idle=RenderProbe(0,0)[64*128+64],hover=RenderProbe(1,0)[64*128+64],button=RenderProbe(1,1)[64*128+64];
            Assert.That(idle.b,Is.GreaterThan(.05f),"The shader must actually render.");
            Assert.That(idle.a,Is.GreaterThan(.05f));
            Assert.That(Mathf.Abs(hover.r-idle.r)+Mathf.Abs(hover.g-idle.g)+Mathf.Abs(hover.b-idle.b),Is.LessThan(.012f));
            Assert.That(hover.a,Is.EqualTo(idle.a).Within(.004f));
            Assert.That(Mathf.Abs(button.r-idle.r)+Mathf.Abs(button.g-idle.g)+Mathf.Abs(button.b-idle.b),Is.GreaterThan(.08f),"Buttons restore their brighter full hover response.");
        }

        [Test]
        public void BorderLightTravelsWhileEveryRenderedEdgeAndInteriorOpacityStaysFixed()
        {
            var first=RenderProbe(0,0,.2f,0,0);
            var second=RenderProbe(0,0,.2f,0,1.7f);
            int changed=0;
            for(int i=0;i<first.Length;i++)
            {
                Assert.That(second[i].a,Is.EqualTo(first[i].a).Within(.004f),"Boundary coverage must never move.");
                float delta=Mathf.Abs(first[i].r-second[i].r)+Mathf.Abs(first[i].g-second[i].g)+Mathf.Abs(first[i].b-second[i].b);
                if(delta>.015f) changed++;
                int x=i%128,y=i/128;
                if(x>20 && x<108 && y>20 && y<108) Assert.That(delta,Is.LessThan(.012f),"Traveling light stays on the border.");
            }
            Assert.That(changed,Is.GreaterThan(30),"The reflection must visibly travel along the fixed border.");
            var buttonA=RenderProbe(0,1,0,0,0); var buttonB=RenderProbe(0,1,0,0,1.7f);
            for(int i=0;i<buttonA.Length;i++) Assert.That(buttonB[i].a,Is.EqualTo(buttonA[i].a).Within(.004f));
        }

        private static Color[] RenderProbe(float hover,float fill,float borderMotion=0,float flow=0,float time=0)
        {
            var previous=RenderTexture.active;
            var target=RenderTexture.GetTemporary(128,128,0,RenderTextureFormat.ARGB32);
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);
            var material=new Material(Shader.Find("TowerOfBabel/UI/Liquid Glass"));
            var mesh=new Mesh();
            try
            {
                material.SetColor("_Color",new Color(.2f,.5f,.65f,.65f));
                material.SetFloat("_Frost",1); material.SetFloat("_SceneBlur",0); material.SetFloat("_Shine",0);
                material.SetFloat("_MotionTime",time);
                using(var vertices=new UnityEngine.UI.VertexHelper())
                {
                    foreach(var p in new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)})
                    {
                        var vertex=UIVertex.simpleVert; vertex.position=p; vertex.uv0=p;
                        vertex.uv1=new Vector4(200,100,0,hover); vertex.uv2=new Vector4(18,1.5f,.5f,.5f); vertex.uv3=new Vector4(.5f,.5f,fill,borderMotion);
                        vertex.tangent=new Vector4(0,flow,0,0);
                        vertices.AddVert(vertex);
                    }
                    vertices.AddTriangle(0,1,2); vertices.AddTriangle(2,3,0); vertices.FillMesh(mesh);
                }
                RenderTexture.active=target; GL.Clear(true,true,Color.clear);
                GL.PushMatrix();
                try { GL.LoadOrtho(); material.SetPass(0); Graphics.DrawMeshNow(mesh,Matrix4x4.identity); }
                finally { GL.PopMatrix(); }
                texture.ReadPixels(new Rect(0,0,128,128),0,0); texture.Apply(); return texture.GetPixels();
            }
            finally
            {
                RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(texture); Object.DestroyImmediate(material); Object.DestroyImmediate(mesh);
            }
        }
    }
}
