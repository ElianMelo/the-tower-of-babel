//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.2
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEditor;
using System.IO;

namespace MoveRotateResize.Dynamic.MeshMirror
{
    public class DynamicMeshMirror : EditorWindow
    {
        private bool mirrorX = true;
        private bool mirrorY = false;
        private bool mirrorZ = false;
        private GameObject previewObject;
        private bool previewActive = false;
        private GameObject originalObject;

        [MenuItem("Tools/MoveRotateResize/DYNAMIC- Mesh Mirror/Mesh Mirror")]
        public static void ShowWindow()
        {
            GetWindow<DynamicMeshMirror>("Mesh Mirror");
        }

        void OnEnable()
        {
            EditorApplication.update += UpdateEditor;
        }

        void OnDisable()
        {
            EditorApplication.update -= UpdateEditor;
            CleanupPreviewObject();
        }

        void OnGUI()
        {
            GUIStyle largeBoldStyle = GetLargeBoldStyle();
            GUILayout.Label("Mirror Settings", largeBoldStyle);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox("Select a GameObject with a MeshFilter and MeshRenderer to mirror its mesh.", MessageType.Info);
            GUILayout.Space(10);

            bool newMirrorX = EditorGUILayout.Toggle("Mirror X", mirrorX);
            bool newMirrorY = EditorGUILayout.Toggle("Mirror Y", mirrorY);
            bool newMirrorZ = EditorGUILayout.Toggle("Mirror Z", mirrorZ);

            if (newMirrorX != mirrorX || newMirrorY != mirrorY || newMirrorZ != mirrorZ)
            {
                mirrorX = newMirrorX;
                mirrorY = newMirrorY;
                mirrorZ = newMirrorZ;

                UpdatePreview();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Confirm Mirror"))
            {
                ConfirmMirror();
            }
        }

        GUIStyle GetLargeBoldStyle()
        {
            GUIStyle largeBoldStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            return largeBoldStyle;
        }

        void UpdateEditor()
        {
            if (previewActive && originalObject != null && previewObject != null)
            {
                UpdatePreview();
            }
        }

        void UpdatePreview()
        {
            CleanupPreviewObject();

            if (!mirrorX && !mirrorY && !mirrorZ)
                return;

            if (Selection.activeGameObject == null)
            {
                Debug.LogError("No GameObject selected!");
                return;
            }

            originalObject = Selection.activeGameObject;
            MeshFilter meshFilter = originalObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = originalObject.GetComponent<MeshRenderer>();

            if (meshFilter == null || meshRenderer == null)
            {
                Debug.LogError("Selected GameObject does not have a MeshFilter or MeshRenderer component!");
                return;
            }

            Mesh originalMesh = meshFilter.sharedMesh;
            if (originalMesh == null)
            {
                Debug.LogError("Selected GameObject does not have a mesh!");
                return;
            }

            Mesh mirroredMesh = CreateMirroredMesh(originalMesh);

            previewObject = new GameObject("MeshPreview");
            MeshFilter previewMeshFilter = previewObject.AddComponent<MeshFilter>();
            MeshRenderer previewMeshRenderer = previewObject.AddComponent<MeshRenderer>();
            previewMeshFilter.mesh = mirroredMesh;

            previewMeshRenderer.sharedMaterial = meshRenderer.sharedMaterial;

            previewObject.transform.position = originalObject.transform.position;
            previewObject.transform.rotation = originalObject.transform.rotation;
            previewObject.transform.localScale = originalObject.transform.localScale;

            previewActive = true;
        }

        void ConfirmMirror()
        {
            if (Selection.activeGameObject == null)
            {
                Debug.LogError("No GameObject selected!");
                return;
            }

            GameObject originalObject = Selection.activeGameObject;
            MeshFilter meshFilter = originalObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = originalObject.GetComponent<MeshRenderer>();

            if (meshFilter == null || meshRenderer == null)
            {
                Debug.LogError("Selected GameObject does not have a MeshFilter or MeshRenderer component!");
                return;
            }

            Mesh originalMesh = meshFilter.sharedMesh;
            if (originalMesh == null)
            {
                Debug.LogError("Selected GameObject does not have a mesh!");
                return;
            }

            Undo.RegisterCompleteObjectUndo(originalObject, "Mirror Mesh");
            Undo.RegisterCompleteObjectUndo(originalMesh, "Mirror Mesh Asset");

            Mesh mirroredMesh = CreateMirroredMesh(originalMesh);
            string originalName = originalMesh.name;
            string mirroredName = originalName + "_mirrored";
            mirroredMesh.name = mirroredName;

            string originalPath = AssetDatabase.GetAssetPath(originalMesh);
            string directory = Path.GetDirectoryName(originalPath);
            string mirroredPath = Path.Combine(directory, mirroredName + ".asset");

            AssetDatabase.CreateAsset(mirroredMesh, mirroredPath);
            AssetDatabase.SaveAssets();

            meshFilter.sharedMesh = mirroredMesh;
            meshRenderer.sharedMaterial = meshRenderer.sharedMaterial;

            CleanupPreviewObject();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Mesh>(mirroredPath);

            Debug.Log($"Mesh '{originalName}' mirrored and saved as '{mirroredName}' at '{mirroredPath}'");
        }

        Mesh CreateMirroredMesh(Mesh originalMesh)
        {
            Mesh mirroredMesh = Instantiate(originalMesh);

            Vector3[] vertices = mirroredMesh.vertices;
            Vector3[] normals = mirroredMesh.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (mirrorX) vertices[i].x = -vertices[i].x;
                if (mirrorY) vertices[i].y = -vertices[i].y;
                if (mirrorZ) vertices[i].z = -vertices[i].z;

                if (mirrorX) normals[i].x = -normals[i].x;
                if (mirrorY) normals[i].y = -normals[i].y;
                if (mirrorZ) normals[i].z = -normals[i].z;
            }

            mirroredMesh.vertices = vertices;
            mirroredMesh.normals = normals;

            int[] triangles = mirroredMesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int temp = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = temp;
            }
            mirroredMesh.triangles = triangles;

            mirroredMesh.RecalculateNormals();
            mirroredMesh.RecalculateBounds();
            return mirroredMesh;
        }

        void CleanupPreviewObject()
        {
            if (previewObject != null)
            {
                DestroyImmediate(previewObject);
                previewObject = null;
            }
        }
    }
}