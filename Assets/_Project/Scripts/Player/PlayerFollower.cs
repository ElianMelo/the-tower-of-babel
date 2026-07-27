using UnityEngine;

public class PlayerFollower : MonoBehaviour
{
    [SerializeField] private Transform player;

    public void SetupPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    private void Update()
    {
        if (player == null) return;
        transform.position = player.position;
    }
}
