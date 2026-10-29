//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using UnityEditor;

namespace MoveRotateResize.EditorInterface
{
    public static class MoveRotateResizeInputManagerSetup
    {
        public static void AddDPadAndRightStickAxes()
        {
            SerializedObject inputManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);
            SerializedProperty axes = inputManager.FindProperty("m_Axes");

            if (!AxisExists(axes, "DPadX"))
            {
                AddAxis(axes, "DPadX", 6);
            }
            if (!AxisExists(axes, "DPadY"))
            {
                AddAxis(axes, "DPadY", 7);
            }
            if (!AxisExists(axes, "Right Stick X"))
            {
                AddAxis(axes, "Right Stick X", 4);
            }
            if (!AxisExists(axes, "Right Stick Y"))
            {
                AddAxis(axes, "Right Stick Y", 5);
            }

            inputManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        static bool AxisExists(SerializedProperty axes, string name)
        {
            for (int i = 0; i < axes.arraySize; i++)
            {
                SerializedProperty axis = axes.GetArrayElementAtIndex(i);
                if (axis.FindPropertyRelative("m_Name").stringValue == name)
                    return true;
            }
            return false;
        }

        static void AddAxis(SerializedProperty axes, string name, int joystickAxis)
        {
            axes.arraySize++;
            SerializedProperty axis = axes.GetArrayElementAtIndex(axes.arraySize - 1);

            axis.FindPropertyRelative("m_Name").stringValue = name;
            axis.FindPropertyRelative("descriptiveName").stringValue = "";
            axis.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
            axis.FindPropertyRelative("negativeButton").stringValue = "";
            axis.FindPropertyRelative("positiveButton").stringValue = "";
            axis.FindPropertyRelative("altNegativeButton").stringValue = "";
            axis.FindPropertyRelative("altPositiveButton").stringValue = "";
            axis.FindPropertyRelative("gravity").floatValue = 0;
            axis.FindPropertyRelative("dead").floatValue = 0.1f;
            axis.FindPropertyRelative("sensitivity").floatValue = 1;
            axis.FindPropertyRelative("snap").boolValue = false;
            axis.FindPropertyRelative("invert").boolValue = false;
            axis.FindPropertyRelative("type").intValue = 2;
            axis.FindPropertyRelative("axis").intValue = joystickAxis - 1;
            axis.FindPropertyRelative("joyNum").intValue = 0;
        }
    }
}