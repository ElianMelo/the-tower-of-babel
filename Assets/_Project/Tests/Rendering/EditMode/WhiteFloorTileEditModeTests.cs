using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>The white marble deck tile stays plain white on every face; GlassWater keeps its own shader.</summary>
    public sealed class WhiteFloorTileEditModeTests
    {
        private const string FloorMaterial = "Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Floor_Tile.mat";

        [Test]
        public void RadialFloorTile_HasNoGlazeOrGlassBandOnAnyFace()
        {
            string flow = File.ReadAllText("Assets/_Project/Shaders/BuildingAquaticFlow.hlsl");
            string finish = File.ReadAllText("Assets/_Project/Shaders/BuildingAquaticFinish.shader");
            Assert.That(flow, Does.Contain("if(_RadialFloor>.5 && _ModelKind>.5 && _ModelKind<1.5)return 0;"), "Side faces must not get the aqua glass band.");
            Assert.That(flow, Does.Not.Contain("_ModelKind<1.5 && abs(input.normalWS.y)>.99)return 0;"), "The exemption must cover side faces, not only the top.");
            Assert.That(finish, Does.Not.Contain("_GlazeColor.rgb,step(.2,input.paletteUV.x)"), "All glaze colouring goes through GlazeMask.");
            Assert.That(finish, Does.Contain("half GlazeMask(Varyings input)"));
        }

        [Test]
        public void FloorShaderStillCompilesAndGlassWaterUsesItsOwnShader()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterial);
            bool asynchronous = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            var copy = new Material(material);
            try
            {
                copy.SetFloat("_RadialFloor", 2);
                for (int pass = 0; pass < copy.passCount; pass++) ShaderUtil.CompilePass(copy, pass, true);
                Assert.That(ShaderUtil.ShaderHasError(copy.shader), Is.False);
            }
            finally { ShaderUtil.allowAsyncCompilation = asynchronous; Object.DestroyImmediate(copy); }
            var glassWater = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/GlassWaterFloor.prefab").GetComponent<Renderer>().sharedMaterial;
            Assert.That(glassWater.shader, Is.Not.EqualTo(material.shader), "GlassWater must not be affected by the white tile change.");
        }
    }
}
