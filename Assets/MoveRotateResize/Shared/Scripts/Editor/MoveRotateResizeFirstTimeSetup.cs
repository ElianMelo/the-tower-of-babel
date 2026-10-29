//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine.Rendering;
#if USING_URP
using UnityEngine.Rendering.Universal;
#endif
#if USING_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace MoveRotateResize.EditorInterface
{
    #if UNITY_EDITOR
    public class MoveRotateResizeFirstTimeSetup: Editor
    {
        static string[] packageNames = new string[]
        {
            "com.unity.textmeshpro",
            "com.unity.ugui",
            "com.unity.surface-gradient-bump-mapping@0.2.1",
            "com.unity.polybrush",
            "com.unity.visualeffectgraph",
            "com.unity.shadergraph"
        };
        
        private static string[] gitRepositories = new string[]
        {
            // Removed for better compatibility with Unity Entity Sub Scenes
            //"https://github.com/WooshiiDev/HierarchyDecorator.git",
        };


        [MenuItem("Tools/MoveRotateResize/DYNAMIC- RainWetness/First Time Setup/Set Up", priority = -10000)]
        public static void FirstTimeSetup()
        {
            InstallDefinitions();
            EditorApplication.delayCall += InstallPackages;
            EditorApplication.delayCall += InstallShaders;
            EditorApplication.delayCall += MoveRotateResizeInputManagerSetup.AddDPadAndRightStickAxes;
        }
        
        static void InstallDefinitions()
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                if (GraphicsSettings.defaultRenderPipeline.GetType().Name.Contains("UniversalRenderPipelineAsset"))
                {
                    Debug.Log("Using Universal Render Pipeline (URP)");
                    if (!symbols.Contains("USING_URP"))
                    {
                        symbols += ";USING_URP";
                        Debug.Log("Scripting Define Symbol 'USING_URP' added."); 
                    }
                }
                else if (GraphicsSettings.defaultRenderPipeline.GetType().Name.Contains("HDRenderPipelineAsset"))
                {
                    Debug.Log("Using High Definition Render Pipeline (HDRP)");
                    if (!symbols.Contains("USING_HDRP"))
                    {
                        symbols += ";USING_HDRP";
                        Debug.Log("Scripting Define Symbol 'USING_HDRP' added.");
                    }
                }
                else{
                    Debug.Log("Using a custom Scriptable Render Pipeline");
                }
            }
            else{
                Debug.Log("Using built-in render pipeline (Legacy)");
            }

            string unityVersion = Application.unityVersion;
            if (unityVersion.StartsWith("2022"))
            {
                if (!symbols.Contains("USING_UNITY_2022"))
                {
                    symbols += ";USING_UNITY_2022";
                    Debug.Log("Scripting Define Symbol 'USING_UNITY_2022' added.");
                }
            }
            else if (unityVersion.StartsWith("2023") || unityVersion.StartsWith("6") || unityVersion.StartsWith("("))
            {
               if (!symbols.Contains("USING_UNITY_6"))
                {
                    symbols += ";USING_UNITY_6";
                    Debug.Log("Scripting Define Symbol 'USING_UNITY_6' added.");
                }
            }

            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
        }

        private static void InstallPackages()
        {
            foreach (string packageName in packageNames)
            {
                AddRequest request = Client.Add(packageName);
                while (!request.IsCompleted)
                {
                }

                if (request.Status == StatusCode.Success)
                {
                    Debug.Log("Package installed successfully: " + packageName);
                }
                else if (request.Status >= StatusCode.Failure)
                {
                    //Debug.LogError("Package installation failed: " + request.Error.message);
                }
            }

            foreach (string gitRepo in gitRepositories)
            {
                string packageURL = gitRepo;
                AddRequest request = Client.Add(packageURL);
                while (!request.IsCompleted)
                {
                }

                if (request.Status == StatusCode.Success)
                {
                    Debug.Log("Package installed successfully from Git repository: " + gitRepo);
                }
                else if (request.Status >= StatusCode.Failure)
                {
                    Debug.LogError("Package installation failed from Git repository: " + gitRepo + ", Error: " + request.Error.message);
                }
            }
        }

        public static void InstallShaders()
        {
            string unityVersion = Application.unityVersion;

            const string HDRP_2022_PATH = "Assets/MoveRotateResize/Dynamic- RainWetness/Shaders/RainWetness Shaders- 2022.3 and Below.unitypackage";
            const string HDRP_2023_PATH = "Assets/MoveRotateResize/Dynamic- RainWetness/Shaders/RainWetness Shaders- 2023.2 and Above.unitypackage";

            if (EditorUtility.DisplayDialog(
                    "Install Shaders",
                    "Do you want to install the shaders for your Unity version? This action cannot be undone.",
                    "Install Shaders",
                    "Skip"))
            {
                if (unityVersion.StartsWith("2022"))
                {
                    #if USING_HDRP
                    ImportPackage(HDRP_2022_PATH);
                    #endif
                }
                else if (unityVersion.StartsWith("2023") || unityVersion.StartsWith("6") || unityVersion.StartsWith("("))
                {
                    #if USING_HDRP
                    ImportPackage(HDRP_2023_PATH);
                    #endif
                }
                else
                {
                    Debug.LogError("Unity version not supported. No demo package installed.");
                }
            }
            else
            {
                Debug.Log("Shader installation skipped.");
            }
        }

        private static void ImportPackage(string packagePath)
        {
            AssetDatabase.ImportPackage(packagePath, false);
            Debug.Log("Package installed: " + packagePath);
        }
    }
    #endif
}