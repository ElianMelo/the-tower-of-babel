using System.Collections.Generic;
using TowerOfBabel.World.Tower;
using UnityEngine;

namespace TowerOfBabel.Buildings
{
    /// <summary>Shared construction presentation: incoming chunks form, then fly into their fixed building.</summary>
    [DisallowMultipleComponent,RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class PillarConstructionMotion : MonoBehaviour
    {
        [SerializeField] private PillarChunkLibrary library;
        private sealed class FlyingPiece
        {
            public Transform Pivot,MeshTransform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Vector3 Center,Offset;
            public Quaternion Rotation;
            public float Delay;
        }
        private readonly List<FlyingPiece> pool=new();
        private readonly List<Material> materials=new();
        private MeshFilter source,baseMesh;
        private MeshRenderer sourceRenderer,baseRenderer;
        private GameObject presentation;
        private MaterialPropertyBlock properties;
        private bool presenting,confirmed,wasForceRenderingOff;
        private float visualProgress,targetProgress,clock,confirmationAge;
        private int activePieces,targetStage;
        private RadialFloorMeshCache radialMeshes;
        private float radialRadius;
        private Vector3 radialScale;

        public void ConfigureRadialFloor(RadialFloorMeshCache meshes,float radius,Vector3 scale)
        {
            radialMeshes=meshes; radialRadius=radius; radialScale=scale;
        }

        private void Awake() => enabled=false;

        private void Initialize()
        {
            if (presentation!=null) return;
            source=GetComponent<MeshFilter>(); sourceRenderer=GetComponent<MeshRenderer>(); properties=new MaterialPropertyBlock();
            presentation=new GameObject(gameObject.name+" incoming chunks"); presentation.hideFlags=HideFlags.DontSave;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(presentation,gameObject.scene);
            var go=new GameObject("Existing building stage",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(presentation.transform,false); go.layer=gameObject.layer;
            baseMesh=go.GetComponent<MeshFilter>(); baseRenderer=go.GetComponent<MeshRenderer>();
            CopySettings(baseRenderer); presentation.SetActive(false);
        }

        private void CopySettings(MeshRenderer renderer)
        {
            renderer.shadowCastingMode=sourceRenderer.shadowCastingMode; renderer.receiveShadows=sourceRenderer.receiveShadows;
            renderer.lightProbeUsage=sourceRenderer.lightProbeUsage; renderer.reflectionProbeUsage=sourceRenderer.reflectionProbeUsage;
            renderer.probeAnchor=sourceRenderer.probeAnchor; renderer.renderingLayerMask=sourceRenderer.renderingLayerMask;
        }

        private void Prepare(Mesh previous,int fromStage,int toStage)
        {
            ResetImmediately(); Initialize();
            sourceRenderer.GetSharedMaterials(materials);
            targetStage=toStage; baseMesh.sharedMesh=previous; baseRenderer.SetSharedMaterials(materials);
            var random=new System.Random(8127+fromStage*193);
            float Range(float a,float b) => Mathf.Lerp(a,b,(float)random.NextDouble());
            foreach (var piece in library.Pieces)
            {
                if (piece.Stage<=fromStage || piece.Stage>toStage || piece.Mesh==null) continue;
                if (activePieces==pool.Count)
                {
                    var pivot=new GameObject("Incoming chunk pivot"); pivot.transform.SetParent(presentation.transform,false);
                    var go=new GameObject("Forming chunk",typeof(MeshFilter),typeof(MeshRenderer));
                    go.transform.SetParent(pivot.transform,false); go.layer=gameObject.layer;
                    var renderer=go.GetComponent<MeshRenderer>(); CopySettings(renderer);
                    pool.Add(new FlyingPiece {Pivot=pivot.transform,MeshTransform=go.transform,Filter=go.GetComponent<MeshFilter>(),Renderer=renderer});
                }
                var item=pool[activePieces++];
                Mesh mesh=radialMeshes!=null ? radialMeshes.Get(piece.Mesh,radialRadius,radialScale) : piece.Mesh;
                item.Filter.sharedMesh=mesh; item.Renderer.SetSharedMaterials(materials);
                item.Center=mesh.bounds.center; item.MeshTransform.localPosition=-item.Center;
                float angle=Range(0,Mathf.PI*2),radius=Range(1,2.1f);
                item.Offset=new Vector3(Mathf.Cos(angle)*radius,Range(.25f,1.2f),Mathf.Sin(angle)*radius);
                item.Rotation=Quaternion.Euler(Range(-45,45),Range(-70,70),Range(-45,45));
                item.Pivot.gameObject.SetActive(true);
            }
            for (int i=0; i<activePieces; i++) pool[i].Delay=(float)i/Mathf.Max(1,activePieces-1)*.43f;
            wasForceRenderingOff=sourceRenderer.forceRenderingOff; sourceRenderer.forceRenderingOff=true;
            presenting=true; presentation.SetActive(true); enabled=true;
        }

        public void BeginWork(int stage)
        {
            if (library==null || library.Pieces.Length==0) return;
            Initialize(); Prepare(source.sharedMesh,stage,stage+1); ApplyPose();
        }
        public void SetProgress(float progress) => targetProgress=Mathf.Clamp01(progress);
        public void FinishWork() => targetProgress=1;
        public void CancelWork() { if (!confirmed) ResetImmediately(); }

        public void ConfirmStage(Mesh previous,int fromStage,int toStage,bool animate)
        {
            if (!animate || library==null || toStage<=fromStage) { ResetImmediately(); return; }
            if (!presenting || targetStage!=toStage) Prepare(previous,fromStage,toStage);
            confirmed=true; targetProgress=1; confirmationAge=0; ApplyPose();
        }

        private void LateUpdate()
        {
            if (!presenting) return;
            clock+=Time.deltaTime;
            if (confirmed) { visualProgress=Mathf.MoveTowards(visualProgress,1,Time.deltaTime/1.15f); confirmationAge+=Time.deltaTime; }
            else visualProgress=Mathf.MoveTowards(visualProgress,targetProgress,Time.deltaTime*6);
            ApplyPose();
            if (confirmed && visualProgress>=1 && confirmationAge>.18f) ResetImmediately();
        }

        private void ApplyPose()
        {
            presentation.transform.SetPositionAndRotation(transform.position,transform.rotation);
            presentation.transform.localScale=transform.lossyScale;
            sourceRenderer.GetPropertyBlock(properties); properties.SetFloat("_BuildMode",0); baseRenderer.SetPropertyBlock(properties);
            baseRenderer.enabled=sourceRenderer.enabled && !wasForceRenderingOff;
            for (int i=0; i<activePieces; i++)
            {
                var item=pool[i]; float t=Mathf.Clamp01((visualProgress-item.Delay)/.57f);
                float form=Mathf.SmoothStep(0,1,t/.32f);
                float flight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,1,t));
                item.Renderer.enabled=t>0 && sourceRenderer.enabled && !wasForceRenderingOff;
                item.Pivot.localPosition=item.Center+item.Offset*(1-flight)+Vector3.up*(Mathf.Sin(flight*Mathf.PI)*.18f);
                item.Pivot.localRotation=Quaternion.Slerp(item.Rotation,Quaternion.identity,flight);
                item.Pivot.localScale=Vector3.one*Mathf.Lerp(.8f,1,form);
                sourceRenderer.GetPropertyBlock(properties);
                // Only the incoming piece uses dissolve. The existing building stays solid.
                properties.SetFloat("_BuildMode",1); properties.SetFloat("_BuildReveal",form);
                properties.SetFloat("_BuildClock",clock);
                item.Renderer.SetPropertyBlock(properties);
            }
        }

        public void ResetImmediately()
        {
            if (sourceRenderer!=null && presenting) sourceRenderer.forceRenderingOff=wasForceRenderingOff;
            presenting=confirmed=false; activePieces=0; visualProgress=targetProgress=clock=confirmationAge=0;
            foreach (var item in pool) item.Pivot.gameObject.SetActive(false);
            if (presentation!=null) presentation.SetActive(false);
            enabled=false;
        }
        private void OnDisable() => ResetImmediately();
        private void OnDestroy()
        {
            ResetImmediately();
            if (presentation!=null) { if (Application.isPlaying) Destroy(presentation); else DestroyImmediate(presentation); }
        }
#if UNITY_EDITOR
        public void SampleEditorFlight(int stage,float progress)
        {
            if (Application.isPlaying) return;
            BeginWork(stage); visualProgress=targetProgress=Mathf.Clamp01(progress); clock=progress*3; ApplyPose();
        }
#endif
    }
}
