using System.Collections.Generic;
using TowerOfBabel.Players;
using UnityEngine;

namespace TowerOfBabel
{
    public enum PlayerAnimationTrigger : byte
    {
        None,
        Dancing,
        Digging,
        Golf,
        HipHopOne,
        HipHopTwo,
        HipHopThree,
        Jump,
        RumbaDance,
        SillyDancing,
        CancelAnimation
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class PlayerVisuals : MonoBehaviour
    {
        private static readonly int WalkingHash = Animator.StringToHash("Walking");
        private static readonly int RunningHash = Animator.StringToHash("Running");

        private static readonly Dictionary<PlayerAnimationTrigger, string> TriggerNames = new()
        {
            { PlayerAnimationTrigger.Dancing, "Dancing" },
            { PlayerAnimationTrigger.Digging, "Digging" },
            { PlayerAnimationTrigger.Golf, "Golf" },
            { PlayerAnimationTrigger.HipHopOne, "HipHopOne" },
            { PlayerAnimationTrigger.HipHopTwo, "HipHopTwo" },
            { PlayerAnimationTrigger.HipHopThree, "HipHopThree" },
            { PlayerAnimationTrigger.Jump, "Jump" },
            { PlayerAnimationTrigger.RumbaDance, "RumbaDance" },
            { PlayerAnimationTrigger.SillyDancing, "SillyDancing" },
            { PlayerAnimationTrigger.CancelAnimation, "CancelAnimation" }
        };

        [SerializeField] private Animator animator;
        [Tooltip("Collapses the head bone only for the locally controlled first-person avatar.")]
        [SerializeField] private bool hideHeadForLocalPlayer = true;

        private bool walking;
        private bool running;
        private bool actionActive;
        private bool actionCancelableByMovement;
        private PlayerAnimationTrigger pendingTrigger;
        private PlayerAnimationState pendingState;
        private bool pendingCancelableByMovement;
        private int pendingAfterFrame;
        private uint lastAppliedRemoteTriggerSequence;
        private bool hasAppliedRemoteTriggerSequence;

        public PlayerAnimationState CurrentAnimationState { get; private set; } = PlayerAnimationState.Idle;
        public PlayerAnimationTrigger LatestTrigger { get; private set; }
        public uint TriggerSequence { get; private set; }

        private bool HasAnimator => animator != null && animator.runtimeAnimatorController != null;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            HideLocalPlayerHead();
            ValidateAnimatorParameters();
        }

        private void Update()
        {
            if (pendingTrigger == PlayerAnimationTrigger.None || Time.frameCount <= pendingAfterFrame)
                return;

            PlayerAnimationTrigger trigger = pendingTrigger;
            PlayerAnimationState state = pendingState;
            bool cancelableByMovement = pendingCancelableByMovement;
            ClearPendingTrigger();
            FireTrigger(trigger);
            CurrentAnimationState = state;
            actionActive = true;
            actionCancelableByMovement = cancelableByMovement;
        }

        public void SetMovement(bool isWalking, bool isRunning, bool isGrounded)
        {
            bool nextRunning = isRunning;
            bool nextWalking = isWalking && !nextRunning;
            bool movementStarted = (nextWalking || nextRunning) && !walking && !running;

            walking = nextWalking;
            running = nextRunning;
            SetBool(WalkingHash, walking);
            SetBool(RunningHash, running);

            if (movementStarted && actionActive && actionCancelableByMovement)
                CancelAnimation();

            if (CurrentAnimationState == PlayerAnimationState.Jump && isGrounded)
            {
                actionActive = false;
                actionCancelableByMovement = false;
            }

            if (!actionActive && pendingTrigger == PlayerAnimationTrigger.None)
                CurrentAnimationState = GetLocomotionState();
        }

        public void PlayJump() => PlayAction(PlayerAnimationTrigger.Jump, PlayerAnimationState.Jump, false);

        public void PlayDigging() => PlayAction(PlayerAnimationTrigger.Digging, PlayerAnimationState.Gather, true);

        public void PlayGolf() => PlayAction(PlayerAnimationTrigger.Golf, PlayerAnimationState.Emote, true);

        public void PlayEmote(PlayerAnimationTrigger trigger)
        {
            if (!IsEmote(trigger))
            {
                Debug.LogWarning($"{trigger} is not an emote animation.", this);
                return;
            }

            PlayAction(trigger, PlayerAnimationState.Dance, true);
        }

        public void CancelAnimation()
        {
            ClearPendingTrigger();
            FireTrigger(PlayerAnimationTrigger.CancelAnimation);
            actionActive = false;
            actionCancelableByMovement = false;
            CurrentAnimationState = GetLocomotionState();
            RecordTrigger(PlayerAnimationTrigger.CancelAnimation);
        }

        public void ApplyNetworkAnimation(PlayerAnimationState state, PlayerAnimationTrigger trigger, uint triggerSequence)
        {
            walking = state == PlayerAnimationState.Walk;
            running = state == PlayerAnimationState.Run;
            SetBool(WalkingHash, walking);
            SetBool(RunningHash, running);

            if (!hasAppliedRemoteTriggerSequence || triggerSequence != lastAppliedRemoteTriggerSequence)
            {
                hasAppliedRemoteTriggerSequence = true;
                lastAppliedRemoteTriggerSequence = triggerSequence;
                if (trigger == PlayerAnimationTrigger.CancelAnimation)
                    CancelWithoutRecording();
                else if (trigger != PlayerAnimationTrigger.None)
                    PlayActionWithoutRecording(trigger, state, IsCancelableByMovement(trigger));
            }

            if (!actionActive && pendingTrigger == PlayerAnimationTrigger.None)
                CurrentAnimationState = state;
        }

        public void ResetNetworkAnimation()
        {
            ClearPendingTrigger();
            walking = false;
            running = false;
            actionActive = false;
            actionCancelableByMovement = false;
            CurrentAnimationState = PlayerAnimationState.Idle;
            LatestTrigger = PlayerAnimationTrigger.None;
            hasAppliedRemoteTriggerSequence = false;
            lastAppliedRemoteTriggerSequence = 0u;
            SetBool(WalkingHash, false);
            SetBool(RunningHash, false);
            FireTrigger(PlayerAnimationTrigger.CancelAnimation);
        }

        public static bool IsEmote(PlayerAnimationTrigger trigger) =>
            trigger == PlayerAnimationTrigger.Dancing ||
            trigger == PlayerAnimationTrigger.HipHopOne ||
            trigger == PlayerAnimationTrigger.HipHopTwo ||
            trigger == PlayerAnimationTrigger.HipHopThree ||
            trigger == PlayerAnimationTrigger.RumbaDance ||
            trigger == PlayerAnimationTrigger.SillyDancing;

        private void PlayAction(PlayerAnimationTrigger trigger, PlayerAnimationState state, bool cancelableByMovement)
        {
            PlayActionInternal(trigger, state, cancelableByMovement);
            RecordTrigger(trigger);
        }

        private void PlayActionWithoutRecording(PlayerAnimationTrigger trigger, PlayerAnimationState state,
            bool cancelableByMovement)
        {
            PlayActionInternal(trigger, state, cancelableByMovement);
            LatestTrigger = trigger;
        }

        private void PlayActionInternal(PlayerAnimationTrigger trigger, PlayerAnimationState state,
            bool cancelableByMovement)
        {
            bool replacesAction = actionActive || pendingTrigger != PlayerAnimationTrigger.None || walking || running;
            if (replacesAction)
            {
                CancelWithoutRecording();
                walking = false;
                running = false;
                SetBool(WalkingHash, false);
                SetBool(RunningHash, false);
                pendingTrigger = trigger;
                pendingState = state;
                pendingCancelableByMovement = cancelableByMovement;
                pendingAfterFrame = Time.frameCount;
                CurrentAnimationState = state;
                actionActive = true;
                actionCancelableByMovement = cancelableByMovement;
                return;
            }

            FireTrigger(trigger);
            CurrentAnimationState = state;
            actionActive = true;
            actionCancelableByMovement = cancelableByMovement;
        }

        private void CancelWithoutRecording()
        {
            ClearPendingTrigger();
            FireTrigger(PlayerAnimationTrigger.CancelAnimation);
            actionActive = false;
            actionCancelableByMovement = false;
            CurrentAnimationState = GetLocomotionState();
        }

        private void RecordTrigger(PlayerAnimationTrigger trigger)
        {
            LatestTrigger = trigger;
            TriggerSequence++;
        }

        private void FireTrigger(PlayerAnimationTrigger trigger)
        {
            if (!HasAnimator || !TriggerNames.TryGetValue(trigger, out string parameterName))
                return;
            animator.SetTrigger(Animator.StringToHash(parameterName));
        }

        private void SetBool(int parameterHash, bool value)
        {
            if (HasAnimator)
                animator.SetBool(parameterHash, value);
        }

        private PlayerAnimationState GetLocomotionState()
        {
            if (running)
                return PlayerAnimationState.Run;
            return walking ? PlayerAnimationState.Walk : PlayerAnimationState.Idle;
        }

        private void ClearPendingTrigger()
        {
            pendingTrigger = PlayerAnimationTrigger.None;
            pendingState = PlayerAnimationState.Idle;
            pendingCancelableByMovement = false;
            pendingAfterFrame = 0;
        }

        private static bool IsCancelableByMovement(PlayerAnimationTrigger trigger) =>
            trigger != PlayerAnimationTrigger.None &&
            trigger != PlayerAnimationTrigger.Jump &&
            trigger != PlayerAnimationTrigger.CancelAnimation;

        private void ValidateAnimatorParameters()
        {
            if (!HasAnimator)
                return;

            Dictionary<int, AnimatorControllerParameterType> parameters = new();
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                parameters[parameter.nameHash] = parameter.type;

            ValidateParameter(parameters, WalkingHash, "Walking", AnimatorControllerParameterType.Bool);
            ValidateParameter(parameters, RunningHash, "Running", AnimatorControllerParameterType.Bool);
            foreach (KeyValuePair<PlayerAnimationTrigger, string> entry in TriggerNames)
                ValidateParameter(parameters, Animator.StringToHash(entry.Value), entry.Value,
                    AnimatorControllerParameterType.Trigger);
        }

        private void HideLocalPlayerHead()
        {
            if (!hideHeadForLocalPlayer || GetComponentInParent<PlayerController>() == null)
                return;

            Transform head = FindHeadTransform();
            if (head != null)
                head.localScale = Vector3.zero;
            else
                Debug.LogWarning("Could not find the local player's head bone to hide it.", this);
        }

        private Transform FindHeadTransform()
        {
            if (animator != null && animator.isHuman)
            {
                Transform humanoidHead = animator.GetBoneTransform(HumanBodyBones.Head);
                if (humanoidHead != null)
                    return humanoidHead;
            }

            foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == "mixamorig:Head" || candidate.name == "Head")
                    return candidate;
            }

            return null;
        }

        private void ValidateParameter(Dictionary<int, AnimatorControllerParameterType> parameters, int hash,
            string parameterName, AnimatorControllerParameterType expectedType)
        {
            if (!parameters.TryGetValue(hash, out AnimatorControllerParameterType actualType) || actualType != expectedType)
            {
                Debug.LogError(
                    $"Animator on {name} must contain a {expectedType} parameter named '{parameterName}'.", this);
            }
        }
    }
}
