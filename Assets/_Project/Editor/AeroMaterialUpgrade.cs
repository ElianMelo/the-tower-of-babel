#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Moves project materials onto the Aero shading library. Idempotent: running it again
/// re-applies the same settings. Textures survive because AeroSurface uses URP/Lit names.
/// </summary>
public static class AeroMaterialUpgrade
{
    public const int Version = 2; // 2: walkable decks without wet beads
    public const string SurfaceShader = "TowerOfBabel/Aero/Surface";
    public const string GlassShader = "TowerOfBabel/Aero/Glass";
    public const string JellyShader = "TowerOfBabel/Aero/Jelly Plastic";
    const string Lighting = "Assets/_Project/Environment/Lighting/";
    const string Resources = "Assets/_Project/Models/Resources/Materials/";
    const string Buildings = "Assets/_Project/Models/Buildings/FrutigerAero/";

    public enum Kind { BrushedSteel, CandyMetal, Pearl, Porous, Ceramic, Glass, Jelly, GlowJelly }

    /// <summary>Every material this upgrade owns, with its target look.</summary>
    public static readonly IReadOnlyDictionary<string, Kind> Targets = new Dictionary<string, Kind>
    {
        { Resources + "Resource_Steel.mat", Kind.BrushedSteel },
        { Resources + "Workshop_Steel.mat", Kind.BrushedSteel },
        { Resources + "Resource_BlueMetal.mat", Kind.CandyMetal },
        { Resources + "Workshop_Azure.mat", Kind.CandyMetal },
        { Resources + "Resource_Ceramic.mat", Kind.Pearl },
        { Resources + "Workshop_Pearl.mat", Kind.Pearl },
        { Lighting + "AquaLampPearl.mat", Kind.Pearl },
        { Resources + "Resource_Glow.mat", Kind.Ceramic },
        { Resources + "Resource_Refined.mat", Kind.Ceramic },
        { Resources + "Resource_Stone.mat", Kind.Porous },
        { Resources + "Workshop_Chip.mat", Kind.Porous },
        { Resources + "Workshop_Sand.mat", Kind.Porous },
        { Resources + "Workshop_SandLight.mat", Kind.Porous },
        { "Assets/_Project/Materials/StoneMaterial.mat", Kind.Porous },
        { Resources + "Workshop_Glass.mat", Kind.Glass },
        { Lighting + "AquaLampBlue.mat", Kind.Jelly },
        { Resources + "Workshop_Heat.mat", Kind.Jelly },
        { Lighting + "AquaLampGlow.mat", Kind.GlowJelly },
    };

    /// <summary>Existing project shaders that gain wetness/subsurface features.</summary>
    public static readonly string[] Marble =
    {
        Buildings + "Aero_WhiteMarble_Arch.mat", Buildings + "Aero_WhiteMarble_Floor_Tile.mat",
        Buildings + "Aero_WhiteMarble_Pillar.mat", Buildings + "Aero_WhiteMarble_Step_Tile.mat",
        Lighting + "GlassWaterFloor.mat",
    };
    public const string BushLeaf = "Assets/_Project/Environment/Foliage/AeroBushLeaf.mat";

    [MenuItem("Tower of Babel/Rendering/Upgrade materials to Aero shading")]
    public static string Apply()
    {
        int changed = 0;
        foreach (var pair in Targets)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(pair.Key);
            if (material == null) { Debug.LogWarning("Aero upgrade: missing " + pair.Key); continue; }
            Upgrade(material, pair.Value); EditorUtility.SetDirty(material); changed++;
        }
        foreach (var path in Marble)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) continue;
            SetFeature(material, "_AERO_WETNESS", "_WetnessOn", true);
            material.SetFloat("_Porosity", .12f);
            // Walkable decks get a clean glossy film; drops would read as speckle/dirt.
            bool walkable = path.Contains("Floor_Tile") || path.Contains("Step_Tile") || path.Contains("GlassWaterFloor");
            material.SetFloat("_WetBeads", walkable ? 0 : .35f);
            if (material.HasProperty("_AlbedoCeiling")) material.SetFloat("_AlbedoCeiling", .94f);
            EditorUtility.SetDirty(material); changed++;
        }
        var bush = AssetDatabase.LoadAssetAtPath<Material>(BushLeaf);
        if (bush != null) { SetFeature(bush, "_AERO_WETNESS", "_WetnessOn", true); EditorUtility.SetDirty(bush); changed++; }
        AssetDatabase.SaveAssets();
        return $"Aero shading v{Version}: {changed} materials configured.";
    }

    public static void Upgrade(Material material, Kind kind)
    {
        // Snapshot values that change meaning between shaders.
        Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
        Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
        bool emissive = material.IsKeywordEnabled("_EMISSION");
        float smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : .7f;
        var giFlags = material.globalIlluminationFlags;

        switch (kind)
        {
            case Kind.Glass:
                Swap(material, GlassShader);
                material.SetColor("_BaseColor", new Color(Mathf.Lerp(baseColor.r, 1, .55f), Mathf.Lerp(baseColor.g, 1, .55f), Mathf.Lerp(baseColor.b, 1, .55f), 1));
                material.SetFloat("_Thickness", .04f); material.SetFloat("_Density", 7);
                material.SetFloat("_Dispersion", .06f); material.SetFloat("_FilmStrength", .15f);
                SetFeature(material, "_AERO_WETNESS", "_WetnessOn", true);
                break;
            case Kind.Jelly:
            case Kind.GlowJelly:
                Swap(material, JellyShader);
                material.SetColor("_BaseColor", baseColor);
                material.SetColor("_RimColor", Color.Lerp(baseColor, Color.white, .7f));
                material.SetFloat("_Smoothness", Mathf.Max(smoothness, .92f));
                material.SetFloat("_InteriorScale", 4.5f); material.SetFloat("_InteriorStrength", .2f);
                material.SetColor("_EmissionColor", emission);
                SetFeature(material, "_EMISSION", "_EmissionOn", kind == Kind.GlowJelly || emissive);
                break;
            default:
                Swap(material, SurfaceShader);
                material.SetColor("_BaseColor", baseColor);
                material.SetColor("_EmissionColor", emission);
                SetFeature(material, "_EMISSION", "_EmissionOn", emissive);
                SetFeature(material, "_AERO_WETNESS", "_WetnessOn", true);
                SetFeature(material, "_AERO_ANISO", "_AnisoOn", kind == Kind.BrushedSteel);
                SetFeature(material, "_AERO_IRIDESCENT", "_IridescenceOn", kind == Kind.Pearl);
                SetFeature(material, "_AERO_SSS", "_SubsurfaceOn", false);
                SetFeature(material, "_AERO_TRANSLUCENT", "_TranslucentOn", false);
                switch (kind)
                {
                    case Kind.BrushedSteel:
                        material.SetFloat("_Smoothness", Mathf.Max(smoothness, .78f));
                        material.SetFloat("_Anisotropy", .75f); material.SetFloat("_AnisoMode", 2);
                        material.SetVector("_AnisoAxis", new Vector4(0, 1, 0, 0)); material.SetFloat("_SheenStrength", .35f);
                        material.SetFloat("_ClearCoatMask", 0); material.SetFloat("_Porosity", 0);
                        break;
                    case Kind.CandyMetal:
                        material.SetFloat("_ClearCoatMask", .85f); material.SetFloat("_ClearCoatSmoothness", .96f);
                        material.SetFloat("_Porosity", 0);
                        break;
                    case Kind.Pearl:
                        material.SetFloat("_ClearCoatMask", .9f); material.SetFloat("_ClearCoatSmoothness", .97f);
                        material.SetFloat("_IridescenceMode", 0); material.SetFloat("_IridescenceStrength", .35f);
                        material.SetFloat("_FilmThickness", 420); material.SetFloat("_FilmThicknessRange", 260); material.SetFloat("_FilmIOR", 1.45f);
                        material.SetFloat("_Porosity", .05f);
                        break;
                    case Kind.Ceramic:
                        material.SetFloat("_ClearCoatMask", .6f); material.SetFloat("_ClearCoatSmoothness", .94f);
                        material.SetFloat("_Porosity", .25f);
                        break;
                    case Kind.Porous:
                        material.SetFloat("_ClearCoatMask", 0); material.SetFloat("_Porosity", .85f);
                        break;
                }
                break;
        }
        material.globalIlluminationFlags = giFlags;
        material.enableInstancing = true;
    }

    static void Swap(Material material, string shaderName)
    {
        var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException("Missing shader " + shaderName);
        if (material.shader != shader) material.shader = shader;
        material.renderQueue = -1;
        // URP/Lit transparency keywords do not belong to the Aero shaders.
        foreach (var k in new[] { "_SURFACE_TYPE_TRANSPARENT", "_ALPHAPREMULTIPLY_ON", "_ALPHATEST_ON", "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP" })
            material.DisableKeyword(k);
    }

    public static void SetFeature(Material material, string keyword, string toggle, bool on)
    {
        if (material.HasProperty(toggle)) material.SetFloat(toggle, on ? 1 : 0);
        if (on) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
    }
}
#endif
