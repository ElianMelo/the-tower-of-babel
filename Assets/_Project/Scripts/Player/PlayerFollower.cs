using System.Collections.Generic;
using TowerOfBabel;
using UnityEngine;

/// <summary>
/// Places the camera target at the local player's eyes (first person) or behind them (third person). The mouse wheel
/// moves between the two through <see cref="Zoom"/>; the rotation is left to <see cref="MouseRotator"/>.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlayerFollower : MonoBehaviour
{
    private static readonly List<PlayerFollower> Active = new();

    [SerializeField] private Transform player;
    [Tooltip("Fallback eye height above the player root when a humanoid head bone is unavailable.")]
    [SerializeField, Min(0f)] private float fallbackHeadHeight = 0.75f;
    [Tooltip("Small vertical offset from the center of the humanoid head bone to approximate eye height.")]
    [SerializeField] private float eyeHeightOffset = 0.06f;

    [Header("Zoom")]
    [Tooltip("Camera distance at start: 0 is first person.")]
    [SerializeField, Min(0f)] private float startDistance;
    [Tooltip("Nearest third-person distance behind the eyes. Rolling the wheel forward past it switches to first person.")]
    [SerializeField, Min(0.1f)] private float minThirdPersonDistance = 1.75f;
    [SerializeField, Min(0.1f)] private float maxThirdPersonDistance = 6f;
    [Tooltip("Metres per mouse-wheel notch.")]
    [SerializeField, Min(0.05f)] private float zoomStep = 0.75f;
    [Tooltip("Third-person offset from the eyes: x to the right, y up.")]
    [SerializeField] private Vector2 shoulderOffset = new(0.45f, 0.25f);
    [Tooltip("How quickly the camera glides to a new zoom distance.")]
    [SerializeField, Min(0.1f)] private float zoomSharpness = 12f;

    [Header("Third-person collision")]
    [SerializeField, Min(0f)] private float collisionRadius = 0.2f;
    [SerializeField] private LayerMask collisionMask = Physics.DefaultRaycastLayers;
    [Tooltip("Metres per second the camera backs out again after an obstacle clears.")]
    [SerializeField, Min(0.1f)] private float collisionRecoverySpeed = 6f;
    [Tooltip("Below this camera distance the local avatar's head stays hidden so it never fills the view.")]
    [SerializeField, Min(0f)] private float headHiddenBelowDistance = 0.6f;

    private readonly RaycastHit[] collisionHits = new RaycastHit[16];
    private Vector3 localHeadPosition;
    private bool hasResolvedHeadPosition;
    private PlayerVisuals visuals;
    private float targetDistance;
    private float currentDistance;
    private float collisionLimit = float.MaxValue;
    private int headVisibility = -1;

    /// <summary>The player this camera follows.</summary>
    public Transform Target => player;
    /// <summary>The eye position the camera looks from in first person and orbits in third person.</summary>
    public Vector3 Pivot { get; private set; }
    /// <summary>The zoom distance the wheel asked for: 0 is first person.</summary>
    public float TargetDistance => targetDistance;
    /// <summary>The camera's actual distance from <see cref="Pivot"/> this frame, after easing and obstacles.</summary>
    public float CameraDistance { get; private set; }
    public bool IsThirdPerson => targetDistance > 0f;
    public float MinThirdPersonDistance => minThirdPersonDistance;
    public float MaxThirdPersonDistance => Mathf.Max(minThirdPersonDistance, maxThirdPersonDistance);

    /// <summary>The camera follower of <paramref name="playerTransform"/> (or of its parent or child), if any.</summary>
    public static PlayerFollower For(Transform playerTransform)
    {
        if (playerTransform == null)
            return null;
        foreach (PlayerFollower follower in Active)
        {
            Transform target = follower != null ? follower.player : null;
            if (target != null && (target == playerTransform || target.IsChildOf(playerTransform) || playerTransform.IsChildOf(target)))
                return follower;
        }

        return null;
    }

    public void SetupPlayer(Transform playerTransform)
    {
        player = playerTransform;
        ResolveHeadPosition();
    }

    /// <summary>Applies <paramref name="scroll"/> mouse-wheel notches: forward zooms in toward first person.</summary>
    public void Zoom(float scroll)
    {
        targetDistance = PlayerCameraZoom.Step(targetDistance, scroll, minThirdPersonDistance, MaxThirdPersonDistance, zoomStep);
    }

    /// <summary>Jumps straight to <paramref name="distance"/> (0 for first person) without easing.</summary>
    public void SetDistanceImmediate(float distance)
    {
        targetDistance = distance <= 0f ? 0f : Mathf.Clamp(distance, minThirdPersonDistance, MaxThirdPersonDistance);
        currentDistance = targetDistance;
        collisionLimit = float.MaxValue;
    }

    private void Awake()
    {
        SetDistanceImmediate(startDistance);
    }

    private void OnEnable()
    {
        if (!Active.Contains(this))
            Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
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

        Pivot = player.TransformPoint(localHeadPosition);
        float deltaTime = Application.isPlaying ? Time.deltaTime : 0f;
        currentDistance = Application.isPlaying
            ? PlayerCameraZoom.Approach(currentDistance, targetDistance, zoomSharpness, deltaTime)
            : targetDistance;

        float blend = PlayerCameraZoom.ThirdPersonBlend(currentDistance, minThirdPersonDistance);
        Vector3 offset = PlayerCameraZoom.OrbitPosition(Pivot, transform.rotation, currentDistance, shoulderOffset, blend) - Pivot;
        float length = offset.magnitude;
        if (length < 0.0001f)
        {
            collisionLimit = float.MaxValue;
            CameraDistance = 0f;
            transform.position = Pivot;
        }
        else
        {
            Vector3 direction = offset / length;
            float clear = ClearDistance(Pivot, direction, length);
            bool blocked = clear < length - 0.001f;
            // Obstacles pull the camera in at once; it backs out again smoothly once they clear.
            float limit = blocked ? clear : float.MaxValue;
            collisionLimit = limit < collisionLimit || deltaTime <= 0f
                ? limit
                : Mathf.MoveTowards(Mathf.Min(collisionLimit, MaxThirdPersonDistance + 1f), limit, collisionRecoverySpeed * deltaTime);
            CameraDistance = Mathf.Min(length, collisionLimit);
            transform.position = Pivot + direction * CameraDistance;
        }

        UpdateHeadVisibility();
    }

    private float ClearDistance(Vector3 origin, Vector3 direction, float length)
    {
        int count = Physics.SphereCastNonAlloc(origin, collisionRadius, direction, collisionHits, length, collisionMask,
            QueryTriggerInteraction.Ignore);
        float nearest = length;
        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = collisionHits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(player))
                continue;
            nearest = Mathf.Min(nearest, collisionHits[i].distance);
        }

        return Mathf.Max(0f, nearest);
    }

    private void UpdateHeadVisibility()
    {
        int visible = CameraDistance > headHiddenBelowDistance ? 1 : 0;
        if (visible == headVisibility || visuals == null)
            return;

        headVisibility = visible;
        visuals.SetLocalHeadVisible(visible == 1);
    }

    private void ResolveHeadPosition()
    {
        hasResolvedHeadPosition = false;
        headVisibility = -1;
        visuals = null;
        if (player == null)
            return;

        visuals = player.GetComponentInChildren<PlayerVisuals>(true);
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
            if (candidate.name == "mixamorig:Head" || candidate.name == "Head" || candidate.name == "Bip001 Head")
                return candidate;
        }

        return null;
    }
}
