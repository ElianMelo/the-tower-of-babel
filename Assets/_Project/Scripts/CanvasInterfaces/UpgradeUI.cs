using System.Collections.Generic;
using TMPro;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Upgrades;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TowerOfBabel.Interface;

namespace TowerOfBabel
{
    public sealed class UpgradeUI : MonoBehaviour
    {
        [SerializeField] private GameObject visuals;
        [SerializeField] private GameObject prefab;
        [SerializeField] private GameObject jobTitle;
        [SerializeField] private Button gatherButton;
        [SerializeField] private Button processorButton;
        [SerializeField] private Button builderButton;
        [SerializeField] private GameObject gatherBoard;
        [SerializeField] private GameObject processorBoard;
        [SerializeField] private GameObject builderBoard;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string toggleActionName = "Player/Upgrade";

        [Header("Progress Presentation")]
        [SerializeField] private Image experienceFill;
        [SerializeField] private TMP_Text boardHint;
        private Vector2 lastBoardSize;

        private readonly Dictionary<UpgradeJob, Dictionary<string, UpgradeButton>> boardButtons = new();
        private NetworkUpgradeService upgradeService;
        private InputAction toggleAction;
        private TMP_Text jobTitleText;
        private TMP_Text levelText;
        private TMP_Text experienceText;
        private UpgradeJob selectedJob = UpgradeJob.Gather;
        private PlayerControlStateMachine controlStateMachine;
        private CursorStateConfig? previousCursorState;
        private bool inputLockApplied;
        private bool boardsBuilt;

        public bool IsVisible => visuals != null && visuals.activeSelf;
        public UpgradeJob SelectedJob => selectedJob;

        private void Awake()
        {
            ResolveReferences();
            RegisterNavigationListeners();
            TryBindService();
            Hide();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindToggleAction();
            TryBindService();
        }

        private void Update()
        {
            if (upgradeService == null || !boardsBuilt)
                TryBindService();
            if (boardsBuilt && gatherBoard != null && gatherBoard.transform is RectTransform board &&
                board.rect.size != lastBoardSize)
                FitBoards();
        }

        private void OnDisable()
        {
            if (toggleAction != null)
            {
                toggleAction.performed -= HandleTogglePerformed;
                toggleAction.Disable();
                toggleAction = null;
            }

            UnbindService();
            RestoreCursorState();
            ReleaseInputLock();
        }

        private void OnDestroy()
        {
            if (gatherButton != null)
                gatherButton.onClick.RemoveListener(ShowGatherBoard);
            if (processorButton != null)
                processorButton.onClick.RemoveListener(ShowProcessorBoard);
            if (builderButton != null)
                builderButton.onClick.RemoveListener(ShowBuilderBoard);
        }

        public void Show()
        {
            ResolveReferences();
            TryBindService();
            if (visuals == null || IsVisible)
                return;

            RefreshSelectedBoard();
            previousCursorState = CursorStateConfig.Current;
            CursorStateConfig.UnlockedVisible.Apply();
            if (controlStateMachine != null)
            {
                controlStateMachine.SetModalInputLocked(true);
                inputLockApplied = true;
            }
            visuals.SetActive(true);
        }

        public void Hide()
        {
            if (visuals != null)
                visuals.SetActive(false);
            RestoreCursorState();
            ReleaseInputLock();
        }

        public void Toggle()
        {
            if (IsVisible)
                Hide();
            else
                Show();
        }

        public void ShowGatherBoard() => SelectBoard(UpgradeJob.Gather);
        public void ShowProcessorBoard() => SelectBoard(UpgradeJob.Process);
        public void ShowBuilderBoard() => SelectBoard(UpgradeJob.Build);

        private void SelectBoard(UpgradeJob job)
        {
            selectedJob = job;
            if (gatherBoard != null)
                gatherBoard.SetActive(job == UpgradeJob.Gather);
            if (processorBoard != null)
                processorBoard.SetActive(job == UpgradeJob.Process);
            if (builderBoard != null)
                builderBoard.SetActive(job == UpgradeJob.Build);
            RefreshSelectedBoard();
        }

        private void BuildBoards()
        {
            if (upgradeService == null || upgradeService.Config == null || prefab == null)
                return;

            ClearBoards();
            BuildBoard(UpgradeJob.Gather, gatherBoard);
            BuildBoard(UpgradeJob.Process, processorBoard);
            BuildBoard(UpgradeJob.Build, builderBoard);
            boardsBuilt = true;
            FitBoards();
            SelectBoard(selectedJob);
        }

        private void BuildBoard(UpgradeJob job, GameObject board)
        {
            Dictionary<string, UpgradeButton> views = new();
            boardButtons[job] = views;
            if (board == null)
                return;

            IReadOnlyList<UpgradeData> upgrades = upgradeService.Config.GetUpgrades(job);
            for (int i = 0; i < upgrades.Count; i++)
            {
                UpgradeData data = upgrades[i];
                if (data == null || string.IsNullOrWhiteSpace(data.Id) || views.ContainsKey(data.Id))
                    continue;

                GameObject instance = Instantiate(prefab, board.transform);
                instance.name = data.IsLevelFiftyUpgrade
                    ? "Upgrade_50"
                    : $"Upgrade_{data.Row}_{data.Column}";
                UpgradeButton view = instance.GetComponent<UpgradeButton>();
                if (view == null)
                {
                    Debug.LogError("The upgrade prefab requires an UpgradeButton component.", instance);
                    Destroy(instance);
                    continue;
                }

                RectTransform rect = instance.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                    Rect available = ((RectTransform)board.transform).rect;
                    rect.sizeDelta = UpgradeBoardLayout.CellSize(available);
                    rect.anchoredPosition = UpgradeBoardLayout.Position(data, available, rect.sizeDelta);
                }

                string upgradeId = data.Id;
                view.Configure(data, () => RequestPurchase(job, upgradeId),
                    UpgradeButtonState.CannotBuy);
                views.Add(data.Id, view);
            }
        }

        private void ClearBoards()
        {
            foreach (GameObject board in new[] { gatherBoard, processorBoard, builderBoard })
            {
                if (board == null)
                    continue;
                for (int i = board.transform.childCount - 1; i >= 0; i--)
                    Destroy(board.transform.GetChild(i).gameObject);
            }
            boardButtons.Clear();
        }

        private void FitBoards()
        {
            foreach (GameObject board in new[] { gatherBoard, processorBoard, builderBoard })
            {
                if (board == null || board.transform is not RectTransform boardRect) continue;
                Vector2 size = UpgradeBoardLayout.CellSize(boardRect.rect);
                foreach (UpgradeButton view in board.GetComponentsInChildren<UpgradeButton>(true))
                {
                    if (view.Data == null) continue;
                    RectTransform rect = (RectTransform)view.transform;
                    rect.sizeDelta = size;
                    rect.anchoredPosition = UpgradeBoardLayout.Position(view.Data, boardRect.rect, size);
                }
            }
            if (gatherBoard != null) lastBoardSize = ((RectTransform)gatherBoard.transform).rect.size;
        }

        private void RequestPurchase(UpgradeJob job, string upgradeId)
        {
            upgradeService?.RequestPurchase(job, upgradeId);
        }

        private void RefreshSelectedBoard()
        {
            if (upgradeService == null || upgradeService.Config == null || !boardsBuilt)
                return;

            UpgradeJobSnapshot snapshot = upgradeService.GetLocalSnapshot(selectedJob);
            HashSet<string> purchased = new(snapshot.PurchasedUpgradeIds ?? System.Array.Empty<string>());
            if (boardButtons.TryGetValue(selectedJob, out Dictionary<string, UpgradeButton> views))
            {
                foreach (KeyValuePair<string, UpgradeButton> pair in views)
                {
                    UpgradeButton view = pair.Value;
                    UpgradeData data = view != null ? view.Data : null;
                    if (data == null)
                        continue;

                    bool isPurchased = purchased.Contains(data.Id);
                    bool isRevealed = isPurchased || IsRevealed(data, purchased);
                    // Keep the roadmap visible while retaining server-authoritative reveal/purchase rules.
                    view.gameObject.SetActive(true);
                    view.SetupUpgradeData(data);
                    if (!isRevealed) view.SetLabel(data.IsLevelFiftyUpgrade ? "Level 50\nCapstone" : "Unexplored\n- -");
                    if (isPurchased)
                        view.SetState(UpgradeButtonState.Purchased);
                    else
                        view.SetState(isRevealed && snapshot.AvailablePoints > 0
                            ? UpgradeButtonState.CanBuy
                            : UpgradeButtonState.CannotBuy);
                }
            }

            if (jobTitleText != null)
                jobTitleText.text = GetJobTitle(selectedJob);
            SetTab(gatherButton, selectedJob == UpgradeJob.Gather);
            SetTab(processorButton, selectedJob == UpgradeJob.Process);
            SetTab(builderButton, selectedJob == UpgradeJob.Build);
            if (boardHint != null)
                boardHint.text = snapshot.AvailablePoints > 0
                    ? "Choose a blue node to grow your skills."
                    : "Earn experience to unlock your next upgrade.";
            if (experienceFill != null)
            {
                int required = UpgradeJobProgress.GetExperienceRequired(snapshot.Level);
                experienceFill.fillAmount = required > 0 ? Mathf.Clamp01((float)snapshot.Experience / required) : 1f;
            }
            if (levelText != null)
                levelText.text = $"Lv {snapshot.Level} | Pts {snapshot.AvailablePoints}";
            if (experienceText != null)
            {
                int required = UpgradeJobProgress.GetExperienceRequired(snapshot.Level);
                experienceText.text = required > 0
                    ? $"XP {snapshot.Experience}/{required}"
                    : "XP MAX";
            }
        }

        private static void SetTab(Button tab, bool selected)
        {
            if (tab == null) return;
            var image = tab.GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.color = selected ? new Color(.24f, .84f, .48f, 1f) : Color.white;
        }

        private bool IsRevealed(UpgradeData data, HashSet<string> purchased)
        {
            UpgradeTreeConfig config = upgradeService.Config;
            if (data.IsLevelFiftyUpgrade)
            {
                UpgradeData prerequisite = config.GetGridUpgrade(selectedJob,
                    UpgradeTreeConfig.LevelFiftyPrerequisiteRow,
                    UpgradeTreeConfig.LevelFiftyPrerequisiteColumn);
                return prerequisite != null && purchased.Contains(prerequisite.Id);
            }

            if (data.Row == UpgradeTreeConfig.CenterCoordinate &&
                data.Column == UpgradeTreeConfig.CenterCoordinate)
                return true;

            return IsPurchasedAt(config, data.Row - 1, data.Column, purchased) ||
                   IsPurchasedAt(config, data.Row + 1, data.Column, purchased) ||
                   IsPurchasedAt(config, data.Row, data.Column - 1, purchased) ||
                   IsPurchasedAt(config, data.Row, data.Column + 1, purchased);
        }

        private bool IsPurchasedAt(UpgradeTreeConfig config, int row, int column,
            HashSet<string> purchased)
        {
            if (row < 0 || row >= UpgradeTreeConfig.GridSize ||
                column < 0 || column >= UpgradeTreeConfig.GridSize)
                return false;

            UpgradeData adjacent = config.GetGridUpgrade(selectedJob, row, column);
            return adjacent != null && purchased.Contains(adjacent.Id);
        }

        private void TryBindService()
        {
            NetworkUpgradeService candidate = NetworkUpgradeService.Instance;
            if (candidate == null)
                candidate = FindFirstObjectByType<NetworkUpgradeService>(FindObjectsInactive.Include);
            if (candidate == null)
                return;

            if (upgradeService != candidate)
            {
                UnbindService();
                upgradeService = candidate;
                upgradeService.LocalProgressChanged += HandleProgressChanged;
                boardsBuilt = false;
            }

            if (!boardsBuilt && upgradeService.Config != null)
                BuildBoards();
        }

        private void UnbindService()
        {
            if (upgradeService != null)
                upgradeService.LocalProgressChanged -= HandleProgressChanged;
            upgradeService = null;
        }

        private void HandleProgressChanged(UpgradeJob job, UpgradeJobSnapshot snapshot)
        {
            if (job == selectedJob)
                RefreshSelectedBoard();
        }

        private void ResolveReferences()
        {
            jobTitleText = jobTitle != null ? jobTitle.GetComponent<TMP_Text>() : null;
            Transform visualTransform = visuals != null ? visuals.transform : transform;
            levelText = visualTransform.Find("Level")?.GetComponent<TMP_Text>();
            experienceText = visualTransform.Find("Experience")?.GetComponent<TMP_Text>();
            if (inputActions == null)
            {
                InventoryUI inventory = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
                inputActions = inventory != null ? inventory.InputActions : null;
            }
            if (controlStateMachine == null)
                controlStateMachine = FindFirstObjectByType<PlayerControlStateMachine>(FindObjectsInactive.Include);
        }

        private void RegisterNavigationListeners()
        {
            if (gatherButton != null)
                gatherButton.onClick.AddListener(ShowGatherBoard);
            if (processorButton != null)
                processorButton.onClick.AddListener(ShowProcessorBoard);
            if (builderButton != null)
                builderButton.onClick.AddListener(ShowBuilderBoard);
        }

        private void BindToggleAction()
        {
            if (toggleAction != null)
                return;

            toggleAction = inputActions?.FindAction(toggleActionName, false);
            if (toggleAction == null)
            {
                Debug.LogWarning($"Input action '{toggleActionName}' was not found for the upgrade menu.", this);
                return;
            }

            toggleAction.performed += HandleTogglePerformed;
            toggleAction.Enable();
        }

        private void HandleTogglePerformed(InputAction.CallbackContext context)
        {
            InterfaceManager.Instance?.ToggleUpgrade();
        }

        private void RestoreCursorState()
        {
            if (!previousCursorState.HasValue)
                return;
            previousCursorState.Value.Apply();
            previousCursorState = null;
        }

        private void ReleaseInputLock()
        {
            if (!inputLockApplied)
                return;
            controlStateMachine?.SetModalInputLocked(false);
            inputLockApplied = false;
        }

        private static string GetJobTitle(UpgradeJob job)
        {
            return job switch
            {
                UpgradeJob.Gather => "Gather",
                UpgradeJob.Process => "Processor",
                UpgradeJob.Build => "Builder",
                _ => job.ToString()
            };
        }
    }
}
