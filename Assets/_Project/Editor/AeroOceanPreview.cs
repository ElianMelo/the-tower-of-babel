using System;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;

public static class AeroOceanPreview
{
    public static object Capture()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
            throw new InvalidOperationException("Use saved Tower in Edit Mode.");
        var camera=Camera.main;var ocean=UnityEngine.Object.FindFirstObjectByType<AeroOceanSurface>();
        var position=camera.transform.position;var rotation=camera.transform.rotation;var waterPosition=ocean.transform.position;
        float fov=camera.fieldOfView,far=camera.farClipPlane;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var rt=new RenderTexture(1440,900,24,RenderTextureFormat.ARGBHalf);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try
        {
            camera.targetTexture=rt;camera.fieldOfView=65;camera.farClipPlane=AeroOceanSurface.MinimumFarClip;
            foreach(float height in new[]{14f,270f})
            {
                camera.transform.position=new Vector3(480,height,-430);
                camera.transform.rotation=Quaternion.Euler(4,135,0);
                for(int i=0;i<4;i++)camera.Render();RenderTexture.active=rt;
                pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes($"Documentation/FrutigerAero/Buildings/Previews/Ocean-Horizon-{height:0}m.png",pixels.EncodeToPNG());
            }
            return new {vertices=ocean.GetComponent<MeshFilter>().sharedMesh.vertexCount,farClip=camera.farClipPlane,scene=scene.path};
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;camera.farClipPlane=far;camera.targetTexture=oldTarget;
            ocean.transform.position=waterPosition;RenderTexture.active=oldActive;ShaderUtil.allowAsyncCompilation=async;
            UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
