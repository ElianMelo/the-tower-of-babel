using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class RiverPoolGeometryTests
    {
        [TestCase(0)] [TestCase(20)] [TestCase(42)]
        public void Enclosure_HasSeparateContainedWaterAndActualBoundaryWalls(int floor)
        {
            var profile=AssetDatabase.LoadAssetAtPath<RiverGlassProfile>("Assets/_Project/Environment/Lighting/RiverGlass.asset");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset");
            float radius=Mathf.Max(3,profile.layout.x-profile.layout.y*(Mathf.Floor(floor*.5f)+1))*.5f;
            var draw=new RiverGlassDraw {mesh=mesh,count=1,floor=floor,matrices=new[]{Matrix4x4.TRS(new Vector3(radius-2,0,2),Quaternion.identity,Vector3.one)}};
            using var parts=new RiverPoolGeometry(); parts.Rebuild(draw,profile);
            Assert.That(parts.Shell.vertexCount,Is.GreaterThan(0));Assert.That(parts.Water.vertexCount,Is.GreaterThan(0));
            Assert.That(parts.Shell,Is.Not.SameAs(parts.Water));
            Assert.That(parts.Shell.bounds.max.y,Is.EqualTo(0).Within(.0001));
            Assert.That(parts.Shell.bounds.min.y,Is.EqualTo(-.2f).Within(.0001));
            Assert.That(parts.Water.bounds.max.y,Is.EqualTo(-.04f).Within(.0001));
            Assert.That(parts.Water.bounds.min.y,Is.EqualTo(-.18f).Within(.0001));
            Assert.That(parts.Shell.normals.Any(n=>Mathf.Abs(n.y)<.1f),Is.True,"The enclosure needs real vertical glass walls.");
            foreach(var p in parts.Water.vertices)
                Assert.That(RiverPoolGeometry.ChannelDistance(new Vector2(p.x,p.z),floor,profile.layout),Is.LessThanOrEqualTo(-.019f));
            Assert.That(parts.Matches(draw,profile),Is.True);
            draw.matrices[0]=Matrix4x4.Translate(Vector3.right)*draw.matrices[0];
            Assert.That(parts.Matches(draw,profile),Is.False,"A changed instance batch must invalidate its cached geometry.");
        }

        [Test]
        public void AdjacentCompletedTiles_DoNotCreateInternalWaterOrGlassWalls()
        {
            var profile=AssetDatabase.LoadAssetAtPath<RiverGlassProfile>("Assets/_Project/Environment/Lighting/RiverGlass.asset");
            var draw=new RiverGlassDraw {mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset"),count=2,
                matrices=new[]{Matrix4x4.Translate(new Vector3(-2,0,2)),Matrix4x4.Translate(new Vector3(2,0,2))}};
            var parts=new RiverPoolGeometry();
            parts.Rebuild(draw,profile);
            Assert.That(parts.Shell.normals.All(n=>Mathf.Abs(n.y)>.9f),Is.True,"Internal tile edges in the central pool must stay open.");
            parts.Dispose();Assert.That(parts.Shell==null && parts.Water==null,Is.True);
        }
    }
}
