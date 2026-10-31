using System;
using TowerOfBabel.Networking.Buildings;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Resources;
using TowerOfBabel.Resources.Interaction;
using TowerOfBabel.Upgrades;
using TowerOfBabel.World.Chunks;
using UnityEngine;

namespace TowerOfBabel.Buildings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class Building : MonoBehaviour, IInteractable, IServerAuthoritativeInteractable,
        IInteractionPresentation, IInteractionResource
    {
        public const int StageCount = ChunkAssetData.CompletedStage + 1;

        private static readonly Color AvailableColor = new(0.9f, 0.65f, 0.15f);
        private static readonly Color UnavailableColor = Color.red;
        private static readonly Color CompleteColor = Color.green;

        [Header("Build Cost")]
        [SerializeField] private ResourceType resourceType = ResourceType.Stone;
        [SerializeField, Min(1)] private int resourceCostPerStage = 1;

        [Header("Interaction")]
        [SerializeField, Min(0.01f)] private float interactionDuration = 3f;
        [SerializeField, Min(0f), InspectorName("Weave Strength")] private float shakeStrength = 0.05f;
        [SerializeField, Min(0f), InspectorName("Weave Tempo")]
        [Tooltip("Higher values weave faster; 24 is one gentle cycle in about 2.6 seconds.")]
        private float shakeSpeed = 24f;
        [SerializeField] private Transform visuals;
        [SerializeField] private TowerOfBabel.Outline outline;

        [Header("Stage Meshes (0-10)")]
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private Mesh[] stageMeshes = new Mesh[StageCount];

        private ChunkManager chunkManager;
        private ChunkKey chunkKey;
        private int localIndex = -1;
        private byte currentStage;
        private AeroInteractionMotion interactionMotion;
        private PillarConstructionMotion constructionMotion;
        private bool isBound;
        private bool interactionFocused;
        private bool scanHighlighted;

        public string ObjectName => gameObject.name;
        public ResourceType InteractionResourceType => resourceType;
        public InteractionResourceData InteractionResources => InteractionResourceData.Build(resourceType,EffectiveResourceCost);
        public string DetailText => IsComplete
            ? "Construction complete"
            : $"Stage {currentStage}/{ChunkAssetData.CompletedStage} - {EffectiveResourceCost} {ResourceTypeNames.Display(resourceType)}";
        public Color DetailColor => IsComplete ? CompleteColor : HasLocalResources ? AvailableColor : UnavailableColor;
        public bool InstantBuildEnabled => NetworkUpgradeService.Instance != null && NetworkUpgradeService.Instance.GetLocalCheatStat(TowerOfBabel.Cheats.CheatStat.InstantBuild) > .5f;
        public string PromptText => IsComplete
            ? "Complete"
            : InstantBuildEnabled ? "Press 'E' - instant build" : HasLocalResources ? "Press 'E'" : $"Need {EffectiveResourceCost} {ResourceTypeNames.Display(resourceType)}";
        public float Duration => EffectiveInteractionDuration;
        public bool CanInteract => IsConstructionAvailable && (InstantBuildEnabled || HasLocalResources);
        public bool IsConstructionAvailable => isActiveAndEnabled && isBound && !IsComplete &&
            chunkManager != null && chunkManager.CanConstructAsset(chunkKey, localIndex);
        public ResourceType ResourceType => resourceType;
        public int ResourceCostPerStage => resourceCostPerStage;
        public float InteractionDuration => interactionDuration;
        public int EffectiveResourceCost
        {
            get
            {
                NetworkUpgradeService upgrades = NetworkUpgradeService.Instance;
                if (InstantBuildEnabled) return 0;
                return upgrades != null
                    ? upgrades.GetLocalActionCost(UpgradeJob.Build, resourceCostPerStage)
                    : resourceCostPerStage;
            }
        }
        public float EffectiveInteractionDuration
        {
            get
            {
                NetworkUpgradeService upgrades = NetworkUpgradeService.Instance;
                if (InstantBuildEnabled) return .01f;
                return upgrades != null
                    ? upgrades.GetLocalActionDuration(UpgradeJob.Build, interactionDuration)
                    : interactionDuration;
            }
        }
        public float ShakeStrength => shakeStrength;
        public float ShakeSpeed => shakeSpeed;
        public byte CurrentStage => currentStage;
        public bool IsComplete => currentStage >= ChunkAssetData.CompletedStage;
        public bool IsBound => isBound;
        public bool ShouldShowInteraction => IsConstructionAvailable;
        public ChunkKey ChunkKey => chunkKey;
        public int LocalIndex => localIndex;
        public event Action ServerRejected;
        public event Action InteractionPresentationChanged;

        private bool HasLocalResources
        {
            get
            {
                NetworkResourceService resources = NetworkResourceService.Instance;
                return resources != null && resources.GetLocalAmount(resourceType) >= EffectiveResourceCost;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyStageMesh(currentStage);
        }

        public void Bind(ChunkManager owner, ChunkKey key, int assetLocalIndex, byte stage)
        {
            Unbind();
            chunkManager = owner;
            chunkKey = key;
            localIndex = assetLocalIndex;
            isBound = owner != null && assetLocalIndex >= 0;
            if (chunkManager != null) chunkManager.AddFloorListener(this);
            ApplyStage(stage,false);
        }

        public void Unbind()
        {
            if (chunkManager != null) chunkManager.RemoveFloorListener(this);
            interactionFocused = false;
            scanHighlighted = false;
            interactionMotion?.Stop(true);
            constructionMotion?.ResetImmediately();
            outline?.SetVisible(false);
            chunkManager = null;
            localIndex = -1;
            isBound = false;
        }

        public void ApplyStage(byte stage) => ApplyStage(stage,true);
        public void ApplyStageImmediately(byte stage) => ApplyStage(stage, false);

        private void ApplyStage(byte stage,bool animate)
        {
            ResolveReferences();
            byte previousStage=currentStage;
            Mesh previousMesh=meshFilter!=null ? meshFilter.sharedMesh : null;
            byte nextStage = stage > ChunkAssetData.CompletedStage
                ? ChunkAssetData.CompletedStage
                : stage;
            bool changed = currentStage != nextStage;
            currentStage = nextStage;
            ApplyStageMesh(currentStage);
            if (changed) constructionMotion?.ConfirmStage(previousMesh,previousStage,currentStage,
                animate && Application.isPlaying && isActiveAndEnabled);

            RefreshOutline();
            if (changed)
                InteractionPresentationChanged?.Invoke();
        }

        public void SetInteractionFocused(bool focused)
        {
            interactionFocused = focused;
            RefreshOutline();
        }

        public void SetScanHighlighted(bool highlighted)
        {
            scanHighlighted = highlighted;
            RefreshOutline();
        }

        private void RefreshOutline()
        {
            outline?.SetVisible((interactionFocused || scanHighlighted) && IsConstructionAvailable);
        }

        /// <summary>Slot in ChunkManager's floor listener registry; -1 when not registered.</summary>
        public int FloorListenerIndex { get; internal set; } = -1;
        public void NotifyConstructionFloorChanged() => HandleConstructionFloorChanged();

        private void HandleConstructionFloorChanged()
        {
            RefreshOutline();
            InteractionPresentationChanged?.Invoke();
        }

        public Mesh GetStageMesh(byte stage)
        {
            int index = Mathf.Clamp(stage, 0, ChunkAssetData.CompletedStage);
            if (stageMeshes != null && index < stageMeshes.Length && stageMeshes[index] != null)
                return stageMeshes[index];
            return meshFilter != null ? meshFilter.sharedMesh : null;
        }

        public void BeginInteraction(GameObject interactor)
        {
            ResolveReferences();
            if (constructionMotion!=null) { constructionMotion.BeginWork(currentStage); return; }
            if (interactionMotion == null)
                interactionMotion = GetComponent<AeroInteractionMotion>() ?? gameObject.AddComponent<AeroInteractionMotion>();
            interactionMotion.Begin(visuals, shakeStrength, shakeSpeed);
        }

        public void UpdateInteraction(float normalizedProgress)
        {
            interactionMotion?.SetProgress(normalizedProgress);
            constructionMotion?.SetProgress(normalizedProgress);
        }

        public void CancelInteraction()
        {
            RestoreVisualPosition();
            constructionMotion?.CancelWork();
        }

        public void CompleteInteraction(GameObject interactor)
        {
            RestoreVisualPosition();
            constructionMotion?.FinishWork();
            NetworkBuildingService.Instance?.NotifyLocalInteractionFinished(this);
        }

        public bool RequestServerStart(GameObject interactor)
        {
            return NetworkBuildingService.Instance != null &&
                   NetworkBuildingService.Instance.RequestBuildStart(this, interactor.transform.position);
        }

        public void RequestServerCancel()
        {
            NetworkBuildingService.Instance?.RequestBuildCancel(this);
        }

        public void RejectByServer()
        {
            constructionMotion?.ResetImmediately();
            ServerRejected?.Invoke();
        }

        private void ApplyStageMesh(byte stage)
        {
            ResolveReferences();
            Mesh mesh = GetStageMesh(stage);
            if (meshFilter != null && mesh != null)
                meshFilter.sharedMesh = mesh;
            if (TryGetComponent<GlassWaterFloorLight>(out var river)) river.RefreshPresentation();
        }

        private void ResolveReferences()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();
            if (visuals == null)
                visuals = transform;
            if (outline == null)
                outline = GetComponent<TowerOfBabel.Outline>();
            if (constructionMotion == null)
                constructionMotion = GetComponent<PillarConstructionMotion>();
        }

        private void RestoreVisualPosition()
        {
            interactionMotion?.Stop();
        }

        private void OnDisable()
        {
            interactionFocused = false;
            scanHighlighted = false;
            interactionMotion?.Stop(true);
            constructionMotion?.ResetImmediately();
            outline?.SetVisible(false);
        }

        private void OnDestroy()
        {
            if (chunkManager != null) chunkManager.RemoveFloorListener(this);
        }

        private void OnValidate()
        {
            resourceCostPerStage = Mathf.Max(1, resourceCostPerStage);
            interactionDuration = Mathf.Max(0.01f, interactionDuration);
            shakeStrength = Mathf.Max(0f, shakeStrength);
            shakeSpeed = Mathf.Max(0f, shakeSpeed);
            ResolveReferences();

            if (stageMeshes == null || stageMeshes.Length != StageCount)
            {
                Mesh[] resized = new Mesh[StageCount];
                if (stageMeshes != null)
                    Array.Copy(stageMeshes, resized, Mathf.Min(stageMeshes.Length, resized.Length));
                stageMeshes = resized;
            }
        }
    }
}
