using System;
using UnityEngine;

namespace TowerOfBabel.Resources.Interaction
{
    public interface IInteractable
    {
        string ObjectName { get; }
        string DetailText { get; }
        Color DetailColor { get; }
        string PromptText { get; }
        float Duration { get; }
        bool CanInteract { get; }

        void BeginInteraction(GameObject interactor);
        void UpdateInteraction(float normalizedProgress);
        void CancelInteraction();
        void CompleteInteraction(GameObject interactor);
    }

    /// <summary>
    /// Optional presentation hooks for interactables whose prompt and local feedback can change
    /// while they remain under the player's crosshair.
    /// </summary>
    public interface IInteractionPresentation
    {
        bool ShouldShowInteraction { get; }
        event Action InteractionPresentationChanged;

        void SetInteractionFocused(bool focused);
    }
}
