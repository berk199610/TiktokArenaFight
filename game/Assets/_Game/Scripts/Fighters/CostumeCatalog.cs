using UnityEngine;

namespace Arena
{
    /// <summary>Accessory pieces built from simple shapes and attached to head / chest / hip / back bones (DESIGN §7.1).</summary>
    public enum Acc
    {
        Mustache, GoldBelt, Beanie, Skirt, FurCape, RoundGlasses, BowTie, Crown, Cape, Headband, ChefHat, Apron,
        Afro, DiscoVest, SpaceHelmet, EyePatch, PirateHat, FlowerCrown, Vest, RobotHead, Antenna, Katana, Scarf, Horns,
    }

    public struct AccSpec
    {
        public Acc kind;
        public Color color;
        public AccSpec(Acc k, Color c) { kind = k; color = c; }
    }

    public class Costume
    {
        public int id;
        public string name;
        public string model;            // stickman base model (Resources/Stickman/<model>)
        public PersonalityType personality;
        public AccSpec[] accessories;
        public Color nameColor;
    }

    /// <summary>
    /// DESIGN §7.2: the first 12 costumes from the document + 3 extra (15). Everyone shares the same humanoid
    /// skeleton, so every animation works on every costume. Costume = base stickman model + accessories + personality.
    /// </summary>
    public static class CostumeCatalog
    {
        // Saved costume IDs still select the original personality. All newcomers now wear the same plain
        // brawler outfit; earned equipment is added by Gear instead of a random king / robot / wizard costume.
        public const string StarterModel = "Bandit";
        static readonly Color Gold = new Color(1f, 0.78f, 0.2f);
        static readonly Color Pink = new Color(1f, 0.45f, 0.72f);
        static readonly Color Red = new Color(0.86f, 0.12f, 0.15f);
        static readonly Color Black = new Color(0.08f, 0.08f, 0.1f);
        static readonly Color White = new Color(0.96f, 0.96f, 0.95f);
        static readonly Color Brown = new Color(0.36f, 0.22f, 0.12f);
        static readonly Color Fur = new Color(0.62f, 0.5f, 0.36f);
        static readonly Color Steel = new Color(0.7f, 0.74f, 0.8f);
        static readonly Color Purple = new Color(0.62f, 0.25f, 0.9f);
        static readonly Color Teal = new Color(0.1f, 0.75f, 0.7f);
        static readonly Color Blue = new Color(0.15f, 0.4f, 0.95f);
        static readonly Color Green = new Color(0.25f, 0.8f, 0.3f);

        public static readonly Costume[] All =
        {
            new Costume { id = 0, name = "Bıyıklı Pehlivan", model = "Bandit", personality = PersonalityType.Aggressive,
                accessories = new[] { new AccSpec(Acc.Mustache, Black), new AccSpec(Acc.GoldBelt, Gold) } },
            new Costume { id = 1, name = "Pembe Pompon", model = "Supergirl", personality = PersonalityType.Showman,
                accessories = new[] { new AccSpec(Acc.Beanie, Pink), new AccSpec(Acc.Skirt, Pink) } },
            new Costume { id = 2, name = "Viking", model = "Viking", personality = PersonalityType.Aggressive,
                accessories = new[] { new AccSpec(Acc.FurCape, Fur) } },
            new Costume { id = 3, name = "Profesör", model = "Business woman", personality = PersonalityType.Cautious,
                accessories = new[] { new AccSpec(Acc.RoundGlasses, Black), new AccSpec(Acc.BowTie, Red) } },
            new Costume { id = 4, name = "Altın Kral", model = "Knight", personality = PersonalityType.Showman,
                accessories = new[] { new AccSpec(Acc.Crown, Gold), new AccSpec(Acc.Cape, Red) } },
            new Costume { id = 5, name = "Ninja", model = "Assasin", personality = PersonalityType.Opportunist,
                accessories = new[] { new AccSpec(Acc.Headband, Red) } },
            new Costume { id = 6, name = "Aşçı", model = "Sniper", personality = PersonalityType.Vindictive,
                accessories = new[] { new AccSpec(Acc.ChefHat, White), new AccSpec(Acc.Apron, White), new AccSpec(Acc.Mustache, Brown) } },
            new Costume { id = 7, name = "Disko", model = "Business woman", personality = PersonalityType.Showman,
                accessories = new[] { new AccSpec(Acc.Afro, Black), new AccSpec(Acc.DiscoVest, Purple) } },
            new Costume { id = 8, name = "Astronot", model = "Soldier", personality = PersonalityType.Cautious,
                accessories = new[] { new AccSpec(Acc.SpaceHelmet, new Color(0.6f, 0.85f, 1f)), new AccSpec(Acc.Antenna, Red) } },
            new Costume { id = 9, name = "Korsan", model = "Bandit", personality = PersonalityType.Opportunist,
                accessories = new[] { new AccSpec(Acc.PirateHat, Black), new AccSpec(Acc.EyePatch, Black) } },
            new Costume { id = 10, name = "Çiçek Taçlı", model = "Mage", personality = PersonalityType.Vindictive,
                accessories = new[] { new AccSpec(Acc.FlowerCrown, Pink), new AccSpec(Acc.Vest, Teal) } },
            new Costume { id = 11, name = "Robot", model = "Soldier", personality = PersonalityType.Aggressive,
                accessories = new[] { new AccSpec(Acc.RobotHead, Steel), new AccSpec(Acc.Antenna, Steel) } },
            new Costume { id = 12, name = "Samuray", model = "Samurai", personality = PersonalityType.Vindictive,
                accessories = new[] { new AccSpec(Acc.Katana, Black), new AccSpec(Acc.Headband, White) } },
            new Costume { id = 13, name = "Keskin Nişancı", model = "Sniper", personality = PersonalityType.Cautious,
                accessories = new[] { new AccSpec(Acc.Scarf, Green) } },
            new Costume { id = 14, name = "Süper Kız", model = "Supergirl", personality = PersonalityType.Showman,
                accessories = new[] { new AccSpec(Acc.Cape, Blue) } },
        };

        public static int Count => All.Length;
        public static Costume Get(int id) => All[Mathf.Clamp(id, 0, All.Length - 1)];
    }
}
