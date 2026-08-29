using System;
using TowerOfBabel.World.Chunks;
using UnityEngine;

namespace TowerOfBabel.Players
{
    public enum PlayerAnimationState : byte
    {
        Idle,
        Walk,
        Run,
        Jump,
        Gather,
        Process,
        Build,
        Emote,
        Dance
    }

    [Serializable]
    public struct PlayerStateSnapshot
    {
        public uint PlayerId;
        public uint Sequence;
        public Vector3 Position;
        public Quaternion Rotation;
        public PlayerAnimationState AnimationState;

        public PlayerStateSnapshot(uint playerId, uint sequence, Vector3 position, Quaternion rotation,
            PlayerAnimationState animationState)
        {
            PlayerId = playerId;
            Sequence = sequence;
            Position = position;
            Rotation = rotation;
            AnimationState = animationState;
        }
    }

    /// <summary>Pure player state. It never owns or modifies a GameObject.</summary>
    public sealed class PlayerInstance
    {
        private bool hasSnapshot;

        public uint PlayerId { get; }
        public bool IsLocal { get; internal set; }
        public bool IsFriend { get; private set; }
        public bool IsConnected { get; private set; } = true;
        public uint Sequence { get; private set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public PlayerAnimationState AnimationState { get; private set; }
        public ChunkKey CurrentChunk { get; private set; }
        public bool HasChunk { get; private set; }
        public PlayerStateSnapshot CurrentSnapshot => new(
            PlayerId, Sequence, Position, Rotation, AnimationState);

        public event Action<PlayerInstance> StateChanged;
        public event Action<PlayerInstance> Disconnected;

        public PlayerInstance(uint playerId, Vector3 position, Quaternion rotation,
            PlayerAnimationState animationState = PlayerAnimationState.Idle,
            bool isFriend = false, bool isLocal = false)
        {
            PlayerId = playerId;
            Position = position;
            Rotation = NormalizeRotation(rotation);
            AnimationState = animationState;
            IsFriend = isFriend;
            IsLocal = isLocal;
        }

        public bool ApplySnapshot(PlayerStateSnapshot snapshot)
        {
            if (snapshot.PlayerId != PlayerId || !IsConnected)
                return false;
            if (hasSnapshot && !IsNewerSequence(snapshot.Sequence, Sequence))
                return false;

            hasSnapshot = true;
            Sequence = snapshot.Sequence;
            Position = snapshot.Position;
            Rotation = NormalizeRotation(snapshot.Rotation);
            AnimationState = snapshot.AnimationState;
            StateChanged?.Invoke(this);
            return true;
        }

        public PlayerStateSnapshot CreateNextLocalSnapshot(Vector3 position, Quaternion rotation,
            PlayerAnimationState animationState)
        {
            uint nextSequence = hasSnapshot ? Sequence + 1u : 1u;
            PlayerStateSnapshot snapshot = new(PlayerId, nextSequence, position, rotation, animationState);
            ApplySnapshot(snapshot);
            return snapshot;
        }

        public void SetFriend(bool value) => IsFriend = value;

        internal void SetChunk(ChunkKey chunk)
        {
            CurrentChunk = chunk;
            HasChunk = true;
        }

        internal void ClearChunk() => HasChunk = false;

        internal void MarkDisconnected()
        {
            if (!IsConnected)
                return;
            IsConnected = false;
            Disconnected?.Invoke(this);
        }

        private static bool IsNewerSequence(uint candidate, uint current)
        {
            uint difference = candidate - current;
            return difference != 0u && difference < 0x80000000u;
        }

        private static Quaternion NormalizeRotation(Quaternion rotation)
        {
            float magnitude = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y +
                                          rotation.z * rotation.z + rotation.w * rotation.w);
            if (magnitude < 0.0001f || float.IsNaN(magnitude))
                return Quaternion.identity;

            float inverse = 1f / magnitude;
            return new Quaternion(rotation.x * inverse, rotation.y * inverse,
                rotation.z * inverse, rotation.w * inverse);
        }
    }
}
