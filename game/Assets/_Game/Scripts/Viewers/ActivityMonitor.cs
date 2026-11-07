using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Intelligence from time since the last interaction (doc §5.5) and the 60-second activity check (doc §3):
    /// no comment / like / gift in the last 120 s -> the character loses HP faster and defends worse.
    /// Gifters are exempt from the activity check for 5 minutes.
    /// </summary>
    public class ActivityMonitor
    {
        float nextIntel, nextCheck;
        static GameConfig Cfg => GameConfig.Current;

        public static float IntelligenceFor(float sinceInteraction)
        {
            var ic = Cfg.intelligence;
            if (sinceInteraction <= ic.fullUntilSec) return ic.fullValue;
            if (sinceInteraction <= ic.halfUntilSec) return Mathf.Lerp(ic.fullValue, ic.halfValue, (sinceInteraction - ic.fullUntilSec) / Mathf.Max(1f, ic.halfUntilSec - ic.fullUntilSec));
            if (sinceInteraction <= ic.minAtSec) return Mathf.Lerp(ic.halfValue, ic.minValue, (sinceInteraction - ic.halfUntilSec) / Mathf.Max(1f, ic.minAtSec - ic.halfUntilSec));
            return ic.minValue;
        }

        public void Tick(float now, float dt)
        {
            var fighters = GameManager.I.Fighters.Active;
            if (now >= nextIntel)
            {
                nextIntel = now + 0.5f;
                foreach (var f in fighters)
                {
                    if (!f.IsAlive || f.IsBot) continue;
                    f.intelligence = IntelligenceFor(now - f.viewer.lastInteraction);
                }
            }
            if (now >= nextCheck)
            {
                nextCheck = now + Cfg.roster.activityCheckIntervalSec;
                foreach (var f in fighters)
                {
                    if (!f.IsAlive || f.IsBot) continue;
                    bool protectedByGift = now - f.viewer.lastGiftTime <= Cfg.roster.giftProtectionSec;
                    f.inactive = !protectedByGift && now - f.viewer.lastInteraction > Cfg.roster.inactiveAfterSec;
                }
            }
            // inactive characters lose HP and die quickly
            foreach (var f in fighters)
            {
                if (!f.IsAlive || !f.inactive || f.IsBot) continue;
                f.hp -= Cfg.health.inactiveDrainPerSec * dt;
                if (f.hp <= 0f) { f.hp = 0f; f.Die(null, MoveKind.None); }
            }
        }
    }
}
