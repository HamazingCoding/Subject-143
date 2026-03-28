using UnityEngine;

public class CameraCollision : MonoBehaviour
{
    public Transform pivot;     // camera pivot (your up/down rotation point)
    public float defaultDistance = 3f;
    public float minDistance = 0.5f;
    public float smoothSpeed = 10f;

    public LayerMask collisionMask;

    float currentDistance;

    void Start()
    {
        currentDistance = defaultDistance;
    }

    void LateUpdate()
    {
        Vector3 direction = (transform.position - pivot.position).normalized;

        RaycastHit hit;

        if (Physics.SphereCast(
            pivot.position,
            0.2f,
            direction,
            out hit,
            defaultDistance,
            collisionMask
        ))
        {
            currentDistance = Mathf.Lerp(
                currentDistance,
                Mathf.Clamp(hit.distance, minDistance, defaultDistance),
                Time.deltaTime * smoothSpeed
            );
        }
        else
        {
            currentDistance = Mathf.Lerp(
                currentDistance,
                defaultDistance,
                Time.deltaTime * smoothSpeed
            );
        }

        transform.position = pivot.position + direction * currentDistance;
    }
}