using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Entry, queue, eviction, death and revival rules (DESIGN §2, §3). Golden rule: a gifter always goes first —
    /// enters at once, revives at once, is never evicted to make room.
    /// </summary>
    public class RosterManager
    {
        readonly ViewerRegistry registry;
        readonly List<Viewer> queue = new List<Viewer>();
        readonly List<Viewer> prepList = new List<Viewer>();
        float nextPrepSpawn;
        float nextBotAt;
        int gateToggle;
        int botSerial;
        static GameConfig Cfg => GameConfig.Current;
        static GameManager GM => GameManager.I;

        public int QueueCount => queue.Count;
        public int PrepRemaining => prepList.Count;

        public RosterManager(ViewerRegistry registry) { this.registry = registry; }

        // ------------------------------------------------------------------ counts
        public void Count(out int real, out int bots)
        {
            real = 0; bots = 0;
            foreach (var f in GM.Fighters.Active)
            {
                if (!f.IsAlive) continue;
                if (f.IsBot) bots++; else real++;
            }
        }

        bool HasRoom => GM.Fighters.AliveCount < Cfg.roster.capacity;

        // ------------------------------------------------------------------ interactions
        /// <summary>Comment / like / follow / share.</summary>
        public void OnInteraction(Viewer v, float now)
        {
            switch (v.state)
            {
                case ViewerState.InArena:
                    return;
                case ViewerState.Dead:
                    v.interactedSinceDeath = true;  // counted when the penalty ends
                    return;
                case ViewerState.Queued:
                    return;
                default: // Watching, WaitingReentry
                    if (GM.CommentEntryOpen && HasRoom && queue.Count == 0) SpawnAtGate(v, now, false);
                    else Enqueue(v, now);
                    return;
            }
        }

        void Enqueue(Viewer v, float now)
        {
            if (queue.Contains(v)) return;
            if (queue.Count >= Cfg.roster.queueMax) return;
            v.state = ViewerState.Queued;
            v.queuedAt = now;
            queue.Add(v);
        }

        /// <summary>A gift: enters / revives at once (making room if needed), then the move runs.</summary>
        public void OnGift(Viewer v, int coins, int count, MoveKind move, float now)
        {
            if (!GM.GiftEntryOpen && (v.fighter == null || !v.fighter.IsAlive))
            {
                // final battle / winner screen: the gift is kept and runs first thing next round (doc §9)
                v.pendingGifts.Add(new PendingGift { coins = coins, count = count });
                if (v.state != ViewerState.Queued) Enqueue(v, now);
                return;
            }
            var f = v.fighter;
            if (f == null || !f.IsAlive)
            {
                queue.Remove(v);
                prepList.Remove(v);
                MakeRoom(now);
                bool revive = v.state == ViewerState.Dead;
                f = SpawnAtGate(v, now, true);
                if (revive)
                {
                    // revived through the gate: HP from the gift, at least 500 (doc §3)
                    float hp = Mathf.Max(Cfg.death.reviveMinHp, Mathf.Min(Cfg.health.max, coins * count * Cfg.health.giftHpPerCoin));
                    if (coins * count >= Cfg.health.giftFullHpCoins) hp = Cfg.health.max;
                    f.hp = hp;
                }
            }
            f.specials.Enqueue(move, count, now);
        }

        void MakeRoom(float now)
        {
            if (HasRoom) return;
            // victims: bots first, then the non-gifter with the fewest interactions in the last 2 minutes
            Fighter pick = null;
            int bestScore = int.MaxValue;
            float since = now - Cfg.roster.evictActivityWindowSec;
            foreach (var f in GM.Fighters.Active)
            {
                if (!f.IsAlive || f.IsGifter) continue;
                int score = f.IsBot ? -1000 : f.viewer.InteractionsSince(since);
                if (score < bestScore || (score == bestScore && pick != null && f.viewer.lastInteraction < pick.viewer.lastInteraction))
                {
                    bestScore = score;
                    pick = f;
                }
            }
            if (pick == null) return; // everyone in there is a gifter: allow going over capacity
            var v = pick.viewer;
            pick.Eject();
            if (!v.isBot) Enqueue(v, now);
            else v.state = ViewerState.Watching;
        }

        Fighter SpawnAtGate(Viewer v, float now, bool gift)
        {
            var gate = GM.World.gates[gateToggle++ % 2];
            gate.Strike();
            var runTo = Vector3.Lerp(gate.SpawnPoint, Vector3.zero, 0.55f) + new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
            var f = GM.Fighters.Spawn(v, gate.SpawnPoint + Random.insideUnitSphere.WithY(0) * 0.4f, runTo, now);
            v.interactedSinceDeath = false;
            if (v.pendingGifts.Count > 0)
            {
                foreach (var g in v.pendingGifts) f.specials.Enqueue(GiftResolver.Resolve(g.coins), g.count, now);
                v.pendingGifts.Clear();
            }
            GM.OnFighterEntered(f);
            return f;
        }

        public void OnFighterDied(Fighter f, float now)
        {
            var v = f.viewer;
            if (v == null) return;
            v.state = ViewerState.Dead;
            v.diedAt = now;
            v.deadUntil = now + Cfg.death.penaltySec;
            v.interactedSinceDeath = false;
        }

        // ------------------------------------------------------------------ round start
        /// <summary>Preparation (doc §9): everyone who interacted in the last 2 minutes + bots enter through the gates,
        /// 5 per second, gates alternating, gifters first.</summary>
        public void BuildPrepList(float now)
        {
            prepList.Clear();
            queue.Clear();
            var gifters = new List<Viewer>();
            var others = new List<Viewer>();
            foreach (var v in registry.All)
            {
                if (v.isBot) { v.state = ViewerState.Watching; continue; }
                v.fighter = null;
                bool recent = now - v.lastInteraction <= Cfg.round.prepRecentSec;
                bool gifter = v.pendingGifts.Count > 0 || now - v.lastGiftTime <= Cfg.roster.giftProtectionSec;
                v.state = ViewerState.Watching;
                if (gifter) gifters.Add(v);
                else if (recent) others.Add(v);
            }
            gifters.Sort((a, b) => b.pendingGifts.Count != a.pendingGifts.Count ? b.pendingGifts.Count.CompareTo(a.pendingGifts.Count) : b.lastGiftTime.CompareTo(a.lastGiftTime));
            others.Sort((a, b) => b.lastInteraction.CompareTo(a.lastInteraction));
            int cap = Cfg.roster.capacity;
            foreach (var v in gifters) if (prepList.Count < cap) prepList.Add(v);
            foreach (var v in others) { if (prepList.Count < cap) prepList.Add(v); else Enqueue(v, now); }
            foreach (var v in prepList) v.state = ViewerState.Queued;

            int real = prepList.Count;
            int bots = real >= Cfg.bots.noNewBotsAboveReal ? 0 : real + Cfg.bots.startCount < Cfg.bots.fillWhenTotalBelow ? Mathf.Max(Cfg.bots.startCount, Cfg.bots.fillTarget - real / 3) : Cfg.bots.startCount;
            bots = Mathf.Min(bots, cap - prepList.Count);
            for (int i = 0; i < bots; i++) prepList.Add(NextBot());
            nextPrepSpawn = now + 0.6f;
        }

        Viewer NextBot()
        {
            for (int tries = 0; tries < 40; tries++)
            {
                var name = BotNames.Pick(botSerial++);
                var v = registry.CreateBot(name);
                if (v.fighter == null || !v.fighter.active) { v.state = ViewerState.Queued; return v; }
            }
            return registry.CreateBot("bot_" + botSerial++);
        }

        // ------------------------------------------------------------------ per frame
        public void Tick(float now, float dt)
        {
            var gm = GM;
            // preparation entries
            if (prepList.Count > 0 && now >= nextPrepSpawn && (gm.State == RoundState.Prep || gm.State == RoundState.Battle))
            {
                nextPrepSpawn = now + 1f / Mathf.Max(0.5f, Cfg.round.entriesPerSec);
                var v = prepList[0];
                prepList.RemoveAt(0);
                if (v.fighter == null || !v.fighter.IsAlive) SpawnAtGate(v, now, false);
            }

            // penalties ending (doc §3 Ölüm ve geri dönüş)
            foreach (var v in registry.All)
            {
                if (v.state != ViewerState.Dead || now < v.deadUntil) continue;
                if (v.isBot) { v.state = ViewerState.Watching; continue; }
                if (v.interactedSinceDeath)
                {
                    if (gm.CommentEntryOpen && HasRoom) SpawnAtGate(v, now, false);
                    else Enqueue(v, now);
                }
                else v.state = ViewerState.WaitingReentry;
            }

            // queue: when there is room, the most active one goes in
            if (gm.CommentEntryOpen && queue.Count > 0 && HasRoom && prepList.Count == 0)
            {
                float since = now - Cfg.roster.evictActivityWindowSec;
                int bi = 0, best = -1;
                for (int i = 0; i < queue.Count; i++)
                {
                    int s = queue[i].InteractionsSince(since);
                    if (s > best) { best = s; bi = i; }
                }
                var v = queue[bi];
                queue.RemoveAt(bi);
                SpawnAtGate(v, now, false);
            }

            Count(out int real, out int bots);
            // bots: the arena is never empty; below 10 characters bots come in (5-7), none above 20 real players (doc §2, §3)
            if ((gm.State == RoundState.Battle || gm.State == RoundState.Prep) && prepList.Count == 0 && now >= nextBotAt)
            {
                nextBotAt = now + 1.2f;
                if (real + bots < Cfg.bots.fillWhenTotalBelow && real <= Cfg.bots.noNewBotsAboveReal && bots < Cfg.bots.fillTarget && HasRoom)
                    SpawnAtGate(NextBot(), now, false);
            }
            // bots slowly die off as real players fill the arena
            if (real > Cfg.bots.botDrainStartsAboveReal)
                foreach (var f in gm.Fighters.Active)
                    if (f.IsBot && f.IsAlive) { f.hp -= Cfg.bots.botDrainPerSec * dt; if (f.hp <= 0f) f.Die(null, MoveKind.None); }
        }

        /// <summary>Streamer panel: remove a user from the game.</summary>
        public void Kick(Viewer v)
        {
            queue.Remove(v);
            prepList.Remove(v);
            if (v.fighter != null && v.fighter.IsAlive) v.fighter.Eject();
            v.state = ViewerState.Watching;
            v.pendingGifts.Clear();
        }
    }

    public static class VecExt
    {
        public static Vector3 WithY(this Vector3 v, float y) { v.y = y; return v; }
    }
}
