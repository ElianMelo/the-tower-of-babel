using System;
using UnityEngine;

namespace TowerOfBabel.World.Chunks
{
    /// <summary>
    /// Lightweight player state. A remote player's visual object is optional and is only
    /// enabled while the owning ChunkManager includes this instance in its priority list.
    /// </summary>
    [Serializable]
    public sealed class PlayerInstance
    {
        [SerializeField] private ulong playerId;
        [SerializeField] private bool isLocal;
        [SerializeField] private bool isFriend;
        [SerializeField] private Vector3 position;
        [SerializeField] private Quaternion rotation = Quaternion.identity;
        [SerializeField] private GameObject visualRoot;

        [NonSerialized] private bool isVisualPrioritized;

        public ulong PlayerId => playerId;
        public bool IsLocal => isLocal;
        public bool IsFriend => isFriend;
        public Vector3 Position => position;
        public Quaternion Rotation => rotation;
        public GameObject VisualRoot => visualRoot;
        public bool IsVisualPrioritized => isLocal || isVisualPrioritized;

        public PlayerInstance(ulong playerId, Vector3 position, Quaternion rotation, bool isFriend = false)
        {
            this.playerId = playerId;
            this.position = position;
            this.rotation = rotation;
            this.isFriend = isFriend;
        }

        internal static PlayerInstance CreateLocal(Transform playerTransform)
        {
            PlayerInstance instance = new(0, playerTransform.position, playerTransform.rotation)
            {
                isLocal = true,
                visualRoot = playerTransform.gameObject,
                isVisualPrioritized = true
            };
            return instance;
        }

        public void UpdateState(Vector3 newPosition, Quaternion newRotation)
        {
            position = newPosition;
            rotation = newRotation;
            ApplyTransformToVisual();
        }

        public void SetFriend(bool value)
        {
            isFriend = value;
        }

        public void BindVisual(GameObject value)
        {
            if (visualRoot != null && visualRoot != value && !isLocal)
                visualRoot.SetActive(false);

            visualRoot = value;
            ApplyTransformToVisual();
            ApplyVisualPriority();
        }

        public void UnbindVisual(bool disableVisual = true)
        {
            if (disableVisual && visualRoot != null && !isLocal)
                visualRoot.SetActive(false);

            visualRoot = null;
        }

        internal void SetVisualPriority(bool value)
        {
            if (isLocal || isVisualPrioritized == value)
                return;

            isVisualPrioritized = value;
            ApplyVisualPriority();
        }

        private void ApplyTransformToVisual()
        {
            if (visualRoot == null || isLocal)
                return;

            visualRoot.transform.SetPositionAndRotation(position, rotation);
        }

        private void ApplyVisualPriority()
        {
            if (visualRoot != null && !isLocal && visualRoot.activeSelf != isVisualPrioritized)
                visualRoot.SetActive(isVisualPrioritized);
        }
    }
}
