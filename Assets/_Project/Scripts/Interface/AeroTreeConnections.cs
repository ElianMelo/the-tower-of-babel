using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace TowerOfBabel.Interface
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AeroTreeConnections : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var nodes=GetComponentsInChildren<UpgradeButton>(true).Where(n=>n.gameObject.activeSelf && n.Data!=null).ToArray();
            for(int i=0;i<nodes.Length;i++)
            for(int j=i+1;j<nodes.Length;j++)
            {
                var a=nodes[i].Data; var b=nodes[j].Data;
                int rowA=a.IsLevelFiftyUpgrade ? 7 : a.Row, rowB=b.IsLevelFiftyUpgrade ? 7 : b.Row;
                if(Mathf.Abs(rowA-rowB)+Mathf.Abs(a.Column-b.Column)!=1) continue;
                var p=((RectTransform)nodes[i].transform).anchoredPosition;
                var q=((RectTransform)nodes[j].transform).anchoredPosition;
                var direction=(q-p).normalized;
                var half=UpgradeBoardLayout.NodeSize*.5f;
                var inset=new Vector2(direction.x*half.x,direction.y*half.y);
                Line(mesh,p+inset,q-inset);
            }
        }
        public void Refresh() => SetVerticesDirty();
        private void Line(VertexHelper mesh,Vector2 a,Vector2 b)
        {
            Vector2 direction=(b-a).normalized; Vector2 normal=new(-direction.y,direction.x); normal*=.9f;
            int n=mesh.currentVertCount;
            mesh.AddVert(a-normal,color,Vector2.zero); mesh.AddVert(b-normal,color,Vector2.zero);
            mesh.AddVert(b+normal,color,Vector2.zero); mesh.AddVert(a+normal,color,Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
        }
    }
}
