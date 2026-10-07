using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Subject143.Nightfarer.EditorTools
{
    /// <summary>
    /// Offscreen pose sheets: samples clips on the dummy (with its weapon attached) and writes PNG grids to
    /// Logs/NightfarerCaptures. Used to review placeholder animations and weapon grips without Play Mode.
    /// </summary>
    public static class NightfarerCapture
    {
        const string OutDir = "Logs/NightfarerCaptures";
        const int W = 260, H = 340;

        class Rig : IDisposable
        {
            public GameObject Model;
            public Animator Animator;
            public Camera Cam;
            public RenderTexture RT;
            readonly List<GameObject> trash = new List<GameObject>();

            public Rig(string weaponPath)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var light = new GameObject("L").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(40, -140, 0);
                light.intensity = 1.4f;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.localScale = new Vector3(6, 0.02f, 6);
                floor.transform.position = new Vector3(0, -0.01f, 0);
                string modelArg = Environment.GetCommandLineArgs().SkipWhile(a => a != "-model").Skip(1).FirstOrDefault();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelArg ?? NightfarerBuilder.CharacterModelPath);
                Model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Animator = Model.GetComponentInChildren<Animator>();
                Animator.applyRootMotion = false;
                var weapon = weaponPath != null ? AssetDatabase.LoadAssetAtPath<WeaponData>(weaponPath) : null;
                if (weapon != null) WeaponMount.Attach(Animator, weapon, out _);
                Cam = new GameObject("Cam").AddComponent<Camera>();
                Cam.fieldOfView = 40f;
                Cam.clearFlags = CameraClearFlags.SolidColor;
                Cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
                RT = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
                Cam.targetTexture = RT;
            }

            public void View(float yawDeg)
            {
                Quaternion r = Quaternion.Euler(10f, yawDeg, 0f);
                Vector3 target = new Vector3(0, 0.95f, 0);
                Cam.transform.position = target - r * Vector3.forward * 3.6f;
                Cam.transform.rotation = r;
            }

            public Texture2D Render()
            {
                Cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = RT;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                return tex;
            }

            public void Dispose()
            {
                RT.Release();
                foreach (var t in trash) UnityEngine.Object.DestroyImmediate(t);
            }
        }

        static void SaveSheet(string name, List<Texture2D> tiles, int columns)
        {
            Directory.CreateDirectory(OutDir);
            int rows = Mathf.CeilToInt(tiles.Count / (float)columns);
            var sheet = new Texture2D(columns * W, rows * H, TextureFormat.RGB24, false);
            var fill = Enumerable.Repeat(new Color(0.1f, 0.1f, 0.1f), sheet.width * sheet.height).ToArray();
            sheet.SetPixels(fill);
            for (int i = 0; i < tiles.Count; i++)
            {
                int cx = i % columns, cy = rows - 1 - i / columns;
                sheet.SetPixels(cx * W, cy * H, W, H, tiles[i].GetPixels());
            }
            sheet.Apply();
            File.WriteAllBytes($"{OutDir}/{name}.png", sheet.EncodeToPNG());
            Debug.Log($"[NightfarerCapture] wrote {OutDir}/{name}.png ({tiles.Count} tiles)");
        }

        /// <summary>One tile per muscle at +0.8 (front 3/4 view) to verify sign conventions.</summary>
        [MenuItem("Subject 143/Nightfarer/Diagnostics/Capture Muscle Probe")]
        public static void CaptureMuscleProbe()
        {
            var basePose = NightfarerBuilder.SamplePose(NightfarerBuilder.LoadFbxClip("HumanM@Idle01.fbx"), 0f);
            string[] tests =
            {
                "", "SF=0.8", "ST=0.8", "CT=0.8", "SL=0.8",
                "RAD=0.8", "RAF=0.8", "RFS=-0.8", "RAT=0.8", "RHD=0.8",
                "RUF=0.8", "RLS=-0.6", "RootPitch=45", "RootYaw=45", "RootDrop=-0.3",
            };
            if (!AssetDatabase.IsValidFolder("Assets/Nightfarer/Generated")) AssetDatabase.CreateFolder("Assets/Nightfarer", "Generated");
            const string tmp = "Assets/Nightfarer/Generated/_probe_tmp.anim";
            var tiles = new List<Texture2D>();
            var log = new List<string>();
            using (var rig = new Rig(null))
            {
                rig.View(205f);
                for (int i = 0; i < tests.Length; i++)
                {
                    string path = tmp.Replace(".anim", $"_{i}.anim");
                    var clip = PlaceholderClips.Build(path, basePose, "", 1f, false, (0f, tests[i]), (1f, tests[i]));
                    clip.SampleAnimation(rig.Animator.gameObject, 0.5f);
                    tiles.Add(rig.Render());
                    AssetDatabase.DeleteAsset(path);
                    log.Add($"{i}: {(tests[i] == "" ? "neutral" : tests[i])}");
                }
            }
            AssetDatabase.DeleteAsset(tmp);
            SaveSheet("muscle_probe", tiles, 5);
            File.WriteAllLines($"{OutDir}/muscle_probe.txt", log);
        }

        /// <summary>Samples every action slot of each animation set at 5 points, plus locomotion, with the greatsword.</summary>
        [MenuItem("Subject 143/Nightfarer/Diagnostics/Capture Pose Sheets")]
        public static void CapturePoseSheets()
        {
            string only = Environment.GetCommandLineArgs().SkipWhile(a => a != "-slots").Skip(1).FirstOrDefault();
            var filter = only != null ? new HashSet<string>(only.Split(',')) : null;

            using (var rig = new Rig("Assets/Nightfarer/Data/Weapon_Greatsword_WylderStyle.asset"))
            {
                var sets = AssetDatabase.FindAssets("t:AnimationSet", new[] { "Assets/Nightfarer/Data" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<AnimationSet>(AssetDatabase.GUIDToAssetPath(g))).ToList();
                foreach (var set in sets)
                {
                    var tiles = new List<Texture2D>();
                    var legend = new List<string>();
                    foreach (var slot in AnimationSlots.Actions.Concat(new[] { "Idle", "Run_F", "Sprint_F", "Surge_F" }))
                    {
                        if (filter != null && !filter.Contains(slot)) continue;
                        var clip = set.Get(slot);
                        if (clip == null) continue;
                        legend.Add($"row {legend.Count}: {slot} ({clip.name}, {clip.length:F2}s)");
                        foreach (float u in new[] { 0f, 0.25f, 0.4f, 0.6f, 0.9f })
                        {
                            clip.SampleAnimation(rig.Animator.gameObject, u * clip.length);
                            rig.View(slot.StartsWith("Roll") || slot.Contains("Land") || slot == "Backstep" ? 270f : 205f);
                            tiles.Add(rig.Render());
                        }
                    }
                    string name = "poses_" + set.name;
                    SaveSheet(name, tiles, 5);
                    File.WriteAllLines($"{OutDir}/{name}.txt", legend);
                }
            }
        }
    }
}
