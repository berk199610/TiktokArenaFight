using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Measurements of a posed character in body space (y up, z forward), taken once per base model from the
    /// baked skinned mesh: where the head is and how big, where the chest and hips are. Accessories are placed from these.
    /// </summary>
    public class RigInfo
    {
        public float height;
        public Vector3 headCenter; public float headR; public float headTop; public float faceZ;
        public Vector3 neck; public Vector3 chest; public float chestFrontZ; public float chestBackZ; public float chestHalfW;
        public Vector3 hips; public float waistR;
    }

    /// <summary>Bones + accessory roots of one fighter instance.</summary>
    public class FighterRig
    {
        public Transform head, neck, chest, hips, handR, handL, footR;
        public RigInfo info;
        public readonly List<Renderer> accessoryRenderers = new List<Renderer>();
        public readonly List<Color> accessoryColors = new List<Color>();
        public readonly List<Renderer> outfitRenderers = new List<Renderer>();
        public readonly List<Material[]> outfitMaterials = new List<Material[]>();
        public readonly List<bool[]> outfitSkinSlots = new List<bool[]>();
        public Transform crown;      // round top gifter crown (shown/hidden)
        public Transform weaponRoot; // bat / shovel
    }

    public static class Accessories
    {
        static readonly Dictionary<string, RigInfo> infoCache = new Dictionary<string, RigInfo>();

        /// <summary>Measure the model in its current (idle) pose. body = the transform whose space is "character space".</summary>
        public static RigInfo Measure(string key, Transform body, Animator a)
        {
            if (infoCache.TryGetValue(key, out var cached)) return cached;
            // Baked vertex space differs between importers / scale setups (this pack uses 100x bone scales), so try
            // the possible interpretations and keep the one whose height matches the renderer bounds.
            var smrs = body.GetComponentsInChildren<SkinnedMeshRenderer>();
            float boundsH = 0f;
            if (smrs.Length > 0)
            {
                var bb = smrs[0].bounds;
                foreach (var s in smrs) bb.Encapsulate(s.bounds);
                boundsH = bb.size.y;
            }
            List<Vector3> verts = null;
            float bestErr = float.MaxValue;
            var tmp = new Mesh();
            var buffer = new List<Vector3>();
            for (int variant = 0; variant < 3; variant++)
            {
                var list = new List<Vector3>();
                foreach (var smr in smrs)
                {
                    smr.BakeMesh(tmp, variant != 0);
                    tmp.GetVertices(buffer);
                    var m = variant == 1 ? Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one) : smr.transform.localToWorldMatrix;
                    foreach (var v in buffer) list.Add(body.InverseTransformPoint(m.MultiplyPoint3x4(v)));
                }
                if (list.Count == 0) continue;
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var v in list) { lo = Mathf.Min(lo, v.y); hi = Mathf.Max(hi, v.y); }
                float err = boundsH > 0.01f ? Mathf.Abs(Mathf.Log((hi - lo + 1e-4f) / boundsH)) : (variant == 0 ? 0f : 1f);
                if (err < bestErr) { bestErr = err; verts = list; }
            }
            Object.Destroy(tmp);
            if (verts == null) verts = new List<Vector3>();
            var info = new RigInfo();
            Vector3 P(HumanBodyBones b)
            {
                var t = a.GetBoneTransform(b);
                return t != null ? body.InverseTransformPoint(t.position) : Vector3.zero;
            }
            var head = P(HumanBodyBones.Head);
            var neck = P(HumanBodyBones.Neck);
            var chest = a.GetBoneTransform(HumanBodyBones.UpperChest) != null ? P(HumanBodyBones.UpperChest) : P(HumanBodyBones.Chest);
            var hips = P(HumanBodyBones.Hips);
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var v in verts) { minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y); }
            if (verts.Count == 0) { minY = 0; maxY = 1.8f; }
            info.height = maxY - minY;
            info.headTop = maxY;
            info.neck = neck; info.chest = chest; info.hips = hips;

            // head: vertices above the neck
            float headR = 0, faceZ = head.z;
            float headLowY = Mathf.Lerp(neck.y, head.y, 0.5f);
            foreach (var v in verts)
            {
                if (v.y < headLowY) continue;
                var d = new Vector2(v.x - head.x, v.z - head.z);
                headR = Mathf.Max(headR, d.magnitude);
                faceZ = Mathf.Max(faceZ, v.z);
            }
            headR = Mathf.Clamp(headR, info.height * 0.06f, info.height * 0.2f);
            info.headR = headR;
            info.faceZ = faceZ;
            info.headCenter = new Vector3(head.x, Mathf.Max(head.y + headR * 0.6f, info.headTop - headR * 1.05f), head.z);

            // chest slice
            float cf = chest.z, cb = chest.z, cw = 0;
            float wr = 0;
            foreach (var v in verts)
            {
                if (Mathf.Abs(v.y - chest.y) < info.height * 0.06f)
                {
                    cf = Mathf.Max(cf, v.z); cb = Mathf.Min(cb, v.z);
                    if (Mathf.Abs(v.x - chest.x) < info.height * 0.2f) cw = Mathf.Max(cw, Mathf.Abs(v.x - chest.x));
                }
                if (Mathf.Abs(v.y - hips.y) < info.height * 0.05f)
                    wr = Mathf.Max(wr, new Vector2(v.x - hips.x, v.z - hips.z).magnitude);
            }
            info.chestFrontZ = cf; info.chestBackZ = cb;
            // widths from the bones (vertex slices also catch the arms on these thin stickmen)
            var la = P(HumanBodyBones.LeftUpperArm); var ra = P(HumanBodyBones.RightUpperArm);
            var ll = P(HumanBodyBones.LeftUpperLeg); var rl = P(HumanBodyBones.RightUpperLeg);
            float shoulders = (la - ra).magnitude * 0.5f;
            float hipsW = (ll - rl).magnitude * 0.5f;
            info.chestHalfW = shoulders > 0.01f ? Mathf.Clamp(shoulders * 0.85f, info.height * 0.04f, info.height * 0.1f) : Mathf.Clamp(cw, info.height * 0.04f, info.height * 0.1f);
            info.waistR = hipsW > 0.01f ? Mathf.Clamp(hipsW * 1.5f, info.height * 0.035f, info.height * 0.09f) : Mathf.Clamp(wr, info.height * 0.035f, info.height * 0.09f);
            float depth = info.chestFrontZ - info.chestBackZ;
            if (depth > info.height * 0.2f || depth < 0.01f) { info.chestFrontZ = chest.z + info.height * 0.06f; info.chestBackZ = chest.z - info.height * 0.06f; }
            if (neck == Vector3.zero) info.neck = Vector3.Lerp(chest, head, 0.6f);
            infoCache[key] = info;
            return info;
        }

        // ------------------------------------------------------------------ building
        public static void BuildAll(Costume c, FighterRig rig, Transform body, bool gold)
        {
            foreach (var a in c.accessories) Build(a.kind, gold ? GoldOf(a.color) : a.color, rig, body);
        }

        static Color GoldOf(Color c) => new Color(1f, 0.8f, 0.25f);

        public static void SetGold(FighterRig rig, bool gold)
        {
            for (int i = 0; i < rig.accessoryRenderers.Count; i++)
            {
                var r = rig.accessoryRenderers[i];
                if (r == null) continue;
                var baseColor = rig.accessoryColors[i];
                r.sharedMaterial = gold ? Mats.Metal(new Color(1f, 0.8f, 0.25f)) : MatFor(baseColor);
            }
        }

        static Material MatFor(Color c) => c.a < 0.99f ? Mats.Fx(false, Texture2D.whiteTexture, c) : Mats.Lit(c);

        /// <summary>Holder placed in body space, then re-parented to a bone (keeps the pose).</summary>
        struct RestPose { public Matrix4x4 bodyToBone; public Vector3 posInBody; public Quaternion rotInBody; public Vector3 scaleRatio; }
        static readonly Dictionary<Transform, RestPose> rest = new Dictionary<Transform, RestPose>();

        /// <summary>
        /// Remember where each bone sits in the idle pose (once, when the fighter is built). Gear added later
        /// (level ups, weapons, medals) is attached with these numbers, so it sits right even when the character
        /// is running, punching or lying on the ground at that moment (it used to float away from the body).
        /// </summary>
        public static void CaptureRest(Transform body, params Transform[] bones)
        {
            foreach (var b in bones)
            {
                if (b == null) continue;
                var bl = b.lossyScale; var dl = body.lossyScale;
                rest[b] = new RestPose
                {
                    bodyToBone = b.worldToLocalMatrix * body.localToWorldMatrix,
                    posInBody = body.InverseTransformPoint(b.position),
                    rotInBody = Quaternion.Inverse(body.rotation) * b.rotation,
                    scaleRatio = new Vector3(bl.x / dl.x, bl.y / dl.y, bl.z / dl.z),
                };
            }
        }

        /// <summary>Position of a bone in body space in the idle pose (falls back to the current pose).</summary>
        public static Vector3 RestPosition(Transform body, Transform bone)
        {
            if (bone != null && rest.TryGetValue(bone, out var r)) return r.posInBody;
            return bone != null ? body.InverseTransformPoint(bone.position) : Vector3.zero;
        }

        public static Transform Holder(string name, Transform body, Vector3 bodyPos, Transform bone)
        {
            var h = new GameObject(name).transform;
            if (bone != null && rest.TryGetValue(bone, out var r))
            {
                h.SetParent(bone, false);
                h.localPosition = r.bodyToBone.MultiplyPoint3x4(bodyPos);
                h.localRotation = Quaternion.Inverse(r.rotInBody);
                h.localScale = new Vector3(1f / r.scaleRatio.x, 1f / r.scaleRatio.y, 1f / r.scaleRatio.z);
                return h;
            }
            h.SetParent(body, false);
            h.localPosition = bodyPos;
            h.localRotation = Quaternion.identity;
            h.localScale = Vector3.one;
            if (bone != null) h.SetParent(bone, true);
            return h;
        }

        static void P(FighterRig rig, Transform holder, Mesh mesh, Color color, Vector3 pos, Vector3 euler, Vector3 scale, bool metal = false, bool emissive = false)
        {
            Material m = emissive ? Mats.Emissive(color, 2.5f) : metal ? Mats.Metal(color) : MatFor(color);
            var go = MeshKit.Part("acc", mesh, m, holder, pos, euler, scale);
            var r = go.GetComponent<Renderer>();
            if (!emissive)
            {
                rig.accessoryRenderers.Add(r);
                rig.accessoryColors.Add(color);
            }
        }

        public static void Build(Acc kind, Color col, FighterRig rig, Transform body)
        {
            var i = rig.info;
            float R = i.headR;
            var hc = i.headCenter;
            Transform H(string n) => Holder(n, body, hc, rig.head);
            switch (kind)
            {
                case Acc.Mustache:
                {
                    var h = Holder("Mustache", body, new Vector3(hc.x, hc.y - R * 0.3f, i.faceZ + R * 0.02f), rig.head);
                    P(rig, h, MeshKit.Capsule, col, new Vector3(-R * 0.32f, 0, 0), new Vector3(0, 0, 70), new Vector3(R * 0.22f, R * 0.32f, R * 0.18f));
                    P(rig, h, MeshKit.Capsule, col, new Vector3(R * 0.32f, 0, 0), new Vector3(0, 0, -70), new Vector3(R * 0.22f, R * 0.32f, R * 0.18f));
                    break;
                }
                case Acc.GoldBelt:
                {
                    var h = Holder("Belt", body, i.hips + Vector3.up * i.height * 0.02f, rig.hips);
                    P(rig, h, MeshKit.Cylinder, col, Vector3.zero, Vector3.zero, new Vector3(i.waistR * 2.3f, i.height * 0.03f, i.waistR * 2.3f), true);
                    P(rig, h, MeshKit.Cylinder, col, new Vector3(0, 0, i.waistR * 1.12f), new Vector3(90, 0, 0), new Vector3(i.waistR * 0.9f, 0.012f, i.waistR * 0.9f), true);
                    break;
                }
                case Acc.Beanie:
                {
                    var h = H("Beanie");
                    P(rig, h, MeshKit.Sphere, col, new Vector3(0, R * 0.25f, 0), Vector3.zero, new Vector3(R * 2.12f, R * 1.7f, R * 2.12f));
                    P(rig, h, MeshKit.Torus(1f, 0.18f), Color.Lerp(col, Color.white, 0.5f), new Vector3(0, R * 0.05f, 0), Vector3.zero, Vector3.one * R * 1.05f);
                    P(rig, h, MeshKit.Sphere, Color.white, new Vector3(0, R * 1.2f, 0), Vector3.zero, Vector3.one * R * 0.62f);
                    break;
                }
                case Acc.Skirt:
                {
                    var h = Holder("Skirt", body, i.hips + Vector3.down * i.height * 0.02f, rig.hips);
                    P(rig, h, MeshKit.Cone(1.6f, 0.75f, 18, false), col, new Vector3(0, -i.height * 0.07f, 0), Vector3.zero, new Vector3(i.waistR * 1.2f, i.height * 0.1f, i.waistR * 1.2f));
                    P(rig, h, MeshKit.Torus(1f, 0.12f), Color.white, new Vector3(0, -i.height * 0.07f, 0), Vector3.zero, Vector3.one * i.waistR * 1.95f);
                    break;
                }
                case Acc.FurCape:
                {
                    var h = Holder("FurCape", body, new Vector3(i.chest.x, i.chest.y + i.height * 0.04f, i.chestBackZ - 0.02f), rig.chest);
                    P(rig, h, MeshKit.Cube, col, new Vector3(0, -i.height * 0.15f, -0.02f), new Vector3(8, 0, 0), new Vector3(i.chestHalfW * 2.6f, i.height * 0.34f, 0.035f));
                    P(rig, h, MeshKit.Torus(1f, 0.45f), Color.Lerp(col, Color.white, 0.35f), new Vector3(0, i.height * 0.03f, i.chestHalfW * 0.6f), Vector3.zero, new Vector3(i.chestHalfW * 1.2f, i.chestHalfW * 1.0f, i.chestHalfW * 1.0f));
                    break;
                }
                case Acc.Cape:
                {
                    var h = Holder("Cape", body, new Vector3(i.chest.x, i.chest.y + i.height * 0.05f, i.chestBackZ - 0.025f), rig.chest);
                    P(rig, h, MeshKit.Cube, col, new Vector3(0, -i.height * 0.2f, -0.03f), new Vector3(10, 0, 0), new Vector3(i.chestHalfW * 2.4f, i.height * 0.44f, 0.025f));
                    break;
                }
                case Acc.RoundGlasses:
                {
                    var h = Holder("Glasses", body, new Vector3(hc.x, hc.y + R * 0.08f, i.faceZ + R * 0.04f), rig.head);
                    var torus = MeshKit.Torus(1f, 0.16f, 20, 6);
                    P(rig, h, torus, col, new Vector3(-R * 0.36f, 0, 0), new Vector3(90, 0, 0), Vector3.one * R * 0.3f, true);
                    P(rig, h, torus, col, new Vector3(R * 0.36f, 0, 0), new Vector3(90, 0, 0), Vector3.one * R * 0.3f, true);
                    P(rig, h, MeshKit.Cube, col, Vector3.zero, Vector3.zero, new Vector3(R * 0.18f, R * 0.05f, R * 0.05f), true);
                    P(rig, h, MeshKit.Quad, new Color(0.8f, 0.95f, 1f, 0.35f), new Vector3(-R * 0.36f, 0, 0.002f), new Vector3(0, 180, 0), Vector3.one * R * 0.55f);
                    P(rig, h, MeshKit.Quad, new Color(0.8f, 0.95f, 1f, 0.35f), new Vector3(R * 0.36f, 0, 0.002f), new Vector3(0, 180, 0), Vector3.one * R * 0.55f);
                    break;
                }
                case Acc.BowTie:
                {
                    var h = Holder("BowTie", body, new Vector3(i.neck.x, i.neck.y - i.height * 0.01f, i.chestFrontZ + 0.01f), rig.chest);
                    var cone = MeshKit.Cone(1f, 0f, 10);
                    P(rig, h, cone, col, new Vector3(-0.005f, 0, 0), new Vector3(0, 0, 90), new Vector3(0.045f, 0.075f, 0.03f));
                    P(rig, h, cone, col, new Vector3(0.005f, 0, 0), new Vector3(0, 0, -90), new Vector3(0.045f, 0.075f, 0.03f));
                    P(rig, h, MeshKit.Sphere, col * 0.8f, Vector3.zero, Vector3.zero, Vector3.one * 0.03f);
                    break;
                }
                case Acc.Crown:
                {
                    var h = Holder("Crown", body, new Vector3(hc.x, i.headTop - R * 0.12f, hc.z), rig.head);
                    BuildCrown(rig, h, col, R);
                    break;
                }
                case Acc.Headband:
                {
                    var h = H("Headband");
                    P(rig, h, MeshKit.Cylinder, col, new Vector3(0, R * 0.25f, 0), Vector3.zero, new Vector3(R * 2.1f, R * 0.13f, R * 2.1f));
                    P(rig, h, MeshKit.Cube, col, new Vector3(R * 0.15f, R * 0.1f, -R * 1.15f), new Vector3(30, 20, 0), new Vector3(R * 0.12f, R * 0.7f, R * 0.04f));
                    P(rig, h, MeshKit.Cube, col, new Vector3(-R * 0.15f, R * 0.1f, -R * 1.15f), new Vector3(30, -20, 0), new Vector3(R * 0.12f, R * 0.6f, R * 0.04f));
                    break;
                }
                case Acc.ChefHat:
                {
                    var h = Holder("ChefHat", body, new Vector3(hc.x, i.headTop - R * 0.3f, hc.z), rig.head);
                    P(rig, h, MeshKit.Cylinder, col, new Vector3(0, R * 0.45f, 0), Vector3.zero, new Vector3(R * 1.5f, R * 0.5f, R * 1.5f));
                    P(rig, h, MeshKit.Sphere, col, new Vector3(0, R * 1.25f, 0), Vector3.zero, new Vector3(R * 2.0f, R * 1.3f, R * 2.0f));
                    break;
                }
                case Acc.Apron:
                {
                    var h = Holder("Apron", body, new Vector3(i.chest.x, i.chest.y, i.chestFrontZ + 0.01f), rig.chest);
                    P(rig, h, MeshKit.Cube, col, new Vector3(0, -i.height * 0.12f, 0), Vector3.zero, new Vector3(i.chestHalfW * 1.6f, i.height * 0.3f, 0.015f));
                    break;
                }
                case Acc.Afro:
                {
                    var h = H("Afro");
                    P(rig, h, MeshKit.Sphere, col, new Vector3(0, R * 0.55f, -R * 0.25f), Vector3.zero, Vector3.one * R * 2.9f);
                    break;
                }
                case Acc.DiscoVest:
                {
                    var h = Holder("DiscoVest", body, i.chest, rig.chest);
                    float d = (i.chestFrontZ - i.chestBackZ);
                    P(rig, h, MeshKit.Cube, col, new Vector3(-i.chestHalfW * 0.55f, -i.height * 0.04f, (i.chestFrontZ + i.chestBackZ) * 0.5f - i.chest.z), Vector3.zero, new Vector3(i.chestHalfW * 0.9f, i.height * 0.2f, d * 1.15f + 0.02f), true);
                    P(rig, h, MeshKit.Cube, col, new Vector3(i.chestHalfW * 0.55f, -i.height * 0.04f, (i.chestFrontZ + i.chestBackZ) * 0.5f - i.chest.z), Vector3.zero, new Vector3(i.chestHalfW * 0.9f, i.height * 0.2f, d * 1.15f + 0.02f), true);
                    break;
                }
                case Acc.Vest:
                {
                    var h = Holder("Vest", body, i.chest, rig.chest);
                    float d = (i.chestFrontZ - i.chestBackZ);
                    P(rig, h, MeshKit.Cube, col, new Vector3(0, -i.height * 0.04f, (i.chestFrontZ + i.chestBackZ) * 0.5f - i.chest.z), Vector3.zero, new Vector3(i.chestHalfW * 2.1f, i.height * 0.2f, d * 1.12f + 0.02f));
                    break;
                }
                case Acc.SpaceHelmet:
                {
                    var h = H("SpaceHelmet");
                    P(rig, h, MeshKit.Sphere, new Color(col.r, col.g, col.b, 0.32f), new Vector3(0, 0, R * 0.05f), Vector3.zero, Vector3.one * R * 2.75f);
                    P(rig, h, MeshKit.Torus(1f, 0.2f), Color.white, new Vector3(0, -R * 1.05f, 0), Vector3.zero, Vector3.one * R * 1.05f, true);
                    break;
                }
                case Acc.EyePatch:
                {
                    var h = Holder("EyePatch", body, new Vector3(hc.x, hc.y + R * 0.08f, i.faceZ + R * 0.02f), rig.head);
                    P(rig, h, MeshKit.Cylinder, col, new Vector3(R * 0.36f, 0, 0), new Vector3(90, 0, 0), new Vector3(R * 0.42f, 0.01f, R * 0.42f));
                    var h2 = H("Strap");
                    P(rig, h2, MeshKit.Torus(1f, 0.04f), col, new Vector3(0, R * 0.12f, 0), new Vector3(0, 0, -18), Vector3.one * R * 1.03f);
                    break;
                }
                case Acc.PirateHat:
                {
                    var h = Holder("PirateHat", body, new Vector3(hc.x, i.headTop - R * 0.35f, hc.z), rig.head);
                    P(rig, h, MeshKit.Cylinder, col, Vector3.zero, Vector3.zero, new Vector3(R * 3.0f, R * 0.06f, R * 2.2f));
                    P(rig, h, MeshKit.Sphere, col, new Vector3(0, R * 0.2f, 0), Vector3.zero, new Vector3(R * 1.9f, R * 1.1f, R * 1.7f));
                    P(rig, h, MeshKit.Sphere, Color.white, new Vector3(0, R * 0.35f, R * 0.86f), Vector3.zero, Vector3.one * R * 0.32f);
                    break;
                }
                case Acc.FlowerCrown:
                {
                    var h = H("FlowerCrown");
                    Color[] petals = { new Color(1f, 0.4f, 0.7f), new Color(1f, 0.85f, 0.2f), new Color(0.6f, 0.4f, 1f), new Color(1f, 0.55f, 0.3f) };
                    int n = 9;
                    for (int k = 0; k < n; k++)
                    {
                        float a = k / (float)n * Mathf.PI * 2f;
                        var p = new Vector3(Mathf.Cos(a) * R * 0.98f, R * 0.42f, Mathf.Sin(a) * R * 0.98f);
                        P(rig, h, MeshKit.Sphere, petals[k % petals.Length], p, Vector3.zero, Vector3.one * R * 0.38f);
                    }
                    P(rig, h, MeshKit.Torus(1f, 0.08f), new Color(0.2f, 0.6f, 0.2f), new Vector3(0, R * 0.42f, 0), Vector3.zero, Vector3.one * R * 0.98f);
                    break;
                }
                case Acc.RobotHead:
                {
                    var h = H("RobotHead");
                    P(rig, h, MeshKit.Cube, col, new Vector3(0, R * 0.05f, 0), Vector3.zero, Vector3.one * R * 2.25f, true);
                    P(rig, h, MeshKit.Cube, new Color(0.2f, 1f, 1f), new Vector3(0, R * 0.2f, R * 1.13f), Vector3.zero, new Vector3(R * 1.6f, R * 0.35f, 0.01f), false, true);
                    P(rig, h, MeshKit.Cylinder, col * 0.8f, new Vector3(R * 1.15f, 0, 0), new Vector3(0, 0, 90), new Vector3(R * 0.5f, R * 0.1f, R * 0.5f), true);
                    P(rig, h, MeshKit.Cylinder, col * 0.8f, new Vector3(-R * 1.15f, 0, 0), new Vector3(0, 0, 90), new Vector3(R * 0.5f, R * 0.1f, R * 0.5f), true);
                    break;
                }
                case Acc.Antenna:
                {
                    var h = Holder("Antenna", body, new Vector3(hc.x, i.headTop, hc.z), rig.head);
                    P(rig, h, MeshKit.Cylinder, col, new Vector3(0, R * 0.4f, 0), Vector3.zero, new Vector3(R * 0.08f, R * 0.4f, R * 0.08f), true);
                    P(rig, h, MeshKit.Sphere, new Color(1f, 0.2f, 0.2f), new Vector3(0, R * 0.85f, 0), Vector3.zero, Vector3.one * R * 0.28f, false, true);
                    break;
                }
                case Acc.Katana:
                {
                    var h = Holder("Katana", body, new Vector3(i.chest.x, i.chest.y - i.height * 0.04f, i.chestBackZ - 0.04f), rig.chest);
                    P(rig, h, MeshKit.Cube, new Color(0.85f, 0.85f, 0.9f), new Vector3(0, 0.05f, 0), new Vector3(0, 0, 40), new Vector3(0.03f, i.height * 0.42f, 0.012f), true);
                    P(rig, h, MeshKit.Cube, col, new Vector3(-i.height * 0.14f, i.height * 0.2f, 0), new Vector3(0, 0, 40), new Vector3(0.04f, i.height * 0.13f, 0.03f));
                    break;
                }
                case Acc.Scarf:
                {
                    var h = Holder("Scarf", body, i.neck, rig.chest);
                    P(rig, h, MeshKit.Torus(1f, 0.38f), col, Vector3.zero, Vector3.zero, new Vector3(i.chestHalfW * 0.75f, i.chestHalfW * 0.6f, i.chestHalfW * 0.75f));
                    P(rig, h, MeshKit.Cube, col, new Vector3(i.chestHalfW * 0.35f, -i.height * 0.08f, i.chestHalfW * 0.6f), new Vector3(-10, 0, 8), new Vector3(i.chestHalfW * 0.4f, i.height * 0.16f, 0.02f));
                    break;
                }
                case Acc.Horns:
                {
                    var h = H("Horns");
                    var cone = MeshKit.Cone(1f, 0f, 10);
                    P(rig, h, cone, col, new Vector3(-R * 0.85f, R * 0.4f, 0), new Vector3(0, 0, 50), new Vector3(R * 0.25f, R * 0.8f, R * 0.25f));
                    P(rig, h, cone, col, new Vector3(R * 0.85f, R * 0.4f, 0), new Vector3(0, 0, -50), new Vector3(R * 0.25f, R * 0.8f, R * 0.25f));
                    break;
                }
            }
        }

        public static void BuildCrown(FighterRig rig, Transform h, Color col, float R)
        {
            P(rig, h, MeshKit.Cone(1.0f, 1.08f, 16, false), col, Vector3.zero, Vector3.zero, new Vector3(R * 0.75f, R * 0.4f, R * 0.75f), true);
            var cone = MeshKit.Cone(1f, 0f, 8);
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2f;
                P(rig, h, cone, col, new Vector3(Mathf.Cos(a) * R * 0.78f, R * 0.38f, Mathf.Sin(a) * R * 0.78f), Vector3.zero, new Vector3(R * 0.16f, R * 0.35f, R * 0.16f), true);
            }
            P(rig, h, MeshKit.Sphere, new Color(0.9f, 0.1f, 0.2f), new Vector3(0, R * 0.2f, R * 0.8f), Vector3.zero, Vector3.one * R * 0.22f, false, true);
        }

        /// <summary>Crown for the round's top gifter (DESIGN §4). Floats above the head, separate from costume accessories.</summary>
        public static Transform BuildGifterCrown(FighterRig rig, Transform body)
        {
            var i = rig.info;
            var h = Holder("GifterCrown", body, new Vector3(i.headCenter.x, i.headTop + i.headR * 0.45f, i.headCenter.z), rig.head);
            var gold = new Color(1f, 0.8f, 0.15f);
            var tmp = new FighterRig { info = i };
            BuildCrown(tmp, h, gold, i.headR * 1.1f);
            h.gameObject.SetActive(false);
            return h;
        }

        // ------------------------------------------------------------------ weapons (gift moves)
        public static Transform BuildWeapon(MoveKind kind, FighterRig rig, Transform body)
        {
            if (rig.handR == null) return null;
            // aligned with the character (forward = +Z) as in the idle pose, whatever the hand is doing right now
            var root = Holder(kind + "Weapon", body, RestPosition(body, rig.handR), rig.handR);
            float h = rig.info.height;
            var tmp = new FighterRig { info = rig.info };
            if (kind == MoveKind.Bat)
            {
                // wooden bat pointing forward/up out of the fist
                var bat = MeshKit.Part("Bat", MeshKit.Cone(0.5f, 1f, 12), Mats.Lit(new Color(0.62f, 0.4f, 0.2f)), root, new Vector3(0, 0, -0.05f), new Vector3(60, 0, 0), new Vector3(0.07f, h * 0.48f, 0.07f));
                MeshKit.Part("Tape", MeshKit.Cylinder, Mats.Lit(new Color(0.15f, 0.15f, 0.15f)), root, new Vector3(0, 0.02f, 0.0f), new Vector3(60, 0, 0), new Vector3(0.045f, 0.06f, 0.045f));
                bat.name = "Bat";
            }
            else
            {
                // shovel: handle + metal blade
                MeshKit.Part("Handle", MeshKit.Cylinder, Mats.Lit(new Color(0.55f, 0.36f, 0.18f)), root, new Vector3(0, h * 0.16f, h * 0.1f), new Vector3(60, 0, 0), new Vector3(0.035f, h * 0.3f, 0.035f));
                var blade = MeshKit.Part("Blade", MeshKit.Cube, Mats.Metal(new Color(0.7f, 0.72f, 0.75f)), root, new Vector3(0, h * 0.42f, h * 0.27f), new Vector3(60, 0, 0), new Vector3(h * 0.13f, h * 0.17f, 0.02f));
                blade.name = "Blade";
            }
            return root;
        }
    }
}
