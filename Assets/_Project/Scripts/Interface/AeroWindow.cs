using UnityEngine;
using UnityEngine.Events;

namespace TowerOfBabel.Interface
{
    /// <summary>Escape uses the same close path as the window button, restoring modal input.</summary>
    public sealed class AeroWindow : MonoBehaviour
    {
        [SerializeField] private UnityEvent close = new();
        private void Update()
        {
            if (TowerOfBabel.Settings.GameInput.SettingsPressed)
                close.Invoke();
        }
        public void Close() => close.Invoke();
    }
}
