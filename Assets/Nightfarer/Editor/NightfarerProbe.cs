using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Subject143.Nightfarer.EditorTools
{
    /// <summary>Diagnostics: dumps humanoid curve bindings of a reference clip and the dummy rig's hand layout.</summary>
    public static class NightfarerProbe
    {
        [MenuItem("Subject 143/Nightfarer/Diagnostics/Dump Probe Info")]
        public static void Dump()
        {
            var sb = new StringBuilder();
            string idle = AssetDatabase.FindAssets("HumanM@Idle01").Select(AssetDatabase.GUIDToAssetPath).First(p => p.EndsWith("HumanM@Idle01.fbx"));
            var clip = AssetDatabase.LoadAllAssetsAtPath(idle).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            sb.AppendLine($"clip {clip.name} len {clip.length} human {clip.humanMotion}");
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                sb.AppendLine($"  [{b.path}] {b.type.Name}.{b.propertyName} = {curve.Evaluate(0f):F3}");
            }
            sb.AppendLine("Muscles: " + string.Join(" | ", HumanTrait.MuscleName));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kevin Iglesias/Human Character Dummy/Prefabs/HumanDummy_M White.prefab");
            var inst = Object.Instantiate(prefab);
            var anim = inst.GetComponentInChildren<Animator>();
            sb.AppendLine($"prefab animator: {anim != null} human {anim?.isHuman} avatar {anim?.avatar} controller {anim?.runtimeAnimatorController}");
            var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var mid = anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            var thumb = anim.GetBoneTransform(HumanBodyBones.RightThumbProximal);
            sb.AppendLine($"hand {hand?.name} lossyScale {hand?.lossyScale} pos {hand?.position} rot {hand?.rotation.eulerAngles}");
            if (mid) sb.AppendLine($"mid {mid.name} local-in-hand {hand.InverseTransformPoint(mid.position)}");
            if (thumb) sb.AppendLine($"thumb {thumb.name} local-in-hand {hand.InverseTransformPoint(thumb.position)}");
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            sb.AppendLine($"head height {head.position.y} rootScale {inst.transform.lossyScale}");
            Object.DestroyImmediate(inst);
            File.WriteAllText("Logs/nightfarer_probe.txt", sb.ToString());
            Debug.Log("[NightfarerProbe] wrote Logs/nightfarer_probe.txt");
        }

        public static void DumpModel()
        {
            string path = System.Environment.GetCommandLineArgs().SkipWhile(a => a != "-model").Skip(1).First();
            var sb = new StringBuilder();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(go);
            var anim = inst.GetComponentInChildren<Animator>();
            sb.AppendLine($"model {path} animator {anim != null} avatar {anim?.avatar} valid {anim?.avatar?.isValid} human {anim?.avatar?.isHuman}");
            foreach (HumanBodyBones b in System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (b == HumanBodyBones.LastBone) continue;
                var t = anim.GetBoneTransform(b);
                if (t != null) sb.AppendLine($"  {b}: {t.name} pos {t.position} scale {t.lossyScale}");
            }
            var r = inst.GetComponentsInChildren<Renderer>();
            var bounds = r[0].bounds; foreach (var x in r) bounds.Encapsulate(x.bounds);
            sb.AppendLine($"renderers {r.Length} bounds {bounds} root scale {inst.transform.lossyScale}");
            foreach (var x in r) sb.AppendLine($"  renderer {x.name} {x.GetType().Name} mats {string.Join(",", x.sharedMaterials.Select(m => m ? m.name + "/" + m.shader.name : "null"))}");
            Object.DestroyImmediate(inst);
            File.WriteAllText("Logs/nightfarer_model.txt", sb.ToString());
        }

        public static void DumpHierarchy()
        {
            string path = System.Environment.GetCommandLineArgs().SkipWhile(a => a != "-model").Skip(1).First();
            var sb = new StringBuilder();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sb.AppendLine($"model {path}");
            void Walk(Transform t, int d)
            {
                var comps = string.Join(",", t.GetComponents<Component>().Where(c => !(c is Transform)).Select(c => c.GetType().Name));
                sb.AppendLine($"{new string(' ', d * 2)}{t.name} pos {t.localPosition} rot {t.localEulerAngles} scale {t.localScale} [{comps}]");
                if (d < 12) foreach (Transform c in t) Walk(c, d + 1);
            }
            Walk(go.transform, 0);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                sb.AppendLine($"SMR {smr.name} bones {smr.bones.Length} root {(smr.rootBone ? smr.rootBone.name : "-")} bounds {smr.sharedMesh.bounds} verts {smr.sharedMesh.vertexCount} mats {string.Join(",", smr.sharedMaterials.Select(m => m ? m.name : "null"))}");
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                sb.AppendLine($"MF {mf.name} bounds {mf.sharedMesh.bounds} verts {mf.sharedMesh.vertexCount}");
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                sb.AppendLine($"clip {clip.name} {clip.length:F2}s");
            File.WriteAllText("Logs/nightfarer_hierarchy.txt", sb.ToString());
        }

        public static void MuscleReadback()
        {
            var sb = new StringBuilder();
            var basePose = NightfarerBuilder.SamplePose(NightfarerBuilder.LoadFbxClip("HumanM@Idle01.fbx"), 0f);
            if (!AssetDatabase.IsValidFolder("Assets/Nightfarer/Generated")) AssetDatabase.CreateFolder("Assets/Nightfarer", "Generated");
            var clip = PlaceholderClips.Build("Assets/Nightfarer/Generated/_readback.anim", basePose, "", 1f, false, (0f, "RAD=0.8 SF=0.8"), (1f, "RAD=0.8 SF=0.8"));
            foreach (var b in AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.Contains("Right Arm") || b.propertyName.Contains("Spine Front")))
                sb.AppendLine($"clip binding {b.propertyName} = {AnimationUtility.GetEditorCurve(clip, b).Evaluate(0.5f)}");
            sb.AppendLine($"humanMotion {clip.humanMotion} bindings {AnimationUtility.GetCurveBindings(clip).Length}");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kevin Iglesias/Human Character Dummy/Prefabs/HumanDummy_M White.prefab");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var anim = inst.GetComponentInChildren<Animator>();
            var arm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var handler = new HumanPoseHandler(anim.avatar, anim.transform);
            var pose = new HumanPose();
            int rad = System.Array.IndexOf(HumanTrait.MuscleName, "Right Arm Down-Up");
            int sf = System.Array.IndexOf(HumanTrait.MuscleName, "Spine Front-Back");
            handler.GetHumanPose(ref pose);
            sb.AppendLine($"before: RAD {pose.muscles[rad]:F3} SF {pose.muscles[sf]:F3} armRot {arm.localRotation.eulerAngles}");
            clip.SampleAnimation(anim.gameObject, 0.5f);
            handler.GetHumanPose(ref pose);
            sb.AppendLine($"after SampleAnimation: RAD {pose.muscles[rad]:F3} SF {pose.muscles[sf]:F3} armRot {arm.localRotation.eulerAngles}");
            pose.muscles[rad] = 0.8f;
            handler.SetHumanPose(ref pose);
            handler.GetHumanPose(ref pose);
            sb.AppendLine($"after SetHumanPose: RAD {pose.muscles[rad]:F3} armRot {arm.localRotation.eulerAngles}");
            Object.DestroyImmediate(inst);
            AssetDatabase.DeleteAsset("Assets/Nightfarer/Generated/_readback.anim");
            File.WriteAllText("Logs/nightfarer_readback.txt", sb.ToString());
        }
    }
}
