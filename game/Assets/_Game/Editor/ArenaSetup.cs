using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Arena.EditorTools
{
    /// <summary>
    /// Sets the whole project up from code so nobody has to click around in the editor:
    /// humanoid rigs on the stickman models, Quaternius animation import (loops, in-place, hit events),
    /// extra animations authored from code (kick, spin kick, flex, slam, tornado, fall), the Animator Controller,
    /// materials, arena textures, the Main scene and player settings.
    /// Menu: Arena > Kurulum.  Batch: -executeMethod Arena.EditorTools.ArenaSetup.RunBatch
    /// </summary>
    public static class ArenaSetup
    {
        public const string StickDir = "Assets/_Private/Resources/Stickman";
        const string StickTexDir = "Assets/_Private/StickmanTextures";
        const string ArenaDir = "Assets/_Private/Resources/ArenaModel";
        const string PrivMatDir = "Assets/_Private/Resources/Materials";
        public const string Ual1 = "Assets/_Game/Art/Animations/UAL1.fbx";
        public const string Ual2 = "Assets/_Game/Art/Animations/UAL2.fbx";
        const string CustomClipDir = "Assets/_Game/Art/Animations/Custom";
        const string ResAnimDir = "Assets/_Game/Resources/Anim";
        public const string ControllerPath = ResAnimDir + "/Fighter.controller";
        const string MatDir = "Assets/_Game/Resources/Materials";
        const string ScenePath = "Assets/Scenes/Main.unity";

        static readonly StringBuilder report = new StringBuilder();

        // ------------------------------------------------------------------ entry points
        [MenuItem("Arena/Kurulum (her şeyi yeniden kur)")]
        public static void RunAll()
        {
            report.Clear();
            ImportStage();
            BuildStage();
            Debug.Log("[Arena] Kurulum tamam.\n" + report);
        }

        public static void RunBatch()
        {
            try { RunAll(); }
            catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        public static void ImportStage()
        {
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(PrivMatDir);
            Directory.CreateDirectory(ResAnimDir);
            Directory.CreateDirectory(CustomClipDir);
            AssetDatabase.Refresh();
            CreateBaseMaterials();
            ConfigureStickman();
            ConfigureAnimationLibrary(Ual1);
            ConfigureAnimationLibrary(Ual2);
            ConfigureArena();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildStage()
        {
            CreateCustomClips();
            CreateAnimator();
            CreateScene();
            ConfigurePlayer();
            ConfigureRenderer();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            try
            {
                Directory.CreateDirectory("../_test-output");
                File.WriteAllText("../_test-output/setup_report.txt", report.ToString());
            }
            catch { }
        }

        // ------------------------------------------------------------------ materials
        /// <summary>Read-only diagnostic; does not rebuild scenes, controllers, imports or player settings.</summary>
        public static void AuditFighterMaterials()
        {
            var audit = new StringBuilder();
            int broken = 0;
            var modelPaths = Directory.Exists(StickDir) ? Directory.GetFiles(StickDir, "*.fbx") : Array.Empty<string>();
            foreach (var path in modelPaths.Select(p => p.Replace('\\', '/')))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { audit.AppendLine("MISSING " + path); broken++; continue; }
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        bool valid = material != null && material.shader != null && material.shader.isSupported
                            && !ShaderUtil.ShaderHasError(material.shader);
                        if (!valid) broken++;
                        audit.AppendLine(Path.GetFileName(path) + " / " + renderer.name + " / "
                            + (material != null ? material.name : "MISSING") + " / "
                            + (material != null && material.shader != null ? material.shader.name : "MISSING SHADER")
                            + " / valid=" + valid);
                    }
                }
            }
            foreach (string name in new[] { "M_Lit", "M_Metal", "M_LitEmissive", "M_XRay", "M_UIOnTop" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + name + ".mat");
                string expected = name == "M_XRay" ? "Arena/XRay" : name == "M_UIOnTop" ? "Arena/UIOnTop"
                    : "Universal Render Pipeline/Lit";
                bool valid = material != null && material.shader != null && material.shader.isSupported
                    && material.shader.name == expected && !ShaderUtil.ShaderHasError(material.shader);
                if (!valid) broken++;
                audit.AppendLine(name + " / " + (material != null && material.shader != null ? material.shader.name : "MISSING")
                    + " / valid=" + valid);
            }
            audit.AppendLine("Invalid material slots: " + broken);
            Directory.CreateDirectory("../_test-output");
            File.WriteAllText("../_test-output/material-audit.txt", audit.ToString());
            Debug.Log("[Arena] Fighter material audit:\n" + audit);
            if (broken > 0) Debug.LogError("[Arena] Unsupported or missing fighter materials: " + broken);
        }

        /// <summary>
        /// Generated materials were copied from the separate build project without that project's shader
        /// .meta files. Rebind only those two broken shader references; preserve material GUIDs and scene setup.
        /// </summary>
        public static void RepairVisualMaterialReferences()
        {
            foreach (var pair in new[] { (material: "M_XRay", shader: "Arena/XRay"), (material: "M_UIOnTop", shader: "Arena/UIOnTop") })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + pair.material + ".mat");
                var shader = Shader.Find(pair.shader);
                if (material == null || shader == null)
                    throw new Exception("Missing visual material or shader: " + pair.material + " / " + pair.shader);
                if (material.shader == shader) continue;
                material.shader = shader;
                EditorUtility.SetDirty(material);
                Debug.Log("[Arena] Repaired shader reference: " + pair.material + " -> " + pair.shader);
            }
            AssetDatabase.SaveAssets();
            AuditFighterMaterials();
        }

        static Material SaveMat(Material m, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = m.shader;
                existing.CopyPropertiesFromMaterial(m);
                existing.shaderKeywords = m.shaderKeywords;
                existing.renderQueue = m.renderQueue;
                existing.enableInstancing = m.enableInstancing;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void CreateBaseMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var simple = Shader.Find("Universal Render Pipeline/Simple Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");

            var mLit = new Material(lit) { enableInstancing = true };
            mLit.SetColor("_BaseColor", Color.white);
            mLit.SetFloat("_Smoothness", 0.3f);
            SaveMat(mLit, MatDir + "/M_Lit.mat");

            var mMetal = new Material(lit) { enableInstancing = true };
            mMetal.SetColor("_BaseColor", Color.white);
            mMetal.SetFloat("_Smoothness", 0.75f);
            mMetal.SetFloat("_Metallic", 0.8f);
            SaveMat(mMetal, MatDir + "/M_Metal.mat");

            var mEm = new Material(lit) { enableInstancing = true };
            mEm.SetColor("_BaseColor", Color.white);
            mEm.EnableKeyword("_EMISSION");
            mEm.SetColor("_EmissionColor", Color.white);
            mEm.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SaveMat(mEm, MatDir + "/M_LitEmissive.mat");

            var mUn = new Material(unlit) { enableInstancing = true };
            mUn.SetColor("_BaseColor", Color.white);
            SaveMat(mUn, MatDir + "/M_Unlit.mat");

            var add = Shader.Find("Arena/FxAdditive");
            var alpha = Shader.Find("Arena/FxAlpha");
            if (add == null || alpha == null) throw new Exception("Arena/Fx shaders not found");
            SaveMat(new Material(add) { enableInstancing = true }, MatDir + "/M_FxAdd.mat");
            SaveMat(new Material(alpha) { enableInstancing = true }, MatDir + "/M_FxAlpha.mat");
            var onTop = Shader.Find("Arena/UIOnTop");
            var xray = Shader.Find("Arena/XRay");
            if (onTop == null || xray == null) throw new Exception("Arena/UIOnTop or Arena/XRay shader not found");
            SaveMat(new Material(onTop), MatDir + "/M_UIOnTop.mat");
            var xm = new Material(xray);
            xm.SetColor("_Color", new Color(0.55f, 0.85f, 1f, 0.55f));
            SaveMat(xm, MatDir + "/M_XRay.mat");

            // stickman materials (palette textures belong to the licensed pack -> _Private)
            var baseTex = LoadTex(StickTexDir + "/TallStickmanPack1_Base_Texture.png", false, false);
            var skinTex = LoadTex(StickTexDir + "/TallStickmanPack1_Skin_Texture.png", false, false);
            var cloth = new Material(simple) { enableInstancing = true };
            cloth.SetTexture("_BaseMap", baseTex);
            cloth.SetColor("_BaseColor", Color.white);
            cloth.SetFloat("_Smoothness", 0.15f);
            SaveMat(cloth, PrivMatDir + "/M_StickClothes.mat");
            var skin = new Material(simple) { enableInstancing = true };
            skin.SetTexture("_BaseMap", skinTex);
            skin.SetColor("_BaseColor", Color.white);
            skin.SetFloat("_Smoothness", 0.2f);
            SaveMat(skin, PrivMatDir + "/M_StickSkin.mat");
            AssetDatabase.SaveAssets();
        }

        static Texture2D LoadTex(string path, bool normal, bool linear, int maxSize = 1024)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                bool changed = false;
                var want = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (imp.textureType != want) { imp.textureType = want; changed = true; }
                if (!normal && imp.sRGBTexture == linear) { imp.sRGBTexture = !linear; changed = true; }
                if (imp.maxTextureSize != maxSize) { imp.maxTextureSize = maxSize; changed = true; }
                if (changed) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ stickman models -> humanoid
        static readonly (string human, string bone)[] StickBones =
        {
            ("Hips", "pelvis"), ("Spine", "spine.001"), ("Chest", "spine.002"), ("UpperChest", "spine.003"),
            ("Neck", "neck"), ("Head", "head"),
            ("LeftShoulder", "shoulder.L"), ("LeftUpperArm", "upper_arm.L"), ("LeftLowerArm", "forearm.L"), ("LeftHand", "hand.L"),
            ("RightShoulder", "shoulder.R"), ("RightUpperArm", "upper_arm.R"), ("RightLowerArm", "forearm.R"), ("RightHand", "hand.R"),
            ("LeftUpperLeg", "thigh.L"), ("LeftLowerLeg", "shin.L"), ("LeftFoot", "foot.L"), ("LeftToes", "toe.L"),
            ("RightUpperLeg", "thigh.R"), ("RightLowerLeg", "shin.R"), ("RightFoot", "foot.R"), ("RightToes", "toe.R"),
        };

        static void ConfigureStickman()
        {
            var cloth = AssetDatabase.LoadAssetAtPath<Material>(PrivMatDir + "/M_StickClothes.mat");
            var skin = AssetDatabase.LoadAssetAtPath<Material>(PrivMatDir + "/M_StickSkin.mat");
            report.AppendLine("== Stickman models");
            foreach (var path in Directory.GetFiles(StickDir, "*.fbx").Select(p => p.Replace('\\', '/')))
            {
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.importAnimation = false;
                imp.importCameras = false;
                imp.importLights = false;
                imp.importBlendShapes = false;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "clothes"), cloth);
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "skin"), skin);
                imp.SaveAndReimport();

                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                {
                    // explicit mapping for the Rigify metarig bone names
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var hd = imp.humanDescription;
                    hd.human = StickBones.Select(b => new HumanBone { humanName = b.human, boneName = b.bone, limit = new HumanLimit { useDefaultValues = true } }).ToArray();
                    hd.skeleton = go.GetComponentsInChildren<Transform>(true)
                        .Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
                    imp.humanDescription = hd;
                    imp.SaveAndReimport();
                    avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                }
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var b = ModelBounds(model);
                report.AppendLine($"{Path.GetFileName(path)} avatar valid={avatar != null && avatar.isValid} human={avatar != null && avatar.isHuman} size={b.size} center={b.center}");
            }
        }

        public static Bounds ModelBounds(GameObject model)
        {
            var go = Object.Instantiate(model);
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds();
            foreach (var r in rs) b.Encapsulate(r.bounds);
            Object.DestroyImmediate(go);
            return b;
        }

        // ------------------------------------------------------------------ animation library
        // Clips we use (doc §6.3). Everything else in the libraries is skipped to keep memory low.
        static readonly HashSet<string> WantedClips = new HashSet<string>
        {
            "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Punch_Jab", "Punch_Cross", "Punch_Enter",
            "Sword_Attack", "Hit_Chest", "Hit_Head", "Death01", "Dance_Loop", "Roll", "Jump_Start", "Jump_Loop", "Jump_Land",
            "Melee_Hook", "Hit_Knockback", "LayToIdle", "Zombie_Idle_Loop", "Zombie_Walk_Fwd_Loop", "Idle_No_Loop", "Yes",
            "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C", "NinjaJump_Land", "Idle_FoldArms_Loop", "Spell_Simple_Enter",
        };

        // normalized time of the contact frame -> Animation Event "OnAnimHit" (doc §6.1)
        static readonly Dictionary<string, float> HitEvents = new Dictionary<string, float>
        {
            { "Punch_Jab", 0.32f }, { "Punch_Cross", 0.38f }, { "Melee_Hook", 0.42f },
            { "Sword_Attack", 0.42f }, { "Sword_Regular_A", 0.38f }, { "Sword_Regular_B", 0.38f }, { "Sword_Regular_C", 0.4f },
        };

        static void ConfigureAnimationLibrary(string path)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            if (imp == null) { report.AppendLine("MISSING " + path); return; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.bakeAxisConversion = true;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.SaveAndReimport();

            var list = new List<ModelImporterClipAnimation>();
            foreach (var d in imp.defaultClipAnimations)
            {
                string shortName = d.takeName.Contains("|") ? d.takeName.Substring(d.takeName.LastIndexOf('|') + 1) : d.takeName;
                if (!WantedClips.Contains(shortName)) continue;
                d.name = shortName;
                d.loopTime = shortName.EndsWith("_Loop");
                d.loopPose = false;
                d.lockRootRotation = true;
                d.keepOriginalOrientation = true;
                d.lockRootHeightY = true;
                d.keepOriginalPositionY = true;
                d.heightFromFeet = false;
                d.lockRootPositionXZ = true;
                d.keepOriginalPositionXZ = true;
                if (HitEvents.TryGetValue(shortName, out float t))
                    d.events = new[] { new AnimationEvent { functionName = "OnAnimHit", time = t } };
                else d.events = new AnimationEvent[0];
                list.Add(d);
            }
            imp.clipAnimations = list.ToArray();
            imp.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            report.AppendLine($"== {path}: avatar valid={avatar != null && avatar.isValid} human={avatar != null && avatar.isHuman}");
            foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
                report.AppendLine($"  clip {c.name} len={c.length:0.00} loop={c.isLooping} human={c.humanMotion}");
        }

        public static AnimationClip Clip(string name)
        {
            foreach (var p in new[] { Ual1, Ual2 })
            {
                var c = AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().FirstOrDefault(x => x.name == name);
                if (c != null) return c;
            }
            var custom = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{CustomClipDir}/{name}.anim");
            if (custom == null) Debug.LogError("[Arena] clip not found: " + name);
            return custom;
        }

        // ------------------------------------------------------------------ arena model
        static readonly (string mat, string tex)[] ArenaMats =
        {
            ("ArenaMaterial", "Arena"), ("ArenaGate", "GateArena"), ("Flag", "Flags"), ("FlagPoleMaterial", "FlagPole"),
            ("MechanismMaterial", "Mechanism"), ("PillarsMaterial", "Pillars"), ("WoodSpike", "WoodSpikes"),
        };

        static void ConfigureArena()
        {
            string fbx = ArenaDir + "/Arena.fbx";
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (imp == null) { report.AppendLine("MISSING arena"); return; }
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var (matName, tex) in ArenaMats)
            {
                var m = new Material(lit) { enableInstancing = true };
                var albedo = LoadTex($"{ArenaDir}/{tex}-Albedo.png", false, false);
                var nm = LoadTex($"{ArenaDir}/{tex}-NM.png", true, true);
                var ao = LoadTex($"{ArenaDir}/{tex}-AO.png", false, true);
                m.SetTexture("_BaseMap", albedo);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", 0.12f);
                if (nm != null) { m.SetTexture("_BumpMap", nm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
                if (ao != null) { m.SetTexture("_OcclusionMap", ao); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP"); }
                var saved = SaveMat(m, $"{PrivMatDir}/MA_{matName}.mat");
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), saved);
            }
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.bakeAxisConversion = true;
            imp.isReadable = true; // runtime MeshCollider ray to find the floor height
            imp.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var go = Object.Instantiate(model);
            report.AppendLine("== Arena parts");
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                report.AppendLine($"  {r.name} bounds center={r.bounds.center} size={r.bounds.size} mats={string.Join(",", r.sharedMaterials.Select(x => x ? x.name : "null"))}");
            Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------------ custom clips (authored from code)
        /// <summary>
        /// Humanoid clips the libraries do not have: front kick, spin kick pose, flex (power-up), ground slam,
        /// tornado pose and a backwards fall that ends exactly in the first frame of LayToIdle (get-up).
        /// Each key pose = Idle pose + bones rotated in character space, read back as muscles with HumanPoseHandler.
        /// </summary>
        static void CreateCustomClips()
        {
            var modelPath = StickDir + "/Knight.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) { report.AppendLine("no model for custom clips"); return; }
            var go = Object.Instantiate(model);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var animator = go.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman) { Object.DestroyImmediate(go); report.AppendLine("model has no human avatar"); return; }
            var handler = new HumanPoseHandler(animator.avatar, go.transform);

            var idle = Clip("Idle_Loop");
            var lay = Clip("LayToIdle");
            var basePose = SampleClip(idle, 0f);
            var lyingPose = lay != null ? SampleClip(lay, 0f) : basePose;

            HumanPose P(Action<Animator> adjust) => PoseFrom(handler, animator, basePose, adjust);

            // front kick (right leg): chamber -> extend (hit) -> rechamber -> back
            var kChamber = P(a => { Rot(a, HumanBodyBones.RightUpperLeg, Vector3.right, -75); Rot(a, HumanBodyBones.RightLowerLeg, Vector3.right, 95); Rot(a, HumanBodyBones.Spine, Vector3.right, -8); });
            var kExtend = P(a => { Rot(a, HumanBodyBones.RightUpperLeg, Vector3.right, -88); Rot(a, HumanBodyBones.RightLowerLeg, Vector3.right, 5); Rot(a, HumanBodyBones.RightFoot, Vector3.right, 30); Rot(a, HumanBodyBones.Spine, Vector3.right, -16); Rot(a, HumanBodyBones.LeftUpperArm, Vector3.forward, -25); Rot(a, HumanBodyBones.RightUpperArm, Vector3.forward, 25); });
            var kBack = P(a => { Rot(a, HumanBodyBones.RightUpperLeg, Vector3.right, -60); Rot(a, HumanBodyBones.RightLowerLeg, Vector3.right, 90); Rot(a, HumanBodyBones.Spine, Vector3.right, -6); });
            SaveClip("Kick", new[] { (0f, basePose), (0.22f, kChamber), (0.36f, kExtend), (0.5f, kExtend), (0.62f, kBack), (0.85f, basePose) }, false, 0.38f);

            // spin kick pose: right leg out to the side, arms out, leaning away (the body spin is done in code)
            var s1 = P(a => { Rot(a, HumanBodyBones.RightUpperLeg, Vector3.forward, 80); Rot(a, HumanBodyBones.LeftUpperLeg, Vector3.forward, 8); Rot(a, HumanBodyBones.Spine, Vector3.forward, 18); Rot(a, HumanBodyBones.LeftUpperArm, Vector3.forward, -70); Rot(a, HumanBodyBones.RightUpperArm, Vector3.forward, 55); });
            var s2 = P(a => { Rot(a, HumanBodyBones.RightUpperLeg, Vector3.forward, 88); Rot(a, HumanBodyBones.LeftUpperLeg, Vector3.forward, 5); Rot(a, HumanBodyBones.Spine, Vector3.forward, 22); Rot(a, HumanBodyBones.LeftUpperArm, Vector3.forward, -80); Rot(a, HumanBodyBones.RightUpperArm, Vector3.forward, 65); });
            SaveClip("SpinKick", new[] { (0f, s1), (0.25f, s2), (0.5f, s1) }, true, -1f);

            // tornado pose: arms wide, slight crouch
            var t1 = P(a => { Rot(a, HumanBodyBones.LeftUpperArm, Vector3.forward, -85); Rot(a, HumanBodyBones.RightUpperArm, Vector3.forward, 85); Rot(a, HumanBodyBones.LeftUpperLeg, Vector3.right, -20); Rot(a, HumanBodyBones.LeftLowerLeg, Vector3.right, 30); Rot(a, HumanBodyBones.RightUpperLeg, Vector3.right, -20); Rot(a, HumanBodyBones.RightLowerLeg, Vector3.right, 30); Rot(a, HumanBodyBones.Head, Vector3.right, 15); });
            var t2 = P(a => { Rot(a, HumanBodyBones.LeftUpperArm, Vector3.forward, -70); Rot(a, HumanBodyBones.RightUpperArm, Vector3.forward, 95); Rot(a, HumanBodyBones.LeftUpperLeg, Vector3.right, -25); Rot(a, HumanBodyBones.LeftLowerLeg, Vector3.right, 35); Rot(a, HumanBodyBones.RightUpperLeg, Vector3.right, -15); Rot(a, HumanBodyBones.RightLowerLeg, Vector3.right, 25); });
            SaveClip("TornadoSpin", new[] { (0f, t1), (0.2f, t2), (0.4f, t1) }, true, -1f);

            // flex / power-up: double biceps, chest out, wide stance
            var f1 = P(a => { Rot(a, HumanBodyBones.LeftUpperArm, Vector3.forward, -80); Rot(a, HumanBodyBones.RightUpperArm, Vector3.forward, 80); Rot(a, HumanBodyBones.LeftLowerArm, Vector3.forward, -95); Rot(a, HumanBodyBones.RightLowerArm, Vector3.forward, 95); Rot(a, HumanBodyBones.Spine, Vector3.right, -10); Rot(a, HumanBodyBones.LeftUpperLeg, Vector3.forward, -10); Rot(a, HumanBodyBones.RightUpperLeg, Vector3.forward, 10); Rot(a, HumanBodyBones.Head, Vector3.right, -12); });
            var f2 = P(a => { Rot(a, HumanBodyBones.LeftUpperArm, Vector3.forward, -78); Rot(a, HumanBodyBones.RightUpperArm, Vector3.forward, 78); Rot(a, HumanBodyBones.LeftLowerArm, Vector3.forward, -110); Rot(a, HumanBodyBones.RightLowerArm, Vector3.forward, 110); Rot(a, HumanBodyBones.Spine, Vector3.right, -14); Rot(a, HumanBodyBones.LeftUpperLeg, Vector3.forward, -12); Rot(a, HumanBodyBones.RightUpperLeg, Vector3.forward, 12); Rot(a, HumanBodyBones.Head, Vector3.right, -18); });
            SaveClip("Flex", new[] { (0f, basePose), (0.12f, f1), (0.3f, f2), (0.45f, f1), (0.6f, f2) }, false, -1f);

            // ground slam (shock wave): arms up while jumping (code moves the body up), then smash down
            var up = P(a => { Rot(a, HumanBodyBones.LeftUpperArm, Vector3.right, -165); Rot(a, HumanBodyBones.RightUpperArm, Vector3.right, -165); Rot(a, HumanBodyBones.Spine, Vector3.right, -10); Rot(a, HumanBodyBones.LeftUpperLeg, Vector3.right, -35); Rot(a, HumanBodyBones.LeftLowerLeg, Vector3.right, 60); Rot(a, HumanBodyBones.RightUpperLeg, Vector3.right, -35); Rot(a, HumanBodyBones.RightLowerLeg, Vector3.right, 60); });
            var smash = P(a => { Rot(a, HumanBodyBones.Spine, Vector3.right, 30); Rot(a, HumanBodyBones.Chest, Vector3.right, 20); Rot(a, HumanBodyBones.LeftUpperArm, Vector3.right, -40); Rot(a, HumanBodyBones.RightUpperArm, Vector3.right, -40); Rot(a, HumanBodyBones.LeftUpperLeg, Vector3.right, -55); Rot(a, HumanBodyBones.LeftLowerLeg, Vector3.right, 100); Rot(a, HumanBodyBones.RightUpperLeg, Vector3.right, -20); Rot(a, HumanBodyBones.RightLowerLeg, Vector3.right, 95); });
            smash.bodyPosition += new Vector3(0, -0.35f * animator.humanScale, 0);
            SaveClip("Slam", new[] { (0f, basePose), (0.15f, up), (0.45f, up), (0.55f, smash), (0.95f, smash), (1.25f, basePose) }, false, 0.55f);

            // backwards fall that ends in LayToIdle's first frame (so get-up continues seamlessly)
            var stumble = P(a => { Rot(a, HumanBodyBones.Spine, Vector3.right, -25); Rot(a, HumanBodyBones.LeftUpperArm, Vector3.right, -60); Rot(a, HumanBodyBones.RightUpperArm, Vector3.right, -70); Rot(a, HumanBodyBones.Head, Vector3.right, 20); });
            var mid = Lerp(stumble, lyingPose, 0.55f);
            SaveClip("Fall", new[] { (0f, basePose), (0.12f, stumble), (0.32f, mid), (0.5f, lyingPose) }, false, -1f);

            // ---- fighting stance that bounces a little (used as "idle" so nobody looks frozen)
            var guardClip = Clip("Punch_Enter");
            var guard = guardClip != null ? SampleClip(guardClip, guardClip.length * 0.98f) : basePose;
            var gDown = PoseFrom(handler, animator, guard, a => { Rot(a, HumanBodyBones.LeftUpperLeg, Vector3.right, -10); Rot(a, HumanBodyBones.LeftLowerLeg, Vector3.right, 18); Rot(a, HumanBodyBones.RightUpperLeg, Vector3.right, -10); Rot(a, HumanBodyBones.RightLowerLeg, Vector3.right, 18); Rot(a, HumanBodyBones.Spine, Vector3.right, 5); });
            gDown.bodyPosition += new Vector3(0, -0.035f, 0);
            var gUp = PoseFrom(handler, animator, guard, a => { Rot(a, HumanBodyBones.Spine, Vector3.right, -3); Rot(a, HumanBodyBones.Head, Vector3.forward, 4); });
            SaveClip("FightIdle", new[] { (0f, gUp), (0.2f, gDown), (0.4f, gUp), (0.6f, gDown), (0.8f, gUp) }, true, -1f);

            // ---- stumbling sideways (strong hit from the side)
            HumanPose Lean(float side, float amount)
            {
                var p = PoseFrom(handler, animator, basePose, a =>
                {
                    Rot(a, HumanBodyBones.Spine, Vector3.forward, side * amount);
                    Rot(a, HumanBodyBones.Chest, Vector3.forward, side * amount * 0.5f);
                    Rot(a, HumanBodyBones.Head, Vector3.forward, side * amount * 0.6f);
                    Rot(a, HumanBodyBones.LeftUpperArm, Vector3.forward, -60f - side * 15f);
                    Rot(a, HumanBodyBones.RightUpperArm, Vector3.forward, 60f - side * 15f);
                    Rot(a, side > 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg, Vector3.forward, -side * amount * 0.7f);
                });
                p.bodyPosition += new Vector3(-side * amount * 0.004f, -0.02f, 0);
                return p;
            }
            // side = +1 leans to -x (left), -1 leans to +x (right)
            SaveClip("StaggerL", new[] { (0f, basePose), (0.12f, Lean(1f, 18f)), (0.32f, Lean(1f, 26f)), (0.65f, basePose) }, false, -1f);
            SaveClip("StaggerR", new[] { (0f, basePose), (0.12f, Lean(-1f, 18f)), (0.32f, Lean(-1f, 26f)), (0.65f, basePose) }, false, -1f);

            // ---- knocked down to a side (ends lying on the back so the normal get-up follows)
            HumanPose Roll(HumanPose p, float deg)
            {
                var o = p; o.muscles = (float[])p.muscles.Clone();
                o.bodyRotation = Quaternion.AngleAxis(deg, Vector3.forward) * p.bodyRotation;
                return o;
            }
            var midL = Roll(Lerp(Lean(1f, 32f), lyingPose, 0.55f), 35f);
            var midR = Roll(Lerp(Lean(-1f, 32f), lyingPose, 0.55f), -35f);
            SaveClip("FallL", new[] { (0f, basePose), (0.14f, Lean(1f, 32f)), (0.32f, midL), (0.5f, lyingPose) }, false, -1f);
            SaveClip("FallR", new[] { (0f, basePose), (0.14f, Lean(-1f, 32f)), (0.32f, midR), (0.5f, lyingPose) }, false, -1f);

            // ---- more deaths: face down (forward), on the side, dropping to the knees first
            var spineAxis = lyingPose.bodyRotation * Vector3.up;
            var faceDown = lyingPose; faceDown.muscles = (float[])lyingPose.muscles.Clone();
            faceDown.bodyRotation = Quaternion.AngleAxis(180f, Vector3.up) * Quaternion.AngleAxis(180f, spineAxis) * lyingPose.bodyRotation;
            faceDown.bodyPosition = new Vector3(-lyingPose.bodyPosition.x, lyingPose.bodyPosition.y, -lyingPose.bodyPosition.z);
            var hunch = PoseFrom(handler, animator, basePose, a => { Rot(a, HumanBodyBones.Spine, Vector3.right, 28); Rot(a, HumanBodyBones.Head, Vector3.right, 20); Rot(a, HumanBodyBones.LeftUpperArm, Vector3.right, -30); Rot(a, HumanBodyBones.RightUpperArm, Vector3.right, -30); Rot(a, HumanBodyBones.LeftUpperLeg, Vector3.right, -25); Rot(a, HumanBodyBones.LeftLowerLeg, Vector3.right, 40); });
            hunch.bodyPosition += new Vector3(0, -0.06f, 0.04f);
            SaveClip("DeathFwd", new[] { (0f, basePose), (0.22f, hunch), (0.55f, Lerp(hunch, faceDown, 0.5f)), (0.85f, faceDown), (1.3f, faceDown) }, false, -1f);

            var sideLie = lyingPose; sideLie.muscles = (float[])lyingPose.muscles.Clone();
            sideLie.bodyRotation = Quaternion.AngleAxis(80f, spineAxis) * lyingPose.bodyRotation;
            SaveClip("DeathSide", new[] { (0f, basePose), (0.25f, Lean(1f, 30f)), (0.6f, Lerp(Lean(1f, 34f), sideLie, 0.6f)), (0.9f, sideLie), (1.3f, sideLie) }, false, -1f);

            var knees = PoseFrom(handler, animator, basePose, a =>
            {
                Rot(a, HumanBodyBones.LeftUpperLeg, Vector3.right, -8); Rot(a, HumanBodyBones.LeftLowerLeg, Vector3.right, 105);
                Rot(a, HumanBodyBones.RightUpperLeg, Vector3.right, -8); Rot(a, HumanBodyBones.RightLowerLeg, Vector3.right, 105);
                Rot(a, HumanBodyBones.Spine, Vector3.right, 18); Rot(a, HumanBodyBones.Head, Vector3.right, 25);
                Rot(a, HumanBodyBones.LeftUpperArm, Vector3.forward, 10); Rot(a, HumanBodyBones.RightUpperArm, Vector3.forward, -10);
            });
            knees.bodyPosition = new Vector3(basePose.bodyPosition.x, basePose.bodyPosition.y * 0.62f, basePose.bodyPosition.z);
            SaveClip("DeathKnees", new[] { (0f, basePose), (0.3f, knees), (0.75f, knees), (1.1f, Lerp(knees, faceDown, 0.6f)), (1.35f, faceDown), (1.6f, faceDown) }, false, -1f);

            handler.Dispose();
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
        }

        static HumanPose PoseFrom(HumanPoseHandler handler, Animator a, HumanPose basePose, Action<Animator> adjust)
        {
            var p = basePose;
            p.muscles = (float[])basePose.muscles.Clone();
            handler.SetHumanPose(ref p);
            adjust(a);
            var o = new HumanPose();
            handler.GetHumanPose(ref o);
            o.bodyPosition = basePose.bodyPosition;
            o.bodyRotation = basePose.bodyRotation;
            return o;
        }

        static HumanPose Lerp(HumanPose a, HumanPose b, float t)
        {
            var o = new HumanPose
            {
                bodyPosition = Vector3.Lerp(a.bodyPosition, b.bodyPosition, t),
                bodyRotation = Quaternion.Slerp(a.bodyRotation, b.bodyRotation, t),
                muscles = new float[a.muscles.Length],
            };
            for (int i = 0; i < o.muscles.Length; i++) o.muscles[i] = Mathf.Lerp(a.muscles[i], b.muscles[i], t);
            return o;
        }

        static void Rot(Animator a, HumanBodyBones bone, Vector3 axis, float deg)
        {
            var t = a.GetBoneTransform(bone);
            if (t == null) return;
            t.rotation = Quaternion.AngleAxis(deg, axis) * t.rotation;
        }

        /// <summary>Clip curve property name for HumanTrait.MuscleName[i] (fingers use "LeftHand.Index.1 Stretched").</summary>
        static string MuscleProp(string muscle)
        {
            string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
            foreach (var side in new[] { "Left", "Right" })
                foreach (var f in fingers)
                {
                    string pre = side + " " + f + " ";
                    if (muscle.StartsWith(pre)) return side + "Hand." + f + "." + muscle.Substring(pre.Length).Replace(" ", ".").Replace(".Stretched", " Stretched").Replace("Spread", "Spread");
                }
            return muscle;
        }

        static HumanPose SampleClip(AnimationClip clip, float time)
        {
            var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount], bodyRotation = Quaternion.identity };
            if (clip == null) return pose;
            var curves = new Dictionary<string, AnimationCurve>();
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
                curves[b.propertyName] = AnimationUtility.GetEditorCurve(clip, b);
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
                if (curves.TryGetValue(MuscleProp(HumanTrait.MuscleName[i]), out var c)) pose.muscles[i] = c.Evaluate(time);
            float E(string n, float d) => curves.TryGetValue(n, out var c) ? c.Evaluate(time) : d;
            pose.bodyPosition = new Vector3(E("RootT.x", 0), E("RootT.y", 1), E("RootT.z", 0));
            var q = new Quaternion(E("RootQ.x", 0), E("RootQ.y", 0), E("RootQ.z", 0), E("RootQ.w", 1));
            pose.bodyRotation = q.normalized;
            return pose;
        }

        static void SaveClip(string name, (float t, HumanPose pose)[] keys, bool loop, float hitTime)
        {
            var clip = new AnimationClip { name = name, frameRate = 30 };
            int n = HumanTrait.MuscleCount;
            for (int i = 0; i < n; i++)
            {
                var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.pose.muscles[i])).ToArray());
                for (int k = 0; k < curve.length; k++) AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.ClampedAuto);
                for (int k = 0; k < curve.length; k++) AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), MuscleProp(HumanTrait.MuscleName[i])), curve);
            }
            void Root(string prop, Func<HumanPose, float> f)
            {
                var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.t, f(k.pose))).ToArray());
                for (int k = 0; k < curve.length; k++) { AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.ClampedAuto); AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.ClampedAuto); }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), prop), curve);
            }
            Root("RootT.x", p => p.bodyPosition.x); Root("RootT.y", p => p.bodyPosition.y); Root("RootT.z", p => p.bodyPosition.z);
            Root("RootQ.x", p => p.bodyRotation.x); Root("RootQ.y", p => p.bodyRotation.y); Root("RootQ.z", p => p.bodyRotation.z); Root("RootQ.w", p => p.bodyRotation.w);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (hitTime >= 0) AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { functionName = "OnAnimHit", time = hitTime } });

            string path = $"{CustomClipDir}/{name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
            }
            else AssetDatabase.CreateAsset(clip, path);
            report.AppendLine($"custom clip {name} len={clip.length:0.00} loop={loop}");
        }

        // ------------------------------------------------------------------ animator controller
        static void CreateAnimator()
        {
            if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var sm = ctrl.layers[0].stateMachine;

            AnimatorState Loco(string name, (string clip, float speed)[] motions)
            {
                var tree = new BlendTree { name = name + "Tree", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(tree, ctrl);
                foreach (var m in motions) tree.AddChild(Clip(m.clip), m.speed);
                var st = sm.AddState(name);
                st.motion = tree;
                return st;
            }

            var loco = Loco("Loco", new[] { ("FightIdle", 0f), ("Walk_Loop", 1.6f), ("Jog_Fwd_Loop", 3.6f), ("Sprint_Loop", 6f) });
            sm.defaultState = loco;
            Loco("LocoHurt", new[] { ("Zombie_Idle_Loop", 0f), ("Zombie_Walk_Fwd_Loop", 1.2f) });

            AnimatorState S(string name, string clip, float speed = 1f, bool backToLoco = false, float exit = 0.9f)
            {
                var st = sm.AddState(name);
                st.motion = Clip(clip);
                st.speed = speed;
                if (backToLoco)
                {
                    var tr = st.AddTransition(loco);
                    tr.hasExitTime = true;
                    tr.exitTime = exit;
                    tr.duration = 0.12f;
                    tr.hasFixedDuration = true;
                }
                return st;
            }

            S("Punch1", "Punch_Jab", 1.15f, true, 0.85f);
            S("Punch2", "Punch_Cross", 1.1f, true, 0.85f);
            S("Punch3", "Melee_Hook", 1.1f, true, 0.85f);
            S("Kick", "Kick", 1f, true, 0.92f);
            S("Bat", "Sword_Regular_A", 1.1f, true, 0.85f);
            S("Shovel", "Sword_Regular_B", 1f, true, 0.85f);
            S("SpinKick", "SpinKick");
            S("Tornado", "TornadoSpin");
            S("Slam", "Slam", 1f, true, 0.95f);
            S("Flex", "Flex", 1f, true, 0.95f);
            S("HitBig", "Hit_Knockback", 1.2f, true, 0.8f);
            S("Fall", "Fall");
            S("Lying", "LayToIdle", 0f);
            S("GetUp", "LayToIdle", 1.3f, true, 0.92f);
            S("Death", "Death01");
            S("Dizzy", "Zombie_Idle_Loop", 0.8f);
            S("Dance", "Dance_Loop");
            S("Yes", "Yes", 1f, true, 0.9f);
            S("Wake", "Idle_No_Loop", 1.4f, true, 0.6f);
            S("Dodge", "Roll", 1.3f, true, 0.85f);
            S("Air", "Jump_Loop");
            S("Guard", "Punch_Enter", 1f, true, 0.95f);
            S("StaggerL", "StaggerL", 1f, true, 0.9f);
            S("StaggerR", "StaggerR", 1f, true, 0.9f);
            S("FallL", "FallL");
            S("FallR", "FallR");
            S("DeathFwd", "DeathFwd");
            S("DeathSide", "DeathSide");
            S("DeathKnees", "DeathKnees");
            S("Cocky", "Idle_FoldArms_Loop", 1f, true, 0.7f);

            // upper-body hit reaction layer (light hits: head / chest jolt while legs keep walking)
            var mask = new AvatarMask { name = "UpperBody" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (var p in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
                mask.SetHumanoidBodyPartActive(p, true);
            AssetDatabase.AddObjectToAsset(mask, ctrl);

            var layer = new AnimatorControllerLayer
            {
                name = "Hit",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = mask,
                stateMachine = new AnimatorStateMachine { name = "Hit", hideFlags = HideFlags.HideInHierarchy },
            };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, ctrl);
            var empty = layer.stateMachine.AddState("Empty");
            layer.stateMachine.defaultState = empty;
            foreach (var (n, c) in new[] { ("HitHead", "Hit_Head"), ("HitChest", "Hit_Chest") })
            {
                var st = layer.stateMachine.AddState(n);
                st.motion = Clip(c);
                st.speed = 1.4f;
                var tr = st.AddTransition(empty);
                tr.hasExitTime = true;
                tr.exitTime = 0.75f;
                tr.duration = 0.15f;
                tr.hasFixedDuration = true;
            }
            ctrl.AddLayer(layer);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            report.AppendLine("animator: " + string.Join(",", sm.states.Select(s => s.state.name)));
        }

        // ------------------------------------------------------------------ scene, player, renderer
        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = new GameObject("Game");
            game.AddComponent<GameManager>();
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.5f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.35f, 0.28f, 0.25f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.1f, 0.1f);
            RenderSettings.fog = false;
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            if (File.Exists("Assets/Settings/SampleSceneProfile.asset")) AssetDatabase.DeleteAsset("Assets/Settings/SampleSceneProfile.asset");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Berk";
            PlayerSettings.productName = "Arena Dovusu";
            PlayerSettings.defaultScreenWidth = 608;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            AddPortraitGameViewSize();
        }

        public const int FighterLayer = 8;

        static void ConfigureRenderer()
        {
            var xrayMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_XRay.mat");
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                // fighters are drawn by their own pass that marks the stencil, then the x-ray pass draws their
                // silhouette where a wall hides them (but not where another fighter does)
                data.opaqueLayerMask = ~(1 << FighterLayer);
                for (int i = data.rendererFeatures.Count - 1; i >= 0; i--)
                {
                    var old = data.rendererFeatures[i];
                    if (old != null && (old.name == "Fighters" || old.name == "FighterXRay"))
                    {
                        data.rendererFeatures.RemoveAt(i);
                        Object.DestroyImmediate(old, true);
                    }
                }
                var draw = ScriptableObject.CreateInstance<RenderObjects>();
                draw.name = "Fighters";
                draw.settings.Event = RenderPassEvent.AfterRenderingOpaques;
                draw.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
                draw.settings.filterSettings.LayerMask = 1 << FighterLayer;
                draw.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
                draw.settings.stencilSettings.overrideStencilState = true;
                draw.settings.stencilSettings.stencilReference = 1;
                draw.settings.stencilSettings.stencilCompareFunction = CompareFunction.Always;
                draw.settings.stencilSettings.passOperation = StencilOp.Replace;
                var xr = ScriptableObject.CreateInstance<RenderObjects>();
                xr.name = "FighterXRay";
                xr.settings.Event = RenderPassEvent.AfterRenderingOpaques;
                xr.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
                xr.settings.filterSettings.LayerMask = 1 << FighterLayer;
                xr.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.Material;
                xr.settings.overrideMaterial = xrayMat;
                xr.settings.overrideDepthState = true;
                xr.settings.depthCompareFunction = CompareFunction.Greater;
                xr.settings.enableWrite = false;
                xr.settings.stencilSettings.overrideStencilState = true;
                xr.settings.stencilSettings.stencilReference = 1;
                xr.settings.stencilSettings.stencilCompareFunction = CompareFunction.NotEqual;
                foreach (var f in new ScriptableRendererFeature[] { draw, xr })
                {
                    AssetDatabase.AddObjectToAsset(f, data);
                    data.rendererFeatures.Add(f);
                }
                var so = new SerializedObject(data);
                var map = so.FindProperty("m_RendererFeatureMap");
                if (map != null)
                {
                    map.arraySize = data.rendererFeatures.Count;
                    for (int i = 0; i < data.rendererFeatures.Count; i++)
                        if (data.rendererFeatures[i] != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string g2, out long lid))
                            map.GetArrayElementAtIndex(i).longValue = lid;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
            report.AppendLine("renderer: fighters on layer " + FighterLayer + " + x-ray silhouettes");

            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                foreach (var f in data.rendererFeatures)
                    if (f != null && f.GetType().Name.Contains("AmbientOcclusion")) f.SetActive(false);
                EditorUtility.SetDirty(data);
            }
            // characters use blob shadows (doc §8.1); no shadow maps on the integrated GPU
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                void SetB(string n, bool v) { var p = so.FindProperty(n); if (p != null) p.boolValue = v; }
                void SetI(string n, int v) { var p = so.FindProperty(n); if (p != null) p.intValue = v; }
                SetB("m_MainLightShadowsSupported", false);
                SetB("m_AdditionalLightShadowsSupported", false);
                SetI("m_MSAA", 2);
                SetI("m_AdditionalLightsPerObjectLimit", 4);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Adds a 1080x1920 portrait size to the Game view (reflection; harmless if Unity changes the API).</summary>
        static void AddPortraitGameViewSize()
        {
            try
            {
                var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
                var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singleType.GetProperty("instance").GetValue(null);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
                var groupType = group.GetType();
                int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
                for (int i = 0; i < count; i++)
                {
                    var sz = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    var txt = (string)sz.GetType().GetProperty("baseText").GetValue(sz);
                    if (txt == "TikTok 1080x1920") return;
                }
                var sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
                var sizeTypeEnum = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeType.GetConstructor(new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
                var newSize = ctor.Invoke(new object[] { Enum.Parse(sizeTypeEnum, "FixedResolution"), 1080, 1920, "TikTok 1080x1920" });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                sizesType.GetMethod("SaveToHDD").Invoke(instance, null);
            }
            catch (Exception e) { Debug.Log("[Arena] Game view size eklenemedi (önemsiz): " + e.Message); }
        }
    }

    public static class BuildScript
    {
        public const string ExePath = "../Builds/Windows/ArenaDovusu.exe";

        [MenuItem("Arena/Windows için derle")]
        public static void BuildWindows()
        {
            ArenaSetup.AuditFighterMaterials();
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = ExePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[Arena] Derleme sonucu: " + report.summary.result + " -> " + Path.GetFullPath(ExePath) + " (" + report.summary.totalSize / (1024 * 1024) + " MB)");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        public static void SetupAndBuild()
        {
            ArenaSetup.RunAll();
            BuildWindows();
        }

        public static void RepairMaterialsAndBuild()
        {
            ArenaSetup.RepairVisualMaterialReferences();
            BuildWindows();
        }
    }
}
