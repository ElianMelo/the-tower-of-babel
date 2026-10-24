using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroIslandAtmosphereEditModeTests
    {
        [Test]
        public void QualityPresetsAndOrdinaryCameraSceneAreResolved()
        {
            var settings=ScriptableObject.CreateInstance<AeroRaySettings>();var root=new GameObject("Normal game camera context");
            try
            {
                Assert.That(settings.Downsample,Is.EqualTo(2));Assert.That(settings.Steps,Is.EqualTo(16));Assert.That(settings.FillCount,Is.EqualTo(2));
                settings.quality=AeroRaySettings.Quality.Low;Assert.That(settings.Downsample,Is.EqualTo(4));Assert.That(settings.Steps,Is.EqualTo(12));Assert.That(settings.FillCount,Is.EqualTo(1));
                var fx=root.AddComponent<AeroTowerAtmosphere>();var camera=root.AddComponent<Camera>();camera.enabled=false;
                Assert.That(AeroTowerLightContext.ForCamera(camera),Is.SameAs(fx));
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(settings);}
        }
        [Test]
        public void FloorTransitions_BlendFixedVolumesWithoutResettingInteriorDensity()
        {
            var root=new GameObject("Stair volume transition fixture");var day=ScriptableObject.CreateInstance<AeroDayNightProfile>();var settings=ScriptableObject.CreateInstance<AeroRaySettings>();
            try
            {
                var cycle=root.AddComponent<AeroDayNightCycle>();cycle.profile=day;
                var fx=root.AddComponent<AeroTowerAtmosphere>();fx.cycle=cycle;fx.raySettings=settings;fx.SetConstructionContext(true,null);
                for(int i=0;i<20;i++)fx.Sample(new Vector3(80,5.9f,0),i*.1f);
                Assert.That(fx.InteriorWeight,Is.EqualTo(1));
                fx.Sample(new Vector3(80,6.3f,0),2);
                Assert.That(fx.CurrentFloor,Is.EqualTo(1));Assert.That(fx.PreviousFloor,Is.Zero);
                Assert.That(fx.VolumeBlend.y,Is.InRange(.01f,.2f));Assert.That(fx.InteriorWeight,Is.EqualTo(1));
                for(int i=21;i<40;i++)fx.Sample(new Vector3(80,6.3f,0),i*.1f);
                Assert.That(fx.PreviousFloor,Is.EqualTo(1));Assert.That(fx.VolumeBlend.y,Is.EqualTo(1));
                fx.Sample(new Vector3(100,6.3f,0),4);Assert.That(fx.InteriorWeight,Is.InRange(.8f,.99f));
                for(int i=41;i<60;i++)fx.Sample(new Vector3(100,6.3f,0),i*.1f);Assert.That(fx.InteriorWeight,Is.Zero);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(day);Object.DestroyImmediate(settings);}
        }

        [Test]
        public void Cycle_HasSmoothPeriodicSunriseAndNight()
        {
            Assert.That(AeroDayNightProfile.DaylightAt(.5f), Is.EqualTo(1));
            Assert.That(AeroDayNightProfile.DaylightAt(0), Is.Zero);
            Assert.That(AeroDayNightProfile.DaylightAt(1.5f), Is.EqualTo(AeroDayNightProfile.DaylightAt(.5f)));
            float before=AeroDayNightProfile.DaylightAt(.25f-.0001f), after=AeroDayNightProfile.DaylightAt(.25f+.0001f);
            Assert.That(after, Is.GreaterThanOrEqualTo(before)); Assert.That(after-before, Is.LessThan(.01f));
        }

        [Test]
        public void Profile_AndMaterialsHaveSavedDependenciesAndNoShaderErrors()
        {
            var profile=AssetDatabase.LoadAssetAtPath<AeroDayNightProfile>("Assets/_Project/Environment/Lighting/AeroDayNight.asset");
            Assert.That(profile,Is.Not.Null); Assert.That(profile.cycleSeconds,Is.GreaterThanOrEqualTo(60));
            foreach(string name in new[]{"GlassWaterFloor","SoftSunBeam","AquaFireflies","AquaProjectionCookie"})
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Environment/Lighting/"+name+".mat");
                Assert.That(material,Is.Not.Null,name);
                Assert.That(ShaderUtil.GetShaderMessages(material.shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),Is.Empty,name);
            }
            var river=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Environment/Lighting/GlassWaterFloor.mat");
            Assert.That(river.GetTexture("_Environment"),Is.Not.Null);
            var native=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Stylized Water 3/Materials/Textures/Caustics/Caustics_1.png");
            Assert.That(river.GetTexture("_CausticsTex"),Is.SameAs(native));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Environment/Lighting/AquaProjectionCookie.mat").GetTexture("_CausticsTex"),Is.SameAs(native));
        }

        [Test]
        public void ExitAtmosphere_FadesTowardEverySideAndRejectsOtherHeights()
        {
            var go=new GameObject("Exit volume geometry fixture");
            try
            {
                var fx=go.AddComponent<AeroTowerAtmosphere>();
                Assert.That(fx.ExitProximity(new Vector3(0,2,0)),Is.Zero);
                for(int i=0;i<16;i++)
                {
                    float angle=i*Mathf.PI/8;
                    var p=new Vector3(Mathf.Cos(angle)*92,2,Mathf.Sin(angle)*92);
                    Assert.That(fx.ExitProximity(p),Is.GreaterThan(.99f));
                    Assert.That(fx.ExitProximity(p+Vector3.up*400),Is.Zero);
                    Assert.That(fx.ExitProximity(p+Vector3.down*10),Is.Zero);
                }
            }
            finally {Object.DestroyImmediate(go);}
        }

        [Test]
        public void Fireflies_DoNotRequireLegacyBeamsAndFillStaysAnchoredUntilDark()
        {
            var go=new GameObject("Beam bounds fixture"); var profile=ScriptableObject.CreateInstance<AeroDayNightProfile>();
            try
            {
                var cycle=go.AddComponent<AeroDayNightCycle>(); cycle.profile=profile;
                var fx=go.AddComponent<AeroTowerAtmosphere>(); fx.cycle=cycle;
                var settings=ScriptableObject.CreateInstance<AeroRaySettings>(); fx.raySettings=settings;
                var child=new GameObject("Independent motes",typeof(ParticleSystem)); child.transform.SetParent(go.transform);
                fx.fireflies=child.GetComponent<ParticleSystem>();
                fx.bounceLights=new Light[2]; for(int i=0;i<2;i++) { var l=new GameObject("Fill",typeof(Light));l.transform.SetParent(go.transform);fx.bounceLights[i]=l.GetComponent<Light>(); }
                fx.SetConstructionContext(true,new[]{new AeroArchOpening(1,2,new Vector3(90,15.8f,0),Vector3.left),new AeroArchOpening(2,2,new Vector3(-90,15.8f,0),Vector3.right)});
                for(int i=0;i<30;i++)fx.Sample(new Vector3(81,14,0),i*.1f);
                Assert.That(fx.CurrentFloor,Is.EqualTo(2)); Assert.That(fx.fireflies.emission.rateOverTime.constant,Is.GreaterThan(0));
                Vector3 anchor=fx.bounceLights[0].transform.position;
                fx.Sample(new Vector3(-81,14,0),3);
                Assert.That(fx.bounceLights[0].transform.position,Is.EqualTo(anchor));
                bool moved=false;
                for(int i=31;i<60;i++)
                {
                    Vector3 before=fx.bounceLights[0].transform.position;
                    fx.Sample(new Vector3(-81,14,0),i*.1f);
                    if(fx.bounceLights[0].transform.position!=before) {moved=true;Assert.That(fx.SourceWeight(0),Is.Zero);Assert.That(fx.bounceLights[0].enabled,Is.False);}
                }
                Assert.That(moved,Is.True); Assert.That(fx.SourceId(0),Is.EqualTo(2));
                fx.SetConstructionContext(false,null);for(int i=60;i<80;i++)fx.Sample(new Vector3(-81,14,0),i*.1f);
                Assert.That(fx.InteriorWeight,Is.Zero); Assert.That(fx.bounceLights.All(l=>!l.enabled),Is.True);
                Object.DestroyImmediate(settings);
            }
            finally {Object.DestroyImmediate(go); Object.DestroyImmediate(profile);}
        }

        [Test]
        public void SunFlare_UsesOcclusionAndRendererSupportsBothFlarePaths()
        {
            var data=AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>("Assets/_Project/Environment/Lighting/AeroSunFlare.asset");
            Assert.That(data.elements.Length,Is.EqualTo(2));
            var rp=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            Assert.That(rp.supportDataDrivenLensFlare,Is.True); Assert.That(rp.supportScreenSpaceLensFlare,Is.True);
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset");
            Assert.That(profile.TryGet<ScreenSpaceLensFlare>(out var flare),Is.True); Assert.That(flare.intensity.value,Is.InRange(.001f,.08f));
            var local=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Environment/Lighting/TowerExitAtmosphere.asset");
            Assert.That(local.TryGet<AeroShaftVolume>(out var shafts),Is.True);
            Assert.That(shafts.intensity.value,Is.GreaterThan(1)); Assert.That(shafts.density.value,Is.GreaterThan(1));
        }
    }
}
