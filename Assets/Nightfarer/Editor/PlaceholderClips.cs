using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Subject143.Nightfarer.EditorTools
{
    /// <summary>
    /// Procedural humanoid placeholder clips (attacks, rolls, hit react...) authored as muscle-space key poses
    /// on top of the project's own idle pose, so they retarget to any humanoid. They exist so the combat
    /// system is playable before real animations exist; replace them by assigning clips in an AnimationSet.
    /// Pose spec: "RAD=0.4 ST=-0.3 RootPitch=90" — muscle abbreviations below, values are absolute muscle
    /// values; RootPitch/RootYaw (deg), RootDrop/RootFwd (normalized body units) offset the body root.
    /// </summary>
    public static class PlaceholderClips
    {
        public enum Style { Baseline, Nightreign }

        static readonly Dictionary<string, string> M = new Dictionary<string, string>
        {
            { "SF", "Spine Front-Back" }, { "SL", "Spine Left-Right" }, { "ST", "Spine Twist Left-Right" },
            { "CF", "Chest Front-Back" }, { "CL", "Chest Left-Right" }, { "CT", "Chest Twist Left-Right" },
            { "UF", "UpperChest Front-Back" }, { "UT", "UpperChest Twist Left-Right" },
            { "NN", "Neck Nod Down-Up" }, { "HN", "Head Nod Down-Up" }, { "HT", "Head Turn Left-Right" },
            { "RSD", "Right Shoulder Down-Up" }, { "RSF", "Right Shoulder Front-Back" },
            { "RAD", "Right Arm Down-Up" }, { "RAF", "Right Arm Front-Back" }, { "RAT", "Right Arm Twist In-Out" },
            { "RFS", "Right Forearm Stretch" }, { "RFT", "Right Forearm Twist In-Out" },
            { "RHD", "Right Hand Down-Up" }, { "RHI", "Right Hand In-Out" },
            { "LSD", "Left Shoulder Down-Up" }, { "LSF", "Left Shoulder Front-Back" },
            { "LAD", "Left Arm Down-Up" }, { "LAF", "Left Arm Front-Back" }, { "LAT", "Left Arm Twist In-Out" },
            { "LFS", "Left Forearm Stretch" }, { "LFT", "Left Forearm Twist In-Out" },
            { "LHD", "Left Hand Down-Up" }, { "LHI", "Left Hand In-Out" },
            { "LUF", "Left Upper Leg Front-Back" }, { "LUI", "Left Upper Leg In-Out" }, { "LLS", "Left Lower Leg Stretch" }, { "LFU", "Left Foot Up-Down" },
            { "RUF", "Right Upper Leg Front-Back" }, { "RUI", "Right Upper Leg In-Out" }, { "RLS", "Right Lower Leg Stretch" }, { "RFU", "Right Foot Up-Down" },
        };

        static readonly string[] Specials = { "RootPitch", "RootYaw", "RootRoll", "RootDrop", "RootFwd" };

        /// <summary>IK goal curves (LeftFootT.x, RightHandQ.w, ...). Copying them pins limbs to the source pose.</summary>
        static bool IsIkGoal(string prop) =>
            (prop.StartsWith("LeftFoot") || prop.StartsWith("RightFoot") || prop.StartsWith("LeftHand") || prop.StartsWith("RightHand")) &&
            (prop.Contains("T.") || prop.Contains("Q."));

        public static Dictionary<string, float> Parse(string spec)
        {
            var d = new Dictionary<string, float>();
            if (string.IsNullOrWhiteSpace(spec)) return d;
            foreach (var tok in spec.Split(new[] { ' ', '\n', '\t' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = tok.Split('=');
                string key = M.TryGetValue(kv[0], out var full) ? full : kv[0];
                d[key] = float.Parse(kv[1], CultureInfo.InvariantCulture);
            }
            return d;
        }

        static Dictionary<string, float> Merge(Dictionary<string, float> a, Dictionary<string, float> b)
        {
            var r = new Dictionary<string, float>(a);
            foreach (var kv in b) r[kv.Key] = kv.Value;
            return r;
        }

        /// <summary>Builds and saves a clip from key poses (time in seconds, spec merged over the stance).</summary>
        public static AnimationClip Build(string path, Dictionary<string, float> basePose, string stance, float length, bool loop,
            params (float t, string spec)[] keys)
        {
            var stanceDict = Parse(stance);
            var keyDicts = keys.Select(k => (k.t, pose: Merge(stanceDict, Parse(k.spec)))).ToList();

            var clip = new AnimationClip { frameRate = 30f };

            var props = new HashSet<string>(basePose.Keys.Where(k => !k.StartsWith("RootQ") && !IsIkGoal(k)));
            foreach (var k in keyDicts) foreach (var p in k.pose.Keys) if (!Specials.Contains(p)) props.Add(p);

            foreach (var prop in props)
            {
                var curve = new AnimationCurve();
                foreach (var k in keyDicts)
                {
                    float v = k.pose.TryGetValue(prop, out var abs) ? abs : basePose.TryGetValue(prop, out var b) ? b : 0f;
                    if (prop == "RootT.y" && k.pose.TryGetValue("RootDrop", out var drop)) v = (basePose.TryGetValue(prop, out var by) ? by : 1f) + drop;
                    if (prop == "RootT.z" && k.pose.TryGetValue("RootFwd", out var fwd)) v = (basePose.TryGetValue(prop, out var bz) ? bz : 0f) + fwd;
                    curve.AddKey(new Keyframe(k.t, v));
                }
                Smooth(curve);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), prop), curve);
            }

            // Body rotation: base orientation * pitch/yaw/roll, kept sign-continuous for component-wise interpolation.
            Quaternion baseQ = new Quaternion(Get(basePose, "RootQ.x"), Get(basePose, "RootQ.y"), Get(basePose, "RootQ.z"), Get(basePose, "RootQ.w", 1f));
            var qc = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            Quaternion prev = baseQ;
            foreach (var k in keyDicts)
            {
                Quaternion q = baseQ * Quaternion.Euler(Get(k.pose, "RootPitch"), Get(k.pose, "RootYaw"), Get(k.pose, "RootRoll"));
                if (Quaternion.Dot(prev, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                prev = q;
                qc[0].AddKey(k.t, q.x); qc[1].AddKey(k.t, q.y); qc[2].AddKey(k.t, q.z); qc[3].AddKey(k.t, q.w);
            }
            string[] qn = { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            for (int i = 0; i < 4; i++)
            {
                Smooth(qc[i]);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), qn[i]), qc[i]);
            }

            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = loop;
            s.loopBlendOrientation = true;
            s.loopBlendPositionY = true;
            s.loopBlendPositionXZ = true;
            s.keepOriginalOrientation = true;
            s.keepOriginalPositionY = true;
            s.keepOriginalPositionXZ = true;
            s.stopTime = length;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            return SaveClip(clip, path);
        }

        /// <summary>
        /// Writes a freshly built clip to path. Existing assets are overwritten in place (GUID kept, so sets
        /// that reference them stay valid); rewriting the same clip object instead returns stale humanoid data.
        /// </summary>
        static AnimationClip SaveClip(AnimationClip fresh, string path)
        {
            fresh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, existing);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static float Get(Dictionary<string, float> d, string k, float def = 0f) => d.TryGetValue(k, out var v) ? v : def;

        static void Smooth(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            }
        }

        /// <summary>Copy a source clip, overriding some muscles with constants and offsetting others ("+").</summary>
        public static AnimationClip Derive(AnimationClip src, string path, string overrides, string offsets = null)
        {
            if (src == null) return null;
            var over = Parse(overrides);
            var off = Parse(offsets);
            var clip = new AnimationClip { frameRate = src.frameRate };
            bool armsOverridden = over.Keys.Any(k => k.Contains("Arm") || k.Contains("Forearm"));
            foreach (var b in AnimationUtility.GetCurveBindings(src))
            {
                if (armsOverridden && IsIkGoal(b.propertyName) && b.propertyName.Contains("Hand")) continue;
                var curve = AnimationUtility.GetEditorCurve(src, b);
                if (over.TryGetValue(b.propertyName, out var v))
                {
                    curve = AnimationCurve.Constant(0f, src.length, v);
                }
                else if (off.TryGetValue(b.propertyName, out var o))
                {
                    var keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++) keys[i].value += o;
                    curve.keys = keys;
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(b.path, b.type, b.propertyName), curve);
            }
            AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(src));
            return SaveClip(clip, path);
        }

        // ------------------------------------------------------------------ poses

        // Two-handed greatsword guard, blade carried at the right side (weapon-holding arms, slight crouch).
        const string StanceNR = "SF=0.25 CF=0.1 ST=0.05 RAD=-0.35 RAF=0.45 RAT=0.1 RFS=-0.15 RHD=0.2 LAD=-0.25 LAF=0.75 LAT=0.2 LFS=-0.35 LHD=0.1 LUF=0.62 LLS=0.7 RUF=0.42 RLS=0.74 RootDrop=-0.02";
        const string StanceBaseline = "SF=0.08 RAD=-0.45 RAF=0.4 RFS=0.1 LAD=-0.45 LAF=0.5 LFS=-0.1";

        public static Dictionary<string, AnimationClip> Generate(string dir, Dictionary<string, float> basePose, Style style)
        {
            bool nr = style == Style.Nightreign;
            string stance = nr ? StanceNR : StanceBaseline;
            float k = nr ? 1f : 0.75f;   // swing amplitude
            string P(float v) => (v * k).ToString("0.###", CultureInfo.InvariantCulture);
            var clips = new Dictionary<string, AnimationClip>();

            AnimationClip Make(string slot, float len, bool loop, params (float, string)[] keys)
            {
                var c = Build($"{dir}/{(nr ? "NR" : "Base")}_{slot}.anim", basePose, stance, len, loop, keys);
                clips[slot] = c;
                return c;
            }

            // Idle: combat stance with a gentle breathing sway (Nightreign set only; the baseline keeps the project idle).
            if (nr)
                Make("Idle", 2.4f, true, (0f, ""), (1.2f, "SF=0.29 CF=0.13 RootDrop=-0.05"), (2.4f, ""));

            // Diagonal slash right-high -> left-low.
            Make("Light1", 1.0f, false,
                (0f, ""),
                (0.3f, $"ST={P(-0.55f)} CT={P(-0.45f)} RAD={P(0.55f)} RAF={P(0.1f)} RFS=-0.5 LAD={P(0.35f)} LAF=0.6 LFS=-0.6 SF=0.05 RUF=0.35 LUF=0.1"),
                (0.42f, $"ST={P(0.55f)} CT={P(0.5f)} RAD={P(-0.35f)} RAF=0.95 RFS=0.6 LAD={P(-0.4f)} LAF=0.95 LFS=0.3 SF={P(0.45f)} LUF=0.65 LLS=0.35 RUF=-0.1 RootDrop=-0.08"),
                (0.6f, $"ST={P(0.7f)} CT={P(0.6f)} RAD={P(-0.55f)} RAF=0.6 RFS=0.4 LAD=-0.5 LAF=0.7 SF={P(0.4f)} LUF=0.55 LLS=0.4 RootDrop=-0.08"),
                (1.0f, ""));
            // Backhand return slash left -> right.
            Make("Light2", 1.0f, false,
                (0f, $"ST={P(0.6f)} CT={P(0.5f)} RAD=-0.5 RAF=0.6 SF=0.35"),
                (0.28f, $"ST={P(0.75f)} CT={P(0.65f)} RAD={P(0.3f)} RAF=0.9 RFS=-0.3 LAD=0.1 LAF=1.0 LFS=-0.4 SF=0.2 LUF=0.3"),
                (0.4f, $"ST={P(-0.6f)} CT={P(-0.55f)} RAD={P(0.05f)} RAF=0.9 RFS=0.7 LAD=-0.1 LAF=0.8 LFS=0.4 SF={P(0.4f)} RUF=0.6 RLS=0.35 RootDrop=-0.07"),
                (0.6f, $"ST={P(-0.7f)} CT={P(-0.6f)} RAD=-0.3 RAF=0.4 SF=0.35 RUF=0.5 RootDrop=-0.07"),
                (1.0f, ""));
            // Overhead vertical chop.
            Make("Light3", 1.15f, false,
                (0f, ""),
                (0.36f, $"RAD={P(1.0f)} RAF=0.7 RFS=-0.6 LAD={P(0.9f)} LAF=0.7 LFS=-0.6 SF=-0.25 CF=-0.25 HN=0.2 RUF=0.1 LUF=0.4"),
                (0.5f, $"RAD={P(-0.2f)} RAF=1.0 RFS=0.8 LAD={P(-0.25f)} LAF=1.0 LFS=0.7 SF={P(0.7f)} CF=0.3 LUF=0.8 LLS=0.3 RUF=-0.2 RootDrop=-0.14"),
                (0.75f, $"RAD=-0.5 RAF=0.8 RFS=0.6 LAD=-0.5 LAF=0.8 SF=0.6 LUF=0.7 LLS=0.35 RootDrop=-0.13"),
                (1.15f, ""));
            // Thrust.
            Make("Light4", 0.9f, false,
                (0f, ""),
                (0.25f, $"ST={P(-0.4f)} RAD=-0.2 RAF=0.1 RFS=-0.8 LAF=0.4 SF=0.1 RUF=0.2"),
                (0.36f, $"ST={P(0.2f)} RAD=-0.05 RAF=1.0 RFS=0.9 LAF=0.9 LFS=0.5 SF={P(0.5f)} LUF=0.7 LLS=0.4 RootDrop=-0.08"),
                (0.9f, ""));
            // Heavy: big wind-up behind the head, ground slam (the charge holds near the wind-up).
            Make("Heavy1", 1.45f, false,
                (0f, ""),
                (0.4f, $"ST={P(-0.7f)} CT={P(-0.5f)} RAD={P(1.0f)} RAF=0.2 RFS=-0.8 LAD={P(0.9f)} LAF=0.4 LFS=-0.8 SF=-0.3 CF=-0.3 RUF=0.4 LUF=0.1"),
                (0.66f, $"ST={P(0.35f)} RAD={P(-0.3f)} RAF=1.0 RFS=0.9 LAD={P(-0.35f)} LAF=1.0 LFS=0.8 SF={P(0.8f)} CF=0.4 LUF=0.9 LLS=0.25 RUF=-0.3 RootDrop=-0.18"),
                (1.0f, $"RAD=-0.6 RAF=0.9 SF=0.7 LUF=0.8 LLS=0.3 RootDrop=-0.17"),
                (1.45f, ""));
            // Heavy 2: horizontal sweep.
            Make("Heavy2", 1.4f, false,
                (0f, ""),
                (0.38f, $"ST={P(-0.9f)} CT={P(-0.7f)} RAD={P(0.3f)} RAF=-0.2 RFS=-0.3 LAD={P(0.2f)} LAF=0.4 LFS=-0.5 SF=0.1 RUF=0.4 RootDrop=-0.05"),
                (0.64f, $"ST={P(0.9f)} CT={P(0.8f)} RAD={P(0.1f)} RAF=0.9 RFS=0.9 LAD=0 LAF=0.9 LFS=0.6 SF={P(0.35f)} LUF=0.6 LLS=0.4 RootDrop=-0.1 RootYaw={P(25f)}"),
                (0.95f, $"ST={P(0.9f)} CT={P(0.8f)} RAD=-0.4 RAF=0.4 SF=0.3 LUF=0.5 RootDrop=-0.09 RootYaw={P(20f)}"),
                (1.4f, ""));
            // Running leap slash.
            Make("SprintAttack", 1.1f, false,
                (0f, $"SF=0.5 RAD=-0.6 RAF=-0.4 LAD=-0.6 LAF=-0.3 LUF=0.8 LLS=0.3 RUF=-0.3"),
                (0.28f, $"RAD={P(1.0f)} RAF=0.6 RFS=-0.6 LAD={P(0.9f)} LAF=0.6 LFS=-0.6 SF=-0.1 CF=-0.2 LUF=0.9 LLS=0.2 RUF=0.6 RLS=0.3 RootDrop=0.05"),
                (0.42f, $"RAD={P(-0.3f)} RAF=1.0 RFS=0.9 LAD={P(-0.35f)} LAF=1.0 LFS=0.8 SF={P(0.85f)} LUF=1.0 LLS=0.2 RUF=-0.4 RootDrop=-0.2"),
                (0.7f, $"RAD=-0.5 RAF=0.8 SF=0.7 LUF=0.8 LLS=0.3 RootDrop=-0.16"),
                (1.1f, ""));
            // Jump attack: overhead raised in the air, strike pose held until landing.
            Make("JumpAttack", 0.95f, false,
                (0f, $"RAD=0.6 RAF=0.5 LAD=0.5 LAF=0.5 LUF=0.6 LLS=0.2 RUF=0.5 RLS=0.3"),
                (0.22f, $"RAD={P(1.0f)} RAF=0.6 RFS=-0.7 LAD={P(0.95f)} LAF=0.6 LFS=-0.7 SF=-0.3 LUF=0.8 LLS=0.1 RUF=0.7 RLS=0.2"),
                (0.36f, $"RAD={P(-0.25f)} RAF=1.0 RFS=0.9 LAD={P(-0.3f)} LAF=1.0 LFS=0.8 SF={P(0.85f)} LUF=0.9 LLS=0.2 RUF=0.2 RootDrop=-0.15"),
                (0.65f, $"RAD=-0.5 RAF=0.9 SF=0.8 LUF=0.9 LLS=0.25 RootDrop=-0.2"),
                (0.95f, ""));
            // Rising slash out of a roll.
            Make("RollAttack", 0.95f, false,
                (0f, $"SF=0.8 RootDrop=-0.25 LUF=0.9 LLS=0.1 RUF=0.6 RLS=0.2 RAD=-0.7 RAF=0.6"),
                (0.18f, $"ST={P(-0.4f)} RAD={P(-0.8f)} RAF=0.5 RFS=0.6 LAD=-0.7 LAF=0.5 SF=0.6 RootDrop=-0.18 LUF=0.8 LLS=0.3"),
                (0.32f, $"ST={P(0.5f)} RAD={P(0.9f)} RAF=0.9 RFS=0.8 LAD={P(0.8f)} LAF=0.9 LFS=0.6 SF=-0.15 CF=-0.2 RootDrop=0.02 RUF=0.4"),
                (0.55f, $"ST={P(0.5f)} RAD=0.6 RAF=0.7 SF=-0.05"),
                (0.95f, ""));
            // Lunging thrust after a backstep.
            Make("BackstepAttack", 0.9f, false,
                (0f, "SF=-0.1"),
                (0.2f, $"ST={P(-0.4f)} RAD=-0.2 RAF=0.05 RFS=-0.9 LAF=0.4 SF=0.05 RUF=0.3"),
                (0.33f, $"ST={P(0.25f)} RAD=0 RAF=1.0 RFS=1.0 LAD=0 LAF=1.0 LFS=0.7 SF={P(0.6f)} LUF=0.9 LLS=0.3 RUF=-0.3 RootDrop=-0.1"),
                (0.9f, ""));
            // Skill: left arm fires the claw, then a brace.
            Make("Skill", 0.8f, false,
                (0f, ""),
                (0.18f, $"LAD=0.15 LAF=1.0 LFS=1.0 LAT=0.2 ST={P(-0.35f)} SF=0.15 RAD=-0.5 RAF=0.2"),
                (0.5f, $"LAD=0.1 LAF=0.9 LFS=0.6 ST={P(-0.25f)} SF=0.4 RootDrop=-0.08 LUF=0.6 LLS=0.4"),
                (0.8f, ""));
            // Claw follow-up: spinning overhead strike.
            Make("SkillFollowUp", 1.0f, false,
                (0f, "SF=0.4 RootDrop=-0.08"),
                (0.2f, $"RAD={P(1.0f)} RAF=0.5 RFS=-0.7 LAD={P(0.9f)} LAF=0.5 LFS=-0.7 SF=-0.2 RootYaw={P(-60f)} RootDrop=0.04"),
                (0.36f, $"RAD={P(-0.3f)} RAF=1.0 RFS=0.9 LAD=-0.3 LAF=1.0 LFS=0.8 SF={P(0.85f)} LUF=0.9 LLS=0.2 RootYaw={P(20f)} RootDrop=-0.18"),
                (0.65f, $"RAD=-0.5 RAF=0.8 SF=0.7 LUF=0.8 RootDrop=-0.15"),
                (1.0f, ""));

            // Forward roll: body pitches a full turn while tucked.
            string tuck = "SF=0.9 CF=0.5 HN=-0.6 LUF=1.0 LLS=-0.6 RUF=1.0 RLS=-0.6 RAD=-0.2 RAF=0.9 LAD=-0.2 LAF=0.9 RFS=-0.4 LFS=-0.4";
            Make("Roll", 0.8f, false,
                (0f, "SF=0.4 RootDrop=-0.1"),
                (0.12f, tuck + " RootPitch=60 RootDrop=-0.35"),
                (0.24f, tuck + " RootPitch=150 RootDrop=-0.5"),
                (0.36f, tuck + " RootPitch=240 RootDrop=-0.5"),
                (0.48f, tuck + " RootPitch=330 RootDrop=-0.35"),
                (0.6f, "SF=0.5 LUF=0.6 LLS=0.3 RUF=0.3 RLS=0.4 RootPitch=360 RootDrop=-0.18"),
                (0.8f, "RootPitch=360"));
            Make("LandRoll", 0.65f, false,
                (0f, "SF=0.6 LUF=0.8 RUF=0.8 LLS=0.2 RLS=0.2 RootDrop=-0.25"),
                (0.1f, tuck + " RootPitch=80 RootDrop=-0.45"),
                (0.2f, tuck + " RootPitch=180 RootDrop=-0.5"),
                (0.3f, tuck + " RootPitch=280 RootDrop=-0.42"),
                (0.42f, "SF=0.5 LUF=0.6 LLS=0.3 RootPitch=360 RootDrop=-0.15"),
                (0.65f, "RootPitch=360"));
            // Backstep: quick hop back with the torso upright.
            Make("Backstep", 0.52f, false,
                (0f, ""),
                (0.12f, "SF=-0.15 CF=-0.15 LUF=0.5 LLS=0.4 RUF=0.6 RLS=0.3 RootDrop=0.03 RootPitch=-8"),
                (0.3f, "SF=0.3 LUF=0.5 LLS=0.45 RUF=0.2 RLS=0.5 RootDrop=-0.08"),
                (0.52f, ""));
            // Hit react: torso snaps back.
            Make("HitReact", 0.45f, false,
                (0f, ""),
                (0.08f, "SF=-0.5 CF=-0.4 HN=0.5 RAD=-0.2 RAF=-0.3 LAD=-0.2 LAF=-0.3 RootPitch=-12 RootDrop=-0.03"),
                (0.25f, "SF=0.1 CF=0 RootPitch=-4"),
                (0.45f, ""));
            // Mantle: reach up, pull, swing the legs over.
            Make("Mantle", 0.6f, false,
                (0f, "RAD=0.9 RAF=0.6 LAD=0.9 LAF=0.6 RFS=0.6 LFS=0.6 SF=-0.1 LUF=0.5 RUF=0.7 RLS=0.5"),
                (0.2f, "RAD=0.6 RAF=0.8 LAD=0.6 LAF=0.8 RFS=-0.6 LFS=-0.6 SF=0.5 LUF=1.0 LLS=-0.3 RUF=0.9 RLS=0.0 RootDrop=-0.15"),
                (0.4f, "RAD=-0.6 RAF=0.4 LAD=-0.6 LAF=0.4 RFS=0.6 LFS=0.6 SF=0.6 LUF=0.9 LLS=0.2 RUF=0.6 RLS=0.4 RootDrop=-0.12"),
                (0.6f, ""));
            // Drink a flask with the off hand.
            Make("Drink", 0.95f, false,
                (0f, ""),
                (0.3f, "LAD=0.1 LAF=1.0 LFS=-0.95 LAT=0.3 LHD=-0.3 HN=-0.4 NN=-0.3"),
                (0.55f, "LAD=0.25 LAF=1.0 LFS=-1.0 LAT=0.3 LHD=-0.4 HN=-0.6 NN=-0.5 SF=-0.1"),
                (0.95f, ""));
            // Ultimate: deep wind-up crouch, explosive drop into the slam.
            Make("Ultimate", 1.45f, false,
                (0f, ""),
                (0.35f, $"SF=-0.3 CF=-0.3 ST={P(-0.4f)} LUF=0.75 LLS=0.5 RUF=0.5 RLS=0.6 RootDrop=0.04"),
                (0.5f, $"SF={P(0.9f)} CF=0.4 LUF=1.0 LLS=0.15 RUF=0.1 RLS=0.5 RootDrop=-0.25"),
                (0.85f, $"SF=0.8 LUF=0.95 LLS=0.2 RootDrop=-0.24"),
                (1.45f, ""));

            // Landing dip.
            if (nr)
                Make("Land", 0.4f, false,
                    (0f, "LUF=0.7 LLS=0.2 RUF=0.6 RLS=0.25 SF=0.45 RootDrop=-0.18"),
                    (0.12f, "LUF=0.8 LLS=0.1 RUF=0.7 RLS=0.15 SF=0.5 RootDrop=-0.22"),
                    (0.4f, ""));
            return clips;
        }

        /// <summary>Nightreign-flavoured locomotion derived from the project's own Kevin Iglesias clips.</summary>
        public static Dictionary<string, AnimationClip> DeriveNightreignLocomotion(string dir, NightfarerBuilder.KevinClips kevin)
        {
            var result = new Dictionary<string, AnimationClip>();
            // Arms carry the weapon (right hand low at the side, left arm free), torso leans into motion.
            const string carry = "RAD=-0.62 RAF=0.12 RAT=0.1 RFS=0.35 RHD=0.25";
            foreach (var kv in kevin.Loco)
            {
                bool sprint = kv.Key.StartsWith("Sprint");
                bool run = kv.Key.StartsWith("Run");
                string lean = sprint ? "SF=0.25 CF=0.1" : run ? "SF=0.12" : "SF=0.05";
                result[kv.Key] = Derive(kv.Value, $"{dir}/NR_{kv.Key}.anim", carry, lean);
            }
            // Surge sprint: aggressive forward lean, arms swept back (Nightreign's surge dash silhouette).
            result["Surge_F"] = Derive(kevin.Loco["Sprint_F"], $"{dir}/NR_Surge_F.anim",
                "RAD=-0.45 RAF=-0.75 RFS=0.6 LAD=-0.45 LAF=-0.75 LFS=0.6",
                "SF=0.45 CF=0.25 HN=-0.2");
            return result;
        }
    }
}
