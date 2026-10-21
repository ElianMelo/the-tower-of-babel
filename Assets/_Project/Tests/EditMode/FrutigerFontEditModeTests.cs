using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class FrutigerFontEditModeTests
    {
        private const string FontPath = "Assets/_Project/Fonts/Frutiger SDF.asset";

        [Test]
        public void RegularAndBoldUseSuppliedFacesWithMatchingAtlasSettings()
        {
            var regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Frutiger Bold SDF.asset");
            Assert.That(regular, Is.Not.Null);
            Assert.That(bold, Is.Not.Null);
            Assert.That(SourceGuid(regular), Is.EqualTo(AssetDatabase.AssetPathToGUID("Assets/_Project/Fonts/Frutiger.ttf")));
            Assert.That(SourceGuid(bold), Is.EqualTo(AssetDatabase.AssetPathToGUID("Assets/_Project/Fonts/Frutiger_bold.ttf")));
            Assert.That(regular.fontWeightTable[7].regularTypeface, Is.EqualTo(bold));
            foreach (var font in new[] { regular, bold })
            {
                Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
                Assert.That(font.faceInfo.scale, Is.EqualTo(1));
                Assert.That(font.material.mainTexture, Is.EqualTo(font.atlasTexture));
                Assert.That(font.HasCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789ÀÁÉÍÓÚàáéíóúãõç", out _, true, false), Is.True);
                var fallback = font.fallbackFontAssetTable.Single();
                Assert.That(fallback.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
                Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(fallback.sourceFontFile)), Is.EqualTo(SourceGuid(font)));
                Assert.That(new SerializedObject(fallback).FindProperty("m_ClearDynamicDataOnBuild").boolValue, Is.True);
                Assert.That(fallback.atlasPadding, Is.EqualTo(font.atlasPadding));
                Assert.That(fallback.faceInfo.pointSize, Is.EqualTo(font.faceInfo.pointSize));
            }
        }

        private static string SourceGuid(TMP_FontAsset font) => new SerializedObject(font).FindProperty("m_SourceFontFileGUID").stringValue;

        [Test]
        public void AllAuthoredGameplayAndConsoleTextUsesFrutiger()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            int count = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project", "Assets/Plugins/IngameDebugConsole" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
                {
                    count++;
                    Assert.That(text.font, Is.EqualTo(font), path + "/" + text.name);
                    Assert.That(text.fontSharedMaterial, Is.EqualTo(font.material), path + "/" + text.name);
                    foreach (var character in text.text.Where(c => !char.IsControl(c)))
                        Assert.That(font.HasCharacter(character, true, false), Is.True, path + ": " + character);
                }
                foreach (var field in prefab.GetComponentsInChildren<TMP_InputField>(true))
                    Assert.That(field.fontAsset, Is.EqualTo(font), path);
            }
            Assert.That(count, Is.GreaterThan(70));
            Assert.That(TMP_Settings.defaultFontAsset, Is.EqualTo(font));
            Assert.That(TMP_Settings.fallbackFontAssets.Single(), Is.EqualTo(font.fallbackFontAssetTable.Single()));
        }

        [Test]
        public void TowerSceneHasNoOldFontOverrides()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Tower.unity");
            try
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                var texts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).ToArray();
                Assert.That(texts.Length, Is.GreaterThan(30));
                foreach (var text in texts) Assert.That(text.font, Is.EqualTo(font), text.name);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
