using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Every incoming viewer event (TikTok bridge, simulator, DevKeys) goes through here and is dispatched to the
    /// right systems (doc §10.3): health (+1 per like, +5 per comment, coins x10 per gift), waking up, entry rules,
    /// gift moves, persistent totals, event feed.
    /// </summary>
    public class EventRouter
    {
        readonly ViewerRegistry registry;
        readonly RosterManager roster;
        readonly PersistenceService persistence;
        static GameConfig Cfg => GameConfig.Current;

        public EventRouter(ViewerRegistry registry, RosterManager roster, PersistenceService persistence)
        {
            this.registry = registry;
            this.roster = roster;
            this.persistence = persistence;
        }

        public void Handle(ViewerEvent e)
        {
            if (e == null || string.IsNullOrEmpty(e.userId)) return;
            float now = Time.time;
            var kind = e.Kind;
            if (kind == ViewerEventType.Join) return; // only joining the LIVE does nothing
            var v = registry.GetOrCreate(e.userId, e.nickname, e.avatarUrl);
            var f = v.fighter != null && v.fighter.IsAlive ? v.fighter : null;
            var hc = Cfg.health;

            switch (kind)
            {
                case ViewerEventType.Comment:
                case ViewerEventType.Follow:
                case ViewerEventType.Share:
                {
                    v.NoteInteraction(now);
                    if (kind == ViewerEventType.Comment && v.record != null) v.record.totalComments++;
                    if (f != null)
                    {
                        f.Heal(hc.commentHp, false);
                        f.Wake(now);
                        f.nameVisibleUntil = now + Cfg.names.showSec;
                        GameManager.I.Names?.Float(f, "+" + Mathf.RoundToInt(hc.commentHp), new Color(0.45f, 1f, 0.5f));
                    }
                    roster.OnInteraction(v, now);
                    break;
                }
                case ViewerEventType.Like:
                {
                    // at most N likes per person per second are counted (doc §3)
                    if (now - v.likeWindowStart >= 1f) { v.likeWindowStart = now; v.likesInWindow = 0; }
                    int allowed = Mathf.Max(0, hc.maxLikesPerSec - v.likesInWindow);
                    int n = Mathf.Min(Mathf.Max(1, e.count), allowed);
                    if (n <= 0) { v.NoteInteraction(now); return; }
                    v.likesInWindow += n;
                    v.NoteInteraction(now);
                    if (v.record != null) v.record.totalLikes += n;
                    if (f != null)
                    {
                        f.Heal(hc.likeHp * n, false);
                        f.Wake(now);
                        f.nameVisibleUntil = now + Cfg.names.showSec;
                        GameManager.I.Names?.Float(f, "+" + Mathf.RoundToInt(hc.likeHp * n), new Color(0.45f, 1f, 0.5f));
                    }
                    roster.OnInteraction(v, now);
                    break;
                }
                case ViewerEventType.Gift:
                {
                    int coins = GiftCatalog.CoinsFor(e.giftName, e.coins);
                    int count = Mathf.Max(1, e.count);
                    int total = coins * count;
                    v.NoteInteraction(now);
                    v.lastGiftTime = now;
                    v.roundCoins += total;
                    v.streamCoins += total;
                    if (v.record != null && !v.isBot)
                    {
                        v.record.totalCoins += total;
                        if (!v.record.goldUnlocked && v.record.totalCoins >= Cfg.gifts.goldUnlockTotalCoins)
                        {
                            v.record.goldUnlocked = true; // golden costume forever (doc §7.3)
                            if (f != null) Accessories.SetGold(f.rig, true);
                        }
                        persistence.MarkDirty();
                    }
                    if (f != null)
                    {
                        float heal = total >= hc.giftFullHpCoins ? hc.max : total * hc.giftHpPerCoin;
                        f.Heal(heal, true);
                        f.Wake(now);
                        GameManager.I.Names?.Float(f, "+" + Mathf.RoundToInt(Mathf.Min(heal, hc.max - 0f)), new Color(0.45f, 1f, 0.5f));
                    }
                    var move = GiftResolver.Resolve(coins);
                    roster.OnGift(v, coins, count, move, now);
                    GameManager.I.OnGift(v, move, total);
                    break;
                }
            }
        }
    }
}
