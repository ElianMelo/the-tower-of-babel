using System;
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

    private bool connected;
    private bool gathering;

    public PlayerControlState CurrentState { get; private set; } = PlayerControlState.Locked;
    public event Action GatheringInterrupted;
    public event Action<PlayerControlState> StateChanged;

    private void Awake()
    {
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

    private void EvaluateState()
    {
        PlayerControlState state = !connected
            ? PlayerControlState.Locked
            : gathering ? PlayerControlState.Gathering : PlayerControlState.Moving;
        ApplyState(state);
    }

    private void ApplyState(PlayerControlState state)
    {
        CurrentState = state;
        bool locked = state != PlayerControlState.Moving;
        playerController?.SetControlLocked(locked);
        mouseRotator?.SetControlLocked(locked);
        StateChanged?.Invoke(state);
    }
}
