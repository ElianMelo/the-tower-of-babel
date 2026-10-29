//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.1
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MoveRotateResize.Dynamic.PuddlePainter
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent] 
    public class PuddlePainterDontTouch : MonoBehaviour
    {
        public DecalProjector sourceProjector;
        public DecalProjector targetProjector;
        public Camera targetCamera; 
        private bool isPlaying = false;

        void OnEnable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += PlayModeStateChanged;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= PlayModeStateChanged;
#endif
        }

#if UNITY_EDITOR
        void PlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.EnteredPlayMode)
            {
                isPlaying = true;
            }
            else if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                isPlaying = false;
            }
        }
#endif

        void Update()
        {
            if (!isPlaying)
            {
                CopySettings();
            }
        }

        void CopySettings()
        {
            if (sourceProjector != null && targetProjector != null)
            {
                targetProjector.size = sourceProjector.size;
                targetProjector.pivot = sourceProjector.pivot;
                targetProjector.decalLayerMask = sourceProjector.decalLayerMask;
                targetProjector.drawDistance = sourceProjector.drawDistance;
                targetProjector.fadeScale = sourceProjector.fadeScale;
                targetProjector.startAngleFade = sourceProjector.startAngleFade;
                targetProjector.endAngleFade = sourceProjector.endAngleFade;
                targetProjector.uvScale = sourceProjector.uvScale;
                targetProjector.uvBias = sourceProjector.uvBias;
                targetProjector.fadeFactor = sourceProjector.fadeFactor;

                if (targetCamera != null)
                {
                    float sizeX = sourceProjector.size.x;
                    targetCamera.orthographicSize = sizeX / 2;
                }
                //Debug.Log($"Source Projector Size: {sourceProjector.size}");
                //Debug.Log($"Target Projector Size: {targetProjector.size}");
                //Debug.Log($"Camera Orthographic Size: {targetCamera.orthographicSize}");
            }
            else
            {
                Debug.LogError("Source or target Decal Projector is not assigned!");
            }
        }
    }
}