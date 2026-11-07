using UnityEngine;

namespace Arena
{
    /// <summary>1.000-4.999 coins Dev Mod: grows 1.4x, double damage, cannot be pushed, 20 s (doc §4). Colour glow.</summary>
    public class GiantMode : SpecialMove
    {
        Renderer glow;
        Material glowMat;
        float nextSparkle;

        protected override void Begin(int count, float now)
        {
            var c = Cfg.moves.giant;
            duration = c.durationSec;
            endsAt = now + Mathf.Min(Cfg.moves.stackMaxSec, duration * Mathf.Max(1, count) * power);
            f.giant = true;
            f.sizeTarget = c.scale + 0.15f * (power - 1f);
            glow = Fx.AttachedGlow(f.transform, new Color(1f, 0.4f, 0.9f, 0.9f), 1.4f);
            glowMat = glow.sharedMaterial;
            Fx.I.GroundRing(f.Pos, 0.5f, 2.5f, 0.6f, new Color(1f, 0.5f, 1f, 1f));
        }

        public override void Tick(float dt, float now)
        {
            if (now >= nextSparkle)
            {
                nextSparkle = now + 0.12f;
                Fx.I.Sparkle(f.Pos, Color.HSVToRGB(Mathf.Repeat(now * 0.5f, 1f), 0.6f, 1f), 2, 0.7f, 2.4f);
            }
            if (glowMat != null)
            {
                var col = Color.HSVToRGB(Mathf.Repeat(now * 0.3f, 1f), 0.7f, 1f);
                col.a = 0.6f + Mathf.Sin(now * 6f) * 0.2f;
                glowMat.SetColor("_Color", col);
            }
        }

        public override void End()
        {
            f.giant = false;
            f.sizeTarget = 1f;
            if (glow != null) Object.Destroy(glow.gameObject);
            if (glowMat != null) Object.Destroy(glowMat);
        }
    }
}
