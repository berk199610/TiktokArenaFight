using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Visible progression (user request): gifters get dressed by how much they sent this stream (tiara -> gold
    /// armour -> royal cape + crown -> purple king cape -> wings + halo); comment / like supporters level up too
    /// (wristbands -> shoulder pads + belt -> hero cape + star badge). Every step up pops in with a light pillar,
    /// sparkles and a "LEVEL UP!" / "VIP!" word so the viewer sees it happen.
    /// </summary>
    public static class Gear
    {
        static GameConfig Cfg => GameConfig.Current;

        public static int SupportLevel(Viewer v)
        {
            if (v == null || v.isBot) return 0;
            var l = Cfg.levels;
            if (v.streamLikes >= l.likesL3 || v.streamComments >= l.commentsL3) return 3;
            if (v.streamLikes >= l.likesL2 || v.streamComments >= l.commentsL2) return 2;
            if (v.streamLikes >= l.likesL1 || v.streamComments >= l.commentsL1) return 1;
            return 0;
        }

        public static int GiftLevel(Viewer v)
        {
            if (v == null || v.isBot) return 0;
            var t = Cfg.levels.giftCoins;
            int lvl = 0;
            for (int i = 0; i < t.Length; i++) if (v.streamCoins >= t[i]) lvl = i + 1;
            return lvl;
        }

        /// <summary>After an interaction: dress the fighter up if a level was reached, with the level-up show.</summary>
        public static void CheckLevelUp(Viewer v)
        {
            var f = v?.fighter;
            if (f == null || !f.IsAlive) return;
            int s = SupportLevel(v), g = GiftLevel(v);
            if (s > f.gearSupport || g > f.gearGift)
            {
                bool giftUp = g > f.gearGift;
                Apply(f, true);
                Celebrate(f, giftUp, giftUp ? g : s);
            }
        }

        static void Celebrate(Fighter f, bool gift, int level)
        {
            var col = gift ? new Color(1f, 0.82f, 0.25f, 1f) : new Color(0.4f, 0.8f, 1f, 1f);
            Fx.I.LightPillar(f.Pos, col, 0.9f);
            Fx.I.Sparkle(f.Pos, col, 18, 0.7f, 2.2f);
            string word = gift ? (level >= 5 ? "LEGEND!" : level >= 4 ? "KING!" : level >= 3 ? "ROYAL!" : "VIP!") : "LEVEL UP!";
            Fx.I.Word(f.transform.position + Vector3.up * (f.HeadHeight + 0.6f), word, col, 1.25f);
            GameManager.I.Emotes?.Show(f, gift ? "starstruck" : "happy", true);
        }

        /// <summary>Rebuild the fighter's gear for its viewer's current levels.</summary>
        public static void Apply(Fighter f, bool animate)
        {
            foreach (var go in f.gearParts) if (go != null) Object.Destroy(go);
            f.gearParts.Clear();
            var v = f.viewer;
            int s = SupportLevel(v), g = GiftLevel(v);
            int oldS = Mathf.Max(0, f.gearSupport), oldG = Mathf.Max(0, f.gearGift);
            f.gearSupport = s;
            f.gearGift = g;
            if (s == 0 && g == 0) return;
            var rig = f.rig;
            var i = rig.info;
            var body = f.body;
            float h = i.height, R = i.headR;
            var gold = Mats.Metal(new Color(1f, 0.78f, 0.25f));

            void Add(Transform holder, bool isNew)
            {
                Fighter.Layerize(holder.gameObject);
                f.gearParts.Add(holder.gameObject);
                if (animate && isNew) holder.gameObject.AddComponent<PopIn>();
            }

            // ---------------- supporters (comments / likes)
            if (s >= 1)
            {
                foreach (var hand in new[] { rig.handL, rig.handR })
                {
                    if (hand == null) continue;
                    var holder = Accessories.Holder("Wristband", body, body.InverseTransformPoint(hand.position), hand);
                    MeshKit.Part("Band", MeshKit.Torus(1f, 0.35f), Mats.Emissive(new Color(0.3f, 0.85f, 1f), 1.6f), holder, Vector3.zero, new Vector3(0, 0, 90), Vector3.one * h * 0.045f);
                    Add(holder, oldS < 1);
                }
            }
            if (s >= 2 && g < 4)
            {
                var holder = Accessories.Holder("ShoulderPads", body, i.chest + Vector3.up * h * 0.02f, rig.chest);
                var blue = Mats.Metal(new Color(0.25f, 0.55f, 1f));
                MeshKit.Part("PadL", MeshKit.Sphere, blue, holder, new Vector3(-i.chestHalfW * 1.25f, 0, 0), Vector3.zero, new Vector3(h * 0.1f, h * 0.06f, h * 0.1f));
                MeshKit.Part("PadR", MeshKit.Sphere, blue, holder, new Vector3(i.chestHalfW * 1.25f, 0, 0), Vector3.zero, new Vector3(h * 0.1f, h * 0.06f, h * 0.1f));
                Add(holder, oldS < 2);
                var belt = Accessories.Holder("HeroBelt", body, i.hips + Vector3.up * h * 0.02f, rig.hips);
                MeshKit.Part("Belt", MeshKit.Cylinder, blue, belt, Vector3.zero, Vector3.zero, new Vector3(i.waistR * 2.3f, h * 0.025f, i.waistR * 2.3f));
                Add(belt, oldS < 2);
            }
            if (s >= 3 && g < 3)
            {
                var cape = Accessories.Holder("HeroCape", body, new Vector3(i.chest.x, i.chest.y + h * 0.05f, i.chestBackZ - 0.035f), rig.chest);
                MeshKit.Part("Cape", MeshKit.Cube, Mats.Lit(new Color(0.15f, 0.45f, 1f)), cape, new Vector3(0, -h * 0.2f, -0.03f), new Vector3(10, 0, 0), new Vector3(i.chestHalfW * 2.6f, h * 0.44f, 0.022f));
                MeshKit.Part("Trim", MeshKit.Cube, gold, cape, new Vector3(0, -h * 0.42f, -0.065f), new Vector3(10, 0, 0), new Vector3(i.chestHalfW * 2.65f, h * 0.025f, 0.026f));
                Add(cape, oldS < 3);
                var badge = Accessories.Holder("StarBadge", body, new Vector3(i.chest.x, i.chest.y, i.chestFrontZ + 0.02f), rig.chest);
                var star = Icons.Get("star");
                MeshKit.Part("Star", MeshKit.Quad, Mats.Fx(false, star.texture, Color.white), badge, Vector3.zero, new Vector3(0, 180, 0), Vector3.one * h * 0.11f);
                Add(badge, oldS < 3);
            }

            // ---------------- gifters (coins this stream)
            if (g >= 1 && g < 3)
            {
                var tiara = Accessories.Holder("Tiara", body, new Vector3(i.headCenter.x, i.headTop - R * 0.18f, i.headCenter.z), rig.head);
                MeshKit.Part("Band", MeshKit.Cone(1f, 1.05f, 18, false), gold, tiara, Vector3.zero, Vector3.zero, new Vector3(R * 0.7f, R * 0.18f, R * 0.7f));
                var spike = MeshKit.Cone(1f, 0f, 8);
                for (int k = 0; k < 5; k++)
                {
                    float a = (k / 5f) * Mathf.PI * 2f;
                    MeshKit.Part("Spike", spike, gold, tiara, new Vector3(Mathf.Cos(a) * R * 0.72f, R * 0.16f, Mathf.Sin(a) * R * 0.72f), Vector3.zero, new Vector3(R * 0.1f, R * 0.22f, R * 0.1f));
                }
                Add(tiara, oldG < 1);
            }
            if (g >= 2)
            {
                var plate = Accessories.Holder("GoldArmor", body, i.chest, rig.chest);
                float d = i.chestFrontZ - i.chestBackZ;
                MeshKit.Part("Plate", MeshKit.Cube, gold, plate, new Vector3(0, -h * 0.03f, (i.chestFrontZ + i.chestBackZ) * 0.5f - i.chest.z), Vector3.zero, new Vector3(i.chestHalfW * 2.2f, h * 0.17f, d * 1.18f + 0.03f));
                MeshKit.Part("Gem", MeshKit.Sphere, Mats.Emissive(new Color(1f, 0.15f, 0.3f), 2f), plate, new Vector3(0, 0, i.chestFrontZ - i.chest.z + 0.03f), Vector3.zero, Vector3.one * h * 0.04f);
                Add(plate, oldG < 2);
            }
            if (g >= 3)
            {
                var crown = Accessories.Holder("RoyalCrown", body, new Vector3(i.headCenter.x, i.headTop - R * 0.1f, i.headCenter.z), rig.head);
                var tmp = new FighterRig { info = i };
                Accessories.BuildCrown(tmp, crown, new Color(1f, 0.8f, 0.2f), R * 1.05f);
                Add(crown, oldG < 3);
                var capeCol = g >= 4 ? new Color(0.45f, 0.12f, 0.75f) : new Color(0.8f, 0.1f, 0.15f);
                var cape = Accessories.Holder("RoyalCape", body, new Vector3(i.chest.x, i.chest.y + h * 0.06f, i.chestBackZ - 0.04f), rig.chest);
                MeshKit.Part("Cape", MeshKit.Cube, Mats.Lit(capeCol), cape, new Vector3(0, -h * 0.22f, -0.035f), new Vector3(12, 0, 0), new Vector3(i.chestHalfW * 3f, h * 0.5f, 0.025f));
                MeshKit.Part("Fur", MeshKit.Torus(1f, 0.4f), Mats.Lit(new Color(0.97f, 0.96f, 0.92f)), cape, new Vector3(0, h * 0.02f, i.chestHalfW * 0.5f), Vector3.zero, new Vector3(i.chestHalfW * 1.25f, i.chestHalfW, i.chestHalfW * 1.1f));
                Add(cape, oldG < 3 || (g >= 4 && oldG < 4));
            }
            if (g >= 4)
            {
                var pads = Accessories.Holder("GoldPads", body, i.chest + Vector3.up * h * 0.02f, rig.chest);
                MeshKit.Part("PadL", MeshKit.Sphere, gold, pads, new Vector3(-i.chestHalfW * 1.3f, 0, 0), Vector3.zero, new Vector3(h * 0.12f, h * 0.07f, h * 0.12f));
                MeshKit.Part("PadR", MeshKit.Sphere, gold, pads, new Vector3(i.chestHalfW * 1.3f, 0, 0), Vector3.zero, new Vector3(h * 0.12f, h * 0.07f, h * 0.12f));
                Add(pads, oldG < 4);
            }
            if (g >= 5)
            {
                var wings = Accessories.Holder("Wings", body, new Vector3(i.chest.x, i.chest.y + h * 0.03f, i.chestBackZ - 0.08f), rig.chest);
                var wingMat = Mats.Fx(false, FxTex.Soft, new Color(1f, 0.95f, 0.75f, 0.95f));
                MeshKit.Part("WingL", MeshKit.Sphere, wingMat, wings, new Vector3(-h * 0.22f, h * 0.06f, -0.02f), new Vector3(0, 0, 35), new Vector3(h * 0.4f, h * 0.16f, 0.03f));
                MeshKit.Part("WingR", MeshKit.Sphere, wingMat, wings, new Vector3(h * 0.22f, h * 0.06f, -0.02f), new Vector3(0, 0, -35), new Vector3(h * 0.4f, h * 0.16f, 0.03f));
                Add(wings, oldG < 5);
                var halo = Accessories.Holder("Halo", body, new Vector3(i.headCenter.x, i.headTop + R * 0.55f, i.headCenter.z), rig.head);
                MeshKit.Part("Ring", MeshKit.Torus(1f, 0.12f), Mats.Emissive(new Color(1f, 0.9f, 0.4f), 3f), halo, Vector3.zero, Vector3.zero, Vector3.one * R * 0.75f);
                Add(halo, oldG < 5);
            }
        }
    }

    /// <summary>Springy pop-in for new gear (0 -> 1.25 -> 1 in 0.4 s).</summary>
    public class PopIn : MonoBehaviour
    {
        Vector3 target;
        float t;
        void Awake() { target = transform.localScale; transform.localScale = Vector3.zero; }
        void Update()
        {
            t += Time.deltaTime / 0.4f;
            float s = t < 0.6f ? Mathf.Lerp(0f, 1.25f, t / 0.6f) : Mathf.Lerp(1.25f, 1f, (t - 0.6f) / 0.4f);
            transform.localScale = target * s;
            if (t >= 1f) { transform.localScale = target; Destroy(this); }
        }
    }
}
