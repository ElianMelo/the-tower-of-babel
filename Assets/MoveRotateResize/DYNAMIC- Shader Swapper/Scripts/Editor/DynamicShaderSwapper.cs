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
    public class TextureReplacementInfo
    {
        public string replacementPropertyName;
        public Texture texture;

        public TextureReplacementInfo(string replacementPropertyName, Texture texture)
        {
            this.replacementPropertyName = replacementPropertyName;
            this.texture = texture;
        }
    }

    public class DynamicShaderSwapper : EditorWindow
    {
        private List<Shader> originalShaders = null; 
        private List<Shader> replacementShaders = null; 
        private List<bool> removeButtons = new List<bool>();

        private int[] MetallicMapChannels01 = new int[1];
        private int[] AOMapChannels01 = new int[1];
        private int[] DetailMaskMapChannels01 = new int[1];
        private int[] SmoothnessMapChannels01 = new int[1];
        private string[] channelOptions = new string[] { "R", "G", "B", "A" };
        private Shader selectedShader;

        public ShaderPropertyGroup shaderProperties;        
        public string combinedSuffixName = "MaskMap";
        private List<string> baseColorMapProperty => shaderProperties.BaseColorMap;
        private List<string> normalMapProperty => shaderProperties.NormalMap;
        private List<string> maskMapProperty => shaderProperties.MaskMap;
        private List<string> metallicMapProperty => shaderProperties.MetallicMap;
        private List<string> ambientOcclusionMapProperty => shaderProperties.AmbientOcclusionMap;
        private List<string> smoothnessMapProperty => shaderProperties.SmoothnessMap;
        private List<string> detailMaskMapProperty => shaderProperties.DetailMaskMap;

        Shader newOriginalShader;
        Shader newReplacementShader;
        List<TextureReplacementInfo> copiedTexturesList = new List<TextureReplacementInfo>();

        private Vector2 scrollPosition;


        [MenuItem("Tools/MoveRotateResize/DYNAMIC- Shader Swapper/Shader Swapper", priority = 2)]
        static void Init()
        {
            DynamicShaderSwapper window = GetWindow<DynamicShaderSwapper>();
            window.titleContent = new GUIContent("Shader Swapper");
            window.Show();
            window.SetUp();
        }

        private void OnEnable()
        {
            int size = 1;

            if (shaderProperties == null)
            {
                shaderProperties = AssetDatabase.LoadAssetAtPath<ShaderPropertyGroup>("Assets/MoveRotateResize/DYNAMIC- Shader Swapper/Property Groups/Default.asset");
            }

            MetallicMapChannels01 = new int[size];
            AOMapChannels01 = new int[size];
            DetailMaskMapChannels01 = new int[size];
            SmoothnessMapChannels01 = new int[size];
            

            for (int i = 0; i < size; i++)
            {
                MetallicMapChannels01[i] = 0;  
                AOMapChannels01[i] = 1;       
                DetailMaskMapChannels01[i] = 2; 
                SmoothnessMapChannels01[i] = 3; 
            }

            if (originalShaders == null) originalShaders = new List<Shader>();
            if (replacementShaders == null) replacementShaders = new List<Shader>();
        }

        #region UI
        void OnGUI()
        {
            GUIStyle buttonStyle = GetButtonStyle();
            GUIStyle largeBoldStyle = GetLargeBoldStyle();
            
            GUILayout.Space(10);
            DrawHeader("Tools", largeBoldStyle);
            GUILayout.Space(10);
            DrawShaderSelection(buttonStyle);
            GUILayout.Space(10);
            DrawSeparator();

            GUILayout.Space(10);
            DrawHeader("Settings", largeBoldStyle);
            GUILayout.Space(10);

            DrawShaderSwapperSettings();

            GUILayout.Space(10);
            DrawSeparator();
            GUILayout.Space(10);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < originalShaders.Count; i++)
            {               
                GUILayout.BeginHorizontal();
                GUILayout.Label("Shaders", largeBoldStyle);           
                GUILayout.FlexibleSpace();
                GUILayout.Space(10);
                if (GUILayout.Button("X", new GUIStyle(GUI.skin.button) { normal = { textColor = Color.red }, fixedWidth = 30 }))
                {
                    RemoveShaderPair(i);
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Original Shader:", GUILayout.Width(125));
                newOriginalShader = (Shader)EditorGUILayout.ObjectField(originalShaders[i], typeof(Shader), false, GUILayout.ExpandWidth(true));
                GUILayout.Space(10);
                if (GUILayout.Button("Get Shader from Selected"))
                {
                    SetSelectedObjectShader(i); // Pass the index to set the correct shader
                }
                GUILayout.EndHorizontal();


                GUILayout.BeginHorizontal();
                GUILayout.Label("Replacement Shader:", GUILayout.Width(125));
                Shader newReplacementShader = (Shader)EditorGUILayout.ObjectField(replacementShaders[i], typeof(Shader), false, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();

                if (newOriginalShader != originalShaders[i] || newReplacementShader != replacementShaders[i])
                {
                    Undo.RecordObject(this, "Change Shader Pair");
                    originalShaders[i] = newOriginalShader;
                    replacementShaders[i] = newReplacementShader;
                }

                GUILayout.Space(10);
                DrawRemapChannels(i, largeBoldStyle);

                GUILayout.Space(10);
                GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(10));
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Swap", buttonStyle, GUILayout.Width(200), GUILayout.Height(40)))
            {
                SetUp();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();


            GUILayout.Space(10);


            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Swap Shader(s)", buttonStyle, GUILayout.Width(200), GUILayout.Height(40)))
            {
                ChangeShaders();
            }
            GUILayout.Space(20);
            if (GUILayout.Button("Swap Selected Shader(s)", buttonStyle, GUILayout.Width(200), GUILayout.Height(40)))
            {
                ChangeShadersOnSelectedObjects();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();


            GUILayout.Space(10);
        }

        void DrawHeader(string title, GUIStyle style)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, style);
            GUILayout.EndHorizontal();
        }

        void DrawShaderSelection(GUIStyle buttonStyle)
        {
            GUILayout.BeginHorizontal();
            selectedShader = (Shader)EditorGUILayout.ObjectField(selectedShader, typeof(Shader), false);
            if (GUILayout.Button("Fix Selected Objects Shaders", buttonStyle))
            {
                ChangeShadersInScene();
            }
            GUILayout.EndHorizontal();


            GUILayout.Space(5);


            GUILayout.BeginHorizontal();
            GUILayout.Space(5);
            EditorGUILayout.HelpBox("Used to fix the Hidden Shader Error you can sometimes get when changing render piplines. If you get error when chagign form URP to HRP set shader to Standard.", MessageType.Info);
            GUILayout.EndHorizontal();
        }

        void DrawShaderSwapperSettings()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Shader Property Group:", GUILayout.Width(150));
            shaderProperties = (ShaderPropertyGroup)EditorGUILayout.ObjectField(shaderProperties, typeof(ShaderPropertyGroup), true, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("Holds shader properites names which the script uses to find your Metallic, AO, Detail Mask, and Smoothness Maps.", MessageType.Info);
            GUILayout.EndHorizontal();
            

            GUILayout.Space(5);


            GUILayout.BeginHorizontal();
            GUILayout.Label("Combined Suffix Name:", GUILayout.Width(150));
            combinedSuffixName = EditorGUILayout.TextField(combinedSuffixName, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("Add a ending name to all combined Mask Maps. Can help you locate them in the editor. Leave blank if not required.", MessageType.Info);
            GUILayout.EndHorizontal();
        }

        void DrawRemapChannels(int index, GUIStyle style)
        {
            GUILayout.Label("Remap Mask Map Channels", style);           
            if (MetallicMapChannels01 != null && MetallicMapChannels01.Length > index)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Metallic Channel:", GUILayout.Width(140));
                MetallicMapChannels01[index] = EditorGUILayout.Popup(MetallicMapChannels01[index], channelOptions);
                GUILayout.EndHorizontal();
            }
            if (AOMapChannels01 != null && AOMapChannels01.Length > index)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("AO Channel:", GUILayout.Width(140));
                AOMapChannels01[index] = EditorGUILayout.Popup(AOMapChannels01[index], channelOptions);
                GUILayout.EndHorizontal();
            }
            if (DetailMaskMapChannels01 != null && DetailMaskMapChannels01.Length > index)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Detail Mask Channel:", GUILayout.Width(140));
                DetailMaskMapChannels01[index] = EditorGUILayout.Popup(DetailMaskMapChannels01[index], channelOptions);
                GUILayout.EndHorizontal();
            }
            if (SmoothnessMapChannels01 != null && SmoothnessMapChannels01.Length > index)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Smoothness Channel:", GUILayout.Width(140));
                SmoothnessMapChannels01[index] = EditorGUILayout.Popup(SmoothnessMapChannels01[index], channelOptions);
                GUILayout.EndHorizontal();
            }
        }

        void DrawSeparator()
        {
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(10));
        }

        GUIStyle GetButtonStyle()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13
            };
            return buttonStyle;
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

        void SetUp()
        {
            Undo.RecordObject(this, "Add Shader Pair");
            originalShaders.Add(null);
            replacementShaders.Add(null);

            removeButtons.Add(false);

            if (MetallicMapChannels01 != null)
                ArrayUtility.Add(ref MetallicMapChannels01, 0);
            if (AOMapChannels01 != null)
                ArrayUtility.Add(ref AOMapChannels01, 1);
            if (SmoothnessMapChannels01 != null)
                ArrayUtility.Add(ref SmoothnessMapChannels01, 2);
            if (DetailMaskMapChannels01 != null)
                ArrayUtility.Add(ref DetailMaskMapChannels01, 3);
        }

        private void SetSelectedObjectShader(int index)
        {
            // Get the currently selected GameObject in the hierarchy
            GameObject selectedObject = Selection.activeGameObject;

            if (selectedObject != null)
            {
                // Assuming the shader is assigned to a Renderer component
                Renderer renderer = selectedObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // Get the shader from the selected object's material
                    Shader selectedShader = renderer.sharedMaterial.shader;

                    // Ensure the index is within bounds
                    if (index >= 0 && index < originalShaders.Count)
                    {
                        // Assign the selected shader to the originalShaders list at the specified index
                        originalShaders[index] = selectedShader; 
                        
                        // Also update the class-level newOriginalShader variable
                        newOriginalShader = selectedShader; 
                    }
                    else
                    {
                        Debug.LogWarning("Index is out of bounds.");
                    }
                }
                else
                {
                    Debug.LogWarning("Selected GameObject does not have a Renderer component.");
                }
            }
            else
            {
                Debug.LogWarning("No GameObject is currently selected.");
            }
        }

    #endregion
        

    #region Change Shader(s)
        void ChangeShaders()
        {
            if (originalShaders == null || replacementShaders == null ||
                originalShaders.Count != replacementShaders.Count)
            {
                Debug.LogError("Please provide original and/or replacement shader(s).");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material");
            int totalMaterials = guids.Length;

            try
            {
                for (int j = 0; j < totalMaterials; j++)
                {
                    string guid = guids[j];
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

                    if (material != null)
                    {
                        ProcessMaterial(material);

                        float progress = (float)(j + 1) / totalMaterials;
                        EditorUtility.DisplayProgressBar("Swapping Shaders", $"Processing material {j + 1}/{totalMaterials}", progress);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        void ChangeShadersOnSelectedObjects()
        {
            if (originalShaders == null || replacementShaders == null ||
                originalShaders.Count != replacementShaders.Count)
            {
                Debug.LogError("Please provide original and replacement shaders of equal count.");
                return;
            }

            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("No GameObjects selected.");
                return;
            }

            int totalSelectedObjects = selectedObjects.Length;
            int processedObjects = 0;

            try
            {
                foreach (GameObject obj in selectedObjects)
                {
                    Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                    foreach (Renderer renderer in renderers)
                    {
                        foreach (Material material in renderer.sharedMaterials)
                        {
                            if (material != null)
                            {
                                ProcessMaterial(material);
                            }
                        }
                    }

                    processedObjects++;
                    float progress = (float)processedObjects / totalSelectedObjects;
                    EditorUtility.DisplayProgressBar("Swapping Shaders", $"Processing object {processedObjects}/{totalSelectedObjects}", progress);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // void ChangeShadersInOpenScene()
        // {
        //     if (originalShaders == null || replacementShaders == null ||
        //         originalShaders.Count != replacementShaders.Count)
        //     {
        //         Debug.LogError("Please provide original and/or replacement shader(s).");
        //         return;
        //     }

        //     List<Material> materialsInScene = new List<Material>();
        //     Renderer[] renderers = GameObject.FindObjectsOfType<Renderer>();
        //     foreach (Renderer renderer in renderers)
        //     {
        //         foreach (Material material in renderer.sharedMaterials)
        //         {
        //             if (material != null && !materialsInScene.Contains(material))
        //             {
        //                 materialsInScene.Add(material);
        //             }
        //         }
        //     }

        //     int totalMaterials = materialsInScene.Count;
        //     try
        //     {
        //         for (int j = 0; j < totalMaterials; j++)
        //         {
        //             Material material = materialsInScene[j];
        //             ProcessMaterial(material);

        //             float progress = (float)(j + 1) / totalMaterials;
        //             EditorUtility.DisplayProgressBar("Swapping Shaders in Scene", $"Processing material {j + 1}/{totalMaterials}", progress);
        //         }
        //     }
        //     finally
        //     {
        //         EditorUtility.ClearProgressBar();
        //     }

        //     AssetDatabase.SaveAssets();
        //     AssetDatabase.Refresh();
        // }

        void ProcessMaterial(Material material)
        {
            Dictionary<Texture, bool> originalReadability = new Dictionary<Texture, bool>();

            var textures = new List<Texture>();
            foreach (var property in material.GetTexturePropertyNames())
            {
                Texture texture = material.GetTexture(property);
                if (texture != null)
                {
                    textures.Add(texture);
                    if (!originalReadability.ContainsKey(texture))
                    {
                        originalReadability[texture] = IsTextureReadable(texture);
                    }
                }
            }

            for (int i = 0; i < originalShaders.Count; i++)
            {
                if (material.shader == originalShaders[i])
                {
                    Undo.RecordObject(material, "Change Shader");

                    ModifyTextureReadability(material, true);
                    CopyTexturesByList(material, baseColorMapProperty, originalShaders[i], replacementShaders[i]);
                    CopyTexturesByList(material, normalMapProperty, originalShaders[i], replacementShaders[i]);
                    CopyTexturesByList(material, maskMapProperty, originalShaders[i], replacementShaders[i]);
                    CopyTexturesByList(material, metallicMapProperty, originalShaders[i], replacementShaders[i]);
                    CopyTexturesByList(material, ambientOcclusionMapProperty, originalShaders[i], replacementShaders[i]);
                    CopyTexturesByList(material, detailMaskMapProperty, originalShaders[i], replacementShaders[i]);
                    CopyTexturesByList(material, smoothnessMapProperty, originalShaders[i], replacementShaders[i]);

                    ReapplyAllSavedTextures(material);
                    copiedTexturesList.Clear();

                    Material tempMaterial = new Material(originalShaders[i]);
                    bool hasMaskMapProperty = false;
                    foreach (string propertyName in maskMapProperty)
                    {
                        if (tempMaterial.HasProperty(propertyName))
                        {
                            hasMaskMapProperty = true;
                            break;
                        }
                    }

                    if (!hasMaskMapProperty)
                    {
                        GenerateAndAssignCombinedTexture(material);
                    }

                    material.shader = replacementShaders[i];

                    EditorUtility.SetDirty(material);

                    foreach (var texture in textures)
                    {
                        SetTextureReadability(texture, originalReadability[texture]);
                    }

                    DestroyImmediate(tempMaterial);

                    break;
                }
            }
        }

        bool IsTextureReadable(Texture texture)
        {
            return texture is Texture2D texture2D && texture2D.isReadable;
        }


       void SetTextureReadability(Texture texture, bool readable)
        {
            if (texture is Texture2D texture2D)
            {
                string assetPath = AssetDatabase.GetAssetPath(texture2D);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                if (importer.isReadable != readable)
                {
                    importer.isReadable = readable;
                    AssetDatabase.ImportAsset(assetPath);
                }
            }
        }
    #endregion

        void RemoveShaderPair(int index)
        {
            Undo.RecordObject(this, "Remove Shader Pair");
            originalShaders.RemoveAt(index);
            replacementShaders.RemoveAt(index);
            removeButtons.RemoveAt(index);
        }

        List<string> GetAllTexturePropertyNames()
        {
            List<string> allTexturePropertyNames = new List<string>();
            allTexturePropertyNames.AddRange(baseColorMapProperty);
            allTexturePropertyNames.AddRange(normalMapProperty);
            allTexturePropertyNames.AddRange(maskMapProperty);
            allTexturePropertyNames.AddRange(metallicMapProperty);
            allTexturePropertyNames.AddRange(ambientOcclusionMapProperty);
            allTexturePropertyNames.AddRange(detailMaskMapProperty);
            allTexturePropertyNames.AddRange(smoothnessMapProperty);
            return allTexturePropertyNames;
        }

    #region Copy Textures

        void LogShaderTextureProperties(Shader shader)
        {
            // Debug.Log($"Texture properties found in shader '{shader.name}':");
            int propertyCount = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < propertyCount; i++)
            {
                ShaderUtil.ShaderPropertyType propertyType = ShaderUtil.GetPropertyType(shader, i);
                string propertyName = ShaderUtil.GetPropertyName(shader, i);

                if (propertyType == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    // Debug.Log($"- Texture property: {propertyName}");
                }
            }
        }
        void CopyTexturesByList(Material material, List<string> propertyNames, Shader oringinalShader, Shader replacementShader)
        {
            LogShaderTextureProperties(material.shader);

            foreach (string originalPropertyName in propertyNames)
            {               
                bool shaderHasProperty = false;
                for (int i = 0; i < ShaderUtil.GetPropertyCount(material.shader); i++)
                {
                    if (ShaderUtil.GetPropertyName(material.shader, i) == originalPropertyName)
                    {
                        shaderHasProperty = true;
                        break;
                    }
                }

                if (!shaderHasProperty)
                {
                    continue;
                }

                if (!material.HasProperty(originalPropertyName))
                {
                    Debug.LogWarning($"Material '{material.name}' does not have property '{originalPropertyName}'");
                    continue;
                }

                Texture texture = material.GetTexture(originalPropertyName);
                if (texture == null)
                {
                    continue;
                }

                //string replacementPropertyName = GetEquivalentPropertyName01(originalPropertyName, propertyNames);
                string replacementPropertyName = GetEquivalentPropertyName(originalPropertyName, propertyNames, replacementShader);               
                if (string.IsNullOrEmpty(replacementPropertyName))
                {
                    //Debug.LogWarning($"Replacement property name for '{originalPropertyName}' is empty or null");
                    continue;
                }

                copiedTexturesList.Add(new TextureReplacementInfo(replacementPropertyName, texture));
                Debug.Log($"Copied texture from '{originalPropertyName}' to '{replacementPropertyName}' in material '{material.name}'");
            }
        }
        
        public void ReapplyAllSavedTextures(Material material)
        {
            if (material == null)
            {
                //Debug.LogWarning("Material is null. Cannot reapply textures.");
                return;
            }

            if (copiedTexturesList == null)
            {
                //Debug.LogWarning("copiedTexturesList is null. No textures to reapply.");
                return;
            }

            foreach (var textureInfo in copiedTexturesList)
            {
                string replacementPropertyName = textureInfo.replacementPropertyName;
                Texture texture = textureInfo.texture;

                material.SetTexture(replacementPropertyName, texture);
            }
        }

        string GetMaskMap(string originalPropertyName, Shader shader)
        {
           
            if (maskMapProperty.Contains(originalPropertyName))
            {
                return "_MaskMap";
            }
            else
            {
                return originalPropertyName;
            }
        }

        // Old has jsut has to contain some of the same name 
        // string GetEquivalentPropertyName01(string originalPropertyName, List<string> propertyNames)
        // {
        //     if (baseColorMapProperty.Contains(originalPropertyName))
        //     {
        //         return "_BaseColorMap";
        //     }
        //     else if (normalMapProperty.Contains(originalPropertyName))
        //     {
        //         return "_NormalMap";
        //     }
        //     else if (maskMapProperty.Contains(originalPropertyName))
        //     {
        //         return "_MaskMap";
        //     }

        //     foreach (string propertyName in propertyNames)
        //     {
        //         if (propertyName.Contains(originalPropertyName))
        //         {
        //             return propertyName;
        //         }
        //     }

        //     return string.Empty; 
        // }

        string GetEquivalentPropertyName(string originalPropertyName, List<string> propertyNames, Shader replacementShader)
        {
            for (int i = 0; i < ShaderUtil.GetPropertyCount(replacementShader); i++)
            {
                string replacementPropertyName = ShaderUtil.GetPropertyName(replacementShader, i);

                // Check if the replacement property name is part of the propertyNames list
                if (propertyNames.Contains(replacementPropertyName))
                {
                    return replacementPropertyName;
                }
            }

            return null; 
        }

    #endregion


    #region Generate Mask Map
        void GenerateAndAssignCombinedTexture(Material material)
        {
            //Debug.Log("Starting texture generation for material: " + material.name);

            int width = 4096;
            int height = 4096;

            Texture2D combinedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            combinedTexture.wrapMode = TextureWrapMode.Repeat;
            combinedTexture.filterMode = FilterMode.Trilinear;

            //Debug.Log("Created combined texture with width: " + width + " and height: " + height);

            // Retrieve and resize individual maps
            Texture2D metallicMap = GetAndResizeTexture(material, metallicMapProperty, width, height);
            Texture2D aoMap = GetAndResizeTexture(material, ambientOcclusionMapProperty, width, height);
            Texture2D detailMaskMap = GetAndResizeTexture(material, detailMaskMapProperty, width, height, true);
            Texture2D smoothnessMap = GetAndResizeTexture(material, smoothnessMapProperty, width, height);

            // Debug.Log("Metallic Map: " + (metallicMap != null ? metallicMap.name : "Failed to load"));
            // Debug.Log("Ambient Occlusion Map: " + (aoMap != null ? aoMap.name : "Failed to load"));
            // Debug.Log("Detail Mask Map: " + (detailMaskMap != null ? detailMaskMap.name : "Failed to load"));
            // Debug.Log("Smoothness Map: " + (smoothnessMap != null ? smoothnessMap.name : "Failed to load"));

            ApplyTexturesToCombined(metallicMap, aoMap, detailMaskMap, smoothnessMap, combinedTexture);
            //Debug.Log("Applied textures to combined texture.");

            string combinedTexturePath = "Assets/MoveRotateResize/Dynamic- Shader Swapper/Mask Maps Combined";
            string materialName = material.name.Replace(" ", "_");
            string combinedTextureName = $"{materialName}_{combinedSuffixName}.png";
            string fullPath = System.IO.Path.Combine(combinedTexturePath, combinedTextureName);
            //Debug.Log("Saving combined texture to: " + fullPath);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
            byte[] bytes = combinedTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(fullPath, bytes);
            //Debug.Log("Saved combined texture to file.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorApplication.delayCall += () =>
            {
                //Debug.Log("Setting texture importer settings for: " + fullPath);
                SetTextureImporterSettings(fullPath);
                AssignCombinedTextureToMaterial(material, fullPath);
                //Debug.Log("Combined texture assigned to material.");
            };
        }

        void AssignCombinedTextureToMaterial(Material material, string texturePath)
        {
            string maskMapPropertyName = GetMaskMap("_MaskMap", material.shader);
            if (!string.IsNullOrEmpty(maskMapPropertyName))
            {
                Undo.RecordObject(material, "Assign Mask Map");
                Texture2D savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (savedTexture != null)
                {
                    material.SetTexture(maskMapPropertyName, savedTexture);
                    //Debug.Log($"Copied Mask Mask to '{material.name}'");
                }
                else
                {
                    Debug.LogError("Failed to load saved combined texture.");
                }
            }
        }

        Texture2D GetAndResizeTexture(Material material, List<string> propertyNames, int width, int height, bool createWhiteIfNotFound = false)
        {
            string propertyName = GetFirstExistingProperty(material, propertyNames);
            if (string.IsNullOrEmpty(propertyName))
            {
                Color defaultColor = createWhiteIfNotFound ? Color.white : Color.black;
                return CreateColouredTexture(width, height, defaultColor);
            }

            Texture texture = material.GetTexture(propertyName);
            if (texture != null && texture is Texture2D)
            {
                Texture2D originalTexture = (Texture2D)texture;

                if (originalTexture.isReadable)
                {
                    if (originalTexture.width != width || originalTexture.height != height)
                    {
                        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                        Graphics.Blit(originalTexture, rt);

                        Texture2D resizedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                        RenderTexture.active = rt;
                        resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        resizedTexture.Apply();

                        RenderTexture.active = null;
                        RenderTexture.ReleaseTemporary(rt);
                        resizedTexture.name = originalTexture.name + "_Resized";

                        return resizedTexture;
                    }
                    else
                    {
                        return originalTexture;
                    }
                }
                else
                {
                    Debug.LogWarning("Texture is not readable: " + propertyName);
                }
            }
            else
            {
                Debug.LogWarning("Texture not found or not of type Texture2D: " + propertyName);
            }

            Color fallbackColor = createWhiteIfNotFound ? Color.white : Color.black;
            return CreateColouredTexture(width, height, fallbackColor);
        }


        private void ApplyTexturesToCombined(Texture2D metallicMap, Texture2D aoMap, Texture2D detailMaskMap, Texture2D smoothnessMap, Texture2D combinedTexture)
        {
            int width = combinedTexture.width;
            int height = combinedTexture.height;
            Color[] combinedColors = new Color[width * height];

            Color[] metallicColors = metallicMap != null ? metallicMap.GetPixels() : new Color[width * height];
            Color[] aoColors = aoMap != null ? aoMap.GetPixels() : new Color[width * height];
            Color[] detailMaskColors = detailMaskMap != null ? detailMaskMap.GetPixels() : new Color[width * height];
            Color[] smoothnessColors = smoothnessMap != null ? smoothnessMap.GetPixels() : new Color[width * height];

            System.Threading.Tasks.Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;

                    int metallicChannelIndex = Mathf.Clamp(MetallicMapChannels01[0], 0, 3);
                    int aoChannelIndex = Mathf.Clamp(AOMapChannels01[0], 0, 3);
                    int detailMaskChannelIndex = Mathf.Clamp(DetailMaskMapChannels01[0], 0, 3);
                    int smoothnessChannelIndex = Mathf.Clamp(SmoothnessMapChannels01[0], 0, 3);

                    combinedColors[index] = new Color(
                        metallicColors[index][metallicChannelIndex],
                        aoColors[index][aoChannelIndex],
                        detailMaskColors[index][detailMaskChannelIndex],
                        smoothnessColors[index][smoothnessChannelIndex]
                    );
                }
            });

            combinedTexture.SetPixels(combinedColors);
            combinedTexture.Apply();
        }


        void SetTextureImporterSettings(string texturePath)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (textureImporter != null)
            {
                textureImporter.alphaSource = TextureImporterAlphaSource.None;
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.isReadable = false;
                textureImporter.sRGBTexture = false;

                //https://docs.unity.cn/560/Documentation/ScriptReference/TextureImporter.GetPlatformTextureSettings.html
                var webGL = textureImporter.GetPlatformTextureSettings("WebGL");
                webGL.overridden = true;
                webGL.format = TextureImporterFormat.DXT5;
                textureImporter.SetPlatformTextureSettings(webGL);

                var windows = textureImporter.GetPlatformTextureSettings("Standalone");
                windows.overridden = true;
                windows.format = TextureImporterFormat.DXT5;
                textureImporter.SetPlatformTextureSettings(windows);

                var web = textureImporter.GetPlatformTextureSettings("Web");
                web.overridden = true;
                web.format = TextureImporterFormat.DXT5;
                textureImporter.SetPlatformTextureSettings(web);

                textureImporter.maxTextureSize = 1024;

                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                
            }
        }
    
        Texture2D CreateColouredTexture(int width, int height, Color color)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            return texture;
        }

        void ModifyTextureReadability(Material material, bool makeReadable)
        {
            List<string> allTexturePropertyNames = GetAllTexturePropertyNames();

            foreach (string propertyName in allTexturePropertyNames)
            {
                if (material.HasProperty(propertyName))
                {
                    Texture texture = material.GetTexture(propertyName);
                    if (texture != null)
                    {
                        string path = AssetDatabase.GetAssetPath(texture);
                        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer != null)
                        {
                            importer.isReadable = makeReadable;
                            AssetDatabase.ImportAsset(path);
                        }
                    }
                }
            }
        }
    #endregion

        string GetFirstExistingProperty(Material material, List<string> propertyNames)
        {
            if (material == null)
            {
                Debug.LogWarning("Material is null.");
                return null;
            }
            Debug.Log($"Checking Textures for Material: {material.name} with shader: {material.shader.name}");
            
            foreach (string propertyName in propertyNames)
            {
                if (material.HasProperty(propertyName))
                {
                    if (material.GetTexture(propertyName) != null)
                    {
                        Debug.Log($"Found existing property: {propertyName}");
                        return propertyName;
                    }
                    else
                    {
                        Debug.LogWarning($"Property '{propertyName}' exists but has no texture assigned.");
                    }
                }
                else
                {
                    //Debug.LogWarning($"Property '{propertyName}' does not exist on the material.");
                }
            }
            
            Debug.LogWarning("No matching properties with textures found.");
            return null;
        }


        private void ChangeShadersInScene()
        {
            if (selectedShader == null)
            {
                Debug.LogError("No shader selected.");
                return;
            }

            GameObject[] selectedObjects = Selection.gameObjects;

            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("No objects selected in the scene.");
                return;
            }

            foreach (var obj in selectedObjects)
            {
                MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>();

                foreach (var renderer in renderers)
                {
                    Material[] materials = renderer.sharedMaterials;

                    foreach (var material in materials)
                    {
                        if (material != null && material.shader != selectedShader)
                        {
                            material.shader = selectedShader;
                            EditorUtility.SetDirty(material);
                            //Debug.Log($"Shader changed to {selectedShader.name} for material {material.name} on object {obj.name}.");
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif