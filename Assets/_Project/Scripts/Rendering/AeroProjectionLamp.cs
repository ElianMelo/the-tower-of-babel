using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>Real URP spotlight cookie, refreshed at 8 Hz. Only four nearby lamps allocate cookies.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class AeroProjectionLamp : MonoBehaviour
    {
        public const int MaximumActiveLamps = 4;
        public const int CookieResolution = 128;
        public const float CookieInterval = .125f;
        private static readonly List<AeroProjectionLamp> lamps = new();
        private static readonly AeroProjectionLamp[] nearest = new AeroProjectionLamp[MaximumActiveLamps];
        private static readonly float[] distances = new float[MaximumActiveLamps];
        private static double nextSelection;
        public Light projection;
        public Material cookieMaterial;
        public Renderer glowingCore;
        [ColorUsage(false, true)] public Color glow = new(.1f, 3.5f, 5.5f);
        [Range(0, 6.28f)] public float phase;
        private RenderTexture cookie;
        private Material animationMaterial;
        private MaterialPropertyBlock block;
        private bool selected;
        private float nextCookie;
        public RenderTexture Cookie => cookie;
        public bool IsBudgetActive => selected;

        private void OnEnable() { if (!lamps.Contains(this)) lamps.Add(this); nextSelection = 0; }
        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextSelection)
            {
                var camera = Camera.main;
                RefreshBudget(camera != null ? camera.transform.position : transform.position);
                nextSelection = now + .25;
            }
            if (!selected) return;
            if (Application.isPlaying && Time.time >= nextCookie)
            {
                Sample(Time.time); nextCookie = Time.time + CookieInterval;
            }
        }

        public static void RefreshBudget(Vector3 view)
        {
            for (int i=0;i<MaximumActiveLamps;i++) {nearest[i]=null; distances[i]=float.PositiveInfinity;}
            foreach (var lamp in lamps)
            {
                if (lamp == null || !lamp.isActiveAndEnabled || lamp.projection == null || lamp.cookieMaterial == null) continue;
                float distance = (lamp.transform.position-view).sqrMagnitude;
                if (distance > 42*42) continue;
                for (int i=0;i<MaximumActiveLamps;i++)
                {
                    if (distance >= distances[i]) continue;
                    for (int j=MaximumActiveLamps-1;j>i;j--) {nearest[j]=nearest[j-1]; distances[j]=distances[j-1];}
                    nearest[i]=lamp; distances[i]=distance; break;
                }
            }
            foreach (var lamp in lamps)
            {
                if (lamp == null) continue;
                bool use=false; for (int i=0;i<MaximumActiveLamps;i++) if(nearest[i]==lamp) use=true;
                lamp.SetSelected(use);
                if(lamp.projection!=null) lamp.projection.shadows=use && nearest[0]==lamp ? LightShadows.Hard : LightShadows.None;
            }
        }

        private void SetSelected(bool active)
        {
            selected=active;
            if (projection != null) projection.enabled=active;
            if (active && cookie==null) Sample(Application.isPlaying ? Time.time : 0);
            if (!active) ReleaseCookie();
        }

        /// <summary>Explicit pose for deterministic Edit Mode previews; runtime calls it only for selected lights.</summary>
        public void Sample(float seconds)
        {
            if(!isActiveAndEnabled || projection==null || cookieMaterial==null || (Application.isPlaying && !selected)) return;
            if(cookie==null)
            {
                cookie=new RenderTexture(CookieResolution,CookieResolution,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
                {name="Aero animated water cookie",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
                cookie.Create();
                animationMaterial=new Material(cookieMaterial) {hideFlags=HideFlags.HideAndDontSave};
            }
            var old=RenderTexture.active;
            animationMaterial.SetFloat("_AeroCookieClock",seconds+phase*3);
            try {Graphics.Blit(null,cookie,animationMaterial,0); cookie.IncrementUpdateCount();}
            finally {RenderTexture.active=old;}
            projection.cookie=cookie;
            if(glowingCore!=null)
            {
                block ??= new MaterialPropertyBlock(); glowingCore.GetPropertyBlock(block);
                block.SetColor("_EmissionColor",glow*(1+.1f*Mathf.Sin(seconds*.9f+phase)));
                glowingCore.SetPropertyBlock(block);
            }
        }

        private void ReleaseCookie()
        {
            if(projection!=null) projection.cookie=null;
            if(cookie!=null) {cookie.Release(); DestroyOwned(cookie); cookie=null;}
            if(animationMaterial!=null) {DestroyOwned(animationMaterial); animationMaterial=null;}
        }
        private static void DestroyOwned(Object value) {if(Application.isPlaying) Destroy(value); else DestroyImmediate(value);}
        private void OnDisable()
        {
            lamps.Remove(this); nextSelection=0; selected=false;
            if(projection!=null) projection.enabled=false;
            ReleaseCookie();
        }
    }
}
