using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private PlayerInputActions inputActions;
    public Animator animator;
    public Rigidbody rb;

    public CharacterController controller;
    public Transform cameraTransform;
    public float sprintBurstMultiplier = 1.8f;
    private bool wasSprintingLastFrame;
    public float jumpHeight = 1.5f;
    private bool jumpPressed;
    private bool isHoldingJump;

    public float maxChargeTime = 1.2f;
    public float launchForce = 20f;
    public float upwardLaunchForce = 12f;
    public float maxSpeed = 45f;

    private float currentCharge;
    private bool isCharging;

    Vector3 airVelocity;
    
    float currentSpeed;
    float speedVelocity;
    Vector3 moveDirection;

    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float sprintSpeed = 10f;

    public float accelerationTime = 0.2f;
    public float sprintAccelerationTime = 0.05f;

    public float gravity = -9.81f;

    private Vector2 moveInput;
    private bool isSprinting;
    private bool isWalking;

    private Vector3 velocity;

    // --- STAGE 1: single authoritative motion data, derived from the CharacterController.
    // Read these from other systems instead of the Rigidbody. Updated at the end of
    // Update(), immediately after controller.Move().
    public Vector3 CurrentVelocity { get; private set; }
    public Vector3 HorizontalVelocity { get; private set; }
    public float HorizontalSpeed { get; private set; }
    public float VerticalSpeed { get; private set; }
    public Vector3 Acceleration { get; private set; }
    public bool Grounded { get; private set; }

    private Vector3 _lastAuthVelocity;

    [Header("Debug (temporary)")]
    public bool showMotionDebug = false;

    void Awake()
    {
        inputActions = new PlayerInputActions();

        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        inputActions.Player.Sprint.performed += _ => isSprinting = true;
        inputActions.Player.Sprint.canceled += _ => isSprinting = false;

        inputActions.Player.Jump.performed += _ =>
            {
                jumpPressed = true;
                isHoldingJump = true;
            };

            inputActions.Player.Jump.canceled += _ =>
            {
                isHoldingJump = false;
            };

        inputActions.Player.WalkToggle.performed += _ => isWalking = !isWalking;

        // STAGE 1: the CharacterController is the sole movement authority. Keep the
        // Rigidbody (other systems still reference it) but make it kinematic so it no
        // longer fights the CharacterController or produces unreliable velocity.
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }


    void FixedUpdate()
    {
        
    }

    public bool IsMoving()
    {
        return HorizontalSpeed > 0.1f;
    }

    public bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, 1.1f);
    }

    public void DoJump()
    {
        if (IsGrounded())
        {
            rb.AddForce(Vector3.up * launchForce, ForceMode.Impulse);
        }
    }

    public Vector3 GetVelocity()
    {
        return CurrentVelocity;
    }

    void Update()
    {
        if (isSprinting)
        {
            isWalking = false;
        }

        // 🔥 REQUIRE FORWARD INPUT TO SPRINT
        if (isSprinting && moveInput.y < 0f)
        {
            isSprinting = false;
        }

        // STAGE 2: authoritative horizontal speed (from the CharacterController,
        // published at the end of the previous Update). Behaviour is unchanged because
        // the input-based animation tiers below still floor this value.
        float speed = HorizontalSpeed;

        // Clamp tiny movement
        if (speed < 1f)
            speed = 0f;

        // Normalize speed
        float inputMagnitude = moveInput.magnitude;
        float normalizedSpeed = speed / maxSpeed;

        // --- INPUT-BASED ANIMATION TIERS ---

        if (moveInput.magnitude > 0.1f)
        {
            if (isSprinting)
            {
                normalizedSpeed = Mathf.Max(normalizedSpeed, 0.75f); // sprint zone
            }
            else if (!isWalking)
            {
                normalizedSpeed = Mathf.Max(normalizedSpeed, 0.45f); // run zone
            }
            else
            {
                normalizedSpeed = Mathf.Max(normalizedSpeed, 0.2f); // walk zone
            }
        }

        if (moveInput.magnitude > 0.1f && normalizedSpeed < 0.1f)
        {
            normalizedSpeed = 0.2f; // instantly trigger movement animation
        }

        // Input-based drop (fix slow stopping)
        if (moveInput.magnitude < 0.1f)
        {
            normalizedSpeed = 0f;
        }

        // Use INPUT instead of velocity for direction
        Vector2 processedInput = moveInput;

        // 🔥 BLOCK BACKWARD + LIMIT STRAFE DURING SPRINT
        if (isSprinting)
        {
            processedInput.y = Mathf.Max(0f, processedInput.y); // no backward
            processedInput.x *= 0.5f; // optional: reduce side movement
        }

        Vector3 inputDir = new Vector3(processedInput.x, 0, processedInput.y);

        // Convert to local space (relative to character)
        Vector3 localInput = transform.InverseTransformDirection(
            cameraTransform.forward * inputDir.z + cameraTransform.right * inputDir.x
        );

        float moveX = localInput.x;
        float moveZ = localInput.z;

        // --- SMOOTHING (comes BEFORE kill zone) ---
        Vector2 currentDir = new Vector2(
            animator.GetFloat("MoveX"),
            animator.GetFloat("MoveZ")
        );

        Vector2 targetDir = new Vector2(moveX, moveZ);

        Vector2 smoothedDir = Vector2.Lerp(currentDir, targetDir, 20f * Time.deltaTime);

        moveX = smoothedDir.x;
        moveZ = smoothedDir.y;

        // --- KILL ZONE (comes AFTER smoothing) ---
        float directionMagnitude = new Vector2(moveX, moveZ).magnitude;

        if (normalizedSpeed < 0.1f || directionMagnitude < 0.1f)
        {
            moveX = 0f;
            moveZ = 0f;
        }

        // --- SEND TO ANIMATOR (ONLY ONCE) ---
        animator.SetFloat("MoveX", moveX, 0.1f, Time.deltaTime);
        animator.SetFloat("MoveZ", moveZ, 0.1f, Time.deltaTime);

        // Speed damping
        float dampTime = (normalizedSpeed > animator.GetFloat("Speed")) ? 0.1f : 0.02f;
        animator.SetFloat("Speed", normalizedSpeed, dampTime, Time.deltaTime);

        if (normalizedSpeed < 0.1f)
        {
            animator.SetFloat("Speed", 0f);
        }

        // Animation playback speed
        animator.speed = Mathf.Lerp(1f, 1.3f, normalizedSpeed);
        // Direction
        Vector3 targetDirection = cameraTransform.forward * processedInput.y + cameraTransform.right * processedInput.x;
        targetDirection.y = 0f;

        targetDirection.Normalize();

        // Add inertia
        moveDirection = Vector3.Lerp(moveDirection, targetDirection, isSprinting ? 0.08f : 0.15f);

        if (isSprinting && moveInput.magnitude > 0.1f)
        {
            Vector3 drift = new Vector3(
                Random.Range(-0.1f, 0.1f),
                0,
                Random.Range(-0.1f, 0.1f)
            );

            moveDirection += drift;
            moveDirection.Normalize();
        }

        // Speed logic
        float targetSpeed;

        if (isWalking)
            targetSpeed = walkSpeed;
        else if (isSprinting)
            targetSpeed = sprintSpeed;
        else
            targetSpeed = runSpeed;

        float accelTime = isSprinting ? sprintAccelerationTime : accelerationTime;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, accelTime);

        //Jumping
        if (controller.isGrounded)
        {
            if (velocity.y < 0)
                velocity.y = -2f;

            // Start charging
            if (isHoldingJump)
            {
                isCharging = true;
                currentCharge += Time.deltaTime;
                currentCharge = Mathf.Clamp(currentCharge, 0f, maxChargeTime);

                // Add tension: slightly slow movement
                currentSpeed *= 0.95f;

                if (!isSprinting)
                {
                    // Freeze movement while charging
                    currentSpeed = 0f;
                }
            }

            // Release → launch or normal jump
            if (!isHoldingJump && isCharging)
            {
                float chargePercent = currentCharge / maxChargeTime;

                if (chargePercent > 0.2f)
                {
                    PerformLaunch(chargePercent);
                }
                else
                {
                    // normal jump
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    animator.SetTrigger("jumpTrigger");
                }

                isCharging = false;
                currentCharge = 0f;
            }
        }

        // Gravity
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;

        // Combine movement
        bool isGrounded = controller.isGrounded;

        Vector3 horizontalMove;

        if (isGrounded)
        {
            // Normal movement on ground
            horizontalMove = moveDirection * currentSpeed;

            Vector3 airControl = moveDirection * (currentSpeed * 0.2f);

            // Store momentum when leaving ground
            airVelocity = horizontalMove;
            airVelocity = Vector3.Lerp(airVelocity, airVelocity + airControl, Time.deltaTime);
        }
        else
        {
            // In air → preserve momentum
            horizontalMove = airVelocity;
        }

        // Combine with gravity
        Vector3 finalMove = horizontalMove;
        finalMove.y = velocity.y;

        animator.SetBool("isGrounded", isGrounded);
        animator.SetFloat("verticalVelocity", velocity.y);

        controller.Move(finalMove * Time.deltaTime);

        // STAGE 1: publish authoritative motion data using what the controller
        // actually did this frame.
        UpdateMotionData();
    }

    // Derives trustworthy velocity/acceleration/grounded from the CharacterController.
    void UpdateMotionData()
    {
        float dt = Time.deltaTime;

        Vector3 v = controller != null ? controller.velocity : Vector3.zero;
        if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) v = Vector3.zero;

        CurrentVelocity = v;
        HorizontalVelocity = new Vector3(v.x, 0f, v.z);
        HorizontalSpeed = HorizontalVelocity.magnitude;
        VerticalSpeed = v.y;

        Vector3 accel = dt > 0f ? (v - _lastAuthVelocity) / dt : Vector3.zero;
        if (float.IsNaN(accel.x) || float.IsNaN(accel.y) || float.IsNaN(accel.z)) accel = Vector3.zero;
        // Light smoothing so single-frame spikes (e.g. landing) don't dominate reads.
        Acceleration = Vector3.Lerp(Acceleration, accel, 1f - Mathf.Exp(-15f * dt));

        Grounded = controller != null && controller.isGrounded;
        _lastAuthVelocity = v;
    }

    public bool IsSprinting()
    {
        return isSprinting;
    }

    public Vector2 GetMoveInput()
    {
        return moveInput;
    }

    void PerformLaunch(float chargePercent)
    {
        // 1. Get directional input
        Vector3 inputDirection = cameraTransform.forward * moveInput.y 
                               + cameraTransform.right * moveInput.x;

        // 2. Flatten it (no vertical influence)
        inputDirection.y = 0f;

        // 3. 🔥 HANDLE "NO INPUT" CASE (PUT IT HERE)
        if (inputDirection.magnitude < 0.1f)
        {
            inputDirection = cameraTransform.forward;
            inputDirection.y = 0f;
        }

        // 4. Normalize AFTER fixing direction
        inputDirection.Normalize();

        // 5. Apply forces (your existing logic)
        float forwardForce;
        float upwardForce;

        if (isSprinting)
        {
            forwardForce = launchForce * (1f + chargePercent * 1.5f);
            upwardForce = upwardLaunchForce * (0.6f + chargePercent * 0.6f);
        }
        else
        {
            forwardForce = launchForce * (0.6f + chargePercent);
            upwardForce = upwardLaunchForce * (0.8f + chargePercent * 0.5f);
        }

        airVelocity = inputDirection * forwardForce;
        velocity.y = upwardForce;

        animator.SetTrigger("jumpTrigger");

        velocity.y += 1.5f;
    }

    // STAGE 4: temporary on-screen validation of the authoritative motion data.
    // Toggle "Show Motion Debug" on the PlayerMovement component. Remove later.
    void OnGUI()
    {
        if (!showMotionDebug) return;

        var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14 };
        GUILayout.BeginArea(new Rect(10, 10, 380, 230), style);
        GUILayout.Label($"Grounded:    {Grounded}");
        GUILayout.Label($"Velocity:    ({CurrentVelocity.x:F2}, {CurrentVelocity.y:F2}, {CurrentVelocity.z:F2})");
        GUILayout.Label($"H-Speed:     {HorizontalSpeed:F2} m/s");
        GUILayout.Label($"V-Speed:     {VerticalSpeed:F2} m/s");
        GUILayout.Label($"Accel:       {Acceleration.magnitude:F2} m/s^2");
        GUILayout.Label($"State:       walk={isWalking}  sprint={isSprinting}");
        GUILayout.Label($"Charging:    {isCharging}   charge={currentCharge:F2}s");
        GUILayout.Label($"rb.kinematic: {(rb != null ? rb.isKinematic.ToString() : "no rb")}");
        GUILayout.EndArea();
    }
}