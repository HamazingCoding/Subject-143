using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLookNew : MonoBehaviour
{
    [Header("References")]
    public Transform playerBody;     // rotates left/right
    public Transform cameraPivot;    // rotates up/down

    [Header("Settings")]
    public float sensitivity = 100f;
    // public float rotationSmoothTime = 0.03f;        // Disabled for now
    // public float sprintRotationSmoothTime = 0.08f;  // Disabled for now
    public float verticalClamp = 60f;

    private PlayerInputActions inputActions;
    private PlayerMovement playerMovement;

    private Vector2 lookInput;
    private float xRotation = 0f;
    private float currentYaw;

    void Awake()
    {
        inputActions = new PlayerInputActions();
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;
    }

    void OnEnable() => inputActions.Enable();
    void OnDisable() => inputActions.Disable();

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        playerMovement = GetComponentInParent<PlayerMovement>();
        currentYaw = playerBody.eulerAngles.y;
    }

    void Update()
    {
        HandleLook();
    }

    void HandleLook()
    {
        // --- Mouse input (no Time.deltaTime) ---
        float mouseX = lookInput.x * sensitivity;
        float mouseY = lookInput.y * sensitivity;

        // --- Vertical rotation (camera pivot) ---
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -verticalClamp, verticalClamp);
        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // --- Horizontal rotation (player) ---
        currentYaw += mouseX;

        // Smooth rotation is commented out for now to remove choppiness
        // float targetYaw = currentYaw + mouseX;
        // float smoothTime = playerMovement != null && playerMovement.IsSprinting()
        //     ? sprintRotationSmoothTime
        //     : rotationSmoothTime;
        // currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, smoothTime);

        playerBody.rotation = Quaternion.Euler(0f, currentYaw, 0f);
    }

    // Optional: Camera kick for impacts or recoil
    public void AddCameraKick(float strength)
    {
        xRotation -= strength;
    }
}