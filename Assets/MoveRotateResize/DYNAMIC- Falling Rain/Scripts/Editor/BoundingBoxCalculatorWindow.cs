
//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEditor;

namespace MoveRotateResize.Dynamic.BoundingBoxCalculatorWindow
{
    public class BoundingBoxCalculatorWindow : EditorWindow
    {
        private Vector3 localCenter;
        private Vector3 localSize;

        private GameObject selectedObject;

        private bool isBoundingBoxUpdated = false;

        [MenuItem("Tools/MoveRotateResize/DYNAMIC: Tools/Bounding Box Calculator")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<BoundingBoxCalculatorWindow>("Bounding Box Calculator");
        }

        void OnGUI()
        {
            GUILayout.Label("Bounding Box Calculator", EditorStyles.boldLabel);

            if (GUILayout.Button("Calculate Bounding Box"))
            {
                CalculateBoundingBox();
            }

            if (isBoundingBoxUpdated)
            {
                EditorGUILayout.Space();
                GUILayout.Label("Bounding Box (Local Space):", EditorStyles.boldLabel);
                GUILayout.Label("Center: " + localCenter);
                GUILayout.Label("Size: " + localSize);
            }
        }

        private void CalculateBoundingBox()
        {
            selectedObject = Selection.activeGameObject;

            if (selectedObject != null)
            {
                MeshRenderer meshRenderer = selectedObject.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                    if (meshFilter != null)
                    {
                        Bounds meshBounds = meshFilter.sharedMesh.bounds;

                        localSize = Vector3.Scale(meshBounds.size, selectedObject.transform.localScale);
                        localCenter = meshBounds.center;
                        
                        isBoundingBoxUpdated = true;

                        Repaint();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Error", "MeshFilter component not found on the object.", "OK");
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "MeshRenderer component not found on the object.", "OK");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "No GameObject selected.", "OK");
            }
        }
    }
}