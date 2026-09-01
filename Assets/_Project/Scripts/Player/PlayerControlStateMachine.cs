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
    private bool cameraInputLocked;

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

    public void SetCameraInputLocked(bool locked)
    {
        if (cameraInputLocked == locked)
            return;

        cameraInputLocked = locked;
        ApplyMouseLock();
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
        bool locked = state != PlayerControlState.Moving;
        playerController?.SetControlLocked(locked);
        ApplyMouseLock();

        if (state == PlayerControlState.Gathering)
            playerVisuals?.PlayDigging();
        else if (previousState == PlayerControlState.Gathering)
            playerVisuals?.CancelAnimation();

        StateChanged?.Invoke(state);
    }

    private void ApplyMouseLock()
    {
        mouseRotator?.SetControlLocked(CurrentState != PlayerControlState.Moving || cameraInputLocked);
    }
}
