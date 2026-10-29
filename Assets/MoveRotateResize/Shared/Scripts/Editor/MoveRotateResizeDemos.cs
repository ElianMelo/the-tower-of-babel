//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.1
//------------------------------------------------------------------------------

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MoveRotateResize.EditorInterface {
#if UNITY_EDITOR
    public class MoveRotateResizeDemos : Editor {
        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- RainWetness Shaders (Static Objects)", priority = 1)]
        public static void LoadDemoStatueDemo() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- RainWetness Shaders (Static Objects).unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- RainWetness Shaders (Rotating Objects)", priority = 1)]
        public static void LoadRotatingObject() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- RainWetness Shaders (Rotating Objects).unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- RainWetness Shaders (Skinned Mesh Renderers)", priority = 1)]
        public static void LoadSkinnedMeshRenderer() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- RainWetness Shaders (Skinned Mesh Renderers).unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- DYNAMIC- Rain Audio", priority = 1)]
        public static void ShowcaseRainWetnessAllFeatures() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- DYNAMIC- Rain Audio.unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- DYNAMIC- Occlusion (Static Object)", priority = 1)]
        public static void LoadShowcaseShowcaseDYNAMICOcclusionStaticObjects() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                string scenePath = "Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- DYNAMIC- Occlusion (Static Objects).unity";
                SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

                if (scene == null) {
                    Debug.LogError($"Scene not found at path: {scenePath}");
                    return;
                }

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- DYNAMIC- Falling Rain (Day)", priority = 1)]
        public static void LoadShowcaseDYNAMICFallingRainDay() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- DYNAMIC- Falling Rain (Day).unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- DYNAMIC- Falling Rain (Night)", priority = 1)]
        public static void LoadShowcaseDYNAMICFallingRainNight() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- DYNAMIC- Falling Rain (Night).unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- DYNAMIC- Occlusion (Moving Objects)", priority = 1)]
        public static void LoadShowcaseShowcaseDYNAMICOcclusionMovingObjects() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- DYNAMIC- Occlusion (Moving Objects).unity", OpenSceneMode.Single);
            }
        }
        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- DYNAMIC- Dripping Rain", priority = 1)]
        public static void LoadShowcaseShowcaseDYNAMICDrippingRain() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- DYNAMIC- Dripping Rain.unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- RainWetness Shaders- Subsurface Scattering", priority = 1)]
        public static void LoadSubsurface() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- RainWetness Shaders- Subsurface Scattering.unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- RainWetness Shaders- Anisotropy ", priority = 1)]
        public static void LoadAnisotropy() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- RainWetness Shaders- Anisotropy.unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- RainWetness Shaders- Iridescence", priority = 1)]
        public static void LoadIridescent() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- RainWetness Shaders- Iridescence.unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- RainWetness Shaders- Glass and Translucent", priority = 1)]
        public static void LoadGlassAndTranslucentDemo() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- RainWetness Shaders- Glass and Translucent.unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- RainWetness Shaders- Random Colors Features", priority = 1)]
        public static void LoadShowcaseRandomColorsFeatures() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/Dynamic- RainWetness/Demos/Showcase- RainWetness Shaders- Random Colors Features.unity", OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/MoveRotateResize/Demos/Showcase- RainWetness All Features (Enviro Integration)", priority = 1)]
        public static void LoadDemoRainWetnessEnviroSkyAndWeatherIntegration() {
            string scenePath = "Assets/MoveRotateResize/DYNAMIC- RainWetness/Integrations/Enviro/Demo/Showcase- RainWetness All Features-Enviro.unity";
            string packageName = "Enviro Integration Demo";
            const string EnviroIntegrationPath = "Assets/MoveRotateResize/DYNAMIC- RainWetness/Integrations/Enviro/Enviro Integration Demo.unitypackage";

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) {
                    Debug.LogError("Enviro demo scene is missing. Attempting to install the package: " + packageName);
                    ImportPackage(EnviroIntegrationPath);
                } else {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
            }
        }

        static void ImportPackage(string packagePath) {
            AssetDatabase.ImportPackage(packagePath, false);
            Debug.Log("Enviro 3 Integration Demo Scene is now installed. Please open scene again to play. (this demo requires Enviro 3 from the asset store");
        }

        static void OpenEnviroDemo() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/MoveRotateResize/DYNAMIC- RainWetness/Demos/Showcase- RainWetness All Features-Enviro.unity", OpenSceneMode.Single);
            }
        }
    }
#endif
}