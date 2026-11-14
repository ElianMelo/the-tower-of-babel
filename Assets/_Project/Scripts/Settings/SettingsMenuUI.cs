using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using TowerOfBabel.Cheats;
using TowerOfBabel.Interface;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TowerOfBabel.Settings
{
    /// <summary>
    /// The game settings, opened and closed with Escape: Video, Graphics, Audio, Gameplay and Controls tabs.
    /// Built in code with the same glass panels, fonts and button prefab as the developer controls. Every change
    /// applies at once and is saved; Escape pressed while another window is open only closes that window.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-10000)]
    public sealed class SettingsMenuUI : MonoBehaviour
    {
        public static SettingsMenuUI Instance { get; private set; }
        public static readonly string[] Tabs = { "Video", "Graphics", "Audio", "Gameplay", "Controls" };
        public static readonly string[] QualityNames = { "Low", "Medium", "High" };

        [SerializeField] private TMP_FontAsset regularFont, boldFont;
        [SerializeField] private Material glassMaterial;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private InputActionAsset inputActions;

        private GameObject visuals;
        private RectTransform content;
        private ScrollRect scroll;
        private TMP_Text status;
        private string tab = Tabs[1];
        private readonly Dictionary<string, UnityEngine.UI.Outline> tabOutlines = new();
        private readonly Dictionary<string, UnityEngine.UI.Button> tabButtons = new();
        private readonly List<GameObject> rows = new();
        private readonly Dictionary<string, UnityEngine.UI.Slider> sliders = new();
        private readonly Dictionary<string, UnityEngine.UI.Toggle> toggles = new();
        private readonly Dictionary<string, (UnityEngine.UI.Button previous, UnityEngine.UI.Button next, TMP_Text value)> choices = new();
        private readonly Dictionary<string, UnityEngine.UI.Button> bindingButtons = new();
        private readonly Dictionary<string, UnityEngine.UI.Button> actionButtons = new();
        private readonly List<Action> refreshers = new();
        // Buttons that belong to rows of the current tab (the panel's own buttons stay).
        private readonly List<string> rowActions = new();
        private PlayerControlStateMachine controls;
        private CursorStateConfig? previousCursor;
        private AeroDock dock;
        private InputActionRebindingExtensions.RebindingOperation rebinding;
        private int ignoreEscapeFrame = -1;
        private float saveAt = -1;
        private Vector2Int[] resolutions = Array.Empty<Vector2Int>();
        private int pendingResolution;
        private DisplayMode pendingDisplayMode;
        private static readonly Color Ink = new(.055f, .20f, .29f, 1);
        private static readonly Color Soft = new(.15f, .35f, .43f, 1);

        public bool IsVisible => visuals != null && visuals.activeSelf;
        public bool IsRebinding => rebinding != null;
        public string CurrentTab => tab;
        public string StatusText => status != null ? status.text : string.Empty;
        public IReadOnlyDictionary<string, UnityEngine.UI.Button> TabButtons => tabButtons;
        public IReadOnlyDictionary<string, UnityEngine.UI.Slider> Sliders => sliders;
        public IReadOnlyDictionary<string, UnityEngine.UI.Toggle> Toggles => toggles;
        public IReadOnlyDictionary<string, UnityEngine.UI.Button> BindingButtons => bindingButtons;
        public IReadOnlyDictionary<string, UnityEngine.UI.Button> ActionButtons => actionButtons;
        public IReadOnlyCollection<string> ChoiceLabels => choices.Keys;
        public string ChoiceText(string label) => choices.TryGetValue(label, out var choice) ? choice.value.text : string.Empty;
        public void StepChoice(string label, int step) { if (choices.TryGetValue(label, out var choice)) (step < 0 ? choice.previous : choice.next).onClick.Invoke(); }

        /// <summary>Fonts, glass and button of the shared interface kit, plus the project's input asset.</summary>
        public void Configure(TMP_FontAsset regular, TMP_FontAsset bold, Material glass, GameObject button, InputActionAsset actions)
        {
            regularFont = regular; boldFont = bold; glassMaterial = glass; buttonPrefab = button;
            if (actions != null) inputActions = actions;
            if (inputActions != null) GameInput.Use(inputActions);
        }

        private void Awake()
        {
            Instance = this;
            if (inputActions != null) GameInput.Use(inputActions);
            if (GetComponent<GameSettingsApplier>() == null) gameObject.AddComponent<GameSettingsApplier>();
            BuildView();
        }

        private void OnDestroy()
        {
            Hide();
            if (Instance == this) Instance = null;
        }

        private void OnDisable() => Hide();

        private void Update()
        {
            if (saveAt >= 0 && Time.unscaledTime >= saveAt) { saveAt = -1; GameSettings.Save(); }
            if (rebinding != null || Time.frameCount <= ignoreEscapeFrame || !GameInput.SettingsPressed) return;
            if (IsVisible) { Hide(); return; }
            // Another window is open: Escape belongs to it (it closes itself later this frame).
            if (OtherWindowOpen()) return;
            Show();
        }

        private bool OtherWindowOpen()
        {
            if (CheatMenuUI.Instance != null && CheatMenuUI.Instance.IsOpen) return true;
            if (InterfaceManager.Instance != null && InterfaceManager.Instance.IsAnyWindowOpen) return true;
            if (dock == null) dock = FindFirstObjectByType<AeroDock>();
            return dock != null && dock.IsExpanded;
        }

        public void Toggle() { if (IsVisible) Hide(); else Show(); }

        public void Show()
        {
            if (IsVisible) return;
            if (visuals == null) BuildView();
            CheatMenuUI.Instance?.Hide();
            InterfaceManager.Instance?.HideInventory(); InterfaceManager.Instance?.HideUpgrade(); InterfaceManager.Instance?.HideEmotes();
            if (dock == null) dock = FindFirstObjectByType<AeroDock>();
            dock?.Close();
            previousCursor = CursorStateConfig.Current;
            CursorStateConfig.UnlockedVisible.Apply();
            if (controls == null) controls = FindFirstObjectByType<PlayerControlStateMachine>();
            controls?.SetModalInputLocked(true);
            visuals.SetActive(true); visuals.transform.SetAsLastSibling();
            AeroUIAudio.Play(AeroUICue.Open);
            RebuildRows();
            SetStatus("Changes apply at once and are saved.");
        }

        public void Hide()
        {
            if (!IsVisible) return;
            CancelRebind();
            visuals.SetActive(false);
            if (saveAt >= 0) { saveAt = -1; GameSettings.Save(); }
            controls?.SetModalInputLocked(false);
            if (previousCursor.HasValue) previousCursor.Value.Apply();
            previousCursor = null;
            AeroUIAudio.Play(AeroUICue.Close);
        }

        public void SelectTab(string name)
        {
            if (Array.IndexOf(Tabs, name) < 0) throw new ArgumentException("Unknown settings tab.", nameof(name));
            CancelRebind();
            tab = name;
            RebuildRows();
        }

        private void SetStatus(string message) { if (status != null) status.text = message; }

        // A dragged slider changes many times a second; write the save once it has settled.
        private void Changed(bool graphics = false)
        {
            GameSettingsData data = GameSettings.Current;
            if (graphics) data.quality = GraphicsPresets.Detect(data, Baseline());
            saveAt = Time.unscaledTime + .4f;
            GameSettings.NotifyChanged();
            Refresh();
        }

        private static GraphicsBaseline Baseline() =>
            GameSettingsApplier.Instance != null ? GameSettingsApplier.Instance.Baseline : GraphicsBaseline.Default;

        private void Refresh() { foreach (Action refresher in refreshers) refresher(); }

        public void BuildView()
        {
            if (visuals != null) return;
            if (!TryGetComponent(out Canvas canvas)) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 950;
            if (!TryGetComponent(out CanvasScaler scaler)) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            var shade = Rect("Settings overlay", transform); Stretch(shade); visuals = shade.gameObject;
            shade.gameObject.AddComponent<Image>().color = new Color(.01f, .06f, .12f, .36f);
            var panel = Panel("Settings", shade); panel.anchorMin = new Vector2(.14f, .08f); panel.anchorMax = new Vector2(.86f, .92f); panel.offsetMin = panel.offsetMax = Vector2.zero;
            var title = Text("Settings", panel, 30, true); Place(title.rectTransform, 26, 12, 400, 42);
            var subtitle = Text("Escape opens and closes this menu", panel, 16); Place(subtitle.rectTransform, 28, 55, 700, 28); subtitle.color = Soft;
            var close = Button(panel, "Close [Esc]", Hide, 140);
            var closeRect = (RectTransform)close.transform; closeRect.anchorMin = closeRect.anchorMax = new Vector2(1, 1); closeRect.pivot = new Vector2(1, 1);
            closeRect.anchoredPosition = new Vector2(-24, -18); closeRect.sizeDelta = new Vector2(140, 42);
            actionButtons["Close"] = close;

            var tabRow = Rect("Tabs", panel); Horizontal(tabRow, 8);
            tabRow.anchorMin = new Vector2(0, 1); tabRow.anchorMax = Vector2.one; tabRow.offsetMin = new Vector2(24, -136); tabRow.offsetMax = new Vector2(-24, -96);
            foreach (string name in Tabs)
            {
                string selected = name;
                var button = Button(tabRow, name, () => SelectTab(selected), 150);
                var element = button.GetComponent<LayoutElement>(); element.minWidth = 90; element.flexibleWidth = 1;
                var outline = button.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = new Color(.5f, 1, .65f); outline.effectDistance = new Vector2(2, -2);
                tabOutlines[name] = outline; tabButtons[name] = button;
            }

            var scrollRoot = Rect("Setting list", panel); Stretch(scrollRoot, 24, 24, 150, 70);
            scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.scrollSensitivity = 38;
            var viewport = Rect("Viewport", scrollRoot); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(.8f, .95f, 1, .15f);
            content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;

            status = Text("", panel, 16); status.color = Soft;
            status.rectTransform.anchorMin = new Vector2(0, 0); status.rectTransform.anchorMax = new Vector2(1, 0); status.rectTransform.pivot = new Vector2(.5f, 0);
            status.rectTransform.offsetMin = new Vector2(28, 18); status.rectTransform.offsetMax = new Vector2(-300, 54);
            var defaults = Button(panel, "Restore defaults", RestoreTabDefaults, 230);
            var defaultsRect = (RectTransform)defaults.transform; defaultsRect.anchorMin = defaultsRect.anchorMax = new Vector2(1, 0); defaultsRect.pivot = new Vector2(1, 0);
            defaultsRect.anchoredPosition = new Vector2(-24, 16); defaultsRect.sizeDelta = new Vector2(230, 42);
            actionButtons["Restore defaults"] = defaults;
            RebuildRows();
            visuals.SetActive(false);
        }

        public void RebuildRows()
        {
            if (content == null) return;
            foreach (GameObject row in rows) { row.SetActive(false); if (Application.isPlaying) Destroy(row); else DestroyImmediate(row); }
            rows.Clear(); sliders.Clear(); toggles.Clear(); choices.Clear(); bindingButtons.Clear(); refreshers.Clear();
            foreach (string label in rowActions) actionButtons.Remove(label);
            rowActions.Clear();
            foreach (var pair in tabOutlines) pair.Value.enabled = pair.Key == tab;
            GameSettingsData data = GameSettings.Current;
            switch (tab)
            {
                case "Video": BuildVideo(data); break;
                case "Graphics": BuildGraphics(data); break;
                case "Audio": BuildAudio(data); break;
                case "Gameplay": BuildGameplay(data); break;
                default: BuildControls(); break;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }

        private void BuildVideo(GameSettingsData data)
        {
            var found = new List<Vector2Int>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (!found.Contains(size)) found.Add(size);
            }
            found.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            resolutions = found.ToArray();
            pendingResolution = Array.IndexOf(resolutions, new Vector2Int(data.resolutionWidth, data.resolutionHeight));
            pendingDisplayMode = data.displayMode;
            ChoiceRow("Resolution", "Size of the picture in a built game", resolutions.Length + 1,
                () => pendingResolution + 1,
                index => { pendingResolution = index - 1; SetStatus("Press Apply to change the display."); },
                index => index == 0 ? $"Current ({Screen.width} x {Screen.height})" : $"{resolutions[index - 1].x} x {resolutions[index - 1].y}");
            ChoiceRow("Display mode", "Exclusive fullscreen, borderless window or window", 3,
                () => (int)pendingDisplayMode,
                index => { pendingDisplayMode = (DisplayMode)index; SetStatus("Press Apply to change the display."); },
                index => ((DisplayMode)index).ToString());
            ActionRow("Apply display", Application.isEditor ? "The Editor's Game view keeps its own size; this applies in a built game" : "Use the selected resolution and display mode",
                "Apply", ApplyVideo);
        }

        /// <summary>Applies and saves the resolution and display mode selected on the Video tab.</summary>
        public void ApplyVideo()
        {
            GameSettingsData data = GameSettings.Current;
            if (pendingResolution >= 0 && pendingResolution < resolutions.Length)
            { data.resolutionWidth = resolutions[pendingResolution].x; data.resolutionHeight = resolutions[pendingResolution].y; }
            else data.resolutionWidth = data.resolutionHeight = 0;
            data.displayMode = pendingDisplayMode;
            GameSettings.Save();
            GameSettingsApplier.Instance?.ApplyVideo();
            SetStatus(Application.isEditor ? "Saved. The display changes in a built game." : "Display applied.");
        }

        private void BuildGraphics(GameSettingsData data)
        {
            ChoiceRow("Quality", "Low: highest frame rate. Medium: balanced. High: the full picture, no compromise", 3,
                () => data.quality == GraphicsQuality.Custom ? -1 : (int)data.quality,
                index => { GraphicsPresets.Apply(data, (GraphicsQuality)index, Baseline()); Changed(); SetStatus("Graphics quality: " + QualityNames[index] + "."); },
                index => index < 0 ? "Custom" : QualityNames[index]);
            ChoiceRow("Shadow quality", "Shadow map size, cascades and softness", 4,
                () => (int)data.shadowQuality, index => { data.shadowQuality = (ShadowQuality)index; Changed(true); }, index => ((ShadowQuality)index).ToString());
            SliderRow("Shadow distance", "How far from the camera the sun casts shadows", GameSettings.Ranges.ShadowDistance,
                () => data.shadowDistance, value => { data.shadowDistance = value; Changed(true); }, value => $"{value:0} m", 5);
            SliderRow("Pixel quality", "Terrain pixel error: lower is sharper ground, higher is faster", GameSettings.Ranges.TerrainPixelError,
                () => data.terrainPixelError, value => { data.terrainPixelError = value; Changed(true); }, value => $"{value:0} px", 1);
            SliderRow("Base map distance", "Beyond this distance the terrain uses its low-resolution texture", GameSettings.Ranges.BaseMapDistance,
                () => data.baseMapDistance, value => { data.baseMapDistance = value; Changed(true); }, value => $"{value:0} m", 10);
            SliderRow("Grass draw distance", "How far grass is drawn", GameSettings.Ranges.GrassDistance,
                () => data.grassDistance, value => { data.grassDistance = value; Changed(true); }, value => $"{value:0} m", 5);
            SliderRow("Grass density", "Share of the grass that is drawn", GameSettings.Ranges.GrassDensity,
                () => data.grassDensity, value => { data.grassDensity = value; Changed(true); }, value => $"{value * 100:0}%", .01f);
            SliderRow("Camera far plane", "Nothing farther than this is drawn", GameSettings.Ranges.CameraFar,
                () => data.cameraFar, value => { data.cameraFar = value; Changed(true); }, value => $"{value:0} m", 0, 3);
            SliderRow("Camera near plane", "Nothing closer than this is drawn", GameSettings.Ranges.CameraNear,
                () => data.cameraNear, value => { data.cameraNear = value; Changed(); }, value => $"{value:0.00} m", .01f);
            SliderRow("Camera field of view", "Vertical field of view", GameSettings.Ranges.CameraFov,
                () => data.cameraFov, value => { data.cameraFov = value; Changed(); }, value => $"{value:0}°", 1);
            SliderRow("Render scale", "Renders the scene at a share of the screen resolution", GameSettings.Ranges.RenderScale,
                () => data.renderScale, value => { data.renderScale = value; Changed(true); }, value => $"{value * 100:0}%", .05f);
            SliderRow("Tower detail distance", "How far vines and lava lamps are drawn on the tower", GameSettings.Ranges.TowerDetailDistance,
                () => data.towerDetailDistance, value => { data.towerDetailDistance = value; Changed(true); }, value => $"{value:0} m", 5);
            SliderRow("Tower mesh detail", "Distance at which far tower pieces switch to simpler meshes; 100% keeps the switch invisible", GameSettings.Ranges.TowerLodScale,
                () => data.towerLodScale, value => { data.towerLodScale = value; Changed(true); }, value => $"{value * 100:0}%", .05f);
            ToggleRow("Tower occlusion culling", "Skips far tower pieces hidden behind solid floors; changes nothing on screen",
                () => data.occlusionCulling, value => { data.occlusionCulling = value; Changed(true); });
            ToggleRow("Floor reflections", "Live reflection of the surroundings on polished floors",
                () => data.liveReflections, value => { data.liveReflections = value; Changed(true); });
            ChoiceRow("Sun shafts", "Light shafts inside the tower", 3,
                () => data.sunShafts, index => { data.sunShafts = index; Changed(true); }, index => index == 0 ? "Off" : index == 1 ? "Low" : "High");
            ChoiceRow("Clouds", "Volumetric cloud resolution and samples", 3,
                () => data.clouds, index => { data.clouds = index; Changed(true); }, index => QualityNames[index]);
            ToggleRow("River refraction", "Water under glass refracts every nearby floor at full resolution",
                () => data.riverRefraction, value => { data.riverRefraction = value; Changed(true); });
            SliderRow("Lava lamp lights", "Most lava lamps that cast real light at once; with fewer, the lit area around you is smaller",
                new Vector2(0, TowerOfBabel.World.Tower.TowerLavaLamps.MaximumLights),
                () => data.lampLights, value => { data.lampLights = Mathf.RoundToInt(value); Changed(true); }, value => $"{value:0}", 1);
        }

        private void BuildAudio(GameSettingsData data)
        {
            SliderRow("Master", "Everything", GameSettings.Ranges.Volume, () => data.masterVolume, value => { data.masterVolume = value; Changed(); }, Percent, .01f);
            SliderRow("Music", "Music", GameSettings.Ranges.Volume, () => data.musicVolume, value => { data.musicVolume = value; Changed(); }, Percent, .01f);
            SliderRow("UI", "Interface sounds", GameSettings.Ranges.Volume, () => data.uiVolume, value => { data.uiVolume = value; Changed(); AeroUIAudio.Play(AeroUICue.Change); }, Percent, .01f);
            SliderRow("VFX", "Rain, harvesting and machines", GameSettings.Ranges.Volume, () => data.vfxVolume, value => { data.vfxVolume = value; Changed(); }, Percent, .01f);
        }

        private static string Percent(float value) => $"{value * 100:0}%";

        private void BuildGameplay(GameSettingsData data)
        {
            SliderRow("Look sensitivity", "Mouse look speed", GameSettings.Ranges.LookSensitivity,
                () => data.lookSensitivity, value => { data.lookSensitivity = value; Changed(); }, value => $"{value:0.00}x", .05f);
            ToggleRow("Invert X axis", "Moving the mouse right looks left", () => data.invertX, value => { data.invertX = value; Changed(); });
            ToggleRow("Invert Y axis", "Moving the mouse up looks down", () => data.invertY, value => { data.invertY = value; Changed(); });
        }

        private void BuildControls()
        {
            string group = null;
            foreach (GameInput.Binding binding in GameInput.Bindings)
            {
                if (binding.Group != group) { group = binding.Group; Header(group); }
                BindingRow(binding);
            }
            ActionRow("Default controls", "Put every key back to its default", "Restore", () => { GameInput.ResetBindings(); RefreshBindings(); SetStatus("Default controls restored."); });
        }

        private void RestoreTabDefaults()
        {
            CancelRebind();
            GameSettingsData data = GameSettings.Current, fresh = new GameSettingsData();
            switch (tab)
            {
                case "Video":
                    data.resolutionWidth = data.resolutionHeight = 0; data.displayMode = fresh.displayMode;
                    GameSettings.Save(); GameSettingsApplier.Instance?.ApplyVideo();
                    break;
                case "Graphics":
                    GraphicsBaseline scene = Baseline();
                    GraphicsPresets.Apply(data, GraphicsQuality.High, scene);
                    data.cameraNear = scene.cameraNear; data.cameraFov = scene.cameraFov;
                    GameSettings.Save();
                    break;
                case "Audio":
                    data.masterVolume = data.musicVolume = data.uiVolume = data.vfxVolume = 1;
                    GameSettings.Save();
                    break;
                case "Gameplay":
                    data.lookSensitivity = fresh.lookSensitivity; data.invertX = data.invertY = false;
                    GameSettings.Save();
                    break;
                default:
                    GameInput.ResetBindings();
                    break;
            }
            RebuildRows();
            SetStatus(tab + " defaults restored.");
        }

        // ---- Controls: remapping ----

        private void BindingRow(GameInput.Binding binding)
        {
            var row = Row(binding.Label, binding.Rebindable ? "Click the key, then press the new one" : "This key cannot be changed");
            UnityEngine.UI.Button button = Button(row, GameInput.DisplayName(binding), () => BeginRebind(binding), 220);
            button.interactable = binding.Rebindable;
            bindingButtons[binding.Id] = button;
            UnityEngine.UI.Button reset = Button(row, "Default", () => { GameInput.SetBinding(binding, binding.DefaultPath); RefreshBindings(); }, 100);
            reset.interactable = binding.Rebindable;
        }

        private void RefreshBindings()
        {
            foreach (GameInput.Binding binding in GameInput.Bindings)
                if (bindingButtons.TryGetValue(binding.Id, out UnityEngine.UI.Button button) && button != null)
                    button.GetComponentInChildren<TMP_Text>().text = GameInput.DisplayName(binding);
        }

        public void BeginRebind(GameInput.Binding binding)
        {
            if (binding == null || !binding.Rebindable) return;
            CancelRebind();
            if (bindingButtons.TryGetValue(binding.Id, out UnityEngine.UI.Button button)) button.GetComponentInChildren<TMP_Text>().text = "Press a key...";
            SetStatus($"Press a key for \"{binding.Label}\". Escape cancels.");
            rebinding = GameInput.StartRebind(binding, accepted =>
            {
                rebinding = null;
                // The key that ended the remap (Escape included) must not also act on the menu this frame.
                ignoreEscapeFrame = Time.frameCount + 1;
                RefreshBindings();
                SetStatus(accepted ? $"\"{binding.Label}\" is now {GameInput.DisplayName(binding)}." : "Key unchanged.");
                if (accepted) AeroUIAudio.Play(AeroUICue.Success);
            });
        }

        private void CancelRebind()
        {
            if (rebinding == null) return;
            var operation = rebinding;
            rebinding = null;
            operation.Cancel();
        }

        // ---- Rows ----

        private void Header(string label)
        {
            var text = Text(label, content, 18, true);
            rows.Add(text.gameObject);
            text.GetComponent<LayoutElement>().preferredHeight = 30;
        }

        private RectTransform Row(string label, string detail)
        {
            var row = Panel(label, content); rows.Add(row.gameObject);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
            Horizontal(row, 10).padding = new RectOffset(14, 14, 8, 8);
            var captions = Rect("Description", row); var size = captions.gameObject.AddComponent<LayoutElement>(); size.flexibleWidth = 1; size.minWidth = 200; size.preferredHeight = 48;
            var title = Text(label, captions, 18, true); Stretch(title.rectTransform, 0, 0, 0, 22);
            var sub = Text(detail, captions, 13); Stretch(sub.rectTransform, 0, 0, 28, 0); sub.color = Soft;
            return row;
        }

        private void ActionRow(string label, string detail, string buttonText, UnityAction action)
        {
            var row = Row(label, detail);
            actionButtons[label] = Button(row, buttonText, action, 180);
            rowActions.Add(label);
        }

        /// <summary>
        /// A slider row. <paramref name="step"/> snaps the value (zero: no snapping); <paramref name="power"/> above one
        /// gives the low end of a wide range more travel.
        /// </summary>
        private void SliderRow(string label, string detail, Vector2 range, Func<float> read, Action<float> write, Func<float, string> format, float step, float power = 1)
        {
            var row = Row(label, detail);
            var value = Text("", row, 16, true); value.alignment = TextAlignmentOptions.MidlineRight;
            var valueSize = value.GetComponent<LayoutElement>(); valueSize.preferredWidth = valueSize.minWidth = 90; valueSize.preferredHeight = 40;
            UnityEngine.UI.Slider slider = NewSlider(row, 300);
            float ToSlider(float v) => Mathf.Pow(Mathf.InverseLerp(range.x, range.y, v), 1f / power);
            float FromSlider(float t)
            {
                float v = Mathf.Lerp(range.x, range.y, Mathf.Pow(t, power));
                if (step > 0) v = Mathf.Round(v / step) * step;
                return Mathf.Clamp(v, range.x, range.y);
            }
            slider.minValue = 0; slider.maxValue = 1;
            slider.SetValueWithoutNotify(ToSlider(read()));
            value.text = format(read());
            slider.onValueChanged.AddListener(t =>
            {
                float v = FromSlider(t);
                if (Mathf.Approximately(v, read())) return;
                write(v);
                value.text = format(read());
            });
            sliders[label] = slider;
            refreshers.Add(() =>
            {
                if (slider == null) return;
                // Leave a slider that already shows this value alone, so a drag is never interrupted.
                if (!Mathf.Approximately(FromSlider(slider.value), read())) slider.SetValueWithoutNotify(ToSlider(read()));
                value.text = format(read());
            });
        }

        private void ToggleRow(string label, string detail, Func<bool> read, Action<bool> write)
        {
            var row = Row(label, detail);
            var control = Panel(label + " toggle", row);
            var size = control.gameObject.AddComponent<LayoutElement>(); size.preferredWidth = size.minWidth = 180; size.preferredHeight = 40;
            var toggle = control.gameObject.AddComponent<UnityEngine.UI.Toggle>(); toggle.targetGraphic = control.GetComponent<Image>();
            toggle.toggleTransition = UnityEngine.UI.Toggle.ToggleTransition.None;
            var mark = Rect("Enabled", control); mark.anchorMin = mark.anchorMax = new Vector2(0, .5f); mark.anchoredPosition = new Vector2(28, 0); mark.sizeDelta = new Vector2(20, 20);
            var check = mark.gameObject.AddComponent<Image>(); check.color = new Color(.25f, .9f, .4f); toggle.graphic = check;
            var caption = Text(read() ? "On" : "Off", control, 17, true); Stretch(caption.rectTransform, 52, 8, 4, 4);
            void Show(bool on) { check.canvasRenderer.SetAlpha(on ? 1 : 0); check.gameObject.SetActive(on); caption.text = on ? "On" : "Off"; }
            toggle.SetIsOnWithoutNotify(read());
            Show(read());
            toggle.onValueChanged.AddListener(on => { if (on != read()) write(on); Show(read()); AeroUIAudio.Play(AeroUICue.Change); });
            toggles[label] = toggle;
            refreshers.Add(() => { if (toggle == null) return; toggle.SetIsOnWithoutNotify(read()); Show(read()); });
        }

        /// <summary>A row that steps through <paramref name="count"/> options. A read below zero shows the name for it (Custom).</summary>
        private void ChoiceRow(string label, string detail, int count, Func<int> read, Action<int> write, Func<int, string> name)
        {
            var row = Row(label, detail);
            UnityEngine.UI.Button previous = Button(row, "<", null, 44);
            var value = Text(name(read()), row, 17, true); value.alignment = TextAlignmentOptions.Center;
            var valueSize = value.GetComponent<LayoutElement>(); valueSize.preferredWidth = valueSize.minWidth = 230; valueSize.preferredHeight = 40;
            UnityEngine.UI.Button next = Button(row, ">", null, 44);
            void Step(int step)
            {
                int current = read();
                // From an off-list value (Custom), the arrows go to the first or the last option.
                int target = current < 0 ? (step > 0 ? count - 1 : 0) : Mathf.Clamp(current + step, 0, count - 1);
                if (target == current) return;
                write(target);
                value.text = name(read());
                AeroUIAudio.Play(AeroUICue.Change);
            }
            previous.onClick.AddListener(() => Step(-1));
            next.onClick.AddListener(() => Step(1));
            choices[label] = (previous, next, value);
            refreshers.Add(() => { if (value != null) value.text = name(read()); });
        }

        // ---- Shared interface kit (as in the developer controls) ----

        private UnityEngine.UI.Slider NewSlider(Transform parent, float width)
        {
            var root = Rect("Slider", parent);
            var size = root.gameObject.AddComponent<LayoutElement>(); size.preferredWidth = size.minWidth = width; size.preferredHeight = 40;
            var track = Rect("Track", root); track.anchorMin = new Vector2(0, .5f); track.anchorMax = new Vector2(1, .5f); track.sizeDelta = new Vector2(0, 10);
            var trackImage = track.gameObject.AddComponent<Image>(); trackImage.color = new Color(.80f, .93f, 1f, .95f);
            var fillArea = Rect("Fill Area", root); fillArea.anchorMin = new Vector2(0, .5f); fillArea.anchorMax = new Vector2(1, .5f);
            fillArea.offsetMin = new Vector2(0, -5); fillArea.offsetMax = new Vector2(0, 5);
            var fill = Rect("Fill", fillArea); fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>(); fillImage.color = AeroInputField.Accent; fillImage.raycastTarget = false;
            var handleArea = Rect("Handle Slide Area", root); handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(11, 0); handleArea.offsetMax = new Vector2(-11, 0);
            var handle = Rect("Handle", handleArea); handle.anchorMin = Vector2.zero; handle.anchorMax = new Vector2(0, 1); handle.sizeDelta = new Vector2(22, -12);
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Color.white;
            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage; slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            var colors = slider.colors; colors.highlightedColor = new Color(.85f, 1f, .9f); colors.pressedColor = AeroInputField.AccentFocus; slider.colors = colors;
            return slider;
        }

        private RectTransform Panel(string name, Transform parent)
        {
            var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>(); image.color = Color.white;
            if (glassMaterial != null) image.material = glassMaterial; else image.color = new Color(.78f, .92f, .98f, .96f);
            rect.gameObject.AddComponent<AeroGlassGeometry>().CornerRadius = 18;
            return rect;
        }

        private UnityEngine.UI.Button Button(Transform parent, string label, UnityAction action, float width)
        {
            GameObject root;
            if (buttonPrefab != null) { root = Instantiate(buttonPrefab, parent, false); root.name = label; }
            else { root = Rect(label, parent).gameObject; root.AddComponent<Image>().color = new Color(.06f, .48f, .8f); root.AddComponent<UnityEngine.UI.Button>(); }
            var button = root.GetComponent<UnityEngine.UI.Button>();
            if (action != null) button.onClick.AddListener(action);
            var text = root.GetComponentInChildren<TMP_Text>();
            if (text == null) text = Text(label, root.transform, 16, true);
            text.text = label; text.font = boldFont != null ? boldFont : regularFont != null ? regularFont : text.font; text.fontSize = 15; text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap; Stretch(text.rectTransform, 8, 8);
            if (!root.TryGetComponent(out LayoutElement element)) element = root.AddComponent<LayoutElement>();
            element.preferredWidth = width; element.minWidth = width; element.preferredHeight = 40;
            return button;
        }

        private TMP_Text Text(string value, Transform parent, float size, bool bold = false)
        {
            var text = Rect(value.Length > 30 ? value.Substring(0, 30) : value.Length == 0 ? "Text" : value, parent).gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = bold && boldFont != null ? boldFont : regularFont;
            if (font != null) text.font = font;
            text.fontSize = size; text.text = value; text.color = Ink; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            var element = text.gameObject.AddComponent<LayoutElement>(); element.preferredWidth = size * Mathf.Min(Mathf.Max(value.Length, 4), 14) * .48f;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rect.SetParent(parent, false); return rect;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private static void Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }
        private static HorizontalLayoutGroup Horizontal(RectTransform rect, float spacing)
        {
            var group = rect.gameObject.AddComponent<HorizontalLayoutGroup>(); group.spacing = spacing; group.childControlHeight = group.childControlWidth = true;
            group.childForceExpandHeight = false; group.childForceExpandWidth = false; group.childAlignment = TextAnchor.MiddleLeft; return group;
        }

        // ---- A scene without the saved menu still gets one, styled from the developer controls. ----

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void WatchScenes()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneLoaded;
            EnsureExists();
        }

        private static void SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => EnsureExists();

        /// <summary>Creates the menu beside the developer controls when the loaded scene has none.</summary>
        public static SettingsMenuUI EnsureExists()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<SettingsMenuUI>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var cheats = FindFirstObjectByType<CheatMenuUI>();
            if (cheats == null) return null;
            var root = new GameObject("SettingsMenu", typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(cheats.transform.parent, false);
            var menu = root.AddComponent<SettingsMenuUI>();
            var inventory = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            menu.Configure(cheats.RegularFont, cheats.BoldFont, cheats.GlassMaterial, cheats.ButtonPrefab, inventory != null ? inventory.InputActions : null);
            root.SetActive(true);
            return menu;
        }
    }
}
