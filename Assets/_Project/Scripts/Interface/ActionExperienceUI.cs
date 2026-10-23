using System.Collections.Generic;
using TMPro;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Upgrades;
using UnityEngine;

namespace TowerOfBabel.Interface
{
    /// <summary>Pooled world-space bubbles collect into the local HUD after server completion receipts.</summary>
    [DisallowMultipleComponent]
    public sealed class ActionExperienceUI : MonoBehaviour
    {
        [Header("Reusable visual components")]
        [SerializeField] private RectTransform effectsLayer;
        [SerializeField] private RectTransform bar;
        [SerializeField] private CanvasGroup barVisibility;
        [SerializeField] private RectTransform track;
        [SerializeField] private UnityEngine.UI.Image fill;
        [SerializeField] private UnityEngine.UI.Image shadowFill, fillShine;
        [SerializeField] private UnityEngine.UI.Image arrivalGlow;
        [SerializeField] private TMP_Text professionText, experienceText, actionText;
        [SerializeField] private UnityEngine.UI.Image actionIcon;
        [SerializeField] private Sprite gatherIcon, processIcon, buildIcon;
        [SerializeField] private MeshRenderer worldBubblePrefab;
        [SerializeField] private UnityEngine.UI.Image popPrefab;
        [SerializeField] private TMP_Text floatingTextPrefab;
        [Header("Game feel")]
        [SerializeField, Range(3, 175)] private int bubbleCount = 35;
        [SerializeField, Range(1, 10)] private int levelUpBubbleMultiplier = 10;
        [SerializeField, Min(.15f)] private float floatDuration = .72f;
        [SerializeField, Min(.15f)] private float flightDuration = .56f;
        [SerializeField, Min(1)] private float visibleDuration = 5f;
        [SerializeField, Range(0, 8)] private float shakeStrength = 3.5f;
        [SerializeField] private bool playSounds = true;
        [SerializeField] private AudioSource audioSource;

        private sealed class Bubble
        {
            public MeshRenderer Renderer;
            public Transform Transform;
            public Vector3 Start, Drift, Weave;
            public float Delay, Size, Phase, Frequency, FloatTime, FlightTime;
            public bool Arrived;
        }
        private sealed class Pop
        {
            public UnityEngine.UI.Image Image;
            public RectTransform Rect;
            public float Age = 1, Size;
        }
        private sealed class Caption
        {
            public TMP_Text Text;
            public Vector2 Origin;
            public float Age = 2;
        }

        private readonly Queue<ActionExperienceReward> pending = new();
        private Bubble[] bubbles;
        private Pop[] pops;
        private Caption[] captions;
        private NetworkUpgradeService service;
        private ActionExperienceReward activeReward;
        private UpgradeJob displayedJob;
        private Canvas canvas;
        private Camera worldCamera;
        private GameObject worldPool;
        private MaterialPropertyBlock bubbleProperties;
        private System.Random random = new();
        private AudioClip popSound;
        private bool initialized, animating;
        private int arrived, lastLevel, popIndex, captionIndex, activeBubbleCount;
        private float age, hold, impact, displayedTotal, targetTotal, animationSpeed, nextPopSoundAge;
        private Vector2 barRest;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized) return;
            if (effectsLayer == null || bar == null || worldBubblePrefab == null || popPrefab == null ||
                floatingTextPrefab == null || track == null || fill == null || shadowFill == null ||
                barVisibility == null || arrivalGlow == null || fillShine == null || professionText == null ||
                experienceText == null || actionText == null || actionIcon == null)
            {
                Debug.LogError("Action experience HUD needs its prefab references.", this);
                enabled = false;
                return;
            }
            initialized = true;
            canvas = GetComponentInParent<Canvas>();
            barRest = bar.anchoredPosition;
            // Allocate once for the larger celebration, then reuse a subset for ordinary gains.
            worldPool = new GameObject("Action XP · world bubbles");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(worldPool, gameObject.scene);
            bubbleProperties = new MaterialPropertyBlock();
            bubbles = new Bubble[Mathf.Clamp(bubbleCount, 3, 175) * Mathf.Clamp(levelUpBubbleMultiplier, 1, 10)];
            for (int i = 0; i < bubbles.Length; i++)
            {
                var renderer = Instantiate(worldBubblePrefab, worldPool.transform);
                renderer.gameObject.SetActive(false);
                bubbles[i] = new Bubble { Renderer = renderer, Transform = renderer.transform };
            }
            pops = new Pop[12];
            for (int i = 0; i < pops.Length; i++)
            {
                var image = Instantiate(popPrefab, effectsLayer);
                image.gameObject.SetActive(false);
                pops[i] = new Pop { Image = image, Rect = image.rectTransform };
            }
            captions = new Caption[6];
            for (int i = 0; i < captions.Length; i++)
            {
                var text = Instantiate(floatingTextPrefab, effectsLayer);
                text.gameObject.SetActive(false);
                captions[i] = new Caption { Text = text };
            }
            barVisibility.alpha = 0;
            arrivalGlow.color = new Color(.7f, 1, 1, 0);
            if (Application.isPlaying && playSounds && audioSource != null)
                CreatePopSound();
        }

        private void Update()
        {
            if (!initialized) return;
            if (service != NetworkUpgradeService.Instance)
            {
                Unsubscribe();
                service = NetworkUpgradeService.Instance;
                if (service != null)
                {
                    service.LocalActionReward += ReceiveReward;
                    service.LocalProgressChanged += ProgressChanged;
                    service.LocalSessionStopped += ClearFeedback;
                }
            }
        }

        private void LateUpdate() { if (initialized) Tick(Time.unscaledDeltaTime); }

        private void OnDisable()
        {
            Unsubscribe();
            if (initialized) ClearFeedback();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (popSound != null) Destroy(popSound);
            if (worldPool != null)
            {
                if (Application.isPlaying) Destroy(worldPool); else DestroyImmediate(worldPool);
            }
        }

        private void Unsubscribe()
        {
            if (service != null)
            {
                service.LocalActionReward -= ReceiveReward;
                service.LocalProgressChanged -= ProgressChanged;
                service.LocalSessionStopped -= ClearFeedback;
            }
            service = null;
        }

        private void ProgressChanged(UpgradeJob job, UpgradeJobSnapshot snapshot)
        {
            if (service == null || !service.IsClientStarted)
            {
                ClearFeedback();
                return;
            }
            if (animating || pending.Count > 0 || job != displayedJob) return;
            displayedTotal = targetTotal = ActionExperienceReward.Total(snapshot.Level, snapshot.Experience);
            lastLevel = snapshot.Level;
            UpdateBar(0);
        }

        private void ReceiveReward(ActionExperienceReward reward)
        {
            // Keep particle counts and receipt backlog bounded during very rapid completions.
            if (pending.Count >= 24) pending.Clear();
            pending.Enqueue(reward);
            if (!animating) BeginNext();
        }

        private void BeginNext()
        {
            activeReward = pending.Dequeue();
            bool levelsUp = activeReward.Level > activeReward.PreviousLevel;
            activeBubbleCount = Mathf.Min(bubbles.Length,
                Mathf.Clamp(bubbleCount, 3, 175) * (levelsUp ? Mathf.Clamp(levelUpBubbleMultiplier, 1, 10) : 1));
            displayedJob = activeReward.Job;
            displayedTotal = targetTotal = activeReward.PreviousTotal;
            lastLevel = activeReward.PreviousLevel;
            age = 0; arrived = 0; nextPopSoundAge = 0; animating = true;
            animationSpeed = pending.Count > 2 ? 2.5f : 1;
            hold = visibleDuration;
            actionText.text = activeReward.Action;
            actionText.richText = false;
            actionIcon.sprite = displayedJob == UpgradeJob.Gather ? gatherIcon :
                displayedJob == UpgradeJob.Process ? processIcon : buildIcon;
            UpdateBar(0);
            shadowFill.fillAmount = fill.fillAmount;
            Vector2 start = ProjectOrigin(activeReward.WorldPosition);
            Vector3 origin = activeReward.WorldPosition;
            float RandomRange(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
            for (int i = 0; i < bubbles.Length; i++)
            {
                Bubble b = bubbles[i];
                b.Renderer.gameObject.SetActive(false);
                if (i >= activeBubbleCount) { b.Arrived = true; continue; }
                float spread = levelsUp ? 2.5f : 1;
                float a = RandomRange(0, Mathf.PI * 2);
                float radius = RandomRange(.04f, .22f) * spread;
                b.Start = origin + new Vector3(Mathf.Cos(a) * radius, RandomRange(-.12f,.14f), Mathf.Sin(a) * radius);
                b.Drift = new Vector3(Mathf.Cos(a) * RandomRange(.3f,.85f), RandomRange(.45f,1.1f), Mathf.Sin(a) * RandomRange(.3f,.85f)) * spread;
                b.Weave = new Vector3(RandomRange(.04f,.16f), RandomRange(.025f,.08f), RandomRange(.04f,.16f));
                b.Phase = RandomRange(0,Mathf.PI * 2); b.Frequency = RandomRange(4,8);
                b.FloatTime = floatDuration * RandomRange(.85f,1.15f);
                b.FlightTime = flightDuration * RandomRange(.85f,1.2f);
                b.Delay = RandomRange(0,.33f);
                b.Size = .18f * RandomRange(.8f,1.2f);
                b.Arrived = false;
                b.Transform.position = b.Start;
                b.Transform.localScale = Vector3.one * b.Size;
            }
            CaptionAt(activeReward.Action, start + new Vector2(0, 40), false);
        }

        private Vector2 ProjectOrigin(Vector3 position)
        {
            if (worldCamera == null) worldCamera = Camera.main;
            Vector3 screen = worldCamera != null ? worldCamera.WorldToScreenPoint(position)
                : new Vector3(Screen.width * .5f, Screen.height * .5f, 1);
            if (screen.z <= 0) screen = new Vector3(Screen.width * .5f, Screen.height * .5f, 1);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(effectsLayer, screen,
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null,
                out Vector2 local);
            Rect r = effectsLayer.rect;
            return new Vector2(Mathf.Clamp(local.x, r.xMin + 140, r.xMax - 140),
                Mathf.Clamp(local.y, r.yMin + 350, r.yMax - 180));
        }

        private Vector2 Destination()
        {
            Vector3 point = track.TransformPoint(new Vector3(
                Mathf.Lerp(track.rect.xMin, track.rect.xMax, fill.fillAmount), track.rect.center.y, 0));
            return effectsLayer.InverseTransformPoint(point);
        }

        private Vector3 WorldDestination(out float depth)
        {
            Vector3 uiPoint = effectsLayer.TransformPoint(Destination());
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, uiPoint);
            depth = Mathf.Max(worldCamera.nearClipPlane + .12f,.35f);
            return worldCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,depth));
        }

        private static Vector3 FloatPosition(Bubble b,float f)
        {
            Vector3 wave = new Vector3(Mathf.Sin(f*b.Frequency+b.Phase),Mathf.Sin(f*b.Frequency*.7f+b.Phase),
                Mathf.Cos(f*b.Frequency*.85f+b.Phase));
            return b.Start + b.Drift*f + Vector3.Scale(wave,b.Weave)*f;
        }

        private void Tick(float dt)
        {
            if (!animating && hold <= 0 && barVisibility.alpha <= 0) return;
            LayoutSafeArea();
            if (animating)
            {
                age += dt * animationSpeed;
                if (worldCamera == null) worldCamera = Camera.main;
                for (int i = 0; i < activeBubbleCount; i++)
                {
                    Bubble b = bubbles[i];
                    if (b.Arrived) continue;
                    float t = age - b.Delay;
                    if (t < 0) continue;
                    // A destroyed camera cannot leave the local reward queue stuck forever.
                    if (worldCamera == null) { Arrive(b); continue; }
                    b.Renderer.gameObject.SetActive(true);
                    float alpha = Mathf.Clamp01(t*10);
                    if (t < b.FloatTime)
                    {
                        float f = t / b.FloatTime;
                        b.Transform.position = FloatPosition(b,f);
                        b.Transform.localScale = Vector3.one * b.Size * Mathf.Lerp(.25f,1,Mathf.SmoothStep(0,1,f*3));
                    }
                    else
                    {
                        float f = Mathf.Clamp01((t - b.FloatTime) / b.FlightTime);
                        float ease = f * f * f;
                        Vector3 from = FloatPosition(b,1);
                        Vector3 end = WorldDestination(out float endDepth);
                        Vector3 bend = from + Vector3.up * (.45f+b.Weave.y*3) + worldCamera.transform.right * (Mathf.Sin(b.Phase)*.55f);
                        b.Transform.position = Vector3.Lerp(Vector3.Lerp(from,bend,ease),Vector3.Lerp(bend,end,ease),ease);
                        float sourceDepth = Mathf.Max(endDepth,Vector3.Dot(from-worldCamera.transform.position,worldCamera.transform.forward));
                        float currentDepth = Mathf.Max(endDepth,Vector3.Dot(b.Transform.position-worldCamera.transform.position,worldCamera.transform.forward));
                        float perspectiveScale = worldCamera.orthographic ? 1 : currentDepth/sourceDepth;
                        b.Transform.localScale = Vector3.one * b.Size * perspectiveScale * Mathf.Lerp(1,.24f,f);
                        if (f >= 1) Arrive(b);
                    }
                    bubbleProperties.SetFloat("_Opacity",alpha);
                    bubbleProperties.SetFloat("_Phase",b.Phase+t*.5f);
                    b.Renderer.SetPropertyBlock(bubbleProperties);
                }
                if (arrived == activeBubbleCount && Mathf.Abs(displayedTotal - targetTotal) < .005f)
                {
                    displayedTotal = targetTotal;
                    animating = false;
                    if (pending.Count > 0) BeginNext();
                }
            }
            UpdateBar(dt);
            impact = Mathf.MoveTowards(impact, 0, dt * 3.2f);
            float wave = Mathf.Sin(age * 75);
            bar.anchoredPosition = barRest + new Vector2(wave, Mathf.Cos(age * 91) * .55f) * (shakeStrength * impact * impact);
            bar.localScale = Vector3.one * (1 + impact * .018f);
            experienceText.rectTransform.localScale = Vector3.one * (1 + impact * .10f);
            arrivalGlow.color = new Color(.67f, 1, .88f, impact * .62f);
            if (!animating) { hold -= dt; age += dt; }
            barVisibility.alpha = Mathf.MoveTowards(barVisibility.alpha, hold > 0 || animating ? 1 : 0, dt * 5);
            for (int i = 0; i < pops.Length; i++)
            {
                Pop p = pops[i];
                if (p.Age >= .42f) continue;
                p.Age += dt;
                float t = Mathf.Clamp01(p.Age / .42f);
                p.Rect.sizeDelta = Vector2.one * Mathf.Lerp(p.Size * .3f, p.Size, 1 - Mathf.Pow(1 - t, 3));
                p.Image.color = new Color(1, 1, 1, (1 - t) * (1 - t));
                if (t >= 1) p.Image.gameObject.SetActive(false);
            }
            foreach (Caption c in captions)
            {
                if (c.Age >= 1.8f) continue;
                c.Age += dt;
                float t = Mathf.Clamp01(c.Age / 1.8f);
                c.Text.rectTransform.anchoredPosition = c.Origin + Vector2.up * (52 * t);
                c.Text.rectTransform.localScale = Vector3.one * (1 + Mathf.Sin(Mathf.Min(1, t * 5) * Mathf.PI) * .06f);
                c.Text.alpha = Mathf.Clamp01((1 - t) * 4) * Mathf.Clamp01(t * 14);
                if (t >= 1) c.Text.gameObject.SetActive(false);
            }
        }

        private void Arrive(Bubble bubble)
        {
            bubble.Arrived = true;
            bubble.Renderer.gameObject.SetActive(false);
            arrived++;
            targetTotal = Mathf.Lerp(activeReward.PreviousTotal, activeReward.TotalExperience, (float)arrived / activeBubbleCount);
            impact = Mathf.Min(1, impact + .6f);
            PopAt(Destination(), 56);
            hold = visibleDuration;
            if (playSounds && Application.isPlaying && audioSource != null && popSound != null &&
                (age >= nextPopSoundAge || arrived == activeBubbleCount))
            {
                nextPopSoundAge = age + .045f;
                audioSource.pitch = Mathf.Lerp(.975f, 1.425f, (float)arrived / activeBubbleCount);
                audioSource.PlayOneShot(popSound, .1f);
            }
            if (arrived == activeBubbleCount)
                CaptionAt(activeReward.Amount > 0 ? $"+{activeReward.Amount} XP  ·  {activeReward.Action}" :
                    $"MAX LEVEL  ·  {activeReward.Action}",
                    effectsLayer.InverseTransformPoint(bar.TransformPoint(new Vector3(bar.rect.center.x, bar.rect.yMax + 28))), false, true);
        }

        private void UpdateBar(float dt)
        {
            displayedTotal = dt > 0 ? Mathf.Lerp(displayedTotal, targetTotal, 1 - Mathf.Exp(-dt * 17)) : displayedTotal;
            if (Mathf.Abs(displayedTotal - targetTotal) < .005f) displayedTotal = targetTotal;
            // Floor the threshold calculation so a new level appears only after its XP has arrived.
            int level = 0;
            while (level < UpgradeTreeConfig.MaxLevel && displayedTotal >= ActionExperienceReward.Total(level + 1, 0)) level++;
            float xp = displayedTotal - ActionExperienceReward.Total(level, 0);
            int required = UpgradeJobProgress.GetExperienceRequired(level);
            float normalized = required > 0 ? Mathf.Clamp01(xp / required) : 1;
            fill.fillAmount = normalized;
            fillShine.fillAmount = normalized;
            if (level != lastLevel)
            {
                shadowFill.fillAmount = 0;
                lastLevel = level;
                if (animating)
                {
                    impact = 1;
                    PopAt(effectsLayer.InverseTransformPoint(bar.TransformPoint(bar.rect.center)), 210);
                    CaptionAt($"LEVEL {level}  ·  +1 upgrade point", effectsLayer.InverseTransformPoint(bar.TransformPoint(new Vector3(bar.rect.center.x, bar.rect.yMax + 76))), true, true);
                }
            }
            // The cyan ghost shows incoming XP ahead of the green fill, then eases into it.
            // A dim band under the green fill remains visible along the track's lower edge.
            int targetLevel = level;
            while (targetLevel < UpgradeTreeConfig.MaxLevel && targetTotal >= ActionExperienceReward.Total(targetLevel + 1, 0)) targetLevel++;
            int targetRequired = UpgradeJobProgress.GetExperienceRequired(targetLevel);
            float ghost = targetLevel > level ? 1 : targetRequired > 0
                ? (targetTotal - ActionExperienceReward.Total(level, 0)) / targetRequired : 1;
            shadowFill.fillAmount = Mathf.Max(normalized, ghost);
            string job = displayedJob == UpgradeJob.Gather ? "Gather" : displayedJob == UpgradeJob.Process ? "Processor" : "Builder";
            professionText.text = $"{job}   ·   Level {level}";
            experienceText.text = required > 0 ? $"{Mathf.Min(required - 1, Mathf.FloorToInt(xp + .001f))} / {required} XP" : "MAX LEVEL";
        }

        private void PopAt(Vector2 position, float size)
        {
            Pop p = pops[popIndex++ % pops.Length];
            p.Age = 0; p.Size = size;
            p.Rect.anchoredPosition = position;
            p.Image.gameObject.SetActive(true);
        }

        private void CaptionAt(string text, Vector2 origin, bool levelUp, bool aboveBar = false)
        {
            Caption c = captions[captionIndex++ % captions.Length];
            float width = Mathf.Min(aboveBar ? bar.rect.width : 760, effectsLayer.rect.width - 32);
            c.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            origin.x = Mathf.Clamp(origin.x, effectsLayer.rect.xMin + width * .5f + 16,
                effectsLayer.rect.xMax - width * .5f - 16);
            c.Age = 0; c.Origin = origin;
            c.Text.text = text;
            c.Text.richText = false;
            c.Text.color = levelUp ? new Color(.81f, 1, .62f) : Color.white;
            c.Text.alpha = 0;
            c.Text.gameObject.SetActive(true);
        }

        private void LayoutSafeArea()
        {
            if (canvas == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            // Recompute width as well as insets: the CanvasScaler can resize without changing safeArea.
            var root = (RectTransform)transform;
            root.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            root.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            root.offsetMin = root.offsetMax = Vector2.zero;
            bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Clamp(root.rect.width - 48, 240, 560));
        }

        private void ClearFeedback()
        {
            pending.Clear(); animating = false; impact = hold = 0;
            // Scene unload can destroy presentation children before this HUD is disabled.
            if (barVisibility != null) barVisibility.alpha = 0;
            if (bar != null) { bar.anchoredPosition = barRest; bar.localScale = Vector3.one; }
            if (experienceText != null) experienceText.rectTransform.localScale = Vector3.one;
            if (arrivalGlow != null) arrivalGlow.color = Color.clear;
            foreach (Bubble b in bubbles) if (b.Renderer != null) b.Renderer.gameObject.SetActive(false);
            foreach (Pop p in pops) { p.Age = 1; if (p.Image != null) p.Image.gameObject.SetActive(false); }
            foreach (Caption c in captions) { c.Age = 2; if (c.Text != null) c.Text.gameObject.SetActive(false); }
            audioSource?.Stop();
        }

        private void CreatePopSound()
        {
            const int rate = 22050;
            var samples = new float[2646];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Sin(Mathf.PI * i / samples.Length) * Mathf.Exp(-t * 36);
                samples[i] = Mathf.Sin(2 * Mathf.PI * (780 * t - 1600 * t * t)) * envelope * .45f;
            }
            popSound = AudioClip.Create("AeroBubblePop", samples.Length, 1, rate, false);
            popSound.SetData(samples, 0);
        }

#if UNITY_EDITOR
        /// <summary>Sample this temporary prefab's visual animation in Edit Mode, without networking or Play Mode.</summary>
        public void SampleEditorPreview(ActionExperienceReward reward, float seconds, Camera previewCamera = null)
        {
            if (Application.isPlaying) return;
            Initialize();
            if (!initialized) return;
            if (previewCamera != null) worldCamera = previewCamera;
            random = new System.Random(8127);
            ClearFeedback();
            ReceiveReward(reward);
            for (float t = 0; t < seconds; t += 1f / 60) Tick(Mathf.Min(1f / 60, seconds - t));
            Canvas.ForceUpdateCanvases();
        }
#endif
    }
}
