using UnityEngine;

namespace TowerOfBabel.Resources.Interaction
{
    /// <summary>Local machine presentation driven by the processor's interaction lifecycle.</summary>
    [DisallowMultipleComponent]
    public sealed class ProcessorWorkAnimation : MonoBehaviour
    {
        [SerializeField] private Transform leftRoller, rightRoller, pressHead, feedStone, outputStone;
        [SerializeField] private Renderer[] statusLights;
        [SerializeField, Min(0)] private float rollerSpeed=280;
        private Transform[] parts;
        private Vector3[] positions, scales;
        private Quaternion[] rotations;
        private MaterialPropertyBlock[] originalLights;
        private MaterialPropertyBlock lightProperties;
        private bool initialized,working,confirmed;
        private float blend,phase,angle,progress,celebration,workTime;
        private const float OutputDuration=1.1f;
        public bool IsWorking => working;
        public Vector3 FeedbackPosition => outputStone!=null ? outputStone.position+Vector3.up*.15f : transform.position;

        private void Awake() { Initialize(); ResetImmediately(); }

        private void Initialize()
        {
            if (initialized) return;
            initialized=true; parts=new[]{leftRoller,rightRoller,pressHead,feedStone,outputStone};
            positions=new Vector3[parts.Length]; scales=new Vector3[parts.Length]; rotations=new Quaternion[parts.Length];
            for (int i=0; i<parts.Length; i++)
                if (parts[i]!=null) { positions[i]=parts[i].localPosition; scales[i]=parts[i].localScale; rotations[i]=parts[i].localRotation; }
            statusLights ??= System.Array.Empty<Renderer>();
            originalLights=new MaterialPropertyBlock[statusLights.Length]; lightProperties=new MaterialPropertyBlock();
            for (int i=0; i<statusLights.Length; i++)
            {
                originalLights[i]=new MaterialPropertyBlock();
                if (statusLights[i]!=null) statusLights[i].GetPropertyBlock(originalLights[i]);
            }
        }

        public void BeginWork()
        {
            Initialize(); working=true; confirmed=false; progress=celebration=workTime=0; ApplyPose();
        }
        public void SetProgress(float value) => progress=Mathf.Clamp01(value);
        public void StopWork(bool completed=false)
        {
            Initialize(); working=false;
            if (completed && !confirmed) { confirmed=true; celebration=OutputDuration; progress=1; }
            ApplyPose();
        }
        private void Update()
        {
            if (!working && blend<=0 && celebration<=0) return;
            Initialize();
            float dt=Time.deltaTime;
            if (working) workTime+=dt;
            blend=Mathf.MoveTowards(blend,working ? 1 : 0,dt/.22f);
            phase+=dt*3.2f; angle=Mathf.Repeat(angle+dt*rollerSpeed*blend,360);
            celebration=Mathf.Max(0,celebration-dt);
            ApplyPose();
        }

        private void ApplyPose()
        {
            float envelope=Mathf.SmoothStep(0,1,blend);
            if (leftRoller!=null) leftRoller.localRotation=rotations[0]*Quaternion.AngleAxis(angle,Vector3.forward);
            if (rightRoller!=null) rightRoller.localRotation=rotations[1]*Quaternion.AngleAxis(-angle,Vector3.forward);
            if (pressHead!=null) pressHead.localPosition=positions[2]+Vector3.down*(.055f*(.5f-.5f*Mathf.Cos(phase*2))*envelope);
            if (feedStone!=null)
            {
                bool visible=working && progress<.94f;
                feedStone.gameObject.SetActive(visible);
                float enter=Mathf.SmoothStep(0,1,workTime/.4f);
                float consume=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,.94f,progress));
                feedStone.localPosition=positions[3]+Vector3.up*((1-enter)*.65f-consume*.52f+Mathf.Sin(phase)*.025f*envelope);
                feedStone.localRotation=rotations[3]*Quaternion.Euler(Mathf.Sin(phase)*4*envelope,0,Mathf.Sin(phase*1.3f)*6*envelope);
                feedStone.localScale=scales[3]*Mathf.Lerp(1,.08f,consume);
            }
            if (outputStone!=null)
            {
                bool visible=confirmed && celebration>0;
                outputStone.gameObject.SetActive(visible);
                float t=1-celebration/OutputDuration;
                float reveal=Mathf.SmoothStep(0,1,t/.18f);
                float collect=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,1,t));
                outputStone.localPosition=visible ? positions[4]+new Vector3(0,Mathf.Sin(t*Mathf.PI)*.13f,-reveal*.19f-collect*.2f) : positions[4];
                outputStone.localRotation=visible ? rotations[4]*Quaternion.Euler(0,Mathf.Sin(t*Mathf.PI)*12,0) : rotations[4];
                outputStone.localScale=scales[4]*(visible ? reveal*(1-collect) : 1);
            }
            for (int i=0; i<statusLights.Length; i++)
            {
                var light=statusLights[i]; if (light==null) continue;
                light.GetPropertyBlock(lightProperties);
                bool filled=progress>=(float)i/statusLights.Length;
                float intensity=celebration>0 ? 2.2f : Mathf.Lerp(.12f,filled ? 1.3f+.25f*Mathf.Sin(phase*1.5f+i) : .2f,envelope);
                lightProperties.SetColor("_EmissionColor",new Color(.08f,.7f,1)*intensity);
                light.SetPropertyBlock(lightProperties);
            }
        }

        public void ResetImmediately()
        {
            if (!initialized) return;
            working=confirmed=false; blend=phase=angle=progress=celebration=workTime=0;
            for (int i=0; i<parts.Length; i++)
                if (parts[i]!=null) { parts[i].localPosition=positions[i]; parts[i].localRotation=rotations[i]; parts[i].localScale=scales[i]; }
            if (feedStone!=null) feedStone.gameObject.SetActive(false);
            if (outputStone!=null) outputStone.gameObject.SetActive(false);
            for (int i=0; i<statusLights.Length; i++) if (statusLights[i]!=null) statusLights[i].SetPropertyBlock(originalLights[i]);
        }
        private void OnDisable() => ResetImmediately();

#if UNITY_EDITOR
        public void SampleEditorPose(float seconds,float workBlend,float normalizedProgress,float completionElapsed=-1)
        {
            if (Application.isPlaying) return;
            Initialize(); blend=Mathf.Clamp01(workBlend); phase=seconds*3.2f; angle=seconds*rollerSpeed; workTime=seconds;
            working=workBlend>0 && completionElapsed<0; confirmed=completionElapsed>=0;
            progress=Mathf.Clamp01(normalizedProgress); celebration=confirmed ? Mathf.Max(0,OutputDuration-completionElapsed) : 0;
            ApplyPose();
        }
#endif
    }
}
