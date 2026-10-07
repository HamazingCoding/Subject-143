using UnityEngine;
using UnityEngine.InputSystem;

namespace Subject143.Nightfarer
{
    /// <summary>One frame of player intent. Gameplay reads only this, never devices directly.</summary>
    public struct NightfarerInputFrame
    {
        public Vector2 move;
        public Vector2 lookMouse;   // pixels this frame
        public Vector2 lookStick;   // -1..1
        public bool dodgeHeld, dodgePressed, dodgeReleased;
        public bool jumpPressed, jumpHeld;
        public bool lightPressed;
        public bool heavyPressed, heavyHeld;
        public bool lockOnPressed;
        public bool skillPressed;
        public bool surgeTogglePressed;
        public bool walkTogglePressed;
        public bool switchWeaponPressed;
        public bool flaskPressed;
        public bool ultimatePressed;
        public bool pausePressed;
    }

    public interface INightfarerInputSource
    {
        /// <summary>Must be idempotent within a frame (several systems read it).</summary>
        NightfarerInputFrame Read();
    }

    /// <summary>Programmatic input for automated tests and demos.</summary>
    public class ScriptedInputSource : INightfarerInputSource
    {
        public Vector2 Move;
        public Vector2 LookMouse;
        public Vector2 LookStick;
        public bool DodgeHeld;
        public bool HeavyHeld;
        /// <summary>Hold Jump (super jump charge). TapJump is a quick press-and-release.</summary>
        public bool JumpHeld;

        bool prevDodge, prevHeavy, prevJump;
        bool lightAttack, jump, lockOn, skill, surge, walkToggle, switchWeapon, flask, ultimate;
        int frame = -1;
        NightfarerInputFrame cached;

        public void TapLight() => lightAttack = true;
        public void TapJump() => jump = true;
        public void TapLockOn() => lockOn = true;
        public void TapSkill() => skill = true;
        public void TapSurge() => surge = true;
        public void TapWalkToggle() => walkToggle = true;
        public void TapSwitchWeapon() => switchWeapon = true;
        public void TapFlask() => flask = true;
        public void TapUltimate() => ultimate = true;

        public NightfarerInputFrame Read()
        {
            if (frame == Time.frameCount) return cached;
            frame = Time.frameCount;
            cached = new NightfarerInputFrame
            {
                move = Move,
                lookMouse = LookMouse,
                lookStick = LookStick,
                dodgeHeld = DodgeHeld,
                dodgePressed = DodgeHeld && !prevDodge,
                dodgeReleased = !DodgeHeld && prevDodge,
                heavyHeld = HeavyHeld,
                heavyPressed = HeavyHeld && !prevHeavy,
                lightPressed = lightAttack,
                jumpPressed = jump || (JumpHeld && !prevJump),
                jumpHeld = JumpHeld,
                lockOnPressed = lockOn,
                skillPressed = skill,
                surgeTogglePressed = surge,
                walkTogglePressed = walkToggle,
                switchWeaponPressed = switchWeapon,
                flaskPressed = flask,
                ultimatePressed = ultimate,
            };
            prevDodge = DodgeHeld;
            prevHeavy = HeavyHeld;
            prevJump = JumpHeld;
            lightAttack = jump = lockOn = skill = surge = walkToggle = switchWeapon = flask = ultimate = false;
            return cached;
        }
    }
}
