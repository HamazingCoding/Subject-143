using UnityEngine;
using UnityEngine.InputSystem;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Compact readout plus test hotkeys:
    /// F1 HUD, F2 animation set, F3 character profile, F4 slow motion, F5 hitbox debug lines, Backspace reset.
    /// </summary>
    public class NightfarerDebugHUD : MonoBehaviour
    {
        public NightfarerCharacter character;
        public TrainingDummy[] dummies;
        public DummyAttacker attacker;
        public bool visible = false;
        public bool showControls = true;

        GUIStyle box, label, small;
        bool slowMo;

        void Start()
        {
            if (dummies == null || dummies.Length == 0) dummies = FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None);
            if (attacker == null) attacker = FindFirstObjectByType<DummyAttacker>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || character == null) return;
            if (kb.f1Key.wasPressedThisFrame) visible = !visible;
            if (kb.f2Key.wasPressedThisFrame) character.CycleAnimationSet();
            if (kb.f3Key.wasPressedThisFrame) character.CycleProfile();
            if (kb.f4Key.wasPressedThisFrame)
            {
                slowMo = !slowMo;
                Time.timeScale = slowMo ? 0.25f : 1f;
            }
            if (kb.f5Key.wasPressedThisFrame && character.hitbox != null) character.hitbox.drawDebug = !character.hitbox.drawDebug;
            if (kb.backspaceKey.wasPressedThisFrame) character.ResetToSpawn();
        }

        void OnDisable() => Time.timeScale = 1f;

        void OnGUI()
        {
            if (!visible || character == null || character.Profile == null) return;
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                small = new GUIStyle(label) { fontSize = 12 };
            }

            var c = character;
            var m = c.Motor;
            var v = c.Vitals;
            GUILayout.BeginArea(new Rect(10, 10, 360, 430), box);
            GUILayout.Label($"<b>{c.Profile.displayName}</b>  <color=#9cf>[{(c.Animator.ActiveSet != null ? c.Animator.ActiveSet.displayName : "none")}]</color>", label);
            GUILayout.Label($"Weapon: {(c.Weapon != null ? c.Weapon.displayName : "-")}", label);
            GUILayout.Label($"State: <b>{c.StateName}</b>   Move: {c.MovementMode}", label);
            GUILayout.Label($"Combat: {c.CombatPhase}   Combo: {c.ComboStep}", label);
            GUILayout.Label($"Anim: {c.Animator.CurrentState}", label);
            GUILayout.Label($"Speed: {m.PlanarSpeed:F2} m/s   Vy: {m.VerticalVelocity:F1}", label);
            GUILayout.Label($"Grounded: {m.Grounded}   Fall: {m.FallHeight:F1} m", label);
            GUILayout.Label($"I-frames: {(c.IsInvulnerable ? "<color=#6f6>YES</color>" : "no")}   Lock: {(c.IsLocked ? c.LockOn.Current.name : "-")}", label);
            Bar("Stamina", v.Stamina, v.MaxStamina, new Color(0.3f, 0.8f, 0.3f));
            Bar("Health", v.Health, v.MaxHealth, new Color(0.8f, 0.25f, 0.25f));
            var skill = c.Profile.skill;
            GUILayout.Label(skill != null
                ? $"Skill ({skill.displayName}): {(c.SkillCooldownRemaining > 0f ? $"{c.SkillCooldownRemaining:F1}s" : "<color=#6f6>ready</color>")}{(c.FollowUpReady ? "  <color=#fc6>FOLLOW-UP!</color>" : "")}"
                : "Skill: none", label);
            GUILayout.Label($"Hits dealt: {c.HitsDealt} (last {c.LastHitDealt:F0})   Dodged: {c.DodgedHits}   Taken: {c.TakenHits}", small);
            if (dummies != null)
                foreach (var d in dummies)
                    if (d != null) GUILayout.Label($"{d.name}: HP {d.Health:F0}/{d.maxHealth:F0}  hits {d.HitCount}{(d.IsStaggered ? "  STAGGER" : "")}", small);
            if (slowMo) GUILayout.Label("<color=#fc6>SLOW MOTION 0.25x</color>", label);
            GUILayout.EndArea();

            if (showControls)
            {
                GUILayout.BeginArea(new Rect(Screen.width - 330, 10, 320, 300), box);
                GUILayout.Label(
                    "<b>Controls</b> (KB/M | Pad)\n" +
                    "Move WASD | LS   Camera Mouse | RS\n" +
                    "Dodge: tap Space | B   Sprint: hold\n" +
                    "Surge sprint: L-Alt | L3 (while moving)\n" +
                    "Jump F | A    Walk toggle L-Ctrl\n" +
                    "Light LMB | RB   Heavy RMB | RT (hold = charge)\n" +
                    "Lock-on Q / MMB | R3 (flick to switch)\n" +
                    "Skill (Claw Shot) E | LT, then Light\n" +
                    "Switch weapon X\n" +
                    "F1 HUD  F2 anim set  F3 profile\n" +
                    "F4 slow-mo  F5 hitbox lines  Backspace reset\n" +
                    "Esc frees the cursor, click to recapture", small);
                GUILayout.EndArea();
            }
        }

        void Bar(string name, float value, float max, Color color)
        {
            GUILayout.Label($"{name}: {value:F0}/{max:F0}", small);
            Rect r = GUILayoutUtility.GetRect(320, 8);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value / Mathf.Max(1f, max)), r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
