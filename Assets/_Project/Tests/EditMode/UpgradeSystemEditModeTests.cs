using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Upgrades;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class UpgradeSystemEditModeTests
    {
        private const string ConfigPath = "Assets/_Project/Resources/UpgradeTreeConfig.asset";
        private const string InputPath = "Assets/InputSystem_Actions.inputactions";

        [TestCase(UpgradeEffectType.Efficiency, 2f, "Efficiency\n-0.2s", false, "Efficiency\n-2s")]
        [TestCase(UpgradeEffectType.Efficiency, 0.25f, "Efficiency\n-0.2s", false, "Efficiency\n-0.25s")]
        [TestCase(UpgradeEffectType.Cost, -3f, "Cost\n-2", false, "Cost\n-3")]
        [TestCase(UpgradeEffectType.Production, 4f, "Production\n+2", false, "Production\n+4")]
        [TestCase(UpgradeEffectType.Production, 5f, "Level 50\nProduction +2", true, "Level 50\nProduction +5")]
        public void InstantiatedUpgradeButton_DisplaysConfiguredEffectValue(UpgradeEffectType effect,
            float value, string displayName, bool isCapstone, string expectedLabel)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Interface/UpgradeButton.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                UpgradeButton button = instance.GetComponent<UpgradeButton>();
                UpgradeData data = new("gather_3_3", displayName, 3, 3, effect, value, isCapstone);

                button.Configure(data, null, UpgradeButtonState.CanBuy);
                Assert.That(button.Label, Is.EqualTo(expectedLabel));
                Assert.That(button.Data.Value, Is.EqualTo(value));

                button.SetupUpgradeData(null);
                Assert.That(button.Label, Is.Empty);
                button.SetupUpgradeData(data);
                Assert.That(button.Label, Is.EqualTo(expectedLabel));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Experience_IsIncrementalPerJobAndResetsAfterLevelUp()
        {
            PlayerUpgradeProgress player = new();
            UpgradeJobProgress gather = player.Get(UpgradeJob.Gather);

            Assert.That(gather.GainExperience(9), Is.True);
            Assert.That(gather.Level, Is.Zero);
            Assert.That(gather.Experience, Is.EqualTo(9));

            gather.GainExperience(1);
            Assert.That(gather.Level, Is.EqualTo(1));
            Assert.That(gather.Experience, Is.Zero);
            Assert.That(gather.AvailablePoints, Is.EqualTo(1));

            gather.GainExperience(20);
            Assert.That(gather.Level, Is.EqualTo(2));
            Assert.That(gather.Experience, Is.Zero);
            Assert.That(gather.AvailablePoints, Is.EqualTo(2));

            Assert.That(player.Get(UpgradeJob.Process).Level, Is.Zero);
            Assert.That(player.Get(UpgradeJob.Build).Level, Is.Zero);
        }

        [Test]
        public void Experience_StopsAtLevelFiftyWithFiftyPoints()
        {
            UpgradeJobProgress progress = new(UpgradeJob.Process);
            int experienceToLevelFifty = Enumerable.Range(1, UpgradeTreeConfig.MaxLevel)
                .Sum(level => level * 10);

            progress.GainExperience(experienceToLevelFifty);

            Assert.That(progress.Level, Is.EqualTo(UpgradeTreeConfig.MaxLevel));
            Assert.That(progress.Experience, Is.Zero);
            Assert.That(progress.AvailablePoints, Is.EqualTo(UpgradeTreeConfig.MaxLevel));
            Assert.That(progress.GainExperience(1), Is.False);
        }

        [Test]
        public void AbsoluteLevel_GrantsPointsOnlyForLevelsGainedAndResetsExperience()
        {
            UpgradeJobProgress progress = new(UpgradeJob.Gather);
            progress.GainExperience(5);

            Assert.That(progress.SetLevel(4), Is.True);
            Assert.That(progress.Level, Is.EqualTo(4));
            Assert.That(progress.Experience, Is.Zero);
            Assert.That(progress.AvailablePoints, Is.EqualTo(4));

            Assert.That(progress.SetLevel(2), Is.True);
            Assert.That(progress.Level, Is.EqualTo(2));
            Assert.That(progress.AvailablePoints, Is.EqualTo(4));
        }

        [Test]
        public void CheatPointGrantAndReset_ClearTheExpectedProgression()
        {
            UpgradeJobProgress progress = new(UpgradeJob.Build);
            progress.SetLevel(2);
            progress.GrantUpgradePoints(3);

            Assert.That(progress.AvailablePoints, Is.EqualTo(5));

            progress.Reset();

            Assert.That(progress.Level, Is.Zero);
            Assert.That(progress.Experience, Is.Zero);
            Assert.That(progress.AvailablePoints, Is.Zero);
            Assert.That(progress.PurchasedUpgradeIds, Is.Empty);
        }

        [Test]
        public void PlaceholderConfig_HasSevenBySevenGridAndOneCapstonePerJob()
        {
            UpgradeTreeConfig config = LoadConfig();

            foreach (UpgradeJob job in new[] { UpgradeJob.Gather, UpgradeJob.Process, UpgradeJob.Build })
            {
                Assert.That(config.GetUpgrades(job), Has.Count.EqualTo(50));
                Assert.That(config.GetUpgrades(job).Count(upgrade => !upgrade.IsLevelFiftyUpgrade),
                    Is.EqualTo(49));
                Assert.That(config.GetLevelFiftyUpgrade(job), Is.Not.Null);
                Assert.That(config.GetGridUpgrade(job, 3, 3), Is.Not.Null);
            }
        }

        [Test]
        public void Purchases_StartAtCenterAndRevealOnlyOrthogonalNeighbors()
        {
            UpgradeTreeConfig config = LoadConfig();
            UpgradeJobProgress progress = CreateProgressWithPoints(UpgradeJob.Gather, 10);
            UpgradeData center = config.GetGridUpgrade(UpgradeJob.Gather, 3, 3);
            UpgradeData top = config.GetGridUpgrade(UpgradeJob.Gather, 2, 3);
            UpgradeData diagonal = config.GetGridUpgrade(UpgradeJob.Gather, 2, 2);

            Assert.That(progress.CanPurchase(config, center.Id), Is.True);
            Assert.That(progress.IsUpgradeRevealed(config, top.Id), Is.False);
            Assert.That(progress.TryPurchase(config, center.Id), Is.True);
            Assert.That(progress.IsUpgradeRevealed(config, top.Id), Is.True);
            Assert.That(progress.IsUpgradeRevealed(config, diagonal.Id), Is.False);
        }

        [Test]
        public void BuyingBottomCenterRevealsLevelFiftyUpgrade()
        {
            UpgradeTreeConfig config = LoadConfig();
            UpgradeJobProgress progress = CreateProgressWithPoints(UpgradeJob.Build, 10);
            int[] rows = { 3, 4, 5, 6 };
            foreach (int row in rows)
            {
                UpgradeData node = config.GetGridUpgrade(UpgradeJob.Build, row, 3);
                Assert.That(progress.TryPurchase(config, node.Id), Is.True, $"Could not buy [{row}][3]");
            }

            UpgradeData capstone = config.GetLevelFiftyUpgrade(UpgradeJob.Build);
            Assert.That(progress.IsUpgradeRevealed(config, capstone.Id), Is.True);
            Assert.That(progress.TryPurchase(config, capstone.Id), Is.True);
        }

        [Test]
        public void InputActions_BindUpgradeToTabAndInventoryToI()
        {
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            InputAction upgrade = input.FindAction("Player/Upgrade", true);
            InputAction inventory = input.FindAction("Player/Inventory", true);

            Assert.That(upgrade.bindings.Any(binding => binding.path == "<Keyboard>/tab"), Is.True);
            Assert.That(inventory.bindings.Any(binding => binding.path == "<Keyboard>/i"), Is.True);
        }

        private static UpgradeTreeConfig LoadConfig()
        {
            UpgradeTreeConfig config = AssetDatabase.LoadAssetAtPath<UpgradeTreeConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null);
            return config;
        }

        private static UpgradeJobProgress CreateProgressWithPoints(UpgradeJob job, int points)
        {
            UpgradeJobProgress progress = new(job);
            progress.ApplySnapshot(new UpgradeJobSnapshot(job, points, 0, points,
                System.Array.Empty<string>()));
            return progress;
        }
    }
}
