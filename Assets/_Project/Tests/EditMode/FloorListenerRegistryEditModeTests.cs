using System.Collections.Generic;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.World.Tests
{
    /// <summary>Lag-spike fix: pooled buildings register in O(1) instead of multicast `+=`.</summary>
    public sealed class FloorListenerRegistryEditModeTests
    {
        [Test]
        public void BindingAndUnbindingKeepsAConsistentRegistryWithoutDelegateSubscriptions()
        {
            var root = new GameObject("Listener registry"); root.SetActive(false);
            var manager = root.AddComponent<ChunkManager>(); manager.enabled = false;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Floor_Tile.prefab");
            var buildings = new List<Building>();
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    var b = Object.Instantiate(prefab, root.transform).GetComponent<Building>();
                    b.Bind(manager, new ChunkKey(0, 0, 0), i, 10); buildings.Add(b);
                }
                Assert.That(manager.FloorListenerCount, Is.EqualTo(6));
                Assert.That(typeof(ChunkManager).GetField("ConstructionFloorChanged",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(manager), Is.Null,
                    "Buildings must not use the multicast event (O(n) copy per subscribe).");
                buildings[1].Unbind(); buildings[4].Unbind(); buildings[1].Unbind();
                Assert.That(manager.FloorListenerCount, Is.EqualTo(4));
                Assert.That(buildings[1].FloorListenerIndex, Is.EqualTo(-1));
                var seen = new HashSet<int>();
                foreach (var b in buildings)
                    if (b.FloorListenerIndex >= 0) Assert.That(seen.Add(b.FloorListenerIndex), Is.True, "Swap-remove keeps indices unique.");
                Assert.That(seen.Count, Is.EqualTo(4));
                buildings[1].Bind(manager, new ChunkKey(0, 0, 0), 1, 10);
                Assert.That(manager.FloorListenerCount, Is.EqualTo(5));
                // Rebinding to another asset keeps a single registration.
                buildings[0].Bind(manager, new ChunkKey(0, 0, 0), 7, 10);
                Assert.That(manager.FloorListenerCount, Is.EqualTo(5));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
