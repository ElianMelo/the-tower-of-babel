using UnityEngine;

namespace TowerOfBabel
{
    public class PlayerVisuals : MonoBehaviour
    {
        private Animator animator;

        void Start()
        {
            animator = GetComponent<Animator>();
        }
    }
}
