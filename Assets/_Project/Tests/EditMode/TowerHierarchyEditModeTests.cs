using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>Keeps the Tower scene hierarchy grouped so new objects land in a named folder.</summary>
    public sealed class TowerHierarchyEditModeTests
    {
        private const string TowerPath = "Assets/_Project/Scenes/Tower.unity";

        // NetworkSystem and IngameDebugConsole call DontDestroyOnLoad, which only works on root objects.
        // ResourceAuthority is an in-scene NetworkObject and stays beside the NetworkManager.
        private static readonly string[] Roots =
        {
            "NetworkSystem", "ResourceAuthority", "IngameDebugConsole", "Lighting & Atmosphere", "Player & Camera",
            "World", "Resources", "Processors", "UI", "Scratch (disabled)",
        };

        private static readonly string[] Groups = Roots.Skip(3).ToArray();

        [Test]
        public void TowerScene_GroupsRootObjectsIntoFolders()
        {
            WithTower(scene =>
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.Select(r => r.name), Is.EqualTo(Roots));
                foreach (var group in roots.Where(r => Groups.Contains(r.name)))
                {
                    Assert.That(group.GetComponents<Component>().Length, Is.EqualTo(1), group.name + " is a plain folder.");
                    Assert.That(group.transform.localPosition, Is.EqualTo(Vector3.zero), group.name);
                    Assert.That(group.transform.localRotation, Is.EqualTo(Quaternion.identity), group.name);
                    Assert.That(group.transform.localScale, Is.EqualTo(Vector3.one), group.name);
                    Assert.That(group.CompareTag("EditorOnly"), Is.False, group.name + " must ship in builds.");
                }
            });
        }

        [Test]
        public void TowerScene_PlacesObjectsInTheMatchingFolder()
        {
            WithTower(scene =>
            {
                var roots = scene.GetRootGameObjects();
                string RootOf(Component c) => c.transform.root.name;
                foreach (var r in roots.SelectMany(g => g.GetComponentsInChildren<Resource>(true))) Assert.That(RootOf(r), Is.EqualTo("Resources"), r.name);
                foreach (var p in roots.SelectMany(g => g.GetComponentsInChildren<ResourceProcessor>(true))) Assert.That(RootOf(p), Is.EqualTo("Processors"), p.name);
                foreach (var c in roots.SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).Where(c => c.transform.parent == null || c.transform.parent.GetComponentInParent<Canvas>(true) == null))
                    Assert.That(new[] { "UI", "IngameDebugConsole" }, Does.Contain(RootOf(c)), c.name);
                foreach (var t in roots.SelectMany(g => g.GetComponentsInChildren<Terrain>(true))) Assert.That(RootOf(t), Is.EqualTo("World"), t.name);
                foreach (var l in roots.SelectMany(g => g.GetComponentsInChildren<Light>(true))) Assert.That(RootOf(l), Is.EqualTo("Lighting & Atmosphere"), l.name);
                foreach (var cam in roots.SelectMany(g => g.GetComponentsInChildren<Camera>(true))) Assert.That(RootOf(cam), Is.EqualTo("Player & Camera"), cam.name);
                var network = roots.SelectMany(g => g.GetComponentsInChildren<FishNet.Managing.NetworkManager>(true)).Single();
                Assert.That(network.transform.parent, Is.Null, "NetworkManager uses DontDestroyOnLoad.");
                for (int i = 1; i <= 4; i++)
                    Assert.That(roots.Single(g => g.name == "World").transform.Find("Terrain markers/Marker" + i), Is.Not.Null);
                foreach (var scratch in roots.Single(g => g.name == "Scratch (disabled)").transform.Cast<Transform>())
                    Assert.That(scratch.gameObject.activeSelf && scratch.GetComponents<Component>().Length > 1, Is.False, scratch.name + " stays inert.");
            });
        }

        private static void WithTower(System.Action<Scene> inspect)
        {
            var loaded = SceneManager.GetSceneByPath(TowerPath);
            bool opened = !(loaded.IsValid() && loaded.isLoaded);
            var scene = opened ? EditorSceneManager.OpenScene(TowerPath, OpenSceneMode.Additive) : loaded;
            try { inspect(scene); }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
