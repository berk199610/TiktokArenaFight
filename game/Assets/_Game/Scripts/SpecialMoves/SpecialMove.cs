using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Common base of the gift moves (doc §4, §10.3). Each move is its own class; adding a new one means
    /// one new class + one line in <see cref="SpecialMoveRunner.Create"/>.
    /// </summary>
    public abstract class SpecialMove
    {
        public Fighter f;
        public MoveKind kind;
        public float startedAt, endsAt;
        public bool finished;
        public float power = 1f;     // 1..2: a bigger gift inside the same coin range is stronger
        protected float duration;

        /// <summary>True when the move drives the body (brain paused, only flinches from hits).</summary>
        public virtual bool ControlsBody => false;
        /// <summary>Weapons / power fists are replaced at once by a new gift (user request).</summary>
        public virtual bool Replaceable => false;

        protected static GameConfig Cfg => GameConfig.Current;

        public void Start(Fighter fighter, MoveKind k, int count, float pow, float now)
        {
            f = fighter;
            kind = k;
            power = Mathf.Clamp(pow, 0.5f, 3f);
            startedAt = now;
            finished = false;
            Begin(count, now);
        }

        protected abstract void Begin(int count, float now);
        public abstract void Tick(float dt, float now);
        public virtual void End() { }

        /// <summary>Same gift again in a series: longer duration, max 60 s (doc §4 Kurallar).</summary>
        public virtual void Extend(int count, float pow, float now)
        {
            if (duration <= 0f) return;
            endsAt = Mathf.Min(startedAt + Cfg.moves.stackMaxSec, endsAt + duration * count);
        }
    }

    public struct Hazard
    {
        public Fighter owner;
        public float radius;
    }

    /// <summary>
    /// Runs a fighter's gift moves. One action at a time (spin kick, wave, thunder, tornado wait for each other, max
    /// 3 in line), but a held weapon or power fist is dropped at once when a new gift arrives, and Giant mode runs
    /// alongside everything (a giant tornado is allowed). Every move starts with the 0.5 s power-up pose.
    /// </summary>
    public class SpecialMoveRunner
    {
        readonly Fighter f;
        public SpecialMove Current { get; private set; }
        public SpecialMove Giant { get; private set; }
        readonly List<(MoveKind kind, int count, float power)> queue = new List<(MoveKind, int, float)>(4);
        MoveKind posing = MoveKind.None;
        int posingCount;
        float posingPower;
        float poseUntil;

        public SpecialMoveRunner(Fighter f) { this.f = f; }

        public bool Busy => Current != null || posing != MoveKind.None;
        public int Queued => queue.Count;

        public static SpecialMove Create(MoveKind k)
        {
            switch (k)
            {
                case MoveKind.PowerPunch: return new PowerPunch();
                case MoveKind.Bat: return new WeaponBuff(MoveKind.Bat);
                case MoveKind.Shovel: return new WeaponBuff(MoveKind.Shovel);
                case MoveKind.SpinKick: return new SpinKick();
                case MoveKind.Shockwave: return new Shockwave();
                case MoveKind.Giant: return new GiantMode();
                case MoveKind.Thunder: return new Thunder();
                case MoveKind.Tornado: return new Tornado();
                default: return null;
            }
        }

        public void Enqueue(MoveKind k, int count, float power, float now)
        {
            if (k == MoveKind.None || count <= 0) return;
            // Giant runs in parallel with everything
            if (k == MoveKind.Giant)
            {
                if (Giant != null && !Giant.finished) { Giant.Extend(count, power, now); return; }
                Giant = Create(k);
                Giant.Start(f, k, count, power, now);
                Fx.I.LightPillar(f.Pos, new Color(1f, 0.5f, 1f, 1f), 0.8f);
                GameManager.I.Hud?.FeedMove(f.Name, k);
                GameManager.I.Cam?.NudgeToward(f, true);
                return;
            }
            if (Current != null && Current.kind == k && !Current.finished) { Current.Extend(count, power, now); return; }
            if (posing == k) { posingCount += count; posingPower = Mathf.Max(posingPower, power); return; }
            // a weapon / power fist in hand is replaced right away by the new gift
            if (Current != null && Current.Replaceable)
            {
                Current.End();
                Current = null;
                if (f.state == FState.Special) f.ToIdle();
            }
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].kind == k) { queue[i] = (k, queue[i].count + count, Mathf.Max(queue[i].power, power)); return; }
            if (queue.Count >= GameConfig.Current.moves.queueMax) queue.RemoveAt(0);
            queue.Add((k, count, power));
        }

        public void Clear()
        {
            if (Current != null) { Current.End(); Current = null; }
            if (Giant != null) { Giant.End(); Giant = null; }
            queue.Clear();
            if (posing != MoveKind.None) f.superArmor = false;
            posing = MoveKind.None;
        }

        bool CanStart => f.state == FState.Idle || f.state == FState.Attacking || f.state == FState.Taunting || f.state == FState.Dizzy || f.state == FState.Waking || f.state == FState.Staggered;

        public void Tick(float dt, float now)
        {
            if (!f.IsAlive) return;
            if (Giant != null)
            {
                Giant.Tick(dt, now);
                if (Giant.finished || (Giant.endsAt > 0f && now >= Giant.endsAt)) { Giant.End(); Giant = null; }
            }
            if (posing != MoveKind.None)
            {
                if (now >= poseUntil)
                {
                    var k = posing; posing = MoveKind.None;
                    f.superArmor = false;
                    if (f.state == FState.Posing) f.state = FState.Idle;
                    Current = Create(k);
                    Current.Start(f, k, posingCount, posingPower, now);
                    if (Current.ControlsBody) f.state = FState.Special;
                    else f.ToIdle();
                }
                return;
            }
            if (Current != null)
            {
                Current.Tick(dt, now);
                if (Current.finished || (Current.endsAt > 0f && now >= Current.endsAt))
                {
                    bool controlled = Current.ControlsBody;
                    Current.End();
                    Current = null;
                    if (controlled && f.state == FState.Special) f.ToIdle();
                }
                return;
            }
            if (queue.Count > 0 && CanStart)
            {
                var (k, c, p) = queue[0];
                queue.RemoveAt(0);
                BeginPose(k, c, p, now);
            }
        }

        /// <summary>"Ben öne geçtim" moment: stops, light pillar comes down, flexes, then does the move (doc §4).</summary>
        void BeginPose(MoveKind k, int count, float power, float now)
        {
            f.combat.Cancel();
            f.brain.ClearTarget();
            posing = k;
            posingCount = count;
            posingPower = power;
            poseUntil = now + GameConfig.Current.moves.powerPoseSec;
            f.state = FState.Posing;
            f.superArmor = true;
            f.motor.Stop();
            f.anim.Play(FighterAnimation.Flex, 0.06f);
            Fx.I.LightPillar(f.Pos, new Color(1f, 0.85f, 0.35f, 1f), 0.8f);
            Fx.I.Sparkle(f.Pos, new Color(1f, 0.85f, 0.3f), 14);
            GameManager.I.Hud?.FeedMove(f.Name, k);
            GameManager.I.Emotes?.Show(f, "starstruck", true);
            GameManager.I.Cam?.NudgeToward(f, k >= MoveKind.SpinKick);
        }
    }
}
