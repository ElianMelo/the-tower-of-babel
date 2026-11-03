using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Player;

namespace TowerOfBabel.Tests.EditMode
{
    /// <summary>Player builds compile without editor-only code, so runtime scripts must not call UnityEditor unguarded.</summary>
    public sealed class PlayerScriptCompileEditModeTests
    {
        [Test, Timeout(600000)]
        public void RuntimeScripts_CompileForTheStandalonePlayer()
        {
            var output = Path.Combine(Path.GetTempPath(), "TowerOfBabelPlayerScripts");
            if (Directory.Exists(output)) Directory.Delete(output, true);
            var settings = new ScriptCompilationSettings
            {
                target = BuildTarget.StandaloneWindows64,
                group = BuildTargetGroup.Standalone,
                options = ScriptCompilationOptions.None
            };
            var result = PlayerBuildInterface.CompilePlayerScripts(settings, output);
            try
            {
                Assert.That(result.assemblies, Does.Contain("TowerOfBabel.Gameplay.dll"), "Player scripts failed to compile; see the console for the error.");
                Assert.That(result.assemblies, Does.Contain("Assembly-CSharp.dll"), "Loose package scripts (Assembly-CSharp) failed to compile for the player.");
            }
            finally
            {
                if (Directory.Exists(output)) Directory.Delete(output, true);
            }
        }
    }
}
