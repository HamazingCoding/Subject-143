using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Subject143.Nightfarer.EditorTools
{
    /// <summary>
    /// Everything specific to Subject 143: the rigged child-proportioned model (Humanoid import + material), the
    /// claw moveset, the movement config (flash step, child scale), the feral animation set and the profile.
    /// Called by NightfarerBuilder.Build.
    /// </summary>
    public static class Subject143Builder
    {
        public const string ModelPath = "Assets/Characters/Subject143/Subject143_Rigged.fbx";
        public const string MaterialPath = "Assets/Characters/Subject143/M_Subject143.mat";
        public const string CoatMaterialPath = "Assets/Characters/Subject143/M_Subject143_Coat.mat";
        const string CoatSourceMaterial = "Assets/Characters/Subject143/143_Coat/Materials/CoatTexture.mat";
        const string SourceDir = "Assets/Prefabs/Meshy_AI_Ashen_Thornchild_1003060058_texture_fbx";
        const string SourceMaterial = SourceDir + "/Materials/Meshy_AI_Ashen_Thornchild_1003060058_texture.mat";
        const string NormalMap = SourceDir + "/Meshy_AI_Ashen_Thornchild_1003060058_texture_normal.png";
        const string DataDir = "Assets/Nightfarer/Data";
        const string ClipDir = "Assets/Nightfarer/Generated/PlaceholderClips/Subject143";

        // Character dimensions (metres). The model is rigged at 1.25 m tall (Tools/Blender/rig_subject143.py).
        public const float Height = 1.25f;
        public const float CapsuleHeight = 1.15f, CapsuleRadius = 0.22f, StepOffset = 0.28f;
        public const float CameraPivot = 1.0f, CameraDistance = 3.2f, CameraLockedDistance = 3.6f;
        public const float ArcCentreHeight = 0.62f;

        public static bool ModelReady => AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null;

        /// <summary>Humanoid import for the rigged FBX (Blender output) and its URP material.</summary>
        public static void ConfigureModel()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Subject143] rigged model missing: run Tools/Blender/rig_subject143.py first");
                return;
            }
            bool changed = importer.animationType != ModelImporterAnimationType.Human || importer.importAnimation;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = false;
            if (changed) importer.SaveAndReimport();

            var normal = AssetImporter.GetAtPath(NormalMap) as TextureImporter;
            if (normal != null && normal.textureType != TextureImporterType.NormalMap)
            {
                normal.textureType = TextureImporterType.NormalMap;
                normal.SaveAndReimport();
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterial);
                mat = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalMap);
            if (nrm != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", nrm);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(mat);

            // Coat: double-sided copy of the coat material so the lining shows when it flaps open.
            var coatMat = AssetDatabase.LoadAssetAtPath<Material>(CoatMaterialPath);
            if (coatMat == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>(CoatSourceMaterial);
                coatMat = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(coatMat, CoatMaterialPath);
            }
            if (coatMat.HasProperty("_Cull")) coatMat.SetFloat("_Cull", 0f);
            coatMat.doubleSidedGI = true;
            if (coatMat.HasProperty("_Smoothness")) coatMat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(coatMat);

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var anim = go != null ? go.GetComponentInChildren<Animator>() : null;
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            Debug.Log($"[Subject143] model import: avatar {(avatar != null ? avatar.name : "none")} valid {avatar?.isValid} human {avatar?.isHuman}");
        }

        public static void ApplyMaterial(GameObject model)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var coatMat = AssetDatabase.LoadAssetAtPath<Material>(CoatMaterialPath);
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var m = IsCoat(r) ? coatMat : mat;
                if (m != null) r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
            }
        }

        static bool IsCoat(Renderer r) => r.name.StartsWith("Coat");

        /// <summary>
        /// Coat physics: bone-chain cloth (ChainCloth on the model root) drives the high-resolution coat through its
        /// chain bones. Unity Cloth is not used: per-vertex cloth on this mesh broke into shards.
        /// </summary>
        public static void SetupCoat(GameObject model)
        {
            bool hasCoat = false;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!IsCoat(r)) continue;
                hasCoat = true;
                r.updateWhenOffscreen = true;
                foreach (var c in r.GetComponents<CoatCloth>()) Object.DestroyImmediate(c, true);
                foreach (var c in r.GetComponents<Cloth>()) Object.DestroyImmediate(c, true);
            }
            if (hasCoat && model.GetComponent<ChainCloth>() == null) model.AddComponent<ChainCloth>();
            // Hair locks (HairStrand_## chains from the rig script): a second, springier chain cloth.
            bool hasHair = false;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("HairStrand_")) { hasHair = true; break; }
            if (hasHair)
            {
                var hair = model.AddComponent<ChainCloth>();
                hair.chainPrefixes = new[] { "HairStrand_" };
                hair.preset = ChainCloth.Preset.Hair;
                hair.linkNeighbours = false;
                hair.collideHead = true;
                hair.ApplyPreset();
            }
        }

        // ------------------------------------------------------------------ data

        public class Result
        {
            public CharacterProfile profile;
            public AnimationSet feral, plain;
            public WeaponData claws;
        }

        public static Result BuildData(bool overwrite, Dictionary<string, float> basePose, NightfarerBuilder.KevinClips kevin, AbilityData skill)
        {
            if (!AssetDatabase.IsValidFolder(ClipDir)) AssetDatabase.CreateFolder("Assets/Nightfarer/Generated/PlaceholderClips", "Subject143");
            var combat = Subject143Clips.Generate(ClipDir, basePose);
            var feralLoco = Subject143Clips.DeriveFeralLocomotion(ClipDir, kevin);

            var feral = NightfarerBuilder.LoadOrCreatePublic<AnimationSet>(DataDir + "/AnimSet_Subject143_Feral.asset", overwrite, out bool newFeral);
            if (newFeral)
            {
                feral.displayName = "Subject 143 (feral)";
                feral.notes = "Hunched, claw-ready locomotion derived from the project's Kevin Iglesias clips, plus procedural claw/step/" +
                              "reaction clips. The weapon layer (WeaponIK claw mode) drives the claw arcs on top.";
                feral.entries.Clear();
                kevin.FillLocomotion(feral);
                foreach (var kv in feralLoco) feral.Set(kv.Key, kv.Value);
            }
            var feralTraversal = new HashSet<string> { "Mantle", "Vault", "Drink", "SuperJumpCharge", "HeroLand", "SideJumpL", "SideJumpR" };
            foreach (var kv in combat)
                if (newFeral || feral.Get(kv.Key) == null || feralTraversal.Contains(kv.Key)) feral.Set(kv.Key, kv.Value);
            EditorUtility.SetDirty(feral);

            var plain = NightfarerBuilder.LoadOrCreatePublic<AnimationSet>(DataDir + "/AnimSet_Subject143_PlainLocomotion.asset", overwrite, out bool newPlain);
            if (newPlain)
            {
                plain.displayName = "Subject 143 (upright locomotion)";
                plain.notes = "Same claw/step clips with the project's original upright locomotion, for comparison (F2).";
                plain.entries.Clear();
                kevin.FillLocomotion(plain);
            }
            // Upright versions of the full-body traversal clips (climb, vault, drink, super jump, hero landing, side jumps).
            var upright = Subject143Clips.Traversal(ClipDir, basePose, "", "S143U");
            var keepUpright = new HashSet<string>(upright.Keys) { "Idle", "JumpStart", "Fall", "Land" };
            foreach (var kv in upright) plain.Set(kv.Key, kv.Value);
            plain.Set("Idle", kevin.Idle);
            plain.Set("JumpStart", kevin.JumpBegin);
            plain.Set("Fall", kevin.Fall);
            plain.Set("Land", kevin.Land);
            foreach (var kv in combat)
                if (!keepUpright.Contains(kv.Key) && (newPlain || plain.Get(kv.Key) == null)) plain.Set(kv.Key, kv.Value);
            EditorUtility.SetDirty(plain);

            var claws = NightfarerBuilder.LoadOrCreatePublic<WeaponData>(DataDir + "/Weapon_Subject143_Claws.asset", overwrite, out bool newClaws);
            if (newClaws || claws.movesetVersion < ClawMoveset.Version) ClawMoveset.Build(claws);
            ApplyAttackFeel(claws);
            EditorUtility.SetDirty(claws);

            var cfg = NightfarerBuilder.LoadOrCreatePublic<NightfarerConfig>(DataDir + "/Config_Subject143.asset", overwrite, out bool newCfg);
            if (newCfg) ConfigureMovement(cfg);
            ConfigureTraversal(cfg);
            EditorUtility.SetDirty(cfg);

            var ult = NightfarerBuilder.LoadOrCreatePublic<OnslaughtStakeAbility>(DataDir + "/Ultimate_Subject143_Thornburst.asset", overwrite, out bool newUlt);
            if (newUlt)
            {
                ult.displayName = "Thornburst";
                ult.animationSlot = "Ultimate";
                ult.cooldown = 0f;
                ult.windup = 0.55f;
                ult.recovery = 0.6f;
                ult.lungeDistance = 1.6f;
                ult.blastRadius = 3.6f;
                ult.blastArc = 360f;
                ult.damage = 380f;
                ult.blastColor = new Color(0.45f, 0.18f, 0.65f, 0.55f);
            }
            EditorUtility.SetDirty(ult);

            var profile = NightfarerBuilder.LoadOrCreatePublic<CharacterProfile>(DataDir + "/Profile_Subject143.asset", overwrite, out bool newProfile);
            if (newProfile || profile.config == null)
            {
                profile.displayName = "Subject 143";
                profile.config = cfg;
                profile.weapons = new[] { claws };
                profile.skill = skill;
                profile.ultimate = ult;
            }
            // Upright locomotion is the default set (user choice); the feral set stays available on F2.
            profile.animationSet = plain;
            EditorUtility.SetDirty(profile);
            return new Result { profile = profile, feral = feral, plain = plain, claws = claws };
        }

        /// <summary>Small, light and fast: a feral child, not an armoured adult.</summary>
        static void ConfigureMovement(NightfarerConfig c)
        {
            c.walkSpeed = 1.5f; c.runSpeed = 4.4f; c.sprintSpeed = 6.6f; c.surgeSpeed = 8.8f;
            c.lockedWalkSpeed = 1.5f; c.lockedRunSpeed = 3.9f;
            c.acceleration = 42f; c.deceleration = 55f; c.sprintAcceleration = 24f; c.surgeAcceleration = 70f;
            c.turnSpeed = 1080f; c.sprintTurnSpeed = 420f; c.lockedTurnSpeed = 900f;
            c.jumpHeight = 1.1f; c.gravity = -26f;
            c.hardLandFallHeight = 4.0f; c.landRollDistance = 0.8f; c.landRollDuration = 0.45f;
            c.mantleReach = 0.55f; c.mantleMaxHeight = 2.15f; c.mantleGroundMaxHeight = 1.3f; c.mantleMinHeight = 0.45f;
            c.evadeStyle = EvadeStyle.FlashStep;
            c.stepDistance = 3.8f; c.stepTravelTime = 0.14f; c.stepRecovery = 0.14f; c.stepIFrameEnd = 0.19f;
            c.stepStaminaCost = 12f; c.stepChainTime = 0.18f;
            c.maxHealth = 800f; c.maxStamina = 100f; c.staminaRegen = 50f;
            c.hitReactDuration = 0.38f; c.hitReactKnockback = 0.6f;
            c.attackFriction = 40f;
        }

        /// <summary>Screen blur / speed lines / air cracks on the strong claw strikes (light 1-3 only draw action lines).</summary>
        public static void ApplyAttackFeel(WeaponData w)
        {
            void Fx(AttackData a, float blur, float lines, bool crack)
            {
                if (a == null) return;
                a.screenBlur = blur;
                a.speedLines = lines;
                a.spaceCrack = crack;
            }
            if (w.lightChain != null && w.lightChain.Count > 3) Fx(w.lightChain[3], 0.02f, 0.35f, false);   // frenzy
            if (w.lightChain != null && w.lightChain.Count > 4) Fx(w.lightChain[4], 0.05f, 0.45f, true);    // pounce finisher
            if (w.heavyChain != null) foreach (var h in w.heavyChain) Fx(h, 0.06f, 0.5f, true);
            Fx(w.sprintAttack, 0.05f, 0.7f, false);
            Fx(w.jumpAttack, 0.05f, 0.4f, true);
            Fx(w.rollAttack, 0.03f, 0.45f, false);
            Fx(w.backstepAttack, 0.03f, 0.3f, false);
        }

        /// <summary>Climbing, super jump, hero landing and side jump tuning for the child body (applied every build).</summary>
        static void ConfigureTraversal(NightfarerConfig c)
        {
            c.mantleBaseDuration = 0.4f;
            c.mantleDurationPerMetre = 0.28f;
            c.superJumpHeight = 7.5f;
            c.superJumpChargeTime = 0.75f;
            c.heroLanding = true;
            c.heroLandMinHeight = 1.9f;
            c.sideJumpDistance = 3.4f;
            c.sideJumpHeight = 0.75f;
        }

        // ------------------------------------------------------------------ scene helpers

        /// <summary>Character controller sized for the child body.</summary>
        public static void SizeController(CharacterController cc)
        {
            cc.height = CapsuleHeight;
            cc.radius = CapsuleRadius;
            cc.center = new Vector3(0f, CapsuleHeight * 0.5f + 0.01f, 0f);
            cc.stepOffset = StepOffset;
            cc.skinWidth = 0.02f;
            cc.slopeLimit = 50f;
        }
    }
}
