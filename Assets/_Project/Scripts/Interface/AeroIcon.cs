using UnityEngine;
using UnityEngine.UI;

namespace TowerOfBabel.Interface
{
    public enum AeroIconKind { Leaf, Tower, Cube, Music, Globe, Bag, Close, Arrow, Sparkle, Pickaxe }

    /// <summary>Resolution-independent pictograms; each authored icon is reused through a prefab.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AeroIcon : MaskableGraphic
    {
        [SerializeField] private AeroIconKind kind;
        public AeroIconKind Kind { get => kind; set { kind = value; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (kind)
            {
                case AeroIconKind.Close:
                    Line(mesh, .28f,.28f,.72f,.72f,.065f); Line(mesh,.28f,.72f,.72f,.28f,.065f); break;
                case AeroIconKind.Arrow:
                    Line(mesh,.22f,.5f,.78f,.5f,.055f); Line(mesh,.54f,.25f,.8f,.5f,.055f); Line(mesh,.54f,.75f,.8f,.5f,.055f); break;
                case AeroIconKind.Tower:
                    Quad(mesh,.31f,.18f,.69f,.34f); Quad(mesh,.37f,.37f,.63f,.54f); Quad(mesh,.42f,.57f,.58f,.75f);
                    Line(mesh,.5f,.74f,.5f,.88f,.035f); break;
                case AeroIconKind.Leaf:
                    for(int i=0;i<20;i++)
                    {
                        float a=i/20f,b=(i+1)/20f;
                        Triangle(mesh,new(.5f,.5f),Curve(new(.18f,.18f),new(.09f,.7f),new(.45f,.92f),new(.84f,.84f),a),Curve(new(.18f,.18f),new(.09f,.7f),new(.45f,.92f),new(.84f,.84f),b));
                        Triangle(mesh,new(.5f,.5f),Curve(new(.84f,.84f),new(.93f,.35f),new(.6f,.08f),new(.18f,.18f),a),Curve(new(.84f,.84f),new(.93f,.35f),new(.6f,.08f),new(.18f,.18f),b));
                    }
                    break;
                case AeroIconKind.Cube:
                    Line(mesh,.22f,.3f,.5f,.15f,.04f); Line(mesh,.5f,.15f,.78f,.3f,.04f); Line(mesh,.78f,.3f,.78f,.68f,.04f);
                    Line(mesh,.78f,.68f,.5f,.85f,.04f); Line(mesh,.5f,.85f,.22f,.68f,.04f); Line(mesh,.22f,.68f,.22f,.3f,.04f);
                    Line(mesh,.22f,.68f,.5f,.52f,.04f); Line(mesh,.78f,.68f,.5f,.52f,.04f); Line(mesh,.5f,.52f,.5f,.15f,.04f); break;
                case AeroIconKind.Bag:
                    Line(mesh,.22f,.22f,.78f,.22f,.045f); Line(mesh,.22f,.22f,.25f,.68f,.045f); Line(mesh,.78f,.22f,.75f,.68f,.045f);
                    Line(mesh,.25f,.68f,.75f,.68f,.045f); Line(mesh,.38f,.66f,.38f,.85f,.04f); Line(mesh,.62f,.66f,.62f,.85f,.04f);
                    Line(mesh,.38f,.85f,.62f,.85f,.04f); break;
                case AeroIconKind.Music:
                    Line(mesh,.4f,.3f,.4f,.78f,.065f); Line(mesh,.74f,.36f,.74f,.86f,.065f); Line(mesh,.4f,.78f,.74f,.86f,.08f);
                    Disc(mesh,new(.28f,.26f),.13f); Disc(mesh,new(.62f,.32f),.13f); break;
                case AeroIconKind.Pickaxe:
                    Line(mesh,.28f,.19f,.68f,.73f,.075f); Line(mesh,.31f,.75f,.67f,.8f,.08f); Line(mesh,.67f,.8f,.83f,.6f,.065f); break;
                case AeroIconKind.Sparkle:
                    Triangle(mesh,new(.5f,.9f),new(.4f,.5f),new(.6f,.5f)); Triangle(mesh,new(.5f,.1f),new(.4f,.5f),new(.6f,.5f));
                    Triangle(mesh,new(.1f,.5f),new(.5f,.4f),new(.5f,.6f)); Triangle(mesh,new(.9f,.5f),new(.5f,.4f),new(.5f,.6f)); break;
                default:
                    for(int i=0;i<36;i++) { float a=i*Mathf.PI*2/36, b=(i+1)*Mathf.PI*2/36;
                        Line(mesh,.5f+Mathf.Cos(a)*.34f,.5f+Mathf.Sin(a)*.34f,.5f+Mathf.Cos(b)*.34f,.5f+Mathf.Sin(b)*.34f,.035f); }
                    Line(mesh,.16f,.5f,.84f,.5f,.03f); Line(mesh,.5f,.16f,.5f,.84f,.03f);
                    Line(mesh,.32f,.22f,.32f,.78f,.02f); Line(mesh,.68f,.22f,.68f,.78f,.02f); break;
            }
        }
        private Vector2 Point(Vector2 p) { Rect r=rectTransform.rect; return new(r.xMin+p.x*r.width,r.yMin+p.y*r.height); }
        private static Vector2 Curve(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t)
        { float u=1-t; return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d; }
        private void Triangle(VertexHelper h,Vector2 a,Vector2 b,Vector2 c)
        {
            int n=h.currentVertCount; h.AddVert(Point(a),color,Vector2.zero); h.AddVert(Point(b),color,Vector2.zero); h.AddVert(Point(c),color,Vector2.zero); h.AddTriangle(n,n+1,n+2);
        }
        private void Quad(VertexHelper h,float x0,float y0,float x1,float y1)
        { Triangle(h,new(x0,y0),new(x1,y0),new(x1,y1)); Triangle(h,new(x0,y0),new(x1,y1),new(x0,y1)); }
        private void Line(VertexHelper h,float x0,float y0,float x1,float y1,float width)
        {
            Vector2 a=new(x0,y0),b=new(x1,y1),v=(b-a).normalized; Vector2 n=new(-v.y,v.x); n*=width*.5f;
            Triangle(h,a-n,b-n,b+n); Triangle(h,a-n,b+n,a+n);
        }
        private void Disc(VertexHelper h,Vector2 center,float radius)
        { for(int i=0;i<24;i++) { float a=i*Mathf.PI*2/24,b=(i+1)*Mathf.PI*2/24; Triangle(h,center,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius); } }
    }
}
