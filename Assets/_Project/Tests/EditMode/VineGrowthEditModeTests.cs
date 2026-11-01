using System.Linq;
using NUnit.Framework;
using TowerOfBabel.World.Tower;
using UnityEngine;

namespace TowerOfBabel.World.Tests
{
    public sealed class VineGrowthEditModeTests
    {
        // A 0.82 m pillar 4 m tall with an arch on each side: solid spandrels, an opening under each arch.
        private static bool Facade(Vector2 p, out float depth, out Vector3 normal)
        {
            normal = Vector3.forward; depth = 0;
            if (p.y < 0 || p.y > 6) return false;
            if (Mathf.Abs(p.x) <= .41f && p.y <= 4) { depth = .41f; return true; }
            if (p.y < 4 || Mathf.Abs(p.x) > 2.2f) return false;
            float fromCentre = Mathf.Abs(Mathf.Abs(p.x) - 1.045f);
            bool opening = fromCentre < .75f && p.y < 4 + 1.4f * Mathf.Sqrt(1 - Mathf.Pow(fromCentre / .75f, 2));
            depth = .2f;
            return !opening;
        }

        private static VineGrowthBuilder.Style Style(float drape = .5f, int seed = 11) => new()
        {
            seed = seed, stems = 2, spreadHeight = 3.7f, spread = 1.6f, shortStems = .2f, drape = drape, density = 1,
            leafSize = .13f, redChance = .05f, maxHeight = 5.85f
        };

        [Test]
        public void Growth_IsDeterministicForTheSameSeed()
        {
            var a = VineGrowthBuilder.Grow(Style(), Facade);
            var b = VineGrowthBuilder.Grow(Style(), Facade);
            Assert.That(a.LeafCount, Is.EqualTo(b.LeafCount));
            Assert.That(a.TotalLength, Is.EqualTo(b.TotalLength));
            Assert.That(a.BuildStage(10).vertices, Is.EqualTo(b.BuildStage(10).vertices));
            Assert.That(VineGrowthBuilder.Grow(Style(seed: 12), Facade).BuildStage(10).vertices, Is.Not.EqualTo(a.BuildStage(10).vertices));
        }

        [Test]
        public void Stages_GrowFromASproutToAVineThatReachesAndSpreadsOverTheArch()
        {
            var vine = VineGrowthBuilder.Grow(Style(), Facade);
            var meshes = Enumerable.Range(0, 11).Select(s => vine.BuildStage(s)).ToArray();
            for (int s = 1; s < 11; s++)
                Assert.That(meshes[s].vertexCount, Is.GreaterThanOrEqualTo(meshes[s - 1].vertexCount), $"stage {s}");
            Assert.That(meshes[0].bounds.max.y, Is.LessThan(1), "Stage 0 is a sprout at the root.");
            Assert.That(meshes[0].vertexCount, Is.GreaterThan(0));
            Assert.That(meshes[10].bounds.max.y, Is.GreaterThan(4.3f), "The grown vine climbs onto the arch.");
            Assert.That(meshes[10].vertices.Max(v => Mathf.Abs(v.x)), Is.GreaterThan(.9f), "It spreads across part of the arch.");
            Assert.That(meshes[10].bounds.max.y, Is.LessThan(6.2f));
            Assert.That(VineGrowthBuilder.GrowthAt(0), Is.EqualTo(VineGrowthBuilder.SproutGrowth));
            Assert.That(VineGrowthBuilder.GrowthAt(10), Is.EqualTo(1));
        }

        [Test]
        public void Vine_ClingsToTheFrontOfTheFacadeAndStaysOnItsSupport()
        {
            var mesh = VineGrowthBuilder.Grow(Style(drape: 0), Facade).BuildStage(10);
            foreach (var v in mesh.vertices)
            {
                Assert.That(v.z, Is.GreaterThan(.12f), "Nothing grows behind the facade.");
                Assert.That(v.z, Is.LessThan(.8f), "Leaves stay close to the stone.");
                // Below the arches only the pillar exists, so the vine stays on its width there.
                if (v.y < 3.6f) Assert.That(Mathf.Abs(v.x), Is.LessThan(.75f), v.ToString());
            }
        }

        [Test]
        public void Drape_HangsLooseStrandsIntoTheArchOpening()
        {
            Assert.That(VineGrowthBuilder.Grow(Style(drape: 0), Facade).HangingStrandCount, Is.Zero);
            var draped = VineGrowthBuilder.Grow(Style(drape: 1), Facade);
            Assert.That(draped.HangingStrandCount, Is.GreaterThan(0));
            var inOpening = draped.BuildStage(10).vertices.Count(v => v.y > 4.05f && v.y < 5 && Mathf.Abs(Mathf.Abs(v.x) - 1.045f) < .4f);
            Assert.That(inOpening, Is.GreaterThan(0), "Strands hang down in front of the opening.");
        }

        [Test]
        public void PillarOnly_StaysOnThePillarBelowTheArch()
        {
            var style = Style(drape: 0); style.maxHeight = 3.85f; style.spreadHeight = 99; style.stems = 3;
            var vine = VineGrowthBuilder.Grow(style, Facade);
            Assert.That(vine.PillarOnly, Is.True); Assert.That(vine.ArchOnly, Is.False);
            var mesh = vine.BuildStage(10);
            Assert.That(mesh.bounds.max.y, Is.LessThan(4.05f));
            Assert.That(mesh.bounds.max.y, Is.GreaterThan(3f), "It still climbs most of the pillar.");
            Assert.That(mesh.vertices.All(v => Mathf.Abs(v.x) < .75f), Is.True);
        }

        [Test]
        public void ArchOnly_RootsOnTheSpandrelAndNeverTouchesThePillar()
        {
            var style = Style(drape: 1); style.rootHeight = 4.2f; style.spreadHeight = 4.1f; style.spread = 2;
            var vine = VineGrowthBuilder.Grow(style, Facade);
            Assert.That(vine.ArchOnly, Is.True);
            var sprout = vine.BuildStage(0);
            Assert.That(sprout.vertexCount, Is.GreaterThan(0));
            Assert.That(sprout.bounds.min.y, Is.GreaterThan(3.8f), "Stage 0 sprouts on the arch.");
            var grown = vine.BuildStage(10);
            Assert.That(grown.vertices.Any(v => Mathf.Abs(v.x) < .25f && v.y < 3.6f), Is.False, "Nothing grows on the pillar face.");
            Assert.That(grown.vertices.Max(v => Mathf.Abs(v.x)), Is.GreaterThan(1f), "It spreads along the arch.");
        }

        [Test]
        public void LeavesFlutterButStemsStayStill()
        {
            var mesh = VineGrowthBuilder.Grow(Style(), Facade).BuildStage(10);
            var alpha = mesh.colors.Select(c => c.a).ToArray();
            Assert.That(alpha.Count(a => a >= .99f), Is.GreaterThan(100), "Leaf rims flutter fully.");
            Assert.That(alpha.Count(a => a == 0), Is.GreaterThan(100), "Clinging stems never move.");
            Assert.That(mesh.colors.Count(c => c.g > c.r), Is.GreaterThan(mesh.vertexCount / 2), "Mostly green.");
        }
    }
}
