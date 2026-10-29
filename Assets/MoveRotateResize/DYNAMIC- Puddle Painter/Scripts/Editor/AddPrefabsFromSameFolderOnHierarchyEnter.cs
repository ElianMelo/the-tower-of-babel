//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.1
//------------------------------------------------------------------------------
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Linq;
using System.IO;

namespace MoveRotateResize.Dynamic.PuddlePainter
{
    [ExecuteInEditMode]
    //[DefaultExecutionOrder(-1000000)]
    public class AddPrefabsFromSameFolderOnHierarchyEnter : MonoBehaviour
    {
#if UNITY_EDITOR

        [SerializeField]
        private bool hasExecuted = false;

        private void OnEnable()
        {
            if (!Application.isPlaying && gameObject.scene.isLoaded)
            {
                if (!hasExecuted)
                {
                    AddPrefabsFromSameFolder();
                    hasExecuted = true;
                }
            }
        }

        public void AddPrefabsFromSameFolder()
        {
            string prefabPath = GetPrefabPath(gameObject);
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogWarning("Could not find the prefab path for the GameObject.");
                return;
            }

            string folderPath = Path.GetDirectoryName(prefabPath);
            if (string.IsNullOrEmpty(folderPath))
            {
                Debug.LogWarning("Could not determine the folder path for the prefab.");
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            GameObject[] prefabsToAdd = prefabGuids
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(prefab => AssetDatabase.GetAssetPath(prefab) != prefabPath)
                .ToArray();

            if (prefabsToAdd.Length == 0)
            {
                Debug.Log("No prefabs found in the same folder.");
                return;
            }

            foreach (GameObject prefab in prefabsToAdd)
            {
                if (prefab != null)
                {
                    GameObject newObject = PrefabUtility.InstantiatePrefab(prefab, transform) as GameObject;
                    newObject.name = prefab.name;
                }
                else
                {
                    Debug.LogWarning("Loaded prefab is null.");
                }
            }

            AssetDatabase.Refresh();
            RemoveScript();
        }

        private string GetPrefabPath(GameObject obj)
        {
            PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(obj);

            if (prefabAssetType == PrefabAssetType.NotAPrefab)
            {
                Debug.LogWarning("The GameObject is not a prefab or prefab instance.");
                return null;
            }

            GameObject prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(obj);
            if (prefabRoot != null)
            {
                return AssetDatabase.GetAssetPath(prefabRoot);
            }

            Debug.LogWarning("Prefab root is null.");
            return null;
        }

        private void RemoveScript()
        {
            EditorApplication.delayCall += () =>
            {
                Undo.RecordObject(gameObject, "Remove AddPrefabsFromSameFolderOnHierarchyEnter Script");
                DestroyImmediate(this);
            };
        }
    }
#endif
}