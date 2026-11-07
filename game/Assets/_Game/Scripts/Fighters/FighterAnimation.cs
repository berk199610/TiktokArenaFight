using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Thin layer over the Animator (controller built by Arena > Kurulum): cross-fades to named states,
    /// walk/run blend by speed, upper-body hit layer, hit stop (doc §6.2), injured locomotion.
    /// </summary>
    public class FighterAnimation
    {
        static int H(string s) => Animator.StringToHash(s);
        public static readonly int SpeedParam = H("Speed");
        public static readonly int Loco = H("Loco"), LocoHurt = H("LocoHurt");
        public static readonly int Punch1 = H("Punch1"), Punch2 = H("Punch2"), Punch3 = H("Punch3"), Kick = H("Kick");
        public static readonly int Bat = H("Bat"), Shovel = H("Shovel"), SpinKick = H("SpinKick"), Tornado = H("Tornado"), Slam = H("Slam");
        public static readonly int Flex = H("Flex"), HitBig = H("HitBig"), Fall = H("Fall"), Lying = H("Lying"), GetUp = H("GetUp"), Death = H("Death");
        public static readonly int Dizzy = H("Dizzy"), Dance = H("Dance"), Yes = H("Yes"), Wake = H("Wake"), Dodge = H("Dodge"), Air = H("Air"), Guard = H("Guard");
        public static readonly int HitHead = H("HitHead"), HitChest = H("HitChest");
        public static readonly int StaggerL = H("StaggerL"), StaggerR = H("StaggerR"), FallL = H("FallL"), FallR = H("FallR");
        public static readonly int DeathFwd = H("DeathFwd"), DeathSide = H("DeathSide"), DeathKnees = H("DeathKnees"), Cocky = H("Cocky");

        // state -> (clip, state speed) — must match ArenaSetup.CreateAnimator
        static readonly Dictionary<int, (string clip, float speed)> stateClips = new Dictionary<int, (string, float)>
        {
            { Punch1, ("Punch_Jab", 1.15f) }, { Punch2, ("Punch_Cross", 1.1f) }, { Punch3, ("Melee_Hook", 1.1f) }, { Kick, ("Kick", 1f) },
            { Bat, ("Sword_Regular_A", 1.1f) }, { Shovel, ("Sword_Regular_B", 1f) }, { Slam, ("Slam", 1f) }, { Flex, ("Flex", 1f) },
            { HitBig, ("Hit_Knockback", 1.2f) }, { GetUp, ("LayToIdle", 1.3f) }, { Death, ("Death01", 1f) }, { Wake, ("Idle_No_Loop", 1.4f) },
            { Dodge, ("Roll", 1.3f) }, { Yes, ("Yes", 1f) }, { Fall, ("Fall", 1f) },
            { StaggerL, ("StaggerL", 1f) }, { StaggerR, ("StaggerR", 1f) }, { FallL, ("FallL", 1f) }, { FallR, ("FallR", 1f) },
        };
        static Dictionary<string, float> clipLengths;

        public readonly Animator animator;
        readonly Fighter f;
        float baseSpeed = 1f;
        float hitStopUntil;
        bool stopped;
        bool hurt;
        int currentState;

        public FighterAnimation(Fighter f, Animator a)
        {
            this.f = f;
            animator = a;
            if (clipLengths == null && a != null && a.runtimeAnimatorController != null)
            {
                clipLengths = new Dictionary<string, float>();
                foreach (var c in a.runtimeAnimatorController.animationClips)
                    if (c != null) clipLengths[c.name] = c.length;
            }
        }

        /// <summary>Seconds a one-shot state takes at the current playback speed.</summary>
        public float Duration(int state)
        {
            if (!stateClips.TryGetValue(state, out var sc) || clipLengths == null || !clipLengths.TryGetValue(sc.clip, out float len)) return 0.8f;
            return len / Mathf.Max(0.05f, sc.speed * baseSpeed);
        }

        public void Reset(float speed)
        {
            baseSpeed = speed;
            stopped = false;
            hurt = false;
            if (animator == null) return;
            animator.speed = speed;
            animator.Rebind();
            animator.Update(0f);
            currentState = Loco;
        }

        public void SetPlaybackSpeed(float s)
        {
            baseSpeed = s;
            if (!stopped && animator != null) animator.speed = s;
        }

        public void Play(int state, float fade = 0.08f)
        {
            if (animator == null) return;
            currentState = state;
            animator.CrossFadeInFixedTime(state, fade, 0, 0f);
        }

        public void PlayHit(bool head)
        {
            if (animator == null) return;
            animator.CrossFadeInFixedTime(head ? HitHead : HitChest, 0.04f, 1, 0f);
        }

        public void ToLoco(float fade = 0.15f) => Play(hurt ? LocoHurt : Loco, fade);

        public bool InLoco => currentState == Loco || currentState == LocoHurt;

        public void SetHurt(bool h)
        {
            if (h == hurt) return;
            hurt = h;
            if (InLoco && f.state == FState.Idle) ToLoco(0.3f);
        }

        public void HitStop(float sec)
        {
            if (animator == null || sec <= 0f) return;
            hitStopUntil = Time.time + sec;
            stopped = true;
            animator.speed = 0f;
        }

        public void Tick(float moveSpeed, float dt, float now)
        {
            if (animator == null) return;
            if (stopped && now >= hitStopUntil) { stopped = false; animator.speed = baseSpeed; }
            // low HP: slower attacks (doc §3); walking is slowed by the motor
            float want = f.animSpeed * (f.hp < GameConfig.Current.health.lowHp ? GameConfig.Current.health.lowAttackSpeedMul : 1f);
            if (f.intelligence < 0.3f && !f.IsBot) want *= 0.85f;
            if (Mathf.Abs(want - baseSpeed) > 0.01f) SetPlaybackSpeed(want);
            animator.SetFloat(SpeedParam, moveSpeed / Mathf.Max(0.5f, f.speedMul), 0.08f, dt);
        }
    }
}
