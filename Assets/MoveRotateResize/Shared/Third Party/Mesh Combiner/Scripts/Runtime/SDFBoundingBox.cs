
//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEngine;

namespace MoveRotateResize.Dynamic.SDFOcclusionZoneCreator
{
    public class SDFBoundingBox : MonoBehaviour
    {
        public Vector3 boundingBoxCenter;
        public Vector3 boundingBoxSize;

        public void CalculateBoundingBox()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    Bounds meshBounds = meshFilter.sharedMesh.bounds;

                    boundingBoxSize = Vector3.Scale(meshBounds.size, transform.localScale);
                    boundingBoxCenter = meshBounds.center;

                    // Debug.Log("Bounding Box (Local Space):");
                    // Debug.Log("Center: " + boundingBoxCenter);
                    // Debug.Log("Size: " + boundingBoxSize);
                }
                else
                {
                    Debug.LogError("MeshFilter component not found on the object.");
                }
            }
            else
            {
                Debug.LogError("MeshRenderer component not found on the object.");
            }
        }
    }
}