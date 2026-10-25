using System.Collections.Generic;
using TowerOfBabel.World.Chunks;
using UnityEngine;

namespace TowerOfBabel.World.Tower
{
    /// <summary>Checks contact with the continuous upper beam of the tower's upright arch mesh.</summary>
    public sealed class TowerArchSupport
    {
        private const float BucketHeight=.25f;
        public const float ContactTolerance=.08f;
        private readonly Bounds roof;
        private readonly Dictionary<int,List<Bearing>> levels=new();
        private readonly struct Bearing
        {
            public readonly ChunkAssetData asset;
            public readonly Matrix4x4 inverse;
            public Bearing(ChunkAssetData asset) {this.asset=asset;inverse=asset.ObjectToWorld.inverse;}
        }
        public TowerArchSupport(IEnumerable<ChunkAssetData> assets,Bounds completedArchBounds)
        {
            roof=completedArchBounds;
            foreach(var asset in assets)
            {
                if(asset.AssetType!=TowerAssetType.Arch || Mathf.Abs(asset.Scale.x*asset.Scale.y*asset.Scale.z)<.00001f ||
                    Vector3.Dot(asset.Rotation*Vector3.up,Vector3.up)<.999f)continue;
                float top=asset.ObjectToWorld.MultiplyPoint3x4(new Vector3(roof.center.x,roof.max.y,roof.center.z)).y;
                int key=Mathf.RoundToInt(top/BucketHeight);
                if(!levels.TryGetValue(key,out var list))levels.Add(key,list=new List<Bearing>());
                list.Add(new Bearing(asset));
            }
        }
        public bool SupportsBase(Vector3 pillarBase)=>TryGetSupport(pillarBase,out _);
        public bool TryGetSupport(Vector3 pillarBase,out ChunkAssetData arch)
        {
            int key=Mathf.RoundToInt(pillarBase.y/BucketHeight);
            for(int offset=-1;offset<=1;offset++)
            {
                if(!levels.TryGetValue(key+offset,out var list))continue;
                foreach(var bearing in list)
                {
                    Vector3 local=bearing.inverse.MultiplyPoint3x4(pillarBase);
                    if(Mathf.Abs((local.y-roof.max.y)*bearing.asset.Scale.y)>ContactTolerance ||
                        local.x<roof.min.x+.015f || local.x>roof.max.x-.015f ||
                        local.z<roof.min.z+.015f || local.z>roof.max.z-.015f)continue;
                    arch=bearing.asset;return true;
                }
            }
            arch=default;return false;
        }
    }
}
