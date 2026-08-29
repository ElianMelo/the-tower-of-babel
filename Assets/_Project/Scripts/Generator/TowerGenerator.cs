using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
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

    public void FillCurrentLevelFloor(float floorRadius, float baseLevel)
    {
        float currentRadius = floorRadius;
        float currentTileAngle = tileAngle;
        int index = 0;
        while (currentRadius > 0)
        {
            currentTileAngle = tileAngle / (currentRadius / radius);

            float angleRad = currentTileAngle * Mathf.Deg2Rad;
            for (float angle = 0f; angle < Mathf.PI * 2f; angle += angleRad)
            {
                Vector2 point = CalculatePillarPoint(angle, currentRadius);
                Vector2 dir = (center - point).normalized;
                float yAngle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                yAngle -= 90f;
                CreateTowerAsset(
                    floorTileObject,
                    TowerAssetType.Floor,
                    new Vector3(point.x, baseLevel + (index * 0.0001f), point.y),
                    Quaternion.Euler(0, yAngle, 0));
                index++;
            }
            currentRadius -= tileDecreaseRate;
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

    private IEnumerator DelayedRemoveBlockingFloor()
    {
        yield return new WaitForSeconds(1f);
        RemoveBlockingFloor();
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
        while(parent.childCount > 0)
        {
            foreach (Transform child in parent)
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    [Button]
    public void GenerateTower()
    {
        baseHeight = 0f;
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
            var calculatedFloorRadius = i % 2 == 0 ? currentRadius + decreaseAmount : currentRadius;
            calculatedFloorRadius = i == 0 ? currentRadius : calculatedFloorRadius;
            FillCurrentLevelFloor(calculatedFloorRadius, baseHeight + 0.2f);
            if(i < (levels - 1))
            {
                FillCurrentLevelStairs(currentRadius, baseHeight + 0.2f);
            }            

            baseHeight += heighIncrease;
        }
        StartCoroutine(DelayedRemoveBlockingFloor());
    }

    private Vector2 CalculatePillarPoint(float value, float radius)
    {
        return center + new Vector2(
            Mathf.Cos(value),
            Mathf.Sin(value)
        ) * radius;
    }

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
