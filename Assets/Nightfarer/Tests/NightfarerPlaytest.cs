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
    /// Scripted playtest that measures movement feel against the Subject 143 design pillars (momentum, commitment,
    /// air control, landing, body language, camera). It asserts nothing about feel; it records numbers to
    /// Logs/nightfarer_playtest.txt and captures to Logs/NightfarerCaptures for review.
    /// </summary>
    public class NightfarerPlaytest
    {
        const string ArenaPath = "Assets/Nightfarer/Scenes/NightfarerTestScene.unity";
        const string Out = "Logs/nightfarer_playtest.txt";
        NightfarerCharacter C;
        ScriptedInputSource In;
        NightfarerConfig Cfg => C.Config;

        static void Log(string line)
        {
            Directory.CreateDirectory("Logs");
            File.AppendAllText(Out, line + "\n");
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
            for (int i = 0; i < 5; i++) yield return null;
            C.ApplyProfile(0);
            yield return null;
        }

        void Warp(Vector3 pos)
        {
            C.Motor.Warp(pos, Quaternion.LookRotation(Vector3.forward));
            C.ChangeState(new LocomotionState(C));
            C.cameraRig.SnapBehind();
            C.Vitals.RefillAll();
            In.Move = Vector2.zero; In.DodgeHeld = false; In.JumpHeld = false;
        }

        IEnumerator Settle(float s = 0.6f) { yield return new WaitForSeconds(s); }

        float ChestLean()
        {
            var a = C.Animator.animator;
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            var head = a.GetBoneTransform(HumanBodyBones.Head);
            Vector3 d = C.transform.InverseTransformDirection(head.position - hips.position);
            return Mathf.Atan2(d.z, d.y) * Mathf.Rad2Deg;   // + = leaning forward
        }

        float ChestRoll()
        {
            var a = C.Animator.animator;
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            var head = a.GetBoneTransform(HumanBodyBones.Head);
            Vector3 d = C.transform.InverseTransformDirection(head.position - hips.position);
            return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;   // + = leaning right
        }

        // Reach a speed tier; returns time to 90% and the steady lean.
        IEnumerator Accelerate(string tier, float target, System.Action<float, float> result)
        {
            In.Move = new Vector2(0f, tier == "walk" ? 0.4f : 1f);
            In.DodgeHeld = tier == "sprint" || tier == "surge";
            float t90 = -1f, t = 0f;
            bool surged = false;
            for (; t < 3f; t += Time.deltaTime)
            {
                if (tier == "surge" && !surged && t > 0.4f) { In.TapSurge(); surged = true; }
                if (t90 < 0f && C.Motor.PlanarSpeed >= target * 0.9f) t90 = t;
                yield return null;
            }
            result(t90, ChestLean());
        }

        IEnumerator Stop(System.Action<float, float> result)
        {
            Vector3 p0 = C.transform.position;
            In.Move = Vector2.zero; In.DodgeHeld = false;
            float t = 0f;
            for (; t < 3f && C.Motor.PlanarSpeed > 0.1f; t += Time.deltaTime) yield return null;
            Vector3 d = C.transform.position - p0; d.y = 0f;
            result(t, d.magnitude);
        }

        [UnityTest]
        public IEnumerator Playtest_MovementFeel()
        {
            File.WriteAllText(Out, "");
            yield return Arena();
            Log($"profile {C.Profile.displayName}, config walk {Cfg.walkSpeed} run {Cfg.runSpeed} sprint {Cfg.sprintSpeed} surge {Cfg.surgeSpeed}, accel {Cfg.acceleration} decel {Cfg.deceleration} sprintAccel {Cfg.sprintAcceleration}, turn {Cfg.turnSpeed} sprintTurn {Cfg.sprintTurnSpeed}, air accel {Cfg.airAcceleration} air turn {Cfg.airTurnSpeed}, gravity {Cfg.gravity}");

            // 1. Acceleration, stopping and lean per tier.
            foreach (var (tier, target) in new[] { ("walk", Cfg.walkSpeed), ("run", Cfg.runSpeed), ("sprint", Cfg.sprintSpeed), ("surge", Cfg.surgeSpeed) })
            {
                Warp(new Vector3(-40f, 0.05f, -40f));
                yield return Settle();
                float t90 = 0, lean = 0, stopT = 0, stopD = 0, fov = 0;
                yield return Accelerate(tier, target, (a, b) => { t90 = a; lean = b; });
                fov = C.cameraRig.cam.fieldOfView;
                float reached = C.Motor.PlanarSpeed;
                yield return Stop((a, b) => { stopT = a; stopD = b; });
                Log($"[{tier}] speed {reached:F2} (target {target}), 90% in {t90:F2}s, chest lean {lean:F1} deg, camera FOV {fov:F1}, stop in {stopT:F2}s over {stopD:F2} m, states {string.Join(">", C.StateLog.Skip(C.StateLog.Count - 3))}");
            }

            // 2. Directional commitment: 90-degree turn and 180-degree reversal at sprint and surge.
            foreach (string tier in new[] { "run", "sprint", "surge" })
            {
                foreach (float angle in new[] { 90f, 180f })
                {
                    Warp(new Vector3(-40f, 0.05f, -40f));
                    yield return Settle(0.4f);
                    float target = tier == "run" ? Cfg.runSpeed : tier == "sprint" ? Cfg.sprintSpeed : Cfg.surgeSpeed;
                    yield return Accelerate(tier, target, (a, b) => { });
                    Vector3 v0 = C.Motor.PlanarVelocity.normalized;
                    Vector3 start = C.transform.position;
                    In.Move = angle >= 180f ? new Vector2(0f, -1f) : new Vector2(1f, 0f);
                    float tTurn = -1f, minSpeed = 99f, maxRoll = 0f, overshoot = 0f;
                    for (float t = 0f; t < 2.5f; t += Time.deltaTime)
                    {
                        Vector3 v = C.Motor.PlanarVelocity;
                        minSpeed = Mathf.Min(minSpeed, v.magnitude);
                        maxRoll = Mathf.Max(maxRoll, Mathf.Abs(ChestRoll()));
                        overshoot = Mathf.Max(overshoot, Vector3.Dot(C.transform.position - start, v0));
                        if (tTurn < 0f && v.magnitude > 0.5f && Vector3.Angle(v0, v) >= angle * 0.85f) tTurn = t;
                        yield return null;
                    }
                    string turnStates = string.Join(">", C.StateLog.Skip(C.StateLog.Count - 3));
                    Log($"[{tier} turn {angle}] ({turnStates}) velocity turned in {(tTurn < 0 ? "never(2.5s)" : tTurn.ToString("F2") + "s")}, speed dipped to {minSpeed:F2}, carried {overshoot:F2} m along the old direction, max body roll {maxRoll:F1} deg");
                    In.Move = Vector2.zero; In.DodgeHeld = false;
                }
            }

            // 3. Jumping: momentum kept at takeoff, and how much input can redirect him mid-air.
            Warp(new Vector3(-40f, 0.05f, -40f));
            yield return Settle(0.4f);
            yield return Accelerate("sprint", Cfg.sprintSpeed, (a, b) => { });
            float takeoff = C.Motor.PlanarSpeed;
            Vector3 dir0 = C.Motor.PlanarVelocity.normalized;
            In.TapJump();
            yield return new WaitForSeconds(0.1f);
            In.Move = new Vector2(0f, -1f); In.DodgeHeld = false;   // pull back hard in the air
            float minAir = 99f, airtime = 0f;
            while (!C.Motor.Grounded && airtime < 3f)
            {
                minAir = Mathf.Min(minAir, Vector3.Dot(C.Motor.PlanarVelocity, dir0));
                airtime += Time.deltaTime;
                yield return null;
            }
            Log($"[jump from sprint] takeoff {takeoff:F2} m/s; pulling back mid-air cut forward speed to {minAir:F2} m/s over {airtime + 0.1f:F2}s airtime (zero air control would keep {takeoff:F2})");
            In.Move = Vector2.zero;
            yield return Settle(0.8f);

            Warp(new Vector3(-40f, 0.05f, -40f));
            yield return Settle(0.4f);
            In.TapJump();
            yield return new WaitForSeconds(0.05f);
            In.Move = new Vector2(1f, 0f);
            float side = 0f;
            for (float t = 0f; t < 1f && !(C.Motor.Grounded && t > 0.2f); t += Time.deltaTime) { side = Mathf.Max(side, C.Motor.PlanarSpeed); yield return null; }
            Log($"[standing jump] sideways input in the air built {side:F2} m/s of drift (zero air control: 0)");
            In.Move = Vector2.zero;
            yield return Settle(0.8f);

            // 4. Landing from a running super jump: compression and recovery back to speed.
            Warp(new Vector3(-40f, 0.05f, -40f));
            yield return Settle(0.4f);
            In.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.6f);
            In.JumpHeld = true;
            yield return new WaitForSeconds(0.8f);
            In.JumpHeld = false;
            yield return new WaitForSeconds(0.2f);
            for (float t = 0f; t < 4f && !C.Motor.Grounded; t += Time.deltaTime) yield return null;
            string landState = C.StateName;
            float landSpeed = C.Motor.PlanarSpeed, back = -1f, comp = 0f, dip = 0f;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                if (back < 0f && C.Motor.PlanarSpeed >= Cfg.runSpeed * 0.9f && C.StateName == "Locomotion") back = t;
                comp = Mathf.Max(comp, C.FootIK.Compression);
                dip = Mathf.Min(dip, C.cameraRig.Dip);
                yield return null;
            }
            Log($"[running super jump] launch speed {SuperJumpChargeState.LaunchSpeed:F1} m/s; landing state {landState}, planar speed at touchdown {landSpeed:F2}, back to run speed after {back:F2}s, pelvis compression {comp:F3} m, camera dip {dip:F3} m");
            In.Move = Vector2.zero;

            // 4b. Surge launch, and a surge turn's camera roll.
            Warp(new Vector3(-40f, 0.05f, -40f));
            yield return Settle(0.4f);
            yield return Accelerate("surge", Cfg.surgeSpeed, (a, b) => { });
            In.Move = new Vector2(1f, 1f);
            float camRoll = 0f;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { camRoll = Mathf.Max(camRoll, Mathf.Abs(C.cameraRig.Roll)); yield return null; }
            In.Move = new Vector2(0f, 1f);
            In.JumpHeld = true;
            yield return new WaitForSeconds(0.9f);
            In.JumpHeld = false;
            yield return new WaitForSeconds(0.1f);
            float surgeLaunch = SuperJumpChargeState.LaunchSpeed;
            Vector3 launchAt = C.transform.position;
            for (float t = 0f; t < 4f && !(C.Motor.Grounded && t > 0.2f); t += Time.deltaTime) yield return null;
            Vector3 flew = C.transform.position - launchAt; flew.y = 0f;
            Log($"[surge launch] camera roll in the turn {camRoll:F1} deg; launch speed {surgeLaunch:F1} m/s, flew {flew.magnitude:F1} m, landed into {C.StateName}");
            In.Move = Vector2.zero; In.DodgeHeld = false;
            yield return Settle(1.2f);

            // 4c. Combat momentum: sprinting into a light attack, knockback, and a flash step out of a sprint.
            var dummy = Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).First(d => d.name.StartsWith("Dummy A"));
            Vector3 toD = dummy.transform.position; toD.y = 0f;
            foreach (bool sprinting in new[] { false, true })
            {
                Vector3 from = dummy.transform.position - toD.normalized * (sprinting ? 7f : 1.4f);
                C.Motor.Warp(new Vector3(from.x, 0.05f, from.z), Quaternion.LookRotation(toD.normalized));
                C.ChangeState(new LocomotionState(C));
                C.cameraRig.SnapBehind();
                yield return Settle(0.3f);
                if (sprinting)
                {
                    In.Move = new Vector2(0f, 1f); In.DodgeHeld = true;
                    for (float t = 0f; t < 2f && Vector3.Distance(C.transform.position, dummy.transform.position) > 2.2f; t += Time.deltaTime) yield return null;
                }
                float approach = C.Motor.PlanarSpeed;
                In.TapLight();
                yield return null; yield return null;
                float kept = C.Motor.PlanarSpeed;
                In.Move = Vector2.zero; In.DodgeHeld = false;
                yield return new WaitForSeconds(0.6f);
                Log($"[light attack {(sprinting ? "out of a sprint" : "standing")}] approach {approach:F2} m/s, kept {kept:F2} m/s into the strike, dummy knockback {dummy.LastKnockback:F2} m");
                yield return Settle(0.8f);
            }
            Warp(new Vector3(-40f, 0.05f, -40f));
            yield return Settle(0.3f);
            yield return Accelerate("sprint", Cfg.sprintSpeed, (a, b) => { });
            In.DodgeHeld = false;
            yield return null;
            In.DodgeHeld = true; yield return null; In.DodgeHeld = false;   // tap: flash step forward
            In.Move = new Vector2(0f, 1f);
            for (float t = 0f; t < 0.6f && !(C.StateName == "Locomotion" && t > 0.1f); t += Time.deltaTime) yield return null;
            Log($"[flash step out of a sprint] speed after the step {C.Motor.PlanarSpeed:F2} m/s (sprint {Cfg.sprintSpeed})");
            In.Move = Vector2.zero;
            yield return Settle(1.2f);

            // 5. Reaver Sweep capture (heavy 1, charged).
            Warp(new Vector3(-40f, 0.05f, -40f));
            yield return Settle(0.5f);
            var camGo = new GameObject("PlaytestCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            var rt = new RenderTexture(420, 380, 24);
            var shots = new List<Texture2D>();
            void Shot()
            {
                camGo.transform.position = C.transform.position + C.transform.forward * 2.4f + C.transform.right * 1.2f + Vector3.up * 1.1f;
                camGo.transform.LookAt(C.transform.position + Vector3.up * 0.6f);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(420, 380, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 420, 380), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                shots.Add(tex);
            }
            var heavy = C.Weapon.heavyChain[0];
            In.HeavyHeld = true;
            float[] at = { 0.15f, 0.45f, 0.85f };
            int k = 0;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                if (k < at.Length && t >= at[k]) { Shot(); k++; }
                yield return null;
            }
            In.HeavyHeld = false;
            var atk = C.State as AttackState;
            float[] strike = { 0.42f, 0.5f, 0.56f, 0.64f, 0.85f };
            int sIdx = 0;
            for (float t = 0f; t < 1.2f && C.State is AttackState; t += Time.deltaTime)
            {
                float u = ((AttackState)C.State).AttackTime;
                if (sIdx < strike.Length && u >= strike[sIdx] * heavy.duration) { Shot(); sIdx++; }
                yield return null;
            }
            int cols = 4, rows = Mathf.CeilToInt(shots.Count / (float)cols);
            var sheet = new Texture2D(420 * cols, 380 * rows, TextureFormat.RGB24, false);
            for (int i = 0; i < shots.Count; i++) sheet.SetPixels((i % cols) * 420, (rows - 1 - i / cols) * 380, 420, 380, shots[i].GetPixels());
            sheet.Apply();
            Directory.CreateDirectory("Logs/NightfarerCaptures");
            File.WriteAllBytes("Logs/NightfarerCaptures/heavy_reaver_sweep.png", sheet.EncodeToPNG());
            Object.Destroy(camGo);
            rt.Release();
            Log($"[heavy] {heavy.name}: {shots.Count} frames captured (charge creep then strike)");
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator Playtest_BodyLanguageCaptures()
        {
            yield return Arena();
            var camGo = new GameObject("BodyCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            var rt = new RenderTexture(420, 380, 24);
            var shots = new List<Texture2D>();
            var pose = C.GetComponent<MomentumPose>();
            void Shot(Vector3 offsetLocal)
            {
                camGo.transform.position = C.transform.TransformPoint(offsetLocal);
                camGo.transform.LookAt(C.transform.position + Vector3.up * 0.55f);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(420, 380, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 420, 380), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                shots.Add(tex);
            }
            // Surge: side view (claw drag, sparks, leg lines), then a hard turn from behind (bank).
            Warp(new Vector3(-40f, 0.05f, -40f));
            yield return Settle(0.4f);
            yield return Accelerate("surge", Cfg.surgeSpeed, (a, b) => { });
            In.Move = new Vector2(0f, 1f); In.DodgeHeld = true;
            float tipLow = 9f;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                if (C.Hitbox.TryGetSegment(0, out _, out var tip)) tipLow = Mathf.Min(tipLow, tip.y - C.transform.position.y);
                yield return null;
            }
            Shot(new Vector3(-2.6f, 0.7f, 0.4f));
            yield return null; yield return null;   // let the frame time settle after the render
            In.Move = new Vector2(1f, 0.3f);
            float roll = 0f;
            var trace = new System.Text.StringBuilder();
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                roll = Mathf.Abs(pose.Roll) > Mathf.Abs(roll) ? pose.Roll : roll;
                if (trace.Length < 900) trace.Append($" [{C.StateName}/{C.MovementMode} v{C.Motor.PlanarSpeed:F1} yaw{C.transform.eulerAngles.y:F0} vel{Mathf.Atan2(C.Motor.PlanarVelocity.x, C.Motor.PlanarVelocity.z) * Mathf.Rad2Deg:F0} r{pose.Roll:F1} g{C.Motor.Grounded}]");
                yield return null;
            }
            Log("[turn trace]" + trace);
            Log($"[surge claw drag] lowest claw tip {tipLow:F2} m above the ground");
            Shot(new Vector3(0f, 1.0f, -3.0f));
            // Skid: let go at surge speed.
            In.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.6f);
            In.Move = Vector2.zero; In.DodgeHeld = false;
            yield return new WaitForSeconds(0.2f);
            string st = C.StateName;
            float pitch = pose.Pitch;
            Shot(new Vector3(2.6f, 0.8f, 0.2f));
            yield return null; yield return null;
            yield return new WaitForSeconds(1.2f);
            // Jump squat.
            In.TapJump();
            yield return new WaitForSeconds(0.04f);
            Shot(new Vector3(2.4f, 0.8f, 0.6f));
            int cols = shots.Count;
            var sheet = new Texture2D(420 * cols, 380, TextureFormat.RGB24, false);
            for (int i = 0; i < shots.Count; i++) sheet.SetPixels(i * 420, 0, 420, 380, shots[i].GetPixels());
            sheet.Apply();
            Directory.CreateDirectory("Logs/NightfarerCaptures");
            File.WriteAllBytes("Logs/NightfarerCaptures/body_language.png", sheet.EncodeToPNG());
            Object.Destroy(camGo);
            rt.Release();
            Log($"[body language] surge turn bank {roll:F1} deg, skid state '{st}' pitch {pitch:F1} deg, sparks emitted {ClawSparks.Emitted}");
            Assert.Pass();
        }
    }
}
