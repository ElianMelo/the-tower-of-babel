//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.0.0
//------------------------------------------------------------------------------
using UnityEngine;

namespace MoveRotateResize.General
{
    [ExecuteInEditMode]
    public class ObjectFollower : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset;

        void Update()
        {
            if (target != null)
            {
                transform.position = target.position + offset;
                transform.rotation = target.rotation;
            }
        }
    }
}