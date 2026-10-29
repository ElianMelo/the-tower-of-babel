//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MoveRotateResize.Dynamic.PuddlePainter
{
    [RequireComponent(typeof(DecalProjector))]
    public class PuddleDecalLayers : MonoBehaviour
    {
        DecalProjector decalProjector;

    #if USING_UNITY_6
        [SerializeField] UnityEngine.Rendering.HighDefinition.RenderingLayerMask decalLayerMaskNew = 
        (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)(1 << 1 | 1 << 9); 
    #endif
    #if USING_UNITY_2022
        [SerializeField] DecalLayerEnum decalLayerMaskOld = (DecalLayerEnum)(1 << 1 | 1 << 9);
    #endif

        void Awake()
        {
            ApplyLayerSettings();
        }

        void OnValidate()
        {
            ApplyLayerSettings();
        }

        void Reset()
        {
            ApplyLayerSettings();
        }

        void ApplyLayerSettings()
        {
            if (!decalProjector)
            {
                decalProjector = GetComponent<DecalProjector>();
            }

            #if USING_UNITY_6
            decalProjector.decalLayerMask = decalLayerMaskNew;
            #endif
            #if USING_UNITY_2022
            decalProjector.decalLayerMask = decalLayerMaskOld;
            #endif
        }
    }
}