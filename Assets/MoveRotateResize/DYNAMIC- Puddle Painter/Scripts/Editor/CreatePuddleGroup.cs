//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;
using System.IO;
using UnityEngine.Rendering.Universal; // Ported from HDRP: project uses URP.
using Object = UnityEngine.Object;

namespace MoveRotateResize.Dynamic.PuddlePainter
{
    #if UNITY_EDITOR
    public class CreatePuddleGroup : EditorWindow
    {
        private string sourceFolderPath = "MoveRotateResize/DYNAMIC- Puddle Painter/Puddle Groups/Puddle Group- Default/";
        private string destinationParentFolderPath = "MoveRotateResize/DYNAMIC- Puddle Painter/Puddle Groups/";
        private string userFolderName = "Default";
        string destinationFolderPath;
        string uniqueFolderName;

        [MenuItem("Tools/MoveRotateResize/DYNAMIC- Puddle Painter/Create Puddle Group")]
        public static void ShowWindow()
        {
            GetWindow<CreatePuddleGroup>("Create Puddle Group");
        }

        private void OnGUI()
        {
            GUIStyle largeBoldStyle = GetLargeBoldStyle();
            GUILayout.Label("Create Puddle Group", largeBoldStyle);
            GUILayout.Space(15);

            userFolderName = EditorGUILayout.TextField("Puddle Group-", userFolderName);
            EditorGUILayout.HelpBox("Type Puddle Group Name.", MessageType.Info);
            GUILayout.Space(5);
            EditorGUILayout.HelpBox("Create as many puddle groups as your scene requires. As each puddle group has it's own set of textutres and settings.", MessageType.Info);

            GUILayout.Space(15);

            if (GUILayout.Button("Create New Puddle Group"))
            {
                string fullSourcePath = Path.Combine(Application.dataPath, sourceFolderPath);
                string fullDestinationParentPath = Path.Combine(Application.dataPath, destinationParentFolderPath);
                uniqueFolderName = GetUniqueFolderName(fullDestinationParentPath, "Puddle Group-" + userFolderName);
                destinationFolderPath = Path.Combine(fullDestinationParentPath, uniqueFolderName);

                if (CreateNewVersionOfObjects(fullSourcePath, destinationFolderPath))
                {
                    FocusOnFolder(destinationFolderPath);
                }
            }
        }

        private GUIStyle GetLargeBoldStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
        }

        private string GetUniqueFolderName(string parentPath, string desiredName)
        {
            string folderName = $"{desiredName}_01";
            string folderPath = Path.Combine(parentPath, folderName);

            int counter = 1;
            while (Directory.Exists(folderPath))
            {
                counter++;
                folderName = $"{desiredName}_{counter:D2}";
                folderPath = Path.Combine(parentPath, folderName);
            }

            return folderName;
        }

        private bool CreateNewVersionOfObjects(string source, string destination)
        {
            if (!Directory.Exists(source))
            {
                Debug.LogError($"Source folder does not exist: {source}");
                return false;
            }

            if (Directory.Exists(destination))
            {
                Debug.LogError($"Destination folder already exists: {destination}");
                return false;
            }

            Directory.CreateDirectory(destination);

            bool success = CopyFilesAndProcessAssets(source, destination);
            bool dirsSuccess = CopyDirectories(source, destination);

            AssetDatabase.Refresh();

            if (success && dirsSuccess)
            {
                string[] prefabFiles = Directory.GetFiles(destination, "*.prefab", SearchOption.AllDirectories);
                foreach (var prefabFile in prefabFiles)
                {
                    string assetPath = "Assets" + prefabFile.Substring(Application.dataPath.Length).Replace('\\', '/');
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    
                    if (prefab != null)
                    {
                        var decalProjectors = prefab.GetComponentsInChildren<DecalProjector>(true);
                        if (decalProjectors.Length > 0)
                        {
                            AssignMaterialsToDecalProjectors(decalProjectors, destinationFolderPath);
                        }
                    }
                }

                CreateAndAddPuddlePainterToHierarchy(destinationFolderPath);
            }            

            return success && dirsSuccess;
        }

        private bool CopyFilesAndProcessAssets(string source, string destination)
        {
            foreach (var file in Directory.GetFiles(source))
            {
                string extension = Path.GetExtension(file).ToLower();
                string destinationFile = Path.Combine(destination, Path.GetFileName(file));

                if (!CopyFile(file, destinationFile, extension))
                {
                    return false;
                }
            }
            return true;
        }

        private bool CopyFile(string sourceFile, string destinationFile, string extension)
        {
            try
            {
                if (extension == ".png")
                {
                    File.Copy(sourceFile, destinationFile);
                    string assetPath = "Assets" + destinationFile.Substring(Application.dataPath.Length).Replace('\\', '/');
                    AssetDatabase.ImportAsset(assetPath);
                }
                else if (extension == ".prefab")
                {
                    CopyPrefab(sourceFile, destinationFile);
                }
                else
                {
                    CopyNonTextureAsset(sourceFile, destinationFile);
                }
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error copying file {sourceFile}: {e.Message}");
                return false;
            }
        }

        private void CopyPrefab(string sourceFile, string destinationFile)
        {
            string assetPath = "Assets" + sourceFile.Substring(Application.dataPath.Length).Replace('\\', '/');
            string newAssetPath = "Assets" + destinationFile.Substring(Application.dataPath.Length).Replace('\\', '/');

            AssetDatabase.CopyAsset(assetPath, newAssetPath);
            ProcessPrefab(newAssetPath);
        }

        private void CopyNonTextureAsset(string sourceFile, string destinationFile)
        {
            string assetPath = "Assets" + sourceFile.Substring(Application.dataPath.Length).Replace('\\', '/');
            Object originalAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            if (originalAsset != null)
            {
                string newAssetPath = "Assets" + destinationFile.Substring(Application.dataPath.Length).Replace('\\', '/');

                if (originalAsset is Texture2D texture)
                {
                    HandleNonReadableTexture(texture, newAssetPath);
                }
                else
                {
                    Object newAsset = Object.Instantiate(originalAsset);
                    AssetDatabase.CreateAsset(newAsset, newAssetPath);
                }

                AssetDatabase.ImportAsset(newAssetPath);
            }
            else
            {
                //Debug.LogWarning($"Original asset not found at path: {assetPath}");
            }
        }

        private void HandleNonReadableTexture(Texture2D texture, string newAssetPath)
        {
            string texturePath = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);

            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            Texture2D newTexture = Object.Instantiate(texture);
            AssetDatabase.CreateAsset(newTexture, newAssetPath);

            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private bool CopyDirectories(string source, string destination)
        {
            foreach (var dir in Directory.GetDirectories(source))
            {
                string destDir = Path.Combine(destination, Path.GetFileName(dir));

                if (!CreateNewVersionOfObjects(dir, destDir))
                {
                    return false; 
                }
            }
            return true;
        }

        private void ProcessPrefab(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab != null)
            {
                //Debug.Log($"Processing prefab: {prefabPath}");

                var decalProjectors = prefab.GetComponentsInChildren<DecalProjector>(true);
                if (decalProjectors.Length > 0)
                {
                    //Debug.Log($"Found {decalProjectors.Length} DecalProjector(s) in prefab: {prefabPath}");
                    AssignMaterialsToDecalProjectors(decalProjectors, destinationFolderPath);
                }
            }
        }

        private void AssignMaterialsToDecalProjectors(DecalProjector[] decalProjectors, string destinationFolderPath)
        {
            string relativeDestinationFolderPath = "Assets" + destinationFolderPath.Substring(Application.dataPath.Length).Replace('\\', '/');

            string texturePath = Path.Combine(relativeDestinationFolderPath, "puddle_wetness_mask.png").Replace('\\', '/');
            Texture2D puddleWetnessTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            foreach (var projector in decalProjectors)
            {
                if (projector.material == null)
                {
                    Debug.LogWarning("DecalProjector has no material assigned.");
                    continue;
                }

                string materialName = projector.material.name;
                string materialPath = Path.Combine(relativeDestinationFolderPath, materialName + ".mat").Replace('\\', '/');
                //Debug.Log($"Searching for material: {materialName} at path: {materialPath}");

                Material newMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (newMaterial != null)
                {
                    projector.material = newMaterial;
                    if (puddleWetnessTexture != null)
                    {
                        newMaterial.SetTexture("_Puddle_Paint_Mask", puddleWetnessTexture);
                        //newMaterial.SetTexture("_Terrain_Render_Texture", puddleWetnessRenderTexture);
                    }
                }
                else
                {
                    Debug.LogWarning($"Material file does not exist at path: {materialPath}");
                }
            }
        }

        void CreateAndAddPuddlePainterToHierarchy(string destinationFolderPath)
        {
            string relativePath = "Assets" + destinationFolderPath.Substring(Application.dataPath.Length).Replace('\\', '/');
            string prefabPath = Path.Combine(relativePath, "DYNAMIC- Puddle Painter.prefab").Replace('\\', '/');
            
            GameObject puddlePainterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (puddlePainterPrefab != null)
            {
                GameObject puddlePainterInstance = PrefabUtility.InstantiatePrefab(puddlePainterPrefab) as GameObject;
                
                if (puddlePainterInstance != null)
                {
                    puddlePainterInstance.name = "DYNAMIC- Puddle Painter" + " (" + userFolderName + ")";
                    
                    Undo.RegisterCreatedObjectUndo(puddlePainterInstance, "Instantiate Puddle Painter");

                    PrefabUtility.SaveAsPrefabAssetAndConnect(
                        puddlePainterInstance,
                        prefabPath,
                        InteractionMode.UserAction
                    );
                }
            }
            else
            {
                Debug.LogWarning($"DYNAMIC- Puddle Painter prefab not found at path: {prefabPath}");
            }
        }

        void FocusOnFolder(string folderPath)
        {
            string relativePath = "Assets" + folderPath.Substring(Application.dataPath.Length).Replace('\\', '/');
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(relativePath));
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(relativePath);
        }

        public static void CreatePuddleGroupFromAnotherScript(string userFolderName)
        {
            CreatePuddleGroup window = GetWindow<CreatePuddleGroup>();
            window.CreatePuddleGroupInternal(userFolderName);
        }

        private void CreatePuddleGroupInternal(string userFolderName)
        {
            string fullSourcePath = Path.Combine(Application.dataPath, sourceFolderPath);
            string fullDestinationParentPath = Path.Combine(Application.dataPath, destinationParentFolderPath);
            uniqueFolderName = GetUniqueFolderName(fullDestinationParentPath, "Puddle Group-" + userFolderName);
            destinationFolderPath = Path.Combine(fullDestinationParentPath, uniqueFolderName);

            if (CreateNewVersionOfObjects(fullSourcePath, destinationFolderPath))
            {
                FocusOnFolder(destinationFolderPath);
                Close(); 

            }
        }
    }
    #endif
}