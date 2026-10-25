using System.Collections.Generic;
using UnityEngine;
using TowerOfBabel.World.Chunks;

namespace TowerOfBabel.World.Tower
{
    /// <summary>Shares the space between neighbouring pillars so setback support tiles meet sideways.</summary>
    public sealed class TowerPillarLedge
    {
        public const float JointOverlap=.04f;
        private readonly Vector2 center;
        private readonly float margin;
        private readonly float minimumRadius=float.PositiveInfinity,maximumRadius;
        private readonly List<Slot> slots=new();
        private struct Slot {public Vector2 point,previous,next;public float radius,angle,left,right,leftFull,rightFull;}
        public TowerPillarLedge(IEnumerable<Vector3> pillars,Vector2 center,float margin)
        {
            this.center=center;this.margin=margin;
            foreach(var pillar in pillars)
            {
                Vector2 point=new(pillar.x,pillar.z),delta=point-center;
                bool duplicate=false;foreach(var existing in slots)if((existing.point-point).sqrMagnitude<.0001f){duplicate=true;break;}
                // Float accumulation can leave the final pillar almost coincident with the first at 360 degrees.
                if(delta.magnitude>margin&&!duplicate)
                {
                    minimumRadius=Mathf.Min(minimumRadius,delta.magnitude);maximumRadius=Mathf.Max(maximumRadius,delta.magnitude);
                    slots.Add(new Slot {point=point,radius=delta.magnitude,angle=Mathf.Repeat(Mathf.Atan2(delta.y,delta.x),Mathf.PI*2)});
                }
            }
            slots.Sort((a,b)=>a.angle.CompareTo(b.angle));
            for(int i=0;i<slots.Count;i++)
            {
                var slot=slots[i];float minimum=Mathf.Asin(Mathf.Clamp01(margin/slot.radius));
                float before=slots.Count>1?Mathf.Repeat(slot.angle-slots[(i+slots.Count-1)%slots.Count].angle,Mathf.PI*2)*.5f:minimum;
                float after=slots.Count>1?Mathf.Repeat(slots[(i+1)%slots.Count].angle-slot.angle,Mathf.PI*2)*.5f:minimum;
                slot.left=Mathf.Max(minimum,before)+JointOverlap/slot.radius;
                slot.right=Mathf.Max(minimum,after)+JointOverlap/slot.radius;
                slot.leftFull=Mathf.Max(minimum,before*2)+JointOverlap/slot.radius;slot.rightFull=Mathf.Max(minimum,after*2)+JointOverlap/slot.radius;
                slot.previous=slots[(i+slots.Count-1)%slots.Count].point;slot.next=slots[(i+1)%slots.Count].point;slots[i]=slot;
            }
        }
        private Slot Find(Vector2 pillar)
        {
            foreach(var slot in slots)if((slot.point-pillar).sqrMagnitude<.0001f)return slot;
            throw new System.ArgumentException("Pillar must belong to this ledge layout.");
        }
        private void Span(Slot slot,TowerFloorSupport mainland,out float left,out float right)
        {
            // At the ends of a support run, reach the neighbouring pillar's existing floor.
            left=mainland!=null&&mainland.SupportsDisk(slot.previous,margin-.05f,out _)?slot.leftFull:slot.left;
            right=mainland!=null&&mainland.SupportsDisk(slot.next,margin-.05f,out _)?slot.rightFull:slot.right;
        }
        public bool TryFindInnerConnection(TowerFloorSupport mainland,Vector2 pillar,out float innerRadius)
        {
            var slot=Find(pillar);Span(slot,mainland,out float left,out float right);
            float stride=Mathf.Max(.05f,margin*.25f);
            for(float radius=slot.radius-margin;radius>.05f;radius-=stride)
            {
                bool connected=true;
                for(int edge=0;edge<=16&&connected;edge++)
                {
                    float angle=slot.angle-left+(left+right)*edge/16;Vector2 direction=new(Mathf.Cos(angle),Mathf.Sin(angle));
                    for(int inset=0;inset<3;inset++)
                        if(!mainland.TryGetHeight(center+direction*Mathf.Max(.001f,radius-inset*.025f),out _)){connected=false;break;}
                }
                if(connected){innerRadius=Mathf.Max(0,radius-.05f);return true;}
            }
            innerRadius=0;return false;
        }
        public TowerFloorLayout.Sector Bridge(Vector2 pillar,float height,float innerRadius,float thicknessScale,TowerFloorSupport mainland=null)
        {
            var slot=Find(pillar);Span(slot,mainland,out float left,out float right);float outer=slot.radius+margin;
            return new TowerFloorLayout.Sector(center,height,outer,slot.angle-left,left+right,
                outer-Mathf.Max(0,Mathf.Min(innerRadius,slot.radius-margin)),thicknessScale);
        }
        public bool IsConnectedSupport(ChunkAssetData asset)
        {
            if(!asset.AssetType.IsFloor())return false;
            Vector2 delta=new Vector2(asset.Position.x,asset.Position.z)-center;float radius=delta.magnitude;
            if(radius<minimumRadius+margin-.01f || radius>maximumRadius+margin+.01f || asset.Scale.x*4<margin*2-.01f)return false;
            float angle=Mathf.Atan2(delta.y,delta.x),sweep=asset.Scale.z*4/radius;
            foreach(var slot in slots)
            {
                if(Mathf.Abs(radius-slot.radius-margin)>=.01f)continue;
                float minimum=Mathf.Asin(Mathf.Clamp01(margin/slot.radius))+JointOverlap/slot.radius;
                // Also recognize a previous seam tile clipped by an almost duplicated end pillar.
                for(int variant=0;variant<9;variant++)
                {
                    float left=variant%3==0?slot.left:variant%3==1?slot.leftFull:minimum;
                    float right=variant/3==0?slot.right:variant/3==1?slot.rightFull:minimum;
                    if(Mathf.Abs(sweep-left-right)<.0001f && Mathf.Abs(Mathf.DeltaAngle(angle*Mathf.Rad2Deg,(slot.angle-left)*Mathf.Rad2Deg))<.005f)return true;
                }
            }
            return false;
        }
        public bool ContainsPillar(ChunkAssetData asset,Vector2 pillar)
        {
            Vector2 delta=new Vector2(asset.Position.x,asset.Position.z)-center;float radius=delta.magnitude;
            if(radius<.001f)return false;
            var sector=new TowerFloorLayout.Sector(center,asset.Position.y,radius,Mathf.Atan2(delta.y,delta.x),asset.Scale.z*4/radius,asset.Scale.x*4,asset.Scale.y);
            return sector.Contains(pillar,center);
        }
    }
}
