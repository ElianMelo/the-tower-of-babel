//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.1
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEditor;
using UnityEngine.VFX;
using System.IO;

namespace MoveRotateResize.Dynamic.PuddlePainter
{
    #if UNITY_EDITOR
    [CustomEditor(typeof(DynamicPuddlePainter))]
    [ExecuteInEditMode]
    public class PuddlePainterEditor : Editor
    {
        // References
        private DynamicPuddlePainter script;
        private VisualEffect vfxComponent;
        
        // Settings
        public bool isEnabled = false;
        [SerializeField] public bool isFillAll = false;
        private bool isEraseAll = false;
        private bool isErasingAll = false;
        public bool isAddingTextures = true;
        public bool isErasingPuddle = false;
 
        // Brush 
        private float brushSize = 1f;
        private string vfxPropertyName = "brushSize";
        private float vfxPropertyStep = 1f;
        private float minVfxPropertyValue = 0.01f;
        private float maxVfxPropertyValue = 1000f;

        // Miscellaneous
        private bool isProceduralTerrainPuddleMask = false;
        private Vector3 hitLocation;
        private Vector2 mousePosition;
        private Quaternion discRotation = Quaternion.identity;
        private const string globalErasePuddleString = "_Global_Erase_Puddle";

        // File Management
        private string fileName = null;
        string filePath;
        string directoryPath;
        string puddlewetnessmask = "puddle_wetness_mask.png";
        string proceduralpuddlemask = "procedural_puddle_mask.png";

        private void OnEnable()
        {
            script = (DynamicPuddlePainter)target;
            if (script == null)
                return;

            brushSize = script.brushSize;
            vfxComponent = script.paintBrush.GetComponent<VisualEffect>();
            directoryPath = script.folderPath;
            //Debug.Log("Directory Path: " + script.folderPath);

            if (script.puddlePaintingRenderTexture != null)
            {
                RenderTexture renderTexture = script.puddlePaintingRenderTexture;
            }

            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        public override void OnInspectorGUI()
        {
            if (script == null)
            {
                EditorGUILayout.HelpBox("Script is not assigned.", MessageType.Error);
                return;
            }

            base.OnInspectorGUI();
            EditorGUILayout.HelpBox("Hold Left Mouse to Paint", MessageType.Info);
            EditorGUILayout.HelpBox("Hold ALT + Left Mouse to Erase", MessageType.Info);
            EditorGUILayout.HelpBox("Press [ or ] to Change Brush Size", MessageType.Info);
            UpdateBrushSize(brushSize);
            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUI.enabled = !isEnabled;
            if (GUILayout.Button("Start"))
            {
                isEnabled = true;
            }
            GUI.enabled = isEnabled;
            if (GUILayout.Button("Stop"))
            {
                isEnabled = false;
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;


            GUILayout.Space(10);


            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill"))
            {
                FillAll(Color.white);
            }
            if (GUILayout.Button("Erase"))
            {
                EraseAll();
            }
            GUILayout.EndHorizontal();
        }

        private void UpdateBrushSize(float brushSize)
        {
            if (vfxComponent != null)
            {
                vfxComponent.SetFloat(vfxPropertyName, script.brushSize);
            }
            SceneView.RepaintAll();
            Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (script == null || script.paintBrush == null || !isEnabled)
            {
                return;
            }

            Event currentEvent = Event.current;
            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    if (currentEvent.control && currentEvent.button == 0 && isEnabled)
                    {
                        // Ctrl + Left mouse button down
                        Shader.SetGlobalInt(globalErasePuddleString, 1);
                        isAddingTextures = false; 
                        isErasingPuddle = true;
                        mousePosition = currentEvent.mousePosition;
                        script.paintBrush.SetActive(true);
                    }
                    else if (!currentEvent.alt && currentEvent.button == 0 && isEnabled)
                    {
                        // Normal left mouse button down (without Ctrl)
                        Shader.SetGlobalInt(globalErasePuddleString, 0);
                        isAddingTextures = true;
                        isErasingPuddle = false;
                        mousePosition = currentEvent.mousePosition;
                        script.paintBrush.SetActive(true);
                    }
                    break;

                case EventType.MouseDrag:
                    if (isEnabled)
                    {
                        mousePosition = currentEvent.mousePosition;
                    }
                    break;

                case EventType.MouseUp:
                    if (isEnabled && (currentEvent.button == 0 || (currentEvent.control && currentEvent.button == 0)))
                    {
                        // Only save when left mouse button or Ctrl + left mouse button is released
                        Shader.SetGlobalInt(globalErasePuddleString, 0);
                        script.paintBrush.SetActive(false);
                        SaveRenderTexture(); 
                    }
                    break;

                case EventType.KeyDown:
                    if (isEnabled)
                    {
                        if (currentEvent.keyCode == KeyCode.LeftBracket)
                        {
                            AdjustBrushSize(-vfxPropertyStep);
                            currentEvent.Use();
                        }
                        else if (currentEvent.keyCode == KeyCode.RightBracket)
                        {
                            AdjustBrushSize(vfxPropertyStep);
                            currentEvent.Use();
                        }
                        else if (currentEvent.keyCode == KeyCode.LeftControl || currentEvent.keyCode == KeyCode.RightControl)
                        {
                            isErasingPuddle = true;
                        }
                    }
                    break;

                case EventType.KeyUp:
                    if (isEnabled && (currentEvent.keyCode == KeyCode.LeftControl || currentEvent.keyCode == KeyCode.RightControl))
                    {
                        isErasingPuddle = false;
                    }
                    break;
            }

            if (isEnabled)
            {
                DrawDisc();
            }

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        private void AdjustBrushSize(float delta)
        {
            float currentValue = vfxComponent.GetFloat(vfxPropertyName);
            float newValue = Mathf.Clamp(currentValue + delta, minVfxPropertyValue, maxVfxPropertyValue);
            brushSize = newValue;
            script.brushSize = newValue;
            vfxComponent.SetFloat(vfxPropertyName, newValue);
            Repaint();
        }

        private void DrawDisc()
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, script.TerrainLayer))
            {
                hitLocation = hit.point;
                script.paintBrush.transform.position = hit.point;
                discRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                Handles.color = isErasingPuddle ? Color.red : Color.green;
                Handles.matrix = Matrix4x4.TRS(hitLocation, discRotation, Vector3.one);
                float newBrushSize = script.brushSize / 3.2f;
                Handles.DrawWireDisc(Vector3.zero, Vector3.up, newBrushSize);
                Handles.matrix = Matrix4x4.identity;
            }
        }

        private Texture2D TextureFromRenderTexture(RenderTexture renderTexture)
        {
            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            return texture;
        }

        private void FillAll(Color color)
        {
            if (script == null || script.puddlePaintingRenderTexture == null)
            {
                #if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.LogWarning("Puddle Render Texture is not assigned.");
                #endif
                return;
            }

            RenderTexture renderTexture = script.puddlePaintingRenderTexture;
            Texture2D newTexture = TextureFromRenderTexture(renderTexture);

            Color[] pixels = newTexture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                switch (script.paintChannel)
                {
                    case DynamicPuddlePainter.PaintChannel.ProceduralPuddleMask:
                        pixel.r = 1;
                        break;
                    case DynamicPuddlePainter.PaintChannel.Wetness:
                        pixel.g = 1;
                        break;
                    case DynamicPuddlePainter.PaintChannel.Puddles:
                        pixel.b = color.b;
                        break;
                    case DynamicPuddlePainter.PaintChannel.ProceduralTerrainPuddleMask:
                        pixel.b = 1f;
                        break;
                    case DynamicPuddlePainter.PaintChannel.AllLayers:
                        pixel.r = color.r;
                        pixel.g = 1f;
                        pixel.b = color.b;
                        break;
                    case DynamicPuddlePainter.PaintChannel.AllLayersMinusTerrain:
                        pixel.r = color.r;
                        pixel.g = 1f;
                        pixel.b = color.b;
                        break;
                }
                pixels[i] = pixel;
            }

            newTexture.SetPixels(pixels);
            newTexture.Apply();
            SaveTexture(newTexture);
            DestroyImmediate(newTexture);

            AssetDatabase.Refresh();
        }
        private void EraseAll()
        {
            isEraseAll = true;

            if (script == null || script.puddlePaintingRenderTexture == null)
            {
                #if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.LogWarning("Puddle Render Texture is not assigned.");
                #endif
                return;
            }

            RenderTexture renderTexture = script.puddlePaintingRenderTexture;
            Texture2D newTexture = TextureFromRenderTexture(renderTexture);

            Color[] pixels = newTexture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                switch (script.paintChannel)
                {
                    case DynamicPuddlePainter.PaintChannel.ProceduralPuddleMask:
                        pixel.r = 0f; 
                        break;
                    case DynamicPuddlePainter.PaintChannel.Wetness:
                        pixel.g = 0f; 
                        break;
                    case DynamicPuddlePainter.PaintChannel.Puddles:
                        pixel.b = 0f; 
                        break;
                    case DynamicPuddlePainter.PaintChannel.ProceduralTerrainPuddleMask:
                        pixel.b = 0f; 
                        break;
                    case DynamicPuddlePainter.PaintChannel.AllLayers:
                        pixel.r = 0f; 
                        pixel.g = 0f;
                        pixel.b = 0f; 
                        break;
                    case DynamicPuddlePainter.PaintChannel.AllLayersMinusTerrain:
                        pixel.r = 0f; 
                        pixel.g = 0f;
                        pixel.b = 0f; 
                        break;
                }
                pixels[i] = pixel;
            }

            newTexture.SetPixels(pixels);
            newTexture.Apply();
            isErasingAll = true;
            SaveTexture(newTexture);
            isErasingAll = false;
            DestroyImmediate(newTexture);
            
            AssetDatabase.Refresh();

            isEraseAll = false;
        }

        private void SaveTexture(Texture2D texture)
        {
            Texture2D textureCopy = new Texture2D(texture.width, texture.height, texture.format, texture.mipmapCount > 1);
             Graphics.CopyTexture(texture, textureCopy);

            switch (script.paintChannel)
            {
                case DynamicPuddlePainter.PaintChannel.ProceduralPuddleMask:
                case DynamicPuddlePainter.PaintChannel.Wetness:
                case DynamicPuddlePainter.PaintChannel.Puddles:    
                case DynamicPuddlePainter.PaintChannel.AllLayersMinusTerrain:
                    fileName = puddlewetnessmask;
                    SaveTextureFile(fileName, texture);
                    break;
                case DynamicPuddlePainter.PaintChannel.ProceduralTerrainPuddleMask:
                    fileName = proceduralpuddlemask;
                    SaveTextureFile(fileName, texture);
                    break;
                case DynamicPuddlePainter.PaintChannel.AllLayers:
                    fileName = puddlewetnessmask; 
                    SaveTextureFile(fileName, texture);

                    fileName = proceduralpuddlemask; 
                    isProceduralTerrainPuddleMask = true;
                    SaveTextureFile(fileName, texture);
                    isProceduralTerrainPuddleMask = false;
                    break;
            }
        }
        private void SaveTextureFile(string filename, Texture2D texture)
        {
            filePath = Path.Combine(directoryPath, fileName);
            if (File.Exists(filePath))
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                Texture2D existingTexture = new Texture2D(2, 2);
                existingTexture.LoadImage(bytes);

                Color[] existingPixels = existingTexture.GetPixels();
                Color[] newPixels = texture.GetPixels();

                for (int i = 0; i < existingPixels.Length; i++)
                { 
                    Color newColor = newPixels[i];
                    
                    switch (script.paintChannel)
                    {
                        case DynamicPuddlePainter.PaintChannel.ProceduralPuddleMask:
                            existingPixels[i].r = newPixels[i].r;
                            break;

                        case DynamicPuddlePainter.PaintChannel.Wetness:
                            existingPixels[i].g = newPixels[i].g;
                            break;

                        case DynamicPuddlePainter.PaintChannel.Puddles:
                            existingPixels[i].b = newPixels[i].b;
                            break;

                        case DynamicPuddlePainter.PaintChannel.ProceduralTerrainPuddleMask:
                            existingPixels[i].b = Mathf.Clamp01(newColor.b);
                            break;

                        case DynamicPuddlePainter.PaintChannel.AllLayers:
                            
                            if(!isProceduralTerrainPuddleMask)
                            {
                                existingPixels[i].r = newPixels[i].r;
                                existingPixels[i].g = newPixels[i].g;
                                existingPixels[i].b = newPixels[i].b; 
                                existingPixels[i].a = newPixels[i].a;
                            }
                            else
                            {
                                //existingPixels[i].b = isEraseAll ? 1 - Mathf.Clamp01(newColor.b) : Mathf.Clamp01(newColor.b);     
                                existingPixels[i].b = isEraseAll ? 0 : Mathf.Clamp01(newColor.b);                                         
                            }
                            break;
                            
                        case DynamicPuddlePainter.PaintChannel.AllLayersMinusTerrain:
                            existingPixels[i].r = newPixels[i].r;
                            existingPixels[i].g = newPixels[i].g;
                            existingPixels[i].b = newPixels[i].b;
                            existingPixels[i].a = newPixels[i].a;
                            break;
                    }
                }

                existingTexture.SetPixels(existingPixels);
                existingTexture.Apply();

                byte[] combinedBytes = existingTexture.EncodeToPNG();
                File.WriteAllBytes(filePath, combinedBytes);

                DestroyImmediate(existingTexture);
                // #if DEVELOPMENT_BUILD || UNITY_EDITOR
                // Debug.Log("Texture saved to " + filePath);
                // #endif
            }
            else
            {
                byte[] bytes = texture.EncodeToPNG();
                File.WriteAllBytes(filePath, bytes);
                // #if DEVELOPMENT_BUILD || UNITY_EDITOR
                // Debug.Log("Texture saved to " + filePath);
                // #endif
            }
        }
             
        private void SaveRenderTexture()
        {
            if (script == null || script.puddlePaintingRenderTexture == null)
            {
                #if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.LogWarning("Puddle Render Texture is not assigned.");
                #endif
                return;
            }

            switch (script.paintChannel)
            {
                case DynamicPuddlePainter.PaintChannel.ProceduralPuddleMask:
                case DynamicPuddlePainter.PaintChannel.Wetness:
                case DynamicPuddlePainter.PaintChannel.Puddles:
                case DynamicPuddlePainter.PaintChannel.AllLayersMinusTerrain:
                    fileName = puddlewetnessmask;
                    SaveRenderTextureFile(fileName);
                    break;  
                case DynamicPuddlePainter.PaintChannel.AllLayers:
                    fileName = puddlewetnessmask;
                    SaveRenderTextureFile(fileName);
                    fileName = proceduralpuddlemask;
                    SaveRenderTextureFile(fileName);
                    break;
                case DynamicPuddlePainter.PaintChannel.ProceduralTerrainPuddleMask:
                    fileName = proceduralpuddlemask;
                    SaveRenderTextureFile(fileName);
                    break;
            }
        }
        private void SaveRenderTextureFile(string fileName)
        {
            RenderTexture renderTexture = script.puddlePaintingRenderTexture;
            Texture2D newTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);

            RenderTexture.active = renderTexture;
            newTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            newTexture.Apply();
            RenderTexture.active = null;

            if (newTexture == null)
            {
                #if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.LogWarning("New texture is null, cannot save.");
                #endif
                return;
            }

            filePath = Path.Combine(directoryPath, fileName);
            if (File.Exists(filePath))
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                Texture2D existingTexture = new Texture2D(2, 2);
                existingTexture.LoadImage(bytes);

                Color[] existingPixels = existingTexture.GetPixels();
                Color[] newPixels = newTexture.GetPixels();

                for (int i = 0; i < existingPixels.Length; i++)
                {
                    Color existingColor = existingPixels[i];
                    Color newColor = newPixels[i];

                    float combinedChannelValue = 0;
                    switch (script.paintChannel)
                    {
                        case DynamicPuddlePainter.PaintChannel.ProceduralPuddleMask:
                            combinedChannelValue = isAddingTextures ? (existingColor.r + newColor.r) : (existingColor.r - newColor.r);
                            existingPixels[i].r = Mathf.Clamp01(combinedChannelValue);
                            break;

                        case DynamicPuddlePainter.PaintChannel.Wetness:
                            combinedChannelValue = isAddingTextures ? (existingColor.g + newColor.g) : (existingColor.g - newColor.g);
                            existingPixels[i].g = Mathf.Clamp01(combinedChannelValue);
                            break;

                        case DynamicPuddlePainter.PaintChannel.Puddles:
                            combinedChannelValue = isAddingTextures ? (existingColor.b + newColor.b) : (existingColor.b - newColor.b);
                            existingPixels[i].b = Mathf.Clamp01(combinedChannelValue);
                            break;

                        case DynamicPuddlePainter.PaintChannel.ProceduralTerrainPuddleMask:
                            if(isErasingAll)
                            {
                                combinedChannelValue = isAddingTextures ? (existingColor.b + newColor.b) : (existingColor.b - newColor.b);
                                existingPixels[i] = new Color(existingColor.r, existingColor.g, Mathf.Clamp01(combinedChannelValue), existingColor.a);
                            }
                            else
                            {
                                combinedChannelValue = isAddingTextures ? (existingColor.b - newColor.b) : (existingColor.b + newColor.b);
                                existingPixels[i] = new Color(existingColor.r, existingColor.g, Mathf.Clamp01(combinedChannelValue), existingColor.a);
                            }
                            break;
                            
                        case DynamicPuddlePainter.PaintChannel.AllLayers:
                            existingPixels[i].r = isAddingTextures ? (existingColor.r + newColor.r) : (existingColor.r - newColor.r);
                            existingPixels[i].g = isAddingTextures ? (existingColor.g + newColor.g) : (existingColor.g - newColor.g);
                            existingPixels[i].b = isAddingTextures ? (existingColor.b + newColor.b) : (existingColor.b - newColor.b);
                            existingPixels[i].a = isAddingTextures ? (existingColor.a + newColor.a) : (existingColor.a - newColor.a);
                            break;

                        case DynamicPuddlePainter.PaintChannel.AllLayersMinusTerrain:
                            existingPixels[i].r = isAddingTextures ? (existingColor.r + newColor.r) : (existingColor.r - newColor.r);
                            existingPixels[i].g = isAddingTextures ? (existingColor.g + newColor.g) : (existingColor.g - newColor.g);
                            existingPixels[i].b = isAddingTextures ? (existingColor.b + newColor.b) : (existingColor.b - newColor.b);
                            break;
                    }
                }

                existingTexture.SetPixels(existingPixels);
                existingTexture.Apply();

                byte[] combinedBytes = existingTexture.EncodeToPNG();
                File.WriteAllBytes(filePath, combinedBytes);

                DestroyImmediate(existingTexture);
            }
            else
            {
                byte[] bytes = newTexture.EncodeToPNG();
                File.WriteAllBytes(filePath, bytes);
            }

            DestroyImmediate(newTexture);
            AssetDatabase.Refresh();
        }
    }
    #endif
}