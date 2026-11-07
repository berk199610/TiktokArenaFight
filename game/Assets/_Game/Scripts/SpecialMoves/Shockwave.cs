using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// 300-999 coins Yumruk Dalgası: jumps, hits the ground, a ring-shaped shock wave; everyone within 4 m takes
    /// 150 damage and falls down (doc §4). Bright expanding ground ring (§6.4).
    /// </summary>
    public class Shockwave : SpecialMove
    {
        public override bool ControlsBody => true;
        float slamAt, endAt;
        bool slammed;
        int remaining;
        static readonly List<Fighter> victims = new List<Fighter>(64);

        protected override void Begin(int count, float now)
        {
            remaining = Mathf.Clamp(count, 1, 3);
            duration = 0f;
            StartJump(now);
        }

        void StartJump(float now)
        {
            var c = Cfg.moves.shockwave;
            slammed = false;
            slamAt = now + c.jumpSec;
            endAt = slamAt + 0.75f;
            f.motor.Stop();
            f.motor.Launch(Vector3.up * (Cfg.combat.gravity * c.jumpSec * 0.5f));
            f.anim.Play(FighterAnimation.Slam, 0.05f);
            GameManager.I.Fighters.AddHazard(f, c.radius);
        }

        public override void Extend(int count, float now) { remaining = Mathf.Min(remaining + count, 3); }

        public override void Tick(float dt, float now)
        {
            var c = Cfg.moves.shockwave;
            if (!slammed && now >= slamAt && !f.motor.airborne)
            {
                slammed = true;
                float r = c.radius * (f.giant ? Cfg.moves.giant.scale : 1f);
                Fx.I.GroundRing(f.Pos, 0.3f, r, 0.55f, new Color(1f, 0.85f, 0.4f, 1f));
                Fx.I.GroundRing(f.Pos, 0.2f, r * 0.7f, 0.4f, new Color(1f, 1f, 1f, 1f));
                Fx.I.Dust(f.Pos, 22, 4f);
                GameManager.I.Fighters.Query(f.Pos, r + 0.4f, victims, f);
                foreach (var v in victims)
                {
                    if (!v.IsAlive) continue;
                    var to = v.Pos - f.Pos; to.y = 0;
                    if (to.magnitude > r) continue;
                    var dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward;
                    float k = 1f - to.magnitude / r;
                    v.TakeHit(new HitInfo { attacker = f, damage = c.damage * (f.giant ? Cfg.moves.giant.damageMul : 1f), kind = HitKind.Launch, dir = dir, push = 2.5f + 4f * k, up = 3f + 3f * k, strong = true, move = MoveKind.Shockwave });
                }
                GameManager.I.Fighters.RemoveHazard(f);
            }
            if (slammed && now >= endAt)
            {
                remaining--;
                if (remaining > 0) StartJump(now);
                else finished = true;
            }
        }

        public override void End() { GameManager.I.Fighters.RemoveHazard(f); }
    }
}
