using UnityEngine;

public class CircleEdgeGenerator : MonoBehaviour
{
    public GameObject pillarObject;
    public GameObject archObject;

    public float radius;
    public float pillarAngle;
    public float yOffset;

    public float radiusFactor;
    public float heighIncrease;
    public int levels;
    public float decreaseAmount;

    public Vector2 center = Vector2.zero;

    private float baseHeight = 0f;

    void Start()
    {
        float currentRadius = radius;
        float currentPillarAngle = pillarAngle;
        for (int i = 0; i < levels; i++)
        {
            currentRadius -= decreaseAmount;
            currentPillarAngle = pillarAngle / (currentRadius / radius);

            float angleRad = currentPillarAngle * Mathf.Deg2Rad;
            for (float angle = 0f; angle < Mathf.PI * 2f; angle += angleRad)
            {
                Vector2 point = CalculatePillarPoint(angle, currentRadius);
                Vector2 nextPoint = CalculatePillarPoint(angle + angleRad, currentRadius);
                var obj = Instantiate(pillarObject, new Vector3(point.x, baseHeight, point.y), Quaternion.identity);
                var objArch = Instantiate(archObject, new Vector3(point.x, baseHeight + yOffset, point.y), Quaternion.identity);
                Vector2 dir = (nextPoint - point).normalized;
                float yAngle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                yAngle -= 90f;
                objArch.transform.rotation = Quaternion.Euler(0, yAngle, 0);
                obj.name = "Pillar " + angle;
            }

            baseHeight += heighIncrease;
        }
    }

    private Vector2 CalculatePillarPoint(float value, float radius)
    {
        return center + new Vector2(
            Mathf.Cos(value),
            Mathf.Sin(value)
        ) * radius;
    }

    void Update()
    {
        
    }
}
