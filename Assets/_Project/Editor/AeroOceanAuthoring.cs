using System;
using System.Linq;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AeroOceanAuthoring
{
    public const string MeshPath="Assets/_Project/Environment/Lighting/AeroSeamlessOcean.asset";
    public static object Apply()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode ||
            scene.path!="Assets/_Project/Scenes/Tower.unity" || scene.isDirty) throw new InvalidOperationException("Use saved Tower in Edit Mode.");
        var water=UnityEngine.Object.FindObjectsByType<StylizedWater3.WaterObject>(FindObjectsSortMode.None).Single(w=>w.name=="StylizedWater_Desktop");
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        var generated=AeroOceanSurface.CreateMesh();
        if(mesh==null) {mesh=generated;AssetDatabase.CreateAsset(mesh,MeshPath);}
        else {EditorUtility.CopySerialized(generated,mesh);UnityEngine.Object.DestroyImmediate(generated);EditorUtility.SetDirty(mesh);}
        water.transform.localScale=Vector3.one;
        water.meshFilter.sharedMesh=mesh;
        water.meshRenderer.localBounds=mesh.bounds;
        var ocean=water.GetComponent<AeroOceanSurface>() ?? water.gameObject.AddComponent<AeroOceanSurface>();
        ocean.PrepareForCamera(Camera.main);
        water.material.SetFloat("_WorldSpaceUV",1);
        water.material.SetVector("_WaveFadeDistance",new Vector4(150,600,0,0));
        water.material.SetVector("_DistanceNormalsFadeDist",new Vector4(80,350,0,0));
        EditorUtility.SetDirty(water.meshFilter);EditorUtility.SetDirty(water.meshRenderer);
        EditorUtility.SetDirty(water.material);EditorUtility.SetDirty(water.transform);EditorUtility.SetDirty(ocean);EditorUtility.SetDirty(Camera.main);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new {vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,extent=AeroOceanSurface.OuterExtent,farClip=Camera.main.farClipPlane,draws=1};
    }
}
