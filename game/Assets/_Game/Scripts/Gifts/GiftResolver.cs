using System;
using System.Collections.Generic;

namespace Arena
{
    public enum MoveKind { None = -1, PowerPunch = 0, Bat = 1, Shovel = 2, SpinKick = 3, Shockwave = 4, Giant = 5, Tornado = 6 }

    /// <summary>TikTok gift name -> coin value (from config). Used when an event carries a name but no coin value.</summary>
    public static class GiftCatalog
    {
        static Dictionary<string, int> byName;

        public static void Rebuild(GameConfig cfg)
        {
            byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in cfg.gifts.catalog)
                if (!string.IsNullOrEmpty(g.name)) byName[g.name] = g.coins;
        }

        public static int CoinsFor(string giftName, int fallback)
        {
            if (fallback > 0) return fallback;
            if (byName == null) Rebuild(GameConfig.Current);
            if (!string.IsNullOrEmpty(giftName) && byName.TryGetValue(giftName, out int c)) return c;
            return Math.Max(1, fallback);
        }
    }

    /// <summary>Coin value -> tier -> special move (DESIGN §4).</summary>
    public static class GiftResolver
    {
        public static int TierIndex(int coins)
        {
            var tiers = GameConfig.Current.gifts.tiers;
            int best = -1;
            for (int i = 0; i < tiers.Length; i++)
                if (coins >= tiers[i].minCoins) best = i;
            return best;
        }

        public static MoveKind Resolve(int coins)
        {
            int i = TierIndex(coins);
            if (i < 0) return MoveKind.None;
            return Enum.TryParse(GameConfig.Current.gifts.tiers[i].move, out MoveKind k) ? k : MoveKind.None;
        }

        public static string Label(MoveKind k)
        {
            foreach (var t in GameConfig.Current.gifts.tiers)
                if (t.move == k.ToString()) return t.label;
            return k.ToString();
        }

        /// <summary>Coins of the cheapest gift of a tier (DevKeys 1-7).</summary>
        public static int CoinsForTier(int tierIndex)
        {
            var tiers = GameConfig.Current.gifts.tiers;
            if (tierIndex < 0 || tierIndex >= tiers.Length) return 1;
            return tiers[tierIndex].minCoins;
        }
    }
}
