using UnityEngine;

namespace TowerOfBabel
{
    /// <summary>
    /// Immutable cursor configuration that can be captured, shared, and applied by any system.
    /// </summary>
    public readonly struct CursorStateConfig
    {
        public static CursorStateConfig Current => new(Cursor.lockState, Cursor.visible);
        public static CursorStateConfig UnlockedVisible { get; } =
            new(CursorLockMode.None, true);

        public CursorLockMode LockMode { get; }
        public bool IsVisible { get; }

        public CursorStateConfig(CursorLockMode lockMode, bool isVisible)
        {
            LockMode = lockMode;
            IsVisible = isVisible;
        }

        public void Apply()
        {
            Cursor.lockState = LockMode;
            Cursor.visible = IsVisible;
        }
    }
}
