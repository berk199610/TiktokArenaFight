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
        static readonly Color[] StarterPalette =
        {
            new Color(0.96f, 0.96f, 0.91f), // white
            new Color(1f, 0.44f, 0.66f),    // intentional cloth pink, independent of the fixed shader error
            new Color(1f, 0.82f, 0.18f),
            new Color(0.20f, 0.61f, 1f),
            new Color(0.34f, 0.84f, 0.40f),
            new Color(1f, 0.48f, 0.20f),
            new Color(0.18f, 0.84f, 0.80f),
            new Color(0.68f, 0.47f, 0.96f),
            new Color(1f, 0.33f, 0.33f),
        };

        /// <summary>Stable across launches, respawns and pooled fighters, including the crowd bots.</summary>
        public static Color StarterColor(Viewer viewer)
        {
            string id = viewer?.userId ?? "starter";
            uint hash = 2166136261;
            unchecked
            {
                for (int i = 0; i < id.Length; i++) hash = (hash ^ id[i]) * 16777619;
            }
            return StarterPalette[hash % (uint)StarterPalette.Length];
        }

        public static int SupportLevel(Viewer v)
        {
            if (v == null || v.isBot) return 0;
            var l = Cfg.levels;
            if (v.streamLikes >= l.likesL3 || v.streamComments >= l.commentsL3) return 3;
            if (v.streamLikes >= l.likesL2 || v.streamComments >= l.commentsL2) return 2;
            if (v.streamLikes >= l.likesL1 || v.streamComments >= l.commentsL1) return 1;
            return 0;
        }

        /// <summary>One number for the name tag: comments / likes reach Lv 1-3, only gifts reach Lv 4-8.</summary>
        public static int DisplayLevel(Viewer v)
        {
            int g = GiftLevel(v);
            return g > 0 ? 3 + g : SupportLevel(v);
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
            Color color = StarterColor(f.viewer);
            // Earned pieces carry the status; the viewer keeps their recognizable color underneath them.
            color = Color.Lerp(color, Color.white, gift >= 4 ? 0.16f : gift >= 2 ? 0.08f : support * 0.035f);
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
            Sfx.I?.LevelUp();
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
            var metal = Mats.Metal(g >= 1 ? Gold : Blue);

            void Add(Transform holder, bool isNew)
            {
                Fighter.Layerize(holder.gameObject);
                f.gearParts.Add(holder.gameObject);
                if (animate && isNew) holder.gameObject.AddComponent<PopIn>();
            }

            // First support detail is blue cuffs. A gift immediately adds visible gold equipment.
            void Cuff(Transform hand)
            {
                if (hand == null) return;
                var holder = Accessories.Holder("EarnedCuff", f.body, Accessories.RestPosition(f.body, hand), hand);
                MeshKit.Part("Cuff", MeshKit.Torus(1f, 0.28f), metal, holder, Vector3.zero,
                    new Vector3(0, 0, 90), Vector3.one * h * 0.038f);
                Add(holder, (s > oldS || g > oldG) && oldS == 0 && oldG == 0);
            }
            Cuff(rig.handL); Cuff(rig.handR);

            if (g >= 1)
            {
                var band = Accessories.Holder("GiftHeadband", f.body,
                    i.headCenter + Vector3.up * i.headR * 0.12f, rig.head);
                MeshKit.Part("Band", MeshKit.Torus(1f, g >= 3 ? 0.12f : 0.085f), metal, band,
                    Vector3.zero, Vector3.zero, Vector3.one * i.headR * 1.035f);
                if (g >= 3)
                {
                    // A small crest on the hat, distinct from the one floating crown reserved for the leader.
                    MeshKit.Part("Crest", MeshKit.Sphere,
                        Mats.Lit(g >= 5 ? new Color(0.42f, 0.85f, 1f) : new Color(0.86f, 0.16f, 0.22f)), band,
                        new Vector3(0, i.headR * 0.08f, i.headR * 1.02f), Vector3.zero,
                        new Vector3(i.headR * 0.27f, i.headR * 0.32f, i.headR * 0.14f));
                }
                Add(band, g > oldG);
            }

            if (s >= 2 || g >= 1)
            {
                var pads = Accessories.Holder("EarnedShoulders", f.body, i.chest + Vector3.up * h * 0.045f, rig.chest);
                float width = g >= 4 ? 0.18f : g >= 1 ? 0.155f : 0.085f;
                float spread = g >= 1 ? 1.85f : 1.15f;
                MeshKit.Part("Left", MeshKit.Sphere, metal, pads, new Vector3(-i.chestHalfW * spread, 0, 0), Vector3.zero, new Vector3(h * width, h * (g >= 1 ? 0.075f : 0.055f), h * (g >= 1 ? 0.14f : 0.09f)));
                MeshKit.Part("Right", MeshKit.Sphere, metal, pads, new Vector3(i.chestHalfW * spread, 0, 0), Vector3.zero, new Vector3(h * width, h * (g >= 1 ? 0.075f : 0.055f), h * (g >= 1 ? 0.14f : 0.09f)));
                Add(pads, g > oldG || (oldS < 2 && oldG == 0));
                var belt = Accessories.Holder("EarnedBelt", f.body, i.hips + Vector3.up * h * 0.01f, rig.hips);
                MeshKit.Part("Belt", MeshKit.Cylinder, metal, belt, Vector3.zero, Vector3.zero, new Vector3(i.waistR * 2.3f, h * (g >= 1 ? 0.028f : 0.018f), i.waistR * 2.3f));
                Add(belt, g > oldG || (oldS < 2 && oldG == 0));
            }

            if (g >= 2)
            {
                var plate = Accessories.Holder("EarnedArmor", f.body, i.chest, rig.chest);
                float depth = i.chestFrontZ - i.chestBackZ;
                MeshKit.Part("Plate", MeshKit.Cube, metal, plate,
                    new Vector3(0, -h * 0.025f, (i.chestFrontZ + i.chestBackZ) * 0.5f - i.chest.z), Vector3.zero,
                    new Vector3(i.chestHalfW * 2.65f, h * 0.20f, depth * 1.35f + 0.035f));
                Add(plate, oldG < 2);
            }

            // A single short cape replaces stacked costume capes, wings and fur. Silhouettes remain readable.
            if (s >= 3 || g >= 1)
            {
                Color capeColor = g >= 5 ? new Color(0.94f, 0.89f, 0.72f) : g >= 4 ? new Color(0.40f, 0.18f, 0.66f)
                    : g >= 1 ? new Color(0.80f, 0.13f, 0.22f) : Blue;
                float length = g >= 3 ? 0.47f : g >= 1 ? 0.29f : 0.36f;
                float width = g >= 3 ? 3.6f : g >= 1 ? 3.3f : 2.5f;
                var cape = Accessories.Holder("EarnedCape", f.body,
                    new Vector3(i.chest.x, i.chest.y + h * 0.07f, i.chestBackZ - 0.055f), rig.chest);
                MeshKit.Part("Cape", MeshKit.Cube, Mats.Lit(capeColor), cape, new Vector3(0, -h * length * 0.5f, -0.04f),
                    new Vector3(14, 0, 0), new Vector3(i.chestHalfW * width, h * length, 0.028f));
                MeshKit.Part("Hem", MeshKit.Cube, metal, cape, new Vector3(0, -h * length * 0.97f, -0.04f - h * length * 0.12f),
                    new Vector3(14, 0, 0), new Vector3(i.chestHalfW * (width + 0.02f), h * 0.022f, 0.035f));
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
