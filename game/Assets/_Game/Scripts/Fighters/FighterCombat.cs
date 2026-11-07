using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    public enum AttackKind { Punch, Kick, Bat, Shovel }

    /// <summary>
    /// Basic attacks (doc §6.1): punch (2-3 different punches in turn) and kick. Damage is dealt at the contact frame
    /// through an Animation Event, in a small arc in front of the character. Bat / shovel replace them while a gift is active.
    /// </summary>
    public class FighterCombat
    {
        readonly Fighter f;
        public float readyAt;
        public float windupUntil;      // enemies can see a kick coming (doc §5.1) and side-step
        public AttackKind current;
        public Fighter target;
        bool hitPending;
        float endAt, fallbackHitAt;
        int punchIndex;

        static readonly List<Fighter> victims = new List<Fighter>(32);

        public FighterCombat(Fighter f) { this.f = f; }

        static GameConfig Cfg => GameConfig.Current;

        public bool CanAttack(float now) => now >= readyAt;

        public float Range(AttackKind k)
        {
            var c = Cfg.combat;
            switch (k)
            {
                case AttackKind.Kick: return c.kickRange;
                case AttackKind.Bat: return c.punchRange * Cfg.moves.bat.rangeMul;
                case AttackKind.Shovel: return Cfg.moves.shovel.range;
                default: return c.punchRange;
            }
        }

        public AttackKind Choose(float dist)
        {
            if (f.weapon == MoveKind.Bat) return AttackKind.Bat;
            if (f.weapon == MoveKind.Shovel) return AttackKind.Shovel;
            if (f.powerPunchCharges > 0) return AttackKind.Punch;
            // kicks reach further, punches are faster; personality decides the mix
            if (dist > Cfg.combat.punchRange * 0.95f) return AttackKind.Kick;
            return Random.value < f.pw.kickBias * 0.6f ? AttackKind.Kick : AttackKind.Punch;
        }

        public void Start(AttackKind k, Fighter t, float now)
        {
            current = k;
            target = t;
            hitPending = true;
            f.state = FState.Attacking;
            f.motor.Stop();
            if (t != null) f.motor.FaceTo(t.Pos);
            int state;
            float cooldown;
            switch (k)
            {
                case AttackKind.Kick:
                    state = FighterAnimation.Kick; cooldown = Cfg.combat.kickCooldown;
                    windupUntil = now + Cfg.combat.kickWindupSec;
                    break;
                case AttackKind.Bat:
                    state = FighterAnimation.Bat; cooldown = Cfg.combat.punchCooldown * 1.2f;
                    break;
                case AttackKind.Shovel:
                    state = FighterAnimation.Shovel; cooldown = Cfg.moves.shovel.cooldown;
                    break;
                default:
                    state = punchIndex == 0 ? FighterAnimation.Punch1 : punchIndex == 1 ? FighterAnimation.Punch2 : FighterAnimation.Punch3;
                    punchIndex = (punchIndex + 1) % 3;
                    cooldown = Cfg.combat.punchCooldown;
                    if (f.powerPunchCharges > 0) state = FighterAnimation.Punch2; // the big cross
                    break;
            }
            float slow = f.hp < Cfg.health.lowHp ? 1f / Cfg.health.lowAttackSpeedMul : 1f;
            float dur = f.anim.Duration(state);
            f.anim.Play(state, 0.06f);
            endAt = now + dur * 0.85f;
            fallbackHitAt = now + dur * 0.6f;
            readyAt = now + Mathf.Max(cooldown * slow, dur * 0.8f);
        }

        public void Cancel()
        {
            hitPending = false;
            windupUntil = 0f;
            target = null;
            if (f.state == FState.Attacking) f.state = FState.Idle;
            f.motor.ClearFace();
        }

        /// <summary>Animation Event "OnAnimHit" (contact frame).</summary>
        public void OnAnimHit()
        {
            if (!hitPending || f.state != FState.Attacking) return;
            hitPending = false;
            Resolve();
        }

        public void Tick(float now)
        {
            if (f.state != FState.Attacking) return;
            if (target != null && target.IsAlive) f.motor.FaceTo(target.Pos);
            if (hitPending && now >= fallbackHitAt) OnAnimHit(); // event lost in a cross-fade
            if (now >= endAt)
            {
                hitPending = false;
                f.motor.ClearFace();
                f.ToIdle();
            }
        }

        void Resolve()
        {
            var c = Cfg.combat;
            float dmgMul = f.giant ? Cfg.moves.giant.damageMul : 1f;
            float range = Range(current) * (f.giant ? Cfg.moves.giant.scale : 1f);
            float arc = c.hitArcDeg;
            var hit = new HitInfo { attacker = f, kind = HitKind.Light, push = c.lightPush, move = MoveKind.None };
            bool powered = false;
            switch (current)
            {
                case AttackKind.Punch:
                    if (f.powerPunchCharges > 0)
                    {
                        powered = true;
                        hit.damage = Cfg.moves.powerPunch.damage;
                        hit.kind = HitKind.Launch; hit.push = c.launchBackSpeed; hit.up = c.launchUpSpeed * 0.8f;
                        hit.strong = true; hit.move = MoveKind.PowerPunch;
                    }
                    else hit.damage = c.punchDamage;
                    break;
                case AttackKind.Kick:
                    hit.damage = c.kickDamage; hit.kind = HitKind.Heavy; hit.push = c.heavyPush; hit.strong = true;
                    break;
                case AttackKind.Bat:
                    hit.damage = c.punchDamage * Cfg.moves.bat.damageMul; hit.kind = HitKind.Heavy; hit.push = c.heavyPush * 1.2f; hit.strong = true; hit.move = MoveKind.Bat;
                    break;
                case AttackKind.Shovel:
                    hit.damage = Cfg.moves.shovel.damage; hit.kind = HitKind.Heavy; hit.push = c.heavyPush * 1.3f; hit.strong = true; hit.move = MoveKind.Shovel;
                    arc = Cfg.moves.shovel.arcDeg;
                    break;
            }
            hit.damage *= dmgMul;
            if (f.giant && hit.kind == HitKind.Heavy) { hit.kind = HitKind.Launch; hit.push = c.launchBackSpeed * 0.8f; hit.up = c.launchUpSpeed * 0.7f; }

            var fwd = f.transform.forward; fwd.y = 0; fwd.Normalize();
            GameManager.I.Fighters.Query(f.Pos, range + 0.6f, victims, f);
            int hits = 0;
            for (int i = 0; i < victims.Count; i++)
            {
                var v = victims[i];
                if (!v.IsAlive || v.motor.airborne) continue;
                var to = v.Pos - f.Pos; to.y = 0;
                float dist = to.magnitude;
                if (dist > range + 0.35f * v.sizeMul) continue;
                if (dist > 0.05f && Vector3.Angle(fwd, to) > arc * 0.5f) continue;
                var h = hit;
                h.dir = dist > 0.05f ? to / dist : fwd;
                bool wasAlive = v.IsAlive;
                v.TakeHit(h);
                hits++;
                if (wasAlive && (!v.IsAlive || v.state == FState.Down || v.state == FState.Airborne)) f.knockedSomeoneAt = Time.time;
                Fx.I.HitBurst(v.Pos + Vector3.up * (1.1f * v.sizeMul), hit.strong);
                if (current == AttackKind.Punch && !powered) break; // a jab hits one character
            }
            if (powered && hits > 0)
            {
                f.powerPunchCharges--;
                Fx.I.GroundRing(f.Pos + fwd * 0.8f, 0.2f, 1.4f, 0.4f, new Color(1f, 0.6f, 0.2f, 1f));
            }
        }
    }
}
