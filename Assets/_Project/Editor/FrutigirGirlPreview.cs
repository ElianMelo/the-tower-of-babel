using System.IO;
using UnityEditor;
using UnityEngine;

public static class FrutigirGirlPreview
{
    public static void Capture(string animation,float time,string path)
    {
        var preview=new PreviewRenderUtility();
        try
        {
            var character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FrutigirGirlBuilder.PrefabPath));
            character.transform.position=Vector3.zero;
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(FrutigirGirlBuilder.Generated+"/"+animation+".anim");
            clip.SampleAnimation(character,time);
            preview.AddSingleGO(character);
            preview.camera.transform.position=new(2.7f,1.5f,4.2f);
            preview.camera.transform.LookAt(new Vector3(0,1.05f,0));
            preview.camera.fieldOfView=30; preview.camera.nearClipPlane=.01f; preview.camera.farClipPlane=30;
            preview.camera.backgroundColor=new(.64f,.86f,.96f,1); preview.camera.clearFlags=CameraClearFlags.SolidColor;
            preview.lights[0].intensity=1.25f; preview.lights[0].transform.rotation=Quaternion.Euler(35,25,0);
            preview.lights[1].intensity=.8f; preview.lights[1].transform.rotation=Quaternion.Euler(330,220,0);
            preview.ambientColor=new(.45f,.5f,.55f);
            preview.BeginStaticPreview(new Rect(0,0,800,1000));
            preview.Render(true);
            var image=preview.EndStaticPreview();
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally { preview.Cleanup(); }
    }
}
