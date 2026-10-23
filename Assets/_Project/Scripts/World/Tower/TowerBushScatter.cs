using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.World.Tower
{
    /// <summary>Deterministic exterior-terrace detail, separate from construction and chunk save data.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(TowerGenerator))]
    public sealed class TowerBushScatter : MonoBehaviour
    {
        [SerializeField] private GameObject[] bushPrefabs = new GameObject[3];
        [SerializeField] private int seed = 7319;
        [SerializeField, Range(0,1)] private float terraceCoverage = .78f;
        [SerializeField, Min(5)] private float averageClusterSpacing = 58;
        [SerializeField, Min(0)] private float wallClearance = .55f;
        [SerializeField, Min(0)] private float edgeClearance = .15f;
        [SerializeField, Min(0)] private float stairClearance = .7f;
        [SerializeField, Min(0)] private float windAllowance = .2f;
        [SerializeField, Min(10)] private float visibleDistance = 150;
        [SerializeField, HideInInspector] private List<Placement> placements = new();
        private readonly List<Batch> batches = new();
        private readonly Plane[] planes = new Plane[6];

        [Serializable]
        public struct Placement
        {
            public int variant, floor;
            public Vector3 position, scale;
            public float yaw, footprint, height, wallRadius, floorRadius;
            public Matrix4x4 Matrix => Matrix4x4.TRS(position,Quaternion.Euler(0,yaw,0),scale);
        }

        private readonly struct StairBox
        {
            public readonly Vector3 position, center, extents;
            public readonly Quaternion inverse;
            public StairBox(Vector3 p, Quaternion rotation, Vector3 scale, Bounds bounds)
            {
                position=p; inverse=Quaternion.Inverse(rotation);
                center=Vector3.Scale(bounds.center,scale); extents=Vector3.Scale(bounds.extents,scale);
            }
            public bool Overlaps(Placement plant,float clearance)
            {
                float bottom=position.y+center.y-extents.y, top=position.y+center.y+extents.y;
                // Keep the arrival and departure walking space clear, including stairs just below a terrace.
                if (top<plant.position.y-1.5f || bottom>plant.position.y+plant.height+.6f) return false;
                Vector3 local=inverse*(plant.position-position)-center;
                float x=Mathf.Max(0,Mathf.Abs(local.x)-extents.x), z=Mathf.Max(0,Mathf.Abs(local.z)-extents.z);
                float radius=plant.footprint+clearance;
                return x*x+z*z<radius*radius;
            }
        }

        private sealed class Batch
        {
            public Mesh mesh;
            public Material material;
            public Matrix4x4[] matrices;
            public Bounds bounds;
        }

        public IReadOnlyList<Placement> Placements => placements;
        public IReadOnlyList<GameObject> BushPrefabs => bushPrefabs;
        public float StairClearance => stairClearance;
        public int BatchCount => batches.Count;

        [ContextMenu("Regenerate exterior bushes")]
        private void Regenerate() => Generate(GetComponent<TowerGenerator>());

        public void Generate(TowerGenerator generator)
        {
            if (generator==null || generator.TowerParent==null || generator.heighIncrease<=0 || generator.stairHeight<=0)
                throw new InvalidOperationException("Assign a valid tower generator before scattering bushes.");
            if (generator.TowerParent.lossyScale!=Vector3.one)
                throw new InvalidOperationException("Bush placement expects the tower's unit-scale authoring parent.");
            if (bushPrefabs==null || bushPrefabs.Length!=3 || Array.Exists(bushPrefabs,p=>p==null))
                throw new InvalidOperationException("Assign small, medium and large bush prefabs.");
            float[] radii=new float[3], heights=new float[3];
            for (int i=0; i<3; i++)
            {
                var prefab=bushPrefabs[i];
                if (prefab.GetComponentInChildren<Collider>(true)!=null)
                    throw new InvalidOperationException("Bush prefabs must not have colliders.");
                var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;
                foreach (var v in mesh.vertices) radii[i]=Mathf.Max(radii[i],new Vector2(v.x,v.z).magnitude);
                heights[i]=mesh.bounds.max.y;
            }
            var result=new List<Placement>();
            var radiusByFloor=new float[Mathf.Max(0,generator.levels)];
            float current=generator.radius;
            for (int floor=0; floor<radiusByFloor.Length; floor++)
            {
                if (floor%2==0) current-=generator.decreaseAmount;
                radiusByFloor[floor]=current;
            }
            for (int floor=1; floor<radiusByFloor.Length; floor++)
            {
                float wall=radiusByFloor[floor];
                float outer=floor%2==0 ? wall+generator.decreaseAmount : wall;
                if (wall<=0 || outer-wall<.5f) continue;
                var random=new System.Random(unchecked(seed+floor*104729));
                if (random.NextDouble()>terraceCoverage) continue;
                var stairs=StairsNearFloor(generator,floor,radiusByFloor);
                int clusters=Mathf.Max(1,Mathf.RoundToInt(2*Mathf.PI*outer/averageClusterSpacing*Range(random,.65f,1.2f)));
                int attempts=clusters*18, placedClusters=0;
                for (int attempt=0; attempt<attempts && placedClusters<clusters; attempt++)
                {
                    float angle=Range(random,0,Mathf.PI*2);
                    int clusterSize=random.Next(1,4), count=0;
                    for (int member=0; member<clusterSize; member++)
                    {
                        int variant=random.NextDouble()<.2 ? 2 : random.NextDouble()<.48 ? 1 : 0;
                        float scale=Range(random,.82f,1.18f);
                        Vector3 size=new(scale*Range(random,.9f,1.1f),scale*Range(random,.9f,1.1f),scale*Range(random,.9f,1.1f));
                        float footprint=radii[variant]*Mathf.Max(size.x,size.z)+windAllowance*size.y;
                        float low=wall+wallClearance+footprint, high=outer-edgeClearance-footprint;
                        if (high<low) continue;
                        float a=angle+(member==0 ? 0 : Range(random,-4.5f,4.5f)/outer);
                        float r=Range(random,low,high);
                        var candidate=new Placement { variant=variant,floor=floor,position=new Vector3(generator.center.x+Mathf.Cos(a)*r,
                            floor*generator.heighIncrease+.2f,generator.center.y+Mathf.Sin(a)*r),scale=size,yaw=Range(random,0,360),
                            footprint=footprint,height=heights[variant]*size.y,wallRadius=wall,floorRadius=outer };
                        if (OverlapsStairs(candidate,stairs) || OverlapsPlants(candidate,result)) continue;
                        result.Add(candidate); count++;
                    }
                    if (count>0) placedClusters++;
                }
            }
            placements=result; RebuildBatches();
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private bool OverlapsStairs(Placement plant,List<StairBox> stairs)
        {
            foreach (var stair in stairs) if (stair.Overlaps(plant,stairClearance)) return true;
            return false;
        }

        private static bool OverlapsPlants(Placement plant,List<Placement> others)
        {
            foreach (var other in others)
            {
                if (other.floor!=plant.floor) continue;
                float spacing=plant.footprint+other.footprint+.25f;
                if ((other.position-plant.position).sqrMagnitude<spacing*spacing) return true;
            }
            return false;
        }

        private static List<StairBox> StairsNearFloor(TowerGenerator g,int floor,float[] radii)
        {
            var result=new List<StairBox>();
            var mesh=g.stairStepObject.GetComponent<TowerOfBabel.Buildings.Building>()?.GetStageMesh(10)
                ?? g.stairStepObject.GetComponent<MeshFilter>().sharedMesh;
            int count=Mathf.CeilToInt(g.floorHeight/g.stairHeight);
            int range=Mathf.CeilToInt(g.floorHeight/g.heighIncrease)+1;
            for (int level=Mathf.Max(0,floor-range); level<=Mathf.Min(g.levels-2,floor+1); level++)
            for (int flight=0; flight<4; flight++)
            for (int step=0; step<count; step++)
            {
                float y=level*g.heighIncrease+.2f+step*g.stairHeight;
                if (y<floor*g.heighIncrease-2 || y>floor*g.heighIncrease+3) continue;
                float a=(flight+(float)step/count)*Mathf.PI*.5f;
                float next=(flight+(float)(step+1)/count)*Mathf.PI*.5f;
                var p=g.center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radii[level];
                var q=g.center+new Vector2(Mathf.Cos(next),Mathf.Sin(next))*radii[level];
                Vector2 direction=(q-p).normalized;
                Quaternion rotation=Quaternion.Euler(0,Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg-180,0);
                result.Add(new StairBox(new Vector3(p.x,y,p.y),rotation,g.stairStepObject.transform.localScale,mesh.bounds));
            }
            return result;
        }

        private void OnEnable() => RebuildBatches();
        private void OnDisable() => batches.Clear();
        private void LateUpdate()
        {
#if !UNITY_SERVER
            if (Application.isPlaying && Camera.main!=null) Draw(Camera.main);
#endif
        }

        public void RebuildBatches()
        {
            batches.Clear();
            if (bushPrefabs==null) return;
            var groups=new Dictionary<(int variant,int floor),List<Placement>>();
            foreach (var plant in placements)
            {
                if (plant.variant<0 || plant.variant>=bushPrefabs.Length || bushPrefabs[plant.variant]==null) continue;
                var key=(plant.variant,plant.floor);
                if (!groups.TryGetValue(key,out var list)) groups[key]=list=new List<Placement>();
                list.Add(plant);
            }
            foreach (var pair in groups)
            {
                var prefab=bushPrefabs[pair.Key.variant]; var list=pair.Value;
                for (int start=0; start<list.Count; start+=1023)
                {
                    int count=Mathf.Min(1023,list.Count-start);
                    var batch=new Batch {mesh=prefab.GetComponent<MeshFilter>().sharedMesh,material=prefab.GetComponent<MeshRenderer>().sharedMaterial,
                        matrices=new Matrix4x4[count],bounds=new Bounds(list[start].position,Vector3.zero)};
                    for (int j=0; j<count; j++)
                    {
                        var plant=list[start+j]; batch.matrices[j]=plant.Matrix;
                        var bounds=new Bounds(plant.position+Vector3.up*plant.height*.5f,
                            new Vector3(plant.footprint*2,plant.height+.3f,plant.footprint*2));
                        batch.bounds.Encapsulate(bounds);
                    }
                    batches.Add(batch);
                }
            }
        }

        public void Draw(Camera camera)
        {
            if (camera==null) return;
            GeometryUtility.CalculateFrustumPlanes(camera,planes);
            foreach (var batch in batches)
            {
                if (batch.bounds.SqrDistance(camera.transform.position)>visibleDistance*visibleDistance ||
                    !GeometryUtility.TestPlanesAABB(planes,batch.bounds)) continue;
                var settings=new RenderParams(batch.material) {camera=camera,worldBounds=batch.bounds,layer=gameObject.layer,
                    shadowCastingMode=ShadowCastingMode.On,receiveShadows=true,lightProbeUsage=LightProbeUsage.Off};
                Graphics.RenderMeshInstanced(settings,batch.mesh,0,batch.matrices);
            }
        }

        private static float Range(System.Random random,float low,float high) => Mathf.Lerp(low,high,(float)random.NextDouble());
    }
}
