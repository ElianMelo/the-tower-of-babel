//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.0.0
//------------------------------------------------------------------------------
using UnityEngine;

namespace MoveRotateResize.General
{
    public class MoveObjectBackAndForth : MonoBehaviour
    {
        public Transform startPoint; 
        public Transform endPoint; 
        public float speed = 2.0f; 

        private Vector3 initialPosition;
        private Vector3 targetPosition;
        private bool movingToEnd = true;
        private Transform _transform; 

        void Start()
        {
            _transform = transform;
            initialPosition = startPoint.position;
            targetPosition = endPoint.position;
        }

        void FixedUpdate()
        {
            float step = speed * Time.deltaTime;

            _transform.position = Vector3.MoveTowards(_transform.position, targetPosition, step);

            if (Vector3.Distance(_transform.position, targetPosition) < 0.01f)
            {
                movingToEnd = !movingToEnd;
                targetPosition = movingToEnd ? initialPosition : endPoint.position;
            }
        }
    }
} 