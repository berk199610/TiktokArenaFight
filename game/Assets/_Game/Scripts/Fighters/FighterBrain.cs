using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    public enum Plan { None, Attack, Revenge, Strafe, Retreat, Center, Taunt, Rest, Wander, Dizzy, WrongWay, Dodge }

    /// <summary>
    /// Utility AI (doc §5): every 0.2-0.4 s (each brain on its own clock) it looks around, scores every option and
    /// does the best one; near-ties are broken at random so nobody looks robotic. Intelligence (§5.5) sets reaction
    /// time, dodging and target quality; a dull character stands dizzy, walks the wrong way, never dodges.
    /// </summary>
    public class FighterBrain
    {
        readonly Fighter f;
        public Plan plan;
        public Fighter target;
        float nextThink;
        float planUntil;
        int strafeSign = 1;
        Vector3 wanderPoint;
        Plan pendingPlan;
        Fighter pendingTarget;
        float pendingAt;
        bool hasPending;
        float lastTauntAt = -99f;

        static readonly List<Fighter> near = new List<Fighter>(64);
        static readonly List<(Plan plan, Fighter target, float score)> options = new List<(Plan, Fighter, float)>(8);

        public FighterBrain(Fighter f) { this.f = f; }

        static GameConfig Cfg => GameConfig.Current;

        public void Reset(float now)
        {
            ClearTarget();
            plan = Plan.None;
            hasPending = false;
            nextThink = now + Random.Range(0f, Cfg.ai.thinkMax);
            strafeSign = Random.value < 0.5f ? -1 : 1;
        }

        public void SetTarget(Fighter t)
        {
            if (target == t) return;
            if (target != null) target.targetedBy = Mathf.Max(0, target.targetedBy - 1);
            target = t;
            if (target != null) target.targetedBy++;
        }

        public void ClearTarget() => SetTarget(null);

        public void ForceThink() => nextThink = 0f;

        public void Tick(float dt, float now)
        {
            if (now >= nextThink)
            {
                nextThink = now + Random.Range(Cfg.ai.thinkMin, Cfg.ai.thinkMax);
                Think(now);
            }
            if (hasPending && now >= pendingAt)
            {
                hasPending = false;
                Apply(pendingPlan, pendingTarget, now);
            }
            Act(dt, now);
        }

        float ReactionTime()
        {
            var ic = Cfg.intelligence;
            float i = f.intelligence;
            float t = i >= 0.95f ? ic.reactionFull : i >= 0.45f ? Mathf.Lerp(ic.reactionHalf, ic.reactionFull, (i - 0.5f) / 0.5f) : Mathf.Lerp(ic.reactionMin, ic.reactionHalf, (i - 0.1f) / 0.4f);
            return Mathf.Max(0.05f, t * f.reactionMul);
        }

        float DodgeChance()
        {
            var ic = Cfg.intelligence;
            if (f.intelligence >= 0.95f) return ic.dodgeChanceFull;
            if (f.intelligence >= 0.45f) return ic.dodgeChanceHalf;
            return 0f;
        }

        // ------------------------------------------------------------------ perception + decision
        void Think(float now)
        {
            var ai = Cfg.ai;
            var gm = GameManager.I;
            float intel = f.intelligence;
            bool dull = intel <= Cfg.intelligence.minValue + 0.05f && !f.IsBot;

            // dull: frequent dizzy pauses ("zzz") and walking the wrong way (doc §5.5)
            if (dull && plan != Plan.WrongWay)
            {
                float roll = Random.value;
                if (roll < Cfg.intelligence.dullDizzyChance * 0.12f) { Decide(Plan.Dizzy, null, now, ReactionTime()); return; }
                if (roll < (Cfg.intelligence.dullDizzyChance + Cfg.intelligence.dullWrongWayChance) * 0.12f) { Decide(Plan.WrongWay, null, now, ReactionTime()); return; }
            }

            gm.Fighters.Query(f.Pos, ai.perceptionRadius, near, f);
            options.Clear();

            // threats: an enemy winding up a kick at me -> side-step (smart only)
            float dodge = DodgeChance();
            if (dodge > 0f)
            {
                for (int i = 0; i < near.Count; i++)
                {
                    var e = near[i];
                    if (e.state != FState.Attacking || e.combat.windupUntil < now || e.combat.target != f) continue;
                    if ((e.Pos - f.Pos).magnitude > Cfg.combat.kickRange + 0.8f) continue;
                    if (Random.value < dodge) { Decide(Plan.Dodge, e, now, ReactionTime() * 0.5f); return; }
                    break;
                }
            }

            // hazards: tornado, spin kick, shock wave
            Vector3 hazardAway = Vector3.zero;
            float hazardScore = 0f;
            foreach (var hz in gm.Fighters.Hazards)
            {
                if (hz.owner == f || hz.owner == null || !hz.owner.IsAlive) continue;
                var d = f.Pos - hz.owner.Pos; d.y = 0;
                float dist = d.magnitude;
                if (dist < hz.radius + 1.6f)
                {
                    hazardScore = Mathf.Max(hazardScore, 1.25f * Mathf.Lerp(0.5f, 1f, intel));
                    hazardAway += dist > 0.01f ? d / dist : Random.onUnitSphere;
                }
            }

            // targets
            Fighter best = null, nearest = null;
            float bestScore = float.MinValue, nearestDist = float.MaxValue;
            int closeEnemies = 0;
            Vector3 threatCentroid = Vector3.zero;
            int enemies = 0;
            bool special = f.powerPunchCharges > 0 || f.weapon != MoveKind.None || f.giant;
            for (int i = 0; i < near.Count; i++)
            {
                var e = near[i];
                if (!e.IsTargetable || e.state == FState.Victory) continue;
                var to = e.Pos - f.Pos; to.y = 0;
                float d = to.magnitude;
                enemies++;
                if (d < nearestDist) { nearestDist = d; nearest = e; }
                if (d < 3f) { closeEnemies++; threatCentroid += e.Pos; }
                // crowd rule: at most N attackers per target (doc §5.3)
                if (e.targetedBy >= ai.maxAttackersPerTarget && e != target) continue;
                float close = 1f - d / ai.perceptionRadius;
                float s = f.pw.aggression * 0.5f + close * 0.65f * f.pw.closeness + (1f - e.Hp01) * 0.7f * f.pw.lowHpHunt;
                if (gm.Cam != null && gm.Cam.InView(e.Pos)) s += 0.2f;     // camera rule
                if (e == target) s += 0.12f;                                // stickiness
                else s -= 0.1f * e.targetedBy;                              // spread fights into small groups (doc §5.3)
                if (e.IsGifter && !special) s -= 0.05f;                     // gifters are a bit safer
                s += Random.Range(-0.05f, 0.05f);
                if (s > bestScore) { bestScore = s; best = e; }
            }
            if (best != null && intel < 0.6f && Random.value < Cfg.intelligence.wrongTargetChanceHalf)
            {
                // half-smart: sometimes picks a worse target
                for (int i = 0; i < near.Count; i++)
                    if (near[i].IsTargetable && Random.value < 0.3f) { best = near[i]; break; }
            }
            if (best != null) options.Add((Plan.Attack, best, bestScore + (special ? 0.45f : 0f)));

            // revenge
            if (f.lastHitBy != null && f.lastHitBy.IsTargetable && now - f.lastHitTime < ai.revengeSec)
            {
                float d = (f.lastHitBy.Pos - f.Pos).magnitude;
                if (d < ai.perceptionRadius) options.Add((Plan.Revenge, f.lastHitBy, 0.45f + 0.75f * f.pw.vengeance));
            }

            // strafe / circle around
            if (nearest != null && nearestDist > 1.8f && nearestDist < 5f && !special)
                options.Add((Plan.Strafe, nearest, 0.18f + 0.6f * f.pw.caution + Random.Range(0f, 0.1f)));

            // retreat
            float lowHp = Cfg.health.lowHp;
            if (hazardScore > 0f) options.Add((Plan.Retreat, null, hazardScore));
            else if (f.hp < lowHp && closeEnemies >= 2 && !special)
                options.Add((Plan.Retreat, null, 0.35f + 0.65f * f.pw.caution + (1f - f.Hp01) * 0.3f));

            // back to the middle: near the edge / outside the fire ring / not on camera
            float r = new Vector2(f.Pos.x, f.Pos.z).magnitude;
            float edge = Mathf.Min(ai.edgeRadius, gm.SafeRadius - 1.5f);
            if (r > edge) options.Add((Plan.Center, null, 0.5f + (r - edge) * 0.4f + (gm.SafeRadius < Cfg.arena.radius - 0.5f && r > gm.SafeRadius - 0.8f ? 1.5f : 0f)));
            else if (gm.Cam != null && !gm.Cam.InView(f.Pos)) options.Add((Plan.Center, null, 0.95f));

            // taunt after knocking someone down
            if (now - f.knockedSomeoneAt < ai.tauntWindowSec && f.knockedSomeoneAt > lastTauntAt)
                options.Add((Plan.Taunt, null, 0.25f + 0.85f * f.pw.showoff));

            // breathe
            if (Random.value < ai.restChance) options.Add((Plan.Rest, null, 0.3f + 0.2f * f.pw.caution));

            if (enemies == 0) options.Add((Plan.Wander, null, 0.3f));

            if (options.Count == 0) return;
            // best two; near-tie -> random between them (doc §5.2)
            int i1 = 0;
            for (int i = 1; i < options.Count; i++) if (options[i].score > options[i1].score) i1 = i;
            int i2 = -1;
            for (int i = 0; i < options.Count; i++) if (i != i1 && (i2 < 0 || options[i].score > options[i2].score)) i2 = i;
            var pick = options[i1];
            if (i2 >= 0 && options[i1].score - options[i2].score < ai.tieEpsilon && Random.value < 0.5f) pick = options[i2];

            // keep the current plan when it is the same (no reaction delay for continuing)
            if (pick.plan == plan && pick.target == target && (plan == Plan.Attack || plan == Plan.Revenge || plan == Plan.Strafe)) return;
            if (pick.plan == Plan.Retreat && hazardAway != Vector3.zero) wanderPoint = f.Pos + hazardAway.normalized * 4f;
            else if (pick.plan == Plan.Retreat && closeEnemies > 0) wanderPoint = f.Pos + (f.Pos - threatCentroid / closeEnemies).normalized * 4f;
            Decide(pick.plan, pick.target, now, ReactionTime());
        }

        void Decide(Plan p, Fighter t, float now, float delay)
        {
            pendingPlan = p;
            pendingTarget = t;
            pendingAt = now + delay;
            hasPending = true;
        }

        void Apply(Plan p, Fighter t, float now)
        {
            if (!f.BrainDriven) return;
            plan = p;
            var ai = Cfg.ai;
            switch (p)
            {
                case Plan.Attack:
                case Plan.Revenge:
                case Plan.Strafe:
                    SetTarget(t);
                    planUntil = now + (p == Plan.Strafe ? Random.Range(0.8f, 1.6f) : 6f);
                    if (p == Plan.Strafe && Random.value < 0.3f) strafeSign = -strafeSign;
                    break;
                case Plan.Taunt:
                    lastTauntAt = now;
                    ClearTarget();
                    f.state = FState.Taunting;
                    bool dance = f.personality == PersonalityType.Showman && Random.value < 0.6f;
                    f.anim.Play(dance ? FighterAnimation.Dance : FighterAnimation.Yes, 0.12f);
                    f.stateUntil = now + (dance ? 1.8f : 1.2f);
                    f.motor.Stop();
                    plan = Plan.None;
                    break;
                case Plan.Rest:
                    ClearTarget();
                    planUntil = now + Random.Range(0.6f, 1.4f);
                    break;
                case Plan.Dizzy:
                    ClearTarget();
                    f.state = FState.Dizzy;
                    f.stateUntil = now + Random.Range(1.8f, 3.5f);
                    f.anim.Play(FighterAnimation.Dizzy, 0.2f);
                    f.motor.Stop();
                    plan = Plan.None;
                    break;
                case Plan.WrongWay:
                    ClearTarget();
                    var away = f.Pos.sqrMagnitude > 0.5f ? f.Pos.normalized : Random.insideUnitSphere;
                    away.y = 0;
                    wanderPoint = f.Pos + (away + Random.insideUnitSphere * 0.5f).normalized * 4f;
                    planUntil = now + Random.Range(1.2f, 2.2f);
                    break;
                case Plan.Dodge:
                {
                    ClearTarget();
                    var from = t != null ? (f.Pos - t.Pos) : -f.transform.forward;
                    from.y = 0;
                    var side = Vector3.Cross(Vector3.up, from.normalized) * (Random.value < 0.5f ? -1 : 1);
                    f.state = FState.Dodging;
                    f.stateUntil = now + 0.55f;
                    f.motor.SnapYaw(Mathf.Atan2(side.x, side.z) * Mathf.Rad2Deg);
                    f.motor.AddPush(side * 5f);
                    f.anim.Play(FighterAnimation.Dodge, 0.05f);
                    plan = Plan.None;
                    break;
                }
                case Plan.Retreat:
                    ClearTarget();
                    planUntil = now + 1.2f;
                    break;
                case Plan.Center:
                    ClearTarget();
                    var focus = GameManager.I.Cam != null ? GameManager.I.Cam.FocusPoint : Vector3.zero;
                    wanderPoint = QuietPoint(Vector3.Lerp(Vector3.zero, focus, 0.5f), 4f);
                    planUntil = now + 3f;
                    break;
                case Plan.Wander:
                    ClearTarget();
                    wanderPoint = QuietPoint(Vector3.zero, Mathf.Min(7f, GameManager.I.SafeRadius - 2f));
                    planUntil = now + 3f;
                    break;
            }
        }

        /// <summary>Of a few random points around <paramref name="center"/>, the one with the fewest characters nearby.</summary>
        Vector3 QuietPoint(Vector3 center, float radius)
        {
            Vector3 best = center;
            int bestN = int.MaxValue;
            for (int k = 0; k < 4; k++)
            {
                var c = Random.insideUnitCircle * radius;
                var p = center + new Vector3(c.x, 0, c.y);
                GameManager.I.Fighters.Query(p, 2.5f, near, f);
                if (near.Count < bestN) { bestN = near.Count; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------------ acting every frame
        float WalkSpeed(bool run)
        {
            var ai = Cfg.ai;
            float s = (run ? ai.runSpeed : ai.walkSpeed) * f.speedMul;
            if (f.hp < Cfg.health.lowHp) s *= Cfg.health.lowWalkMul;
            if (f.hp < Cfg.health.veryLowHp) s *= 0.75f;
            if (f.intelligence < 0.3f && !f.IsBot) s *= 0.8f;
            if (f.inactive) s *= 0.85f;
            if (f.giant) s *= 1.1f;
            return s;
        }

        void Act(float dt, float now)
        {
            if (!f.BrainDriven) return;
            var m = f.motor;
            switch (plan)
            {
                case Plan.Attack:
                case Plan.Revenge:
                {
                    var t = target;
                    if (t == null || !t.IsTargetable || now > planUntil) { ClearTarget(); plan = Plan.None; nextThink = 0f; m.Stop(); return; }
                    var to = t.Pos - f.Pos; to.y = 0;
                    float d = to.magnitude;
                    var kind = f.combat.Choose(d);
                    float range = f.combat.Range(kind) * (f.giant ? Cfg.moves.giant.scale : 1f) * 0.9f;
                    if (d > range)
                    {
                        // approach a point slightly to the side so attackers spread around the target
                        var side = Vector3.Cross(Vector3.up, to.normalized) * (strafeSign * 0.35f);
                        m.ClearFace();
                        m.Move((to.normalized + side * Mathf.Clamp01((d - range) / 3f)).normalized, WalkSpeed(d > 3f));
                    }
                    else
                    {
                        m.Stop();
                        m.FaceTo(t.Pos);
                        if (f.combat.CanAttack(now) && m.FacingAngleTo(t.Pos) < 40f)
                            f.combat.Start(f.combat.Choose(d), t, now);
                    }
                    break;
                }
                case Plan.Strafe:
                {
                    var t = target;
                    if (t == null || !t.IsTargetable || now > planUntil) { ClearTarget(); plan = Plan.None; nextThink = 0f; return; }
                    var to = t.Pos - f.Pos; to.y = 0;
                    float d = to.magnitude;
                    var tangent = Vector3.Cross(Vector3.up, to / Mathf.Max(0.01f, d)) * strafeSign;
                    var radial = to / Mathf.Max(0.01f, d) * Mathf.Clamp((d - 3f) * 0.5f, -1f, 1f);
                    m.FaceTo(t.Pos);
                    m.Move(tangent + radial, WalkSpeed(false));
                    break;
                }
                case Plan.Retreat:
                case Plan.Wander:
                case Plan.Center:
                case Plan.WrongWay:
                {
                    var to = wanderPoint - f.Pos; to.y = 0;
                    if (to.magnitude < 0.6f || now > planUntil) { plan = Plan.None; nextThink = 0f; m.Stop(); return; }
                    m.ClearFace();
                    m.Move(to, WalkSpeed(plan == Plan.Retreat || plan == Plan.Center));
                    break;
                }
                case Plan.Rest:
                    m.Stop();
                    if (now > planUntil) { plan = Plan.None; nextThink = 0f; }
                    break;
                default:
                    m.Stop();
                    break;
            }
        }
    }
}
