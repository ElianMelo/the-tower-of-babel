using System;
using TowerOfBabel;
using UnityEngine;

public enum PlayerControlState : byte
{
    Locked,
    Gathering,
    Moving
}

[DisallowMultipleComponent]
public sealed class PlayerControlStateMachine : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private MouseRotator mouseRotator;
    [SerializeField] private PlayerVisuals playerVisuals;

    private bool connected;
    private bool gathering;
    private bool modalInputLocked;

    public PlayerControlState CurrentState { get; private set; } = PlayerControlState.Locked;
    public event Action GatheringInterrupted;
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
        if (!connected && gathering)
        {
            gathering = false;
            GatheringInterrupted?.Invoke();
        }

        EvaluateState();
    }

    public bool BeginGathering()
    {
        if (!connected || gathering)
            return false;

        gathering = true;
        EvaluateState();
        return true;
    }

    public void EndGathering()
    {
        if (!gathering)
            return;

        gathering = false;
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
            : gathering ? PlayerControlState.Gathering : PlayerControlState.Moving;
        ApplyState(state);
    }

    private void ApplyState(PlayerControlState state)
    {
        PlayerControlState previousState = CurrentState;
        CurrentState = state;
        ApplyControlLocks();

        if (state == PlayerControlState.Gathering)
            playerVisuals?.PlayDigging();
        else if (previousState == PlayerControlState.Gathering)
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
