using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Subject143.Nightfarer.Tests
{
    /// <summary>
    /// Climbing with hands and feet on the wall, super jump, hero landing, side jump, claw line in the air and its
    /// momentum, foot IK, upright traversal clips, and the combat screen effects. Captures go to
    /// Logs/NightfarerCaptures for visual review.
    /// </summary>
    public class NightfarerTraversalTests
    {
        const string ArenaPath = "Assets/Nightfarer/Scenes/NightfarerTestScene.unity";
        NightfarerCharacter C;
        ScriptedInputSource In;
        NightfarerConfig Cfg => C.Config;

        static void Metric(string line)
        {
            Directory.CreateDirectory("Logs");
            File.AppendAllText("Logs/nightfarer_playmode_metrics.txt", line + "\n");
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator Arena()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(ArenaPath);
#endif
            yield return null;
            C = Object.FindFirstObjectByType<NightfarerCharacter>();
            In = new ScriptedInputSource();
            C.SetInputSource(In);
            yield return Frames(5);
            C.ApplyProfile(0);
            yield return Frames(2);
        }

        void Warp(Vector3 pos, Vector3 facing)
        {
            C.Motor.Warp(pos, Quaternion.LookRotation(facing));
            C.ChangeState(new LocomotionState(C));
            C.cameraRig.SnapBehind();
            C.Vitals.RefillAll();
        }

        IEnumerator WaitForState(string prefix, float timeout)
        {
            for (float t = 0f; t < timeout && !C.StateName.StartsWith(prefix); t += Time.deltaTime) yield return null;
        }

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        // ------------------------------------------------------------------ captures

        class Sheet
        {
            readonly List<Texture2D> shots = new List<Texture2D>();
            const int W = 420, H = 380;

            public void Shot(Camera cam, Vector3 from, Vector3 lookAt)
            {
                cam.transform.position = from;
                cam.transform.LookAt(lookAt);
                Grab(cam);
            }

            public void Grab(Camera cam)
            {
                var rt = new RenderTexture(W, H, 24);
                var prev = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = prev;
                rt.Release();
                shots.Add(tex);
            }

            public int Count => shots.Count;

            public void Save(string name)
            {
                int cols = Mathf.Min(3, shots.Count), rows = Mathf.CeilToInt(shots.Count / 3f);
                var sheet = new Texture2D(W * cols, H * rows, TextureFormat.RGB24, false);
                for (int i = 0; i < shots.Count; i++) sheet.SetPixels((i % 3) * W, (rows - 1 - i / 3) * H, W, H, shots[i].GetPixels());
                sheet.Apply();
                Directory.CreateDirectory("Logs/NightfarerCaptures");
                File.WriteAllBytes($"Logs/NightfarerCaptures/{name}.png", sheet.EncodeToPNG());
            }
        }

        static Camera TestCamera()
        {
            var go = new GameObject("TestCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.enabled = false;
            return cam;
        }

        // ------------------------------------------------------------------ climbing

        [UnityTest]
        public IEnumerator Climb_HandsOnTheLip_FeetStepUpTheWall()
        {
            yield return Arena();
            Warp(new Vector3(-12.5f, 0.05f, 22.6f), Vector3.forward);   // 2 m wall, front face at z = 24.5
            yield return Frames(3);
            In.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.25f);
            In.TapJump();
            yield return WaitForState("Mantle", 1.2f);
            Assert.AreEqual("Mantle", C.StateName);
            var mantle = (MantleState)C.State;
            var anim = C.Animator.animator;
            var lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            var rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            var cam = TestCamera();
            var sheet = new Sheet();
            float handGap = 9f, footWall = 9f, footRise = 0f, maxFootY = -9f, minFootY = 9f;
            float wallZ = mantle.Lip.z;
            while (C.State is MantleState)
            {
                float u = mantle.Progress;
                if (u > 0.25f && u < 0.85f)
                {
                    float l = Vector3.Distance(lHand.position, mantle.Lip), r = Vector3.Distance(rHand.position, mantle.Lip);
                    handGap = Mathf.Min(handGap, Mathf.Max(l, r));
                }
                if (u > 0.25f && u < 0.6f)
                {
                    footWall = Mathf.Min(footWall, Mathf.Abs(Mathf.Min(lFoot.position.z, rFoot.position.z) - wallZ));
                    maxFootY = Mathf.Max(maxFootY, lFoot.position.y);
                    minFootY = Mathf.Min(minFootY, lFoot.position.y);
                }
                if (sheet.Count < 6 && u > sheet.Count * 0.16f + 0.05f)
                    sheet.Shot(cam, C.transform.position + new Vector3(2.6f, 0.9f, -1.4f), C.transform.position + Vector3.up * 0.7f);
                yield return null;
            }
            footRise = maxFootY - minFootY;
            sheet.Save("climb");
            Object.Destroy(cam.gameObject);
            In.Move = Vector2.zero;
            yield return new WaitForSeconds(0.3f);
            Metric($"climb: hands within {handGap:F2} m of the lip (each hand ~0.17 m to the side), feet {footWall:F2} m from the wall, left foot stepped up {footRise:F2} m, ended at y={C.transform.position.y:F2}");
            Assert.Less(handGap, 0.3f, "both hands grip the lip while climbing");
            Assert.Less(footWall, 0.2f, "feet push against the wall");
            Assert.Greater(footRise, 0.15f, "feet step up the wall");
            Assert.AreEqual(2.0f, C.transform.position.y, 0.2f);
        }

        [UnityTest]
        public IEnumerator UprightSet_UsesUprightClimbAndDrinkClips()
        {
            yield return Arena();
            Assert.That(C.Animator.ActiveSet.displayName, Does.Contain("upright"));
            foreach (var slot in new[] { "Mantle", "Vault", "Drink", "HeroLand", "SuperJumpCharge", "SideJumpL" })
                Assert.That(C.Animator.GetClip(slot).name, Does.StartWith("S143U_"), slot + " should be the upright clip");
            C.CycleAnimationSet();
            Assert.That(C.Animator.GetClip("Mantle").name, Does.StartWith("S143_"), "feral set keeps its hunched climb");
            Metric($"upright set: Mantle={C.Animator.ActiveSet.Get("Mantle")?.name}, upright Drink/Mantle are S143U_ clips");
        }

        // ------------------------------------------------------------------ super jump + hero landing

        [UnityTest]
        public IEnumerator SuperJump_HoldToCharge_ReleaseToSpringHigh_HeroLanding()
        {
            yield return Arena();
            Warp(new Vector3(0f, 0.05f, -6f), Vector3.forward);
            yield return new WaitForSeconds(0.3f);
            var cam = TestCamera();
            var sheet = new Sheet();
            float y0 = C.transform.position.y;
            Vector3 p0 = C.transform.position;
            In.JumpHeld = true;
            yield return new WaitForSeconds(0.35f);
            Assert.AreEqual("Super Jump Charge", C.StateName, "holding Jump charges");
            yield return new WaitForSeconds(0.6f);
            float charge = ((SuperJumpChargeState)C.State).Charge;
            float drift = Vector3.Distance(p0, C.transform.position);
            sheet.Shot(cam, C.transform.position + C.transform.right * 2.2f + Vector3.up * 0.7f, C.transform.position + Vector3.up * 0.5f);
            In.JumpHeld = false;
            float peak = y0;
            bool shotAir = false;
            for (float t = 0f; t < 4f && !(C.StateName == "Hero Landing"); t += Time.deltaTime)
            {
                peak = Mathf.Max(peak, C.transform.position.y);
                if (!shotAir && t > 0.25f)
                {
                    shotAir = true;
                    sheet.Shot(cam, C.transform.position + C.transform.right * 3f + Vector3.up * 0.5f, C.transform.position + Vector3.up * 0.5f);
                }
                yield return null;
            }
            Assert.AreEqual("Hero Landing", C.StateName, "the drop ends in a hero landing");
            var hero = (HeroLandingState)C.State;
            yield return new WaitForSeconds(0.15f);
            var anim = C.Animator.animator;
            var knee = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            var claw = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            float ground = C.transform.position.y;
            float kneeH = knee.position.y - ground, clawH = claw.position.y - ground;
            sheet.Shot(cam, C.transform.position + C.transform.forward * 2.2f + C.transform.right * 0.8f + Vector3.up * 0.6f, C.transform.position + Vector3.up * 0.35f);
            sheet.Shot(cam, C.transform.position + C.transform.right * 2.2f + Vector3.up * 0.5f, C.transform.position + Vector3.up * 0.35f);
            sheet.Save("superjump_herolanding");
            Object.Destroy(cam.gameObject);
            Metric($"super jump: charge {charge:P0} (moved {drift:F2} m while crouched), apex {peak - y0:F1} m; hero landing severity {hero.Severity:F2}, knee {kneeH:F2} m and claw {clawH:F2} m above the ground");
            Assert.Greater(charge, 0.95f);
            Assert.Less(drift, 0.15f, "stationary charge stays in place");
            Assert.Greater(peak - y0, 5.5f, "springs up high");
            Assert.Less(kneeH, 0.3f, "knee down");
            Assert.Less(clawH, 0.3f, "claw planted on the ground (wrist height; the claws reach the floor)");
            yield return WaitForState("Locomotion", 1.5f);
            Assert.AreEqual("Locomotion", C.StateName);
        }

        [UnityTest]
        public IEnumerator TapJump_IsStillANormalJump_AndHoldWhileRunningJumpsForward()
        {
            yield return Arena();
            Warp(new Vector3(0f, 0.05f, -6f), Vector3.forward);
            yield return Frames(5);
            In.TapJump();
            yield return Frames(3);
            Assert.AreEqual("Jump", C.StateName);
            yield return WaitForState("Locomotion", 2f);
            yield return new WaitForSeconds(0.3f);
            Warp(new Vector3(-6f, 0.05f, -14f), Vector3.forward);
            In.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.6f);
            Vector3 start = C.transform.position;
            In.JumpHeld = true;
            yield return new WaitForSeconds(0.6f);
            In.JumpHeld = false;
            float peak = start.y, speed = 0f;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                peak = Mathf.Max(peak, C.transform.position.y);
                speed = Mathf.Max(speed, C.Motor.PlanarSpeed);
                yield return null;
            }
            In.Move = Vector2.zero;
            Metric($"running super jump: air speed {speed:F1} m/s, still rising at {peak - start.y:F1} m after 0.5 s");
            Assert.Greater(speed, Cfg.runSpeed);
            Assert.Greater(peak - start.y, 2.5f);
        }

        [UnityTest]
        public IEnumerator HighDrop_HeroLanding_ShakesAndCracksTheGround()
        {
            yield return Arena();
            Warp(new Vector3(10 + 8 * 2.2f, 9.1f, 40f), Vector3.back);   // 9 m ledge
            yield return Frames(3);
            int impacts = GroundImpact.Spawned;
            float hp = C.Vitals.Health;
            In.Move = new Vector2(0f, 1f);
            yield return WaitForState("Hero Landing", 4f);
            In.Move = Vector2.zero;
            Metric($"high drop: {C.StateName}, ground impacts +{GroundImpact.Spawned - impacts}, health {hp}->{C.Vitals.Health}");
            Assert.AreEqual("Hero Landing", C.StateName);
            Assert.Greater(GroundImpact.Spawned, impacts);
            Assert.AreEqual(hp, C.Vitals.Health, 0.01f);
        }

        // ------------------------------------------------------------------ side jump

        [UnityTest]
        public IEnumerator SideJump_LockedOn_HopsSidewaysFacingTheTarget()
        {
            yield return Arena();
            var dummy = Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).First(d => d.name.StartsWith("Dummy A"));
            Vector3 dir = -dummy.transform.position; dir.y = 0f; dir.Normalize();
            Warp(dummy.transform.position + dir * 4.5f, -dir);
            yield return Frames(3);
            In.TapLockOn();
            yield return Frames(10);
            Assert.IsTrue(C.IsLocked);
            Vector3 start = C.transform.position;
            In.Move = new Vector2(1f, 0f);
            yield return Frames(2);
            In.TapJump();
            yield return Frames(2);
            In.Move = Vector2.zero;
            Assert.AreEqual("Side Jump", C.StateName);
            bool iframes = false;
            for (float t = 0f; t < 1f && !C.StateName.StartsWith("Locomotion"); t += Time.deltaTime)
            {
                iframes |= C.IsInvulnerable;
                yield return null;
            }
            Vector3 moved = C.transform.position - start; moved.y = 0f;
            float lateral = Mathf.Abs(Vector3.Dot(moved, Vector3.Cross(Vector3.up, -dir)));
            float facingErr = Vector3.Angle(C.transform.forward, C.LockTargetDirection());
            Metric($"side jump: lateral {lateral:F2} m, facing error {facingErr:F1} deg, i-frames {iframes}");
            Assert.Greater(lateral, 2.6f);
            Assert.Less(facingErr, 25f);
            Assert.IsTrue(iframes);
        }

        // ------------------------------------------------------------------ claw line: air + momentum

        IEnumerator AimLevel()
        {
            // Level the camera so the claw line aims straight ahead.
            typeof(NightfarerCamera).GetField("pitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(C.cameraRig, 0f);
            yield return null;
            yield return null;
        }

        GameObject Wall(Vector3 at)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "TestHookWall";
            w.transform.position = at;
            w.transform.localScale = new Vector3(8f, 4f, 0.5f);
            return w;
        }

        [UnityTest]
        public IEnumerator ClawShot_InTheAir_CarriesMomentum_JumpBoostsIt()
        {
            yield return Arena();
            Warp(new Vector3(-30f, 0.05f, -20f), Vector3.forward);
            var wall = Wall(new Vector3(-30f, 1.2f, -6f));
            yield return AimLevel();
            yield return Frames(3);
            In.TapJump();
            yield return new WaitForSeconds(0.2f);
            bool airborneAtThrow = !C.Motor.Grounded;
            In.TapSkill();
            yield return Frames(2);
            Assert.That(C.StateName, Does.StartWith("Skill:"), "claw line usable in the air");
            for (float t = 0f; t < 2f && C.StateName.StartsWith("Skill:"); t += Time.deltaTime) yield return null;
            float carried = C.Motor.PlanarSpeed;
            float momentumLeft = C.MomentumTimer;
            Metric($"air claw line: thrown airborne {airborneAtThrow}, planar speed carried out {carried:F1} m/s (run {Cfg.runSpeed}), momentum {momentumLeft:F2}s");
            Assert.IsTrue(airborneAtThrow);
            Assert.Greater(carried, Cfg.runSpeed * 1.4f, "pull speed is carried out");

            // Again from the ground, jump-cancelling the pull: a faster leap.
            yield return new WaitForSeconds(1.5f);
            typeof(NightfarerCharacter).GetProperty("SkillCooldownRemaining").SetValue(C, 0f);
            Warp(new Vector3(-30f, 0.05f, -22f), Vector3.forward);
            yield return AimLevel();
            yield return Frames(3);
            In.TapSkill();
            yield return Frames(2);
            string afterSkill = C.StateName;
            for (float t = 0f; t < 1f && C.CombatPhase != "Pull"; t += Time.deltaTime) yield return null;
            string phase = C.CombatPhase;
            yield return new WaitForSeconds(0.05f);
            In.TapJump();
            yield return new WaitForSeconds(0.05f);
            float leapSpeed = C.Motor.PlanarSpeed;
            string leapState = C.StateName;
            Metric($"claw line jump cancel: after skill '{afterSkill}', phase '{phase}', state {leapState}, leap speed {leapSpeed:F1} m/s (sprint {Cfg.sprintSpeed}), log {string.Join(">", C.StateLog.Skip(C.StateLog.Count - 5))}");
            Object.Destroy(wall);
            Assert.AreEqual("Jump", leapState);
            Assert.Greater(leapSpeed, Cfg.sprintSpeed * 1.3f);
        }

        [UnityTest]
        public IEnumerator SurgeSprint_HoldJump_KeepsFullSpeed_NoCrouch_ThenBigJump()
        {
            yield return Arena();
            Warp(new Vector3(-12f, 0.05f, -20f), Vector3.forward);
            In.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.3f);
            In.TapSurge();
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(C.IsSurging, "surge sprinting");
            var fx = C.GetComponent<CombatFX>();
            bool lines = fx.SurgeLinesEmitting;
            float y0 = C.transform.position.y;
            In.JumpHeld = true;
            float minSpeed = 99f;
            string anim = "";
            bool charging = false;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                if (t > 0.25f)
                {
                    minSpeed = Mathf.Min(minSpeed, C.Motor.PlanarSpeed);
                    anim = C.Animator.CurrentState;
                    charging |= C.State is SuperJumpChargeState s && s.Running;
                }
                yield return null;
            }
            In.JumpHeld = false;
            float peak = y0, airSpeed = 0f;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                peak = Mathf.Max(peak, C.transform.position.y);
                airSpeed = Mathf.Max(airSpeed, C.Motor.PlanarSpeed);
                yield return null;
            }
            In.Move = Vector2.zero;
            Metric($"surge super jump: surge lines {lines}, running charge {charging}, min speed while holding {minSpeed:F1} m/s (surge {Cfg.surgeSpeed}), animation '{anim}', apex {peak - y0:F1} m, air speed {airSpeed:F1} m/s");
            Assert.IsTrue(lines, "action lines while surge sprinting");
            Assert.IsTrue(charging);
            Assert.Greater(minSpeed, Cfg.surgeSpeed * 0.9f, "no slowdown");
            Assert.AreEqual("Locomotion", anim, "no crouch animation");
            Assert.Greater(peak - y0, 4f, "the big jump still happens");
            Assert.Greater(airSpeed, Cfg.surgeSpeed * 0.9f);
        }

        // ------------------------------------------------------------------ hair

        [UnityTest]
        public IEnumerator Hair_StrandsSwayWithMotion_StayAttached()
        {
            yield return Arena();
            Warp(new Vector3(-12f, 0.05f, -20f), Vector3.forward);
            var hair = C.GetComponentsInChildren<ChainCloth>().FirstOrDefault(c => c.preset == ChainCloth.Preset.Hair);
            Assert.IsNotNull(hair, "hair strands use chain cloth");
            Assert.GreaterOrEqual(hair.ChainCount, 5);
            var head = C.Animator.animator.GetBoneTransform(HumanBodyBones.Head);
            var tips = C.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("HairStrand_"))
                .GroupBy(t => t.name.Substring(0, t.name.LastIndexOf('_'))).Select(g => g.OrderBy(t => t.name).Last()).ToList();
            Vector3[] Local() => tips.Select(t => head.InverseTransformPoint(t.position)).ToArray();
            yield return new WaitForSeconds(0.6f);
            var rest = Local();
            var cam = TestCamera();
            var sheet = new Sheet();
            sheet.Shot(cam, head.position + C.transform.right * 0.7f + C.transform.forward * 0.3f, head.position);   // at rest
            yield return new WaitForSeconds(0.3f);
            In.Move = new Vector2(0f, 1f);
            In.DodgeHeld = true;
            float maxSway = 0f, maxStretch = 0f, maxDist = 0f;
            for (float t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                var now = Local();
                for (int i = 0; i < now.Length; i++)
                {
                    maxSway = Mathf.Max(maxSway, (now[i] - rest[i]).magnitude * head.lossyScale.x);
                    maxDist = Mathf.Max(maxDist, Vector3.Distance(tips[i].position, head.position));
                }
                maxStretch = Mathf.Max(maxStretch, hair.MaxStretch);
                yield return null;
            }
            In.DodgeHeld = false;
            In.Move = Vector2.zero;
            sheet.Shot(cam, head.position + C.transform.right * 0.7f + C.transform.forward * 0.3f, head.position);   // just stopped
            sheet.Shot(cam, head.position + C.transform.forward * 0.7f, head.position);
            sheet.Save("hair_strands");
            Object.Destroy(cam.gameObject);
            Metric($"hair: {hair.ChainCount} strands, max sway {maxSway:F3} m while sprinting, max stretch {maxStretch:F3}, furthest tip {maxDist:F2} m from the head");
            Assert.Greater(maxSway, 0.008f, "strands move with the motion");
            Assert.Less(maxStretch, 1.05f, "strands don't stretch");
            Assert.Less(maxDist, 0.35f, "strands stay on the head");
        }

        // ------------------------------------------------------------------ foot IK

        [UnityTest]
        public IEnumerator FootIK_PlantsBothFeetOnAStep()
        {
            yield return Arena();
            var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = "TestStep";
            step.transform.position = new Vector3(-44f, 0.125f, -35f);
            step.transform.localScale = new Vector3(2f, 0.25f, 2f);
            // Stand on the step's edge: one foot up on it, the other on the floor beside it.
            Warp(new Vector3(-44.93f, 0.3f, -35f), Vector3.forward);
            yield return new WaitForSeconds(1.0f);
            var ik = C.FootIK;
            Assert.IsNotNull(ik);
            var anim = C.Animator.animator;
            float lY = anim.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, rY = anim.GetBoneTransform(HumanBodyBones.RightFoot).position.y;
            var cam = TestCamera();
            var sheet = new Sheet();
            sheet.Shot(cam, C.transform.position + new Vector3(0f, 0.5f, 2.2f), C.transform.position + Vector3.up * 0.4f);
            sheet.Save("foot_ik");
            Object.Destroy(cam.gameObject);
            Metric($"foot IK: ankle {ik.AnkleHeight:F3} m, weight {ik.Weight:F2}, pelvis {ik.PelvisOffset:F3} m, sole gaps L {ik.LeftGap:F3} R {ik.RightGap:F3}, foot heights L {lY:F2} R {rY:F2}");
            Object.Destroy(step);
            Assert.Greater(ik.Weight, 0.95f);
            Assert.Less(Mathf.Abs(ik.LeftGap), 0.05f);
            Assert.Less(Mathf.Abs(ik.RightGap), 0.05f);
            Assert.Greater(Mathf.Abs(lY - rY), 0.1f, "one foot up on the step, the other down");
        }

        // ------------------------------------------------------------------ effects

        [UnityTest]
        public IEnumerator Effects_ActionLinesBlurCracks_AndUltimateImpactFrames()
        {
            yield return Arena();
            var dummy = Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).First(d => d.name.StartsWith("Dummy A"));
            Vector3 dir = -dummy.transform.position; dir.y = 0f; dir.Normalize();
            Warp(dummy.transform.position + dir * 1.4f, -dir);
            yield return Frames(3);
            var fx = C.GetComponent<CombatFX>();
            Assert.IsNotNull(fx);
            var gameCam = C.cameraRig.cam;
            var sheet = new Sheet();
            int blurs = ScreenFX.BlurPulses, cracks = SpaceCrack.Spawned;
            bool lines = false;
            float maxBlur = 0f;
            In.HeavyHeld = true;
            yield return Frames(2);
            In.HeavyHeld = false;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                lines |= fx.TrailsEmitting;
                maxBlur = Mathf.Max(maxBlur, ScreenFX.CurrentBlur);
                if (sheet.Count == 0 && ScreenFX.CurrentBlur > 0.02f) sheet.Grab(gameCam);
                if (sheet.Count == 1 && SpaceCrack.Spawned > cracks) { yield return Frames(2); sheet.Grab(gameCam); }
                yield return null;
            }
            yield return new WaitForSeconds(0.4f);
            int impacts = ScreenFX.ImpactFramesPlayed;
            C.AddUltimateGauge(100000f);
            In.TapUltimate();
            bool impactSeen = false;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                if (ScreenFX.CurrentImpact > 0.5f && sheet.Count < 5) { impactSeen = true; sheet.Grab(gameCam); }
                yield return null;
            }
            sheet.Save("combat_fx");
            Metric($"effects: action lines {lines}, blur pulses +{ScreenFX.BlurPulses - blurs} (max {maxBlur:F3}), space cracks +{SpaceCrack.Spawned - cracks}, impact frames +{ScreenFX.ImpactFramesPlayed - impacts} (seen {impactSeen})");
            Assert.IsTrue(lines, "claw action lines while striking");
            Assert.Greater(ScreenFX.BlurPulses, blurs);
            Assert.Greater(SpaceCrack.Spawned, cracks);
            Assert.Greater(ScreenFX.ImpactFramesPlayed, impacts);
            Assert.IsTrue(impactSeen);
        }
    }
}
