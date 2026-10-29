//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MoveRotateResize.Dynamic.Tools.FindInstancedMaterials
{
    public class FindInstancedMaterials : EditorWindow
    {
        private List<GameObject> objectsWithInstancedMaterials = new List<GameObject>();
        private Dictionary<GameObject, Material> objectToOriginalMaterial = new Dictionary<GameObject, Material>();
        private Vector2 scrollPosition;

        [MenuItem("Tools/MoveRotateResize/DYNAMIC: Tools/Find Instanced Materials")]
        public static void ShowWindow()
        {
            GetWindow<FindInstancedMaterials>("Find Instanced Materials");
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ClearData();
            }
        }

        private void ClearData()
        {
            objectsWithInstancedMaterials.Clear();
            objectToOriginalMaterial.Clear();
        }

        void OnDestroy()
        {
            objectsWithInstancedMaterials.Clear();
            objectToOriginalMaterial.Clear();
        }

        void OnGUI()
        {
            GUIStyle largeBoldStyle = GetLargeBoldStyle();
            GUIStyle buttonStyle = GetButtonStyle();

            GUILayout.Label("DYNAMIC: Tools - Find Instanced Materials", largeBoldStyle);
            GUILayout.Space(10);

            if (GUILayout.Button("Search Scene", buttonStyle))
            {
                SearchForInstancedMaterials();
            }

            GUILayout.Space(10);

            if (objectsWithInstancedMaterials.Count > 0)
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(600));

                foreach (var obj in objectsWithInstancedMaterials)
                {
                    GUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
                    if (objectToOriginalMaterial.ContainsKey(obj))
                    {
                        Material originalMaterial = objectToOriginalMaterial[obj];
                        GUILayout.Label("Original Material:", GUILayout.Width(120));
                        Material newMaterial = (Material)EditorGUILayout.ObjectField(originalMaterial, typeof(Material), false);

                        if (newMaterial != originalMaterial)
                        {
                            objectToOriginalMaterial[obj] = newMaterial;
                        }

                        if (GUILayout.Button("Replace", GUILayout.Width(75)) && newMaterial != null)
                        {
                            ReplaceMaterialForObject(obj, newMaterial);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.Space(5);
                }

                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Label("No instanced materials found in the scene.", EditorStyles.wordWrappedLabel);
            }
        }

        private void SearchForInstancedMaterials()
        {
            objectsWithInstancedMaterials.Clear();
            objectToOriginalMaterial.Clear();

            Renderer[] renderers = GameObject.FindObjectsOfType<Renderer>();
            foreach (var renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;

                foreach (var material in materials)
                {
                    if (material == null || EditorUtility.IsPersistent(material)) continue;

                    if (material.name.EndsWith("(Instance)"))
                    {
                        string materialName = material.name.Substring(0, material.name.Length - "(Instance)".Length);
                        Material modifiedMaterial = new Material(material) { name = materialName };

                        if (!objectsWithInstancedMaterials.Contains(renderer.gameObject))
                        {
                            objectsWithInstancedMaterials.Add(renderer.gameObject);
                        }

                        if (!objectToOriginalMaterial.ContainsKey(renderer.gameObject))
                        {
                            Material originalMaterial = FindOriginalMaterial(modifiedMaterial);
                            objectToOriginalMaterial[renderer.gameObject] = originalMaterial;
                        }
                    }
                }
            }

            if (objectsWithInstancedMaterials.Count == 0)
            {
                Debug.Log("No instanced materials found in the scene.");
            }
        }

        private void ReplaceMaterialForObject(GameObject targetObject, Material newMaterial)
        {
            if (targetObject == null || newMaterial == null)
            {
                Debug.LogWarning("Invalid target object or material.");
                return;
            }

            Renderer renderer = targetObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"No Renderer found on {targetObject.name}");
                return;
            }

            Undo.RecordObject(renderer, "Replace Material");

            Material[] materials = renderer.materials; 
            bool materialReplaced = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i].name.Contains("(Instance)"))
                {
                    materials[i] = newMaterial; 
                    materialReplaced = true;
                }
            }

            if (materialReplaced)
            {
                renderer.materials = materials;
                EditorUtility.SetDirty(renderer);
                Debug.Log($"Material replaced on {targetObject.name}");

                objectsWithInstancedMaterials.Remove(targetObject);
                objectToOriginalMaterial.Remove(targetObject);
            }
            else
            {
                Debug.LogWarning($"No instanced material found to replace on {targetObject.name}");
            }
        }

        private Material FindOriginalMaterial(Material instanceMaterial)
        {
            if (instanceMaterial == null) return null;

            string materialNameWithoutSuffix = instanceMaterial.name;
            while (materialNameWithoutSuffix.Contains("(Instance)"))
            {
                materialNameWithoutSuffix = materialNameWithoutSuffix.Replace("(Instance)", "").Trim();
            }

            string[] guids = AssetDatabase.FindAssets($"{materialNameWithoutSuffix} t:Material");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material foundMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (foundMaterial != null && foundMaterial.name == materialNameWithoutSuffix)
                {
                    return foundMaterial;
                }
            }

            Debug.LogWarning($"No original material found for {instanceMaterial.name}. Please select one manually.");
            return null;
        }


        GUIStyle GetLargeBoldStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, fontStyle = FontStyle.Bold };
        }

        GUIStyle GetButtonStyle()
        {
            return new GUIStyle(GUI.skin.button) { fontSize = 14 };
        }
    }
}