using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PlayerFollower : MonoBehaviour
{
    [SerializeField] private Transform player;
    [Tooltip("Fallback eye height above the player root when a humanoid head bone is unavailable.")]
    [SerializeField, Min(0f)] private float fallbackHeadHeight = 0.75f;
    [Tooltip("Small vertical offset from the center of the humanoid head bone to approximate eye height.")]
    [SerializeField] private float eyeHeightOffset = 0.06f;

    private Vector3 localHeadPosition;
    private bool hasResolvedHeadPosition;

    public void SetupPlayer(Transform playerTransform)
    {
        player = playerTransform;
        ResolveHeadPosition();
    }

    private void Start()
    {
        ResolveHeadPosition();
    }

    private void LateUpdate()
    {
        if (player == null)
            return;
        if (!hasResolvedHeadPosition)
            ResolveHeadPosition();

        transform.position = player.TransformPoint(localHeadPosition);
    }

    private void ResolveHeadPosition()
    {
        hasResolvedHeadPosition = false;
        if (player == null)
            return;

        localHeadPosition = Vector3.up * fallbackHeadHeight;
        Animator animator = player.GetComponentInChildren<Animator>(true);
        Transform head = animator != null && animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.Head)
            : FindHeadTransform();
        if (head != null)
        {
            localHeadPosition = player.InverseTransformPoint(head.position);
            localHeadPosition.y += eyeHeightOffset;
        }

        hasResolvedHeadPosition = true;
    }

    private Transform FindHeadTransform()
    {
        foreach (Transform candidate in player.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == "mixamorig:Head" || candidate.name == "Head")
                return candidate;
        }

        return null;
    }
}
