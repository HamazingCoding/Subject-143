using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLookNew : MonoBehaviour
{
    public Transform playerBody;     // rotates left/right
    public Transform cameraPivot;    // rotates up/down
    float currentYaw;
    private PlayerMovement playerMovement;
    float yawVelocity;
    public float rotationSmoothTime = 0.03f;
    public float sprintRotationSmoothTime = 0.08f;


    public float sensitivity = 100f;

    private PlayerInputActions inputActions;
    private Vector2 lookInput;

    float xRotation = 0f;

    public void AddCameraKick(float strength)
    {
        xRotation -= strength;
    }

    void Awake()
    {
        inputActions = new PlayerInputActions();

        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        playerMovement = GetComponentInParent<PlayerMovement>();

        currentYaw = playerBody.eulerAngles.y;
    }

    void Update()
    {
        float mouseX = lookInput.x * sensitivity * Time.deltaTime;
        float mouseY = lookInput.y * sensitivity * Time.deltaTime;

        // Vertical (camera pivot)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -60f, 60f);

        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Horizontal (player rotation)
        float targetYaw = currentYaw + mouseX;

        float smoothTime = playerMovement.IsSprinting()
            ? sprintRotationSmoothTime
            : rotationSmoothTime;

        currentYaw = Mathf.SmoothDampAngle(
            currentYaw,
            targetYaw,
            ref yawVelocity,
            smoothTime
        );

        playerBody.rotation = Quaternion.Euler(0f, currentYaw, 0f);
    }
}