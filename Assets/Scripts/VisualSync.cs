using UnityEngine;

public class VisualSync : MonoBehaviour
{
    public Transform playerBody;

    void LateUpdate()
    {
        // Follow position
        transform.position = playerBody.position;

        // Follow rotation (Y only)
        Vector3 rot = playerBody.eulerAngles;
        transform.rotation = Quaternion.Euler(0, rot.y, 0);
    }
}