using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Subject143.Nightfarer.Tests
{
    /// <summary>Climbing, spirit springs, flasks, the ultimate, weapon IK, UI and the integrated game scenes.</summary>
    public class NightfarerFeatureTests
    {
        const string ArenaPath = "Assets/Nightfarer/Scenes/NightfarerTestScene.unity";
        NightfarerCharacter C;
        ScriptedInputSource In;

        static IEnumerator Load(string path)
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(path);
#endif
            yield return null;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static void Metric(string line)
        {
            Directory.CreateDirectory("Logs");
            File.AppendAllText("Logs/nightfarer_playmode_metrics.txt", line + "\n");
        }

        IEnumerator Arena()
        {
            yield return Load(ArenaPath);
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

        // ------------------------------------------------------------------ movement features

        [UnityTest]
        public IEnumerator Mantle_ClimbsOntoTwoMetreWall()
        {
            yield return Arena();
            Warp(new Vector3(-12.5f, 0.05f, 22.6f), Vector3.forward);   // 2 m wall, front face at z = 24.5
            yield return Frames(3);
            In.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.25f);
            In.TapJump();
            yield return WaitForState("Mantle", 1.2f);
            Assert.AreEqual("Mantle", C.StateName, "should grab the ledge");
            yield return WaitForState("Locomotion", 1.5f);
            In.Move = Vector2.zero;
            yield return new WaitForSeconds(0.3f);
            Metric($"mantle: ended at y={C.transform.position.y:F2} (wall top 2.0), states {string.Join(">", C.StateLog.Skip(C.StateLog.Count - 4))}");
            Assert.AreEqual(2.0f, C.transform.position.y, 0.2f);
        }

        [UnityTest]
        public IEnumerator SpiritSpring_LaunchesHigh_LandsSafely()
        {
            yield return Arena();
            Warp(new Vector3(4f, 0.05f, 24f), Vector3.forward);
            yield return new WaitForSeconds(1.2f);   // spring refreshes its character list once per second
            float hp = C.Vitals.Health;
            In.TapJump();
            yield return Frames(3);
            Assert.AreEqual("Spiritstream", C.StateName);
            float peak = 0f;
            for (float t = 0f; t < 6f && !(C.Motor.Grounded && t > 0.5f); t += Time.deltaTime)
            {
                peak = Mathf.Max(peak, C.transform.position.y);
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
            Metric($"spirit spring: peak {peak:F1} m, health {hp}->{C.Vitals.Health}, states {string.Join(">", C.StateLog.Skip(C.StateLog.Count - 3))}");
            Assert.Greater(peak, 8f);
            Assert.AreEqual(hp, C.Vitals.Health, 0.01f, "no fall damage");
        }

        [UnityTest]
        public IEnumerator Flask_HealsWhileWalking_AndUsesACharge()
        {
            yield return Arena();
            C.ReceiveDamage(new DamageInfo { amount = 500f, direction = Vector3.forward });
            yield return new WaitForSeconds(C.Config.hitReactDuration + 0.1f);
            float before = C.Vitals.Health;
            int charges = C.FlaskCharges;
            In.Move = new Vector2(0f, 1f);
            In.TapFlask();
            yield return Frames(2);
            Assert.AreEqual("Flask", C.StateName);
            float moveSpeed = 0f;
            for (float t = 0f; t < C.Config.flaskDuration + 0.1f; t += Time.deltaTime)
            {
                if (C.StateName == "Flask") moveSpeed = Mathf.Max(moveSpeed, C.Motor.PlanarSpeed);
                yield return null;
            }
            Metric($"flask: health {before:F0}->{C.Vitals.Health:F0}, charges {charges}->{C.FlaskCharges}, move speed while drinking {moveSpeed:F2}");
            Assert.Greater(C.Vitals.Health, before + 300f);
            Assert.AreEqual(charges - 1, C.FlaskCharges);
            Assert.Greater(moveSpeed, 0.5f, "can walk while drinking");
            Assert.Less(moveSpeed, C.Config.runSpeed * 0.7f);
        }

        [UnityTest]
        public IEnumerator Ultimate_NeedsGauge_ThenBlastsTargets()
        {
            yield return Arena();
            var dummy = Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).First(d => d.name.StartsWith("Dummy A"));
            Vector3 dir = -dummy.transform.position; dir.y = 0f; dir.Normalize();
            Warp(dummy.transform.position + dir * 3f, -dir);
            yield return Frames(3);
            In.TapUltimate();
            yield return Frames(3);
            Assert.AreEqual("Locomotion", C.StateName, "gauge empty: no ultimate");
            C.AddUltimateGauge(100000f);
            Assert.IsTrue(C.UltimateReady);
            int hits = dummy.HitCount;
            In.TapUltimate();
            yield return Frames(2);
            Assert.That(C.StateName, Does.StartWith("Skill:"));
            yield return WaitForState("Locomotion", 3f);
            Metric($"ultimate: dummy hits +{dummy.HitCount - hits}, last damage {dummy.LastDamage}, gauge now {C.UltimateGauge:F2}");
            Assert.Greater(dummy.HitCount, hits);
            Assert.AreEqual(0f, C.UltimateGauge, 0.01f);
        }

        [UnityTest]
        public IEnumerator UltimateGauge_FillsFromDealtDamage()
        {
            yield return Arena();
            var dummy = Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).First(d => d.name.StartsWith("Dummy A"));
            Vector3 dir = -dummy.transform.position; dir.y = 0f; dir.Normalize();
            Warp(dummy.transform.position + dir * 1.4f, -dir);
            yield return Frames(3);
            In.TapLight();
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(C.UltimateGauge, 0.03f);
        }

        // ------------------------------------------------------------------ weapon layer


        [UnityTest]
        public IEnumerator ClawIK_ReadyPose_AndClawArc()
        {
            yield return Arena();
            yield return new WaitForSeconds(0.8f);
            // At rest the arms hang at his sides (claw well below the shoulder, not held up in front).
            var anim = C.Animator.animator;
            var sh = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var hand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            var offSh = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var offHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 clawRest = C.transform.InverseTransformPoint(hand.position) - C.transform.InverseTransformPoint(sh.position);
            Vector3 offRest = C.transform.InverseTransformPoint(offHand.position) - C.transform.InverseTransformPoint(offSh.position);
            {
                var camGo = new GameObject("IdleCam");
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 35f;
                var rt = new RenderTexture(480, 400, 24);
                cam.targetTexture = rt;
                var sheet = new Texture2D(960, 400, TextureFormat.RGB24, false);
                for (int i = 0; i < 2; i++)
                {
                    camGo.transform.position = C.transform.position + (i == 0 ? C.transform.forward * 2.6f : C.transform.right * -2.6f) + Vector3.up * 0.7f;
                    camGo.transform.LookAt(C.transform.position + Vector3.up * 0.6f);
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(480, 400, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 480, 400), 0, 0);
                    tex.Apply();
                    sheet.SetPixels(i * 480, 0, 480, 400, tex.GetPixels());
                }
                RenderTexture.active = null;
                sheet.Apply();
                Directory.CreateDirectory("Logs/NightfarerCaptures");
                File.WriteAllBytes("Logs/NightfarerCaptures/idle_arms.png", sheet.EncodeToPNG());
                Object.Destroy(camGo);
                rt.Release();
            }
            yield return new WaitForSeconds(0.3f);   // let the frame time settle after the renders
            In.TapLight();
            float minX = 9f, maxX = -9f, maxForward = -9f;
            for (float t = 0f; t < C.Weapon.lightChain[0].duration; t += Time.deltaTime)
            {
                if (C.Hitbox.TryGetSegment(0, out _, out var tip))
                {
                    Vector3 local = C.transform.InverseTransformPoint(tip);
                    minX = Mathf.Min(minX, local.x); maxX = Mathf.Max(maxX, local.x); maxForward = Mathf.Max(maxForward, local.z);
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.4f);
            float readyWeight = C.weaponIK.PrimaryClawWeight;
            bool stance = C.InCombatStance;
            Metric($"claw IK: idle claw hand {-clawRest.y:F2} m below the shoulder ({clawRest.z:F2} m forward), off hand {-offRest.y:F2} m below; after attacking: combat stance {stance}, ready weight {readyWeight:F2}; {C.Weapon.lightChain[0].name} tip sweep x {minX:F2}..{maxX:F2} m, forward reach {maxForward:F2} m");
            Assert.Greater(-clawRest.y, 0.25f, "claw arm hangs down at rest");
            Assert.Less(clawRest.z, 0.15f, "not held out in front at rest");
            Assert.Greater(-offRest.y, 0.25f, "off arm hangs down at rest");
            Assert.IsTrue(stance, "claws come up after attacking");
            Assert.Greater(maxX - minX, 0.4f, "the rend sweeps across the body");
            Assert.Greater(maxForward, 0.35f, "the claw reaches in front");
        }

        [UnityTest]
        public IEnumerator ClawArm_SwingsWithTheGait_WhenRunning()
        {
            yield return Arena();
            Warp(new Vector3(-12f, 0.05f, 0f), Vector3.forward);   // sprint lane
            var anim = C.Animator.animator;
            var claw = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            var shoulder = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var camGo = new GameObject("SideCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 35f;
            var rt = new RenderTexture(480, 400, 24);
            var shots = new List<Texture2D>();
            In.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.6f);
            float runMin = 9f, runMax = -9f;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                float z = C.transform.InverseTransformPoint(claw.position).z - C.transform.InverseTransformPoint(shoulder.position).z;
                runMin = Mathf.Min(runMin, z); runMax = Mathf.Max(runMax, z);
                if (shots.Count < 6 && t > shots.Count * 0.07f)
                {
                    camGo.transform.position = C.transform.position + C.transform.right * -3.2f + Vector3.up * 0.8f;
                    camGo.transform.LookAt(C.transform.position + Vector3.up * 0.65f);
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(480, 400, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 480, 400), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    shots.Add(tex);
                }
                yield return null;
            }
            In.DodgeHeld = true;
            yield return new WaitForSeconds(1.0f);
            float sprintMin = 9f, sprintMax = -9f;
            for (float t = 0f; t < 1.0f; t += Time.deltaTime)
            {
                float z = C.transform.InverseTransformPoint(claw.position).z - C.transform.InverseTransformPoint(shoulder.position).z;
                sprintMin = Mathf.Min(sprintMin, z); sprintMax = Mathf.Max(sprintMax, z);
                yield return null;
            }
            In.DodgeHeld = false;
            In.Move = Vector2.zero;
            var sheet = new Texture2D(480 * 3, 400 * 2, TextureFormat.RGB24, false);
            for (int i = 0; i < shots.Count; i++) sheet.SetPixels((i % 3) * 480, (1 - i / 3) * 400, 480, 400, shots[i].GetPixels());
            sheet.Apply();
            Directory.CreateDirectory("Logs/NightfarerCaptures");
            File.WriteAllBytes("Logs/NightfarerCaptures/claw_arm_run.png", sheet.EncodeToPNG());
            Object.Destroy(camGo);
            rt.Release();
            Metric($"claw arm gait: run swing {runMin:F2}..{runMax:F2} m ({runMax - runMin:F2}), sprint swing {sprintMin:F2}..{sprintMax:F2} m ({sprintMax - sprintMin:F2})");
            Assert.Greater(runMax - runMin, 0.15f, "the claw arm should swing while running");
            Assert.Greater(sprintMax - sprintMin, 0.1f, "and while sprinting");
        }

        [UnityTest]
        public IEnumerator Coat_ChainCloth_TrailsWithoutTearing()
        {
            yield return Arena();
            Warp(new Vector3(-12f, 0.05f, 0f), Vector3.forward);
            var cloth = C.GetComponentInChildren<ChainCloth>();
            Assert.IsNotNull(cloth, "the coat should be driven by chain cloth");
            Assert.GreaterOrEqual(cloth.ChainCount, 8, "coat chains found");
            Assert.IsNull(C.GetComponentInChildren<Cloth>(), "Unity per-vertex cloth is no longer used");
            var tips = C.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("CoatChain_")).GroupBy(t => t.name.Substring(0, t.name.LastIndexOf('_')))
                .Select(g => g.OrderBy(t => t.name).Last()).ToList();
            float TipZ() => tips.Average(t => C.transform.InverseTransformPoint(t.position + t.up * 0.0f).z);

            var camGo = new GameObject("CoatCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 35f;
            var rt = new RenderTexture(480, 400, 24);
            cam.targetTexture = rt;
            var shots = new List<Texture2D>();
            float maxStretch = 0f;
            void Shot(Vector3 offsetDir, float dist, float height)
            {
                camGo.transform.position = C.transform.position + offsetDir * dist + Vector3.up * height;
                camGo.transform.LookAt(C.transform.position + Vector3.up * 0.65f);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(480, 400, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 480, 400), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                shots.Add(tex);
            }
            yield return new WaitForSeconds(0.8f);
            float idle = TipZ();
            Shot(C.transform.forward, 2.0f, 0.85f);
            Shot(Quaternion.Euler(0f, 45f, 0f) * C.transform.forward, 2.0f, 0.85f);
            Shot(-C.transform.forward, 2.0f, 0.85f);

            In.Move = new Vector2(0f, 1f);
            In.DodgeHeld = true;
            for (float t = 0f; t < 1.3f; t += Time.deltaTime)
            {
                maxStretch = Mathf.Max(maxStretch, cloth.MaxStretch);
                if (t > 0.9f && shots.Count < 4) Shot(-C.transform.right, 3.0f, 0.8f);
                yield return null;
            }
            float running = TipZ();
            In.DodgeHeld = false;
            In.Move = Vector2.zero;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                maxStretch = Mathf.Max(maxStretch, cloth.MaxStretch);
                if (t > 0.15f && shots.Count < 5) Shot(-C.transform.right, 3.0f, 0.8f);
                yield return null;
            }
            // Flash step: the coat must come along without whipping or tearing.
            In.Move = new Vector2(1f, 0f);
            In.DodgeHeld = true; yield return null; yield return null; In.DodgeHeld = false;
            float maxTipDistance = 0f;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                maxStretch = Mathf.Max(maxStretch, cloth.MaxStretch);
                maxTipDistance = Mathf.Max(maxTipDistance, tips.Max(tp => Vector3.Distance(tp.position, C.transform.position + Vector3.up * 0.5f)));
                if (t > 0.1f && shots.Count < 6) Shot(-C.transform.forward, 3.0f, 0.9f);
                yield return null;
            }
            In.Move = Vector2.zero;
            var sheet = new Texture2D(480 * 3, 400 * 2, TextureFormat.RGB24, false);
            for (int i = 0; i < shots.Count && i < 6; i++) sheet.SetPixels((i % 3) * 480, (1 - i / 3) * 400, 480, 400, shots[i].GetPixels());
            sheet.Apply();
            File.WriteAllBytes("Logs/NightfarerCaptures/coat_chaincloth.png", sheet.EncodeToPNG());
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(camGo);
            Metric($"coat chain cloth: {cloth.ChainCount} chains, hem tips z idle {idle:F2} -> sprinting {running:F2} (trail {idle - running:F2} m), max segment stretch {maxStretch:F3}, max tip distance after flash step {maxTipDistance:F2} m");
            Assert.Greater(idle - running, 0.08f, "the coat should stream behind when sprinting");
            Assert.Less(maxStretch, 1.05f, "segments must not stretch (no shards)");
            Assert.Less(maxTipDistance, 1.3f, "the coat stays with him through a flash step");
        }

        [UnityTest]
        public IEnumerator ClawShot_LongRange_ArmThrownAtTarget()
        {
            yield return Arena();
            var dummy = Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).First(d => d.name.StartsWith("Dummy A"));
            Vector3 dir = -dummy.transform.position; dir.y = 0f; dir.Normalize();
            Warp(dummy.transform.position + dir * 20f, -dir);   // beyond the old 12-16 m range
            yield return Frames(4);
            var anim = C.Animator.animator;
            var shoulder = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var elbow = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var hand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            float armLength = Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, hand.position);
            In.TapSkill();
            float maxExtension = 0f, aimError = 180f;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                maxExtension = Mathf.Max(maxExtension, Vector3.Distance(shoulder.position, hand.position) / armLength);
                aimError = Mathf.Min(aimError, Vector3.Angle(hand.position - shoulder.position, dummy.transform.position + Vector3.up * 1.35f - shoulder.position));
                yield return null;
            }
            for (float t = 0f; t < 2.5f && !C.StateName.StartsWith("Locomotion"); t += Time.deltaTime) yield return null;
            Vector3 gap = dummy.transform.position - C.transform.position; gap.y = 0f;
            Metric($"claw shot long range: arm extension {maxExtension:P0} of arm length, aim error {aimError:F1} deg, end distance {gap.magnitude:F2} m from 20 m");
            Assert.Greater(maxExtension, 0.85f, "arm thrown out straight");
            Assert.Less(aimError, 20f, "arm points at the target");
            Assert.Less(gap.magnitude, 2.6f, "pulled all the way in from 20 m");
        }

        // ------------------------------------------------------------------ UI

        [UnityTest]
        public IEnumerator HUD_TracksVitalsLockOnAndPause()
        {
            yield return Arena();
            var ui = Object.FindFirstObjectByType<NightfarerUI>();
            Assert.IsNotNull(ui, "rig should include the HUD");
            C.ReceiveDamage(new DamageInfo { amount = 300f, direction = Vector3.forward });
            yield return Frames(3);
            Assert.Less(ui.hpFill.fillAmount, 0.8f);
            Assert.Greater(ui.hpTrail.fillAmount, ui.hpFill.fillAmount, "damage trail lags behind");

            var dummy = Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).First(d => d.name.StartsWith("Dummy A"));
            Vector3 dir = -dummy.transform.position; dir.y = 0f; dir.Normalize();
            Warp(dummy.transform.position + dir * 5f, -dir);
            yield return Frames(3);
            In.TapLockOn();
            yield return Frames(3);
            Assert.IsTrue(ui.reticle.gameObject.activeSelf, "lock-on reticle");
            Assert.IsTrue(ui.targetBar.activeSelf, "target health bar");

            ui.Pause();
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(C.InputBlocked);
            Assert.IsTrue(ui.pauseRoot.activeSelf);
            ui.Resume();
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(C.InputBlocked);
            Metric($"HUD: hp fill {ui.hpFill.fillAmount:F2}, trail {ui.hpTrail.fillAmount:F2}, reticle + target bar shown, pause/resume ok");
        }

        [UnityTest]
        public IEnumerator Capture_HudAndCombat()
        {
            yield return Arena();
            var ui = Object.FindFirstObjectByType<NightfarerUI>();
            var cam = C.cameraRig.cam;
            ui.canvas.renderMode = RenderMode.ScreenSpaceCamera;   // so an offscreen render includes the HUD
            ui.canvas.worldCamera = cam;
            ui.canvas.planeDistance = 0.5f;
            var rt = new RenderTexture(960, 540, 24);
            var shots = new List<Texture2D>();
            void Shot()
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.active = rt;
                var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                shots.Add(tex);
            }

            var dummy = Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).First(d => d.name.StartsWith("Dummy A"));
            Vector3 dir = -dummy.transform.position; dir.y = 0f; dir.Normalize();
            Warp(dummy.transform.position + dir * 1.7f, -dir);
            yield return new WaitForSeconds(0.6f);
            Shot();                                               // guard + HUD
            In.TapLockOn();
            In.TapLight();
            yield return new WaitForSeconds(0.24f); Shot();       // wind-up
            yield return new WaitForSeconds(0.18f); Shot();       // strike
            yield return new WaitForSeconds(0.75f);
            In.HeavyHeld = true;
            yield return new WaitForSeconds(0.7f); Shot();        // charging
            In.HeavyHeld = false;
            yield return new WaitForSeconds(0.32f); Shot();       // slam
            yield return new WaitForSeconds(1.3f);
            C.AddUltimateGauge(100000f);
            In.TapUltimate();
            yield return new WaitForSeconds(0.72f); Shot();       // ultimate blast
            yield return new WaitForSeconds(1.2f);
            In.TapLockOn();
            In.Move = new Vector2(0f, 1f);
            In.DodgeHeld = true;
            yield return new WaitForSeconds(0.9f); Shot();        // sprint carry
            In.TapSurge();
            yield return new WaitForSeconds(0.6f); Shot();        // surge

            Directory.CreateDirectory("Logs/NightfarerCaptures");
            var sheet = new Texture2D(960 * 2, 540 * 4, TextureFormat.RGB24, false);
            for (int i = 0; i < shots.Count && i < 8; i++)
                sheet.SetPixels((i % 2) * 960, (3 - i / 2) * 540, 960, 540, shots[i].GetPixels());
            sheet.Apply();
            File.WriteAllBytes("Logs/NightfarerCaptures/play_hud_combat.png", sheet.EncodeToPNG());
            rt.Release();
            Assert.AreEqual(8, shots.Count);
        }

        // ------------------------------------------------------------------ integrated scenes

        [UnityTest]
        public IEnumerator GameScene_UsesNightfarerRig_AndPlays()
        {
            yield return Load("Assets/Scenes/SampleScene.unity");
            yield return Frames(5);
            C = Object.FindFirstObjectByType<NightfarerCharacter>();
            Assert.IsNotNull(C, "SampleScene should contain the Nightfarer rig");
            Assert.IsNull(GameObject.Find("Player"), "old player should be disabled");
            Assert.IsNotNull(Object.FindFirstObjectByType<NightfarerUI>());
            var renderer = C.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(renderer.sharedMaterial.name, Does.Contain("Subject143"), "Subject 143 with its texture");
            Assert.IsNull(GameObject.Find("Subject_143_True"), "the static placeholder is replaced by the playable rig");
            In = new ScriptedInputSource();
            C.SetInputSource(In);
            Vector3 start = C.transform.position;
            In.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(1f);
            float moved = Vector3.Distance(start, C.transform.position);
            Metric($"game scene: moved {moved:F2} m in 1 s, grounded {C.Motor.Grounded}, state {C.StateName}");
            Assert.Greater(moved, 2.5f);
        }

        [UnityTest]
        public IEnumerator Capture_GameSceneAndMenu()
        {
            var shots = new List<Texture2D>();
            IEnumerator Grab(Camera cam, Canvas canvas)
            {
                yield return null;   // (WaitForEndOfFrame never fires in batch mode)
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 0.5f;
                }
                var rt = new RenderTexture(960, 540, 24);
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.active = rt;
                var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                rt.Release();
                shots.Add(tex);
            }

            yield return Load("Assets/Nightfarer/Scenes/MainMenu.unity");
            yield return new WaitForSeconds(0.5f);
            yield return Grab(Camera.main, Object.FindFirstObjectByType<MainMenuUI>().GetComponent<Canvas>());

            yield return Load("Assets/Scenes/SampleScene.unity");
            C = Object.FindFirstObjectByType<NightfarerCharacter>();
            In = new ScriptedInputSource();
            C.SetInputSource(In);
            yield return new WaitForSeconds(0.6f);
            var ui = Object.FindFirstObjectByType<NightfarerUI>();
            yield return Grab(C.cameraRig.cam, ui.canvas);
            In.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.8f);
            In.Move = Vector2.zero;
            ui.Pause();
            yield return null;
            yield return Grab(C.cameraRig.cam, ui.canvas);
            ui.Resume();

            Directory.CreateDirectory("Logs/NightfarerCaptures");
            var sheet = new Texture2D(960, 540 * shots.Count, TextureFormat.RGB24, false);
            for (int i = 0; i < shots.Count; i++) sheet.SetPixels(0, (shots.Count - 1 - i) * 540, 960, 540, shots[i].GetPixels());
            sheet.Apply();
            File.WriteAllBytes("Logs/NightfarerCaptures/play_menu_game_pause.png", sheet.EncodeToPNG());
            Assert.AreEqual(3, shots.Count);
        }

        [UnityTest]
        public IEnumerator MainMenu_StartLoadsGameScene()
        {
            yield return Load("Assets/Nightfarer/Scenes/MainMenu.unity");
            yield return Frames(3);
            var menu = Object.FindFirstObjectByType<MainMenuUI>();
            Assert.IsNotNull(menu);
            Assert.IsTrue(menu.menuPanel.activeSelf);
            menu.startButton.onClick.Invoke();
            for (int i = 0; i < 60 && SceneManager.GetActiveScene().name != "SampleScene"; i++) yield return null;
            Assert.AreEqual("SampleScene", SceneManager.GetActiveScene().name);
        }
    }
}
