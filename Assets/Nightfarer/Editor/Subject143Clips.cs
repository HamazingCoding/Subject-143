using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer.EditorTools
{
    /// <summary>
    /// Procedural body clips for Subject 143 (muscle-space key poses on the project's idle pose). They give the
    /// body the creature's posture and weight: hunched, low, twisting into each claw strike, crouching into
    /// flash steps. The arms during attacks are driven by WeaponIK (claw paths), so these clips focus on the
    /// spine, legs and root. Replace any of them by assigning a clip to the slot in an AnimationSet.
    /// </summary>
    public static class Subject143Clips
    {
        // Hunched, knees bent, head up to look forward.
        const string Stance = "SF=0.38 CF=0.2 HN=0.25 NN=0.1 LUF=0.75 LLS=0.45 RUF=0.6 RLS=0.5 RootDrop=-0.06 " +
                              "LAD=-0.35 LAF=0.55 LFS=-0.2 RAD=-0.3 RAF=0.45 RFS=-0.35";

        public static Dictionary<string, AnimationClip> Generate(string dir, Dictionary<string, float> basePose)
        {
            var clips = new Dictionary<string, AnimationClip>();
            AnimationClip Make(string slot, float len, bool loop, params (float, string)[] keys)
            {
                var c = PlaceholderClips.Build($"{dir}/S143_{slot}.anim", basePose, Stance, len, loop, keys);
                clips[slot] = c;
                return c;
            }

            Make("Idle", 2.2f, true, (0f, ""), (0.7f, "SF=0.42 CF=0.24 RootDrop=-0.07 HT=0.15"), (1.5f, "SF=0.36 RootDrop=-0.055 HT=-0.12"), (2.2f, ""));

            // Flash steps: a compressed crouch that leans hard into the direction (mostly hidden mid-travel).
            Make("StepF", 0.3f, false, (0f, "SF=0.7 CF=0.3 RootDrop=-0.16 LUF=1.0 LLS=0.2"), (0.12f, "SF=0.8 RootDrop=-0.2 RootPitch=12 LUF=1.0 LLS=0.15 RUF=0.2 RLS=0.6"), (0.3f, ""));
            Make("StepB", 0.3f, false, (0f, "SF=0.5 RootDrop=-0.14"), (0.12f, "SF=0.25 CF=-0.1 RootPitch=-10 RootDrop=-0.12 LUF=0.9 LLS=0.3 RUF=0.9 RLS=0.3"), (0.3f, ""));
            Make("StepL", 0.3f, false, (0f, "SF=0.5 RootDrop=-0.14"), (0.12f, "SL=0.6 CL=0.4 RootRoll=14 RootDrop=-0.15 LUI=0.4 RUI=-0.2 LUF=0.8 RUF=0.8 LLS=0.3 RLS=0.3"), (0.3f, ""));
            Make("StepR", 0.3f, false, (0f, "SF=0.5 RootDrop=-0.14"), (0.12f, "SL=-0.6 CL=-0.4 RootRoll=-14 RootDrop=-0.15 RUI=0.4 LUI=-0.2 LUF=0.8 RUF=0.8 LLS=0.3 RLS=0.3"), (0.3f, ""));

            // Beast-style claw attacks: low, twisting rips, a three-swipe frenzy, pounces and mauls.
            Make("Light1", 0.42f, false, (0f, ""),
                (0.14f, "ST=-0.5 CT=-0.4 SF=0.5 RootDrop=-0.12 LUF=0.9 LLS=0.3"),
                (0.26f, "ST=0.5 CT=0.5 SF=0.45 HN=0.3 RootDrop=-0.08 RUF=0.8 RLS=0.35"),
                (0.42f, ""));
            Make("Light2", 0.4f, false, (0f, "ST=0.3"),
                (0.14f, "ST=0.5 CT=0.4 SF=0.5 RootDrop=-0.12 RUF=0.9 RLS=0.3"),
                (0.25f, "ST=-0.5 CT=-0.5 SF=0.45 RootDrop=-0.08 LUF=0.8 LLS=0.35"),
                (0.4f, ""));
            Make("Light3", 0.52f, false, (0f, ""),
                (0.2f, "SF=-0.1 CF=-0.3 HN=0.4 RootDrop=0.04 LUF=0.6 LLS=0.6"),
                (0.3f, "SF=0.9 CF=0.4 RootDrop=-0.22 LUF=1.0 LLS=0.1 RUF=0.5 RLS=0.3"),
                (0.52f, ""));
            Make("Light4", 0.7f, false, (0f, ""),
                (0.12f, "ST=0.5 CT=0.4 SF=0.5 RootDrop=-0.14 LUF=0.9 LLS=0.3"),
                (0.27f, "ST=-0.5 CT=-0.45 SF=0.5 RootDrop=-0.14 RUF=0.9 RLS=0.3"),
                (0.42f, "ST=0.55 CT=0.45 SF=0.55 RootDrop=-0.16 LUF=0.9 LLS=0.3"),
                (0.7f, ""));
            Make("Light5", 0.85f, false, (0f, ""),
                (0.15f, "SF=0.8 RootDrop=-0.25 LUF=1.0 LLS=0.0 RUF=1.0 RLS=0.0"),
                (0.3f, "SF=-0.1 CF=-0.3 HN=0.4 RootDrop=0.12 RootPitch=-8 LUF=0.9 LLS=0.2 RUF=0.6 RLS=0.6"),
                (0.46f, "SF=1.0 CF=0.5 RootDrop=-0.3 RootPitch=10 LUF=1.0 LLS=0.05 RUF=0.4 RLS=0.3"),
                (0.65f, "SF=0.9 RootDrop=-0.28"),
                (0.85f, ""));
            Make("Heavy1", 0.95f, false, (0f, ""),
                (0.15f, "SF=0.1 CF=-0.25 ST=-0.5 RootDrop=-0.12 LUF=0.9 LLS=0.2 RUF=0.9 RLS=0.2"),
                (0.42f, "SF=-0.1 CF=-0.3 ST=-0.3 RootDrop=0.05 LUF=0.5 LLS=0.8 RUF=0.2 RLS=0.9"),
                (0.52f, "SF=0.95 CF=0.4 ST=0.3 RootDrop=-0.25 LUF=1.0 LLS=0.1 RUF=0.1 RLS=0.5"),
                (0.75f, "SF=0.85 RootDrop=-0.22"),
                (0.95f, ""));
            Make("Heavy2", 0.9f, false, (0f, ""),
                (0.15f, "ST=-0.7 CT=-0.6 RootDrop=-0.12 RootYaw=40"),
                (0.35f, "ST=0.2 RootYaw=-60 RootDrop=-0.15 SL=0.2"),
                (0.48f, "ST=0.6 RootYaw=-200 RootDrop=-0.15"),
                (0.62f, "ST=0.3 RootYaw=-330 RootDrop=-0.12"),
                (0.9f, "RootYaw=-360"));
            Make("SprintAttack", 0.75f, false, (0f, "SF=0.7 RootDrop=-0.15 LUF=1.0 LLS=0.3"),
                (0.2f, "SF=0.85 RootDrop=-0.25 ST=-0.4 LUF=1.0 LLS=0.0 RUF=0.3 RLS=0.7"),
                (0.32f, "SF=0.5 ST=0.2 RootDrop=-0.1 RootPitch=8 LUF=0.2 LLS=0.9 RUF=1.0 RLS=0.3"),
                (0.55f, "SF=0.6 RootDrop=-0.15"),
                (0.75f, ""));
            Make("JumpAttack", 0.7f, false, (0f, "SF=0.2 LUF=1.0 LLS=0.2 RUF=0.9 RLS=0.2"),
                (0.2f, "SF=-0.2 CF=-0.3 HN=0.3 LUF=1.0 LLS=0.1 RUF=1.0 RLS=0.1"),
                (0.38f, "SF=0.95 CF=0.4 RootDrop=-0.25 LUF=1.0 LLS=0.1 RUF=0.6 RLS=0.2"),
                (0.7f, ""));
            Make("RollAttack", 0.6f, false, (0f, "SF=0.6 RootDrop=-0.15 ST=0.3"),
                (0.12f, "SF=0.4 CF=-0.1 HN=0.3 RootDrop=-0.05"),
                (0.24f, "SF=0.85 CF=0.3 RootDrop=-0.22 LUF=1.0 LLS=0.1 RUF=0.8 RLS=0.3"),
                (0.6f, ""));
            Make("BackstepAttack", 0.6f, false, (0f, "SF=0.5 RootDrop=-0.12"),
                (0.12f, "SF=0.3 CF=-0.15 HN=0.35 RootDrop=-0.03"),
                (0.24f, "SF=0.85 CF=0.3 RootDrop=-0.22 LUF=1.0 LLS=0.1 RUF=0.8 RLS=0.3"),
                (0.6f, ""));
            // Claw Shot throw: plant the feet, twist the claw shoulder forward, lean into the throw, then brace for the yank.
            Make("Skill", 0.7f, false, (0f, ""),
                (0.06f, "ST=-0.6 CT=-0.5 SF=0.15 LUF=0.95 LLS=0.45 RUF=0.35 RLS=0.7 RootDrop=-0.06"),
                (0.2f, "ST=-0.55 CT=-0.45 SF=0.3 HN=0.2 LUF=1.0 LLS=0.35 RUF=0.3 RLS=0.75 RootDrop=-0.1"),
                (0.5f, "ST=-0.35 SF=0.55 RootDrop=-0.14 LUF=1.0 LLS=0.3"),
                (0.7f, ""));
            Make("SkillFollowUp", 0.75f, false, (0f, "SF=0.5 RootDrop=-0.1"),
                (0.2f, "SF=-0.2 CF=-0.3 RootDrop=0.08 LUF=0.8 LLS=0.6"),
                (0.35f, "SF=0.95 CF=0.4 RootDrop=-0.25 LUF=1.0 LLS=0.1"),
                (0.75f, ""));
            Make("Ultimate", 1.15f, false, (0f, ""),
                (0.3f, "SF=0.1 CF=-0.35 HN=0.4 RootDrop=0.05 LUF=0.6 LLS=0.7 RUF=0.5 RLS=0.7"),
                (0.48f, "SF=1.0 CF=0.5 RootDrop=-0.3 LUF=1.0 LLS=0.05 RUF=0.3 RLS=0.3"),
                (0.8f, "SF=0.9 RootDrop=-0.28"),
                (1.15f, ""));

            // Reactions and traversal.
            Make("HitReact", 0.38f, false, (0f, ""), (0.07f, "SF=-0.35 CF=-0.4 HN=0.6 RootPitch=-14 RootDrop=-0.04"), (0.22f, "SF=0.2 RootPitch=-4"), (0.38f, ""));
            Make("Land", 0.32f, false, (0f, "SF=0.6 RootDrop=-0.2 LUF=1.0 LLS=0.1 RUF=0.9 RLS=0.15"), (0.1f, "SF=0.7 RootDrop=-0.24"), (0.32f, ""));
            // Hard landing: three-point crouch instead of a roll.
            Make("LandRoll", 0.45f, false, (0f, "SF=0.9 CF=0.4 RootDrop=-0.32 LUF=1.0 LLS=-0.1 RUF=1.0 RLS=0.0 LAD=-0.7 LAF=0.9"),
                (0.18f, "SF=0.95 CF=0.45 RootDrop=-0.34 LUF=1.0 LLS=-0.15 RUF=1.0 RLS=-0.05 LAD=-0.75 LAF=0.95"),
                (0.45f, ""));
            foreach (var kv in Traversal(dir, basePose, Stance, "S143")) clips[kv.Key] = kv.Value;
            Make("JumpStart", 0.4f, false, (0f, "SF=0.6 RootDrop=-0.15 LUF=1.0 LLS=0.2 RUF=1.0 RLS=0.2"), (0.15f, "SF=0.2 LUF=0.9 LLS=0.4 RUF=0.6 RLS=0.6"), (0.4f, "SF=0.3 LUF=1.0 LLS=0.2 RUF=0.8 RLS=0.3"));
            Make("Fall", 0.8f, true, (0f, "SF=0.25 LUF=0.9 LLS=0.3 RUF=0.7 RLS=0.4 LAD=0.1 RAD=0.0"), (0.4f, "SF=0.3 LUF=0.85 LLS=0.35 RUF=0.75 RLS=0.35 LAD=0.15 RAD=0.05"), (0.8f, "SF=0.25 LUF=0.9 LLS=0.3 RUF=0.7 RLS=0.4 LAD=0.1 RAD=0.0"));
            return clips;
        }

        /// <summary>
        /// Climbing, vaulting, drinking, the super jump crouch, the hero landing and side jumps, on a given stance
        /// ("" = the project's upright idle pose, for the default upright set; Stance = the hunched feral set).
        /// Climb/vault clips are normalised to their state's duration; hands and feet are placed by IK on top.
        /// </summary>
        public static Dictionary<string, AnimationClip> Traversal(string dir, Dictionary<string, float> basePose, string stance, string prefix)
        {
            var clips = new Dictionary<string, AnimationClip>();
            void Make(string slot, float len, bool loop, params (float, string)[] keys) =>
                clips[slot] = PlaceholderClips.Build($"{dir}/{prefix}_{slot}.anim", basePose, stance, len, loop, keys);

            // Climb: reach up, knees walk up the wall (feet are planted by FootIK), lean over the lip, knee up, stand.
            Make("Mantle", 1f, false,
                (0f, "RAD=0.8 LAD=0.8 RAF=0.6 LAF=0.6 SF=0.1 HN=0.35 LUF=0.5 LLS=0.3 RUF=0.2 RLS=0.6"),
                (0.16f, "RAD=0.9 LAD=0.9 SF=0.35 CF=0.2 HN=0.45 LUF=0.9 LLS=-0.4 RUF=0.3 RLS=0.2 RootDrop=-0.05"),
                (0.3f, "RAD=0.85 LAD=0.85 SF=0.35 CF=0.2 HN=0.4 RUF=0.9 RLS=-0.4 LUF=0.4 LLS=0.3 RootDrop=-0.05"),
                (0.44f, "RAD=0.8 LAD=0.8 SF=0.4 CF=0.2 HN=0.35 LUF=0.9 LLS=-0.4 RUF=0.4 RLS=0.3 RootDrop=-0.05"),
                (0.58f, "RAD=0.6 LAD=0.6 SF=0.45 CF=0.25 HN=0.3 RUF=0.9 RLS=-0.4 LUF=0.5 LLS=0.2 RootDrop=-0.05"),
                (0.7f, "RAD=0.1 LAD=0.1 SF=0.9 CF=0.5 HN=0.3 LUF=0.8 LLS=-0.2 RUF=0.3 RLS=0.5 RootDrop=-0.1"),
                (0.84f, "RAD=-0.2 LAD=-0.2 SF=0.6 CF=0.3 LUF=1.0 LLS=-0.8 RUF=0.6 RLS=-0.2 RootDrop=-0.25"),
                (1f, ""));
            // Low wall: one-handed claw vault, legs tucked and swung over.
            Make("Vault", 1f, false,
                (0f, "SF=0.4 RootDrop=-0.08 LUF=0.6 LLS=0.3 RUF=0.6 RLS=0.3"),
                (0.25f, "SF=0.6 CF=0.3 HN=0.3 LUF=1.0 LLS=-0.6 RUF=1.0 RLS=-0.6 RootRoll=-15 RootDrop=-0.15 RAD=0.2 RAF=-0.4"),
                (0.55f, "SF=0.4 CF=0.2 LUF=0.9 LLS=-0.3 RUF=0.6 RLS=0.0 RootRoll=-10 RAD=0.3 RAF=-0.5"),
                (0.8f, "SF=0.5 RootDrop=-0.2 LUF=0.9 LLS=0.0 RUF=0.7 RLS=0.2"),
                (1f, ""));
            // Crimson Tears: raise the flask, tip the head back to drink, lower it.
            Make("Drink", 0.95f, false, (0f, ""),
                (0.25f, "RAD=0.15 RAF=0.9 RFS=-0.9 RAT=0.3 HN=-0.1"),
                (0.5f, "RAD=0.35 RAF=0.95 RFS=-1.0 RAT=0.3 HN=0.45 NN=0.25 SF=-0.1 CF=-0.1"),
                (0.75f, "RAD=0.0 RAF=0.4 RFS=-0.3 HN=0.05"),
                (0.95f, ""));
            // Super jump: sink into a coiled crouch, arms swept back (held at the end while charging).
            Make("SuperJumpCharge", 0.75f, false, (0f, ""),
                (0.15f, "SF=0.5 RootDrop=-0.18 LUF=0.9 LLS=0.0 RUF=0.9 RLS=0.0 LAD=-0.5 RAD=-0.5 LAF=-0.3 RAF=-0.3"),
                (0.75f, "SF=0.7 CF=0.3 HN=0.5 RootDrop=-0.34 LUF=1.0 LLS=-0.4 RUF=1.0 RLS=-0.4 LAD=-0.6 RAD=-0.6 LAF=-0.5 RAF=-0.5"));
            // Three-point superhero landing: right knee down, left foot planted, head up; the claw hand is IK'd to the ground.
            Make("HeroLand", 1f, false,
                (0f, "RootDrop=-0.41 SF=0.75 CF=0.35 HN=0.55 NN=0.2 RUF=0.15 RLS=-0.95 LUF=0.95 LLS=-0.35 RAD=0.1 RAF=-0.7"),
                (0.35f, "RootDrop=-0.4 SF=0.65 CF=0.3 HN=0.5 NN=0.15 RUF=0.15 RLS=-0.95 LUF=0.95 LLS=-0.35 RAD=0.05 RAF=-0.6"),
                (0.7f, "RootDrop=-0.25 SF=0.45 HN=0.3 RUF=0.6 RLS=0.0 LUF=0.8 LLS=0.0"),
                (1f, ""));
            // Side hops: lean and tuck into the direction, land in a crouch.
            Make("SideJumpL", 0.55f, false, (0f, "SF=0.3 RootDrop=-0.12 LUF=0.7 RUF=0.7"),
                (0.15f, "RootRoll=28 SL=0.4 CL=0.3 SF=0.3 LUF=1.0 LLS=-0.5 RUF=1.0 RLS=-0.5 RAD=0.3 LAD=0.1"),
                (0.35f, "RootRoll=18 SL=0.3 SF=0.3 LUF=0.9 LLS=-0.3 RUF=0.9 RLS=-0.3"),
                (0.5f, "RootRoll=5 RootDrop=-0.15 SF=0.4 LUF=0.8 RUF=0.8"), (0.55f, ""));
            Make("SideJumpR", 0.55f, false, (0f, "SF=0.3 RootDrop=-0.12 LUF=0.7 RUF=0.7"),
                (0.15f, "RootRoll=-28 SL=-0.4 CL=-0.3 SF=0.3 LUF=1.0 LLS=-0.5 RUF=1.0 RLS=-0.5 LAD=0.3 RAD=0.1"),
                (0.35f, "RootRoll=-18 SL=-0.3 SF=0.3 LUF=0.9 LLS=-0.3 RUF=0.9 RLS=-0.3"),
                (0.5f, "RootRoll=-5 RootDrop=-0.15 SF=0.4 LUF=0.8 RUF=0.8"), (0.55f, ""));
            return clips;
        }

        /// <summary>Feral locomotion from the project's Kevin Iglesias clips: hunched, knees bent, low sprint.</summary>
        public static Dictionary<string, AnimationClip> DeriveFeralLocomotion(string dir, NightfarerBuilder.KevinClips kevin)
        {
            var result = new Dictionary<string, AnimationClip>();
            foreach (var kv in kevin.Loco)
            {
                bool sprint = kv.Key.StartsWith("Sprint");
                bool run = kv.Key.StartsWith("Run");
                string offsets = sprint ? "SF=0.55 CF=0.25 HN=0.35 LLS=-0.15 RLS=-0.15"
                    : run ? "SF=0.35 CF=0.15 HN=0.25 LLS=-0.12 RLS=-0.12"
                    : "SF=0.3 CF=0.12 HN=0.2 LLS=-0.15 RLS=-0.15 LUF=0.08 RUF=0.08";
                // Arms keep the clip's swing (only offset, never frozen): the claw arm is driven from the off arm's gait.
                if (sprint) offsets += " RAF=-0.15 LAF=-0.15";
                result[kv.Key] = PlaceholderClips.Derive(kv.Value, $"{dir}/S143_{kv.Key}.anim", "", offsets);
            }
            result["Surge_F"] = PlaceholderClips.Derive(kevin.Loco["Sprint_F"], $"{dir}/S143_Surge_F.anim",
                "", "SF=0.7 CF=0.35 HN=0.45 LLS=-0.2 RLS=-0.2 RAF=-0.25 LAF=-0.25");
            return result;
        }
    }
}
