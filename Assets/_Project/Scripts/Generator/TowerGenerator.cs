using NaughtyAttributes;
using System.Collections.Generic;
using System.Linq;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;

public class TowerGenerator : MonoBehaviour
{
    public Transform parent;
    public GameObject pillarObject;
    public GameObject archObject;
    public GameObject floorTileObject;
    public GameObject stairStepObject;

    public float radius;
    public float pillarAngle;
    public float tileAngle;
    public float yOffset;
    public float tileDecreaseRate;

    public float radiusFactor;
    public float heighIncrease;
    public int levels;
    public float decreaseAmount;

    public float stairHeight;
    public float floorHeight;

    public Vector2 center = Vector2.zero;
    public Vector3 center3D = Vector3.zero;

    private float baseHeight = 0f;
    private List<Vector3> stairs = new();
    private readonly RadialFloorMeshCache floorMeshes = new();

    public bool IsBusy { get; private set; }
    public Transform TowerParent => parent;
    public float GetWallRadius(int floor) => radius - decreaseAmount * (floor / 2 + 1);
    public float PillarSupportRadius
    {
        get
        {
            var mesh = pillarObject != null ? pillarObject.GetComponent<TowerOfBabel.Buildings.Building>()?.GetStageMesh(10) : null;
            if (mesh == null) return .65f;
            var bounds = mesh.bounds; var scale = pillarObject.transform.localScale;
            return new Vector2(Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)) * scale.x,
                Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)) * scale.z).magnitude + .05f;
        }
    }
    public float GetFloorOuterRadius(int floor) => Mathf.Max(GetWallRadius(floor), GetWallRadius(Mathf.Max(0, floor - 1))) + PillarSupportRadius;

    public void FillCurrentLevelFloor(float floorRadius, float baseLevel)
    {
        float arcLength = radius * tileAngle * Mathf.Deg2Rad;
        Mesh template = TowerFloorLayout.SupportsRadialFloor(floorTileObject)
            ? floorTileObject.GetComponent<MeshFilter>().sharedMesh : null;
        Mesh walkingTemplate=floorTileObject.GetComponent<TowerOfBabel.Buildings.Building>()?.GetStageMesh(10) ?? template;
        foreach (var sector in TowerFloorLayout.Build(center, baseLevel, floorRadius,
                     tileDecreaseRate, arcLength, floorTileObject.transform.localScale.y))
        {
            GameObject instance = CreateTowerAsset(floorTileObject, TowerAssetType.Floor, sector.Position, sector.Rotation);
            instance.transform.localScale = new Vector3(sector.Scale.x / parent.lossyScale.x,
                sector.Scale.y / parent.lossyScale.y, sector.Scale.z / parent.lossyScale.z);
            if (template != null)
            {
                Mesh mesh = floorMeshes.Get(template, sector.Radius, sector.Scale);
                instance.GetComponent<MeshFilter>().sharedMesh = mesh;
                var collider = instance.GetComponent<MeshCollider>();
                if (collider != null) collider.sharedMesh = floorMeshes.Get(walkingTemplate,sector.Radius,sector.Scale);
            }
        }
    }

    public void FillCurrentLevelStairs(float floorRadius, float baseLevel)
    {
        // Number of steps needed to climb from this floor to the next one
        int stepCount = Mathf.CeilToInt(floorHeight / stairHeight);
        if (stepCount <= 0) return;

        float totalSpinRad = 90f * Mathf.Deg2Rad;

        // Four staircases starting at 0, 90, 180 and 270 degrees, each spinning
        // 90 degrees as it climbs to the next floor
        for (int flight = 0; flight < 4; flight++)
        {
            float startAngleRad = flight * 90f * Mathf.Deg2Rad;                                                           

            for (int step = 0; step < stepCount; step++)
            {
                float t = (float)step / stepCount;
                float nextT = (float)(step + 1) / stepCount;

                float angle = startAngleRad + t * totalSpinRad;
                float nextAngle = startAngleRad + nextT * totalSpinRad;

                Vector2 point = CalculatePillarPoint(angle, floorRadius);
                Vector2 nextPoint = CalculatePillarPoint(nextAngle, floorRadius);

                float y = baseLevel + step * stairHeight;

                Vector2 dir = (nextPoint - point).normalized;
                float yAngle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                yAngle -= 180f;
                GameObject obj = CreateTowerAsset(
                    stairStepObject,
                    TowerAssetType.Stair,
                    new Vector3(point.x, y, point.y),
                    Quaternion.Euler(0, yAngle, 0),
                    "Stair " + flight + "_" + step);
                stairs.Add(obj.transform.position);
            }
        }
    }

    private void RemoveBlockingFloor()
    {
        foreach (var stair in stairs)
        {
            Vector3 direction = stair - center3D;
            direction.y = 0;
            direction = direction.normalized;
            float directionMod = -1f;

            Vector3 origin = stair + (Vector3.up * 0.2f) + (direction * directionMod);
            float rayLength = 2f;
            bool didHit = Physics.Raycast(origin, Vector3.up, out RaycastHit hit, rayLength);
            Debug.DrawRay(origin, Vector3.up * rayLength, didHit ? Color.green : Color.red, 10f);
            if (didHit)
            {
                DestroyImmediate(hit.collider.gameObject);
            }
        }
        stairs.Clear();
    }

    [Button]
    public void ClearGeneratedTower()
    {
        if (IsBusy)
            throw new System.InvalidOperationException("TowerGenerator is already processing an authoring operation.");
        if (parent == null)
            throw new System.InvalidOperationException("TowerGenerator needs a Tower Parent.");

        IsBusy = true;
        try
        {
            while(parent.childCount > 0)
            {
                for (int i = parent.childCount - 1; i >= 0; i--)
                    DestroyImmediate(parent.GetChild(i).gameObject);
            }
            stairs.Clear();
            floorMeshes.Dispose();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [Button]
    public void GenerateTower()
    {
        if (IsBusy)
            throw new System.InvalidOperationException("TowerGenerator is already processing an authoring operation.");
        if (parent == null)
            throw new System.InvalidOperationException("TowerGenerator needs a Tower Parent.");

        IsBusy = true;
        try
        {
            baseHeight = 0f;
            stairs.Clear();
            float currentRadius = radius;
            float currentPillarAngle = pillarAngle;
            for (int i = 0; i < levels; i++)
            {
                currentRadius -= i % 2 == 0 ? decreaseAmount : 0;
                currentPillarAngle = pillarAngle / (currentRadius / radius);

                float angleRad = currentPillarAngle * Mathf.Deg2Rad;
                for (float angle = 0f; angle < Mathf.PI * 2f; angle += angleRad)
                {
                    Vector2 point = CalculatePillarPoint(angle, currentRadius);
                    Vector2 nextPoint = CalculatePillarPoint(angle + angleRad, currentRadius);
                    Vector2 dir = (nextPoint - point).normalized;
                    float yAngle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                    yAngle -= 90f;
                    CreateTowerAsset(
                        pillarObject,
                        TowerAssetType.Pillar,
                        new Vector3(point.x, baseHeight, point.y),
                        Quaternion.identity,
                        "Pillar " + angle);
                    CreateTowerAsset(
                        archObject,
                        TowerAssetType.Arch,
                        new Vector3(point.x, baseHeight + yOffset, point.y),
                        Quaternion.Euler(0, yAngle, 0));
                }
                var calculatedFloorRadius = GetFloorOuterRadius(i);
                FillCurrentLevelFloor(calculatedFloorRadius, baseHeight + 0.2f);
                if(i < (levels - 1))
                {
                    FillCurrentLevelStairs(currentRadius, baseHeight + 0.2f);
                }

                baseHeight += heighIncrease;
            }
            Physics.SyncTransforms();
            RemoveBlockingFloor();
            EnsurePillarSupports();
            // Decoration data survives ConfigureTower's removal of temporary construction objects.
            GetComponent<TowerBushScatter>()?.Generate(this);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Vector2 CalculatePillarPoint(float value, float radius)
    {
        return center + new Vector2(
            Mathf.Cos(value),
            Mathf.Sin(value)
        ) * radius;
    }

    private void EnsurePillarSupports()
    {
        if (!TowerFloorLayout.SupportsRadialFloor(floorTileObject)) return;
        var assets = parent.GetComponentsInChildren<TowerAsset>();
        var pillars = assets.Where(a => a.AssetType == TowerAssetType.Pillar).GroupBy(a => Mathf.FloorToInt((a.transform.position.y + .001f) / heighIncrease)).ToDictionary(g => g.Key, g => g.ToArray());
        var groups = assets.Where(a => a.AssetType == TowerAssetType.Floor).GroupBy(a => Mathf.FloorToInt((a.transform.position.y + .001f) / heighIncrease));
        var completed = floorTileObject.GetComponent<TowerOfBabel.Buildings.Building>()?.GetStageMesh(10) ?? floorTileObject.GetComponent<MeshFilter>().sharedMesh;
        var template = floorTileObject.GetComponent<MeshFilter>().sharedMesh;
        foreach (var group in groups)
        {
            var data = group.Select(a => new ChunkAssetData(0, TowerAssetType.Floor, a.transform.position, a.transform.rotation, a.transform.lossyScale)).ToArray();
            var support = new TowerFloorSupport(data, center, completed.bounds.max.y);
            if (!pillars.TryGetValue(group.Key, out var levelPillars)) levelPillars = System.Array.Empty<TowerAsset>();
            foreach (var pillar in levelPillars)
            {
                var point = new Vector2(pillar.transform.position.x, pillar.transform.position.z);
                if (support.SupportsDisk(point, PillarSupportRadius - .05f, out _)) continue;
                var sector = TowerFloorLayout.SupportPad(center, data[0].Position.y, point, PillarSupportRadius, data[0].Scale.y);
                if (group.Key > 0 && GetWallRadius(group.Key) < GetWallRadius(group.Key - 1) - .01f)
                {
                    if (!support.TryFindInnerConnection(point, PillarSupportRadius, out float connection))
                        throw new System.InvalidOperationException($"No connected deck behind pillar at {point}.");
                    sector = TowerFloorLayout.SupportBridge(center, data[0].Position.y, point, PillarSupportRadius, connection, data[0].Scale.y);
                }
                var instance = CreateTowerAsset(floorTileObject, TowerAssetType.Floor, sector.Position, sector.Rotation, "Pillar support deck");
                instance.transform.localScale = new Vector3(sector.Scale.x / parent.lossyScale.x, sector.Scale.y / parent.lossyScale.y, sector.Scale.z / parent.lossyScale.z);
                if (TowerFloorLayout.SupportsRadialFloor(floorTileObject))
                {
                    instance.GetComponent<MeshFilter>().sharedMesh = floorMeshes.Get(template, sector.Radius, sector.Scale);
                    var collider = instance.GetComponent<MeshCollider>(); if (collider != null) collider.sharedMesh = floorMeshes.Get(completed, sector.Radius, sector.Scale);
                }
                support.Add(new ChunkAssetData(0, TowerAssetType.Floor, sector.Position, sector.Rotation, sector.Scale));
            }
        }
    }

    private void OnDestroy() => floorMeshes.Dispose();

    public GameObject GetModelPrefab(TowerAssetType assetType)
    {
        return assetType switch
        {
            TowerAssetType.Floor => floorTileObject,
            TowerAssetType.Stair => stairStepObject,
            TowerAssetType.Pillar => pillarObject,
            TowerAssetType.Arch => archObject,
            _ => null
        };
    }

    private GameObject CreateTowerAsset(GameObject modelPrefab, TowerAssetType assetType,
        Vector3 position, Quaternion rotation, string assetName = null)
    {
        if (modelPrefab == null)
            throw new System.InvalidOperationException($"No model prefab is assigned for {assetType}.");

        GameObject instance = Instantiate(modelPrefab, position, rotation, parent);
        TowerAsset marker = instance.GetComponent<TowerAsset>();
        if (marker == null)
            marker = instance.AddComponent<TowerAsset>();
        marker.SetAssetType(assetType);
        if (!string.IsNullOrEmpty(assetName))
            instance.name = assetName;
        return instance;
    }
}
