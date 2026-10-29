//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;
using MarkupAttributes.Editor;
using MoveRotateResize.Dynamic.SystemsManager;

namespace MoveRotateResize.Dynamic.Occlusion
{
    [CustomEditor(typeof(DynamicOcclusion))]
    public class DynamicOcclusionEditor : MarkedUpEditor
    {
        DynamicOcclusion dynamicOcclusion;

        void CacheGlobalVariables()
        {
            dynamicOcclusion = DynamicSystemsManager.dynamicOcclusion;
        }

        public override void OnInspectorGUI()
        {
            CacheGlobalVariables();
            DrawMarkedUpInspector();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Enter Edit Mode Occlusion"))
            {
                dynamicOcclusion.EnterEditModeOcclusion();
            }

            if (GUILayout.Button("Exit Edit Mode Occlusion"))
            {
                dynamicOcclusion.ExitEditModeOcclusion();
            }
            GUILayout.EndHorizontal();
        }
    }
}