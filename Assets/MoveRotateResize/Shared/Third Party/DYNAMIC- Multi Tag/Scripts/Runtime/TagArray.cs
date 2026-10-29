//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.2
//------------------------------------------------------------------------------
using System;
using UnityEngine;
using UnityEditor;

namespace MoveRotateResize.Dynamic.MultiTag
{
    public sealed class TagArray : ScriptableObject
    {
        [SerializeField] private Tag[] _tags = Array.Empty<Tag>();

        //internal IEnumerable<Tag> Tags => _tags;
        internal Tag[] Tags => _tags;


        internal void Add(GameObject instance, int hash)
        {
            for (int i = 0; i < _tags.Length; i++)
                _tags[i].Add(instance, hash);
        }

        internal void Remove(GameObject instance, int hash)
        {
            for (int i = 0; i < _tags.Length; i++)
                _tags[i].Remove(instance, hash);
        }

        internal bool HasInstance(GameObject instance, bool allRequired)
        {
            Debug.Log($"Checking if instance has required tags.");
            return instance.HasTags(_tags, allRequired);
        }

#if UNITY_EDITOR
        [MenuItem("Tools/MoveRotateResize/DYNAMIC- Multi Tag/Tag Array")]
        public static void CreateTagArray()
        {
            string folderPath = "Assets/MoveRotateResize/Shared/Third Party/DYNAMIC- Multi Tag/Tag Arrays"; 
            
            TagArray tagArray = ScriptableObject.CreateInstance<TagArray>();

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/New_TagArray.asset");

            AssetDatabase.CreateAsset(tagArray, assetPath);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = tagArray;
        }
#endif
    }
}