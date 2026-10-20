using UnityEngine;
using UnityEngine.UI;

namespace TowerOfBabel.Interface
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AeroTreeConnections : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2 cell=UpgradeBoardLayout.CellSize(rectTransform.rect);
            Vector2 stride=cell+Vector2.one*9;
            for(int row=0;row<7;row++)
            for(int column=0;column<7;column++)
            {
                Vector2 p=new((column-3)*stride.x,(3.5f-row)*stride.y);
                if(column<6) Line(mesh,p+new Vector2(cell.x/2,0),p+new Vector2(stride.x-cell.x/2,0));
                if(row<6 || row==6 && column==3) Line(mesh,p-new Vector2(0,cell.y/2),p-new Vector2(0,stride.y-cell.y/2));
            }
        }
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
