//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MoveRotateResize.Dynamic.FindUnusedGameObjects
{
    public class FindUnusedGameObjects : EditorWindow
    {
        private string folderPath = "Assets"; 
        private List<GameObject> unusedGameObjects = new List<GameObject>();
        private bool isSearching = false; 
        private Vector2 scrollPosition; 

        [MenuItem("Tools/MoveRotateResize/DYNAMIC: Tools/Find Unused GameObjects")]
        public static void ShowWindow()
        {
            GetWindow(typeof(FindUnusedGameObjects));
        }

        private void OnGUI()
        {
            GUILayout.Label("Find Unused GameObjects in Folder", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            folderPath = EditorGUILayout.TextField("Folder Path:", folderPath);

            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string selectedFolder = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selectedFolder))
                {
                    if (selectedFolder.StartsWith(Application.dataPath))
                    {
                        folderPath = "Assets" + selectedFolder.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        Debug.LogError("Selected folder must be inside the Assets folder.");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            if (isSearching)
            {
                if (GUILayout.Button("Cancel Search"))
                {
                }
            }
            else
            {
                if (GUILayout.Button("Find Unused GameObjects"))
                {
                    FindUnusedGameObjectsInFolder();
                }
            }

            if (unusedGameObjects.Count > 0)
            {
                GUILayout.Label("Unused GameObjects:", EditorStyles.boldLabel);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

                foreach (GameObject go in unusedGameObjects)
                {
                    if (GUILayout.Button(go.name, EditorStyles.linkLabel))
                    {
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void FindUnusedGameObjectsInFolder()
        {
            isSearching = true;
            unusedGameObjects.Clear();

            string[] allAssetGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            HashSet<GameObject> usedGameObjects = new HashSet<GameObject>();

            int sceneCount = EditorSceneManager.sceneCount;

            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = EditorSceneManager.GetSceneAt(i);

                if (!scene.isLoaded)
                    continue;

                GameObject[] sceneRootObjects = scene.GetRootGameObjects();

                foreach (GameObject go in sceneRootObjects)
                {
                    Component[] components = go.GetComponentsInChildren<Component>(true);

                    foreach (Component comp in components)
                    {
                        if (comp != null)
                        {
                            var fields = comp.GetType().GetFields();
                            foreach (var field in fields)
                            {
                                if (field.FieldType == typeof(GameObject))
                                {
                                    GameObject referencedGo = field.GetValue(comp) as GameObject;

                                    if (referencedGo != null)
                                    {
                                        usedGameObjects.Add(referencedGo);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            foreach (string guid in allAssetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab != null && !usedGameObjects.Contains(prefab))
                {
                    unusedGameObjects.Add(prefab);
                }
            }

            isSearching = false;
            Debug.Log($"{unusedGameObjects.Count} unused GameObjects found in folder {folderPath}");
        }
    }
}
