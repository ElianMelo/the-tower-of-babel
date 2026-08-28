using FishNet.Managing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerOfBabel.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private string localAddress = "localhost";

        private void Awake()
        {
            if (networkManager == null)
                networkManager = GetComponent<NetworkManager>();

#if UNITY_SERVER
            StartDedicatedServer();
#endif
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.f1Key.wasPressedThisFrame)
                StartHost();
            else if (Keyboard.current.f2Key.wasPressedThisFrame)
                StartLocalClient();
        }

        public bool StartHost()
        {
            if (networkManager == null || networkManager.ServerManager.Started || networkManager.ClientManager.Started)
                return false;

            bool serverStarted = networkManager.ServerManager.StartConnection();
            bool clientStarted = serverStarted && networkManager.ClientManager.StartConnection(localAddress);
            return serverStarted && clientStarted;
        }

        public bool StartLocalClient()
        {
            if (networkManager == null || networkManager.ClientManager.Started)
                return false;

            return networkManager.ClientManager.StartConnection(localAddress);
        }

        public bool StartDedicatedServer()
        {
            if (networkManager == null || networkManager.ServerManager.Started)
                return false;

            return networkManager.ServerManager.StartConnection();
        }
    }
}
