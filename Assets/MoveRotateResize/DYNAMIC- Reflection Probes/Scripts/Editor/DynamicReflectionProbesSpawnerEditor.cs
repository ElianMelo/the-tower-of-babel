//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;
using MarkupAttributes.Editor;
using MoveRotateResize.Dynamic.SystemsManager;

namespace MoveRotateResize.Dynamic.ReflectionProbes
{
    [CustomEditor(typeof(DynamicReflectionProbesSpawner))]
    public class DynamicReflectionProbesSpawnerEditor : MarkedUpEditor
    {
        DynamicReflectionProbesSpawner dynamicReflectionProbesSpawner;

        public override void OnInspectorGUI()
        {
            DrawMarkedUpInspector();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Spawn Reflection Probes"))
            {
                var dynamicReflectionProbesSpawner = DynamicSystemsManager.dynamicReflectionProbesSpawner;
                if (dynamicReflectionProbesSpawner != null)
                {
                    dynamicReflectionProbesSpawner.SpawnReflectionProbes();
                }
                else
                {
                    Debug.LogError("DynamicSystemsManager.Instance is null. Ensure it is properly initialized.");
                }
            }

            if (GUILayout.Button("Remove Reflection Probes"))
            {
                var dynamicReflectionProbesSpawner = DynamicSystemsManager.dynamicReflectionProbesSpawner;
                if (dynamicReflectionProbesSpawner != null)
                {
                    dynamicReflectionProbesSpawner.RemoveReflectionProbes();
                }
                else
                {
                    Debug.LogError("DynamicSystemsManager.Instance is null. Ensure it is properly initialized.");
                }
            }

            GUILayout.EndHorizontal();
        }
    }
}