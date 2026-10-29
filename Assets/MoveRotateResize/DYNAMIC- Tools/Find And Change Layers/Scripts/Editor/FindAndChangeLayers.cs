//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

namespace MoveRotateResize.Dynamic.Tools.FindAndChangeLayers
{
    public class FindAndChangeObjectLayers : EditorWindow
    {
        private int layerToSearch;                               
        private int newLayer = 0;                              
        private HashSet<GameObject> objectsWithLayer = new HashSet<GameObject>();
        private Dictionary<GameObject, int> objectNewLayers = new Dictionary<GameObject, int>();
        private HashSet<GameObject> prefabsWithLayer = new HashSet<GameObject>();
        private List<string> scenesWithLayer = new List<string>(); 
        private List<GameObject> foundObjectsInScenes = new List<GameObject>(); 

        private Vector2 scrollPositionObjects;
        private Vector2 scrollPositionPrefabs;
        private Vector2 scrollPositionScenes;

        private enum SearchResultList
        {
            None,
            FoundObjects,
            FoundPrefabs,
            FoundScenes
        }

        private SearchResultList currentList = SearchResultList.None;

        [MenuItem("Tools/MoveRotateResize/DYNAMIC: Tools/Find and Change Layers")]
        public static void ShowWindow()
        {
            GetWindow<FindAndChangeObjectLayers>("Find and Change Layers");
        }

        private void OnGUI()
        {
            GUIStyle largeBoldStyle = GetLargeBoldStyle();
            GUIStyle buttonStyle = GetButtonStyle();

            GUILayout.Label("DYNAMIC: Tools - Find and Change Layers", largeBoldStyle);
            GUILayout.Space(10);

            GUILayout.BeginVertical(EditorStyles.helpBox);
            layerToSearch = EditorGUILayout.LayerField("Layer to Search", layerToSearch);
            GUILayout.Space(10);

            if (GUILayout.Button("Search All Open Scenes", buttonStyle))
            {
                FindAllObjectsWithLayerInOpenScenes(layerToSearch);
                currentList = SearchResultList.FoundObjects; 
            }

            if (GUILayout.Button("Search Prefabs in Assets", buttonStyle))
            {
                FindPrefabsWithLayer(layerToSearch);
                currentList = SearchResultList.FoundPrefabs; 
            }

            if (GUILayout.Button("Search All Scenes in Project", buttonStyle))
            {
                FindAllScenesWithLayer(layerToSearch);
                currentList = SearchResultList.FoundScenes; 
            }

            GUILayout.EndVertical();
            
            GUILayout.Space(10);

            if (currentList == SearchResultList.FoundObjects)
            {
                if (objectsWithLayer.Count > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Found Objects:", EditorStyles.boldLabel);

                    scrollPositionObjects = EditorGUILayout.BeginScrollView(scrollPositionObjects, GUILayout.Height(1000));

                    var objectList = new List<GameObject>(objectsWithLayer);

                    foreach (var obj in objectList)
                    {
                        GUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.BeginHorizontal();

                        EditorGUILayout.ObjectField(obj, typeof(GameObject), true);

                        GUILayout.Label("New Layer", GUILayout.Width(100));

                        if (!objectNewLayers.ContainsKey(obj))
                        {
                            objectNewLayers[obj] = obj.layer;
                        }

                        int objNewLayer = EditorGUILayout.LayerField(objectNewLayers[obj], GUILayout.Width(100));
                        objectNewLayers[obj] = objNewLayer;

                        if (GUILayout.Button("Reassign", GUILayout.Width(100)))
                        {
                            ReassignLayerToObject(obj, objNewLayer);
                            FindAllObjectsWithLayerInOpenScenes(layerToSearch); 
                        }
                    
                        EditorGUILayout.Space(10);
                        EditorGUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                    }

                    EditorGUILayout.EndScrollView();

                    EditorGUILayout.Space(10);

                    GUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField("Reassign Layer to All Found Objects", EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("New Layer for All", GUILayout.Width(150));
                    newLayer = EditorGUILayout.LayerField(newLayer);
                    EditorGUILayout.EndHorizontal();

                    if (GUILayout.Button("Reassign Layer to All Found Objects", buttonStyle))
                    {
                        ReassignLayerToAllObjects(newLayer);
                        FindAllObjectsWithLayerInOpenScenes(layerToSearch);
                    }
                    GUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.LabelField("No objects found for the selected layer.");
                }
            }
            else if (currentList == SearchResultList.FoundPrefabs)
            {
                if (prefabsWithLayer.Count > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Found Prefabs:", EditorStyles.boldLabel);

                    scrollPositionPrefabs = EditorGUILayout.BeginScrollView(scrollPositionPrefabs, GUILayout.Height(1000));

                    var prefabList = new List<GameObject>(prefabsWithLayer);

                    foreach (var prefab in prefabList)
                    {
                        GUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.BeginHorizontal();

                        EditorGUILayout.ObjectField(prefab, typeof(GameObject), true);

                        GUILayout.Label("New Layer", GUILayout.Width(100));
                        int prefabNewLayer = EditorGUILayout.LayerField(prefab.layer, GUILayout.Width(100));

                        if (GUILayout.Button("Reassign", GUILayout.Width(100)))
                        {
                            ReassignLayerToPrefab(prefab, prefabNewLayer);
                            FindPrefabsWithLayer(layerToSearch);
                        }

                        EditorGUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                    }

                    EditorGUILayout.EndScrollView();

                    EditorGUILayout.Space(10);

                    GUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField("Reassign Layer to All Found Prefabs", EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("New Layer for All", GUILayout.Width(150));
                    newLayer = EditorGUILayout.LayerField(newLayer);
                    EditorGUILayout.EndHorizontal();

                    if (GUILayout.Button("Reassign Layer to All Found Prefabs", buttonStyle))
                    {
                        ReassignLayerToAllPrefabs(newLayer);
                        FindPrefabsWithLayer(layerToSearch); 
                    }
                    GUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.LabelField("No prefabs found for the selected layer.");
                }
            }
            else if (currentList == SearchResultList.FoundScenes)
            {
                if (scenesWithLayer.Count > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Scenes with Objects:", EditorStyles.boldLabel);

                    scrollPositionScenes = EditorGUILayout.BeginScrollView(scrollPositionScenes, GUILayout.Height(1000));
                    foreach (var scenePath in scenesWithLayer)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(System.IO.Path.GetFileNameWithoutExtension(scenePath)))
                        {
                            EditorSceneManager.OpenScene(scenePath);
                        }
                        GUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndScrollView(); 
                }
                else
                {
                    EditorGUILayout.LabelField("No scenes found with objects on the selected layer.");
                }
            }
        }

        GUIStyle GetLargeBoldStyle()
        {
            GUIStyle largeBoldStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            return largeBoldStyle;
        }

        GUIStyle GetButtonStyle()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14
            };
            return buttonStyle;
        }

        private void FindAllObjectsWithLayerInOpenScenes(int layer)
        {
            objectsWithLayer.Clear();
            objectNewLayers.Clear();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (scene.isLoaded)
                {
                    GameObject[] rootObjects = scene.GetRootGameObjects();

                    foreach (GameObject rootObject in rootObjects)
                    {
                        CheckGameObjectAndChildren(rootObject, layer);
                    }
                }
            }

            Debug.Log($"Found {objectsWithLayer.Count} objects with the specified layer in all open scenes.");
        }

        private void FindPrefabsWithLayer(int layer)
        {
            prefabsWithLayer.Clear();

            string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab");

            foreach (string guid in prefabGUIDs)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab.layer == layer)
                {
                    prefabsWithLayer.Add(prefab);
                }
            }

            Debug.Log($"Found {prefabsWithLayer.Count} prefabs with the specified layer in the Assets folder.");
        }

        private void FindAllScenesWithLayer(int layer)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                scenesWithLayer.Clear();
                foundObjectsInScenes.Clear(); 

                string[] allScenePaths = AssetDatabase.FindAssets("t:Scene");
                List<string> scenePaths = new List<string>();

                foreach (string guid in allScenePaths)
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(guid);

                    if (!scenePath.StartsWith("Packages/"))
                    {
                        scenePaths.Add(scenePath);
                    }
                }

                int totalScenes = scenePaths.Count;

                for (int i = 0; i < totalScenes; i++)
                {
                    EditorUtility.DisplayProgressBar("Searching Scenes", $"Checking scene {i + 1} of {totalScenes}...", (float)(i + 1) / totalScenes);

                    string scenePath = scenePaths[i];

                    if (EditorSceneManager.GetSceneByPath(scenePath).isLoaded)
                    {
                        continue; 
                    }

                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    bool foundInScene = false;

                    foreach (GameObject rootObject in rootObjects)
                    {
                        foundInScene = CheckGameObjectAndChildren(rootObject, layer) || foundInScene;
                    }

                    if (foundInScene)
                    {
                        scenesWithLayer.Add(scenePath);
                    }

                    EditorSceneManager.CloseScene(scene, true);
                }

                EditorUtility.ClearProgressBar();

                Debug.Log($"Found {scenesWithLayer.Count} scenes with objects on the specified layer in the Assets folder.");
            }
            else
            {
                Debug.Log("Scene was not saved. Aborting search for scenes with specified layer.");
            }
        }




        private bool CheckGameObjectAndChildren(GameObject obj, int layer)
        {
            bool found = false;

            if (obj.layer == layer)
            {
                objectsWithLayer.Add(obj);
                found = true;
            }

            foreach (Transform child in obj.transform)
            {
                found |= CheckGameObjectAndChildren(child.gameObject, layer);
            }

            return found;
        }

        private void ReassignLayerToObject(GameObject obj, int newLayer)
        {
            if (obj == null) return;

            Undo.RecordObject(obj, "Change Layer");

            obj.layer = newLayer;
            EditorUtility.SetDirty(obj);

            Debug.Log($"Reassigned {obj.name} to layer: {LayerMask.LayerToName(newLayer)}");
        }

        private void ReassignLayerToAllObjects(int newLayer)
        {
            Undo.SetCurrentGroupName("Reassign All");

            foreach (GameObject obj in objectsWithLayer)
            {
                ReassignLayerToObject(obj, newLayer);
            }

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Debug.Log($"Reassigned {objectsWithLayer.Count} objects to layer: {LayerMask.LayerToName(newLayer)}");
        }

        private void ReassignLayerToPrefab(GameObject prefab, int newLayer)
        {
            if (prefab == null) return;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            Undo.RecordObject(instance, "Change Layer");

            instance.layer = newLayer;
            EditorUtility.SetDirty(instance);

            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.UserAction);

            Debug.Log($"Reassigned prefab {prefab.name} to layer: {LayerMask.LayerToName(newLayer)}");

            Object.DestroyImmediate(instance);
        }

        private void ReassignLayerToAllPrefabs(int newLayer)
        {
            Undo.SetCurrentGroupName("Reassign All Prefabs");

            foreach (GameObject prefab in prefabsWithLayer)
            {
                ReassignLayerToPrefab(prefab, newLayer);
            }

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Debug.Log($"Reassigned {prefabsWithLayer.Count} prefabs to layer: {LayerMask.LayerToName(newLayer)}");
        }
    }
}