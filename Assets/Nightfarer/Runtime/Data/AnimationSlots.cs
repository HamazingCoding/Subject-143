namespace Subject143.Nightfarer
{
    /// <summary>
    /// Logical animation slots. The generated base Animator Controller contains one placeholder clip per
    /// slot (named SLOT_&lt;slot&gt;); an <see cref="AnimationSet"/> fills the slots through an
    /// AnimatorOverrideController. Gameplay code only ever refers to slot names, never to clips, so any
    /// humanoid clip can be dropped into a set without touching gameplay.
    /// </summary>
    public static class AnimationSlots
    {
        public const string SlotPrefix = "SLOT_";
        public const string LocomotionState = "Locomotion";

        // Locomotion blend tree children (2D freeform, MoveX/MoveZ in "speed tier" units:
        // 1 = walk, 2 = run, 3 = sprint, 4 = surge sprint).
        public static readonly string[] Locomotion =
        {
            "Idle",
            "Walk_F", "Walk_B", "Walk_L", "Walk_R", "Walk_FL", "Walk_FR", "Walk_BL", "Walk_BR",
            "Run_F", "Run_B", "Run_L", "Run_R", "Run_FL", "Run_FR", "Run_BL", "Run_BR",
            "Sprint_F", "Sprint_FL", "Sprint_FR",
            "Surge_F"
        };

        // One Animator state per action slot; the state name equals the slot name.
        public static readonly string[] Actions =
        {
            "JumpStart", "Fall", "Land", "LandRoll",
            "Roll", "Backstep", "HitReact",
            "Light1", "Light2", "Light3", "Light4", "Light5",
            "Heavy1", "Heavy2",
            "SprintAttack", "JumpAttack", "RollAttack", "BackstepAttack",
            "Skill", "SkillFollowUp",
            "Mantle", "Drink", "Ultimate",
            "StepF", "StepB", "StepL", "StepR",
            "Vault", "SuperJumpCharge", "HeroLand", "SideJumpL", "SideJumpR"
        };

        /// <summary>Action states that loop and ignore the ActionSpeed multiplier.</summary>
        public static bool IsLooping(string slot) => slot == "Fall";
    }
}
