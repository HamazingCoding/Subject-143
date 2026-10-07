using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Subject143.Nightfarer.EditorTools
{
    /// <summary>
    /// Generates everything the Nightfarer experiment needs. Safe to re-run: generated assets are rebuilt,
    /// tunable data assets (configs, weapons, profiles, sets) are only created if missing unless
    /// <c>overwriteData</c> is true. Never touches the project's existing animations, scripts or scenes.
    /// Menu: Subject 143 / Nightfarer / Build Test Setup.
    /// </summary>
    public static class NightfarerBuilder
    {
        const string Root = "Assets/Nightfarer";
        const string Gen = Root + "/Generated";
        const string SlotDir = Gen + "/Slots";
        const string ClipDir = Gen + "/PlaceholderClips";
        const string PrefabDir = Gen + "/Prefabs";
        const string MatDir = Gen + "/Materials";
        const string DataDir = Root + "/Data";
        const string SceneDir = Root + "/Scenes";
        public const string ScenePath = SceneDir + "/NightfarerTestScene.unity";
        public const string ControllerPath = Gen + "/NightfarerBase.controller";
        public const string CharacterModelPath = "Assets/Prefabs/Meshy_AI_Character_output.fbx";
        public const string DummyPrefab = "Assets/Kevin Iglesias/Human Character Dummy/Prefabs/HumanDummy_M White.prefab";
        public const string CharacterMaterialPath = "Assets/Prefabs/Materials/Meshy_AI_Nocturnal_Warden_0512105939_texture.mat";
        public const string RigPrefabPath = Root + "/Prefabs/NightfarerRig.prefab";
        public const string MainMenuPath = SceneDir + "/MainMenu.unity";
        public const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        public const string GameSceneBackupPath = "Assets/Scenes/SampleScene_BeforeNightfarer.unity";
        const string UiDir = Gen + "/UI";

        [MenuItem("Subject 143/Nightfarer/Build Test Setup")]
        public static void BuildMenu() => Build(false);

        [MenuItem("Subject 143/Nightfarer/Rebuild (overwrite tuning data)")]
        public static void RebuildMenu()
        {
            if (EditorUtility.DisplayDialog("Overwrite tuning data?", "Configs, weapons, profiles and animation sets will be reset to defaults.", "Overwrite", "Cancel"))
                Build(true);
        }

        /// <summary>Batch-mode entry point.</summary>
        public static void BuildBatch() => Build(Environment.GetCommandLineArgs().Contains("-overwriteData"));

        public static void Build(bool overwriteData)
        {
            foreach (var d in new[] { Gen, SlotDir, ClipDir, ClipDir + "/Baseline", ClipDir + "/Nightreign", PrefabDir, MatDir, DataDir, SceneDir, UiDir, Root + "/Prefabs" })
                EnsureFolder(d);

            var slotClips = BuildSlotClips();
            var controller = BuildController(slotClips);
            var kevin = new KevinClips();
            var basePose = SamplePose(kevin.Idle, 0f);

            var baselineClips = PlaceholderClips.Generate(ClipDir + "/Baseline", basePose, PlaceholderClips.Style.Baseline);
            var nightreignClips = PlaceholderClips.Generate(ClipDir + "/Nightreign", basePose, PlaceholderClips.Style.Nightreign);
            var nrLocomotion = PlaceholderClips.DeriveNightreignLocomotion(ClipDir + "/Nightreign", kevin);

            var current = LoadOrCreate<AnimationSet>(DataDir + "/AnimSet_Subject143_Current.asset", overwriteData, out bool newCurrent);
            if (newCurrent)
            {
                current.displayName = "Subject 143 Current";
                current.notes = "The project's existing Kevin Iglesias locomotion (the clips PlayerAnimator uses) + baseline placeholder combat clips.";
                current.entries.Clear();
                kevin.FillLocomotion(current);
                foreach (var kv in baselineClips) current.Set(kv.Key, kv.Value);
                EditorUtility.SetDirty(current);
            }
            var experimental = LoadOrCreate<AnimationSet>(DataDir + "/AnimSet_Nightreign_Experimental.asset", overwriteData, out bool newExp);
            if (newExp)
            {
                experimental.displayName = "Nightreign-inspired (experimental)";
                experimental.notes = "Placeholder clips authored to Nightreign-style timing and posture (weapon-carry locomotion, forward-lean surge sprint, wider swings). " +
                                     "No Nightreign assets are used. Drop reference or original clips into any slot to replace a placeholder.";
                experimental.entries.Clear();
                kevin.FillLocomotion(experimental);
                foreach (var kv in nrLocomotion) experimental.Set(kv.Key, kv.Value);
                foreach (var kv in nightreignClips) experimental.Set(kv.Key, kv.Value);
                EditorUtility.SetDirty(experimental);
            }

            // New slots added in later versions are filled into existing sets without touching user edits.
            foreach (var kv in baselineClips) if (current.Get(kv.Key) == null) current.Set(kv.Key, kv.Value);
            foreach (var kv in nightreignClips) if (experimental.Get(kv.Key) == null) experimental.Set(kv.Key, kv.Value);
            EditorUtility.SetDirty(current);
            EditorUtility.SetDirty(experimental);

            var wylderCfg = LoadOrCreate<NightfarerConfig>(DataDir + "/Config_Nightfarer_Wylder.asset", overwriteData, out _);
            var baselineCfg = LoadOrCreate<NightfarerConfig>(DataDir + "/Config_Subject143_Baseline.asset", overwriteData, out bool newBaseCfg);
            if (newBaseCfg) ConfigureBaseline(baselineCfg);

            var mats = new Materials();
            var gsPrefab = BuildSwordPrefab(PrefabDir + "/Placeholder_Greatsword.prefab", 1.15f, 0.095f, 0.34f, 0.36f, mats);
            var ssPrefab = BuildSwordPrefab(PrefabDir + "/Placeholder_StraightSword.prefab", 0.78f, 0.055f, 0.2f, 0.2f, mats);

            var greatsword = LoadOrCreate<WeaponData>(DataDir + "/Weapon_Greatsword_WylderStyle.asset", overwriteData, out bool newGs);
            if (newGs) Movesets.Greatsword(greatsword);
            greatsword.modelPrefab = gsPrefab;
            EditorUtility.SetDirty(greatsword);
            var straight = LoadOrCreate<WeaponData>(DataDir + "/Weapon_StraightSword.asset", overwriteData, out bool newSs);
            if (newSs) Movesets.StraightSword(straight);
            straight.modelPrefab = ssPrefab;
            EditorUtility.SetDirty(straight);

            var claw = LoadOrCreate<ClawShotAbility>(DataDir + "/Ability_ClawShot.asset", overwriteData, out bool newClaw);
            if (newClaw)
            {
                claw.displayName = "Claw Shot";
                claw.followUpAttack = Movesets.ClawFollowUp();
                EditorUtility.SetDirty(claw);
            }

            var ultimate = LoadOrCreate<OnslaughtStakeAbility>(DataDir + "/Ultimate_OnslaughtStake.asset", overwriteData, out bool newUlt);
            if (newUlt)
            {
                ultimate.displayName = "Onslaught Stake";
                ultimate.animationSlot = "Ultimate";
                ultimate.cooldown = 0f;
                EditorUtility.SetDirty(ultimate);
            }

            var wylder = LoadOrCreate<CharacterProfile>(DataDir + "/Profile_Nightfarer_WylderStyle.asset", overwriteData, out _);
            wylder.displayName = string.IsNullOrEmpty(wylder.displayName) || wylder.displayName == "Character" ? "Nightfarer (Wylder-style)" : wylder.displayName;
            wylder.config ??= wylderCfg;
            if (wylder.weapons == null || wylder.weapons.Length == 0) wylder.weapons = new[] { greatsword, straight };
            wylder.skill ??= claw;
            wylder.ultimate ??= ultimate;
            wylder.animationSet ??= experimental;
            EditorUtility.SetDirty(wylder);

            var baseline = LoadOrCreate<CharacterProfile>(DataDir + "/Profile_Subject143_Baseline.asset", overwriteData, out _);
            baseline.displayName = string.IsNullOrEmpty(baseline.displayName) || baseline.displayName == "Character" ? "Subject 143 baseline feel" : baseline.displayName;
            baseline.config ??= baselineCfg;
            if (baseline.weapons == null || baseline.weapons.Length == 0) baseline.weapons = new[] { greatsword, straight };
            baseline.skill ??= claw;
            baseline.ultimate ??= ultimate;
            baseline.animationSet ??= current;
            EditorUtility.SetDirty(baseline);

            // Subject 143: claws, flash step, child-proportioned rigged model. This is what the rig plays.
            Subject143Builder.ConfigureModel();
            var s143Skill = LoadOrCreate<ClawShotAbility>(DataDir + "/Ability_Subject143_ClawShot.asset", overwriteData, out bool newS143Skill);
            if (newS143Skill)
            {
                s143Skill.displayName = "Claw Shot";
                s143Skill.stopDistance = 1.1f;
                s143Skill.followUpAttack = ClawMoveset.ClawShotFollowUp();
                s143Skill.lineColor = new Color(0.45f, 0.25f, 0.6f);
                EditorUtility.SetDirty(s143Skill);
            }
            // Claw line reach (user request: longer); applied every build so older assets pick it up.
            s143Skill.range = 30f;
            s143Skill.lineSpeed = 90f;
            s143Skill.pullSpeed = 30f;
            s143Skill.thrustTime = 0.09f;
            // Wylder-style hook momentum (carried out of the pull; jump to turn it into a leap).
            s143Skill.momentumCarry = 0.4f;
            s143Skill.enemyMomentumCarry = 0.15f;
            s143Skill.momentumPop = 3f;
            s143Skill.momentumTime = 0.7f;
            if (s143Skill.followUpAttack != null)
            {
                s143Skill.followUpAttack.screenBlur = 0.05f;
                s143Skill.followUpAttack.speedLines = 0.5f;
                s143Skill.followUpAttack.spaceCrack = true;
            }
            EditorUtility.SetDirty(s143Skill);
            var s143 = Subject143Builder.BuildData(overwriteData, basePose, kevin, s143Skill);

            AssetDatabase.SaveAssets();
            var spriteSet = BuildUISprites();
            var rigPrefab = BuildRigPrefab(controller, new[] { s143.profile }, new[] { s143.plain, s143.feral }, s143.plain, spriteSet, null);
            BuildScene(rigPrefab, mats);
            BuildMainMenu(spriteSet, mats, s143.feral, null);
            if (!Environment.GetCommandLineArgs().Contains("-skipGameScene")) IntegrateIntoGameScene(rigPrefab, mats);
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Nightfarer] Build complete: " + ScenePath);
        }

        // ------------------------------------------------------------------ helpers

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static T LoadOrCreatePublic<T>(string path, bool overwrite, out bool created) where T : ScriptableObject =>
            LoadOrCreate<T>(path, overwrite, out created);

        static T LoadOrCreate<T>(string path, bool overwrite, out bool created) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null && !overwrite)
            {
                created = false;
                return existing;
            }
            if (existing != null)
            {
                var fresh = ScriptableObject.CreateInstance<T>();
                EditorUtility.CopySerialized(fresh, existing);
                UnityEngine.Object.DestroyImmediate(fresh);
                created = true;
                return existing;
            }
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            created = true;
            return asset;
        }

        public static AnimationClip LoadFbxClip(string fileName)
        {
            string path = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(fileName))
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith("/" + fileName) && p.Contains("/Male/"));
            if (path == null)
            {
                Debug.LogWarning("[Nightfarer] clip not found: " + fileName);
                return null;
            }
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        public static Dictionary<string, float> SamplePose(AnimationClip clip, float time)
        {
            var pose = new Dictionary<string, float>();
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.type != typeof(Animator)) continue;
                pose[b.propertyName] = AnimationUtility.GetEditorCurve(clip, b).Evaluate(time);
            }
            return pose;
        }

        static Dictionary<string, AnimationClip> BuildSlotClips()
        {
            var result = new Dictionary<string, AnimationClip>();
            foreach (var slot in AnimationSlots.Locomotion.Concat(AnimationSlots.Actions))
            {
                string path = $"{SlotDir}/{AnimationSlots.SlotPrefix}{slot}.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    clip = new AnimationClip { name = AnimationSlots.SlotPrefix + slot };
                    AssetDatabase.CreateAsset(clip, path);
                }
                result[slot] = clip;
            }
            return result;
        }

        static readonly Dictionary<string, Vector2> BlendPositions = new Dictionary<string, Vector2>
        {
            { "Idle", Vector2.zero },
            { "Walk_F", new Vector2(0, 1) }, { "Walk_B", new Vector2(0, -1) }, { "Walk_L", new Vector2(-1, 0) }, { "Walk_R", new Vector2(1, 0) },
            { "Walk_FL", new Vector2(-0.707f, 0.707f) }, { "Walk_FR", new Vector2(0.707f, 0.707f) }, { "Walk_BL", new Vector2(-0.707f, -0.707f) }, { "Walk_BR", new Vector2(0.707f, -0.707f) },
            { "Run_F", new Vector2(0, 2) }, { "Run_B", new Vector2(0, -2) }, { "Run_L", new Vector2(-2, 0) }, { "Run_R", new Vector2(2, 0) },
            { "Run_FL", new Vector2(-1.414f, 1.414f) }, { "Run_FR", new Vector2(1.414f, 1.414f) }, { "Run_BL", new Vector2(-1.414f, -1.414f) }, { "Run_BR", new Vector2(1.414f, -1.414f) },
            { "Sprint_F", new Vector2(0, 3) }, { "Sprint_FL", new Vector2(-2.12f, 2.12f) }, { "Sprint_FR", new Vector2(2.12f, 2.12f) },
            { "Surge_F", new Vector2(0, 4) },
        };

        static AnimatorController BuildController(Dictionary<string, AnimationClip> slots)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "ActionSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;
            var loco = ctrl.CreateBlendTreeInController(AnimationSlots.LocomotionState, out BlendTree tree, 0);
            tree.blendType = BlendTreeType.FreeformCartesian2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            foreach (var slot in AnimationSlots.Locomotion) tree.AddChild(slots[slot], BlendPositions[slot]);
            sm.defaultState = loco;
            loco.writeDefaultValues = true;

            int i = 0;
            foreach (var slot in AnimationSlots.Actions)
            {
                var st = sm.AddState(slot, new Vector3(450 + (i / 10) * 250, (i % 10) * 60, 0));
                st.motion = slots[slot];
                st.writeDefaultValues = true;
                if (!AnimationSlots.IsLooping(slot))
                {
                    st.speedParameterActive = true;
                    st.speedParameter = "ActionSpeed";
                }
                i++;
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void ConfigureBaseline(NightfarerConfig c)
        {
            // Mirrors the feel of the project's existing PlayerMovement (walk 2 / run 5 / sprint 10 m/s,
            // 0.2 s acceleration, real-world gravity, 1.5 m jump), with the new combat layered on.
            c.walkSpeed = 2f; c.runSpeed = 5f; c.sprintSpeed = 10f; c.surgeSpeed = 10f;
            c.lockedWalkSpeed = 1.8f; c.lockedRunSpeed = 4.2f;
            c.acceleration = 25f; c.deceleration = 25f; c.sprintAcceleration = 60f;
            c.turnSpeed = 540f; c.sprintTurnSpeed = 300f;
            c.surgeEnabled = false;
            c.gravity = -9.81f; c.jumpHeight = 1.5f; c.maxFallSpeed = -30f;
            c.hardLandFallHeight = 6f;
            c.rollDuration = 0.95f; c.rollDistance = 3.0f; c.rollIFrameStart = 0.08f; c.rollIFrameEnd = 0.4f;
            c.rollCancelTime = 0.75f; c.rollMoveCancelTime = 0.85f; c.rollAttackWindow = new Vector2(0.55f, 1.0f);
            c.sprintStaminaPerSecond = 12f;
            EditorUtility.SetDirty(c);
        }

        // ------------------------------------------------------------------ prefabs and materials

        public class Materials
        {
            public readonly Material Ground, Lane, Block, Accent, Metal, Grip, Dummy, Telegraph, Pillar;

            public Materials()
            {
                Ground = Make("M_Ground", new Color(0.36f, 0.38f, 0.41f));
                Lane = Make("M_Lane", new Color(0.85f, 0.8f, 0.55f));
                Block = Make("M_Block", new Color(0.52f, 0.55f, 0.6f));
                Accent = Make("M_Accent", new Color(0.3f, 0.45f, 0.65f));
                Metal = Make("M_Metal", new Color(0.78f, 0.8f, 0.82f), 0.8f, 0.75f);
                Grip = Make("M_Grip", new Color(0.25f, 0.16f, 0.1f));
                Dummy = Make("M_Dummy", new Color(0.72f, 0.6f, 0.42f));
                Pillar = Make("M_Pillar", new Color(0.45f, 0.42f, 0.4f));
                string tPath = MatDir + "/M_Telegraph.mat";
                Telegraph = AssetDatabase.LoadAssetAtPath<Material>(tPath);
                if (Telegraph == null)
                {
                    Telegraph = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.15f, 0.1f, 0.25f) };
                    AssetDatabase.CreateAsset(Telegraph, tPath);
                }
            }

            static Material Make(string name, Color color, float metallic = 0f, float smoothness = 0.3f)
            {
                string path = $"{MatDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    m = new Material(shader);
                    AssetDatabase.CreateAsset(m, path);
                }
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                m.color = color;
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
                EditorUtility.SetDirty(m);
                return m;
            }
        }

        static GameObject BuildSwordPrefab(string path, float bladeLength, float bladeWidth, float guardWidth, float gripLength, Materials mats)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            Part(root.transform, PrimitiveType.Cylinder, new Vector3(0, gripLength * 0.25f, 0), new Vector3(0.035f, gripLength * 0.5f, 0.035f), mats.Grip);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0, -gripLength * 0.25f - 0.02f, 0), Vector3.one * 0.055f, mats.Metal);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0, gripLength * 0.75f, 0), new Vector3(guardWidth, 0.04f, 0.06f), mats.Metal);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0, gripLength * 0.75f + bladeLength * 0.5f + 0.02f, 0), new Vector3(bladeWidth, bladeLength, 0.022f), mats.Metal);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            // Weapon models never carry colliders (they would push the CharacterController around).
            var col = go.GetComponent<Collider>();
            if (col != null && parent != null && parent.name.StartsWith("Placeholder_")) UnityEngine.Object.DestroyImmediate(col);
            return go;
        }

        // ------------------------------------------------------------------ scene

        static void BuildScene(GameObject rigPrefab, Materials mats)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Switching scenes in batch mode can unload assets loaded earlier: reload what this step uses.
            rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            mats = new Materials();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f);

            var env = new GameObject("Environment").transform;
            Block(env, "Ground", new Vector3(0, -0.5f, 20), new Vector3(140, 1, 140), mats.Ground);

            // Sprint lane: markers every 5 m to judge speed/acceleration.
            var lane = new GameObject("Sprint Lane (5 m markers)").transform;
            lane.SetParent(env);
            for (int z = 0; z <= 60; z += 5)
            {
                Block(lane, $"Marker {z}m", new Vector3(-12, 0.01f, z), new Vector3(4, 0.02f, 0.15f), mats.Lane);
                Label(lane, $"{z} m", new Vector3(-14.6f, 0.4f, z));
            }

            // Slopes and stairs.
            var ramps = new GameObject("Slopes & Stairs").transform;
            ramps.SetParent(env);
            Ramp(ramps, "Ramp 20deg", new Vector3(-24, 0, 10), 20f, mats.Block);
            Ramp(ramps, "Ramp 35deg", new Vector3(-30, 0, 10), 35f, mats.Block);
            for (int s = 0; s < 12; s++)
                Block(ramps, $"Step {s}", new Vector3(-37, 0.1f + s * 0.2f, 4 + s * 0.4f), new Vector3(3, 0.2f + s * 0.4f, 0.4f), mats.Block).transform.position =
                    new Vector3(-37, (0.2f + s * 0.2f) * 0.5f, 4 + s * 0.4f);

            // Jump/drop tower: blocks rising by 1 m (jumpable) up to 9 m, for soft vs hard landings.
            var tower = new GameObject("Drop Tower (1-9 m)").transform;
            tower.SetParent(env);
            for (int h = 1; h <= 9; h++)
            {
                var b = Block(tower, $"Ledge {h}m", new Vector3(10 + (h - 1) * 2.2f, h * 0.5f, 40), new Vector3(2.2f, h, 4), h % 2 == 0 ? mats.Accent : mats.Block);
                Label(tower, $"{h} m", new Vector3(10 + (h - 1) * 2.2f, h + 0.6f, 37.8f));
            }

            // Combat arena.
            var arena = new GameObject("Combat Arena").transform;
            arena.SetParent(env);
            var dummies = new List<TrainingDummy>
            {
                Dummy(arena, "Dummy A", new Vector3(14, 0, 12), mats),
                Dummy(arena, "Dummy B", new Vector3(19, 0, 15), mats),
                Dummy(arena, "Dummy C (far)", new Vector3(16, 0, 22), mats),
            };
            var attacker = Attacker(arena, new Vector3(26, 0, 8), mats);
            for (int p = 0; p < 4; p++)
                Block(arena, $"Pillar {p}", new Vector3(10 + p * 6, 2, 28), new Vector3(1.2f, 4, 1.2f), mats.Pillar);
            Block(arena, "Low Wall", new Vector3(8, 0.6f, 18), new Vector3(0.6f, 1.2f, 6), mats.Pillar);
            Label(arena, "Shockwave dummy: roll through the red wave", new Vector3(26, 3.4f, 8));

            // Climbing walls (jump into them to mantle) and a spirit spring.
            var climb = new GameObject("Climbing & Spirit Spring").transform;
            climb.SetParent(env);
            float[] heights = { 1.2f, 2.0f, 3.0f, 4.5f };
            for (int i = 0; i < heights.Length; i++)
            {
                Block(climb, $"Climb Wall {heights[i]}m", new Vector3(-8 - i * 4.5f, heights[i] * 0.5f, 26), new Vector3(3.5f, heights[i], 3f), i % 2 == 0 ? mats.Accent : mats.Block);
                Label(climb, $"{heights[i]} m", new Vector3(-8 - i * 4.5f, heights[i] + 0.6f, 24.4f));
            }
            Label(climb, "Run + jump into a wall to climb onto it", new Vector3(-14.75f, 5.6f, 24.4f));
            SpiritSpringProp(climb, new Vector3(4, 0, 24), mats);
            Label(climb, "Spirit spring: stand in it, press Jump", new Vector3(4, 3.2f, 24));

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.position = new Vector3(0, 0.05f, 0);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ------------------------------------------------------------------ rig prefab (player + camera + UI)

        static GameObject BuildRigPrefab(AnimatorController controller, CharacterProfile[] profiles, AnimationSet[] sets, AnimationSet fallback, UISpriteSet sprites, WeaponData previewWeapon)
        {
            var root = new GameObject("NightfarerRig");

            var player = new GameObject("NightfarerPlayer");
            player.transform.SetParent(root.transform, false);
            var cc = player.AddComponent<CharacterController>();
            Subject143Builder.SizeController(cc);
            player.AddComponent<NightfarerMotor>();
            player.AddComponent<NightfarerVitals>();
            var lockOn = player.AddComponent<LockOnSystem>();
            var hitbox = player.AddComponent<WeaponHitbox>();
            hitbox.drawDebug = false;
            hitbox.arcCentreHeight = Subject143Builder.ArcCentreHeight;
            var input = player.AddComponent<DeviceInputSource>();
            var driver = player.AddComponent<NightfarerAnimator>();
            var ik = player.AddComponent<WeaponIK>();
            player.AddComponent<FlashStepVFX>();
            var feet = player.AddComponent<FootIK>();
            var combatFx = player.AddComponent<CombatFX>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);
            var modelPrefab = PlayerModel();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            model.name = "Subject 143";
            model.transform.SetParent(visual, false);
            ApplyPlayerMaterial(model);
            Subject143Builder.SetupCoat(model);
            var animator = model.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var relay = animator.gameObject.AddComponent<AnimationEventRelay>();
            foreach (var col in model.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(col);

            driver.animator = animator;
            driver.baseController = controller;
            driver.fallbackSet = fallback;
            driver.visualRoot = visual;

            var character = player.AddComponent<NightfarerCharacter>();
            character.profiles = profiles;
            character.animationSets = sets;
            character.animatorDriver = driver;
            character.hitbox = hitbox;
            character.lockOn = lockOn;
            character.inputSource = input;
            character.eventRelay = relay;
            character.weaponIK = ik;
            ik.character = character;
            character.footIK = feet;
            feet.character = character;
            combatFx.character = character;

            var camGo = new GameObject("NightfarerCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.fieldOfView = 55f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<ScreenFX>();
            var camRig = camGo.AddComponent<NightfarerCamera>();
            camRig.character = character;
            camRig.cam = cam;
            camRig.pivotHeight = Subject143Builder.CameraPivot;
            camRig.distance = Subject143Builder.CameraDistance;
            camRig.lockedDistance = Subject143Builder.CameraLockedDistance;
            camRig.minDistance = 0.4f;
            camGo.transform.localPosition = new Vector3(0, 2.4f, -4.4f);
            character.cameraRig = camRig;

            var debugGo = new GameObject("NightfarerDebug");
            debugGo.transform.SetParent(root.transform, false);
            var debugHud = debugGo.AddComponent<NightfarerDebugHUD>();
            debugHud.character = character;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var ui = NightfarerUIFactory.Build(root.transform, sprites, font);
            ui.character = character;
            ui.debugHud = debugHud;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, RigPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>The playable model: Subject 143 if its rigged FBX exists, else the earlier Meshy character.</summary>
        static GameObject PlayerModel() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(Subject143Builder.ModelPath)
            ?? AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath)
            ?? AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefab);

        static void ApplyPlayerMaterial(GameObject model)
        {
            if (Subject143Builder.ModelReady) Subject143Builder.ApplyMaterial(model);
            else ApplyCharacterMaterial(model);
        }

        static void ApplyCharacterMaterial(GameObject model)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CharacterMaterialPath);
            if (mat == null) return;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
        }

        static UISpriteSet BuildUISprites()
        {
            Sprite Save(Texture2D tex, string name, float border = 0f)
            {
                string path = $"{UiDir}/{name}.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spriteBorder = new Vector4(border, border, border, border);
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return new UISpriteSet
            {
                white = Save(UISprites.WhiteTexture(), "ui_white"),
                circle = Save(UISprites.CircleTexture(128, 0f, "ui_circle"), "ui_circle"),
                ring = Save(UISprites.CircleTexture(128, 0.82f, "ui_ring"), "ui_ring"),
                vignette = Save(UISprites.VignetteTexture(256), "ui_vignette"),
                gradient = Save(UISprites.GradientTexture(), "ui_gradient"),
            };
        }

        static UISpriteSet LoadUISprites()
        {
            Sprite L(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/{n}.png");
            return new UISpriteSet { white = L("ui_white"), circle = L("ui_circle"), ring = L("ui_ring"), vignette = L("ui_vignette"), gradient = L("ui_gradient") };
        }

        static void SpiritSpringProp(Transform parent, Vector3 pos, Materials mats)
        {
            var root = new GameObject("Spirit Spring");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            var spring = root.AddComponent<SpiritSpring>();
            var glow = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_SpiritGlow.mat");
            if (glow == null)
            {
                glow = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.55f, 0.85f, 1f, 0.35f) };
                AssetDatabase.CreateAsset(glow, MatDir + "/M_SpiritGlow.mat");
            }
            var disc = Part(root.transform, PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0), new Vector3(2.6f, 0.02f, 2.6f), glow, "Disc");
            UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
            var column = Part(root.transform, PrimitiveType.Cylinder, new Vector3(0, 2.5f, 0), new Vector3(1.4f, 2.5f, 1.4f), glow, "Column");
            UnityEngine.Object.DestroyImmediate(column.GetComponent<Collider>());
            var ring = Part(root.transform, PrimitiveType.Cylinder, new Vector3(0, 0.25f, 0), new Vector3(2.2f, 0.01f, 2.2f), glow, "Ring");
            UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
            spring.ring = ring.transform;
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 1.5f, 0);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.55f, 0.85f, 1f);
            l.range = 6f;
            l.intensity = 2f;
        }

        // ------------------------------------------------------------------ main menu

        static void BuildMainMenu(UISpriteSet sprites, Materials mats, AnimationSet set, WeaponData weapon)
        {
            const string setPath = DataDir + "/AnimSet_Subject143_PlainLocomotion.asset";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            sprites = LoadUISprites();
            mats = new Materials();
            set = AssetDatabase.LoadAssetAtPath<AnimationSet>(setPath);
            weapon = null;   // Subject 143 fights with claws
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.13f, 0.17f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.04f, 0.045f, 0.06f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.06f;

            var camGo = new GameObject("Menu Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.045f, 0.06f);
            cam.fieldOfView = 35f;
            camGo.transform.position = new Vector3(-0.8f, 1.0f, 3.2f);
            camGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.65f, 0.72f, 0f) - camGo.transform.position);
            camGo.AddComponent<AudioListener>();

            var key = new GameObject("Key Light").AddComponent<Light>();
            key.type = LightType.Spot;
            key.color = new Color(1f, 0.8f, 0.55f);
            key.intensity = 30f;
            key.range = 14f;
            key.spotAngle = 45f;
            key.shadows = LightShadows.Soft;
            key.transform.position = new Vector3(-2.5f, 4.5f, 3f);
            key.transform.rotation = Quaternion.LookRotation(new Vector3(0.6f, 1f, 0f) - key.transform.position);
            var rim = new GameObject("Rim Light").AddComponent<Light>();
            rim.type = LightType.Spot;
            rim.color = new Color(0.45f, 0.65f, 1f);
            rim.intensity = 25f;
            rim.range = 12f;
            rim.spotAngle = 50f;
            rim.transform.position = new Vector3(2.5f, 3.5f, -3f);
            rim.transform.rotation = Quaternion.LookRotation(new Vector3(0.6f, 1.2f, 0f) - rim.transform.position);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Stage";
            ground.transform.position = new Vector3(0.6f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(5f, 0.05f, 5f);
            ground.GetComponent<Renderer>().sharedMaterial = mats.Pillar;

            var modelPrefab = PlayerModel();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            model.name = "Subject 143";
            model.transform.position = new Vector3(0.6f, 0f, 0f);
            model.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
            ApplyPlayerMaterial(model);
            Subject143Builder.SetupCoat(model);
            var anim = model.GetComponentInChildren<Animator>();
            anim.applyRootMotion = false;
            string idlePath = Gen + "/MenuIdle.controller";
            AssetDatabase.DeleteAsset(idlePath);
            var idleCtrl = AnimatorController.CreateAnimatorControllerAtPath(idlePath);
            var idleState = idleCtrl.layers[0].stateMachine.AddState("Idle");
            idleState.motion = set.Get("Idle");
            anim.runtimeAnimatorController = idleCtrl;
            var menuChar = model.AddComponent<MenuCharacter>();
            menuChar.weapon = weapon;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MainMenuUI.Build(null, sprites, font);
            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        // ------------------------------------------------------------------ the game scene

        /// <summary>
        /// Puts the Nightfarer rig into the project's main scene. A backup of the original scene is made once;
        /// the old Player and its cameras are disabled (not deleted) so they can be switched back on.
        /// </summary>
        static void IntegrateIntoGameScene(GameObject rigPrefab, Materials mats)
        {
            if (!File.Exists(GameScenePath)) return;
            if (!File.Exists(GameSceneBackupPath)) AssetDatabase.CopyAsset(GameScenePath, GameSceneBackupPath);
            const string s143Backup = "Assets/Scenes/SampleScene_BeforeSubject143.unity";
            if (!File.Exists(s143Backup)) AssetDatabase.CopyAsset(GameScenePath, s143Backup);
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            mats = new Materials();

            Vector3 spawn = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;
            bool spawnFromSubject = false;
            foreach (var go in scene.GetRootGameObjects())
            {
                // The static Subject_143_True placed in the scene marks where the player should stand; the rig's
                // rigged copy of that model replaces it (the static one is disabled, not deleted).
                // The loose coat placed in the scene is now worn by the rig (skinned + cloth): disable the static copy.
                if (go.name == "Coat" && go.GetComponentInChildren<NightfarerCharacter>(true) == null)
                {
                    go.SetActive(false);
                    continue;
                }
                if (go.name == "Subject_143_True")
                {
                    spawn = go.transform.position;
                    spawnRot = Quaternion.Euler(0f, go.transform.eulerAngles.y, 0f);
                    spawnFromSubject = true;
                    go.SetActive(false);
                    continue;
                }
                if (go.name == "NightfarerRig" || go.name.StartsWith("Nightfarer Practice"))
                {
                    UnityEngine.Object.DestroyImmediate(go);
                    continue;
                }
                if (go.name == "Player" && go.GetComponentInChildren<NightfarerCharacter>(true) == null)
                {
                    if (!spawnFromSubject)
                    {
                        spawn = go.transform.position;
                        spawnRot = Quaternion.Euler(0f, go.transform.eulerAngles.y, 0f);
                    }
                    go.SetActive(false);
                }
                else if (go.GetComponent<Camera>() != null) go.SetActive(false);
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
            rig.transform.SetPositionAndRotation(spawn + Vector3.up * 0.05f, spawnRot);

            // A few practice props near the spawn (safe to delete).
            var practice = new GameObject("Nightfarer Practice (safe to delete)").transform;
            Vector3 fwd = spawnRot * Vector3.forward, right = spawnRot * Vector3.right;
            Dummy(practice, "Training Dummy A", spawn + fwd * 7f + right * 2f, mats);
            Dummy(practice, "Training Dummy B", spawn + fwd * 9f - right * 2.5f, mats);
            SpiritSpringProp(practice, spawn - right * 7f + fwd * 3f, mats);
            Block(practice, "Climb Block 2m", spawn + right * 7f + fwd * 4f + Vector3.up * 1f, new Vector3(3, 2, 3), mats.Accent);
            foreach (Transform t in practice) t.LookAt(new Vector3(spawn.x, t.position.y, spawn.z));

            EditorSceneManager.SaveScene(scene);
        }

        static void UpdateBuildSettings()
        {
            var list = EditorBuildSettings.scenes.Where(s => s.path != MainMenuPath && s.path != ScenePath && s.path != GameSceneBackupPath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(MainMenuPath, true));
            if (!list.Any(s => s.path == GameScenePath) && File.Exists(GameScenePath)) list.Add(new EditorBuildSettingsScene(GameScenePath, true));
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        static void Ramp(Transform parent, string name, Vector3 foot, float angle, Material mat)
        {
            float length = 10f;
            float rad = angle * Mathf.Deg2Rad;
            var go = Block(parent, name, Vector3.zero, new Vector3(4, 0.4f, length), mat);
            go.transform.rotation = Quaternion.Euler(-angle, 0, 0);
            go.transform.position = foot + new Vector3(0, Mathf.Sin(rad) * length * 0.5f - 0.2f * Mathf.Cos(rad), Mathf.Cos(rad) * length * 0.5f);
            Label(parent, name, foot + new Vector3(0, 0.6f, -0.8f));
        }

        static void Label(Transform parent, string text, Vector3 pos)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;
            go.transform.rotation = Quaternion.Euler(0, 0, 0);
        }

        static TrainingDummy Dummy(Transform parent, string name, Vector3 pos, Materials mats)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.LookRotation(new Vector3(-pos.x, 0, -pos.z));
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            Part(body, PrimitiveType.Cylinder, new Vector3(0, 0.45f, 0), new Vector3(0.12f, 0.45f, 0.12f), mats.Pillar, "Post");
            Part(body, PrimitiveType.Capsule, new Vector3(0, 1.25f, 0), new Vector3(0.6f, 0.55f, 0.45f), mats.Dummy, "Torso");
            Part(body, PrimitiveType.Sphere, new Vector3(0, 1.95f, 0), Vector3.one * 0.38f, mats.Dummy, "Head");
            Part(body, PrimitiveType.Cube, new Vector3(0, 1.45f, 0), new Vector3(1.3f, 0.12f, 0.12f), mats.Dummy, "Arms");
            var d = root.AddComponent<TrainingDummy>();
            d.body = body;
            var t = root.AddComponent<LockOnTarget>();
            t.pointHeight = 1.35f;
            return d;
        }

        static DummyAttacker Attacker(Transform parent, Vector3 pos, Materials mats)
        {
            var root = new GameObject("Shockwave Dummy");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            Part(root.transform, PrimitiveType.Cylinder, new Vector3(0, 0.6f, 0), new Vector3(0.9f, 0.6f, 0.9f), mats.Pillar, "Base");
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0, 1.6f, 0), Vector3.one * 0.7f, mats.Accent, "Core");
            var tele = Part(root.transform, PrimitiveType.Sphere, new Vector3(0, 1f, 0), Vector3.one * 0.3f, mats.Telegraph, "Telegraph");
            UnityEngine.Object.DestroyImmediate(tele.GetComponent<Collider>());
            var a = root.AddComponent<DummyAttacker>();
            a.telegraph = tele.transform;
            var t = root.AddComponent<LockOnTarget>();
            t.pointHeight = 1.6f;
            return a;
        }

        // ------------------------------------------------------------------ Kevin Iglesias clips (existing project assets)

        public class KevinClips
        {
            public readonly AnimationClip Idle = LoadFbxClip("HumanM@Idle01.fbx");
            public readonly Dictionary<string, AnimationClip> Loco = new Dictionary<string, AnimationClip>();
            public readonly AnimationClip JumpBegin = LoadFbxClip("HumanM@Jump01 - Begin.fbx");
            public readonly AnimationClip Fall = LoadFbxClip("HumanM@Fall01.fbx");
            public readonly AnimationClip Land = LoadFbxClip("HumanM@Jump01 - Land.fbx");

            static readonly Dictionary<string, string> Dir = new Dictionary<string, string>
            {
                { "F", "Forward" }, { "B", "Backward" }, { "L", "Left" }, { "R", "Right" },
                { "FL", "ForwardLeft" }, { "FR", "ForwardRight" }, { "BL", "BackwardLeft" }, { "BR", "BackwardRight" },
            };

            public KevinClips()
            {
                foreach (var d in Dir)
                {
                    Loco["Walk_" + d.Key] = LoadFbxClip($"HumanM@Walk01_{d.Value}.fbx");
                    Loco["Run_" + d.Key] = LoadFbxClip($"HumanM@Run01_{d.Value}.fbx");
                }
                Loco["Sprint_F"] = LoadFbxClip("HumanM@Sprint01_Forward.fbx");
                Loco["Sprint_FL"] = LoadFbxClip("HumanM@Sprint01_ForwardLeft.fbx");
                Loco["Sprint_FR"] = LoadFbxClip("HumanM@Sprint01_ForwardRight.fbx");
            }

            public void FillLocomotion(AnimationSet set)
            {
                set.Set("Idle", Idle);
                foreach (var kv in Loco) set.Set(kv.Key, kv.Value);
                set.Set("Surge_F", Loco["Sprint_F"]);
                set.Set("JumpStart", JumpBegin);
                set.Set("Fall", Fall);
                set.Set("Land", Land);
            }
        }
    }
}
