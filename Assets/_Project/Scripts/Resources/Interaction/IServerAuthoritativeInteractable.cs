using System;
using UnityEngine;

namespace TowerOfBabel.Resources.Interaction
{
    public interface IServerAuthoritativeInteractable
    {
        event Action ServerRejected;
        bool RequestServerStart(GameObject interactor);
        void RequestServerCancel();
    }
}
