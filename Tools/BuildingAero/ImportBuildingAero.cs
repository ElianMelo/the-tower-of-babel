// Evaluate through Unity MCP script.eval_file in the connected project Editor.
// Authoring only: retains prefab GUIDs, roots, gameplay references and stage count.
if (Application.dataPath.Replace('\\','/') != "D:/PersonalProjects/the-tower-of-babel/Assets")
    throw new InvalidOperationException("Wrong Unity project");
if (EditorApplication.isPlaying)
    throw new InvalidOperationException("Import only in Edit Mode");

const string dir="Assets/_Project/Models/Buildings/FrutigerAero";
const string matPath=dir+"/Aero_BuildingAtlas.mat";
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

foreach(var name in new[]{"Aero_Palette","Aero_MetallicSmoothness","Aero_Emission","Aero_Roughness_Source"})
{
    var imp=(TextureImporter)AssetImporter.GetAtPath(dir+"/"+name+".png");
    imp.textureType=TextureImporterType.Default;
    imp.sRGBTexture=name=="Aero_Palette" || name=="Aero_Emission";
    imp.alphaSource=TextureImporterAlphaSource.FromInput;
    imp.alphaIsTransparency=false;
    imp.mipmapEnabled=true;
    imp.wrapMode=TextureWrapMode.Clamp;
    imp.filterMode=FilterMode.Bilinear;
    imp.textureCompression=TextureImporterCompression.Uncompressed;
    imp.maxTextureSize=512;
    imp.SaveAndReimport();
}

var shader=Shader.Find("TowerOfBabel/Buildings/Water Glass");
if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");
if(shader==null) throw new InvalidOperationException("URP Lit not available");
var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);}
material.shader=shader;material.name="Aero_BuildingAtlas";material.enableInstancing=true;
material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/Aero_Palette.png"));
material.SetColor("_BaseColor",new Color(.91f,.965f,1,1));
material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/Aero_MetallicSmoothness.png"));
material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);
material.SetFloat("_SmoothnessTextureChannel",0);
material.EnableKeyword("_METALLICSPECGLOSSMAP");
material.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/Aero_Emission.png"));
material.SetColor("_EmissionColor",new Color(.3f,.3f,.3f,1));material.EnableKeyword("_EMISSION");
bool waterGlass=shader.name=="TowerOfBabel/Buildings/Water Glass";
material.SetFloat("_Surface",waterGlass?1:0);material.SetFloat("_Cull",2);
material.renderQueue=waterGlass?3000:-1;
if(waterGlass){
    material.shaderKeywords=Array.Empty<string>();
    material.SetTexture("_CausticsMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/Aero_WaterCaustics.png"));
}
material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
EditorUtility.SetDirty(material);

var results=new List<object>();
foreach(var name in new[]{"Arch","Floor_Tile","Pillar","Step_Tile"})
{
    var modelMaterial=AssetDatabase.LoadAssetAtPath<Material>(dir+"/Aero_WhiteMarble_"+name+".mat")
        ?? AssetDatabase.LoadAssetAtPath<Material>(dir+"/Aero_WaterGlass_"+name+".mat") ?? material;
    var path=dir+"/"+name+".fbx";
    var importer=(ModelImporter)AssetImporter.GetAtPath(path);
    importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;
    importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
    importer.importBlendShapes=false;importer.addCollider=false;
    importer.meshCompression=ModelImporterMeshCompression.Off;
    importer.isReadable=true;
    importer.importNormals=ModelImporterNormals.Import;
    importer.importTangents=ModelImporterTangents.CalculateMikk;
    importer.generateSecondaryUV=false;
    importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Aero_BuildingAtlas"),modelMaterial);
    importer.SaveAndReimport();
    var meshes=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
    if(meshes.Length!=1 || meshes[0].subMeshCount!=1)
        throw new InvalidOperationException(name+" needs one mesh and one submesh");
    var mesh=meshes[0];
    var prefabPath="Assets/_Project/Prefabs/Buildings/"+name+".prefab";
    var root=PrefabUtility.LoadPrefabContents(prefabPath);
    try
    {
        var filter=root.GetComponent<MeshFilter>();
        filter.sharedMesh=mesh;
        root.GetComponent<MeshRenderer>().sharedMaterials=new[]{modelMaterial};
        var building=root.GetComponent<TowerOfBabel.Buildings.Building>();
        var serialized=new SerializedObject(building);
        serialized.FindProperty("meshFilter").objectReferenceValue=filter;
        var stages=serialized.FindProperty("stageMeshes");
        var construction=name=="Pillar" ? AssetDatabase.LoadAllAssetsAtPath(dir+"/Construction/PillarStages.fbx").OfType<Mesh>().ToArray() : Array.Empty<Mesh>();
        for(int i=0;i<stages.arraySize;i++)
            stages.GetArrayElementAtIndex(i).objectReferenceValue=i<10 ? construction.FirstOrDefault(m=>m.name=="Pillar_Stage_"+i.ToString("00")) ?? mesh : mesh;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if(name=="Pillar" && construction.Length==10) filter.sharedMesh=building.GetStageMesh(0);
        if(name=="Floor_Tile")
        {
            var oldBox=root.GetComponent<BoxCollider>();
            if(oldBox!=null)UnityEngine.Object.DestroyImmediate(oldBox);
            var collider=root.GetComponent<MeshCollider>();
            if(collider==null)collider=root.AddComponent<MeshCollider>();
            collider.sharedMesh=mesh;collider.convex=false;
        }
        if(name=="Arch")
        {
            // A full box would obstruct the arch's opening. Use its solid panel mesh.
            var oldBox=root.GetComponent<BoxCollider>();
            if(oldBox!=null)UnityEngine.Object.DestroyImmediate(oldBox);
            var collider=root.GetComponent<MeshCollider>();
            if(collider==null)collider=root.AddComponent<MeshCollider>();
            collider.sharedMesh=null;collider.sharedMesh=mesh;collider.convex=false;collider.isTrigger=false;
        }
        PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        results.Add(new{name,vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,
            min=mesh.bounds.min.ToString("F5"),max=mesh.bounds.max.ToString("F5"),
            rootChildren=root.transform.childCount,stages=stages.arraySize,
            rootScale=root.transform.localScale.ToString(),collider=root.GetComponent<Collider>().GetType().Name});
    }
    finally{PrefabUtility.UnloadPrefabContents(root);}
}
// These were temporary axis/scale calibration exports, never gameplay assets.
AssetDatabase.DeleteAsset(dir+"/Calibration.fbx");
AssetDatabase.DeleteAsset(dir+"/Calibration2.fbx");
AssetDatabase.SaveAssets();
System.IO.Directory.CreateDirectory("Documentation/FrutigerAero/Buildings");
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/ImportReport.json",
    Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
return results;
