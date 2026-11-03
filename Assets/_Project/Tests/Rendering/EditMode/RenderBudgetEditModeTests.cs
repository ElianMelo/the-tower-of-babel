using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>
    /// Performance pass (2026-11-03): the tower shaders need the procedural variant the GPU-resident far renderer draws
    /// with. (Depth priming was measured and rejected: the far tower is vertex bound, and the prepass cost 10 ms.)
    /// </summary>
    public sealed class RenderBudgetEditModeTests
    {
        [TestCase("TowerOfBabel/Buildings/Bubble Tube Pillar")]
        [TestCase("TowerOfBabel/Buildings/Aurora Arch")]
        [TestCase("TowerOfBabel/Buildings/Aquatic Finish")]
        [TestCase("TowerOfBabel/Buildings/Glass water floor")]
        [TestCase("TowerOfBabel/Buildings/Glass Step")]
        [TestCase("TowerOfBabel/Foliage/Clinging Vine")]
        public void TowerShaders_SupportGpuResidentFarDrawing(string name)
        {
            var shader = Shader.Find(name);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.keywordSpace.FindKeyword("PROCEDURAL_INSTANCING_ON").isValid, Is.True,
                "#pragma instancing_options procedural:AeroFarSetup on every instanced pass.");
            Assert.That(ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), Is.Empty);
        }
    }
}
