using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.Rendering
{
    /// <summary>Clips completed tile faces once, retaining only the real channel volume.
    /// World-space meshes share the source batch's completion, culling and floor identity.</summary>
    public sealed class RiverPoolGeometry : IDisposable
    {
        public static float ChannelDistance(Vector2 point,int floorIndex,Vector4 layout)
        {
            float wall=Mathf.Max(3,layout.x-layout.y*(Mathf.Floor(floorIndex*.5f)+1));
            float radius=point.magnitude,width=Mathf.Min(layout.w,Mathf.Max(.55f,wall*.014f));
            float ring=Mathf.Min(Mathf.Abs(radius-wall*.5f),Mathf.Abs(radius-wall*.88f));
            return Mathf.Max(Mathf.Min(Mathf.Min(ring,Mathf.Min(Mathf.Abs(point.x),Mathf.Abs(point.y)))-width,radius-wall*.16f),radius-wall*.96f);
        }
        private static Vector3 Bend(Vector3 p,float radius,Vector3 scale)
        {
            float remaining=Mathf.Max(0,radius-p.x*scale.x),angle=-p.z*scale.z/radius;
            return new Vector3((radius-remaining*Mathf.Cos(angle))/scale.x,p.y,-remaining*Mathf.Sin(angle)/scale.z);
        }
        public Mesh Water { get; private set; }
        public Mesh Shell { get; private set; }
        private Mesh source;
        private Matrix4x4[] transforms;
        private int count, floor;
        private float radial;
        private Vector2 center;
        private Vector4 layout, enclosure;
        private struct Point
        {
            public Vector3 world, local;
            public Point(Vector3 world, Vector3 local) { this.world = world; this.local = local; }
            public static Point Lerp(Point a, Point b, float t) => new(Vector3.LerpUnclamped(a.world,b.world,t), Vector3.LerpUnclamped(a.local,b.local,t));
        }
        private sealed class Builder
        {
            public readonly List<Vector3> vertices = new(), normals = new(), patches = new();
            public readonly List<int> indices = new();
            public void Triangle(Point a, Point b, Point c, Vector3 normal)
            {
                if (Vector3.Cross(b.world-a.world,c.world-a.world).sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(Vector3.Cross(b.world-a.world,c.world-a.world),normal) < 0) (b,c) = (c,b);
                int start = vertices.Count;
                vertices.Add(a.world); vertices.Add(b.world); vertices.Add(c.world);
                patches.Add(a.local); patches.Add(b.local); patches.Add(c.local);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                indices.Add(start); indices.Add(start+1); indices.Add(start+2);
            }
            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(2,patches); mesh.SetTriangles(indices,0);
                mesh.RecalculateBounds(); return mesh;
            }
        }

        public bool Matches(RiverGlassDraw draw, RiverGlassProfile profile)
        {
            if (source != draw.mesh || count != draw.count || floor != draw.floor || radial != draw.radial ||
                center != draw.center || layout != profile.layout || enclosure != profile.Enclosure) return false;
            for (int i=0;i<count;i++) if (transforms[i] != draw.matrices[i]) return false;
            return true;
        }

        public void Rebuild(RiverGlassDraw draw, RiverGlassProfile profile)
        {
            Dispose();
            source=draw.mesh; count=draw.count; floor=draw.floor; radial=draw.radial; center=draw.center;
            layout=profile.layout; enclosure=profile.Enclosure;
            transforms=new Matrix4x4[count]; Array.Copy(draw.matrices,transforms,count);
            var vertices=source.vertices; var normals=source.normals; var triangles=source.GetTriangles(0);
            var patches=new List<Vector3>(); source.GetUVs(2,patches);
            var shell=new Builder(); var water=new Builder();
            // Only upper source triangles are needed; caps and channel walls are generated below.
            var positions=new Point[vertices.Length];
            var polygon=new Point[5]; var distances=new float[5];
            for (int instance=0;instance<count;instance++)
            {
                Matrix4x4 matrix=transforms[instance];
                var origin=matrix.MultiplyPoint3x4(Vector3.zero);
                var scale=new Vector3(matrix.GetColumn(0).magnitude,matrix.GetColumn(1).magnitude,matrix.GetColumn(2).magnitude);
                float radius=Vector2.Distance(new Vector2(origin.x,origin.z),center);
                for (int v=0;v<vertices.Length;v++)
                {
                    var local=vertices[v];
                    var bent=radial>.5f && radial<1.5f && radius>.0001f ? Bend(local,radius,scale) : local;
                    positions[v]=new Point(matrix.MultiplyPoint3x4(bent),radial>1.5f && patches.Count==vertices.Length ? patches[v] : local);
                }
                for (int t=0;t<triangles.Length;t+=3)
                {
                    int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                    if (normals.Length==vertices.Length ? normals[a].y<.9f || normals[b].y<.9f || normals[c].y<.9f :
                        Mathf.Abs(vertices[a].y-source.bounds.max.y)>.001f || Mathf.Abs(vertices[b].y-source.bounds.max.y)>.001f || Mathf.Abs(vertices[c].y-source.bounds.max.y)>.001f) continue;
                    Subdivide(positions[a],positions[b],positions[c],0);
                }

                void Subdivide(Point a,Point b,Point c,int depth)
                {
                    // Coarse source models also work; sub-metre cells cannot skip a narrow channel.
                    if (depth<5 && Mathf.Max((a.world-b.world).sqrMagnitude,Mathf.Max((a.world-c.world).sqrMagnitude,(b.world-c.world).sqrMagnitude))>.64f)
                    {
                        Point ab=Point.Lerp(a,b,.5f),bc=Point.Lerp(b,c,.5f),ca=Point.Lerp(c,a,.5f);
                        Subdivide(a,ab,ca,depth+1);Subdivide(ab,b,bc,depth+1);Subdivide(ca,bc,c,depth+1);Subdivide(ab,bc,ca,depth+1);return;
                    }
                    ClipVolume(a,b,c,0,0,-.2f,shell);
                    ClipVolume(a,b,c,enclosure.w,-enclosure.x,-enclosure.y,water);
                }
                void ClipVolume(Point a,Point b,Point c,float inset,float top,float bottom,Builder builder)
                {
                    int n=0;
                    Edge(a,b); Edge(b,c); Edge(c,a);
                    if(n<3)return;
                    for(int i=0;i<n;i++) distances[i]=Distance(polygon[i]);
                    for(int i=1;i<n-1;i++)
                    {
                        builder.Triangle(At(polygon[0],top),At(polygon[i],top),At(polygon[i+1],top),Vector3.up);
                        builder.Triangle(At(polygon[0],bottom),At(polygon[i+1],bottom),At(polygon[i],bottom),Vector3.down);
                    }
                    for(int i=0;i<n;i++)
                    {
                        int j=(i+1)%n;
                        // Only the cut edge is a real channel boundary; tile edges remain open.
                        if(Mathf.Abs(distances[i])>.002f || Mathf.Abs(distances[j])>.002f)continue;
                        var p=polygon[i];var q=polygon[j];var middle=(p.world+q.world)*.5f;
                        var normal=new Vector3(DistanceAt(middle+Vector3.right*.002f)-DistanceAt(middle-Vector3.right*.002f),0,
                            DistanceAt(middle+Vector3.forward*.002f)-DistanceAt(middle-Vector3.forward*.002f)).normalized;
                        builder.Triangle(At(p,top),At(p,bottom),At(q,bottom),normal);
                        builder.Triangle(At(p,top),At(q,bottom),At(q,top),normal);
                    }
                    float Distance(Point p)=>DistanceAt(p.world);
                    float DistanceAt(Vector3 p)=>ChannelDistance(new Vector2(p.x,p.z)-center,floor,layout)+inset;
                    Point At(Point p,float offset) { p.world.y=origin.y+offset*scale.y; p.local.y=offset;return p; }
                    void Edge(Point p,Point q)
                    {
                        float dp=Distance(p),dq=Distance(q);
                        if(dp<=0)polygon[n++]=p;
                        if((dp<0)==(dq<0))return;
                        float lo=0,hi=1;
                        for(int step=0;step<12;step++) {float m=(lo+hi)*.5f;if((Distance(Point.Lerp(p,q,m))<0)==(dp<0))lo=m;else hi=m;}
                        polygon[n++]=Point.Lerp(p,q,(lo+hi)*.5f);
                    }
                }
            }
            Shell=shell.Finish("River glass shell — cached channel boundaries");
            Water=water.Finish("River water interior — recessed 4 to 18 cm");
        }
        public void Dispose()
        {
            CoreUtils.Destroy(Water);CoreUtils.Destroy(Shell);Water=Shell=null;source=null;transforms=null;
        }
    }
}
