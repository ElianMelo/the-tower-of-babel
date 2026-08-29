using NaughtyAttributes;
using TowerOfBabel.World.Chunks;
using UnityEngine;

/// <summary>Editor-only coordinator that converts temporary generated objects into runtime tower data.</summary>
[DisallowMultipleComponent]
public sealed class ConfigureTower : MonoBehaviour
{
    [SerializeField] private TowerGenerator towerGenerator;
    [SerializeField] private ChunkManager chunkManager;

    public bool IsRunning { get; private set; }

#if UNITY_EDITOR
    private ConfigurationStep step;

    [Button("Configure Tower Data")]
    public void RunConfiguration()
    {
        if (IsRunning)
        {
            Debug.LogWarning("Tower configuration is already running.", this);
            return;
        }

        ResolveReferences();
        if (towerGenerator == null || chunkManager == null)
        {
            Debug.LogError("ConfigureTower needs a TowerGenerator and ChunkManager in the opened scene.", this);
            return;
        }

        if (!ValidateConfiguration())
            return;

        IsRunning = true;
        step = ConfigurationStep.ClearInitialAssets;
        UnityEditor.EditorApplication.update += AdvanceConfiguration;
        Debug.Log("Tower configuration started.", this);
    }

    private void AdvanceConfiguration()
    {
        try
        {
            switch (step)
            {
                case ConfigurationStep.ClearInitialAssets:
                    towerGenerator.ClearGeneratedTower();
                    step = ConfigurationStep.WaitForInitialClear;
                    break;

                case ConfigurationStep.WaitForInitialClear:
                    if (!towerGenerator.IsBusy)
                        step = ConfigurationStep.GenerateAssets;
                    break;

                case ConfigurationStep.GenerateAssets:
                    towerGenerator.GenerateTower();
                    step = ConfigurationStep.WaitForGeneration;
                    break;

                case ConfigurationStep.WaitForGeneration:
                    if (!towerGenerator.IsBusy)
                        step = ConfigurationStep.CacheData;
                    break;

                case ConfigurationStep.CacheData:
                    EnsureRuntimeRenderers();
                    chunkManager.ConfigureAssetPrefabs(towerGenerator);
                    chunkManager.CacheChunks();
                    if (chunkManager.CachedAssetCount == 0)
                        throw new System.InvalidOperationException(
                            "Tower generation produced no cacheable assets. Temporary assets were left in the scene for inspection.");
                    step = ConfigurationStep.DestroyTemporaryAssets;
                    break;

                case ConfigurationStep.DestroyTemporaryAssets:
                    towerGenerator.ClearGeneratedTower();
                    step = ConfigurationStep.WaitForFinalClear;
                    break;

                case ConfigurationStep.WaitForFinalClear:
                    if (!towerGenerator.IsBusy)
                        CompleteConfiguration();
                    break;
            }
        }
        catch (System.Exception exception)
        {
            StopConfiguration();
            Debug.LogException(exception, this);
        }
    }

    private void EnsureRuntimeRenderers()
    {
        if (chunkManager.GetComponent<NearTowerAssetPool>() == null)
            chunkManager.gameObject.AddComponent<NearTowerAssetPool>();
        if (chunkManager.GetComponent<InstancedFarChunkRenderer>() == null)
            chunkManager.gameObject.AddComponent<InstancedFarChunkRenderer>();
    }

    private bool ValidateConfiguration()
    {
        if (towerGenerator.TowerParent == null)
        {
            Debug.LogError("ConfigureTower cannot run because TowerGenerator has no Tower Parent.", this);
            return false;
        }

        if (chunkManager.TowerRoot != towerGenerator.TowerParent.gameObject)
        {
            Debug.LogError(
                "ConfigureTower requires ChunkManager Tower Root and TowerGenerator Tower Parent to reference the same object.",
                this);
            return false;
        }

        TowerOfBabel.World.Tower.TowerAssetType[] assetTypes =
        {
            TowerOfBabel.World.Tower.TowerAssetType.Floor,
            TowerOfBabel.World.Tower.TowerAssetType.Stair,
            TowerOfBabel.World.Tower.TowerAssetType.Pillar,
            TowerOfBabel.World.Tower.TowerAssetType.Arch
        };
        for (int i = 0; i < assetTypes.Length; i++)
        {
            if (towerGenerator.GetModelPrefab(assetTypes[i]) != null)
                continue;

            Debug.LogError($"ConfigureTower cannot run because no {assetTypes[i]} prefab is assigned.", this);
            return false;
        }

        return true;
    }

    private void CompleteConfiguration()
    {
        UnityEditor.EditorUtility.SetDirty(chunkManager);
        UnityEditor.EditorUtility.SetDirty(this);
        if (chunkManager.gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(chunkManager.gameObject.scene);

        int chunkCount = chunkManager.CachedChunks.Count;
        int assetCount = chunkManager.CachedAssetCount;
        StopConfiguration();
        Debug.Log(
            $"Tower configuration completed. Cached {assetCount} assets in {chunkCount} chunks as data and removed temporary assets.",
            this);
    }

    private void StopConfiguration()
    {
        UnityEditor.EditorApplication.update -= AdvanceConfiguration;
        IsRunning = false;
        step = ConfigurationStep.Idle;
    }

    private void ResolveReferences()
    {
        if (towerGenerator == null)
            towerGenerator = FindFirstObjectByType<TowerGenerator>(FindObjectsInactive.Include);
        if (chunkManager == null)
            chunkManager = FindFirstObjectByType<ChunkManager>(FindObjectsInactive.Include);
    }

    private void OnDisable()
    {
        if (IsRunning)
            StopConfiguration();
    }

    private enum ConfigurationStep : byte
    {
        Idle,
        ClearInitialAssets,
        WaitForInitialClear,
        GenerateAssets,
        WaitForGeneration,
        CacheData,
        DestroyTemporaryAssets,
        WaitForFinalClear
    }
#endif
}
