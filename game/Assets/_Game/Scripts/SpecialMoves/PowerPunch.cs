using UnityEngine;

namespace Arena
{
    /// <summary>1-4 coins: the next punch does 3x damage (60) and throws the opponent back (doc §4).</summary>
    public class PowerPunch : SpecialMove
    {
        Transform glowL, glowR;
        public override bool Replaceable => true;

        protected override void Begin(int count, float now)
        {
            f.powerPunchCharges = Mathf.Min(Cfg.moves.powerPunch.maxCharges, f.powerPunchCharges + Mathf.Max(1, Mathf.RoundToInt(count * power)));
            endsAt = now + Cfg.moves.powerPunch.timeoutSec;
            duration = 0f;
            glowR = Glow(f.rig.handR);
            glowL = Glow(f.rig.handL);
        }

        Transform Glow(Transform hand)
        {
            if (hand == null) return null;
            var go = MeshKit.Part("FistGlow", MeshKit.Sphere, Mats.Fx(true, FxTex.Soft, new Color(1f, 0.45f, 0.1f, 1f) * 2f), hand, Vector3.zero, Vector3.zero, Vector3.one * 0.3f);
            return go.transform;
        }

        public override void Extend(int count, float pow, float now)
        {
            f.powerPunchCharges = Mathf.Min(Cfg.moves.powerPunch.maxCharges, f.powerPunchCharges + count);
            endsAt = Mathf.Max(endsAt, now + Cfg.moves.powerPunch.timeoutSec);
        }

        public override void Tick(float dt, float now)
        {
            if (f.powerPunchCharges <= 0) finished = true;
            float s = 0.28f + Mathf.Sin(now * 14f) * 0.05f;
            if (glowR) glowR.localScale = Vector3.one * s / Mathf.Max(0.01f, glowR.parent.lossyScale.x);
            if (glowL) glowL.localScale = Vector3.one * s / Mathf.Max(0.01f, glowL.parent.lossyScale.x);
        }

        public override void End()
        {
            f.powerPunchCharges = 0;
            if (glowR) Object.Destroy(glowR.gameObject);
            if (glowL) Object.Destroy(glowL.gameObject);
        }
    }
}
