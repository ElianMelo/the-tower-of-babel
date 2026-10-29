//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Rendering.Universal; // Ported from HDRP: project uses URP.

namespace MoveRotateResize.Dynamic.PuddlePainter
{
    [RequireComponent(typeof(DecalProjector))]
    public class PuddleDecalLayers : MonoBehaviour
    {
        DecalProjector decalProjector;

        // URP rendering layers 1 and 9 (matches the original HDRP default).
        [SerializeField] RenderingLayerMask decalLayerMask = (RenderingLayerMask)(uint)(1 << 1 | 1 << 9);

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

            decalProjector.renderingLayerMask = decalLayerMask;
        }
    }
}