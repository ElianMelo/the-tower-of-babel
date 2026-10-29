//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.0.0
//------------------------------------------------------------------------------
using UnityEngine;
namespace MoveRotateResize.DYNAMIC.General
{
    public class FrameRateLimiter : MonoBehaviour
    {
        [Header("Frame Rate Settings")]
        [Tooltip("Set the target frame rate. Set to -1 for unlimited.")]
        public int targetFrameRate = 60;

        [Header("VSync Settings")]
        [Tooltip("Set the VSync count. 0 = Don't Sync, 1 = Sync every V Blank, 2 = Sync every 2nd V Blank")]
        public int vSyncCount = 1;

        void Start()
        {
            ApplySettings();
        }

        void OnValidate()
        {
            ApplySettings();
        }

        void ApplySettings()
        {
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = vSyncCount;
        }
    }
}