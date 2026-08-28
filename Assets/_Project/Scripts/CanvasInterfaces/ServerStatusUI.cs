using TMPro;
using UnityEngine;

namespace TowerOfBabel
{
    [DisallowMultipleComponent]
    public sealed class ServerStatusUI : MonoBehaviour
    {
        [SerializeField] private GameObject visuals;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Color disconnectedColor = Color.red;
        [SerializeField] private Color connectingColor = Color.yellow;

        public bool IsVisible => visuals != null && visuals.activeSelf;
        public string StatusText => statusText != null ? statusText.text : string.Empty;

        public void ShowDisconnected() => Show("Not connected to server", disconnectedColor);
        public void ShowConnecting() => Show("Connecting...", connectingColor);
        public void Hide() => visuals?.SetActive(false);

        private void Show(string message, Color color)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
            }
            visuals?.SetActive(true);
        }
    }
}
