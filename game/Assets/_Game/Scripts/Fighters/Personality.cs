namespace Arena
{
    /// <summary>DESIGN §5.4. Every costume has one personality, so a viewer's fighter behaves the same every stream.</summary>
    public enum PersonalityType { Aggressive, Opportunist, Cautious, Vindictive, Showman }

    /// <summary>Weights the utility brain multiplies its option scores with.</summary>
    public struct PersonalityWeights
    {
        public float closeness;   // prefer the nearest target
        public float lowHpHunt;   // prefer weak targets
        public float caution;     // strafe / keep distance / flee early
        public float vengeance;   // chase whoever hit me
        public float showoff;     // taunt and dance after a knock-down
        public float aggression;  // base attack desire
        public float kickBias;    // 0..1 prefers kicks over punches
    }

    public static class Personality
    {
        public static PersonalityWeights For(PersonalityType t)
        {
            switch (t)
            {
                case PersonalityType.Aggressive:
                    return new PersonalityWeights { closeness = 1.0f, lowHpHunt = 0.2f, caution = 0.05f, vengeance = 0.4f, showoff = 0.2f, aggression = 0.85f, kickBias = 0.35f };
                case PersonalityType.Opportunist:
                    return new PersonalityWeights { closeness = 0.4f, lowHpHunt = 1.0f, caution = 0.35f, vengeance = 0.2f, showoff = 0.25f, aggression = 0.55f, kickBias = 0.25f };
                case PersonalityType.Cautious:
                    return new PersonalityWeights { closeness = 0.3f, lowHpHunt = 0.4f, caution = 1.0f, vengeance = 0.2f, showoff = 0.1f, aggression = 0.35f, kickBias = 0.6f };
                case PersonalityType.Vindictive:
                    return new PersonalityWeights { closeness = 0.6f, lowHpHunt = 0.3f, caution = 0.2f, vengeance = 1.0f, showoff = 0.15f, aggression = 0.6f, kickBias = 0.4f };
                default: // Showman
                    return new PersonalityWeights { closeness = 0.6f, lowHpHunt = 0.3f, caution = 0.25f, vengeance = 0.3f, showoff = 1.0f, aggression = 0.55f, kickBias = 0.5f };
            }
        }

        public static string Label(PersonalityType t)
        {
            switch (t)
            {
                case PersonalityType.Aggressive: return "Saldırgan";
                case PersonalityType.Opportunist: return "Fırsatçı";
                case PersonalityType.Cautious: return "Temkinli";
                case PersonalityType.Vindictive: return "Kinci";
                default: return "Şovmen";
            }
        }
    }
}
