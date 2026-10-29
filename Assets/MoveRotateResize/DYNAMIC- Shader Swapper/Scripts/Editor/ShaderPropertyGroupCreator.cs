using UnityEngine;
using UnityEditor;
using System.IO;

namespace MoveRotateResize.Dynamic.ShaderSwapper
{
    public static class ShaderPropertyGroupCreator
    {
        [MenuItem("Tools/MoveRotateResize/DYNAMIC- Shader Swapper/Create Property Group", priority = 1)]
        public static void CreateShaderPropertyGroup()
        {
            ShaderPropertyGroup asset = ScriptableObject.CreateInstance<ShaderPropertyGroup>();

            string directory = "Assets/MoveRotateResize/DYNAMIC- Shader Swapper/Property Groups/";
            string baseFileName = "PropertyGroup_";
            string fileExtension = ".asset";

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            int count = 1;
            string fileName;
            do
            {
                fileName = baseFileName + count + fileExtension;
                count++;
            } while (File.Exists(Path.Combine(directory, fileName)));

            string path = Path.Combine(directory, fileName);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;

            EditorGUIUtility.PingObject(asset);
        }
    }
}