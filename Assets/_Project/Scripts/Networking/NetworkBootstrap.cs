using FishNet.Managing;
using UnityEngine;

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
            // Remappable keys (F1 and F2 by default).
            if (TowerOfBabel.Settings.GameInput.StartHostPressed)
                StartHost();
            else if (TowerOfBabel.Settings.GameInput.JoinGamePressed)
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
