using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// 5.000+ coins Legendary Tornado: the character spins like a twister (fast) and roams the arena; nearby fighters
    /// are sucked in, everyone it touches takes 40 damage every 0.5 s and is flung around (doc §4). Big layered funnel,
    /// orbiting debris, dust ring, flashes (§6.4, user request: "daha ihtişamlı").
    /// </summary>
    public class Tornado : SpecialMove
    {
        public override bool ControlsBody => true;
        Transform cone;
        float nextTick, nextDebris, retargetAt, nextFlash;
        Vector3 goal;
        float radius;
        static readonly List<Fighter> victims = new List<Fighter>(64);

        protected override void Begin(int count, float now)
        {
            var c = Cfg.moves.tornado;
            duration = c.durationSec;
            endsAt = now + Mathf.Min(Cfg.moves.stackMaxSec, duration * Mathf.Max(1, count) * power);
            radius = c.radius * (1f + (power - 1f) * 0.25f);
            f.spinSpeed = c.spinDegPerSec;
            f.anim.Play(FighterAnimation.Tornado, 0.1f);
            cone = Fx.AttachedSpin(f.transform, true);
            cone.localScale = Vector3.one * (radius / 1.8f);
            GameManager.I.Fighters.AddHazard(f, radius + 1f);
            nextTick = now + 0.3f;
            Fx.I.GroundRing(f.Pos, 0.5f, radius * 2.2f, 0.8f, new Color(0.8f, 0.85f, 1f, 1f));
            Fx.I.Word(f.Pos + Vector3.up * 3f, "TORNADO!", new Color(0.75f, 0.85f, 1f), 1.7f);
            PickGoal();
        }

        void PickGoal()
        {
            var e = GameManager.I.Fighters.NearestEnemy(f, 30f);
            float safe = GameManager.I.SafeRadius - 1.5f;
            if (e != null) goal = e.Pos;
            else { var r = Random.insideUnitCircle * Mathf.Max(2f, safe * 0.7f); goal = new Vector3(r.x, 0, r.y); }
        }

        public override void Tick(float dt, float now)
        {
            var c = Cfg.moves.tornado;
            if (now >= retargetAt) { retargetAt = now + 0.8f; PickGoal(); }
            var to = goal - f.Pos; to.y = 0;
            var wobble = new Vector3(Mathf.Sin(now * 3.1f), 0, Mathf.Cos(now * 2.3f)) * 0.6f;
            f.motor.Move(to.normalized + wobble, c.speed);
            f.spinSpeed = c.spinDegPerSec;
            if (cone)
            {
                cone.Rotate(0, -1080f * dt, 0, Space.World);
                float pulse = 1f + Mathf.Sin(now * 9f) * 0.05f;
                cone.localScale = Vector3.one * (radius / 1.8f) * pulse;
            }

            // orbiting debris and dust around the funnel
            if (now >= nextDebris)
            {
                nextDebris = now + 0.03f;
                Fx.I.TornadoDebris(f.Pos, radius, now);
            }
            if (now >= nextFlash)
            {
                nextFlash = now + Random.Range(0.6f, 1.4f);
                Fx.I.Impact(f.Pos + Vector3.up * Random.Range(1.5f, 4f) + Random.insideUnitSphere * radius, ImpactStyle.Thunder, false);
            }

            // suction: pull nearby fighters toward the funnel
            GameManager.I.Fighters.Query(f.Pos, radius * 2.6f, victims, f);
            foreach (var v in victims)
            {
                if (!v.IsAlive || v.motor.airborne || v.giant) continue;
                var d = f.Pos - v.Pos; d.y = 0;
                float dist = d.magnitude;
                if (dist < 0.1f) continue;
                var tangent = Vector3.Cross(Vector3.up, d / dist);
                v.motor.AddPush((d / dist * 0.8f + tangent * 0.6f) * c.pull * dt * 4f);
            }

            if (now >= nextTick)
            {
                nextTick = now + c.tickSec;
                float r = radius * (f.giant ? Cfg.moves.giant.scale : 1f);
                GameManager.I.Fighters.Query(f.Pos, r + 0.4f, victims, f);
                foreach (var v in victims)
                {
                    if (!v.IsAlive || v.motor.airborne) continue;
                    var d = v.Pos - f.Pos; d.y = 0;
                    if (d.magnitude > r + 0.3f) continue;
                    var outDir = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
                    var tangent = Vector3.Cross(Vector3.up, outDir);
                    v.TakeHit(new HitInfo { attacker = f, damage = c.damage * (f.giant ? Cfg.moves.giant.damageMul : 1f), kind = HitKind.Heavy, dir = (outDir + tangent * 0.9f).normalized, push = c.fling, force = Cfg.combat.forceTornado, strong = false, move = MoveKind.Tornado });
                    Fx.I.Impact(v.Pos + Vector3.up, ImpactStyle.Wind);
                }
            }
        }

        public override void End()
        {
            f.spinSpeed = 0f;
            if (cone) Object.Destroy(cone.gameObject);
            GameManager.I.Fighters.RemoveHazard(f);
            Fx.I.Dust(f.Pos, 20, 4f);
        }
    }
}
