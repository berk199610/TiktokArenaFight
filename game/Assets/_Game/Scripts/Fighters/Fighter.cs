using UnityEngine;

namespace Arena
{
    public enum FState { Idle, Entering, Attacking, Staggered, Airborne, Down, GettingUp, Dead, Posing, Special, Taunting, Dizzy, Dodging, Waking, Ejected, Victory }

    public enum HitKind { Light, Heavy, Knockdown, Launch }

    public struct HitInfo
    {
        public Fighter attacker;
        public float damage;
        public HitKind kind;
        public Vector3 dir;      // push direction (flat)
        public float push;       // push speed (Light/Heavy) or backward launch speed (Launch)
        public float up;         // upward launch speed (Launch)
        public bool strong;      // hit stop (doc §6.2)
        public MoveKind move;    // which gift move caused it (None for basic attacks)
    }

    /// <summary>
    /// One character in the arena: health and state (DESIGN §3, §6). Owns its brain, motor, combat, animation and
    /// special-move runner. Pooled per costume; never destroyed during a stream.
    /// </summary>
    public class Fighter : MonoBehaviour
    {
        public int uid;
        public Viewer viewer;
        public Costume costume;
        public PersonalityType personality;
        public PersonalityWeights pw;

        public FighterMotor motor;
        public FighterCombat combat;
        public FighterBrain brain;
        public FighterAnimation anim;
        public SpecialMoveRunner specials;
        public FighterRig rig;
        public Transform body;          // scaled for bulk (muscular / skinny) and giant mode
        public Transform model;
        public Renderer blob;           // round blob shadow (doc §8.1)
        public Renderer aura;           // gold glow for gifters
        float modelScale = 1f;

        public bool active;
        public float hp;
        public FState state;
        public float stateUntil;

        public float intelligence = 1f; // DESIGN §5.5
        public bool inactive;           // activity check failed (§3): loses HP faster, weaker defence
        public float speedMul = 1f, reactionMul = 1f, animSpeed = 1f;

        public Fighter lastHitBy;
        public float lastHitTime = -99f;
        public int targetedBy;
        public float nameVisibleUntil;
        public float knockedSomeoneAt = -99f;
        public float wokeAt = -99f;
        public float spawnedAt;

        public MoveKind weapon = MoveKind.None;
        public Transform weaponObj;
        public int powerPunchCharges;
        public bool giant;
        public float sizeMul = 1f, sizeTarget = 1f;
        public float bulk = 1f;
        public float spinSpeed;
        public bool superArmor;
        float nextStarAt, nextSweatAt;
        Vector3 entryTarget;
        public float hurtFlashUntil;

        static GameConfig Cfg => GameConfig.Current;
        public Vector3 Pos => motor.pos;
        public float Hp01 => hp / Cfg.health.max;
        public bool IsAlive => active && state != FState.Dead && state != FState.Ejected;
        public bool IsTargetable => IsAlive && state != FState.Airborne && state != FState.Down && state != FState.GettingUp && state != FState.Entering;
        public bool IsBot => viewer != null && viewer.isBot;
        public string Name => viewer != null ? viewer.DisplayName : name;
        public bool IsGifter => viewer != null && Time.time - viewer.lastGiftTime <= Cfg.roster.giftProtectionSec;
        public bool BrainDriven => state == FState.Idle;
        public float HeadHeight => (rig.info.headTop + 0.15f) * sizeMul;
        public float ModelScale => modelScale;

        // ------------------------------------------------------------------ lifecycle
        public void Setup(int uid, Costume c, Transform body, Transform model, Animator animator, FighterRig rig, float modelScale)
        {
            this.uid = uid;
            costume = c;
            personality = c.personality;
            pw = Personality.For(c.personality);
            this.body = body;
            this.model = model;
            this.rig = rig;
            this.modelScale = modelScale;
            motor = new FighterMotor(this);
            combat = new FighterCombat(this);
            brain = new FighterBrain(this);
            anim = new FighterAnimation(this, animator);
            specials = new SpecialMoveRunner(this);
        }

        public void Activate(Viewer v, Vector3 pos, Vector3 runTo, float now)
        {
            viewer = v;
            v.fighter = this;
            name = "F_" + v.DisplayName;
            active = true;
            gameObject.SetActive(true);
            hp = Cfg.health.start;
            inactive = false;
            intelligence = v.isBot ? Cfg.intelligence.botValue : 1f;
            lastHitBy = null; lastHitTime = -99f; targetedBy = 0;
            knockedSomeoneAt = -99f; wokeAt = -99f;
            weapon = MoveKind.None; powerPunchCharges = 0; giant = false; sizeMul = sizeTarget = 1f; spinSpeed = 0; superArmor = false;
            spawnedAt = now;
            nameVisibleUntil = now + Cfg.names.showSec;
            // per-character variety (doc §5.3): speed ±10 %, reaction, animation speed
            var r = new System.Random(v.userId.GetHashCode());
            speedMul = 1f + ((float)r.NextDouble() * 2f - 1f) * Cfg.ai.speedVariance;
            reactionMul = 0.85f + (float)r.NextDouble() * 0.3f;
            animSpeed = 1f + ((float)r.NextDouble() * 2f - 1f) * Cfg.ai.animSpeedVariance;
            pw.kickBias = Mathf.Clamp01(Personality.For(personality).kickBias + ((float)r.NextDouble() - 0.5f) * 0.3f);
            bulk = v.isBot ? 1f : 1.1f;
            ApplyBodyScale();
            motor.Place(pos, Quaternion.LookRotation(runTo - pos).eulerAngles.y);
            combat.Cancel();
            brain.Reset(now);
            specials.Clear();
            anim.Reset(animSpeed);
            SetGifterLook(false, false);
            if (rig.crown) rig.crown.gameObject.SetActive(false);
            Accessories.SetGold(rig, v.record != null && v.record.goldUnlocked);
            entryTarget = runTo;
            state = FState.Entering;
            stateUntil = now + 2.2f;
        }

        public void Deactivate()
        {
            if (viewer != null && viewer.fighter == this) viewer.fighter = null;
            brain.ClearTarget();
            specials.Clear();
            DropWeapon();
            active = false;
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ per frame
        public void Tick(float dt, float now)
        {
            specials.Tick(dt, now);
            switch (state)
            {
                case FState.Idle:
                    brain.Tick(dt, now);
                    break;
                case FState.Entering:
                {
                    var to = entryTarget - motor.pos; to.y = 0;
                    if (to.magnitude < 1.2f || now >= stateUntil) { state = FState.Idle; motor.Stop(); }
                    else motor.Move(to.normalized, Cfg.ai.runSpeed * 1.15f);
                    break;
                }
                case FState.Attacking:
                    break;
                case FState.Staggered:
                case FState.Taunting:
                case FState.Dodging:
                case FState.Waking:
                case FState.Dizzy:
                    motor.Stop();
                    if (now >= stateUntil) ToIdle();
                    break;
                case FState.Down:
                    motor.Stop();
                    if (now >= stateUntil)
                    {
                        state = FState.GettingUp;
                        stateUntil = now + 1.0f;
                        anim.Play(FighterAnimation.GetUp, 0.1f);
                    }
                    break;
                case FState.GettingUp:
                    motor.Stop();
                    if (now >= stateUntil) ToIdle();
                    break;
                case FState.Dead:
                    motor.Stop();
                    if (now >= nextStarAt && !motor.airborne)
                    {
                        // "başında yıldızlar döner"
                        nextStarAt = now + 0.09f;
                        float a = now * 7f;
                        var head = rig.head != null ? rig.head.position : transform.position + Vector3.up;
                        Fx.I.Sparkle(head + new Vector3(Mathf.Cos(a) * 0.35f, 0.15f, Mathf.Sin(a) * 0.35f), new Color(1f, 0.9f, 0.3f), 1, 0.02f, 0.02f);
                    }
                    if (now >= stateUntil && !motor.airborne)
                    {
                        Fx.I.SmokePuff(transform.position);
                        GameManager.I.Fighters.Despawn(this);
                        return;
                    }
                    break;
                case FState.Ejected:
                    if (now >= stateUntil)
                    {
                        Fx.I.Dust(transform.position, 6);
                        GameManager.I.Fighters.Despawn(this);
                        return;
                    }
                    break;
            }

            combat.Tick(now);
            motor.Tick(dt);
            UpdateLook(dt, now);
            anim.Tick(motor.HorizontalSpeed, dt, now);
        }

        public void ToIdle()
        {
            if (!IsAlive) return;
            state = FState.Idle;
            spinSpeed = 0;
            anim.ToLoco();
        }

        void UpdateLook(float dt, float now)
        {
            // bulk: muscular after interaction / gifts, skinny the longer nobody interacts (user request, PDF p.17)
            if (viewer != null && !viewer.isBot)
            {
                var vc = Cfg.vigor;
                float since = now - Mathf.Max(viewer.lastInteraction, spawnedAt - 20f);
                float k = Mathf.InverseLerp(vc.fullForSec, vc.thinAfterSec, since);
                float target = Mathf.Lerp(IsGifter ? vc.maxBulk : 1.1f, vc.minBulk, k);
                if (IsGifter) target = Mathf.Max(target, vc.maxBulk);
                bulk = Mathf.MoveTowards(bulk, target, dt * 0.25f);
            }
            sizeMul = Mathf.MoveTowards(sizeMul, sizeTarget, dt * 1.2f);
            ApplyBodyScale();

            // very low HP: sweat drops; inactive: drooping, slow
            if (IsAlive && (hp < Cfg.health.veryLowHp || inactive) && now >= nextSweatAt && rig.head != null)
            {
                nextSweatAt = now + 0.35f;
                Fx.I.Sweat(rig.head.position);
            }
            anim.SetHurt(hp < Cfg.health.veryLowHp);
        }

        void ApplyBodyScale()
        {
            float s = sizeMul;
            body.localScale = new Vector3(s * bulk, s, s * bulk);
            if (blob != null) blob.transform.localScale = new Vector3(1.1f * s, 1.1f * s, 1);
        }

        public void SetGifterLook(bool gifter, bool topGifter)
        {
            if (aura != null) aura.enabled = gifter;
            if (rig.crown != null) rig.crown.gameObject.SetActive(topGifter && IsAlive);
        }

        // ------------------------------------------------------------------ health
        public void Heal(float amount, bool showRing)
        {
            if (!IsAlive) return;
            hp = Mathf.Min(Cfg.health.max, hp + amount);
            if (showRing) Fx.I.GroundRing(Pos, 0.4f, 1.6f, 0.7f, new Color(0.3f, 1f, 0.4f, 1f));
        }

        /// <summary>DESIGN §5.5: interaction wakes a dull character: "!" above head and a short shake.</summary>
        public void Wake(float now)
        {
            bool wasDull = intelligence < 0.95f || inactive;
            inactive = false;
            if (!IsBot) intelligence = 1f;
            if (wasDull && IsAlive)
            {
                wokeAt = now;
                if (state == FState.Idle || state == FState.Dizzy)
                {
                    state = FState.Waking;
                    stateUntil = now + 0.7f;
                    anim.Play(FighterAnimation.Wake, 0.08f);
                }
            }
        }

        public void TakeHit(in HitInfo h)
        {
            if (!IsAlive) return;
            float now = Time.time;
            float dmg = h.damage * (inactive ? Cfg.health.inactiveDamageTakenMul : 1f);
            hp -= dmg;
            hurtFlashUntil = now + 0.12f;
            if (h.attacker != null && h.attacker != this) { lastHitBy = h.attacker; lastHitTime = now; }
            nameVisibleUntil = now + Cfg.names.showSec;
            if (h.strong && h.attacker != null) { anim.HitStop(Cfg.combat.hitStopSec); h.attacker.anim.HitStop(Cfg.combat.hitStopSec); }

            if (hp <= 0f)
            {
                hp = 0f;
                if (h.kind == HitKind.Launch) Launch(h.dir, h.push, h.up);
                Die(h.attacker, h.move);
                return;
            }

            // giants and body-controlling moves only flinch
            bool armored = giant || superArmor || (specials.Current != null && specials.Current.ControlsBody);
            var kind = h.kind;
            if (armored) kind = HitKind.Light;
            else if (kind == HitKind.Heavy && hp < Cfg.health.veryLowHp && Random.value < Cfg.health.veryLowKnockdownChance) kind = HitKind.Knockdown;

            switch (kind)
            {
                case HitKind.Light:
                    anim.PlayHit(Random.value < 0.5f);
                    if (!giant) motor.AddPush(h.dir * Cfg.combat.lightPush);
                    break;
                case HitKind.Heavy:
                    combat.Cancel();
                    state = FState.Staggered;
                    stateUntil = now + Cfg.combat.heavyStaggerSec;
                    anim.Play(FighterAnimation.HitBig, 0.05f);
                    motor.AddPush(h.dir * h.push);
                    break;
                case HitKind.Knockdown:
                    Knockdown(h.dir, h.push * 0.5f);
                    break;
                case HitKind.Launch:
                    Launch(h.dir, h.push, h.up);
                    break;
            }
        }

        public void Knockdown(Vector3 dir, float push)
        {
            if (!IsAlive || giant) return;
            combat.Cancel();
            state = FState.Down;
            stateUntil = Time.time + 0.5f + Cfg.combat.knockdownSec;
            anim.Play(FighterAnimation.Fall, 0.05f);
            motor.AddPush(dir * push);
        }

        /// <summary>Super hits throw the character backwards through the air (user request): fly, land, lie, get up.</summary>
        public void Launch(Vector3 dir, float back, float up)
        {
            if (!active || giant) return;
            combat.Cancel();
            if (state != FState.Dead) state = FState.Airborne;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.001f) dir = -transform.forward;
            dir.Normalize();
            motor.Launch(dir * back + Vector3.up * up);
            anim.Play(FighterAnimation.Fall, 0.05f);
            // face the attacker while flying backwards
            motor.SnapYaw(Quaternion.LookRotation(-dir).eulerAngles.y);
        }

        public void OnLanded()
        {
            Fx.I.Dust(Pos, 8, 1.8f);
            if (state == FState.Airborne)
            {
                state = FState.Down;
                stateUntil = Time.time + Cfg.combat.knockdownSec;
                anim.Play(FighterAnimation.Lying, 0.1f);
            }
            else if (state == FState.Dead)
            {
                anim.Play(FighterAnimation.Lying, 0.1f);
                stateUntil = Mathf.Max(stateUntil, Time.time + 1.2f);
            }
            else if (state == FState.Ejected)
            {
                stateUntil = Mathf.Min(stateUntil, Time.time + 0.2f);
            }
        }

        public void Die(Fighter killer, MoveKind move)
        {
            if (state == FState.Dead) return;
            bool flying = motor.airborne;
            state = FState.Dead;
            stateUntil = Time.time + Cfg.death.lieSec + (flying ? 0.6f : 0f);
            combat.Cancel();
            brain.ClearTarget();
            specials.Clear();
            DropWeapon();
            giant = false; sizeTarget = 1f; spinSpeed = 0;
            if (!flying) anim.Play(FighterAnimation.Death, 0.08f);
            if (rig.crown) rig.crown.gameObject.SetActive(false);
            if (killer != null && killer != this) killer.knockedSomeoneAt = Time.time;
            GameManager.I.OnFighterKilled(this, killer, move);
        }

        /// <summary>Arena full and a gifter needs the slot: thrown comically out of the arena (doc §3).</summary>
        public void Eject()
        {
            if (!IsAlive) return;
            combat.Cancel();
            brain.ClearTarget();
            specials.Clear();
            DropWeapon();
            state = FState.Ejected;
            stateUntil = Time.time + 2.2f;
            var outDir = Pos.sqrMagnitude > 0.01f ? Pos.normalized : Vector3.forward;
            motor.ignoreWall = true;
            motor.Launch(outDir * 9f + Vector3.up * 11f);
            spinSpeed = 720f;
            anim.Play(FighterAnimation.Fall, 0.05f);
        }

        public void Victory()
        {
            if (!IsAlive) return;
            combat.Cancel();
            specials.Clear();
            state = FState.Victory;
            motor.Stop();
            anim.Play(FighterAnimation.Dance, 0.15f);
            if (rig.crown) rig.crown.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ weapons
        public void EquipWeapon(MoveKind kind)
        {
            DropWeapon();
            weapon = kind;
            weaponObj = Accessories.BuildWeapon(kind, rig, body);
        }

        public void DropWeapon()
        {
            weapon = MoveKind.None;
            if (weaponObj != null) Destroy(weaponObj.gameObject);
            weaponObj = null;
        }
    }

    /// <summary>Forwards Animation Events from the model (where the Animator lives) to the fighter.</summary>
    public class AnimEventRelay : MonoBehaviour
    {
        public Fighter fighter;
        public void OnAnimHit()
        {
            if (fighter != null && fighter.active) fighter.combat.OnAnimHit();
        }
    }
}
