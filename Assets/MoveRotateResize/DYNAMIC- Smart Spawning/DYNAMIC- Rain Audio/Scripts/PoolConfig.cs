//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.2.1
//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections.Generic;
using MoveRotateResize.Dynamic.MultiTag;

namespace MoveRotateResize.Dynamic.RainAudio
{
    [System.Serializable]
    public class PoolConfig
    {
        public SpawnInteriorAudio spawn;
        public Tag poolTag;
        public int initialPoolSize = 3;
        public int maxPoolSize = 5;
        public bool useCollectionChecks = true;
        public List<AudioClip> audioClips;
    }
}