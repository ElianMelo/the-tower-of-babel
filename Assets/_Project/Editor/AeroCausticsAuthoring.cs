using System;
using System.Linq;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Caustics upgrade (2026-11-03): the lamp spotlight cookie and the nearby water light sample looping caustics
/// flipbooks (Texture2DArrays) instead of one scrolling Crest still. The lamp gets a 512 px, 32-frame
/// refraction-baked loop; the water light gets a different, finer CC0 16-frame net at a smaller tile size.
/// </summary>
public static class AeroCausticsAuthoring
{
    public const string Folder = "Assets/_Project/Environment/Lighting/Caustics";
    public const string LampFlipbookPath = Folder + "/LavaLampCaustics.png";
    public const string PoolFlipbookPath = Folder + "/PoolCausticsFlipbook.png";
    public const int LampFrames = 32, LampColumns = 8, LampRows = 4;
    public const int PoolFrames = 16, PoolColumns = 4, PoolRows = 4;
    /// <summary>World tiles per metre of the water light: one tile is about 1.2 m (was 2.4 m).</summary>
    public const float WaterLightScale = .85f;
    public const float WaterLightSpeed = .04f;

    [MenuItem("Tower of Babel/Environment/Apply caustics flipbooks")]
    public static object Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run in this project's Editor, in Edit Mode.");
        var lamp = ImportFlipbook(LampFlipbookPath, LampColumns, LampRows);
        var pool = ImportFlipbook(PoolFlipbookPath, PoolColumns, PoolRows);

        var cookie = AssetDatabase.LoadAssetAtPath<Material>(AeroAtmosphereAuthoring.CookiePath);
        ConfigureCookie(cookie, lamp);

        foreach (var name in new[] { "PC_Renderer", "Mobile_Renderer" })
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>($"Assets/Settings/{name}.asset");
            if (renderer == null) continue;
            foreach (var feature in renderer.rendererFeatures.OfType<AeroLightRendererFeature>()) ConfigureWaterLight(feature, pool);
        }
        AssetDatabase.SaveAssets();
        return new { lamp = $"{lamp.width}x{lamp.height}x{lamp.depth} {lamp.format}", pool = $"{pool.width}x{pool.height}x{pool.depth} {pool.format}" };
    }

    public static Texture2DArray LampFlipbook => AssetDatabase.LoadAssetAtPath<Texture2DArray>(LampFlipbookPath);
    public static Texture2DArray PoolFlipbook => AssetDatabase.LoadAssetAtPath<Texture2DArray>(PoolFlipbookPath);

    public static void ConfigureCookie(Material cookie, Texture2DArray flipbook)
    {
        if (cookie == null || flipbook == null) throw new InvalidOperationException("Cookie material and lamp flipbook are required.");
        cookie.shader = Shader.Find("Hidden/TowerOfBabel/Water Projection Cookie");
        cookie.SetTexture("_CausticsArray", flipbook);
        cookie.SetFloat("_FrameCount", flipbook.depth);
        cookie.SetFloat("_LoopSeconds", 9); cookie.SetFloat("_Dispersion", .018f); cookie.SetFloat("_Tiling", 1.5f);
        EditorUtility.SetDirty(cookie);
    }

    public static void ConfigureWaterLight(AeroLightRendererFeature feature, Texture2DArray flipbook)
    {
        if (feature == null || flipbook == null) throw new InvalidOperationException("Water light feature and pool flipbook are required.");
        feature.caustics = flipbook;
        feature.settings.scale = WaterLightScale; feature.settings.speed = WaterLightSpeed;
        feature.Create(); EditorUtility.SetDirty(feature);
    }

    /// <summary>Imports an atlas of animation frames as a single-channel, mipmapped, tiling Texture2DArray.</summary>
    public static Texture2DArray ImportFlipbook(string path, int columns, int rows)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path) ?? throw new InvalidOperationException("Missing " + path);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.textureType = TextureImporterType.SingleChannel;
        settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
        settings.textureShape = TextureImporterShape.Texture2DArray;
        settings.flipbookColumns = columns; settings.flipbookRows = rows;
        settings.sRGBTexture = false; settings.mipmapEnabled = true; settings.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
        settings.wrapMode = TextureWrapMode.Repeat; settings.filterMode = FilterMode.Trilinear; settings.aniso = 4;
        settings.alphaSource = TextureImporterAlphaSource.None;
        importer.SetTextureSettings(settings);
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
    }
}
