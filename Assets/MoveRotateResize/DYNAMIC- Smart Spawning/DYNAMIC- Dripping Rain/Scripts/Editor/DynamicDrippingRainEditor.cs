//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEditor;
using MarkupAttributes.Editor;
using MoveRotateResize.Dynamic.SystemsManager;
using UnityEngine;

namespace MoveRotateResize.Dynamic.DrippingRain
{
    [CustomEditor(typeof(DynamicDrippingRain))]
    public class DynamicDrippingRainEditor : MarkedUpEditor
    {
        DynamicDrippingRain dynamicDrippingRain;

        void CacheGlobalVariables()
        {
            dynamicDrippingRain = DynamicSystemsManager.dynamicDrippingRain;
        }

        public override void OnInspectorGUI()
        {
            CacheGlobalVariables();
            DrawMarkedUpInspector();


            GUILayout.Space(10);


            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Edit Mode Spawns"))
            {
                dynamicDrippingRain.CreateDrippingRainEditModeSpawns();
            }
            if (GUILayout.Button("Destory Edit Mode Spawns"))
            {
                dynamicDrippingRain.DestroyDrippingRainSpawns();
            }
            GUILayout.EndHorizontal();


            GUILayout.Space(10);


            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Enable Edit Mode Spawns"))
            {
                dynamicDrippingRain.EnableEditMode();
            }
            if (GUILayout.Button("Disable Edit Mode Spawns"))
            {
                dynamicDrippingRain.DisableEditMode();
            }
            GUILayout.EndHorizontal();
        }
    }
}