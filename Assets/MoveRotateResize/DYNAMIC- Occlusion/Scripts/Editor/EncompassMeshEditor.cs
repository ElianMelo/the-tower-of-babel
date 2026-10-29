
//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEditor;

namespace MoveRotateResize.Dynamic.EncompassMeshWindow
{
    public class EncompassMeshWindow : EditorWindow
    {
        private GameObject selectedObject;
        private GameObject objectToPlace;
        private float objectSpacing = 1.0f; 
        private float objectScale = 1.0f; 

        //[MenuItem("Tools/Encompass Mesh/Place Objects Window")]
        public static void ShowWindow()
        {
            GetWindow<EncompassMeshWindow>("Encompass Mesh");
        }

        private void OnGUI()
        {
            GUILayout.Label("Encompass Mesh Tool", EditorStyles.boldLabel);

            if (Selection.activeGameObject != selectedObject)
            {
                selectedObject = Selection.activeGameObject;
            }

            if (selectedObject == null)
            {
                EditorGUILayout.HelpBox("No GameObject selected! Please select a GameObject with a MeshFilter.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("Selected GameObject:", selectedObject.name);
                objectToPlace = (GameObject)EditorGUILayout.ObjectField("Object to Place", objectToPlace, typeof(GameObject), false);
                objectSpacing = EditorGUILayout.FloatField("Object Spacing", objectSpacing);
                objectScale = EditorGUILayout.FloatField("Object Scale", objectScale);

                if (GUILayout.Button("Place Objects Around Mesh"))
                {
                    PlaceObjectsAroundMesh();
                }
            }
        }

        private void PlaceObjectsAroundMesh()
        {
            if (selectedObject == null || objectToPlace == null)
            {
                Debug.LogError("Selected GameObject or Object to Place is not assigned!");
                return;
            }

            MeshFilter meshFilter = selectedObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.mesh == null)
            {
                Debug.LogError("Selected GameObject does not have a valid MeshFilter!");
                return;
            }

            ClearPreviousObjects();

            Mesh mesh = meshFilter.mesh;

            Vector3[] vertices = mesh.vertices;

            Transform meshTransform = selectedObject.transform;
            Vector3[] worldVertices = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                worldVertices[i] = meshTransform.TransformPoint(vertices[i]);
            }

            int positionsToPlace = Mathf.FloorToInt(vertices.Length / objectSpacing);

            for (int i = 0; i < positionsToPlace; i++)
            {
                int randomIndex = Random.Range(0, vertices.Length);
                Vector3 position = worldVertices[randomIndex];
                PlaceObjectAtPosition(position);
            }
        }

        private void PlaceObjectAtPosition(Vector3 position)
        {
            if (objectToPlace == null)
            {
                Debug.LogError("Object to place is not set.");
                return;
            }

            GameObject placedObject = Instantiate(objectToPlace, position, Quaternion.identity);
            placedObject.transform.SetParent(selectedObject.transform);

            placedObject.transform.localScale = new Vector3(objectScale, objectScale, objectScale);
        }

        private void ClearPreviousObjects()
        {
            for (int i = selectedObject.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(selectedObject.transform.GetChild(i).gameObject);
            }
        }
    }
}