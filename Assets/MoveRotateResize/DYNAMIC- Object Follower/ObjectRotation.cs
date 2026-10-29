//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEngine;

namespace MoveRotateResize.General
{
    public class ObjectRotation : MonoBehaviour
    {
        public Vector3 rotationSpeed = new Vector3(10f, 20f, 30f); 
        public Quaternion startRotation;
        Quaternion targetRotation;
        [SerializeField]private bool pauseRotation = true; 
        [SerializeField]private bool resetPosition = false; 
        float previousDPadX = 0;
        float previousDPadY = 0;


        private void Awake()
        {
            startRotation = transform.rotation;
            targetRotation = transform.rotation;
        }

        private void Update()
        {
            if (!pauseRotation)
            {
                transform.Rotate(rotationSpeed * Time.deltaTime);
            }

            if (resetPosition)
            {
                ResetRotation();
                resetPosition = false; 
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                ToggleRotationPause();
            }

            if (Input.GetKeyDown(KeyCode.F4))
            {
                resetPosition = true;
            }

            float dPadX = Input.GetAxis("DPadX");
            float dPadY = Input.GetAxis("DPadY");
            if (dPadX != 0 && dPadX != previousDPadX)
            {
                if (dPadX < 0)
                {
                    ResetRotation();
                }
            }
            if (dPadY != 0 && dPadY != previousDPadY)
            {
                if (dPadY < 0)
                {
                    ToggleRotationPause();
                }
            }

            previousDPadX = dPadX;
            previousDPadY = dPadY;
        }

        public void ToggleRotationPause()
        {
            pauseRotation = !pauseRotation;

            if (!pauseRotation)
            {
                targetRotation = transform.rotation;
            }
        }

        public void ResetRotation()
        {
            pauseRotation = true; 
            transform.rotation = startRotation;
        }
    }
}