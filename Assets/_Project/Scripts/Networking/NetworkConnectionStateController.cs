using FishNet.Managing;
using FishNet.Transporting;
using TowerOfBabel;
using UnityEngine;

namespace TowerOfBabel.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkConnectionStateController : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private PlayerControlStateMachine playerStateMachine;

        private void Awake()
        {
            if (networkManager == null)
                networkManager = GetComponent<NetworkManager>();

            playerStateMachine?.SetConnected(false);
            InterfaceManager.Instance?.ShowServerDisconnected();
        }

        private void OnEnable()
        {
            if (networkManager?.ClientManager != null)
                networkManager.ClientManager.OnClientConnectionState += HandleClientConnectionState;
        }

        private void Start()
        {
            if (networkManager != null && networkManager.ClientManager.Started)
                ApplyConnected();
            else
                ApplyDisconnected();
        }

        private void OnDisable()
        {
            if (networkManager?.ClientManager != null)
                networkManager.ClientManager.OnClientConnectionState -= HandleClientConnectionState;
        }

        private void HandleClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Starting:
                    playerStateMachine?.SetConnected(false);
                    InterfaceManager.Instance?.ShowServerConnecting();
                    break;
                case LocalConnectionState.Started:
                    ApplyConnected();
                    break;
                case LocalConnectionState.Stopping:
                case LocalConnectionState.Stopped:
                    ApplyDisconnected();
                    break;
            }
        }

        private void ApplyConnected()
        {
            playerStateMachine?.SetConnected(true);
            InterfaceManager.Instance?.HideServerStatus();
        }

        private void ApplyDisconnected()
        {
            playerStateMachine?.SetConnected(false);
            InterfaceManager.Instance?.ShowServerDisconnected();
        }
    }
}
