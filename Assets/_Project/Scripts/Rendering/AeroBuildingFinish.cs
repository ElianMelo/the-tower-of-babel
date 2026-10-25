using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace TowerOfBabel.Rendering
{
    /// <summary>One reusable, opaque-only mirrored view. Captures outside the main SRP rendering loop.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class AeroBuildingFinish : MonoBehaviour
    {
        public AeroBuildingFinishProfile profile;
        public bool liveReflections = true;
        public static bool IsCapturing { get; private set; }
        public int ReflectionFloor { get; private set; } = -1;
        public int LastSubmittedInstances { get; private set; }
        public int CaptureCount { get; private set; }
        public string LastStatus { get; private set; } = "Environment only";
        public RenderTexture ReflectionTexture => target;
        public float ReflectionWeight => blend;
        private Camera reflection, owner;
        private RenderTexture target;
        private readonly Plane[] planes = new Plane[6];
        private float height, blend;
        private Matrix4x4 textureMatrix;
        private bool valid, failed;
        private static readonly int Planar = Shader.PropertyToID("_AeroPlanarParams");
        private static readonly List<AeroBuildingFinish> Sources = new();

        private void OnEnable()
        {
            failed = false;
            if (Sources.Contains(this)) return;
            if (Sources.Count == 0) RenderPipelineManager.beginCameraRendering += BeginCamera;
            Sources.Add(this);
        }
        private void OnDisable()
        {
            Sources.Remove(this);
            if (Sources.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering -= BeginCamera;
                Shader.SetGlobalFloat("_AeroFinishEnabled", 0);
                Shader.SetGlobalFloat("_AeroFinishCapturing", 0);
            }
            Release();
        }

        private void LateUpdate()
        {
            if (Application.isPlaying && !IsCapturing) RenderForCamera(Camera.main, Time.unscaledDeltaTime);
        }

        private static void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            // A camera in another scene must never inherit a previous view's texture or settings.
            Shader.SetGlobalVector(Planar, Vector4.zero);
            Shader.SetGlobalFloat("_AeroFinishEnabled", 0);
            var scene = camera.cameraType == CameraType.SceneView ? SceneManager.GetActiveScene() :
                camera.scene.IsValid() ? camera.scene : camera.gameObject.scene;
            foreach (var source in Sources)
                if (source != null && source.isActiveAndEnabled && source.profile != null && source.gameObject.scene == scene)
                { source.profile.Apply(); source.Publish(camera); return; }
        }

        private void Publish(Camera camera)
        {
            bool use = !IsCapturing && valid && owner != null && camera == owner && liveReflections;
            Shader.SetGlobalVector(Planar, new Vector4(use ? blend : 0, height, profile != null ? profile.range : 32, 0));
            if (use)
            {
                Shader.SetGlobalTexture("_AeroPlanarTexture", target);
                Shader.SetGlobalMatrix("_AeroPlanarMatrix", textureMatrix);
            }
        }

        /// <summary>Also used by disposable Edit Mode authoring previews. Does not enter Play Mode.</summary>
        public void RenderForCamera(Camera camera, float deltaTime)
        {
            LastSubmittedInstances = 0;
            if (IsCapturing) return;
            if (profile == null || camera == null) { LastStatus = "No camera or finish profile"; Release(); return; }
            profile.Apply();
            valid = false;
            if (!liveReflections || failed || !profile.SupportsLive || camera.stereoEnabled || profile.reflectionRendererIndex < 0)
            { LastStatus = "Environment only"; Release(); return; }
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            if (!AeroBuildingReflectionRegistry.FindDeck(camera, planes, profile.range, out int floor, out float planeHeight, out float pitch))
            { LastStatus = "No visible completed deck"; ReflectionFloor = -1; blend = 0; Publish(camera); return; }
            if (camera != owner || floor != ReflectionFloor || Mathf.Abs(planeHeight - height) > .01f)
            { blend = 0; ReflectionFloor = floor; height = planeHeight; }
            owner = camera;
            EnsureResources(camera);
            reflection.CopyFrom(camera);
            reflection.enabled = false;
            reflection.cameraType = CameraType.Reflection;
            reflection.targetTexture = target;
            reflection.scene = camera.scene.IsValid() ? camera.scene : camera.gameObject.scene;
            reflection.allowMSAA = false;
            reflection.useOcclusionCulling = false;
            reflection.farClipPlane = Mathf.Max(camera.nearClipPlane + .1f, profile.range);
            reflection.depthTextureMode = DepthTextureMode.None;
            var data = reflection.GetUniversalAdditionalCameraData();
            data.SetRenderer(profile.reflectionRendererIndex);
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.volumeLayerMask = 0;
            Matrix4x4 mirror = Matrix4x4.identity;
            mirror.m11 = -1; mirror.m13 = 2 * height;
            reflection.transform.SetPositionAndRotation(mirror.MultiplyPoint(camera.transform.position),
                Quaternion.LookRotation(mirror.MultiplyVector(camera.transform.forward), -mirror.MultiplyVector(camera.transform.up)));
            reflection.worldToCameraMatrix = camera.worldToCameraMatrix * mirror;
            // Keep the reflected view above the deck, including objects touching the floor.
            Vector3 point = reflection.worldToCameraMatrix.MultiplyPoint(new Vector3(0, height + .005f, 0));
            Vector3 normal = reflection.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
            // Rebuild the far plane before applying the oblique near plane.
            reflection.ResetProjectionMatrix();
            reflection.projectionMatrix = reflection.CalculateObliqueMatrix(new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal)));
            textureMatrix = GL.GetGPUProjectionMatrix(reflection.projectionMatrix, true) * reflection.worldToCameraMatrix;
            GeometryUtility.CalculateFrustumPlanes(reflection, planes);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(reflection, request))
            { LastStatus = "Render requests unsupported; environment fallback"; Release(); return; }
            bool invert = GL.invertCulling;
            try
            {
                IsCapturing = true;
                GL.invertCulling = !invert;
                Shader.SetGlobalVector(Planar, Vector4.zero);
                Shader.SetGlobalFloat("_AeroFinishCapturing", 1);
                Shader.SetGlobalVector("_AeroReflectionVolume", new Vector4(height - .02f, height + pitch + .25f, profile.range, 0));
                Shader.SetGlobalVector("_AeroReflectionOrigin", camera.transform.position);
                LastSubmittedInstances = AeroBuildingReflectionRegistry.Submit(reflection, camera, planes, floor, profile.range);
                RenderPipeline.SubmitRenderRequest(reflection, request);
                target.GenerateMips();
                blend = Mathf.MoveTowards(blend, 1, Mathf.Max(0, deltaTime) / Mathf.Max(.05f, profile.transitionSeconds));
                valid = true; CaptureCount++; LastStatus = "Live bounded reflection";
            }
            catch (System.Exception exception)
            {
                failed = true; LastStatus = "Capture failed; environment fallback";
                Debug.LogException(exception, this);
            }
            finally
            {
                GL.invertCulling = invert;
                IsCapturing = false;
                Shader.SetGlobalFloat("_AeroFinishCapturing", 0);
                Publish(camera);
            }
        }

        private void EnsureResources(Camera source)
        {
            if (reflection == null)
            {
                var go = new GameObject("Aero bounded floor reflection", typeof(Camera), typeof(UniversalAdditionalCameraData));
                go.hideFlags = HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                reflection = go.GetComponent<Camera>(); reflection.enabled = false;
            }
            float scale = Mathf.Min(Mathf.Clamp(profile.resolutionScale, .25f, .5f),
                Mathf.Clamp(profile.maximumResolution, 128, 1024) / (float)Mathf.Max(source.pixelWidth, source.pixelHeight));
            int width = Mathf.Max(16, Mathf.RoundToInt(source.pixelWidth * scale));
            int size = Mathf.Max(16, Mathf.RoundToInt(source.pixelHeight * scale));
            if (target != null && target.width == width && target.height == size) return;
            if (target != null)
            {
                Shader.SetGlobalTexture("_AeroPlanarTexture", Texture2D.blackTexture);
                target.Release(); CoreUtils.Destroy(target);
            }
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            target = new RenderTexture(width, size, 24, format)
            {
                name = "Aero shared planar reflection", hideFlags = HideFlags.HideAndDontSave,
                useMipMap = true, autoGenerateMips = false, filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp, antiAliasing = 1
            };
            target.Create();
        }

        private void Release()
        {
            valid = false; blend = 0; ReflectionFloor = -1;
            Shader.SetGlobalVector(Planar, Vector4.zero);
            Shader.SetGlobalTexture("_AeroPlanarTexture", Texture2D.blackTexture);
            if (reflection != null) CoreUtils.Destroy(reflection.gameObject);
            if (target != null) { target.Release(); CoreUtils.Destroy(target); }
            reflection = null; target = null; owner = null;
        }
    }
}
