//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;
using MarkupAttributes.Editor;

namespace MoveRotateResize.Dynamic.SystemsManager
{
    [CustomEditor(typeof(DynamicSystemsManager))]
    public class DynamicGameManagerEditor : MarkedUpEditor
    {
        DynamicSystemsManager dynamicGameManager;

        void CacheGlobalVariables()
        {
            dynamicGameManager = DynamicSystemsManager.Instance;
        }

        public override void OnInspectorGUI()
        {
            DrawMarkedUpInspector();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Set Debugs All True"))
            {
                var dynamicGameManager = DynamicSystemsManager.Instance;
                if (dynamicGameManager != null)
                {
                    dynamicGameManager.SetDebugForAllScriptsTrue();
                }
                else
                {
                    Debug.LogError("DynamicSystemsManager.Instance is null. Ensure it is properly initialized.");
                }
            }

            if (GUILayout.Button("Set Debugs All False"))
            {
                var dynamicGameManager = DynamicSystemsManager.Instance;
                if (dynamicGameManager != null)
                {
                    dynamicGameManager.SetDebugForAllScriptsFalse();
                }
                else
                {
                    Debug.LogError("DynamicSystemsManager.Instance is null. Ensure it is properly initialized.");
                }
            }

            GUILayout.EndHorizontal();
        }

        // protected override void OnInitialize()
        // {
        //     AddCallback(serializedObject.FindProperty("doNotDestroyOnLoad"), 
        //         CallbackEvent.AfterProperty, Button1);

        //     AddCallback(serializedObject.FindProperty("doNotDestroyOnLoad"),
        //         CallbackEvent.BeforeProperty, Button2);
        // }

        // private void Button1(SerializedProperty property)
        // {
        //     if (GUILayout.Button("Set Debugs All True"))
        //     {
        //         dynamicGameManager.SetDebugForAllScriptsTrue();
        //     }
        // }

        // private void Button2(SerializedProperty property)
        // {
        //     if (GUILayout.Button("Set Debugs All False"))
        //     {
        //         dynamicGameManager.SetDebugForAllScriptsFalse();
        //     }
        //}
    }
}