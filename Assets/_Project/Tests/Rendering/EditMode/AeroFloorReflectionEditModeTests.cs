using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>
    /// The live floor reflection covers the whole deck the camera sees. It used to fade toward the screen edges and over
    /// 24-32 m back to the environment cubemap, which at night is far brighter than the real sky: the floor showed a
    /// dark frame inside a bright border that followed the player (reported 2026-11-14).
    /// </summary>
    public sealed class AeroFloorReflectionEditModeTests
    {
        private const string FloorMaterial = "Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Floor_Tile.mat";

        private static readonly string[] Floats =
            { "_AeroFinishEnabled", "_AeroFinishEnvironmentReady", "_AeroFinishEnvironmentShared", "_AeroFinishCapturing" };
        private static readonly string[] Vectors =
            { "_AeroFinishSurface", "_AeroFinishOptics", "_AeroPlanarParams", "_AeroFinishDayTint", "_AeroFinishNightTint" };

        [Test]
        public void LiveFloorReflection_ReachesTheScreenEdgesAndTheFarDeck()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterial);
            Assume.That(material, Is.Not.Null);
            var savedFloats = new Dictionary<string, float>();
            var savedVectors = new Dictionary<string, Vector4>();
            foreach (var name in Floats) savedFloats[name] = Shader.GetGlobalFloat(name);
            foreach (var name in Vectors) savedVectors[name] = Shader.GetGlobalVector(name);
            Matrix4x4 savedMatrix = Shader.GetGlobalMatrix("_AeroPlanarMatrix");
            Texture savedDay = Shader.GetGlobalTexture("_AeroFinishDay"), savedPlanar = Shader.GetGlobalTexture("_AeroPlanarTexture");

            using var h = new AeroRenderHarness(128);
            float planar = 0; Color tint = Color.white;
            // A bright, uniform sky cubemap (the environment fallback) and a black live image: wherever the live weight
            // is w, the floor shows w of the black image instead of the cubemap.
            var sky = h.Own(new Cubemap(8, TextureFormat.RGBAHalf, false));
            var white = new Color[64]; for (int i = 0; i < white.Length; i++) white[i] = new Color(4, 4, 4, 1);
            for (int face = 0; face < 6; face++) sky.SetPixels(white, (CubemapFace)face);
            sky.Apply();
            // Set after the scene's own AeroBuildingFinish resets the finish globals for every camera.
            void Begin(ScriptableRenderContext context, Camera camera)
            {
                if (camera != h.Camera) return;
                Shader.SetGlobalFloat("_AeroFinishEnabled", 1); Shader.SetGlobalFloat("_AeroFinishEnvironmentReady", 1);
                Shader.SetGlobalFloat("_AeroFinishEnvironmentShared", 1); Shader.SetGlobalFloat("_AeroFinishCapturing", 0);
                Shader.SetGlobalVector("_AeroFinishSurface", new Vector4(1, 0, 0, 1));
                Shader.SetGlobalVector("_AeroFinishOptics", Vector4.zero);
                Shader.SetGlobalTexture("_AeroFinishDay", sky);
                Shader.SetGlobalColor("_AeroFinishDayTint", tint); Shader.SetGlobalColor("_AeroFinishNightTint", tint);
                // Range 32 m, deck at y = 0. For a point on the mirror plane the reflection camera's matrix projects
                // it where the main camera does, so the camera's own matrix stands in for it.
                Shader.SetGlobalVector("_AeroPlanarParams", new Vector4(planar, 0, 32, 0));
                Shader.SetGlobalTexture("_AeroPlanarTexture", Texture2D.blackTexture);
                Shader.SetGlobalMatrix("_AeroPlanarMatrix", GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix);
            }
            RenderPipelineManager.beginCameraRendering += Begin;
            try
            {
                // Player eye height over a deck reaching 110 m ahead and 60 m to each side.
                h.Camera.transform.SetPositionAndRotation(new Vector3(0, 1.7f, 0), Quaternion.Euler(8, 0, 0));
                h.Camera.fieldOfView = 60; h.Camera.nearClipPlane = .1f; h.Camera.farClipPlane = 300;
                h.Spawn(PrimitiveType.Plane, h.Own(new Material(material)), new Vector3(0, 0, 50), new Vector3(12, 1, 12));

                planar = 1; tint = Color.white; var live = h.Render();
                planar = 0; var environment = h.Render();
                tint = Color.black; var none = h.Render();

                float edgeWeight = 1, farWeight = 1, centreWeight = 1; int edges = 0, far = 0, centre = 0;
                var deck = new Plane(Vector3.up, 0);
                for (int y = 0; y < h.Size; y++)
                for (int x = 0; x < h.Size; x++)
                {
                    float reflection = h.Pixel(environment, x, y).grayscale - h.Pixel(none, x, y).grayscale;
                    var ray = h.Camera.ViewportPointToRay(new Vector3((x + .5f) / h.Size, (y + .5f) / h.Size));
                    if (reflection < .05f || !deck.Raycast(ray, out float metres)) continue;
                    float weight = (h.Pixel(environment, x, y).grayscale - h.Pixel(live, x, y).grayscale) / reflection;
                    bool edge = x < 3 || x >= h.Size - 3 || y < 3;
                    if (edge) { edgeWeight = Mathf.Min(edgeWeight, weight); edges++; }
                    if (metres > 40 && metres < 100) { farWeight = Mathf.Min(farWeight, weight); far++; }
                    if (!edge && metres < 20) { centreWeight = Mathf.Min(centreWeight, weight); centre++; }
                }
                Assume.That(centre, Is.GreaterThan(100), "Sanity: the near deck shows the environment reflection.");
                Assert.That(centreWeight, Is.GreaterThan(.95f), "Near the camera the deck reflects the live image.");
                Assert.That(edges, Is.GreaterThan(100), "Sanity: the deck reaches the left, right and bottom screen edges.");
                Assert.That(edgeWeight, Is.GreaterThan(.95f),
                    "The live image covers the deck up to the screen edges; a fade there shows as a bright frame.");
                Assert.That(far, Is.GreaterThan(20), "Sanity: deck pixels past the 32 m capture range are in view.");
                Assert.That(farWeight, Is.GreaterThan(.95f),
                    "Past the capture range the deck keeps the live image (it holds the sky); a fade there shows as a band ahead.");
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= Begin;
                foreach (var pair in savedFloats) Shader.SetGlobalFloat(pair.Key, pair.Value);
                foreach (var pair in savedVectors) Shader.SetGlobalVector(pair.Key, pair.Value);
                Shader.SetGlobalMatrix("_AeroPlanarMatrix", savedMatrix);
                if (savedDay != null) Shader.SetGlobalTexture("_AeroFinishDay", savedDay);
                Shader.SetGlobalTexture("_AeroPlanarTexture", savedPlanar != null ? savedPlanar : Texture2D.blackTexture);
            }
        }
    }
}
