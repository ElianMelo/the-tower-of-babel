using System;
using TowerOfBabel;
using UnityEngine;

public enum PlayerControlState : byte
{
    Locked,
    Interacting = 1,
    Gathering = Interacting,
    Moving = 2
}

[DisallowMultipleComponent]
public sealed class PlayerControlStateMachine : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private MouseRotator mouseRotator;
    [SerializeField] private PlayerVisuals playerVisuals;

    private bool connected;
    private bool interacting;
    private bool useGatheringAnimation;
    private bool modalInputLocked;

    public PlayerControlState CurrentState { get; private set; } = PlayerControlState.Locked;
    public event Action InteractionInterrupted;
    public event Action GatheringInterrupted
    {
        add => InteractionInterrupted += value;
        remove => InteractionInterrupted -= value;
    }
    public event Action<PlayerControlState> StateChanged;

    private void Awake()
    {
        if (playerVisuals == null)
            playerVisuals = GetComponentInChildren<PlayerVisuals>(true);
        ApplyState(PlayerControlState.Locked);
    }

    public void SetConnected(bool value)
    {
        if (connected == value)
            return;

        connected = value;
        if (!connected && interacting)
        {
            interacting = false;
            InteractionInterrupted?.Invoke();
        }

        EvaluateState();
    }

    public bool BeginGathering() => BeginInteraction(true);

    public bool BeginInteraction(bool playGatheringAnimation = false)
    {
        if (!connected || interacting || modalInputLocked)
            return false;

        interacting = true;
        useGatheringAnimation = playGatheringAnimation;
        EvaluateState();
        return true;
    }

    public void EndGathering() => EndInteraction();

    public void EndInteraction()
    {
        if (!interacting)
            return;

        interacting = false;
        EvaluateState();
    }

    public void SetModalInputLocked(bool locked)
    {
        if (modalInputLocked == locked)
            return;

        modalInputLocked = locked;
        ApplyControlLocks();
    }

    private void EvaluateState()
    {
        PlayerControlState state = !connected
            ? PlayerControlState.Locked
            : interacting ? PlayerControlState.Interacting : PlayerControlState.Moving;
        ApplyState(state);
    }

    private void ApplyState(PlayerControlState state)
    {
        PlayerControlState previousState = CurrentState;
        CurrentState = state;
        ApplyControlLocks();

        if (state == PlayerControlState.Interacting && useGatheringAnimation)
            playerVisuals?.PlayDigging();
        else if (previousState == PlayerControlState.Interacting)
            playerVisuals?.CancelAnimation();

        StateChanged?.Invoke(state);
    }

    private void ApplyControlLocks()
    {
        bool locked = CurrentState != PlayerControlState.Moving || modalInputLocked;
        playerController?.SetControlLocked(locked);
        mouseRotator?.SetControlLocked(locked);
    }
}
