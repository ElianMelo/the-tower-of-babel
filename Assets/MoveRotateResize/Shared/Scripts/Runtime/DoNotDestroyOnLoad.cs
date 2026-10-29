//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.1
//------------------------------------------------------------------------------
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MoveRotateResize.General
{
    [DisallowMultipleComponent]
    public class DoNotDestroyOnLoad : MonoBehaviour
    {
        private static DoNotDestroyOnLoad instance;
        [SerializeField]private bool doNotDestroyOnLoad = false; 

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                if (Application.isPlaying && doNotDestroyOnLoad)
                {
#if UNITY_EDITOR
                    if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                    {
                        Debug.LogWarning("Dynamic.GameManager instance cannot be destroyed during prefab instantiation.");
                        return;
                    }
#endif

                    DontDestroyOnLoad(transform.root.gameObject);
                }
            }
            else
            {
                DestroyImmediate(gameObject);
                return;
            }
        }
    }
}
