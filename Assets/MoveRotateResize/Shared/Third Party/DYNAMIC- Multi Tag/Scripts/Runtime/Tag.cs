//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.1
//------------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace MoveRotateResize.Dynamic.MultiTag
{
    public sealed class Tag : ScriptableObject
    {
        private readonly HashSet<int> _instancesHash = new HashSet<int>();
        private readonly List<GameObject> _instances = new List<GameObject>(128);

        internal void Add(GameObject instance, int hash)
        {
            if (_instancesHash.Contains(hash))
                return;
            
            _instances.Add(instance);
            _instancesHash.Add(hash);
        }

        internal void Remove(GameObject instance, int hash)
        {
            if (!_instancesHash.Contains(hash))
                return;
            
            _instances.Remove(instance);
            _instancesHash.Remove(hash);
        }

        internal bool HasInstance(int hash)
        {
            return _instancesHash.Contains(hash);
        }

        public IEnumerable<GameObject> GetInstances()
        {
            int instancesCount = _instances.Count - 1;
            for (int i = instancesCount; i >= 0; i--)
            {
                var instance = _instances[i];
                
                if (instance == null)
                {
                    _instances.RemoveAt(i);
                    continue;
                }

                yield return instance;
            }
        }

#if UNITY_EDITOR
        [MenuItem("Tools/MoveRotateResize/DYNAMIC- Multi Tag/Tag", priority = 1)]
        public static void CreateTag()
        {
            // Define the desired folder path.
            string folderPath = "Assets/MoveRotateResize/Shared/Third Party/DYNAMIC- Multi Tag/Tags";

            // Check if the folder exists; if not, create it.
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string parentFolderPath = Path.GetDirectoryName(folderPath);
                if (!AssetDatabase.IsValidFolder(parentFolderPath))
                {
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(parentFolderPath), Path.GetFileName(parentFolderPath));
                }
                AssetDatabase.CreateFolder(parentFolderPath, Path.GetFileName(folderPath));
            }

            // Create an instance of Tag.
            Tag tag = ScriptableObject.CreateInstance<Tag>();

            // Define the asset path and name.
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/New_Tag.asset");

            // Create and save the asset.
            AssetDatabase.CreateAsset(tag, assetPath);
            AssetDatabase.SaveAssets();

            // Focus the created asset in the Project window.
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = tag;
        }
#endif
    }
}