using UnityEngine;

namespace TowerOfBabel.Players
{
    /// <summary>Visual-only remote avatar driven by buffered PlayerInstance snapshots.</summary>
    [DisallowMultipleComponent]
    public sealed class RemotePlayerView : MonoBehaviour
    {
        private const int BufferCapacity = 4;

        private struct PresentationSnapshot
        {
            public double ReceivedAt;
            public Vector3 Position;
            public Quaternion Rotation;
            public PlayerAnimationState AnimationState;
        }

        [SerializeField] private Animator animator;
        [SerializeField] private string animationStateParameter = "AnimationState";

        private readonly PresentationSnapshot[] snapshots = new PresentationSnapshot[BufferCapacity];
        private PlayerInstance instance;
        private int oldestIndex;
        private int snapshotCount;
        private int animationStateHash;
        private PlayerAnimationState appliedAnimationState;
        private bool hasAppliedAnimation;
        private double interpolationDelay = 0.1d;

        public PlayerInstance Instance => instance;
        public bool IsBound => instance != null;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            animationStateHash = Animator.StringToHash(animationStateParameter);
        }

        public void Bind(PlayerInstance playerInstance, double snapshotInterpolationDelay)
        {
            Unbind();
            instance = playerInstance;
            interpolationDelay = System.Math.Max(0d, snapshotInterpolationDelay);
            oldestIndex = 0;
            snapshotCount = 0;
            hasAppliedAnimation = false;

            if (instance == null)
                return;
            instance.StateChanged += HandleStateChanged;
            PushSnapshot(instance, Time.unscaledTimeAsDouble);
            transform.SetPositionAndRotation(instance.Position, instance.Rotation);
            ApplyAnimation(instance.AnimationState);
        }

        public void Unbind()
        {
            if (instance != null)
                instance.StateChanged -= HandleStateChanged;
            instance = null;
            oldestIndex = 0;
            snapshotCount = 0;
            hasAppliedAnimation = false;
        }

        public void Render(double now)
        {
            if (instance == null || snapshotCount == 0)
                return;

            double renderTime = now - interpolationDelay;
            PresentationSnapshot oldest = GetSnapshot(0);
            PresentationSnapshot newest = GetSnapshot(snapshotCount - 1);
            if (snapshotCount == 1 || renderTime <= oldest.ReceivedAt)
            {
                ApplySnapshot(oldest);
                return;
            }
            if (renderTime >= newest.ReceivedAt)
            {
                ApplySnapshot(newest);
                return;
            }

            for (int i = 1; i < snapshotCount; i++)
            {
                PresentationSnapshot next = GetSnapshot(i);
                if (next.ReceivedAt < renderTime)
                    continue;

                PresentationSnapshot previous = GetSnapshot(i - 1);
                double duration = next.ReceivedAt - previous.ReceivedAt;
                float t = duration <= 0.000001d ? 1f : (float)((renderTime - previous.ReceivedAt) / duration);
                transform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(previous.Position, next.Position, t),
                    Quaternion.SlerpUnclamped(previous.Rotation, next.Rotation, t));
                ApplyAnimation(t < 0.5f ? previous.AnimationState : next.AnimationState);
                return;
            }
        }

        private void HandleStateChanged(PlayerInstance changedInstance)
        {
            PushSnapshot(changedInstance, Time.unscaledTimeAsDouble);
        }

        private void PushSnapshot(PlayerInstance source, double receivedAt)
        {
            int index;
            if (snapshotCount < BufferCapacity)
            {
                index = (oldestIndex + snapshotCount) % BufferCapacity;
                snapshotCount++;
            }
            else
            {
                index = oldestIndex;
                oldestIndex = (oldestIndex + 1) % BufferCapacity;
            }

            snapshots[index] = new PresentationSnapshot
            {
                ReceivedAt = receivedAt,
                Position = source.Position,
                Rotation = source.Rotation,
                AnimationState = source.AnimationState
            };
        }

        private PresentationSnapshot GetSnapshot(int logicalIndex) =>
            snapshots[(oldestIndex + logicalIndex) % BufferCapacity];

        private void ApplySnapshot(PresentationSnapshot snapshot)
        {
            transform.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
            ApplyAnimation(snapshot.AnimationState);
        }

        private void ApplyAnimation(PlayerAnimationState state)
        {
            if (hasAppliedAnimation && appliedAnimationState == state)
                return;
            appliedAnimationState = state;
            hasAppliedAnimation = true;

            // The placeholder Animator intentionally has no controller. Avoid warnings until
            // the final model/controller is assigned by the developer.
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetInteger(animationStateHash, (int)state);
        }

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }
    }
}
