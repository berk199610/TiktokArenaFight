using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Common interface of the seven gift moves (doc §4, §10.3). Each move is its own class; adding a new one means
    /// one new class + one line in <see cref="SpecialMoveRunner.Create"/>.
    /// </summary>
    public abstract class SpecialMove
    {
        public Fighter f;
        public MoveKind kind;
        public float startedAt, endsAt;
        public bool finished;
        protected float duration;

        /// <summary>True when the move drives the body (brain paused, only flinches from hits).</summary>
        public virtual bool ControlsBody => false;

        protected static GameConfig Cfg => GameConfig.Current;

        public void Start(Fighter fighter, MoveKind k, int count, float now)
        {
            f = fighter;
            kind = k;
            startedAt = now;
            finished = false;
            Begin(count, now);
        }

        protected abstract void Begin(int count, float now);
        public abstract void Tick(float dt, float now);
        public virtual void End() { }

        /// <summary>Same gift again in a series: longer duration, max 60 s (doc §4 Kurallar).</summary>
        public virtual void Extend(int count, float now)
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

    /// <summary>One special move at a time per fighter; the next waits (max 3 in line). 0.5 s power-up pose first.</summary>
    public class SpecialMoveRunner
    {
        readonly Fighter f;
        public SpecialMove Current { get; private set; }
        readonly List<(MoveKind kind, int count)> queue = new List<(MoveKind, int)>(4);
        MoveKind posing = MoveKind.None;
        int posingCount;
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
                case MoveKind.Tornado: return new Tornado();
                default: return null;
            }
        }

        public void Enqueue(MoveKind k, int count, float now)
        {
            if (k == MoveKind.None || count <= 0) return;
            if (Current != null && Current.kind == k && !Current.finished) { Current.Extend(count, now); return; }
            if (posing == k) { posingCount += count; return; }
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].kind == k) { queue[i] = (k, queue[i].count + count); return; }
            if (queue.Count >= GameConfig.Current.moves.queueMax) return;
            queue.Add((k, count));
        }

        public void Clear()
        {
            if (Current != null) { Current.End(); Current = null; }
            queue.Clear();
            if (posing != MoveKind.None) f.superArmor = false;
            posing = MoveKind.None;
        }

        bool CanStart => f.state == FState.Idle || f.state == FState.Attacking || f.state == FState.Taunting || f.state == FState.Dizzy || f.state == FState.Waking || f.state == FState.Staggered;

        public void Tick(float dt, float now)
        {
            if (!f.IsAlive) return;
            if (posing != MoveKind.None)
            {
                if (now >= poseUntil)
                {
                    var k = posing; posing = MoveKind.None;
                    f.superArmor = false;
                    if (f.state == FState.Posing) f.state = FState.Idle;
                    Current = Create(k);
                    Current.Start(f, k, posingCount, now);
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
                var (k, c) = queue[0];
                queue.RemoveAt(0);
                BeginPose(k, c, now);
            }
        }

        /// <summary>"Ben öne geçtim" moment: stops, light pillar comes down, flexes, then does the move (doc §4).</summary>
        void BeginPose(MoveKind k, int count, float now)
        {
            f.combat.Cancel();
            f.brain.ClearTarget();
            posing = k;
            posingCount = count;
            poseUntil = now + GameConfig.Current.moves.powerPoseSec;
            f.state = FState.Posing;
            f.superArmor = true;
            f.motor.Stop();
            f.anim.Play(FighterAnimation.Flex, 0.06f);
            Fx.I.LightPillar(f.Pos, new Color(1f, 0.85f, 0.35f, 1f), 0.8f);
            // event line when the move really starts ("Ahmet34 TORNADO!", doc §9)
            GameManager.I.Hud?.Feed($"{f.Name} {Ui.UpperTr(GiftResolver.Label(k))}!", new Color(1f, 0.85f, 0.3f));
            Fx.I.Sparkle(f.Pos, new Color(1f, 0.85f, 0.3f), 14);
            GameManager.I.Cam?.NudgeToward(f, k >= MoveKind.SpinKick);
        }
    }
}
