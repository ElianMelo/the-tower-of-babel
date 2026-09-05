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
        IInteractionPresentation
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
        [SerializeField, Min(0f)] private float shakeStrength = 0.05f;
        [SerializeField, Min(0f)] private float shakeSpeed = 24f;
        [SerializeField] private Transform visuals;
        [SerializeField] private TowerOfBabel.Outline outline;

        [Header("Stage Meshes (0-10)")]
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private Mesh[] stageMeshes = new Mesh[StageCount];

        private ChunkManager chunkManager;
        private ChunkKey chunkKey;
        private int localIndex = -1;
        private byte currentStage;
        private Vector3 visualsStartPosition;
        private bool isBound;

        public string ObjectName => gameObject.name;
        public string DetailText => IsComplete
            ? "Construction complete"
            : $"Stage {currentStage}/{ChunkAssetData.CompletedStage} - {EffectiveResourceCost} {resourceType}";
        public Color DetailColor => IsComplete ? CompleteColor : HasLocalResources ? AvailableColor : UnavailableColor;
        public string PromptText => IsComplete
            ? "Complete"
            : HasLocalResources ? "Press 'E'" : $"Need {EffectiveResourceCost} {resourceType}";
        public float Duration => EffectiveInteractionDuration;
        public bool CanInteract => enabled && gameObject.activeInHierarchy && isBound && !IsComplete && HasLocalResources;
        public ResourceType ResourceType => resourceType;
        public int ResourceCostPerStage => resourceCostPerStage;
        public float InteractionDuration => interactionDuration;
        public int EffectiveResourceCost
        {
            get
            {
                NetworkUpgradeService upgrades = NetworkUpgradeService.Instance;
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
        public bool ShouldShowInteraction => !IsComplete;
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
            visualsStartPosition = visuals.localPosition;
            ApplyStageMesh(currentStage);
        }

        public void Bind(ChunkManager owner, ChunkKey key, int assetLocalIndex, byte stage)
        {
            chunkManager = owner;
            chunkKey = key;
            localIndex = assetLocalIndex;
            isBound = owner != null && assetLocalIndex >= 0;
            ApplyStage(stage);
        }

        public void Unbind()
        {
            RestoreVisualPosition();
            outline?.SetVisible(false);
            chunkManager = null;
            localIndex = -1;
            isBound = false;
        }

        public void ApplyStage(byte stage)
        {
            byte nextStage = stage > ChunkAssetData.CompletedStage
                ? ChunkAssetData.CompletedStage
                : stage;
            bool changed = currentStage != nextStage;
            currentStage = nextStage;
            ApplyStageMesh(currentStage);

            if (IsComplete)
                outline?.SetVisible(false);
            if (changed)
                InteractionPresentationChanged?.Invoke();
        }

        public void SetInteractionFocused(bool focused)
        {
            outline?.SetVisible(focused && ShouldShowInteraction);
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
            visualsStartPosition = visuals.localPosition;
        }

        public void UpdateInteraction(float normalizedProgress)
        {
            float phase = Time.time * shakeSpeed;
            Vector3 offset = new(Mathf.Sin(phase), 0f, Mathf.Cos(phase * 0.83f));
            visuals.localPosition = visualsStartPosition + offset * shakeStrength;
        }

        public void CancelInteraction()
        {
            RestoreVisualPosition();
        }

        public void CompleteInteraction(GameObject interactor)
        {
            RestoreVisualPosition();
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
            ServerRejected?.Invoke();
        }

        private void ApplyStageMesh(byte stage)
        {
            ResolveReferences();
            Mesh mesh = GetStageMesh(stage);
            if (meshFilter != null && mesh != null)
                meshFilter.sharedMesh = mesh;
        }

        private void ResolveReferences()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();
            if (visuals == null)
                visuals = transform;
            if (outline == null)
                outline = GetComponent<TowerOfBabel.Outline>();
        }

        private void RestoreVisualPosition()
        {
            if (visuals != null)
                visuals.localPosition = visualsStartPosition;
        }

        private void OnDisable()
        {
            RestoreVisualPosition();
            outline?.SetVisible(false);
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
