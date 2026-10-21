using System;
using System.Collections.Generic;
using System.Linq;
using TowerOfBabel;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Uses the supplied matching body skin and binds the complete model's accessories to it.</summary>
public static class FrutigirGirlBuilder
{
    public const string Source="Assets/_Project/Models/FrutigirGirl";
    public const string Generated=Source+"/Generated";
    public const string PrefabPath="Assets/_Project/Prefabs/Player/FrutigirGirl.prefab";
    public const string ControllerPath=Generated+"/FrutigirGirl.controller";

    [MenuItem("Tower of Babel/Player/Install FrutigirGirl")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets")
            throw new InvalidOperationException("Use this project's Editor in Edit Mode.");
        System.IO.Directory.CreateDirectory(Generated+"/Meshes");
        System.IO.Directory.CreateDirectory(Generated+"/Materials"); AssetDatabase.Refresh();
        var idle=CopyClip("Idle",true); var walk=CopyClip("Walk",true); var jump=CopyClip("Jump",false);
        var controller=BuildController(idle,walk,jump);
        const string legacyPath="Assets/_Project/Prefabs/Player/PlayerVisuals.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null)
        {
            string error=AssetDatabase.MoveAsset(legacyPath,PrefabPath);
            if(!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        var root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach(Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            root.name="FrutigirGirl"; root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); root.transform.localScale=Vector3.one;
            var rig=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source+"/Frutigirl@Idle.fbx"));
            rig.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); rig.transform.localScale=Vector3.one;
            foreach(Transform child in rig.transform.Cast<Transform>().ToArray()) child.SetParent(root.transform,false);
            UnityEngine.Object.DestroyImmediate(rig);
            var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
            var sourceModel=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"/FrutigirlModel.fbx");
            var originalBody=sourceModel.transform.Find("Female Basemesh").GetComponent<MeshFilter>().sharedMesh;
            if(originalBody.vertexCount!=skin.sharedMesh.vertexCount ||
                originalBody.vertices.Where((p,i)=>(p-skin.sharedMesh.vertices[i]).sqrMagnitude>1e-10f).Any())
                throw new InvalidOperationException("Body topology changed; accessory binding must be reviewed.");
            RestoreBindPose(skin);
            var body=CharacterMaterial("Corpo",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"/Teste.png"),Color.white);
            var fabric=CharacterMaterial("Accessories",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"/Teste2.png"),Color.white);
            var hairSource=sourceModel.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("NurbsPath")).sharedMaterial;
            var hair=CharacterMaterial("Hair",hairSource.GetTexture("_BaseMap") as Texture2D,hairSource.HasProperty("_BaseColor")?hairSource.GetColor("_BaseColor"):new Color(.1f,.23f,.36f));
            skin.sharedMaterial=body; skin.updateWhenOffscreen=true;
            Vector3[] bodyVertices=skin.sharedMesh.vertices.Select(skin.transform.TransformPoint).ToArray();
            BoneWeight[] weights=skin.sharedMesh.boneWeights;
            foreach(var renderer in sourceModel.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.name=="Female Basemesh") continue;
                var sourceMesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                var mesh=UnityEngine.Object.Instantiate(sourceMesh); mesh.name="FrutigirGirl_"+renderer.name;
                Matrix4x4 matrix=renderer.transform.localToWorldMatrix;
                Vector3[] vertices=mesh.vertices;
                Vector3[] normals=mesh.normals;
                for(int i=0;i<vertices.Length;i++) vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
                for(int i=0;i<normals.Length;i++) normals[i]=matrix.MultiplyVector(normals[i]).normalized;
                mesh.vertices=vertices; mesh.normals=normals;
                var tangents=mesh.tangents;
                for(int i=0;i<tangents.Length;i++)
                { Vector3 tangent=matrix.MultiplyVector(new(tangents[i].x,tangents[i].y,tangents[i].z)).normalized; tangents[i]=new(tangent.x,tangent.y,tangent.z,tangents[i].w); }
                mesh.tangents=tangents;
                int rigidBone=renderer.name=="Fone" || renderer.name.StartsWith("NurbsPath") ? Array.FindIndex(skin.bones,b=>b.name=="Bip001 Head") :
                    renderer.name=="Boia" ? Array.FindIndex(skin.bones,b=>b.name=="Bip001 Spine1") : -1;
                var accessoryWeights=new BoneWeight[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    if(rigidBone>=0) { accessoryWeights[i]=new BoneWeight{boneIndex0=rigidBone,weight0=1}; continue; }
                    int nearest=0; float distance=float.MaxValue;
                    for(int j=0;j<bodyVertices.Length;j++)
                    { float candidate=(vertices[i]-bodyVertices[j]).sqrMagnitude; if(candidate<distance){distance=candidate;nearest=j;} }
                    accessoryWeights[i]=weights[nearest];
                }
                mesh.boneWeights=accessoryWeights;
                mesh.bindposes=skin.bones.Select(b=>b.worldToLocalMatrix).ToArray(); mesh.RecalculateBounds();
                string meshPath=Generated+"/Meshes/"+mesh.name+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(existing==null) AssetDatabase.CreateAsset(mesh,meshPath);
                else { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; }
                var accessory=new GameObject(renderer.name,typeof(SkinnedMeshRenderer)); accessory.transform.SetParent(root.transform,false);
                var skinned=accessory.GetComponent<SkinnedMeshRenderer>(); skinned.sharedMesh=mesh; skinned.bones=skin.bones; skinned.rootBone=skin.rootBone;
                skinned.sharedMaterial=renderer.name.StartsWith("NurbsPath")?hair:fabric;
                skinned.localBounds=new Bounds(new(0,.85f,0),new(2.5f,3f,2.5f)); skinned.updateWhenOffscreen=true;
            }
            var animator=root.GetComponent<Animator>(); if(animator==null) animator=root.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller; animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            string avatarPath=Generated+"/FrutigirGirlAvatar.asset";
            var avatar=AvatarBuilder.BuildGenericAvatar(root,"Bip001"); avatar.name="FrutigirGirlAvatar";
            var oldAvatar=AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
            if(oldAvatar==null) AssetDatabase.CreateAsset(avatar,avatarPath);
            else { EditorUtility.CopySerialized(avatar,oldAvatar); UnityEngine.Object.DestroyImmediate(avatar); avatar=oldAvatar; }
            if(!avatar.isValid) throw new InvalidOperationException("FrutigirGirl avatar is invalid.");
            animator.avatar=avatar;
            var visuals=root.GetComponent<PlayerVisuals>(); if(visuals==null) visuals=root.AddComponent<PlayerVisuals>();
            Set(visuals,"animator",animator);
            idle.SampleAnimation(root,0);
            root.transform.localPosition=new(0,-1.09f,0); root.transform.localRotation=Quaternion.identity; root.transform.localScale=Vector3.one*1.2f;
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        foreach(string path in new[]{"Assets/_Project/Prefabs/Player/Player.prefab","Assets/_Project/Prefabs/Player/RemotePlayer.prefab"})
        {
            var player=PrefabUtility.LoadPrefabContents(path);
            try { ConfigureVisualInstance(player); PrefabUtility.SaveAsPrefabAsset(player,path); }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }
        foreach(var player in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if(player.GetComponentInChildren<PlayerVisuals>(true)!=null) ConfigureVisualInstance(player);
        AssetDatabase.SaveAssets();
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureVisualInstance(GameObject player)
    {
        foreach(var visual in player.GetComponentsInChildren<PlayerVisuals>(true))
        {
            visual.name="FrutigirGirl"; visual.transform.localScale=Vector3.one*1.2f; visual.transform.localPosition=new(0,-1.09f,0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.gameObject);
        }
    }
    private static void RestoreBindPose(SkinnedMeshRenderer renderer)
    {
        var matrices=new Dictionary<Transform,Matrix4x4>();
        for(int i=0;i<renderer.bones.Length;i++) matrices[renderer.bones[i]]=renderer.transform.localToWorldMatrix*renderer.sharedMesh.bindposes[i].inverse;
        foreach(var bone in renderer.bones.OrderBy(b=>AnimationUtility.CalculateTransformPath(b,renderer.transform.root).Count(c=>c=='/')))
        { var matrix=matrices[bone]; bone.SetPositionAndRotation(matrix.GetColumn(3),matrix.rotation); bone.localScale=Vector3.one; }
    }
    private static AnimationClip CopyClip(string name,bool loop)
    {
        var source=AssetDatabase.LoadAllAssetsAtPath(Source+"/Frutigirl@"+name+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
        string path=Generated+"/"+name+".anim";
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(clip==null) { clip=UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(clip,path); }
        else EditorUtility.CopySerialized(source,clip);
        clip.name=name;
        var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=loop; AnimationUtility.SetAnimationClipSettings(clip,settings); EditorUtility.SetDirty(clip);
        return clip;
    }
    private static AnimatorController BuildController(AnimationClip idle,AnimationClip walk,AnimationClip jump)
    {
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var stateMachine=controller.layers[0].stateMachine;
        foreach(var transition in stateMachine.anyStateTransitions) stateMachine.RemoveAnyStateTransition(transition);
        foreach(var state in stateMachine.states) stateMachine.RemoveState(state.state);
        controller.parameters=Array.Empty<AnimatorControllerParameter>();
        controller.AddParameter("Walking",AnimatorControllerParameterType.Bool); controller.AddParameter("Running",AnimatorControllerParameterType.Bool);
        var idleState=stateMachine.AddState("Idle"); idleState.motion=idle; stateMachine.defaultState=idleState;
        var walking=stateMachine.AddState("Walk"); walking.motion=walk;
        var running=stateMachine.AddState("Run"); running.motion=walk; running.speed=1.5f;
        Transition(idleState,walking,"Walking",true); Transition(idleState,running,"Running",true);
        Transition(walking,running,"Running",true); Transition(running,walking,"Walking",true);
        foreach(var state in new[]{walking,running})
        { var back=Transition(state,idleState,"Walking",false); back.AddCondition(AnimatorConditionMode.IfNot,0,"Running"); }
        controller.AddParameter("CancelAnimation",AnimatorControllerParameterType.Trigger);
        var cancel=stateMachine.AddAnyStateTransition(idleState); cancel.hasExitTime=false; cancel.duration=.05f;
        cancel.AddCondition(AnimatorConditionMode.If,0,"CancelAnimation"); cancel.canTransitionToSelf=true;
        foreach(string trigger in new[]{"Jump","Dancing","HipHopOne","HipHopTwo","HipHopThree","RumbaDance","SillyDancing","Golf","Digging"})
        {
            controller.AddParameter(trigger,AnimatorControllerParameterType.Trigger);
            var state=stateMachine.AddState(trigger); state.motion=trigger=="Digging"?idle:jump;
            var transition=stateMachine.AddAnyStateTransition(state); transition.hasExitTime=false; transition.duration=.08f; transition.canTransitionToSelf=false;
            transition.AddCondition(AnimatorConditionMode.If,0,trigger);
            if(trigger!="Digging") { var exit=state.AddTransition(idleState); exit.hasExitTime=true; exit.exitTime=1; exit.duration=.1f; }
        }
        EditorUtility.SetDirty(controller); return controller;
    }
    private static AnimatorStateTransition Transition(AnimatorState from,AnimatorState to,string parameter,bool value)
    {
        var t=from.AddTransition(to); t.hasExitTime=false; t.duration=.12f; t.AddCondition(value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,parameter); return t;
    }
    private static Material CharacterMaterial(string name,Texture2D texture,Color tint)
    {
        string path=Generated+"/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
        material.SetTexture("_BaseMap",texture); material.SetColor("_BaseColor",tint); material.SetFloat("_Smoothness",.32f);
        material.SetFloat("_Cull",2); EditorUtility.SetDirty(material); return material;
    }
    private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
    { var data=new SerializedObject(target); data.FindProperty(field).objectReferenceValue=value; data.ApplyModifiedPropertiesWithoutUndo(); }
}
