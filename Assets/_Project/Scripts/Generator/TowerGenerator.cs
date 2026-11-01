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
    public GameObject glassWaterFloorObject;
    public GameObject stairStepObject;

    [Header("Detail phase")]
    [Tooltip("Buildable bush released after a floor's structure is complete.")]
    public GameObject detailBushObject;
    [Tooltip("One bush is placed beside a random pillar in every group of this many pillars.")]
    [Min(1)] public int detailBushPillarInterval = 6;
    public int detailBushSeed = 2417;

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
    /// <summary>Radial half-thickness of the arch: its outer face sits this far outside the wall ring.</summary>
    public float ArchOuterHalfDepth
    {
        get
        {
            var mesh = archObject != null ? archObject.GetComponent<TowerOfBabel.Buildings.Building>()?.GetStageMesh(10) : null;
            if (mesh == null) return .2f;
            var b = mesh.bounds; return Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)) * archObject.transform.localScale.z;
        }
    }
    // The ground floor ends just past its pillar bases. Every upper floor rests on the arcade below and ends
    // flush with that arcade's outer arch face instead of overhanging it.
    public float GetFloorOuterRadius(int floor) =>
        floor <= 0 ? GetWallRadius(0) + PillarSupportRadius : GetWallRadius(floor - 1) + ArchOuterHalfDepth;

    public void FillCurrentLevelFloor(float floorRadius, float baseLevel)
    {
        float arcLength = radius * tileAngle * Mathf.Deg2Rad;
        Mesh template = TowerFloorLayout.SupportsRadialFloor(floorTileObject)
            ? floorTileObject.GetComponent<MeshFilter>().sharedMesh : null;
        Mesh walkingTemplate=floorTileObject.GetComponent<TowerOfBabel.Buildings.Building>()?.GetStageMesh(10) ?? template;
        foreach (var sector in TowerFloorLayout.Build(center, baseLevel, floorRadius,
                     tileDecreaseRate, arcLength, floorTileObject.transform.localScale.y))
        {
            int floor = Mathf.Max(0, Mathf.RoundToInt((baseLevel - .2f) / heighIncrease));
            bool glass = glassWaterFloorObject != null && GlassWaterFloorLayout.IsChannel(
                GlassWaterFloorLayout.TileCenter(sector.Position, sector.Rotation, sector.Scale, center), GetWallRadius(floor), tileDecreaseRate);
            var prefab = glass ? glassWaterFloorObject : floorTileObject;
            GameObject instance = CreateTowerAsset(prefab, glass ? TowerAssetType.GlassWaterFloor : TowerAssetType.Floor, sector.Position, sector.Rotation);
            float thickness = sector.Scale.y * (glass ? 1f : TowerFloorLayout.DeckThicknessStretch);
            instance.transform.localScale = new Vector3(sector.Scale.x / parent.lossyScale.x,
                thickness / parent.lossyScale.y, sector.Scale.z / parent.lossyScale.z);
            if (template != null)
            {
                Mesh mesh = floorMeshes.Get(prefab.GetComponent<MeshFilter>().sharedMesh, sector.Radius, sector.Scale);
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
            for (int i = 0; i < levels; i++)
            {
                currentRadius -= i % 2 == 0 ? decreaseAmount : 0;
                foreach (var bay in RingLayout(i))
                {
                    CreateTowerAsset(
                        pillarObject,
                        TowerAssetType.Pillar,
                        bay.Pillar,
                        Quaternion.identity,
                        "Pillar " + bay.Angle);
                    GameObject arch = CreateTowerAsset(
                        archObject,
                        TowerAssetType.Arch,
                        bay.Arch,
                        bay.ArchRotation);
                    // Stretch along the bay so the arch ends exactly on the next pillar's axis.
                    Vector3 scale = arch.transform.localScale;
                    arch.transform.localScale = new Vector3(scale.x * bay.ArchScale, scale.y, scale.z);
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
            CreateDetailBushes();
            // Decoration data survives ConfigureTower's removal of temporary construction objects.
            GetComponent<TowerBushScatter>()?.Generate(this);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public readonly struct RingBay
    {
        public readonly float Angle; public readonly Vector3 Pillar, Arch; public readonly Quaternion ArchRotation; public readonly float ArchScale;
        public RingBay(float angle, Vector3 pillar, Vector3 arch, Quaternion archRotation, float archScale)
        { Angle = angle; Pillar = pillar; Arch = arch; ArchRotation = archRotation; ArchScale = archScale; }
    }

    /// <summary>
    /// Pillar and arch placement for one level. A whole number of bays closes the ring exactly; the old float
    /// loop added an overlapping extra pillar and arch where the angle wrapped on about half the floors.
    /// </summary>
    public IEnumerable<RingBay> RingLayout(int level)
    {
        float ringRadius = GetWallRadius(level);
        int bays = PillarsPerRing(pillarAngle / (ringRadius / radius));
        float step = Mathf.PI * 2f / bays;
        float archScale = ArchChordScale(ringRadius, bays, radius, pillarAngle);
        float height = level * heighIncrease;
        for (int bay = 0; bay < bays; bay++)
        {
            float angle = bay * step;
            Vector2 point = CalculatePillarPoint(angle, ringRadius);
            Vector2 dir = (CalculatePillarPoint(angle + step, ringRadius) - point).normalized;
            float yAngle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg - 90f;
            yield return new RingBay(angle, new Vector3(point.x, height, point.y), new Vector3(point.x, height + yOffset, point.y),
                Quaternion.Euler(0, yAngle, 0), archScale);
        }
    }

    /// <summary>Whole number of pillar bays closest to the requested angular spacing.</summary>
    public static int PillarsPerRing(float pillarAngleDegrees) =>
        Mathf.Max(3, Mathf.RoundToInt(360f / Mathf.Max(pillarAngleDegrees, .01f)));

    /// <summary>
    /// X scale that fits the arch, modelled for the base ring's chord, to a ring of <paramref name="bays"/> bays.
    /// </summary>
    public static float ArchChordScale(float ringRadius, int bays, float baseRadius, float basePillarAngleDegrees)
    {
        float modelled = 2f * baseRadius * Mathf.Sin(basePillarAngleDegrees * Mathf.Deg2Rad * .5f);
        float chord = 2f * ringRadius * Mathf.Sin(Mathf.PI / bays);
        return modelled > 0f ? chord / modelled : 1f;
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
        var archMesh=archObject!=null?archObject.GetComponent<TowerOfBabel.Buildings.Building>()?.GetStageMesh(10):null;
        var arches=archMesh!=null?new TowerArchSupport(assets.Where(a=>a.AssetType==TowerAssetType.Arch)
            .Select(a=>new ChunkAssetData(0,TowerAssetType.Arch,a.transform.position,a.transform.rotation,a.transform.lossyScale)),archMesh.bounds):null;
        var groups = assets.Where(a => a.AssetType.IsFloor()).GroupBy(a => Mathf.FloorToInt((a.transform.position.y + .001f) / heighIncrease));
        var completed = floorTileObject.GetComponent<TowerOfBabel.Buildings.Building>()?.GetStageMesh(10) ?? floorTileObject.GetComponent<MeshFilter>().sharedMesh;
        var template = floorTileObject.GetComponent<MeshFilter>().sharedMesh;
        foreach (var group in groups)
        {
            var data = group.Select(a => new ChunkAssetData(0, TowerAssetType.Floor, a.transform.position, a.transform.rotation, a.transform.lossyScale)).ToArray();
            var support = new TowerFloorSupport(data, center, completed.bounds.max.y);
            if (!pillars.TryGetValue(group.Key, out var levelPillars)) levelPillars = System.Array.Empty<TowerAsset>();
            var ledge=new TowerPillarLedge(levelPillars.Select(p=>p.transform.position),center,PillarSupportRadius);
            var mainland=new TowerFloorSupport(data,center,completed.bounds.max.y);
            foreach (var pillar in levelPillars)
            {
                var point = new Vector2(pillar.transform.position.x, pillar.transform.position.z);
                if (support.SupportsDisk(point, PillarSupportRadius - .05f, out _) || arches?.SupportsBase(pillar.transform.position)==true) continue;
                var sector = TowerFloorLayout.SupportPad(center, data[0].Position.y, point, PillarSupportRadius, data[0].Scale.y);
                if (group.Key > 0 && GetWallRadius(group.Key) < GetWallRadius(group.Key - 1) - .01f)
                {
                    if (!ledge.TryFindInnerConnection(mainland,point,out float connection))
                        throw new System.InvalidOperationException($"No connected deck behind pillar at {point}.");
                    sector = ledge.Bridge(point,data[0].Position.y,connection,data[0].Scale.y,mainland);
                }
                var instance = CreateTowerAsset(floorTileObject, TowerAssetType.Floor, sector.Position, sector.Rotation, "Pillar support deck");
                float padThickness = floorTileObject.transform.localScale.y * TowerFloorLayout.DeckThicknessStretch;
                instance.transform.localScale = new Vector3(sector.Scale.x / parent.lossyScale.x, padThickness / parent.lossyScale.y, sector.Scale.z / parent.lossyScale.z);
                if (TowerFloorLayout.SupportsRadialFloor(floorTileObject))
                {
                    instance.GetComponent<MeshFilter>().sharedMesh = floorMeshes.Get(template, sector.Radius, sector.Scale);
                    var collider = instance.GetComponent<MeshCollider>(); if (collider != null) collider.sharedMesh = floorMeshes.Get(completed, sector.Radius, sector.Scale);
                }
                support.Add(new ChunkAssetData(0, TowerAssetType.Floor, sector.Position, sector.Rotation, sector.Scale));
            }
        }
    }

    private void CreateDetailBushes()
    {
        if (detailBushObject == null) return;
        var assets = parent.GetComponentsInChildren<TowerAsset>()
            .Select(a => new ChunkAssetData(0, a.AssetType, a.transform.position, a.transform.rotation, a.transform.lossyScale));
        foreach (var bush in BuildDetailBushes(assets))
        {
            var instance = CreateTowerAsset(detailBushObject, TowerAssetType.Bush, bush.Position, bush.Rotation, "Detail bush");
            instance.transform.localScale = new Vector3(bush.Scale.x / parent.lossyScale.x,
                bush.Scale.y / parent.lossyScale.y, bush.Scale.z / parent.lossyScale.z);
        }
    }

    /// <summary>
    /// Deterministic detail-phase bushes: one beside a random pillar in every group of
    /// <see cref="detailBushPillarInterval"/>, nudged by a small random offset, resting on the deck and clear of stairs.
    /// </summary>
    public List<ChunkAssetData> BuildDetailBushes(IEnumerable<ChunkAssetData> towerAssets)
    {
        var result = new List<ChunkAssetData>();
        if (detailBushObject == null || heighIncrease <= 0) return result;
        Mesh bushMesh = detailBushObject.GetComponent<TowerOfBabel.Buildings.Building>()?.GetStageMesh(10)
            ?? detailBushObject.GetComponent<MeshFilter>()?.sharedMesh;
        if (bushMesh == null) return result;
        Bounds bushBounds = bushMesh.bounds;
        float bushRadius = new Vector2(Mathf.Max(Mathf.Abs(bushBounds.min.x), Mathf.Abs(bushBounds.max.x)),
            Mathf.Max(Mathf.Abs(bushBounds.min.z), Mathf.Abs(bushBounds.max.z))).magnitude;
        Vector3 baseScale = detailBushObject.transform.localScale;
        Mesh floorMesh = floorTileObject != null
            ? floorTileObject.GetComponent<TowerOfBabel.Buildings.Building>()?.GetStageMesh(10) ?? floorTileObject.GetComponent<MeshFilter>()?.sharedMesh
            : null;
        float floorTop = floorMesh != null ? floorMesh.bounds.max.y : 0f;
        Mesh stairMesh = stairStepObject != null
            ? stairStepObject.GetComponent<TowerOfBabel.Buildings.Building>()?.GetStageMesh(10) ?? stairStepObject.GetComponent<MeshFilter>()?.sharedMesh
            : null;

        int FloorOf(ChunkAssetData a) => Mathf.FloorToInt((a.Position.y + .001f) / heighIncrease);
        var all = towerAssets.ToArray();
        var floorsByLevel = all.Where(a => a.AssetType.IsFloor()).GroupBy(FloorOf).ToDictionary(g => g.Key, g => g.ToArray());
        var stairs = all.Where(a => a.AssetType == TowerAssetType.Stair).ToArray();
        float pillarClearance = PillarSupportRadius;
        foreach (var level in all.Where(a => a.AssetType == TowerAssetType.Pillar).GroupBy(FloorOf).OrderBy(g => g.Key))
        {
            if (!floorsByLevel.TryGetValue(level.Key, out var floors)) continue;
            var support = new TowerFloorSupport(floors, center, floorTop);
            var pillars = level.OrderBy(p => Mathf.Repeat(Mathf.Atan2(p.Position.z - center.y, p.Position.x - center.x), Mathf.PI * 2)).ToArray();
            var random = new System.Random(unchecked(detailBushSeed + level.Key * 7919));
            int interval = Mathf.Max(1, detailBushPillarInterval);
            for (int group = 0; group < pillars.Length; group += interval)
            {
                var pillar = pillars[Mathf.Min(pillars.Length - 1, group + random.Next(interval))];
                float size = Range(random, .85f, 1.15f);
                Vector3 scale = baseScale * size;
                float footprint = bushRadius * Mathf.Max(scale.x, scale.z);
                Quaternion rotation = Quaternion.Euler(0, Range(random, 0, 360), 0);
                Vector2 point = new(pillar.Position.x, pillar.Position.z);
                Vector2 outward = (point - center).normalized;
                Vector2 tangent = new(-outward.y, outward.x);
                float sideways = Range(random, -.35f, .35f);
                float gap = pillarClearance + footprint + Range(random, .05f, .25f);
                // Prefer the inner (walkable) side; fall back to the outer ledge when it has room.
                foreach (float side in random.NextDouble() < .75 ? new[] { -1f, 1f } : new[] { 1f, -1f })
                {
                    Vector2 candidate = point + outward * (side * gap) + tangent * sideways;
                    if (!support.SupportsDisk(candidate, footprint, out float surface)) continue;
                    float bottom = surface - bushBounds.min.y * scale.y - TowerBushScatter.RootEmbedDepth;
                    float top = bottom + bushBounds.max.y * scale.y;
                    if (stairMesh != null && OverlapsStair(stairs, stairMesh.bounds, candidate, footprint + .35f, bottom, top)) continue;
                    result.Add(new ChunkAssetData(0, TowerAssetType.Bush, new Vector3(candidate.x, bottom, candidate.y), rotation, scale));
                    break;
                }
            }
        }
        return result;
    }

    private static bool OverlapsStair(ChunkAssetData[] stairs, Bounds mesh, Vector2 point, float radius, float bottom, float top)
    {
        foreach (var stair in stairs)
        {
            Vector3 c = Vector3.Scale(mesh.center, stair.Scale), e = Vector3.Scale(mesh.extents, stair.Scale);
            float stairBottom = stair.Position.y + c.y - e.y, stairTop = stair.Position.y + c.y + e.y;
            // Steps well overhead leave head room; anything near walking height must stay clear.
            if (stairTop < bottom - .2f || stairBottom > top + .6f) continue;
            Vector3 local = Quaternion.Inverse(stair.Rotation) * (new Vector3(point.x, stair.Position.y, point.y) - stair.Position) - c;
            float x = Mathf.Max(0, Mathf.Abs(local.x) - e.x), z = Mathf.Max(0, Mathf.Abs(local.z) - e.z);
            if (x * x + z * z < radius * radius) return true;
        }
        return false;
    }

    private static float Range(System.Random random, float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());

    private void OnDestroy() => floorMeshes.Dispose();

    public GameObject GetModelPrefab(TowerAssetType assetType)
    {
        return assetType switch
        {
            TowerAssetType.Floor => floorTileObject,
            TowerAssetType.GlassWaterFloor => glassWaterFloorObject,
            TowerAssetType.Stair => stairStepObject,
            TowerAssetType.Pillar => pillarObject,
            TowerAssetType.Arch => archObject,
            TowerAssetType.Bush => detailBushObject,
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
