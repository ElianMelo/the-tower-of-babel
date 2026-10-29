//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.1
//------------------------------------------------------------------------------
using UnityEngine;

namespace MoveRotateResize.RandomColourPerObject
{
    public class RandomColourPerObject : MonoBehaviour
    {
        [SerializeField] private Color[] selectableColors;
        private bool colorAssignedByMaster = false; 
        private Renderer objectRenderer;

        private void Awake()
        {
            objectRenderer = GetComponent<Renderer>();
        }

        private void Start()
        {
            if (!colorAssignedByMaster)
            {
                AssignRandomColor();
            }
        }

        public void AssignRandomColor()
        {
            if (selectableColors.Length == 0)
            {
                Debug.LogWarning("Selectable colors array is empty. No color will be assigned.");
                return;
            }

            if (objectRenderer == null && !HasChildWithRenderer())
            {
                Debug.LogWarning("No Renderer found on parent or children. Color assignment aborted.");
                return;
            }

            Color randomColor = selectableColors[Random.Range(0, selectableColors.Length)];

            if (objectRenderer != null)
            {
                objectRenderer.material.color = randomColor;
            }

            SetColorForChildren(randomColor);

            colorAssignedByMaster = true;
        }

        private void SetColorForChildren(Color color)
        {
            foreach (Transform child in transform)
            {
                Renderer childRenderer = child.GetComponent<Renderer>();
                if (childRenderer != null)
                {
                    childRenderer.material.color = color;
                }
            }
        }

        private bool HasChildWithRenderer()
        {
            foreach (Transform child in transform)
            {
                if (child.GetComponent<Renderer>() != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}