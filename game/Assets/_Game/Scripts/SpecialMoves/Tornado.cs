using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// 5.000+ coins Efsanevi Tornado: spins like a twister around the arena for 10 s; everyone it touches takes 40
    /// damage every 0.5 s and is flung away (doc §4). Spinning striped cone + flying dust (§6.4).
    /// </summary>
    public class Tornado : SpecialMove
    {
        public override bool ControlsBody => true;
        Transform cone;
        float nextTick, nextDust, retargetAt;
        Vector3 goal;
        static readonly List<Fighter> victims = new List<Fighter>(64);

        protected override void Begin(int count, float now)
        {
            var c = Cfg.moves.tornado;
            duration = c.durationSec;
            endsAt = now + Mathf.Min(Cfg.moves.stackMaxSec, duration * Mathf.Max(1, count));
            f.spinSpeed = 900f;
            f.anim.Play(FighterAnimation.Tornado, 0.1f);
            cone = Fx.AttachedSpin(f.transform, true);
            cone.localScale = Vector3.one * (c.radius / 1.6f);
            GameManager.I.Fighters.AddHazard(f, c.radius + 0.5f);
            nextTick = now;
            PickGoal();
        }

        void PickGoal()
        {
            var e = GameManager.I.Fighters.NearestEnemy(f, 30f);
            float safe = GameManager.I.SafeRadius - 1f;
            if (e != null) goal = e.Pos;
            else { var r = Random.insideUnitCircle * Mathf.Max(2f, safe * 0.7f); goal = new Vector3(r.x, 0, r.y); }
        }

        public override void Tick(float dt, float now)
        {
            var c = Cfg.moves.tornado;
            if (now >= retargetAt) { retargetAt = now + 0.8f; PickGoal(); }
            var to = goal - f.Pos; to.y = 0;
            var wobble = new Vector3(Mathf.Sin(now * 3.1f), 0, Mathf.Cos(now * 2.3f)) * 0.5f;
            f.motor.Move(to.normalized + wobble, c.speed);
            if (cone) cone.Rotate(0, -720f * dt, 0, Space.World);
            if (now >= nextDust) { nextDust = now + 0.08f; Fx.I.Dust(f.Pos, 2, 3f); }
            if (now >= nextTick)
            {
                nextTick = now + c.tickSec;
                float r = c.radius * (f.giant ? Cfg.moves.giant.scale : 1f);
                GameManager.I.Fighters.Query(f.Pos, r + 0.4f, victims, f);
                foreach (var v in victims)
                {
                    if (!v.IsAlive || v.motor.airborne) continue;
                    var d = v.Pos - f.Pos; d.y = 0;
                    if (d.magnitude > r + 0.3f) continue;
                    var outDir = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
                    var tangent = Vector3.Cross(Vector3.up, outDir);
                    v.TakeHit(new HitInfo { attacker = f, damage = c.damage * (f.giant ? Cfg.moves.giant.damageMul : 1f), kind = HitKind.Launch, dir = (outDir + tangent * 0.8f).normalized, push = c.fling, up = 6f, strong = false, move = MoveKind.Tornado });
                    Fx.I.HitBurst(v.Pos + Vector3.up, true);
                }
            }
        }

        public override void End()
        {
            f.spinSpeed = 0f;
            if (cone) Object.Destroy(cone.gameObject);
            GameManager.I.Fighters.RemoveHazard(f);
        }
    }
}
