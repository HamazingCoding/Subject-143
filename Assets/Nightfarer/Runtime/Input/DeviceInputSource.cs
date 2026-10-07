using UnityEngine;
using UnityEngine.InputSystem;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Keyboard/mouse + gamepad bindings, laid out like Elden Ring's PC/pad defaults where the project allows.
    /// Built in code so the project's existing PlayerInputActions asset and generated class stay untouched.
    /// </summary>
    public class DeviceInputSource : MonoBehaviour, INightfarerInputSource
    {
        public bool lockCursor = true;
        /// <summary>Set by menus that need a free cursor.</summary>
        public static bool CursorReleased;

        InputAction move, lookMouse, lookStick, dodge, jump, lightAttack, heavy, lockOn, skill, surge, walk, switchWeapon, flask, ultimate, pause;
        int cachedFrame = -1;
        NightfarerInputFrame cached;

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            lookMouse = new InputAction("LookMouse", InputActionType.Value, "<Mouse>/delta");
            lookStick = new InputAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            dodge = Button("Dodge", "<Keyboard>/space", "<Gamepad>/buttonEast");
            jump = Button("Jump", "<Keyboard>/f", "<Gamepad>/buttonSouth");
            lightAttack = Button("Light", "<Mouse>/leftButton", "<Gamepad>/rightShoulder");
            heavy = Button("Heavy", "<Mouse>/rightButton", "<Gamepad>/rightTrigger");
            lockOn = Button("LockOn", "<Mouse>/middleButton", "<Keyboard>/q", "<Gamepad>/rightStickPress");
            skill = Button("Skill", "<Keyboard>/e", "<Gamepad>/leftTrigger");
            surge = Button("Surge", "<Keyboard>/leftAlt", "<Gamepad>/leftStickPress");
            walk = Button("WalkToggle", "<Keyboard>/leftCtrl", "<Gamepad>/dpad/up");
            switchWeapon = Button("SwitchWeapon", "<Keyboard>/x", "<Gamepad>/dpad/right");
            flask = Button("Flask", "<Keyboard>/r", "<Gamepad>/buttonWest");
            ultimate = Button("Ultimate", "<Keyboard>/g", "<Gamepad>/buttonNorth");
            pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");
        }

        static InputAction Button(string name, params string[] paths)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
            return a;
        }

        InputAction[] All => new[] { move, lookMouse, lookStick, dodge, jump, lightAttack, heavy, lockOn, skill, surge, walk, switchWeapon, flask, ultimate, pause };

        void OnEnable()
        {
            foreach (var a in All) a.Enable();
            if (lockCursor) Cursor.lockState = CursorLockMode.Locked;
        }

        void OnDisable()
        {
            foreach (var a in All) a.Disable();
        }

        void OnDestroy()
        {
            foreach (var a in All) a.Dispose();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            // The pause menu owns the cursor while open; otherwise a click recaptures it.
            var mouse = Mouse.current;
            if (lockCursor && !CursorReleased && mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked;
        }

        public NightfarerInputFrame Read()
        {
            if (cachedFrame == Time.frameCount) return cached;
            cachedFrame = Time.frameCount;
            bool cursorFree = lockCursor && Cursor.lockState != CursorLockMode.Locked;
            cached = new NightfarerInputFrame
            {
                move = move.ReadValue<Vector2>(),
                lookMouse = cursorFree ? Vector2.zero : lookMouse.ReadValue<Vector2>(),
                lookStick = lookStick.ReadValue<Vector2>(),
                dodgeHeld = dodge.IsPressed(),
                dodgePressed = dodge.WasPressedThisFrame(),
                dodgeReleased = dodge.WasReleasedThisFrame(),
                jumpPressed = jump.WasPressedThisFrame(),
                jumpHeld = jump.IsPressed(),
                lightPressed = !cursorFree && lightAttack.WasPressedThisFrame(),
                heavyPressed = heavy.WasPressedThisFrame(),
                heavyHeld = heavy.IsPressed(),
                lockOnPressed = lockOn.WasPressedThisFrame(),
                skillPressed = skill.WasPressedThisFrame(),
                surgeTogglePressed = surge.WasPressedThisFrame(),
                walkTogglePressed = walk.WasPressedThisFrame(),
                switchWeaponPressed = switchWeapon.WasPressedThisFrame(),
                flaskPressed = flask.WasPressedThisFrame(),
                ultimatePressed = ultimate.WasPressedThisFrame(),
                pausePressed = pause.WasPressedThisFrame(),
            };
            return cached;
        }
    }
}
