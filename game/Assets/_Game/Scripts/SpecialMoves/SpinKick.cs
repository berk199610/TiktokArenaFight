using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// 100-299 coins Döner Tekme: lifts a leg and spins; every turn hits everyone within 2 m for 50 + push,
    /// 3 turns in 3 s (doc §4). Wind ring around the foot (§6.4).
    /// </summary>
    public class SpinKick : SpecialMove
    {
        public override bool ControlsBody => true;
        float nextHitAt, hitInterval;
        int spinsLeft;
        Transform wind;
        static readonly List<Fighter> victims = new List<Fighter>(32);

        protected override void Begin(int count, float now)
        {
            var c = Cfg.moves.spinKick;
            duration = c.durationSec;
            endsAt = now + Mathf.Min(Cfg.moves.stackMaxSec, duration * Mathf.Max(1, count)) + 0.3f;
            spinsLeft = c.spins * Mathf.Max(1, count);
            hitInterval = c.durationSec / Mathf.Max(1, c.spins);
            nextHitAt = now + hitInterval * 0.5f;
            f.spinSpeed = 360f / hitInterval;
            f.anim.Play(FighterAnimation.SpinKick, 0.08f);
            wind = Fx.AttachedSpin(f.transform, false);
            wind.localScale = Vector3.one * (c.radius / 1.9f);
            GameManager.I.Fighters.AddHazard(f, c.radius);
        }

        public override void Extend(int count, float now)
        {
            base.Extend(count, now);
            spinsLeft += Cfg.moves.spinKick.spins * count;
        }

        public override void Tick(float dt, float now)
        {
            var c = Cfg.moves.spinKick;
            // drift slowly toward the nearest enemy
            var target = GameManager.I.Fighters.NearestEnemy(f, 6f);
            if (target != null) f.motor.Move(target.Pos - f.Pos, 1.4f);
            else f.motor.Stop();
            if (wind) wind.Rotate(0, -900f * dt, 0, Space.World);
            if (now >= nextHitAt && spinsLeft > 0)
            {
                nextHitAt = now + hitInterval;
                spinsLeft--;
                float r = c.radius * (f.giant ? Cfg.moves.giant.scale : 1f);
                GameManager.I.Fighters.Query(f.Pos, r + 0.4f, victims, f);
                foreach (var v in victims)
                {
                    if (!v.IsAlive || v.motor.airborne) continue;
                    var to = v.Pos - f.Pos; to.y = 0;
                    if (to.magnitude > r + 0.3f) continue;
                    var dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward;
                    v.TakeHit(new HitInfo { attacker = f, damage = c.damagePerSpin * (f.giant ? Cfg.moves.giant.damageMul : 1f), kind = HitKind.Launch, dir = dir, push = c.push, up = 3.5f, strong = true, move = MoveKind.SpinKick });
                    Fx.I.HitBurst(v.Pos + Vector3.up * 1.1f, true);
                }
                Fx.I.GroundRing(f.Pos, 0.4f, r, 0.35f, new Color(0.7f, 0.9f, 1f, 0.9f));
            }
            if (spinsLeft <= 0 && now >= nextHitAt - hitInterval * 0.5f) finished = true;
        }

        public override void End()
        {
            f.spinSpeed = 0f;
            if (wind) Object.Destroy(wind.gameObject);
            GameManager.I.Fighters.RemoveHazard(f);
        }
    }
}
