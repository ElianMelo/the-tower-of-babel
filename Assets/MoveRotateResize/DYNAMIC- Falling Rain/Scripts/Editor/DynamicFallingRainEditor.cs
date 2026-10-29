//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.1
//------------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using MarkupAttributes.Editor;

namespace MoveRotateResize.Dynamic.FallingRain
{
    [CustomEditor(typeof(DynamicFallingRain))]
    public class DynamicFallingRainEditor : MarkedUpEditor
    {
        List<string> _childNames = new List<string>();
        DynamicFallingRain dynamicFallingRain;
        Transform _lastCheckedTransform;

        public override void OnInspectorGUI()
        {
            DrawMarkedUpInspector();

            GUILayout.Space(10);

            dynamicFallingRain = (DynamicFallingRain)target;
            if (dynamicFallingRain.fallingRainVFXS != null)
            {
                if (dynamicFallingRain.fallingRainVFXS.transform != _lastCheckedTransform)
                {
                    UpdateChildNames();
                    _lastCheckedTransform = dynamicFallingRain.fallingRainVFXS.transform;
                }

                if (!_childNames.Contains(dynamicFallingRain.selectedEffectName) && _childNames.Count > 0)
                {
                    dynamicFallingRain.selectedEffectName = _childNames[0];
                }

                EditorGUILayout.HelpBox("If using a dropdown that includes SDF please assign 'Your Created SDF Occlusion' in the Dynamic Occlusion script.", MessageType.Warning);
                int selectedIndex = _childNames.IndexOf(dynamicFallingRain.selectedEffectName);
                selectedIndex = EditorGUILayout.Popup("Select Falling Rain Type", selectedIndex, _childNames.ToArray());

                string newSelectedEffect = _childNames[selectedIndex];
                if (newSelectedEffect != dynamicFallingRain.selectedEffectName)
                {
                    dynamicFallingRain.selectedEffectName = newSelectedEffect;
                    
                    if (newSelectedEffect == "None")
                    {
                        dynamicFallingRain.DisableAllFallingRainObjects();
                    }
                    else
                    {
                        dynamicFallingRain.ApplyFallingRainVFX(newSelectedEffect);

                        // Toggle booleans based on the selected effect name
                        dynamicFallingRain.UpdateEffectBooleans(newSelectedEffect);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("fallingRainVFXs is not assigned or has no children.", MessageType.Warning);
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(dynamicFallingRain);
            }
        }

        private void UpdateChildNames()
        {
            _childNames.Clear();
            foreach (Transform child in dynamicFallingRain.fallingRainVFXS.transform)
            {
                _childNames.Add(child.name);
            }
            _childNames.Add("None");
        }
    }
}
