using UnityEngine;

namespace Arena
{
    /// <summary>Plain starter clothes, then readable support / gift stages. All changes are cosmetic.</summary>
    public static class Gear
    {
        static GameConfig Cfg => GameConfig.Current;
        static readonly Color Slate = new Color(0.26f, 0.32f, 0.38f);
        static readonly Color Blue = new Color(0.16f, 0.46f, 0.72f);
        static readonly Color Gold = new Color(1f, 0.74f, 0.22f);
        static readonly Color Silver = new Color(0.66f, 0.79f, 0.86f);
        static readonly Color Bronze = new Color(0.69f, 0.40f, 0.20f);
        static readonly Color Skin = new Color(0.98f, 0.82f, 0.66f);

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
            int level = 0;
            for (int i = 0; i < t.Length; i++) if (v.streamCoins >= t[i]) level = i + 1;
            return level;
        }

        /// <summary>
        /// Resolve imported material slots once. Runtime clothing never inherits an embedded FBX shader or
        /// its bright palette accents: skin and cloth both use the project's authored URP base material.
        /// </summary>
        public static void PrepareBase(FighterRig rig, Transform model)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var skin = new bool[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    skin[i] = source != null && source.name.IndexOf("skin", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (source == null || source.shader == null || !source.shader.isSupported)
                        Debug.LogWarning("[Arena] Replacing missing/unsupported fighter material on " + renderer.name + " slot " + i);
                    materials[i] = Mats.Lit(skin[i] ? Skin : Slate);
                }
                renderer.sharedMaterials = materials;
                rig.outfitRenderers.Add(renderer);
                rig.outfitMaterials.Add(materials);
                rig.outfitSkinSlots.Add(skin);
            }
        }

        static void DressBase(Fighter f, int support, int gift)
        {
            Color color = support >= 3 ? new Color(0.10f, 0.49f, 0.62f) : support >= 2 ? Blue
                : support >= 1 ? new Color(0.27f, 0.43f, 0.54f) : Slate;
            if (gift == 1) color = new Color(0.47f, 0.32f, 0.22f);
            else if (gift == 2) color = new Color(0.17f, 0.39f, 0.47f);
            else if (gift == 3) color = new Color(0.62f, 0.39f, 0.12f);
            else if (gift == 4) color = new Color(0.27f, 0.16f, 0.45f);
            else if (gift >= 5) color = new Color(0.80f, 0.84f, 0.87f);
            var cloth = Mats.Lit(color);
            var skin = Mats.Lit(Skin);
            for (int i = 0; i < f.rig.outfitRenderers.Count; i++)
            {
                var materials = f.rig.outfitMaterials[i];
                var slots = f.rig.outfitSkinSlots[i];
                for (int j = 0; j < materials.Length; j++) materials[j] = slots[j] ? skin : cloth;
                f.rig.outfitRenderers[i].sharedMaterials = materials;
            }
        }

        public static void CheckLevelUp(Viewer v)
        {
            var f = v?.fighter;
            if (f == null || !f.IsAlive) return;
            int s = SupportLevel(v), g = GiftLevel(v);
            if (s <= f.gearSupport && g <= f.gearGift) return;
            bool giftUp = g > f.gearGift;
            Apply(f, true);
            var color = giftUp ? Gold : Blue;
            Fx.I.GroundRing(f.Pos, 0.35f, 1.2f, 0.65f, color);
            Fx.I.Sparkle(f.Pos, color, 10, 0.5f, 1.8f);
            Fx.I.Word(f.transform.position + Vector3.up * (f.HeadHeight + 0.35f), giftUp ? "VIP UP!" : "LEVEL UP!", color, 0.9f);
        }

        public static void Apply(Fighter f, bool animate)
        {
            foreach (var part in f.gearParts)
                if (part != null) { part.SetActive(false); Object.Destroy(part); }
            f.gearParts.Clear();
            int s = SupportLevel(f.viewer), g = GiftLevel(f.viewer);
            int oldS = Mathf.Max(0, f.gearSupport), oldG = Mathf.Max(0, f.gearGift);
            f.gearSupport = s; f.gearGift = g;
            DressBase(f, s, g);
            if (s == 0 && g == 0) return;
            var rig = f.rig;
            var i = rig.info;
            float h = i.height;
            var metal = Mats.Metal(g >= 3 ? Gold : g >= 2 ? Silver : g >= 1 ? Bronze : Blue);

            void Add(Transform holder, bool isNew)
            {
                Fighter.Layerize(holder.gameObject);
                f.gearParts.Add(holder.gameObject);
                if (animate && isNew) holder.gameObject.AddComponent<PopIn>();
            }

            // First earned detail: two small cuffs. Gifts use copper; support uses blue.
            void Cuff(Transform hand)
            {
                if (hand == null) return;
                var holder = Accessories.Holder("EarnedCuff", f.body, f.body.InverseTransformPoint(hand.position), hand);
                MeshKit.Part("Cuff", MeshKit.Torus(1f, 0.28f), metal, holder, Vector3.zero,
                    new Vector3(0, 0, 90), Vector3.one * h * 0.038f);
                Add(holder, (s > oldS || g > oldG) && oldS == 0 && oldG == 0);
            }
            Cuff(rig.handL); Cuff(rig.handR);

            if (s >= 2 || g >= 2)
            {
                var pads = Accessories.Holder("EarnedShoulders", f.body, i.chest + Vector3.up * h * 0.02f, rig.chest);
                float width = g >= 4 ? 0.105f : 0.085f;
                MeshKit.Part("Left", MeshKit.Sphere, metal, pads, new Vector3(-i.chestHalfW * 1.15f, 0, 0), Vector3.zero, new Vector3(h * width, h * 0.055f, h * 0.09f));
                MeshKit.Part("Right", MeshKit.Sphere, metal, pads, new Vector3(i.chestHalfW * 1.15f, 0, 0), Vector3.zero, new Vector3(h * width, h * 0.055f, h * 0.09f));
                Add(pads, oldS < 2 && oldG < 2);
                var belt = Accessories.Holder("EarnedBelt", f.body, i.hips + Vector3.up * h * 0.01f, rig.hips);
                MeshKit.Part("Belt", MeshKit.Cylinder, metal, belt, Vector3.zero, Vector3.zero, new Vector3(i.waistR * 2.15f, h * 0.018f, i.waistR * 2.15f));
                Add(belt, oldS < 2 && oldG < 2);
            }

            if (g >= 2)
            {
                var plate = Accessories.Holder("EarnedArmor", f.body, i.chest, rig.chest);
                float depth = i.chestFrontZ - i.chestBackZ;
                MeshKit.Part("Plate", MeshKit.Cube, metal, plate,
                    new Vector3(0, -h * 0.025f, (i.chestFrontZ + i.chestBackZ) * 0.5f - i.chest.z), Vector3.zero,
                    new Vector3(i.chestHalfW * 2.05f, h * 0.15f, depth * 1.12f + 0.025f));
                Add(plate, oldG < 2);
            }

            // A single short cape replaces stacked costume capes, wings and fur. Silhouettes remain readable.
            if (s >= 3 || g >= 3)
            {
                Color capeColor = g >= 5 ? new Color(0.94f, 0.89f, 0.72f) : g >= 4 ? new Color(0.40f, 0.18f, 0.66f)
                    : g >= 3 ? new Color(0.72f, 0.12f, 0.16f) : Blue;
                var cape = Accessories.Holder("EarnedCape", f.body,
                    new Vector3(i.chest.x, i.chest.y + h * 0.04f, i.chestBackZ - 0.035f), rig.chest);
                MeshKit.Part("Cape", MeshKit.Cube, Mats.Lit(capeColor), cape, new Vector3(0, -h * 0.18f, -0.03f),
                    new Vector3(10, 0, 0), new Vector3(i.chestHalfW * 2.5f, h * 0.36f, 0.025f));
                MeshKit.Part("Hem", MeshKit.Cube, metal, cape, new Vector3(0, -h * 0.35f, -0.06f),
                    new Vector3(10, 0, 0), new Vector3(i.chestHalfW * 2.52f, h * 0.018f, 0.028f));
                Add(cape, s > oldS || g > oldG);
            }
            if (g >= 5)
            {
                var crest = Accessories.Holder("LegendCrest", f.body,
                    new Vector3(i.chest.x, i.chest.y + h * 0.035f, i.chestFrontZ + 0.025f), rig.chest);
                MeshKit.Part("Gem", MeshKit.Sphere, Mats.Emissive(new Color(0.45f, 0.85f, 1f), 1.3f), crest,
                    Vector3.zero, Vector3.zero, Vector3.one * h * 0.045f);
                Add(crest, oldG < 5);
            }
        }

        /// <summary>Leaderboard rank is a small shoulder medal; it never changes combat statistics.</summary>
        public static void SetRank(Fighter f, int rank)
        {
            int visibleRank = rank >= 1 && rank <= 3 ? rank : 0;
            if (f.gearRank == visibleRank) return;
            if (f.rankInsignia != null)
            {
                f.rankInsignia.gameObject.SetActive(false);
                Object.Destroy(f.rankInsignia.gameObject);
                f.rankInsignia = null;
            }
            f.gearRank = visibleRank;
            if (visibleRank == 0 || !f.IsAlive) return;
            var i = f.rig.info;
            var color = visibleRank == 1 ? Gold : visibleRank == 2 ? Silver : Bronze;
            var holder = Accessories.Holder("RankMedal", f.body,
                i.chest + new Vector3(-i.chestHalfW * 0.88f, i.height * 0.065f, 0), f.rig.chest);
            MeshKit.Part("Medal", MeshKit.Cylinder, Mats.Metal(color), holder,
                Vector3.zero, Vector3.zero, new Vector3(i.height * 0.075f, i.height * 0.013f, i.height * 0.075f));
            Fighter.Layerize(holder.gameObject);
            f.rankInsignia = holder;
        }
    }

    public class PopIn : MonoBehaviour
    {
        Vector3 target;
        float t;
        void Awake() { target = transform.localScale; transform.localScale = Vector3.zero; }
        void Update()
        {
            t += Time.deltaTime / 0.4f;
            float s = t < 0.6f ? Mathf.Lerp(0f, 1.18f, t / 0.6f) : Mathf.Lerp(1.18f, 1f, (t - 0.6f) / 0.4f);
            transform.localScale = target * s;
            if (t >= 1f) { transform.localScale = target; Destroy(this); }
        }
    }
}
