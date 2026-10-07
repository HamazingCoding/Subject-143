using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Subject143.Nightfarer.Tests
{
    /// <summary>
    /// Drives the Nightfarer character in the real test scene through scripted input and checks movement,
    /// defence, combat, lock-on, the skill, landing and animation-set switching. Measured values are appended
    /// to Logs/nightfarer_playmode_metrics.txt.
    /// </summary>
    public class NightfarerPlayModeTests
    {
        const string ScenePath = "Assets/Nightfarer/Scenes/NightfarerTestScene.unity";
        const string MetricsPath = "Logs/nightfarer_playmode_metrics.txt";

        NightfarerCharacter C;
        ScriptedInputSource In;
        NightfarerConfig Cfg => C.Config;

        static void Metric(string line)
        {
            Directory.CreateDirectory("Logs");
            File.AppendAllText(MetricsPath, line + "\n");
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(ScenePath);
#endif
            yield return null;
            C = Object.FindFirstObjectByType<NightfarerCharacter>();
            Assert.IsNotNull(C, "NightfarerCharacter missing from test scene");
            In = new ScriptedInputSource();
            C.SetInputSource(In);
            yield return Frames(5);
            C.ApplyProfile(0);
            yield return Frames(2);
        }

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator TapDodge()
        {
            In.DodgeHeld = true;
            yield return null;
            yield return null;
            In.DodgeHeld = false;
            yield return null;
        }

        IEnumerator WaitForState(string prefix, float timeout)
        {
            float t = 0f;
            while (t < timeout && !C.StateName.StartsWith(prefix))
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        void Warp(Vector3 pos, Vector3 facing)
        {
            facing.y = 0f;
            C.Motor.Warp(pos, Quaternion.LookRotation(facing.normalized));
            C.ChangeState(new LocomotionState(C));
            C.cameraRig.SnapBehind();
            C.Vitals.RefillAll();
        }

        TrainingDummy Dummy(string name) => Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).First(d => d.name.StartsWith(name));

        // ------------------------------------------------------------------ movement

        [UnityTest]
        public IEnumerator Run_AcceleratesToRunSpeed_AndStopsQuickly()
        {
            Vector3 start = C.transform.position;
            In.Move = new Vector2(0f, 1f);
            float t = 0f, t90 = -1f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                if (t90 < 0f && C.Motor.PlanarSpeed >= Cfg.runSpeed * 0.9f) t90 = t;
                yield return null;
            }
            float speed = C.Motor.PlanarSpeed;
            float moved = Vector3.Distance(start, C.transform.position);
            In.Move = Vector2.zero;
            float stopT = 0f;
            while (stopT < 1f && C.Motor.PlanarSpeed > 0.05f) { stopT += Time.deltaTime; yield return null; }
            Metric($"run: speed {speed:F2} m/s (target {Cfg.runSpeed}), 90% in {t90:F3}s, moved {moved:F2} m in 1.2s, stop in {stopT:F3}s");
            Assert.AreEqual(Cfg.runSpeed, speed, 0.3f);
            Assert.That(t90, Is.InRange(0f, 0.35f), "acceleration too slow");
            Assert.Less(stopT, 0.35f, "deceleration too slow");
            Assert.AreEqual("Locomotion", C.StateName);
        }

        [UnityTest]
        public IEnumerator Turning_FacesCameraRelativeInputQuickly()
        {
            In.Move = new Vector2(1f, 0f);
            float t = 0f;
            while (t < 1f && Vector3.Dot(C.transform.forward, C.cameraRig.transform.right) < 0.97f) { t += Time.deltaTime; yield return null; }
            Metric($"turn 90 deg: {t:F3}s");
            Assert.Less(t, 0.35f);
            In.Move = new Vector2(-1f, 0f);
            t = 0f;
            while (t < 1f && Vector3.Dot(C.transform.forward, -C.cameraRig.transform.right) < 0.97f) { t += Time.deltaTime; yield return null; }
            Metric($"turn 180 deg (reversal): {t:F3}s");
            Assert.Less(t, 0.5f);
        }

        [UnityTest]
        public IEnumerator Sprint_ThenSurgeSprint_ReachTheirSpeeds()
        {
            In.Move = new Vector2(0f, 1f);
            In.DodgeHeld = true;
            yield return new WaitForSeconds(1.4f);
            float sprint = C.Motor.PlanarSpeed;
            Assert.IsTrue(C.IsSprinting, "should sprint while dodge is held");
            float staminaBefore = C.Vitals.Stamina;
            In.TapSurge();
            yield return new WaitForSeconds(0.7f);
            float surge = C.Motor.PlanarSpeed;
            Metric($"sprint {sprint:F2} m/s (target {Cfg.sprintSpeed}), surge {surge:F2} m/s (target {Cfg.surgeSpeed}), surge entry stamina {staminaBefore - C.Vitals.Stamina:F1}");
            Assert.AreEqual(Cfg.sprintSpeed, sprint, 0.4f);
            Assert.IsTrue(C.IsSurging);
            Assert.AreEqual(Cfg.surgeSpeed, surge, 0.5f);
            In.Move = Vector2.zero;
            In.DodgeHeld = false;
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(C.IsSurging, "surge ends when movement stops");
        }

        [UnityTest]
        public IEnumerator Jump_GoesAirborne_AndLands()
        {
            In.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.5f);
            float y0 = C.transform.position.y;
            In.TapJump();
            float peak = y0, t = 0f;
            bool wasAir = false;
            while (t < 2f)
            {
                t += Time.deltaTime;
                peak = Mathf.Max(peak, C.transform.position.y);
                if (!C.Motor.Grounded) wasAir = true;
                if (wasAir && C.Motor.Grounded) break;
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            Metric($"jump: apex {peak - y0:F2} m (target {Cfg.jumpHeight}), airtime {t:F2}s, states {string.Join(">", C.StateLog.Skip(C.StateLog.Count - 4))}");
            Assert.IsTrue(wasAir);
            Assert.AreEqual(Cfg.jumpHeight, peak - y0, 0.25f);
            Assert.Contains("Jump", C.StateLog);
            Assert.Contains("Landing", C.StateLog);
        }

        [UnityTest]
        public IEnumerator HighFall_EndsInHeroLanding_WithoutDamage()
        {
            // Top of the 9 m ledge in the drop tower, walk off its side.
            Warp(new Vector3(10 + 8 * 2.2f, 9.1f, 40f), Vector3.back);
            yield return Frames(3);
            float hp = C.Vitals.Health;
            In.Move = new Vector2(0f, 1f);
            yield return WaitForState("Hero Landing", 4f);
            Metric($"high fall: states {string.Join(">", C.StateLog.Skip(C.StateLog.Count - 4))}, health {hp}->{C.Vitals.Health}");
            Assert.AreEqual("Hero Landing", C.StateName);
            Assert.AreEqual(hp, C.Vitals.Health, 0.01f);
        }

        // ------------------------------------------------------------------ defence


        [UnityTest]
        public IEnumerator FlashStep_Forward_IsFastEvasiveAndHidesBody()
        {
            In.Move = new Vector2(0f, 1f);
            yield return Frames(2);
            Vector3 start = C.transform.position;
            yield return TapDodge();
            In.Move = Vector2.zero;
            Assert.AreEqual("Flash Step", C.StateName);
            bool sawIFrames = false, rejected = false, hidden = false;
            float arrive = -1f, t = 0f;
            var vfx = C.GetComponent<FlashStepVFX>();
            while (C.StateName == "Flash Step" && t < 1f)
            {
                t += Time.deltaTime;
                if (vfx != null && vfx.IsHidden) hidden = true;
                if (C.IsInvulnerable && !sawIFrames)
                {
                    sawIFrames = true;
                    rejected = !C.ReceiveDamage(new DamageInfo { amount = 50f, direction = Vector3.forward });
                }
                if (arrive < 0f && Vector3.Distance(start, C.transform.position) >= Cfg.stepDistance * 0.9f) arrive = t;
                yield return null;
            }
            float dist = Vector3.Distance(start, C.transform.position);
            Metric($"flash step: {dist:F2} m (cfg {Cfg.stepDistance}), 90% of it in {arrive:F3}s, total state {t:F2}s, body hidden {hidden}, damage rejected {rejected}");
            Assert.IsTrue(rejected, "invulnerable while flashing");
            Assert.IsTrue(hidden, "body vanishes during the flash");
            Assert.AreEqual(Cfg.stepDistance, dist, 0.5f);
            Assert.That(arrive, Is.InRange(0f, 0.2f), "the displacement should be near-instant");
            Assert.AreEqual("Locomotion", C.StateName);
        }

        [UnityTest]
        public IEnumerator FlashStep_LockedOn_SidestepsKeepingTargetInFront()
        {
            var dummy = Dummy("Dummy A");
            yield return FaceDummy(dummy, 4f);
            In.TapLockOn();
            yield return Frames(2);
            Assert.IsTrue(C.IsLocked);
            Vector3 toTarget0 = C.LockTargetDirection().normalized;
            Vector3 start = C.transform.position;
            In.Move = new Vector2(1f, 0f);
            yield return Frames(2);
            yield return TapDodge();
            var step = C.State as FlashStepState;
            Assert.IsNotNull(step);
            string label = step.DirectionLabel;
            In.Move = Vector2.zero;
            yield return new WaitForSeconds(Cfg.stepTravelTime + Cfg.stepRecovery + 0.05f);
            Vector3 moved = C.transform.position - start; moved.y = 0f;
            float lateral = Vector3.Dot(moved, Vector3.Cross(Vector3.up, toTarget0));
            float facing = Vector3.Angle(C.transform.forward, C.LockTargetDirection());
            Metric($"locked sidestep: label {label}, lateral {lateral:F2} m, still facing target within {facing:F1} deg");
            Assert.AreEqual("R", label);
            Assert.Greater(lateral, Cfg.stepDistance * 0.6f);
            Assert.Less(facing, 20f);
        }

        [UnityTest]
        public IEnumerator FlashStep_Chains()
        {
            In.Move = new Vector2(-1f, 0f);
            yield return Frames(2);
            yield return TapDodge();
            yield return new WaitForSeconds(Cfg.stepChainTime + 0.01f);
            yield return TapDodge();
            int steps = C.StateLog.Skip(C.StateLog.Count - 4).Count(n => n == "Flash Step");
            In.Move = Vector2.zero;
            Assert.GreaterOrEqual(steps, 2, "a second step should chain out of the first");
        }


        [UnityTest]
        public IEnumerator FlashStep_WithoutInput_StepsBack()
        {
            Vector3 start = C.transform.position;
            Vector3 fwd = C.transform.forward;
            yield return TapDodge();
            Assert.AreEqual("Flash Step", C.StateName);
            Assert.AreEqual("B", ((FlashStepState)C.State).DirectionLabel);
            yield return new WaitForSeconds(Cfg.stepTravelTime + Cfg.stepRecovery + 0.05f);
            float back = Vector3.Dot(C.transform.position - start, -fwd);
            Metric($"neutral step: {back:F2} m back");
            Assert.Greater(back, Cfg.stepDistance * 0.8f);
        }

        [UnityTest]
        public IEnumerator HitOutsideIFrames_CausesHitReact()
        {
            Assert.IsTrue(C.ReceiveDamage(new DamageInfo { amount = 100f, direction = Vector3.forward }));
            Assert.AreEqual("Hit React", C.StateName);
            yield return new WaitForSeconds(Cfg.hitReactDuration + 0.1f);
            Assert.AreEqual("Locomotion", C.StateName);
        }

        // ------------------------------------------------------------------ combat

        IEnumerator FaceDummy(TrainingDummy d, float distance)
        {
            Vector3 dir = -d.transform.position;
            dir.y = 0f;
            dir.Normalize();
            Vector3 pos = d.transform.position + dir * distance;
            Warp(pos, d.transform.position - pos);
            yield return Frames(3);
        }

        [UnityTest]
        public IEnumerator LightChain_FiveStrikes_FrenzyHitsThreeTimes()
        {
            var dummy = Dummy("Dummy A");
            yield return FaceDummy(dummy, 1.4f);
            int hits0 = dummy.HitCount;
            var chain = C.Weapon.lightChain;
            In.TapLight();
            yield return Frames(2);
            Assert.AreEqual(chain[0].name, C.StateName);
            for (int i = 0; i < chain.Count - 1; i++)
            {
                yield return new WaitForSeconds(chain[i].chainTime * 0.8f);
                In.TapLight();
            }
            yield return new WaitForSeconds(1.8f);
            var log = C.StateLog.Skip(C.StateLog.Count - 8).ToList();
            Metric($"claw chain: {string.Join(">", log)}, dummy hits +{dummy.HitCount - hits0}");
            Assert.Contains(chain[1].name, log);
            Assert.Contains(chain[2].name, log);
            Assert.Contains(chain[3].name, log);
            Assert.Contains(chain[4].name, log);
            Assert.AreEqual(5, chain.Count, "beast-style chain: swipe, counter swipe, twin rake, frenzy, pounce");
            Assert.GreaterOrEqual(dummy.HitCount - hits0, 6, "every strike lands, and the frenzy hits more than once");
            Assert.AreEqual("Locomotion", C.StateName, "recovers to locomotion");
        }

        [UnityTest]
        public IEnumerator HeavyAttack_ChargesWhileHeld_AndHitsHarder()
        {
            var dummy = Dummy("Dummy A");
            yield return FaceDummy(dummy, 1.6f);
            In.HeavyHeld = true;
            float maxCharge = 0f, windStart = -1f, windEnd = 0f;
            var heavy = C.Weapon.heavyChain[0];
            for (float t = 0f; t < 1.0f; t += Time.deltaTime)
            {
                if (C.State is AttackState a)
                {
                    maxCharge = Mathf.Max(maxCharge, a.ChargeRatio);
                    if (a.ChargeRatio > 0f && windStart < 0f) windStart = a.AttackTime;
                    if (a.ChargeRatio > 0f) windEnd = a.AttackTime;
                }
                yield return null;
            }
            In.HeavyHeld = false;
            yield return new WaitForSeconds(1.6f);
            Metric($"heavy: {heavy.name}, max charge {maxCharge:P0}, wind-up crept {windStart:F2}s -> {windEnd:F2}s while held (hit at {heavy.hitStart:F2}s), last hit {C.LastHitDealt:F0} (base {heavy.damage})");
            Assert.That(heavy.name, Does.StartWith("Pouncing Rake"), "original heavy restored");
            Assert.Greater(windEnd - windStart, 0.1f, "keeps winding up slowly while charging (not frozen)");
            Assert.Less(windEnd, heavy.hitStart, "the strike waits for the release");
            Assert.Greater(maxCharge, 0.5f);
            Assert.Greater(C.LastHitDealt, C.Weapon.heavyChain[0].damage * 1.2f);
        }

        [UnityTest]
        public IEnumerator Attack_CanBeDodgeCancelled_AfterCancelPoint()
        {
            In.TapLight();
            yield return Frames(2);
            var attack = (AttackState)C.State;
            while (attack.AttackTime < attack.Data.dodgeCancelTime - 0.15f) yield return null;
            In.Move = new Vector2(0f, -1f);
            yield return TapDodge();
            yield return WaitForState("Flash Step", 0.5f);
            Metric($"step cancel: flash step started at attack t={attack.AttackTime:F2}s (cancel point {attack.Data.dodgeCancelTime}s)");
            Assert.AreEqual("Flash Step", C.StateName);
            Assert.GreaterOrEqual(attack.AttackTime, attack.Data.dodgeCancelTime - 0.02f, "cancel must not happen before the cancel point");
        }

        [UnityTest]
        public IEnumerator SprintAttack_FromSprint()
        {
            In.Move = new Vector2(0f, 1f);
            In.DodgeHeld = true;
            yield return new WaitForSeconds(1.0f);
            In.TapLight();
            yield return Frames(2);
            Assert.AreEqual(C.Weapon.sprintAttack.name, C.StateName);
            In.DodgeHeld = false;
            In.Move = Vector2.zero;
        }


        [UnityTest]
        public IEnumerator StepAttack_FromFlashStep()
        {
            In.Move = new Vector2(0f, 1f);
            yield return Frames(2);
            yield return TapDodge();
            In.Move = Vector2.zero;
            yield return new WaitForSeconds(Cfg.stepAttackWindow.x + 0.02f);
            In.TapLight();
            yield return WaitForState(C.Weapon.rollAttack.name, 0.5f);
            Assert.AreEqual(C.Weapon.rollAttack.name, C.StateName);
        }

        [UnityTest]
        public IEnumerator JumpAttack_InAir()
        {
            In.TapJump();
            yield return Frames(6);
            In.TapLight();
            yield return Frames(2);
            Assert.AreEqual(C.Weapon.jumpAttack.name, C.StateName);
            yield return new WaitForSeconds(2f);
            Assert.AreEqual("Locomotion", C.StateName);
        }


        [UnityTest]
        public IEnumerator Claws_NoWeaponModel_HitVolumesFollowHands()
        {
            yield return Frames(3);
            Assert.IsTrue(C.Weapon.claws, "Subject 143 fights with claws");
            Assert.IsNull(C.WeaponModel, "no weapon model");
            var anim = C.Animator.animator;
            Assert.IsTrue(C.Hitbox.TryGetSegment(0, out var a, out var b), "main claw segment");
            float toLeftHand = Vector3.Distance(a, anim.GetBoneTransform(HumanBodyBones.LeftHand).position);
            Assert.IsTrue(C.Hitbox.TryGetSegment(1, out var c, out var d), "off-hand segment");
            float toRightHand = Vector3.Distance(c, anim.GetBoneTransform(HumanBodyBones.RightHand).position);
            Metric($"claws: main claw segment {Vector3.Distance(a, b):F2} m from {toLeftHand:F2} m off the left hand; off-hand segment {Vector3.Distance(c, d):F2} m");
            Assert.Less(toLeftHand, 0.15f);
            Assert.Less(toRightHand, 0.15f);
        }

        // ------------------------------------------------------------------ lock-on and skill

        [UnityTest]
        public IEnumerator LockOn_StrafesAroundTarget()
        {
            var dummy = Dummy("Dummy A");
            yield return FaceDummy(dummy, 5f);
            In.TapLockOn();
            yield return Frames(2);
            Metric($"lock-on query: {C.LockOn.LastQueryReport}");
            Assert.IsTrue(C.IsLocked);
            In.Move = new Vector2(1f, 0f);
            yield return new WaitForSeconds(1.2f);
            Vector3 toTarget = C.LockTargetDirection().normalized;
            float facing = Vector3.Angle(C.transform.forward, toTarget);
            float sideways = Mathf.Abs(Vector3.Dot(C.Motor.PlanarVelocity.normalized, toTarget));
            Metric($"lock-on strafe: facing error {facing:F1} deg, velocity·toTarget {sideways:F2}, speed {C.Motor.PlanarSpeed:F2}");
            Assert.Less(facing, 15f);
            Assert.Less(sideways, 0.35f, "should circle, not walk into the target");
            In.TapLockOn();
            yield return Frames(2);
            Assert.IsFalse(C.IsLocked);
        }

        [UnityTest]
        public IEnumerator ClawShot_PullsToTarget_ThenFollowUp()
        {
            var dummy = Dummy("Dummy A");
            yield return FaceDummy(dummy, 9f);
            In.TapLockOn();
            yield return Frames(2);
            int hits0 = dummy.HitCount;
            In.TapSkill();
            yield return Frames(2);
            Assert.That(C.StateName, Does.StartWith("Skill"));
            yield return WaitForState("Locomotion", 2f);
            Vector3 gap = dummy.transform.position - C.transform.position;
            gap.y = 0f;
            float dist = gap.magnitude;
            Assert.IsTrue(C.FollowUpReady);
            In.TapLight();
            yield return Frames(2);
            Metric($"claw shot: end distance {dist:F2} m, follow-up state '{C.StateName}', hook hits +{dummy.HitCount - hits0}");
            Assert.Less(dist, 2.6f);
            Assert.AreEqual(((ClawShotAbility)C.Profile.skill).followUpAttack.name, C.StateName);
            Assert.Greater(C.SkillCooldownRemaining, 0f);
        }

        // ------------------------------------------------------------------ animation architecture

        [UnityTest]
        public IEnumerator AnimationSets_SwapClips_WithoutChangingGameplay()
        {
            var setA = C.Animator.ActiveSet;
            var idleA = C.Animator.GetClip("Idle");
            var lightA = C.Animator.GetClip("Light1");
            C.CycleAnimationSet();
            yield return Frames(2);
            var setB = C.Animator.ActiveSet;
            Metric($"anim sets: '{setA.displayName}' Idle={idleA.name} Light1={lightA.name} -> '{setB.displayName}' Idle={C.Animator.GetClip("Idle").name} Light1={C.Animator.GetClip("Light1").name}");
            Assert.AreNotEqual(setA, setB);
            Assert.AreNotEqual(idleA, C.Animator.GetClip("Idle"));
            // Gameplay timing is unchanged: the attack still lasts its data duration.
            In.TapLight();
            yield return Frames(2);
            Assert.AreEqual(C.Weapon.lightChain[0].name, C.StateName);
            yield return new WaitForSeconds(C.Weapon.lightChain[0].duration + 0.15f);
            Assert.AreEqual("Locomotion", C.StateName);
        }


        [UnityTest]
        public IEnumerator Subject143_ChildScale()
        {
            yield return Frames(3);
            var cc = C.Motor.Controller;
            var skins = C.GetComponentsInChildren<SkinnedMeshRenderer>();
            var body = System.Array.Find(skins, r => r.name.Contains("Body")) ?? skins[0];   // not the coat
            float height = body.bounds.size.y;
            Metric($"child scale: model height {height:F2} m, capsule {cc.height:F2} x r{cc.radius:F2}, camera pivot {C.cameraRig.pivotHeight:F2}, evade {Cfg.evadeStyle}");
            Assert.AreEqual("Subject 143", C.Profile.displayName);
            Assert.That(height, Is.InRange(1.1f, 1.45f), "child-proportioned body");
            Assert.That(cc.height, Is.InRange(1.0f, 1.3f));
            Assert.Less(C.cameraRig.pivotHeight, 1.2f);
            Assert.AreEqual(EvadeStyle.FlashStep, Cfg.evadeStyle);
        }

        // ------------------------------------------------------------------ real device bindings

        [UnityTest]
        public IEnumerator DeviceInput_KeyboardDrivesTheCharacter()
        {
            // Batch-mode play has no focused Game view, so the Input System would drop keyboard events.
            // Use a temporary copy of the settings (the project's settings asset is never modified).
            var originalSettings = InputSystem.settings;
            var testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            var kb = InputSystem.AddDevice<Keyboard>("NightfarerTestKeyboard");
            try
            {
                var device = C.inputSource as INightfarerInputSource;
                Assert.IsNotNull(device, "scene must wire a DeviceInputSource");
                C.SetInputSource(device);

                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.W));
                yield return new WaitForSeconds(0.6f);
                float speed = C.Motor.PlanarSpeed;

                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.W, Key.Space));   // tap Space = roll
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.W));
                yield return WaitForState("Flash Step", 0.3f);
                string afterSpace = C.StateName;

                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return new WaitForSeconds(1.0f);
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.F));               // F = jump
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return WaitForState("Jump", 0.3f);
                string afterF = C.StateName;
                yield return new WaitForSeconds(1.0f);

                var setBefore = C.Animator.ActiveSet;
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.F2));              // F2 = cycle animation set
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return null;
                Metric($"device input: W speed {speed:F2}, Space -> {afterSpace}, F -> {afterF}, F2 set '{setBefore.displayName}' -> '{C.Animator.ActiveSet.displayName}'");
                Assert.Greater(speed, Cfg.runSpeed * 0.8f);
                Assert.AreEqual("Flash Step", afterSpace);
                Assert.AreEqual("Jump", afterF);
                Assert.AreNotEqual(setBefore, C.Animator.ActiveSet);
            }
            finally
            {
                InputSystem.RemoveDevice(kb);
                InputSystem.settings = originalSettings;
                Object.Destroy(testSettings);
            }
        }

        // ------------------------------------------------------------------ visual capture (manual review)

        [UnityTest]
        public IEnumerator Capture_GameplayFrames()
        {
            var cam = C.cameraRig.cam;
            var rt = new RenderTexture(640, 360, 24);
            var shots = new List<Texture2D>();
            void Shot()
            {
                var prev = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prev;
                RenderTexture.active = rt;
                var tex = new Texture2D(640, 360, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                shots.Add(tex);
            }

            var dummy = Dummy("Dummy A");
            yield return FaceDummy(dummy, 1.8f);
            yield return Frames(10);
            Shot();                                   // idle stance
            In.TapLockOn();
            In.TapLight();
            yield return new WaitForSeconds(0.25f); Shot();   // windup
            yield return new WaitForSeconds(0.15f); Shot();   // strike
            In.HeavyHeld = true;
            yield return new WaitForSeconds(0.9f); Shot();    // charging heavy
            In.HeavyHeld = false;
            yield return new WaitForSeconds(0.35f); Shot();   // heavy release
            yield return new WaitForSeconds(1.2f);
            In.Move = new Vector2(1f, 0f);
            yield return Frames(2);
            yield return TapDodge();
            yield return new WaitForSeconds(0.25f); Shot();   // locked roll
            In.TapLockOn();
            In.Move = new Vector2(0f, 1f);
            In.DodgeHeld = true;
            yield return new WaitForSeconds(1.0f);
            In.TapSurge();
            yield return new WaitForSeconds(0.6f); Shot();    // surge sprint
            In.DodgeHeld = false;
            In.TapJump();
            yield return new WaitForSeconds(0.3f); Shot();    // surge jump

            Directory.CreateDirectory("Logs/NightfarerCaptures");
            var sheet = new Texture2D(640 * 2, 360 * 4, TextureFormat.RGB24, false);
            for (int i = 0; i < shots.Count && i < 8; i++)
                sheet.SetPixels((i % 2) * 640, (3 - i / 2) * 360, 640, 360, shots[i].GetPixels());
            sheet.Apply();
            File.WriteAllBytes("Logs/NightfarerCaptures/play_" + C.Animator.ActiveSet.name + ".png", sheet.EncodeToPNG());
            rt.Release();
            Assert.AreEqual(8, shots.Count);
        }
    }

    /// <summary>The project's existing scene must keep working untouched.</summary>
    public class ExistingProjectSmokeTests
    {
        [UnityTest]
        public IEnumerator SampleScene_StillRunsWithoutErrors()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return null;
#endif
            // Any Debug.LogError / exception during these frames fails the test (Test Framework default).
            for (int i = 0; i < 120; i++) yield return null;
            Assert.AreEqual("SampleScene", SceneManager.GetActiveScene().name);
        }
    }
}
