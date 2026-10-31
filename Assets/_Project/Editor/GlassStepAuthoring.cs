using System;
using System.Linq;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives the stair step a frosted glass look: white with a slight blue, deeper blue at the rim.
/// Only the step prefab's material changes; its mesh stages, collider, Building settings and
/// construction motion stay as they are. The old marble material remains as history.
/// </summary>
public static class GlassStepAuthoring
{
    public const string PrefabPath = "Assets/_Project/Prefabs/Buildings/Step_Tile.prefab";
    public const string MaterialPath = "Assets/_Project/Models/Buildings/FrutigerAero/Aero_Glass_Step_Tile.mat";
    public const string ShaderName = "TowerOfBabel/Buildings/Glass Step";

    [MenuItem("Tower of Babel/Buildings/Apply glass stair steps")]
    public static void ApplyMenu() => Apply();

    public static object Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project's Editor in Edit Mode.");
        Material material = CreateMaterial();
        string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            root.GetComponent<MeshRenderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        return new
        {
            prefab = PrefabPath, prefabGuidPreserved = guid == AssetDatabase.AssetPathToGUID(PrefabPath),
            material = AssetDatabase.GetAssetPath(prefab.GetComponent<MeshRenderer>().sharedMaterial),
            shader = material.shader.name, material.renderQueue, instancing = material.enableInstancing,
            completedMesh = prefab.GetComponent<Building>().GetStageMesh(10).name,
            shaderMessages = ShaderUtil.GetShaderMessages(material.shader).Select(m => m.message + " @" + m.line).ToArray()
        };
    }

    private static Material CreateMaterial()
    {
        var shader = Shader.Find(ShaderName) ?? throw new InvalidOperationException("Shader not compiled: " + ShaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) { material = new Material(shader) { name = "Aero_Glass_Step_Tile" }; AssetDatabase.CreateAsset(material, MaterialPath); }
        material.shader = shader;
        // 2026-10-31: glass steps, white with a slight blue. Second pass: clear glass with
        // refraction and real thick-glass edges (deep aqua band, dark/bright edge lines).
        material.SetColor("_TintColor", new Color(.95f, .98f, 1f));
        material.SetColor("_EdgeTint", new Color(.5f, .85f, .94f));
        material.SetFloat("_EdgeLength", 1f);
        material.SetFloat("_BodyBlend", .6f); material.SetFloat("_Dispersion", .02f);
        // The slab frame used to trace rays against the walls comes from the completed mesh.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Bounds bounds = prefab.GetComponent<Building>().GetStageMesh(10).bounds;
        material.SetVector("_SlabCenter", bounds.center);
        material.SetVector("_SlabHalfSize", bounds.extents);
        material.SetFloat("_CornerRadius", .3f);
        material.renderQueue = -1;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }
}
