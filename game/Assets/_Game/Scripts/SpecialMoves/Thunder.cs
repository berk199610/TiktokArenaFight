using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// 2.500-4.999 coins Thunder (user request: big gifts between Giant and Tornado need their own moment): the
    /// character raises its arms and calls lightning down on enemies around the arena, one strike after another.
    /// A bigger gift = more strikes.
    /// </summary>
    public class Thunder : SpecialMove
    {
        public override bool ControlsBody => true;
        int strikesLeft;
        float nextStrike;
        static readonly List<Fighter> victims = new List<Fighter>(64);
        static readonly List<Fighter> candidates = new List<Fighter>(64);

        protected override void Begin(int count, float now)
        {
            var c = Cfg.moves.thunder;
            duration = 0f;
            strikesLeft = Mathf.RoundToInt(c.strikes * Mathf.Max(1, count) * power);
            nextStrike = now + 0.35f;
            endsAt = 0f;
            f.motor.Stop();
            f.anim.Play(FighterAnimation.Flex, 0.08f);
            Fx.I.Word(f.Pos + Vector3.up * 3f, "THUNDER!", new Color(0.6f, 0.85f, 1f), 1.6f);
        }

        public override void Extend(int count, float pow, float now) { strikesLeft += Mathf.RoundToInt(Cfg.moves.thunder.strikes * count * pow); }

        public override void Tick(float dt, float now)
        {
            var c = Cfg.moves.thunder;
            f.motor.Stop();
            if (strikesLeft <= 0) { if (now >= nextStrike) finished = true; return; }
            if (now < nextStrike) return;
            nextStrike = now + c.intervalSec;
            strikesLeft--;
            f.anim.Play(FighterAnimation.Flex, 0.05f);

            // target: a random enemy within reach (prefer ones near others), else a random spot
            GameManager.I.Fighters.Query(f.Pos, 11f, candidates, f);
            Vector3 at;
            Fighter pick = null;
            int tries = 0;
            while (candidates.Count > 0 && tries++ < 6)
            {
                var e = candidates[Random.Range(0, candidates.Count)];
                if (e.IsAlive && !e.motor.airborne) { pick = e; break; }
            }
            if (pick != null) at = pick.Pos;
            else { var r = Random.insideUnitCircle * 6f; at = new Vector3(r.x, 0, r.y); }

            Fx.I.Lightning(at);
            Fx.I.Impact(at + Vector3.up * 1.2f, ImpactStyle.Thunder);
            float rad = c.radius * (f.giant ? Cfg.moves.giant.scale : 1f);
            GameManager.I.Fighters.Query(at, rad + 0.4f, victims, f);
            foreach (var v in victims)
            {
                if (!v.IsAlive) continue;
                var d = v.Pos - at; d.y = 0;
                if (d.magnitude > rad) continue;
                var dir = d.sqrMagnitude > 0.01f ? d.normalized : (v.Pos - f.Pos).normalized;
                v.TakeHit(new HitInfo { attacker = f, damage = c.damage * (f.giant ? Cfg.moves.giant.damageMul : 1f), kind = HitKind.Heavy, dir = dir, push = 3f, force = Cfg.combat.forceThunder, strong = true, move = MoveKind.Thunder });
            }
        }
    }
}
