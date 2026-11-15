using UnityEngine;

/// <summary>
/// The math behind the mouse-wheel camera: a distance of 0 is the first-person eye, anything from
/// the nearest third-person distance up to the maximum is an over-the-shoulder orbit behind the eyes.
/// </summary>
public static class PlayerCameraZoom
{
    /// <summary>
    /// The camera distance after <paramref name="scroll"/> wheel notches. Rolling the wheel forward (positive)
    /// brings the camera in and switches to first person (0) once it passes <paramref name="minDistance"/>;
    /// rolling it back from first person starts the orbit at <paramref name="minDistance"/>.
    /// </summary>
    public static float Step(float distance, float scroll, float minDistance, float maxDistance, float step)
    {
        if (scroll == 0f || float.IsNaN(scroll))
            return distance;

        minDistance = Mathf.Max(0.01f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        step = Mathf.Max(0.01f, step);

        if (scroll < 0f && distance < minDistance)
            return Mathf.Clamp(minDistance + (-scroll - 1f) * step, minDistance, maxDistance);

        float next = distance - scroll * step;
        if (next < minDistance - 0.001f)
            return 0f;
        return Mathf.Min(next, maxDistance);
    }

    /// <summary>0 in first person, 1 once the camera is at least <paramref name="minDistance"/> behind the eyes.</summary>
    public static float ThirdPersonBlend(float distance, float minDistance)
    {
        if (minDistance <= 0f)
            return distance > 0f ? 1f : 0f;
        return Mathf.Clamp01(distance / minDistance);
    }

    /// <summary>
    /// Where the camera sits: <paramref name="distance"/> behind <paramref name="pivot"/> along the view, moved to the
    /// right and up by <paramref name="shoulderOffset"/> scaled by <paramref name="blend"/>. The side offset follows
    /// the camera's heading only and the height offset is world-up, so tilting the view never swings the pivot.
    /// </summary>
    public static Vector3 OrbitPosition(Vector3 pivot, Quaternion rotation, float distance, Vector2 shoulderOffset, float blend)
    {
        Vector3 right = rotation * Vector3.right;
        right.y = 0f;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.zero;
        Vector3 orbitCenter = pivot + right * (shoulderOffset.x * blend) + Vector3.up * (shoulderOffset.y * blend);
        return orbitCenter - rotation * Vector3.forward * distance;
    }

    /// <summary>Frame-rate independent exponential approach of <paramref name="current"/> toward <paramref name="target"/>.</summary>
    public static float Approach(float current, float target, float sharpness, float deltaTime)
    {
        if (sharpness <= 0f || deltaTime <= 0f)
            return sharpness <= 0f ? target : current;
        float next = Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * deltaTime));
        return Mathf.Abs(next - target) < 0.001f ? target : next;
    }

    /// <summary>
    /// Moves the start of a ray cast from the camera up to where it passes closest to <paramref name="eye"/>, so a
    /// third-person camera reaches exactly as far in front of the player as the first-person one, and never hits the
    /// player's own body between the camera and the eyes.
    /// </summary>
    public static Ray StartNear(Ray cameraRay, Vector3 eye)
    {
        float along = Vector3.Dot(eye - cameraRay.origin, cameraRay.direction);
        if (along > 0f)
            cameraRay.origin += cameraRay.direction * along;
        return cameraRay;
    }
}
