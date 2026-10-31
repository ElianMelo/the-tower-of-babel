using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FishNet;
using TMPro;
using TowerOfBabel.Buildings;
using TowerOfBabel.Interface;
using TowerOfBabel.Networking;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Networking.Weather;
using TowerOfBabel.Resources;
using TowerOfBabel.Resources.Interaction;
using TowerOfBabel.Upgrades;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace TowerOfBabel.Cheats
{
    [DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class CheatMenuUI : MonoBehaviour
    {
        public static CheatMenuUI Instance { get; private set; }
        [SerializeField] private TMP_FontAsset regularFont, boldFont;
        [SerializeField] private Material glassMaterial;
        [SerializeField] private GameObject buttonPrefab;
        private GameObject visuals;
        private RectTransform content;
        private TMP_Text status, context, jobLabel;
        private TMP_InputField search, amountInput, floorInput;
        private UnityEngine.UI.ScrollRect scroll;
        private readonly List<(TMP_InputField field, CheatStat stat)> statFields = new();
        private readonly Dictionary<string, UnityEngine.UI.Button> actionButtons = new();
        private readonly Dictionary<string, UnityEngine.UI.Outline> tabOutlines = new();
        private readonly List<GameObject> rows = new();
        private readonly Dictionary<string, UnityEngine.UI.Toggle> toggleControls = new();
        private readonly List<(UnityEngine.UI.Toggle toggle, Func<bool> read, TMP_Text caption)> toggleValues = new();
        private readonly Dictionary<string, UnityEngine.UI.Outline> professionOutlines = new();
        private bool allProfessions;
        private Collider[] flightColliders = Array.Empty<Collider>();
        private bool[] flightColliderStates = Array.Empty<bool>();
        private NetworkUpgradeService boundService;
        private PlayerControlStateMachine controls;
        private PlayerController player;
        private CursorStateConfig? previousCursor;
        private ChunkManager chunks;
        private ChunkKey targetKey;
        private int targetIndex = -1;
        private ulong targetNode;
        private string targetProcessor = "", targetDescription = "No nearby target";
        private string category = "Quick actions";
        private UpgradeJob job;
        private float refreshTime;
        private bool flying;
        private Vector3 spawn;
        private bool hasSpawn;
        private bool playerWasEnabled;
        private static int consumedEscapeFrame = -1;
        public static bool ConsumedEscapeThisFrame => consumedEscapeFrame == Time.frameCount;
        private static readonly Color Ink = new(.055f, .20f, .29f, 1);
        private static readonly string[] Categories = { "Quick actions", "Progression", "Construction", "Inventory", "Stats", "World", "Animations" };
        public bool IsVisible => visuals != null && visuals.activeSelf;
        public bool IsGodMode => flying;
        public bool AllProfessions => allProfessions;
        public IReadOnlyDictionary<string, UnityEngine.UI.Toggle> ToggleControls => toggleControls;
        public IReadOnlyDictionary<string, UnityEngine.UI.Button> ActionButtons => actionButtons;
        public int SelectedFloor => Mathf.Max(0, Number(floorInput, 1) - 1);
        public int Amount => Mathf.Clamp(Number(amountInput, 1000), 1, 1000000);

        private void Awake() { Instance = this; BuildView(); }
        private void OnDestroy() { Hide(); Unbind(); RestoreFlight(); if (Instance == this) Instance = null; }
        private void OnDisable() { Hide(); RestoreFlight(); }
        private void Update()
        {
            Bind();
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) Toggle();
            if (IsVisible && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { consumedEscapeFrame = Time.frameCount; Hide(); }
            if (IsVisible && Time.unscaledTime >= refreshTime) { refreshTime = Time.unscaledTime + .2f; RefreshValues(); }
            UpdateFlight();
        }
        private void LateUpdate()
        {
            // Apply after Cinemachine has updated the scene camera; resetting the stat restores its lens.
            var service = NetworkUpgradeService.Instance;
            if (service == null || Camera.main == null) return;
            float fieldOfView = service.GetLocalCheatStat(CheatStat.CameraFov);
            if (!Mathf.Approximately(fieldOfView, CheatProfile.Default(CheatStat.CameraFov))) Camera.main.fieldOfView = fieldOfView;
        }
        public void Toggle() { if (IsVisible) Hide(); else Show(); }
        public void SelectCategory(string name)
        {
            if (!Categories.Contains(name)) throw new ArgumentException("Unknown cheat category.", nameof(name));
            category = name; search.SetTextWithoutNotify(""); RebuildRows(); RefreshValues();
        }
        public void Search(string term) { search.text = term ?? ""; }
        public void SetAmount(int amount) { amountInput.text = Mathf.Clamp(amount, 1, 1000000).ToString(); }
        public void SetFloor(int floor) { floorInput.text = Mathf.Max(1, floor).ToString(); }
        public void SelectAllProfessions() { allProfessions = true; RebuildRows(); RefreshValues(); }
        public void SelectProfession(UpgradeJob value) { job = value; allProfessions = false; RebuildRows(); RefreshValues(); }
        private void ForSelectedProfessions(Action<UpgradeJob> action)
        {
            if (allProfessions) foreach (UpgradeJob value in Enum.GetValues(typeof(UpgradeJob))) action(value);
            else action(job);
        }
        public void Show()
        {
            if (IsVisible) return;
            if (visuals == null) BuildView();
            ResolvePlayer(); CaptureTarget();
            InterfaceManager.Instance?.HideInventory(); InterfaceManager.Instance?.HideUpgrade(); InterfaceManager.Instance?.HideEmotes();
            FindFirstObjectByType<AeroDock>()?.Close();
            previousCursor = CursorStateConfig.Current; CursorStateConfig.UnlockedVisible.Apply();
            controls?.SetModalInputLocked(true);
            visuals.SetActive(true); visuals.transform.SetAsLastSibling();
            if (chunks != null) floorInput.text = ((chunks.ActiveConstructionFloor ?? chunks.CurrentChunk.FloorIndex) + 1).ToString();
            Bind(); RebuildRows(); RefreshValues();
            SetStatus(InstanceFinder.IsClientStarted ? "Ready - changes are confirmed by the server." : "Offline - start a local host to use gameplay cheats.");
        }
        public void Hide()
        {
            if (!IsVisible) return;
            visuals.SetActive(false); controls?.SetModalInputLocked(false);
            if (previousCursor.HasValue) previousCursor.Value.Apply(); previousCursor = null;
        }
        public void ShowPreview() { if (visuals == null) BuildView(); visuals.SetActive(true); RefreshValues(); SetStatus("F3 toggles this menu - choose a category or search for an action."); }
        private void ResolvePlayer()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (controls == null) controls = player != null ? player.GetComponent<PlayerControlStateMachine>() : FindFirstObjectByType<PlayerControlStateMachine>();
            if (chunks == null) chunks = FindFirstObjectByType<ChunkManager>();
            if (!hasSpawn && player != null) { spawn = player.transform.position; hasSpawn = true; }
        }
        private void CaptureTarget()
        {
            targetIndex = -1; targetNode = 0; targetProcessor = ""; targetDescription = "No nearby target";
            var raycaster = player != null ? player.GetComponent<PlayerInteractionRaycaster>() : FindFirstObjectByType<PlayerInteractionRaycaster>();
            GameObject target = raycaster != null ? raycaster.CurrentTarget : null;
            var building = target != null ? target.GetComponentInParent<Building>() : null;
            var resource = target != null ? target.GetComponentInParent<Resource>() : null;
            var processor = target != null ? target.GetComponentInParent<ResourceProcessor>() : null;
            if (building == null && resource == null && processor == null && player != null && chunks != null)
            {
                var nearby = new List<Building>(); chunks.GetNearbyConstruction(player.transform.position, 15, nearby);
                building = nearby.OrderBy(b => (b.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            }
            if (building != null && building.IsBound) { targetKey = building.ChunkKey; targetIndex = building.LocalIndex; targetDescription = $"{building.ObjectName} - floor {targetKey.FloorIndex + 1}, stage {building.CurrentStage}/10"; }
            if (resource != null) { targetNode = resource.NodeId; targetDescription = resource.ObjectName; }
            if (processor != null) { targetProcessor = processor.ProcessorId; targetDescription = processor.ObjectName; }
        }
        private void Bind()
        {
            if (boundService == NetworkUpgradeService.Instance) return;
            Unbind(); boundService = NetworkUpgradeService.Instance;
            if (boundService != null) boundService.CheatResult += SetStatus;
        }
        private void Unbind() { if (boundService != null) boundService.CheatResult -= SetStatus; boundService = null; }
        private void SetStatus(string message) { if (status != null) status.text = message; }
        private void Sent(bool sent) { SetStatus(sent ? "Request sent..." : "Connect to a session first, then try again."); }
        private void RequestWeather(Func<NetworkWeatherService, bool> request, string done)
        {
            var service = NetworkWeatherService.Instance;
            if (service == null) { SetStatus("No weather service in this scene."); return; }
            if (!request(service)) { SetStatus("Connect to a session first, then try again."); return; }
            SetStatus(service.HasAuthority ? done : "Request sent... " + done);
            RefreshValues();
        }
        private void Command(CheatCommand command)
        {
            ResolvePlayer(); player?.GetComponent<PlayerInteractionRaycaster>()?.CancelCurrentInteraction();
            Sent(NetworkUpgradeService.Instance != null && NetworkUpgradeService.Instance.RequestCheatCommand(command, Amount, SelectedFloor, targetKey, targetIndex, targetNode, targetProcessor));
        }
        private void RefreshValues()
        {
            var service = NetworkUpgradeService.Instance;
            var snapshot = service != null ? service.GetLocalSnapshot(job) : new UpgradeJobSnapshot(job, 0, 0, 0, null);
            if (jobLabel != null) jobLabel.text = $"{job}   Level {snapshot.Level}/50   XP {snapshot.Experience}/{UpgradeJobProgress.GetExperienceRequired(snapshot.Level)}   Points {snapshot.AvailablePoints}";
            if (jobLabel != null && allProfessions) jobLabel.text = "ALL classes  |  " + string.Join("   ", Enum.GetValues(typeof(UpgradeJob)).Cast<UpgradeJob>().Select(j => $"{j}: Lv {service?.GetLocalSnapshot(j).Level ?? 0}"));
            foreach (var item in professionOutlines) item.Value.enabled = item.Key == (allProfessions ? "ALL" : job.ToString());
            foreach (var item in toggleValues) if (item.toggle != null) { bool value = item.read(); item.toggle.SetIsOnWithoutNotify(value); item.toggle.graphic.gameObject.SetActive(value); item.caption.text = value ? "On" : "Off"; }
            if (context != null) context.text = $"Target: {targetDescription}     |     Bag: {NetworkResourceService.Instance?.LocalCapacity ?? 50} per resource";
            foreach (var pair in statFields) if (pair.field != null && !pair.field.isFocused)
                pair.field.SetTextWithoutNotify((service != null ? service.GetLocalCheatStat(pair.stat) : CheatProfile.Default(pair.stat)).ToString("0.##", CultureInfo.InvariantCulture));
        }

        public void BuildView()
        {
            if (visuals != null) return;
            var canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 900;
            var scaler = GetComponent<UnityEngine.UI.CanvasScaler>() ?? gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var shade = Rect("Cheat overlay", transform); Stretch(shade); visuals = shade.gameObject;
            shade.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.01f, .06f, .12f, .30f);
            var panel = Panel("Developer controls", shade); panel.anchorMin = new Vector2(.055f, .06f); panel.anchorMax = new Vector2(.945f, .94f); panel.offsetMin = panel.offsetMax = Vector2.zero;
            var title = Text("Developer controls", panel, 30, true); Place(title.rectTransform, 26, 12, 560, 42);
            var subtitle = Text("F3  /  Escape to close - quick actions, every stat, no console commands", panel, 16); Place(subtitle.rectTransform, 28, 55, 950, 28);
            var close = Button(panel, "Close [F3]", Hide, 130); var closeRect = (RectTransform)close.transform; closeRect.anchorMin = closeRect.anchorMax = new Vector2(1, 1); closeRect.pivot = new Vector2(1, 1); closeRect.anchoredPosition = new Vector2(-24, -18); closeRect.sizeDelta = new Vector2(140, 42);
            var tabs = Rect("Categories", panel); Place(tabs, 24, 96, 1370, 40); Horizontal(tabs, 8);
            tabs.anchorMax = Vector2.one; tabs.offsetMax = new Vector2(-24, -96); tabs.offsetMin = new Vector2(24, -136); tabs.anchorMin = new Vector2(0, 1);
            foreach (var tab in Categories) { string selected = tab; var tabButton = Button(tabs, tab, () => { category = selected; search.SetTextWithoutNotify(""); RebuildRows(); }, 145); var le = tabButton.GetComponent<UnityEngine.UI.LayoutElement>(); le.minWidth = 80; le.flexibleWidth = 1; var outline = tabButton.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = new Color(.5f, 1, .65f); outline.effectDistance = new Vector2(2, -2); tabOutlines[tab] = outline; }
            var toolbar = Rect("Controls", panel); Place(toolbar, 24, 149, 1370, 42); Horizontal(toolbar, 10);
            toolbar.anchorMin = new Vector2(0, 1); toolbar.anchorMax = Vector2.one; toolbar.offsetMin = new Vector2(24, -191); toolbar.offsetMax = new Vector2(-24, -149);
            search = Input(toolbar, "Search actions or stats", "", 260, false); search.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1; search.GetComponent<UnityEngine.UI.LayoutElement>().minWidth = 170; search.onValueChanged.AddListener(_ => RebuildRows());
            Text("Amount", toolbar, 17).GetComponent<UnityEngine.UI.LayoutElement>().minWidth = 64; amountInput = Input(toolbar, "Amount", "1000", 100, true);
            foreach (int preset in new[] { 100, 1000, 10000 }) { int n = preset; Button(toolbar, n.ToString(), () => amountInput.text = n.ToString(), 70); }
            Text("Floor", toolbar, 17); floorInput = Input(toolbar, "Floor", "1", 65, true);
            Button(toolbar, "-", () => floorInput.text = Mathf.Max(1, SelectedFloor).ToString(), 36);
            Button(toolbar, "+", () => floorInput.text = (SelectedFloor + 2).ToString(), 36);
            jobLabel = Text("", panel, 19, true); Place(jobLabel.rectTransform, 28, 204, 950, 30);
            var jobs = Rect("Professions", panel); Place(jobs, 905, 202, 480, 32); Horizontal(jobs, 6);
            jobs.anchorMin = jobs.anchorMax = new Vector2(1, 1); jobs.pivot = new Vector2(1, 1); jobs.anchoredPosition = new Vector2(-28, -202);
            jobLabel.rectTransform.anchorMax = Vector2.one; jobLabel.rectTransform.offsetMin = new Vector2(28, -234); jobLabel.rectTransform.offsetMax = new Vector2(-530, -204); jobLabel.rectTransform.anchorMin = new Vector2(0, 1);
            void ProfessionButton(string label, UnityAction action, float width)
            {
                var button = Button(jobs, label, action, width); var outline = button.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(.5f, 1, .65f); outline.effectDistance = new Vector2(2, -2); professionOutlines[label] = outline;
            }
            foreach (UpgradeJob option in Enum.GetValues(typeof(UpgradeJob))) { var captured = option; ProfessionButton(option.ToString(), () => SelectProfession(captured), 120); }
            ProfessionButton("ALL", SelectAllProfessions, 90);
            context = Text("Target captured when F3 opens", panel, 16); Place(context.rectTransform, 28, 242, 1330, 28);
            var scrollRoot = Rect("Action list", panel); Stretch(scrollRoot, 24, 24, 282, 70);
            scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.horizontal = false; scroll.scrollSensitivity = 38;
            var viewport = Rect("Viewport", scrollRoot); Stretch(viewport); viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.8f, .95f, 1, .15f);
            content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing = 8; layout.padding = new RectOffset(8, 8, 8, 8); layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            status = Text("Ready", panel, 17); Stretch(status.rectTransform, 28, 28, 0, 14); status.rectTransform.anchorMax = new Vector2(1, 0); status.rectTransform.sizeDelta = new Vector2(-56, 38);
            status.rectTransform.anchoredPosition = new Vector2(0, 32);
            RebuildRows(); visuals.SetActive(false);
        }
        private bool Matches(string section, string label) => string.IsNullOrWhiteSpace(search?.text) ? category == section || category == "Quick actions" && section == "Quick actions" : (section + " " + label).IndexOf(search.text, StringComparison.OrdinalIgnoreCase) >= 0;
        private void Action(string section, string label, string detail, UnityAction action, string button = "Run")
        {
            if (!Matches(section, label) || actionButtons.ContainsKey(label)) return;
            var row = Row(label, detail); var b = Button(row, button, action, 180); actionButtons[label] = b;
        }
        private void ToggleRow(string section, string label, string detail, Func<bool> read, UnityAction<bool> changed)
        {
            if (!Matches(section, label) || toggleControls.ContainsKey(label)) return;
            var row = Row(label, detail); var control = Panel(label + " toggle", row);
            var layout = control.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); layout.preferredWidth = layout.minWidth = 180; layout.preferredHeight = 40;
            var toggle = control.gameObject.AddComponent<UnityEngine.UI.Toggle>(); toggle.targetGraphic = control.GetComponent<UnityEngine.UI.Image>();
            toggle.toggleTransition = UnityEngine.UI.Toggle.ToggleTransition.None;
            var mark = Rect("Enabled", control); mark.anchorMin = mark.anchorMax = new Vector2(0, .5f); mark.anchoredPosition = new Vector2(28, 0); mark.sizeDelta = new Vector2(20, 20);
            var check = mark.gameObject.AddComponent<UnityEngine.UI.Image>(); check.color = new Color(.25f, .9f, .4f); toggle.graphic = check;
            var caption = Text(read() ? "On" : "Off", control, 17, true); Stretch(caption.rectTransform, 52, 8, 4, 4);
            toggle.SetIsOnWithoutNotify(read());
            mark.gameObject.SetActive(read());
            toggle.onValueChanged.AddListener(value => { if (value != read()) changed(value); bool state = read(); mark.gameObject.SetActive(state); caption.text = state ? "On" : "Off"; });
            toggleValues.Add((toggle, read, caption)); toggleControls[label] = toggle;
        }
        public void RebuildRows()
        {
            if (content == null) return;
            foreach (var row in rows) { row.SetActive(false); if (Application.isPlaying) Destroy(row); else DestroyImmediate(row); }
            rows.Clear(); statFields.Clear(); actionButtons.Clear(); toggleControls.Clear(); toggleValues.Clear();
            foreach (var tab in tabOutlines) tab.Value.enabled = tab.Key == category;
            var service = NetworkUpgradeService.Instance;
            UnityAction level = () => ForSelectedProfessions(j => Sent(service != null && service.RequestCheatLevel(j, Mathf.Min(50, service.GetLocalSnapshot(j).Level + 1))));
            UnityAction xp = () => ForSelectedProfessions(j => Sent(service != null && service.RequestCheatExperience(j, Amount)));
            foreach (string section in new[] { "Quick actions", "Progression" })
            {
                Action(section, "Level up", "Selected profession - gain one level and its upgrade point", level, "+1 level");
                Action(section, "Gain XP", "Add the selected amount of experience to the selected profession", xp, "Grant XP");
            }
            foreach (string section in new[] { "Quick actions", "Construction" })
            {
                ToggleRow(section, "Instantly build asset", "Fills your bag with every resource; while enabled, interact with any available asset to finish it for free", () => NetworkUpgradeService.Instance?.GetLocalCheatStat(CheatStat.InstantBuild) > .5f,
                    value => Sent(NetworkUpgradeService.Instance != null && NetworkUpgradeService.Instance.RequestCheatStat(CheatStat.InstantBuild, value ? 1 : 0)));
                Action(section, "Instantly build floor", "Complete the next unfinished floor, wherever you are standing; repeat to advance", () => Command(CheatCommand.BuildFloor), "Build next floor");
            }
            foreach (string section in new[] { "Quick actions", "World" }) ToggleRow(section, "God mode", "Fly through walls with WASD, Space up, Ctrl down and Shift to accelerate", () => flying, SetGodMode);
            foreach (string section in new[] { "Quick actions", "Inventory" })
                Action(section, "Increase bag capacity", "Add the amount to the capacity of each resource", () => Sent(service != null && service.RequestCheatStat(CheatStat.BagCapacity, (NetworkResourceService.Instance?.LocalCapacity ?? 50) + Amount)), "+ capacity");
            Action("Quick actions", "Start local host", "Start a session here, then use all gameplay actions", () => { var bootstrap = FindFirstObjectByType<NetworkBootstrap>(); SetStatus(bootstrap != null && bootstrap.StartHost() ? "Starting host..." : "A session is already running or the network service is unavailable."); }, "Start host");
            Action("Progression", "Gain 10 levels", "Selected classes, clamped to level 50", () => ForSelectedProfessions(j => Sent(service != null && service.RequestCheatLevel(j, Mathf.Min(50, service.GetLocalSnapshot(j).Level + 10)))), "+10 levels");
            Action("Progression", "Max level", "Set the selected classes to level 50", () => ForSelectedProfessions(j => Sent(service != null && service.RequestCheatLevel(j, 50))), "Level 50");
            Action("Progression", "Grant upgrade points", "Add the selected amount of spendable points", () => ForSelectedProfessions(j => Sent(service != null && service.RequestCheatPoints(j, Amount))), "Grant points");
            Action("Progression", "Unlock all upgrades", "Max every profession and purchase every node", () => Command(CheatCommand.UnlockAllUpgrades), "Unlock all");
            Action("Progression", "Reset selected profession", "Reset the selected classes' level, XP, points and purchased nodes", () => ForSelectedProfessions(j => Sent(service != null && service.RequestCheatReset(j))), "Reset selected");
            Action("Progression", "Reset all progression", "Reset all three professions", () => Sent(service != null && service.RequestCheatResetAll()), "Reset all jobs");
            if (service != null && service.Config != null && category == "Progression")
                foreach (var upgrade in service.Config.GetUpgrades(job)) { string id = upgrade.Id; int row = upgrade.Row, column = upgrade.Column; Action("Progression", "Purchase " + upgrade.DisplayName + " [" + row + "," + column + "]", allProfessions ? "Matching node position in every class" : id, () => ForSelectedProfessions(j => Sent(service.RequestCheatPurchase(j, row, column))), "Purchase"); }
            foreach (var entry in new[] {
                ("Advance asset one stage", "Add one stage to the captured asset", CheatCommand.AdvanceAsset),
                ("Reset asset", "Return the captured asset to its blueprint", CheatCommand.ResetAsset),
                ("Reset floor", "Reset the selected floor and hide floors above it", CheatCommand.ResetFloor),
                ("Instantly build tower", "Finish every floor in one batched operation", CheatCommand.BuildTower),
                ("Reset tower", "Return the whole tower to the first-floor blueprint", CheatCommand.ResetTower) })
            { var cmd = entry.Item3; Action("Construction", entry.Item1, entry.Item2, () => Command(cmd)); }
            foreach (var entry in new[] {
                ("Gain stone", "Add the selected amount, respecting the bag capacity", CheatCommand.AddStone),
                ("Gain processed stone", "Add the selected amount, respecting the bag capacity", CheatCommand.AddProcessedStone),
                ("Gain sand", "Add the selected amount, respecting the bag capacity", CheatCommand.AddSand),
                ("Gain glass", "Add the selected amount, respecting the bag capacity", CheatCommand.AddGlass),
                ("Gain river essence", "Add the selected amount, respecting the bag capacity", CheatCommand.AddRiverEssence),
                ("Gain river block", "Add the selected amount, respecting the bag capacity", CheatCommand.AddRiverBlock),
                ("Gain water essence", "Add the selected amount, respecting the bag capacity", CheatCommand.AddWaterEssence),
                ("Gain water block", "Add the selected amount, respecting the bag capacity", CheatCommand.AddWaterBlock),
                ("Gain sky essence", "Add the selected amount, respecting the bag capacity", CheatCommand.AddSkyEssence),
                ("Gain sky block", "Add the selected amount, respecting the bag capacity", CheatCommand.AddSkyBlock),
                ("Fill bag", "Fill every resource to its current capacity", CheatCommand.FillBag),
                ("Clear bag", "Set every resource amount to zero", CheatCommand.ClearBag),
                ("Instantly gather target", "Collect the captured resource without waiting", CheatCommand.InstantGather),
                ("Instantly process target", "Process the captured machine immediately, consuming its normal cost", CheatCommand.InstantProcess),
                ("Respawn all resources", "Remove cooldowns from every resource node", CheatCommand.ResetResources) })
            { var cmd = entry.Item3; Action("Inventory", entry.Item1, entry.Item2, () => Command(cmd)); }
            foreach (CheatStat stat in Enum.GetValues(typeof(CheatStat))) StatRow(stat);
            Action("Stats", "Reset every stat", "Restore movement, actions, capacity and experience multipliers", () => Command(CheatCommand.ResetModifiers), "Restore defaults");
            Action("World", "Teleport to selected floor", "Move to a safe floor tile on an unlocked floor", TeleportFloor, "Teleport");
            Action("World", "Return to spawn", "Move back to the saved starting position", () => { ResolvePlayer(); if (hasSpawn) Teleport(spawn); }, "Return");
            foreach (float scale in new[] { 0f, .25f, 1f, 2f, 5f }) { float value = scale; Action("World", "Time scale " + scale.ToString("0.##", CultureInfo.InvariantCulture), "Local simulation speed; menu remains responsive while paused", () => { Time.timeScale = value; SetStatus("Time scale: " + value); }, "Apply"); }
            // Weather and time of day are server-authoritative and synced to every player.
            ToggleRow("World", "Rain", "Falling rain, splashes and wet surfaces for every player; pauses automatic weather",
                () => NetworkWeatherService.Instance != null && NetworkWeatherService.Instance.Raining,
                value => RequestWeather(service => service.RequestRain(value), value ? "Rain started for everyone." : "Rain stopped; surfaces dry out."));
            ToggleRow("World", "Automatic weather", "Let the server start and stop rain on its own, by day or night",
                () => NetworkWeatherService.Instance != null && NetworkWeatherService.Instance.AutomaticWeather,
                value => RequestWeather(service => service.RequestAutomaticWeather(value), value ? "Automatic weather on." : "Automatic weather off; rain stays as set."));
            Action("World", "Set day", "Jump everyone to noon", () => RequestWeather(service => service.RequestDay(), "Time of day: noon."), "Day");
            Action("World", "Set night", "Jump everyone to midnight", () => RequestWeather(service => service.RequestNight(), "Time of day: midnight."), "Night");
            ToggleRow("World", "Freeze time of day", "Stop the day/night cycle for everyone to inspect assets",
                () => NetworkWeatherService.Instance != null && NetworkWeatherService.Instance.TimeFrozen,
                value => RequestWeather(service => service.RequestFreezeTime(value), value ? "Time of day frozen." : "Day/night cycle running."));
            Action("World", "Wetness preset", "Cycle dry, dew, soaked and weather-driven surface film",
                () => { var wet = TowerOfBabel.Rendering.AeroWetnessController.Instance; SetStatus(wet != null ? "Wetness: " + wet.CycleWetnessPreset() : "No wetness controller in this scene."); }, "Cycle");
            Action("World", "Post look", "Cycle Restrained, Bright Aero and Y2K colour grading",
                () => { var post = FindFirstObjectByType<TowerOfBabel.Rendering.AeroPostProfileSwitcher>(); SetStatus(post != null ? "Post look: " + post.Cycle() : "No post look switcher in this scene."); }, "Cycle");
            Action("World", "Scan construction", "Close this menu and highlight nearby unfinished work", () => { Hide(); FindFirstObjectByType<PlayerInteractionRaycaster>()?.ScanNearbyConstruction(); }, "Scan");
            Action("World", "Cancel current action", "Stop gathering, processing or construction", () => { ResolvePlayer(); player?.GetComponent<PlayerInteractionRaycaster>()?.CancelCurrentInteraction(); SetStatus("Action cancelled."); }, "Cancel");
            foreach (PlayerAnimationTrigger trigger in Enum.GetValues(typeof(PlayerAnimationTrigger)))
            {
                var action = trigger; if (action == PlayerAnimationTrigger.None) continue;
                Action("Animations", "Play " + action, "Play this action on the local character", () => { ResolvePlayer(); var v = player != null ? player.GetComponentInChildren<PlayerVisuals>() : null; if (v == null) return; if (action == PlayerAnimationTrigger.Jump) v.PlayJump(); else if (action == PlayerAnimationTrigger.Digging) v.PlayDigging(); else if (action == PlayerAnimationTrigger.Golf) v.PlayGolf(); else if (action == PlayerAnimationTrigger.CancelAnimation) v.CancelAnimation(); else v.PlayEmote(action); }, "Play");
            }
            if (rows.Count == 0) Row("No matching actions", "Try another search or category.");
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content); if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }
        private void StatRow(CheatStat stat)
        {
            if (stat is CheatStat.GatherCost or CheatStat.InstantBuild) return; // Dedicated toggle; gathering has no input cost.
            string label = System.Text.RegularExpressions.Regex.Replace(stat.ToString(), "([a-z])([A-Z])", "$1 $2");
            label = stat switch {
                CheatStat.BuildSpeed => "Construction speed",
                CheatStat.BuildProduction => "Construction stages per action",
                CheatStat.BuildCost => "Construction cost",
                CheatStat.CameraFov => "Camera field of view",
                _ => char.ToUpperInvariant(label[0]) + label.Substring(1).ToLowerInvariant()
            };
            if (!Matches("Stats", label)) return;
            bool absolute = stat is CheatStat.BagCapacity or CheatStat.CameraFov or CheatStat.ExtraJumps;
            var row = Row(label, absolute ? "Absolute value" : "Multiplier - 1 restores normal gameplay");
            float current = NetworkUpgradeService.Instance != null ? NetworkUpgradeService.Instance.GetLocalCheatStat(stat) : CheatProfile.Default(stat);
            var input = Input(row, label, current.ToString(CultureInfo.InvariantCulture), 100, true); statFields.Add((input, stat));
            UnityAction<float> set = value => SetSelectedStat(stat, value);
            Button(row, "Apply", () => { if (float.TryParse(input.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) set(value); }, 80);
            foreach (float factor in absolute ? new[] { 1f, 2f } : new[] { 2f, 5f, 10f, 100f }) { float value = factor; Button(row, absolute ? "+" + (stat == CheatStat.BagCapacity ? (factor * 1000).ToString() : factor.ToString()) : factor + "x", () => set(absolute ? (NetworkUpgradeService.Instance?.GetLocalCheatStat(stat) ?? current) + (stat == CheatStat.BagCapacity ? value * 1000 : value) : value), 62); }
            Button(row, "Reset", () => set(CheatProfile.Default(stat)), 74);
        }
        private void TeleportFloor()
        {
            ResolvePlayer(); if (chunks == null || player == null || !chunks.IsFloorVisible(SelectedFloor)) { SetStatus("Complete the lower floors first, or build this floor using Construction."); return; }
            var tiles = chunks.CachedChunks.Where(c => c.Key.FloorIndex == SelectedFloor).SelectMany(c => c.Assets).Where(a => a.AssetType.IsFloor()).OrderBy(a => (a.Position - player.transform.position).sqrMagnitude).ToArray();
            if (tiles.Length == 0) { SetStatus("No floor tiles at that level."); return; } Teleport(tiles[0].Position + Vector3.up * 2);
        }
        private void Teleport(Vector3 position)
        {
            if (player == null) return; var body = player.GetComponent<CharacterController>(); bool was = body != null && body.enabled;
            if (body != null) body.enabled = false; player.transform.position = position; if (body != null) body.enabled = was;
            chunks?.RefreshTowerVisibility(); SetStatus("Teleported.");
        }
        private void ToggleFlight()
        {
            ResolvePlayer(); if (player == null) return;
            if (flying) { RestoreFlight(); SetStatus("God mode disabled."); return; }
            playerWasEnabled = player.enabled;
            player.GetComponent<PlayerInteractionRaycaster>()?.CancelCurrentInteraction();
            flightColliders = player.GetComponentsInChildren<Collider>(true); flightColliderStates = flightColliders.Select(c => c.enabled).ToArray();
            player.enabled = false; foreach (var collider in flightColliders) collider.enabled = false;
            flying = true; SetStatus("God mode enabled. Close F3 to fly; Space rises, Ctrl descends.");
        }
        private void RestoreFlight()
        {
            if (!flying) return; flying = false; if (player == null) return; player.enabled = playerWasEnabled;
            for (int i = 0; i < flightColliders.Length; i++) if (flightColliders[i] != null) flightColliders[i].enabled = flightColliderStates[i];
            flightColliders = Array.Empty<Collider>(); flightColliderStates = Array.Empty<bool>();
        }
        public void SetGodMode(bool enabled) { if (enabled != flying) ToggleFlight(); }
        private void SetSelectedStat(CheatStat stat, float value)
        {
            int index = (int)stat, first = (int)CheatStat.GatherSpeed, last = (int)CheatStat.BuildCost;
            if (allProfessions && index >= first && index <= last)
            {
                int start = first + (index - first) / 3 * 3;
                ForSelectedProfessions(j => { var target = (CheatStat)(start + (int)j); if (target != CheatStat.GatherCost) Sent(NetworkUpgradeService.Instance != null && NetworkUpgradeService.Instance.RequestCheatStat(target, value)); });
            }
            else Sent(NetworkUpgradeService.Instance != null && NetworkUpgradeService.Instance.RequestCheatStat(stat, value));
        }
        private void UpdateFlight()
        {
            var k = Keyboard.current; if (!flying || IsVisible || player == null || k == null || Camera.main == null) return;
            float x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), z = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0), y = (k.spaceKey.isPressed ? 1 : 0) - (k.leftCtrlKey.isPressed ? 1 : 0);
            var direction = Camera.main.transform.right * x + Camera.main.transform.forward * z + Vector3.up * y;
            player.transform.position += Vector3.ClampMagnitude(direction, 1) * 18 * (k.leftShiftKey.isPressed ? 3 : 1) * Time.unscaledDeltaTime;
        }
        private RectTransform Row(string label, string detail)
        {
            var row = Panel(label, content); rows.Add(row.gameObject); var layout = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); layout.preferredHeight = 70; Horizontal(row, 8).padding = new RectOffset(14, 14, 8, 8);
            var captions = Rect("Description", row); var captionSize = captions.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); captionSize.flexibleWidth = 1; captionSize.preferredHeight = 54;
            var title = Text(label, captions, 19, true); Stretch(title.rectTransform, 0, 0, 0, 24);
            var sub = Text(detail, captions, 13); Stretch(sub.rectTransform, 0, 0, 30, 0); sub.color = new Color(.15f, .35f, .43f);
            return row;
        }
        private RectTransform Panel(string name, Transform parent)
        {
            var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = Color.white; if (glassMaterial != null) image.material = glassMaterial; else image.color = new Color(.78f, .92f, .98f, .96f);
            rect.gameObject.AddComponent<AeroGlassGeometry>().CornerRadius = 18; return rect;
        }
        private UnityEngine.UI.Button Button(Transform parent, string label, UnityAction action, float width)
        {
            GameObject root;
            if (buttonPrefab != null) { root = Instantiate(buttonPrefab, parent, false); root.name = label; }
            else { root = Rect(label, parent).gameObject; root.AddComponent<UnityEngine.UI.Image>().color = new Color(.06f, .48f, .8f); root.AddComponent<UnityEngine.UI.Button>(); }
            var button = root.GetComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(action);
            var text = root.GetComponentInChildren<TMP_Text>() ?? Text(label, root.transform, 16, true); text.text = label; text.font = boldFont != null ? boldFont : regularFont; text.fontSize = 15; text.color = Color.white; text.textWrappingMode = TextWrappingModes.NoWrap; Stretch(text.rectTransform, 8, 8);
            var le = root.GetComponent<UnityEngine.UI.LayoutElement>() ?? root.AddComponent<UnityEngine.UI.LayoutElement>(); le.preferredWidth = width; le.minWidth = width; le.preferredHeight = 40;
            return button;
        }
        private TMP_InputField Input(Transform parent, string label, string value, float width, bool numeric)
        {
            var rect = Rect(label, parent); rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.95f, .99f, 1, .95f);
            var le = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); le.preferredWidth = width; le.minWidth = width; le.preferredHeight = 38;
            var viewport = Rect("Text area", rect); Stretch(viewport, 9, 9, 4, 4); viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text = Text(value, viewport, 17); Stretch(text.rectTransform); var placeholder = Text(label, viewport, 17); Stretch(placeholder.rectTransform); placeholder.color = new Color(.25f, .45f, .52f, .65f);
            var input = rect.gameObject.AddComponent<TMP_InputField>(); input.textViewport = viewport; input.textComponent = text; input.placeholder = placeholder; input.contentType = numeric ? TMP_InputField.ContentType.DecimalNumber : TMP_InputField.ContentType.Standard; input.characterLimit = numeric ? 12 : 80; input.text = value; return input;
        }
        private TMP_Text Text(string value, Transform parent, float size, bool bold = false)
        {
            var text = Rect(value.Length > 30 ? value.Substring(0, 30) : value, parent).gameObject.AddComponent<TextMeshProUGUI>(); text.font = bold && boldFont != null ? boldFont : regularFont; text.fontSize = size; text.text = value; text.color = Ink; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            var le = text.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); le.preferredWidth = size * Mathf.Min(value.Length, 14) * .48f; return text;
        }
        private static int Number(TMP_InputField input, int fallback) => input != null && int.TryParse(input.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : fallback;
        private static RectTransform Rect(string name, Transform parent) { var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rect.SetParent(parent, false); return rect; }
        private static void Place(RectTransform rect, float x, float y, float width, float height) { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); }
        private static void Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
        private static UnityEngine.UI.HorizontalLayoutGroup Horizontal(RectTransform rect, float spacing) { var h = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); h.spacing = spacing; h.childControlHeight = h.childControlWidth = true; h.childForceExpandHeight = false; h.childForceExpandWidth = false; h.childAlignment = TextAnchor.MiddleLeft; return h; }
    }
}
