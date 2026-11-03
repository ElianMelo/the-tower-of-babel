using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace TowerOfBabel.World.Tests
{
    /// <summary>The full suite runs only right before a build, and it covers every project test assembly.</summary>
    public sealed class FullTestBuildGateEditModeTests
    {
        private static Type Gate => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("FullTestBuildGate")).FirstOrDefault(t => t != null);

        [Test]
        public void Gate_CoversEveryProjectTestAssembly()
        {
            Assert.That(Gate, Is.Not.Null);
            var listed = ((string[])Gate.GetField("EditModeAssemblies").GetValue(null)).Concat((string[])Gate.GetField("PlayModeAssemblies").GetValue(null)).ToArray();
            var project = Directory.GetFiles("Assets/_Project/Tests", "*.asmdef", SearchOption.AllDirectories)
                .Select(f => System.Text.RegularExpressions.Regex.Match(File.ReadAllText(f), "\"name\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value).ToArray();
            Assert.That(listed, Is.EquivalentTo(project), "A new test assembly must join the pre-build run.");
            Assert.That(AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name), Is.SupersetOf(listed));
        }

        [Test]
        public void Gate_HoldsInteractiveBuilds_UnlessSkipped()
        {
            var should = Gate.GetMethod("ShouldGate", BindingFlags.Public | BindingFlags.Static);
            string variable = (string)Gate.GetField("SkipVariable").GetValue(null);
            string old = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, null);
                var args = new object[] { null };
                Assert.That((bool)should.Invoke(null, args), Is.EqualTo(!UnityEngine.Application.isBatchMode), "Editor builds wait for the full suite.");
                Environment.SetEnvironmentVariable(variable, "1");
                Assert.That((bool)should.Invoke(null, args), Is.False);
            }
            finally { Environment.SetEnvironmentVariable(variable, old); }
            var preprocess = Gate.GetNestedType("Preprocess", BindingFlags.NonPublic);
            Assert.That(typeof(UnityEditor.Build.IPreprocessBuildWithReport).IsAssignableFrom(preprocess));
        }
    }
}
