#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEngine;

/// <summary>Rebuildable, shared finished slab. Blueprint and partial stage meshes remain authored models.</summary>
public static class TowerTileRepairAuthoring
{
    public const string MeshPath = "Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset";
    public const int ArcSegments = 16;

    public static Mesh CreateComplete()
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var palette = new List<Vector2>();
        var triangles = new List<int>();
        void Strip(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int spans, bool blue = false)
        {
            int start = positions.Count;
            for (int i = 0; i <= spans; i++)
            {
                float t = (float)i / spans;
                positions.Add(Vector3.Lerp(a, b, t)); positions.Add(Vector3.Lerp(c, d, t));
                normals.Add(normal); normals.Add(normal);
                uv.Add(new Vector2(t, 0)); uv.Add(new Vector2(t, 1));
                palette.Add(new Vector2(blue ? .35f : .05f, .5f));
                palette.Add(new Vector2(blue ? .35f : .05f, .5f));
            }
            bool reverse = Vector3.Dot(Vector3.Cross(c - a, b - a), normal) < 0;
            for (int i = 0; i < spans; i++)
            {
                int x = start + i * 2;
                if (reverse) triangles.AddRange(new[] {x, x + 2, x + 1, x + 1, x + 2, x + 3});
                else triangles.AddRange(new[] {x, x + 1, x + 2, x + 1, x + 3, x + 2});
            }
        }

        // Both slab faces meet the full 4 x 4 footprint, including the centre tip.
        // Matching angular subdivisions prevent the former glass/marble edge mismatch.
        Strip(new Vector3(0, 0, 0), new Vector3(0, 0, -4), new Vector3(4, 0, 0), new Vector3(4, 0, -4), Vector3.up, ArcSegments);
        Strip(new Vector3(0, -.2f, 0), new Vector3(0, -.2f, -4), new Vector3(4, -.2f, 0), new Vector3(4, -.2f, -4), Vector3.down, ArcSegments);
        float[] profile = {-.2f, -.15f, -.08f, 0};
        for (int row = 0; row < profile.Length - 1; row++)
        {
            float y0 = profile[row], y1 = profile[row + 1]; bool blue = row == 1;
            Strip(new Vector3(0,y0,0),new Vector3(0,y0,-4),new Vector3(0,y1,0),new Vector3(0,y1,-4),Vector3.left,ArcSegments,blue);
            Strip(new Vector3(4,y0,0),new Vector3(4,y0,-4),new Vector3(4,y1,0),new Vector3(4,y1,-4),Vector3.right,ArcSegments,blue);
            Strip(new Vector3(0,y0,0),new Vector3(4,y0,0),new Vector3(0,y1,0),new Vector3(4,y1,0),Vector3.forward,1,blue);
            Strip(new Vector3(0,y0,-4),new Vector3(4,y0,-4),new Vector3(0,y1,-4),new Vector3(4,y1,-4),Vector3.back,1,blue);
        }
        var mesh = new Mesh {name = "Floor tile flush surface"};
        mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetUVs(0,uv); mesh.SetUVs(1,palette);
        mesh.SetTriangles(triangles,0); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    public static string Apply()
    {
        if (Application.dataPath.Replace('\\','/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Author only this project in Edit Mode.");
        Mesh generated = CreateComplete();
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null) {mesh = generated; AssetDatabase.CreateAsset(mesh,MeshPath);}
        else {EditorUtility.CopySerialized(generated,mesh); UnityEngine.Object.DestroyImmediate(generated); EditorUtility.SetDirty(mesh);}
        foreach (string name in new[] {"Floor_Tile","GlassWaterFloor"})
        {
            string path = "Assets/_Project/Prefabs/Buildings/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<Building>());
                serialized.FindProperty("stageMeshes").GetArrayElementAtIndex(10).objectReferenceValue = mesh;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<MeshCollider>().sharedMesh = mesh;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        var stone = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Floor_Tile.prefab").GetComponent<Renderer>().sharedMaterial;
        var glass = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/GlassWaterFloor.prefab").GetComponent<Renderer>().sharedMaterial;
        foreach(string property in new[]{"_MarbleMap","_NormalMap"}) glass.SetTexture(property,stone.GetTexture(property));
        foreach(string property in new[]{"_VeinStrength","_NormalStrength","_MarbleScale","_TextureAspect","_PearlCoat"}) glass.SetFloat(property,stone.GetFloat(property));
        glass.SetColor("_BaseColor",stone.GetColor("_BaseColor")); glass.SetFloat("_MarbleSmoothness",stone.GetFloat("_Smoothness"));
        EditorUtility.SetDirty(glass);
        AssetDatabase.SaveAssets();
        return $"Shared flush slab: {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles; stage 0–9 unchanged.";
    }
}
#endif
