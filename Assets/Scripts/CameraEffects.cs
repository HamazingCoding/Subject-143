using UnityEngine;

public class CameraEffects : MonoBehaviour
{
    public Camera cam;
    public PlayerMovement playerMovement;

    public float normalFOV = 90f;
    public float sprintFOV = 105f;
    public float fovSmooth = 5f;

    void Update()
    {
        float targetFOV = playerMovement.IsSprinting() ? sprintFOV : normalFOV;

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovSmooth);
    }
}