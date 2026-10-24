using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.Rendering
{
    /// <summary>A single seamless water mesh with a regular near grid and increasingly coarse outer rings.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AeroOceanSurface : MonoBehaviour
    {
        public const int NearCells = 64;
        public const float NearExtent = 512;
        public const float OuterExtent = 131072;
        public const float MinimumFarClip = 65536;
        private void OnEnable() => RenderPipelineManager.beginCameraRendering += OnCameraRendering;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnCameraRendering;
        private void LateUpdate() { if (Camera.main != null) PrepareForCamera(Camera.main); }
        private void OnCameraRendering(ScriptableRenderContext context, Camera camera) => PrepareForCamera(camera);

        public void PrepareForCamera(Camera camera)
        {
            if (camera == null || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) ||
                (camera.scene.IsValid() && camera.scene != gameObject.scene)) return;
            float grid = NearExtent * 2 / NearCells;
            var p = camera.transform.position;
            transform.position = new Vector3(Mathf.Floor(p.x/grid)*grid,transform.position.y,Mathf.Floor(p.z/grid)*grid);
            if (camera.cameraType == CameraType.Game && camera.CompareTag("MainCamera"))
                camera.farClipPlane = Mathf.Max(camera.farClipPlane,MinimumFarClip);
        }

        public static Mesh CreateMesh()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            int stride = NearCells+1;
            float spacing = NearExtent*2/NearCells;
            for(int z=0;z<=NearCells;z++) for(int x=0;x<=NearCells;x++)
                vertices.Add(new Vector3(-NearExtent+x*spacing,0,-NearExtent+z*spacing));
            void Triangle(int a,int b,int c)
            {
                if(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).y<0) (b,c)=(c,b);
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
            for(int z=0;z<NearCells;z++) for(int x=0;x<NearCells;x++)
            {
                int a=z*stride+x;
                Triangle(a,a+stride,a+1); Triangle(a+1,a+stride,a+stride+1);
            }
            var inner = new List<int>();
            for(int i=0;i<NearCells;i++) inner.Add(i*stride);
            for(int i=0;i<NearCells;i++) inner.Add(NearCells*stride+i);
            for(int i=0;i<NearCells;i++) inner.Add((NearCells-i)*stride+NearCells);
            for(int i=0;i<NearCells;i++) inner.Add(NearCells-i);
            var directions = inner.ConvertAll(i=>vertices[i]/NearExtent);
            foreach(float extent in new[]{2048f,8192f,32768f,OuterExtent})
            {
                var outer = new List<int>(inner.Count);
                foreach(var direction in directions) {outer.Add(vertices.Count);vertices.Add(direction*extent);}
                for(int i=0;i<inner.Count;i++)
                {
                    int j=(i+1)%inner.Count;
                    Triangle(inner[i],inner[j],outer[i]); Triangle(inner[j],outer[j],outer[i]);
                }
                inner=outer;
            }
            var mesh=new Mesh {name="Aero seamless ocean grid"};
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0);
            var normals=new Vector3[vertices.Count]; var uv=new Vector2[vertices.Count]; var colors=new Color[vertices.Count];
            for(int i=0;i<vertices.Count;i++) {normals[i]=Vector3.up;uv[i]=new Vector2(vertices[i].x,vertices[i].z);colors[i]=Color.clear;}
            mesh.normals=normals; mesh.uv=uv; mesh.colors=colors;
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(OuterExtent*2,40,OuterExtent*2));
            return mesh;
        }
    }
}
