using System;
using System.IO;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Every tunable number of the game (DESIGN §3, §4, §5, §6, §9). Read from StreamingAssets/config.json,
    /// hot-reloaded when the file changes so numbers can be tuned while the stream is running.
    /// The defaults below are the same as the shipped config.json.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        [Serializable] public class Roster
        {
            public int capacity = 100;
            public float commentEntryWindowSec = 90;
            public float activityCheckIntervalSec = 60;
            public float inactiveAfterSec = 120;
            public float giftProtectionSec = 300;
            public float evictActivityWindowSec = 120;
            public bool reentryByCommentUntilFinal = true; // dead players come back with a comment during the whole battle
            public int queueMax = 300;
        }

        [Serializable] public class Intelligence
        {
            public float fullUntilSec = 45, halfUntilSec = 70, minAtSec = 90;
            public float fullValue = 1f, halfValue = 0.5f, minValue = 0.1f, botValue = 0.7f;
            public float reactionFull = 0.15f, reactionHalf = 0.5f, reactionMin = 1f;
            public float dodgeChanceFull = 0.3f, dodgeChanceHalf = 0.05f;
            public float wrongTargetChanceHalf = 0.25f;
            public float dullDizzyChance = 0.45f, dullWrongWayChance = 0.3f;
        }

        [Serializable] public class Health
        {
            public float start = 1000, max = 1000;
            public float likeHp = 1; public int maxLikesPerSec = 5;
            public float commentHp = 5, giftHpPerCoin = 10; public int giftFullHpCoins = 100;
            public float lowHp = 300, veryLowHp = 150;
            public float lowWalkMul = 0.7f, lowAttackSpeedMul = 0.75f, veryLowKnockdownChance = 0.35f;
            public float inactiveDrainPerSec = 6, inactiveDamageTakenMul = 1.5f;
        }

        [Serializable] public class Death
        {
            public float lieSec = 2, penaltySec = 3, reviveMinHp = 500;
        }

        [Serializable] public class Bots
        {
            public int startCount = 4, fillWhenTotalBelow = 10, fillTarget = 6, noNewBotsAboveReal = 20, botDrainStartsAboveReal = 10;
            public float botDrainPerSec = 4;
        }

        [Serializable] public class Combat
        {
            public float punchDamage = 20, punchRange = 1.2f, punchCooldown = 0.8f;
            public float kickDamage = 35, kickRange = 1.6f, kickCooldown = 1.4f, kickWindupSec = 0.45f;
            public float hitArcDeg = 80, hitStopSec = 0.05f;
            public float lightPush = 0.6f, heavyPush = 3.5f, heavyStaggerSec = 0.6f, knockdownSec = 1f;
            public float launchUpSpeed = 5.5f, launchBackSpeed = 7f, gravity = 20f, wallBounce = 0.6f;
            public float superHitFlyDamage = 50;
            // reaction by force (user request: not every hit throws people; strength decides)
            public float poiseDecayPerSec = 55, staggerImpact = 28, knockdownPoise = 95, launchPoise = 135;
            public float launchMinBack = 3f, launchMaxBack = 9f, launchMinUp = 2.5f, launchMaxUp = 7f, sideSpreadDeg = 35f;
            public float forcePunch = 1f, forceKick = 1.3f, forceBat = 1.4f, forceShovel = 1.3f, forcePower = 1.7f;
            public float forceSpin = 1.4f, forceWave = 1.5f, forceTornado = 1.25f, forceThunder = 1.4f, giantForceMul = 1.4f;
        }

        [Serializable] public class PowerPunchCfg { public float damage = 60; public int maxCharges = 5; public float timeoutSec = 12; }
        [Serializable] public class BatCfg { public float durationSec = 15, rangeMul = 1.5f, damageMul = 2f; }
        [Serializable] public class ShovelCfg { public float durationSec = 20, damage = 60, range = 2.2f, arcDeg = 180, cooldown = 1.3f; }
        [Serializable] public class SpinKickCfg { public float durationSec = 3, radius = 2f, damagePerSpin = 50; public int spins = 3; public float push = 4f; }
        [Serializable] public class ShockwaveCfg { public float radius = 4f, damage = 150, jumpSec = 0.55f; }
        [Serializable] public class GiantCfg { public float durationSec = 20, scale = 1.4f, damageMul = 2f; }
        [Serializable] public class TornadoCfg { public float durationSec = 10, tickSec = 0.5f, damage = 40, radius = 1.8f, speed = 3.2f, fling = 6f, spinDegPerSec = 1440f, pull = 2.5f; }
        [Serializable] public class ThunderCfg { public int strikes = 6; public float intervalSec = 0.45f, damage = 110, radius = 1.8f; }

        [Serializable] public class Moves
        {
            public PowerPunchCfg powerPunch = new PowerPunchCfg();
            public BatCfg bat = new BatCfg();
            public ShovelCfg shovel = new ShovelCfg();
            public SpinKickCfg spinKick = new SpinKickCfg();
            public ShockwaveCfg shockwave = new ShockwaveCfg();
            public GiantCfg giant = new GiantCfg();
            public TornadoCfg tornado = new TornadoCfg();
            public ThunderCfg thunder = new ThunderCfg();
            public float stackMaxSec = 60;
            public int queueMax = 3;
            public float powerPoseSec = 0.5f;
        }

        [Serializable] public class GiftTier { public int minCoins; public string move; public string label; public string icon; }
        [Serializable] public class GiftItem { public string name; public int coins; public string move; public float power; }

        [Serializable] public class Gifts
        {
            public GiftTier[] tiers = new GiftTier[0];
            public GiftItem[] catalog = new GiftItem[0];
            public int goldUnlockTotalCoins = 1000;
        }

        [Serializable] public class Ai
        {
            public float thinkMin = 0.2f, thinkMax = 0.4f, perceptionRadius = 8f, revengeSec = 10f;
            public int maxAttackersPerTarget = 3;
            public float separationRadius = 0.9f, separationStrength = 3f;
            public float speedVariance = 0.1f, animSpeedVariance = 0.08f;
            public float walkSpeed = 1.6f, runSpeed = 3.6f, turnSpeedDeg = 540f;
            public float edgeRadius = 9.5f, tieEpsilon = 0.06f, restChance = 0.08f, tauntWindowSec = 2f;
        }

        [Serializable] public class Round
        {
            public float prepSec = 15; public float entriesPerSec = 5; public float prepRecentSec = 120;
            public float finalAfterSec = 180; public int finalAtAlive = 10;
            public float ringStartRadius = 11.6f, ringEndRadius = 6.5f, ringShrinkSec = 75, ringDps = 30;
            public float winnerSec = 8, maxRoundSec = 330;
        }

        [Serializable] public class Points { public int win = 100, kill = 10, top10 = 20, bossKill = 50, bossMinRoundCoins = 100; }
        [Serializable] public class Names { public float showSec = 3; public int alwaysShowAtOrBelow = 20; }
        [Serializable] public class ArenaCfg { public float radius = 12f, gateAngleDeg = 90f; }

        [Serializable] public class CameraCfg
        {
            public float pitchDeg = 42, yawDeg = 45, fov = 45, minDistance = 16.5f, maxDistance = 21.5f, spreadForMax = 7;
            public float followSec = 1.5f, maxOffsetFromCenter = 6, maxOffsetForward = 3, giftNudgeWeight = 6, lookAhead = 3.5f; public int giftNudgeMinCoins = 100;
            public float zoomSec = 3f, breathAmp = 1f, breathPeriodSec = 24f, maxFocusSpeed = 2.5f, maxZoomSpeed = 1.2f;
        }

        [Serializable] public class Board { public int rows = 10; public float screenY = 0.745f, heightFrac = 0.255f, depth = 30f, tiltDeg = 10f; public float distanceBehind = 0.3f, height = 1f, scale = 1f; }
        [Serializable] public class Levels { public int likesL1 = 20, commentsL1 = 3, likesL2 = 100, commentsL2 = 10, likesL3 = 300, commentsL3 = 25; public int[] giftCoins = { 1, 30, 300, 1000, 5000 }; }
        [Serializable] public class Emotes { public float cooldownSec = 4f, showSec = 1.7f; public int maxActive = 14; }
        [Serializable] public class Vigor { public float maxBulk = 1.22f, minBulk = 0.8f, fullForSec = 30, thinAfterSec = 150; }
        [Serializable] public class Bridge { public string url = "ws://127.0.0.1:8788/game"; public float reconnectSec = 2; }
        [Serializable] public class Dev { public int trafficViewers = 60; public float trafficEventsPerSec = 6; }

        public Roster roster = new Roster();
        public Intelligence intelligence = new Intelligence();
        public Health health = new Health();
        public Death death = new Death();
        public Bots bots = new Bots();
        public Combat combat = new Combat();
        public Moves moves = new Moves();
        public Gifts gifts = new Gifts();
        public Ai ai = new Ai();
        public Round round = new Round();
        public Points points = new Points();
        public Names names = new Names();
        public ArenaCfg arena = new ArenaCfg();
        public CameraCfg camera = new CameraCfg();
        public Board board = new Board();
        public Vigor vigor = new Vigor();
        public Levels levels = new Levels();
        public Emotes emotes = new Emotes();
        public Bridge bridge = new Bridge();
        public Dev dev = new Dev();

        // ------------------------------------------------------------------ loading
        public static GameConfig Current { get; private set; } = new GameConfig();
        public static event Action Reloaded;

        static string Path => System.IO.Path.Combine(Application.streamingAssetsPath, "config.json");
        static DateTime lastWrite;
        static float nextCheck;

        public static void Load()
        {
            try
            {
                if (File.Exists(Path))
                {
                    var cfg = JsonUtility.FromJson<GameConfig>(File.ReadAllText(Path));
                    if (cfg != null)
                    {
                        if (cfg.gifts.tiers == null || cfg.gifts.tiers.Length == 0) cfg.gifts.tiers = DefaultTiers();
                        Current = cfg;
                    }
                    lastWrite = File.GetLastWriteTimeUtc(Path);
                }
                else
                {
                    Current.gifts.tiers = DefaultTiers();
                    Debug.LogWarning("[Arena] config.json bulunamadı, varsayılanlar kullanılıyor: " + Path);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Arena] config.json okunamadı (eski ayarlarla devam): " + e.Message);
            }
            Reloaded?.Invoke();
        }

        /// <summary>Call every frame; re-reads config.json at most every 2 seconds when it changed on disk.</summary>
        public static void HotReloadTick()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 2f;
            try
            {
                if (File.Exists(Path) && File.GetLastWriteTimeUtc(Path) != lastWrite)
                {
                    Load();
                    Debug.Log("[Arena] config.json yeniden yüklendi.");
                }
            }
            catch { }
        }

        static GiftTier[] DefaultTiers()
        {
            return new[]
            {
                new GiftTier { minCoins = 1, move = "PowerPunch", label = "PUNCH" },
                new GiftTier { minCoins = 5, move = "Bat", label = "BAT" },
                new GiftTier { minCoins = 30, move = "Shovel", label = "SHOVEL" },
                new GiftTier { minCoins = 100, move = "SpinKick", label = "SPIN KICK" },
                new GiftTier { minCoins = 300, move = "Shockwave", label = "WAVE" },
                new GiftTier { minCoins = 1000, move = "Giant", label = "GIANT" },
                new GiftTier { minCoins = 2500, move = "Thunder", label = "THUNDER" },
                new GiftTier { minCoins = 5000, move = "Tornado", label = "TORNADO" },
            };
        }
    }
}
