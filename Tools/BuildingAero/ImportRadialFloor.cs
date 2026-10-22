// Import only the rebuilt floor; retain its prefab/material GUID and construction stages.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets"||EditorApplication.isPlaying)throw new Exception("Use this project in Edit Mode");
const string dir="Assets/_Project/Models/Buildings/FrutigerAero";
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
var importer=(ModelImporter)AssetImporter.GetAtPath(dir+"/Floor_Tile.fbx");
importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.isReadable=true;
var material=AssetDatabase.LoadAssetAtPath<Material>(dir+"/Aero_WhiteMarble_Floor_Tile.mat");
material.SetFloat("_RadialFloor",0);material.SetFloat("_BorderGlass",0);material.DisableKeyword("_AERO_BORDER_GLASS");
EditorUtility.SetDirty(material);
importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Aero_BuildingAtlas"),material);importer.SaveAndReimport();
var mesh=AssetDatabase.LoadAllAssetsAtPath(dir+"/Floor_Tile.fbx").OfType<Mesh>().Single();
var path="Assets/_Project/Prefabs/Buildings/Floor_Tile.prefab";var root=PrefabUtility.LoadPrefabContents(path);
try{
    root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterial=material;
    var so=new SerializedObject(root.GetComponent<TowerOfBabel.Buildings.Building>());var stages=so.FindProperty("stageMeshes");
    for(int i=0;i<stages.arraySize;i++)stages.GetArrayElementAtIndex(i).objectReferenceValue=mesh;
    so.ApplyModifiedPropertiesWithoutUndo();
    var box=root.GetComponent<BoxCollider>();if(box!=null)UnityEngine.Object.DestroyImmediate(box);
    var collider=root.GetComponent<MeshCollider>();if(collider==null)collider=root.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=false;
    PrefabUtility.SaveAsPrefabAsset(root,path);
}finally{PrefabUtility.UnloadPrefabContents(root);}
AssetDatabase.SaveAssets();
return new{mesh.name,triangles=mesh.triangles.Length/3,mesh.subMeshCount,bounds=mesh.bounds.ToString(),normals=mesh.normals.Length,tangents=mesh.tangents.Length,uv=mesh.uv.Length,uv1=mesh.uv2.Length};
