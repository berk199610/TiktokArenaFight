using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Arena
{
    public enum MoveKind { None = -1, PowerPunch = 0, Bat = 1, Shovel = 2, SpinKick = 3, Shockwave = 4, Giant = 5, Thunder = 6, Tornado = 7 }

    /// <summary>
    /// TikTok gift name -> coin value (and optionally a fixed move) from config. Any gift works through its coin value;
    /// the catalog is only needed when the event has no coin value or a gift should do a specific move.
    /// </summary>
    public static class GiftCatalog
    {
        static Dictionary<string, GameConfig.GiftItem> byName;

        public static void Rebuild(GameConfig cfg)
        {
            byName = new Dictionary<string, GameConfig.GiftItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in cfg.gifts.catalog)
                if (!string.IsNullOrEmpty(g.name)) byName[g.name] = g;
        }

        public static GameConfig.GiftItem Find(string giftName)
        {
            if (byName == null) Rebuild(GameConfig.Current);
            if (string.IsNullOrEmpty(giftName)) return null;
            byName.TryGetValue(giftName, out var g);
            return g;
        }

        public static int CoinsFor(string giftName, int fallback)
        {
            if (fallback > 0) return fallback;
            var g = Find(giftName);
            return g != null && g.coins > 0 ? g.coins : Math.Max(1, fallback);
        }
    }

    /// <summary>
    /// Coin value -> tier -> special move (doc §4), with ranges so every TikTok gift lands somewhere:
    /// 1-4 Punch, 5-29 Bat, 30-99 Shovel, 100-299 Spin kick, 300-999 Wave, 1000-2499 Giant, 2500-4999 Thunder,
    /// 5000+ Tornado. Inside a range a bigger gift is stronger: power 1.0 at the bottom of the range, 2.0 at the top.
    /// </summary>
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

        public static MoveKind Resolve(int coins) => Resolve(coins, null, out _);

        public static MoveKind Resolve(int coins, string giftName, out float power)
        {
            power = 1f;
            var tiers = GameConfig.Current.gifts.tiers;
            var item = GiftCatalog.Find(giftName);
            if (item != null && !string.IsNullOrEmpty(item.move) && Enum.TryParse(item.move, out MoveKind forced))
            {
                power = Mathf.Clamp(item.power > 0 ? item.power : 1f, 0.5f, 3f);
                return forced;
            }
            int i = TierIndex(coins);
            if (i < 0) return MoveKind.None;
            int lo = tiers[i].minCoins;
            int hi = i + 1 < tiers.Length ? tiers[i + 1].minCoins : lo * 2;
            power = 1f + Mathf.Clamp01((coins - lo) / (float)Mathf.Max(1, hi - lo));
            return Enum.TryParse(tiers[i].move, out MoveKind k) ? k : MoveKind.None;
        }

        public static string Label(MoveKind k)
        {
            foreach (var t in GameConfig.Current.gifts.tiers)
                if (t.move == k.ToString()) return t.label;
            return k.ToString().ToUpperInvariant();
        }

        /// <summary>Coins of the cheapest gift of a tier (DevKeys 1-8).</summary>
        public static int CoinsForTier(int tierIndex)
        {
            var tiers = GameConfig.Current.gifts.tiers;
            if (tierIndex < 0 || tierIndex >= tiers.Length) return 1;
            return tiers[tierIndex].minCoins;
        }

        // ------------------------------------------------------------------ custom gift pictures for the HUD guide
        static readonly Dictionary<string, Sprite> pictures = new Dictionary<string, Sprite>();

        /// <summary>Picture of a TikTok gift put by the streamer into StreamingAssets/GiftIcons (e.g. "rose.png").</summary>
        public static Sprite Picture(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            if (pictures.TryGetValue(file, out var s)) return s;
            s = null;
            try
            {
                var path = Path.Combine(Application.streamingAssetsPath, "GiftIcons", file);
                if (File.Exists(path))
                {
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                    if (t.LoadImage(File.ReadAllBytes(path))) s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
                }
            }
            catch (Exception e) { Debug.LogWarning("[Arena] Hediye resmi okunamadı: " + file + " " + e.Message); }
            pictures[file] = s;
            return s;
        }
    }
}
