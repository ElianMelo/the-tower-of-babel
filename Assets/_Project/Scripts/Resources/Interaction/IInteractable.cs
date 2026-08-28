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
}
