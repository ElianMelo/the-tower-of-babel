//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEngine;

namespace MoveRotateResize.Dynamic.SDFOcclusionZoneCreator
{
    public class SDFInfo : MonoBehaviour
    {
        public Texture3D texture3D;
        public Vector3 boxColliderCenter;
        public Vector3 boxColliderSize;
    

        public void GetBoxColliderInfo()
        {
            BoxCollider boxCollider = GetComponent<BoxCollider>();

            if (boxCollider != null)
            {
                boxColliderCenter = boxCollider.center;
                boxColliderSize = boxCollider.size;

            }
            else
            {
                Debug.LogError("No BoxCollider found on this GameObject.");
            }
        }
    }
}