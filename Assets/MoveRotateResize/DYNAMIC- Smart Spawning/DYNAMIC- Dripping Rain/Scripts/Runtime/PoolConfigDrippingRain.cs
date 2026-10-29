//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.2.1
//------------------------------------------------------------------------------
using UnityEngine;
using MoveRotateResize.Dynamic.MultiTag;

namespace MoveRotateResize.Dynamic.DrippingRain
{
    [System.Serializable]
    public class PoolConfigDrippingRain
    {
        public SpawnDrippingRain spawn;
        public Tag poolTag;
        public int initialPoolSize = 3;
        public int maxPoolSize = 5;
        public bool useCollectionChecks = true;
    }
}