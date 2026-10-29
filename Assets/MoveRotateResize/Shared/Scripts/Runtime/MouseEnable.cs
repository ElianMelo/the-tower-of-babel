using UnityEngine;

namespace MoveRotateResize.UI
{
    public class MouseEnable : MonoBehaviour
    {
        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}