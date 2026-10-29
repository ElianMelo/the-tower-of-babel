//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.0.0
//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections.Generic;

namespace MoveRotateResize.PlayModeOptimizations
{
    public class PlayModeOptimizations : MonoBehaviour
    {
        [SerializeField]private bool destroyGameObjectOnAwake = true;
        [SerializeField]private bool autoRemoveComponents = false;

        void Awake()
        {
            if (autoRemoveComponents)
            {
                Component[] allComponents = GetComponents<Component>();

                List<Component> componentsToRemove = new List<Component>();

                foreach (var component in allComponents)
                {
                    if (component is Transform)
                        continue;

                    componentsToRemove.Add(component);
                }

                foreach (var component in componentsToRemove)
                {
                    if (component != null)
                    {
                        DestroyImmediate(component);
                    }
                }
            }

            if (destroyGameObjectOnAwake)
            {
                Destroy(gameObject);
            }
        }
    }
}