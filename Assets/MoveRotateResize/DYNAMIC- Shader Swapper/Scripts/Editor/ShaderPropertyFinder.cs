//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.1
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_EDITOR
namespace MoveRotateResize.Dynamic.ShaderSwapper
{
    public class ShaderPropertyFinder : EditorWindow
    {
        private Shader selectedShader;
        private Vector2 scrollPos;
        private Dictionary<string, List<string>> categoryToNames = new Dictionary<string, List<string>>();
        public ShaderPropertyGroup shaderProperties; 
        public string combinedSuffixName = "MaskMap";


        [MenuItem("Tools/MoveRotateResize/DYNAMIC- Shader Swapper/Shader Property Finder", priority = 1)]
        static void Init()
        {
            ShaderPropertyFinder window = GetWindow<ShaderPropertyFinder>();
            window.titleContent = new GUIContent("Shader Property Finder");
            window.Show();
        }

        private void OnEnable() 
        { 
            if (shaderProperties != null)
            {
                shaderProperties.OnShaderPropertyUpdated += ListTextureProperties;
            }

            ListTextureProperties();
        }

        private void OnDisable() 
        {
            // Unsubscribe from the event when the window is disabled
            if (shaderProperties != null)
            {
                shaderProperties.OnShaderPropertyUpdated -= ListTextureProperties;
            }
        }

        private void OnGUI()  
        {
            GUIStyle largeBoldStyle = GetLargeBoldStyle();

            GUILayout.Label("Shader Property Finder", largeBoldStyle);

            GUILayout.Space(10);

            selectedShader = (Shader)EditorGUILayout.ObjectField("Shader", selectedShader, typeof(Shader), false);
            GUILayout.Space(5);
            shaderProperties = (ShaderPropertyGroup)EditorGUILayout.ObjectField("Shader Properties", shaderProperties, typeof(ShaderPropertyGroup), false);

         
            ListTextureProperties();
            

            GUILayout.Space(10);

            if (selectedShader == null)
            {
                GUILayout.Label("Warning: No shader selected.", EditorStyles.boldLabel);
            }

            GUILayout.Space(10);

            if (Selection.activeGameObject == null && selectedShader == null)
            {
                GUILayout.Label("Warning: No GameObject selected in the hierarchy.", EditorStyles.boldLabel);
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Set Selected Object Shader"))
            {
                SetSelectedObjectShader();
            }

            if (selectedShader != null && shaderProperties != null)
            {
                if (categoryToNames.Count > 0)
                {
                    GUILayout.Space(10);
                    scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                    foreach (var kvp in categoryToNames)
                    {
                        DrawCategoryList(kvp.Key, kvp.Value);
                    }

                    EditorGUILayout.EndScrollView();
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

        private void ListTextureProperties()
        {
            if (selectedShader == null || shaderProperties == null)
            {
                return;
            }

            var newCategoryToNames = new Dictionary<string, List<string>>();
            var shaderPropertyNames = new HashSet<string>(); 

            int propertyCount = ShaderUtil.GetPropertyCount(selectedShader);

            // Initialize categories based on the ShaderPropertyGroup
            foreach (var property in shaderProperties.GetType().GetFields())
            {
                string categoryName = property.Name;
                newCategoryToNames[categoryName] = new List<string>();
            }

            for (int i = 0; i < propertyCount; i++)
            {
                if (ShaderUtil.GetPropertyType(selectedShader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    string propertyName = ShaderUtil.GetPropertyName(selectedShader, i);
                    shaderPropertyNames.Add(propertyName);

                    // Check if the propertyName exists in any of the ShaderPropertyGroup lists
                    foreach (var field in shaderProperties.GetType().GetFields())
                    {
                        var propertyList = field.GetValue(shaderProperties) as List<string>;
                        if (propertyList != null && propertyList.Contains(propertyName))
                        {
                            newCategoryToNames[field.Name].Add(propertyName);
                            break;
                        }
                    }
                }
            }

            // Add any remaining properties that weren't categorized
            foreach (var propertyName in shaderPropertyNames)
            {
                bool foundInShaderProperties = false;

                foreach (var propertyList in newCategoryToNames.Values)
                {
                    if (propertyList.Contains(propertyName))
                    {
                        foundInShaderProperties = true;
                        break;
                    }
                }

                if (!foundInShaderProperties)
                {
                    if (!newCategoryToNames.ContainsKey("No Category"))
                    {
                        newCategoryToNames["No Category"] = new List<string>();
                    }
                    newCategoryToNames["No Category"].Add(propertyName);
                }
            }

            categoryToNames.Clear();
            foreach (var category in newCategoryToNames.Keys)
            {
                categoryToNames[category] = newCategoryToNames[category];
            }

            Repaint();
        }

        private void DrawCategoryList(string categoryName, List<string> names)
        {
            if (names == null || names.Count == 0) return;

            GUIStyle largeBoldStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField(categoryName, largeBoldStyle);
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(5));

            foreach (var name in names)
            {
                Color textColor = Color.white;
                if (categoryName == "No Category")
                {
                    textColor = Color.red; 
                }
                else if (categoryToNames[categoryName].Contains(name))
                {
                    textColor = Color.green;
                }

                GUI.color = textColor;
                EditorGUILayout.LabelField(name);
                GUI.color = Color.white; 
            }
            GUILayout.Space(30);
        }

        private void SetSelectedObjectShader()
        {
            GameObject selectedGameObject = Selection.activeGameObject;

            if (selectedGameObject == null)
            {
                Debug.LogWarning("No GameObject selected.");
                return;
            }

            // Get the first material of the selected GameObject
            var renderer = selectedGameObject.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterials.Length > 0)
            {
                // Get the first material and its shader
                Material selectedMaterial = renderer.sharedMaterials[0];
                selectedShader = selectedMaterial.shader; // Assign the shader to the selectedShader property

                Debug.Log($"Shader {selectedShader.name} assigned from {selectedGameObject.name}");
            }
            else if (selectedShader == null)
            {
                // Show warning only if no shader is assigned in selectedShader
                Debug.LogWarning("Selected GameObject has no materials and selectedShader is null.");
            }
        }

    }
}
#endif