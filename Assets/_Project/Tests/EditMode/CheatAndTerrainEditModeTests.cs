using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Cheats;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Resources;
using TowerOfBabel.World;
using TowerOfBabel.World.Chunks;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class CheatAndTerrainEditModeTests
    {
        [Test]
        public void Profiles_ClampValuesCopySnapshotsAndRestoreEveryDefault()
        {
            var profile = new CheatProfile();
            foreach (CheatStat stat in Enum.GetValues(typeof(CheatStat))) Assert.That(profile.Get(stat), Is.EqualTo(CheatProfile.Default(stat)));
            profile.Set(CheatStat.BuildSpeed, 10000); profile.Set(CheatStat.BagCapacity, 1500);
            profile.Set(CheatStat.Gravity, float.NaN); profile.Set(CheatStat.ExtraJumps, -10);
            Assert.That(profile.Get(CheatStat.BuildSpeed), Is.EqualTo(1000));
            Assert.That(profile.Get(CheatStat.BagCapacity), Is.EqualTo(1500));
            Assert.That(profile.Get(CheatStat.Gravity), Is.EqualTo(1));
            Assert.That(profile.Get(CheatStat.ExtraJumps), Is.Zero);
            var snapshot = profile.Snapshot(); snapshot[(int)CheatStat.BuildSpeed] = 10;
            Assert.That(profile.Get(CheatStat.BuildSpeed), Is.EqualTo(1000));
            var restored = new CheatProfile(); restored.Apply(snapshot);
            Assert.That(restored.Get(CheatStat.BuildSpeed), Is.EqualTo(10));
            restored.Reset(); Assert.That(restored.Get(CheatStat.BuildSpeed), Is.EqualTo(1));
            Assert.That(profile.Set((CheatStat)999, 5), Is.False);
        }

        [Test]
        public void Capacity_IsPerPlayerClampsExistingBalancesAndHandlesOverflow()
        {
            var store = new ServerPlayerResourceStore(50);
            store.SetCapacity(1, 5000); store.TryAdd(1, ResourceType.Stone, int.MaxValue, out int amount);
            Assert.That(amount, Is.EqualTo(5000)); Assert.That(store.GetCapacity(2), Is.EqualTo(50));
            store.SetAmount(1, ResourceType.ProcessedStone, 4000);
            Assert.That(store.TryExchange(1, ResourceType.Stone, 5, ResourceType.ProcessedStone, 100, out _, out int target), Is.True);
            Assert.That(target, Is.EqualTo(4100));
            store.SetCapacity(1, 100);
            Assert.That(store.GetAmount(1, ResourceType.Stone), Is.EqualTo(100));
            Assert.That(store.GetAmount(1, ResourceType.ProcessedStone), Is.EqualTo(100));
            store.RemovePlayer(1); Assert.That(store.GetCapacity(1), Is.EqualTo(50));
        }

        [Test]
        public void BulkConstruction_UpdatesStagesProgressionAndCacheVersionsTogether()
        {
            var root = new GameObject("Bulk construction fixture"); root.SetActive(false);
            var manager = root.AddComponent<ChunkManager>();
            try
            {
                var chunks = new List<ChunkSceneCache>();
                for (int floor = 0; floor < 3; floor++)
                {
                    var assets = new List<ChunkAssetData> { new(0, World.Tower.TowerAssetType.Floor, Vector3.up * floor * 6, Quaternion.identity, Vector3.one), new(1, World.Tower.TowerAssetType.Pillar, Vector3.up * floor * 6 + Vector3.right, Quaternion.identity, Vector3.one) };
                    chunks.Add((ChunkSceneCache)Activator.CreateInstance(typeof(ChunkSceneCache), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { new ChunkKey(floor, 0, 0), assets }, null));
                }
                Set(manager, "cachedChunks", chunks);
                Assert.That(manager.SetConstructionStage(0, 10), Is.EqualTo(2));
                Assert.That(manager.ActiveConstructionFloor, Is.EqualTo(1));
                Assert.That(chunks[0].Version, Is.EqualTo(2));
                Assert.That(manager.SetConstructionStage(null, 10), Is.EqualTo(4));
                Assert.That(manager.ActiveConstructionFloor, Is.Null);
                Assert.That(manager.SetConstructionStage(0, 0), Is.EqualTo(2));
                Assert.That(manager.ActiveConstructionFloor, Is.Zero);
                Assert.That(manager.IsFloorVisible(2), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void FloorBuildCheats_EntireFloorStructureOnlyAndDetailOnlyAfterStructure()
        {
            var root = new GameObject("Floor build cheat fixture"); root.SetActive(false);
            var manager = root.AddComponent<ChunkManager>();
            try
            {
                var chunks = new List<ChunkSceneCache>();
                for (int floor = 0; floor < 2; floor++)
                {
                    var assets = new List<ChunkAssetData> {
                        new(0, World.Tower.TowerAssetType.Floor, Vector3.up * floor * 6, Quaternion.identity, Vector3.one),
                        new(1, World.Tower.TowerAssetType.Pillar, Vector3.up * floor * 6 + Vector3.right, Quaternion.identity, Vector3.one),
                        new(2, World.Tower.TowerAssetType.Vine0, Vector3.up * floor * 6 + Vector3.forward, Quaternion.identity, Vector3.one) };
                    chunks.Add((ChunkSceneCache)Activator.CreateInstance(typeof(ChunkSceneCache), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { new ChunkKey(floor, 0, 0), assets }, null));
                }
                Set(manager, "cachedChunks", chunks);
                byte Stage(int floor, int index) { manager.TryGetAsset(new ChunkKey(floor, 0, 0), index, out var a); return a.Stage; }
                bool Run(CheatCommand command, out string failure)
                {
                    if (!Networking.Buildings.FloorBuildCheat.TryResolve(manager, command, out int floor, out failure)) return false;
                    manager.SetConstructionStage(floor, ChunkAssetData.CompletedStage, Networking.Buildings.FloorBuildCheat.PhaseOf(command));
                    return true;
                }

                // Detail is released only after structure, so it refuses and says which command to use.
                Assert.That(Run(CheatCommand.BuildFloorDetail, out string failure), Is.False);
                Assert.That(failure, Does.Contain("structure first"));
                Assert.That(Stage(0, 2), Is.Zero);

                Assert.That(Run(CheatCommand.BuildFloorStructure, out _), Is.True);
                Assert.That(Stage(0, 0), Is.EqualTo(10)); Assert.That(Stage(0, 1), Is.EqualTo(10));
                Assert.That(Stage(0, 2), Is.Zero, "Structure leaves the vines unbuilt.");
                Assert.That(manager.IsStructureComplete(0), Is.True); Assert.That(manager.IsFloorComplete(0), Is.False);
                Assert.That(manager.ActiveConstructionFloor, Is.Zero, "The floor stays active for its detail.");
                Assert.That(Run(CheatCommand.BuildFloorStructure, out failure), Is.False);
                Assert.That(failure, Does.Contain("already built"));

                Assert.That(Run(CheatCommand.BuildFloorDetail, out _), Is.True);
                Assert.That(Stage(0, 2), Is.EqualTo(10)); Assert.That(manager.IsFloorComplete(0), Is.True);
                Assert.That(manager.ActiveConstructionFloor, Is.EqualTo(1), "Finishing the detail unlocks the next floor.");
                Assert.That(Stage(1, 0), Is.Zero, "Phase builds never touch other floors.");

                Assert.That(Run(CheatCommand.BuildFloor, out _), Is.True);
                for (int i = 0; i < 3; i++) Assert.That(Stage(1, i), Is.EqualTo(10), "Entire floor builds every phase.");
                Assert.That(manager.ActiveConstructionFloor, Is.Null);
                Assert.That(Run(CheatCommand.BuildFloor, out failure), Is.False);
                Assert.That(failure, Does.Contain("Every floor"));
                Assert.That(Networking.Buildings.FloorBuildCheat.PhaseOf(CheatCommand.BuildFloor), Is.Null);
                Assert.That(Networking.Buildings.FloorBuildCheat.IsFloorBuild(CheatCommand.BuildTower), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void CheatPrefab_ContainsEveryQuickActionAndAScrollableStatsList()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/FrutigerAero/Prefabs/Screens/CheatMenu.prefab");
            Assert.That(prefab, Is.Not.Null);
            var root = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var menu = root.GetComponent<CheatMenuUI>(); menu.BuildView(); menu.ShowPreview();
                foreach (string action in new[] { "Max level", "Unlock all upgrades", "Instantly build entire floor", "Instantly build floor structure", "Instantly build floor detail", "Instantly build tower", "Set day", "Set night" })
                    Assert.That(menu.ActionButtons.ContainsKey(action), Is.True, action);
                Assert.That(menu.ToggleControls.ContainsKey("God mode"), Is.True);
                Assert.That(menu.ToggleControls.ContainsKey("Rain"), Is.True, "Rain is a quick action.");
                var order = root.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true).content.Cast<Transform>().Select(t => t.name).ToList();
                string[] expected = { "God mode", "Max level", "Unlock all upgrades", "Instantly build entire floor", "Instantly build floor structure", "Instantly build floor detail", "Instantly build tower", "Set day", "Set night", "Rain" };
                Assert.That(order.Take(expected.Length), Is.EqualTo(expected), "Most used quick actions come first, in order.");
                Assert.That(menu.ActionButtons.ContainsKey("Level up"), Is.False, "Level up moved out of quick actions.");
                menu.SelectCategory("Progression");
                foreach (string action in new[] { "Level up", "Gain XP", "Max level", "Unlock all upgrades" }) Assert.That(menu.ActionButtons.ContainsKey(action), Is.True, action);
                Assert.That(menu.ActionButtons.Keys.Any(k => k.StartsWith("Purchase ")), Is.False, "Per-node purchase rows were removed.");
                menu.SelectCategory("Construction"); Assert.That(menu.ToggleControls.ContainsKey("Instantly build asset"), Is.True);
                foreach (string action in new[] { "Instantly build entire floor", "Instantly build floor structure", "Instantly build floor detail" })
                    Assert.That(menu.ActionButtons.ContainsKey(action), Is.True, "Floor builds also stay under Construction: " + action);
                menu.SelectCategory("Inventory"); Assert.That(menu.ActionButtons.ContainsKey("Increase bag capacity"), Is.True);
                menu.SelectCategory("Quick actions");
                Assert.That(menu.ToggleControls["God mode"].graphic.canvasRenderer.GetAlpha(), Is.Zero, "An off toggle must not show the green on mark.");
                Canvas.ForceUpdateCanvases();
                Assert.That(menu.ToggleControls["God mode"].graphic.gameObject.activeSelf, Is.False, "Canvas rebuilds must keep the off indicator hidden.");
                menu.SelectCategory("World");
                Assert.That(menu.ToggleControls.ContainsKey("Rain"), Is.True, "Synced rain toggle.");
                Assert.That(menu.ToggleControls.ContainsKey("Automatic weather"), Is.True);
                Assert.That(menu.ToggleControls.ContainsKey("Freeze time of day"), Is.True);
                Assert.That(menu.ActionButtons.ContainsKey("Set day"), Is.True);
                Assert.That(menu.ActionButtons.ContainsKey("Set night"), Is.True);
                Assert.That(menu.ActionButtons.ContainsKey("Wetness preset"), Is.True);
                Assert.That(menu.ActionButtons.ContainsKey("Post look"), Is.True);
                menu.SelectAllProfessions(); Assert.That(menu.AllProfessions, Is.True);
                menu.SelectCategory("Stats");
                var fields = root.GetComponentsInChildren<TMPro.TMP_InputField>(true);
                Assert.That(fields.Length, Is.GreaterThan(20));
                Assert.That(fields.All(f => f.GetComponent<TowerOfBabel.Interface.AeroInputField>() != null), Is.True, "Every field uses the Aero input style.");
                var scroll = root.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
                Assert.That(scroll.content != null && scroll.viewport != null && !scroll.horizontal && scroll.vertical, Is.True);
                menu.Search("build tower"); Assert.That(menu.ActionButtons.ContainsKey("Instantly build tower"), Is.True);
                menu.SetAmount(10000); menu.SetFloor(3);
                Assert.That(menu.Amount, Is.EqualTo(10000)); Assert.That(menu.SelectedFloor, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void VisualPanel_OpensSmallInTheTopLeftInPlaceOfTheFullMenuAndReturns()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/FrutigerAero/Prefabs/Screens/CheatMenu.prefab");
            var root = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var menu = root.GetComponent<CheatMenuUI>(); menu.BuildView(); menu.ShowPreview();
                Assert.That(menu.ActionButtons.ContainsKey("Visual controls"), Is.True, "Quick actions open the visual panel.");
                menu.SelectCategory("World"); Assert.That(menu.ActionButtons.ContainsKey("Visual controls"), Is.True, "Also listed under World.");
                menu.ActionButtons["Visual controls"].onClick.Invoke();
                Assert.That(menu.IsVisualPanelVisible, Is.True);
                Assert.That(menu.IsVisible, Is.False, "The full menu and its dark overlay hide so the scene stays visible.");
                Assert.That(menu.IsOpen, Is.True);
                foreach (string key in new[] { "Cycle day/night", "Sunrise", "Noon", "Sunset", "Midnight", "-1 hour", "+1 hour", "Freeze time", "Rain", "Automatic weather",
                    "Previous cloud preset", "Next cloud preset", "Automatic clouds", "Reroll clouds", "Wetness preset", "Post look", "Full menu", "Close" })
                    Assert.That(menu.VisualButtons.ContainsKey(key), Is.True, key);

                var panel = menu.VisualButtons["Cycle day/night"].GetComponentInParent<UnityEngine.UI.VerticalLayoutGroup>().GetComponent<RectTransform>();
                Assert.That(panel.name, Is.EqualTo("Visual controls"));
                Assert.That(panel.anchorMin, Is.EqualTo(new Vector2(0, 1))); Assert.That(panel.anchorMax, Is.EqualTo(new Vector2(0, 1)));
                Assert.That(panel.pivot, Is.EqualTo(new Vector2(0, 1)));
                Assert.That(panel.anchoredPosition.x, Is.InRange(0, 40)); Assert.That(panel.anchoredPosition.y, Is.InRange(-40, 0));
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
                Assert.That(panel.rect.width, Is.LessThanOrEqualTo(420), "Compact: about a quarter of the 1600-wide reference screen.");
                Assert.That(panel.rect.height, Is.InRange(200, 520), "Compact: leaves most of the 900-high reference screen visible.");
                foreach (var b in menu.VisualButtons.Values)
                {
                    var r = (RectTransform)b.transform; Assert.That(r.rect.width, Is.GreaterThan(0), b.name);
                    Assert.That(panel.rect.Contains(panel.InverseTransformPoint(r.TransformPoint(r.rect.center))), Is.True, b.name + " sits inside the panel.");
                }

                menu.VisualButtons["Next cloud preset"].onClick.Invoke();
                Assert.That(menu.VisualStatusText, Is.Not.Empty, "Every visual action reports in the compact panel.");
                menu.SetLookAround(true); Assert.That(menu.IsLookingAround, Is.True, "Right mouse hands the camera back while the panel is open.");
                menu.VisualButtons["Full menu"].onClick.Invoke();
                Assert.That(menu.IsVisible, Is.True); Assert.That(menu.IsVisualPanelVisible, Is.False);
                Assert.That(menu.IsLookingAround, Is.False, "Leaving the panel ends look-around.");
                menu.SetLookAround(true); Assert.That(menu.IsLookingAround, Is.False, "Look-around only works from the visual panel.");
                menu.ShowVisualPanel(); menu.VisualButtons["Close"].onClick.Invoke();
                Assert.That(menu.IsOpen, Is.False, "Close hides everything.");
                menu.ShowVisualPanel(); Assert.That(menu.IsVisualPanelVisible, Is.True, "The panel can also open directly.");
                menu.Toggle(); Assert.That(menu.IsOpen, Is.False, "F3 closes the visual panel.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void VisualPanel_DayCycleStepsSunriseNoonSunsetMidnightAndFormatsTheClock()
        {
            Assert.That(CheatMenuUI.DayPhases.Select(p => p.name), Is.EqualTo(new[] { "Sunrise", "Noon", "Sunset", "Midnight" }));
            string Next(float time) => CheatMenuUI.DayPhases[CheatMenuUI.NextDayPhase(time)].name;
            Assert.That(Next(0), Is.EqualTo("Sunrise"));
            Assert.That(Next(.27f), Is.EqualTo("Noon"));
            Assert.That(Next(.5f), Is.EqualTo("Sunset"));
            Assert.That(Next(.73f), Is.EqualTo("Midnight"));
            Assert.That(Next(.9f), Is.EqualTo("Midnight"));
            Assert.That(Next(.996f), Is.EqualTo("Sunrise"), "Just before midnight counts as midnight already.");
            Assert.That(Next(.4f), Is.EqualTo("Noon"));
            Assert.That(CheatMenuUI.FormatTimeOfDay(0), Is.EqualTo("00:00"));
            Assert.That(CheatMenuUI.FormatTimeOfDay(.5f), Is.EqualTo("12:00"));
            Assert.That(CheatMenuUI.FormatTimeOfDay(.75f), Is.EqualTo("18:00"));
            Assert.That(CheatMenuUI.FormatTimeOfDay(1.25f), Is.EqualTo("06:00"));
            Assert.That(CheatMenuUI.FormatTimeOfDay(.9999f), Is.EqualTo("00:00"));
        }

        [Test]
        public void AeroInputField_BuildsAGlassWellWithCaretAccentAndPlaceholder()
        {
            var canvas = new GameObject("Canvas", typeof(Canvas));
            try
            {
                var field = TowerOfBabel.Interface.AeroInputField.Create(canvas.transform, "Amount", "42", 120, true, null);
                var aero = field.GetComponent<TowerOfBabel.Interface.AeroInputField>();
                Assert.That(aero, Is.Not.Null); Assert.That(aero.Input, Is.SameAs(field));
                Assert.That(field.GetComponent<TowerOfBabel.Interface.AeroGlassGeometry>(), Is.Not.Null, "Rounded glass well.");
                Assert.That(field.GetComponent<TowerOfBabel.Interface.AeroHoverFeedback>(), Is.Not.Null, "Glass glow on hover/focus.");
                Assert.That(field.text, Is.EqualTo("42")); Assert.That(((TMPro.TMP_Text)field.placeholder).text, Is.EqualTo("Amount"));
                Assert.That(field.contentType, Is.EqualTo(TMPro.TMP_InputField.ContentType.DecimalNumber));
                Assert.That(field.customCaretColor, Is.True); Assert.That(field.caretColor, Is.EqualTo(TowerOfBabel.Interface.AeroInputField.Accent));
                Assert.That(field.textViewport.GetComponent<UnityEngine.UI.RectMask2D>(), Is.Not.Null);
                Assert.That(aero.AccentBar.raycastTarget, Is.False);
                Assert.That(aero.AccentStrength, Is.EqualTo(TowerOfBabel.Interface.AeroInputField.IdleAccent).Within(.001f), "Unfocused underline is dim.");
                Assert.That(field.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth, Is.EqualTo(120));
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void Terrain_RetainsFoundationHasRollingMeadowsAndAccessibleLakeBanks()
        {
            var (scene, landscape) = LoadLandscape();
            try
            {
            Assert.That(landscape.ClearingRadius, Is.LessThanOrEqualTo(100));
            foreach (float angle in new[] { 0f, 1f, 2f, 3f, 4f })
                Assert.That(Mathf.Abs(landscape.SampleLandscapeHeight(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 90)), Is.LessThan(.02f));
            var samples = Enumerable.Range(0, 36).Select(i => landscape.SampleLandscapeHeight(new Vector2(Mathf.Cos(i), Mathf.Sin(i)) * 160)).ToArray();
            Assert.That(samples.Max() - samples.Min(), Is.GreaterThan(2), "Outer meadows should undulate.");
            foreach (var lake in landscape.Lakes)
            {
                Assert.That(landscape.SampleLandscapeHeight(lake.offset), Is.LessThan(landscape.WaterElevation - 2));
                Assert.That(lake.offset.magnitude - Mathf.Max(lake.radii.x, lake.radii.y), Is.GreaterThan(landscape.ClearingRadius));
            }
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void Terrain_GrassCoversDryOuterMeadowAndNeverTheTowerOrWater()
        {
            var (scene, landscape) = LoadLandscape();
            try
            {
            var terrain = landscape.GetComponent<Terrain>(); var data = terrain.terrainData;
            int resolution = data.detailResolution, checkedDry = 0, populated = 0, clearingGrass = 0, underwaterGrass = 0;
            var detail = data.GetDetailLayer(0, 0, resolution, resolution, 0);
            for (int z = 0; z < resolution; z += 7)
            for (int x = 0; x < resolution; x += 7)
            {
                float u = (x + .5f) / resolution, v = (z + .5f) / resolution;
                Vector2 point = new(terrain.transform.position.x + u * data.size.x, terrain.transform.position.z + v * data.size.z);
                float height = data.GetInterpolatedHeight(u, v) + terrain.transform.position.y;
                float radius = Vector2.Distance(point, landscape.ClearingCenter);
                if (radius < landscape.ClearingRadius) clearingGrass += detail[z, x];
                if (height <= landscape.WaterElevation + .3f) underwaterGrass += detail[z, x];
                if (radius > landscape.ClearingRadius + 8 && height > landscape.WaterElevation + 2 && data.GetSteepness(u, v) < 40)
                { checkedDry++; if (detail[z, x] > 0) populated++; }
            }
            Assert.That(clearingGrass, Is.Zero); Assert.That(underwaterGrass, Is.Zero);
            Assert.That(checkedDry, Is.GreaterThan(1000));
            Assert.That((float)populated / checkedDry, Is.GreaterThan(.98f));
            var quality = terrain.GetComponent<TerrainPresentationQuality>();
            Assert.That(quality, Is.Not.Null);
            Assert.That(terrain.detailObjectDistance, Is.EqualTo(quality.EditorGrassDistance));
            Assert.That(terrain.detailObjectDensity, Is.EqualTo(quality.EditorGrassDensity));
            Assert.That(quality.RuntimeGrassDistance, Is.InRange(70, 110));
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }
        private static (UnityEngine.SceneManagement.Scene, AeroTowerTerrain) LoadLandscape()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Tower.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
            var landscape = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AeroTowerTerrain>(true)).First();
            return (scene, landscape);
        }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
