using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroBuildingFinishEditModeTests
    {
        [Test]
        public void EnablingTheFinish_DestroysAnOrphanedReflectionCamera()
        {
            // A script reload leaves the hidden reflection camera behind with no owner; enabled and without a target it
            // rendered over the Game view from the origin, so the game showed only the lake (2026-11-09).
            var orphan = new GameObject(AeroBuildingFinish.ReflectionCameraName, typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
            var orphanCamera = orphan.GetComponent<Camera>();
            orphanCamera.enabled = true;
            var host = new GameObject("Finish host");
            try
            {
                host.AddComponent<AeroBuildingFinish>();
                Assert.That(orphanCamera == null, Is.True, "The orphaned reflection camera must be destroyed when a finish enables.");
                Assert.That(AeroBuildingFinish.DestroyOrphanedReflections(), Is.EqualTo(0));
            }
            finally
            {
                if (orphan != null) Object.DestroyImmediate(orphan);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ReflectionCamera_IsCreatedDisabled_AndReleasedWithItsFinish()
        {
            // The leak: the camera was created live, then moving it into an unloading scene threw and left it drawing
            // over the Game view. It is now created disabled and a finish in an unloaded scene creates nothing.
            var host = new GameObject("Finish host");
            try
            {
                var finish = host.AddComponent<AeroBuildingFinish>();
                finish.profile = UnityEditor.AssetDatabase.LoadAssetAtPath<AeroBuildingFinishProfile>("Assets/_Project/Environment/Lighting/BuildingFinish.asset");
                Assume.That(finish.profile, Is.Not.Null);
                var source = new GameObject("Source camera", typeof(Camera)).GetComponent<Camera>();
                try
                {
                    typeof(AeroBuildingFinish).GetMethod("EnsureResources", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(finish, new object[] { source });
                    var reflection = UnityEngine.Resources.FindObjectsOfTypeAll<Camera>().Single(c => c.name == AeroBuildingFinish.ReflectionCameraName);
                    Assert.That(reflection.enabled, Is.False, "The reflection renders only through explicit requests.");
                    Assert.That(reflection.gameObject.scene, Is.EqualTo(host.scene));
                }
                finally { Object.DestroyImmediate(source.gameObject); }
            }
            finally { Object.DestroyImmediate(host); }
            Assert.That(UnityEngine.Resources.FindObjectsOfTypeAll<Camera>().Count(c => c.name == AeroBuildingFinish.ReflectionCameraName), Is.Zero,
                "Disabling the finish releases its camera.");
        }

        [Test]
        public void OrphanCleanup_LeavesOtherCamerasAlone()
        {
            var other = new GameObject("Some other camera", typeof(Camera));
            try
            {
                AeroBuildingFinish.DestroyOrphanedReflections();
                Assert.That(other.GetComponent<Camera>() != null, Is.True);
            }
            finally { Object.DestroyImmediate(other); }
        }
    }
}
