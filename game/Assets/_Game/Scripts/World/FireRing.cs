using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Final battle (doc §9): a ring of fire closes in from the wall (11.6 m -> 6.5 m in 75 s, user request: not too
    /// small); outside it everyone loses 30 HP per second. Layered flame wall + glowing ground line + flames and
    /// rising embers. After a long while it slowly closes a bit more so the round always ends.
    /// </summary>
    public class FireRing
    {
        readonly Transform root, wallOuter, wallInner, ground;
        readonly Material outerMat, innerMat, groundMat;
        public float Radius { get; private set; }
        public bool Active { get; private set; }
        float startedAt, emitAcc;

        public FireRing(Transform parent)
        {
            root = new GameObject("FireRing").transform;
            root.SetParent(parent, false);
            outerMat = Mats.FxNew(true, FxTex.Flame, new Color(1f, 0.4f, 0.08f, 1f) * 1.7f);
            outerMat.SetVector("_Scroll", new Vector4(0.12f, -1.1f, 0, 0));
            outerMat.SetTextureScale("_MainTex", new Vector2(18f, 1f));
            innerMat = Mats.FxNew(true, FxTex.Flame, new Color(1f, 0.8f, 0.3f, 1f) * 1.4f);
            innerMat.SetVector("_Scroll", new Vector4(-0.2f, -1.6f, 0, 0));
            innerMat.SetTextureScale("_MainTex", new Vector2(26f, 1f));
            groundMat = Mats.FxNew(true, FxTex.Soft, new Color(1f, 0.45f, 0.1f, 0.9f));
            wallOuter = MeshKit.Part("FlameOuter", MeshKit.Tube(1f, 1.02f, 128), outerMat, root, Vector3.zero, Vector3.zero, Vector3.one).transform;
            wallInner = MeshKit.Part("FlameInner", MeshKit.Tube(0.985f, 0.99f, 128), innerMat, root, Vector3.zero, new Vector3(0, 7, 0), Vector3.one).transform;
            ground = MeshKit.Part("GroundGlow", MeshKit.FlatRing(0.93f, 1.05f, 128), groundMat, root, new Vector3(0, 0.12f, 0), Vector3.zero, Vector3.one).transform;
            root.gameObject.SetActive(false);
            Radius = GameConfig.Current.round.ringStartRadius;
        }

        public void Begin(float now)
        {
            Active = true;
            startedAt = now;
            Radius = GameConfig.Current.round.ringStartRadius;
            root.gameObject.SetActive(true);
            GameManager.I.Hud?.FeedIcon("fire", "FINAL FIGHT!", new Color(1f, 0.55f, 0.2f));
        }

        public void Stop()
        {
            Active = false;
            Radius = GameConfig.Current.round.ringStartRadius;
            root.gameObject.SetActive(false);
        }

        public void Tick(float dt, float now)
        {
            if (!Active) return;
            var rc = GameConfig.Current.round;
            float k = Mathf.Clamp01((now - startedAt) / rc.ringShrinkSec);
            // ease in-out so the ring starts and stops moving gently
            float e = k * k * (3f - 2f * k);
            Radius = Mathf.Lerp(rc.ringStartRadius, rc.ringEndRadius, e);
            // a long stalemate: close slowly a little more
            float over = now - startedAt - rc.ringShrinkSec - 40f;
            if (over > 0f) Radius = Mathf.Max(3f, rc.ringEndRadius - over * 0.06f);

            float flick = Mathf.Sin(now * 9f) * 0.06f + Mathf.Sin(now * 23f) * 0.04f;
            wallOuter.localScale = new Vector3(Radius, 1.6f + flick, Radius);
            wallInner.localScale = new Vector3(Radius, 1.1f - flick, Radius);
            ground.localScale = new Vector3(Radius, 1f, Radius);
            groundMat.SetColor("_Color", new Color(1f, 0.45f, 0.1f, 0.75f + flick));

            // flames and embers along the ring (amount scales with the circumference)
            emitAcc += dt * Radius * 40f;
            int n = (int)emitAcc;
            emitAcc -= n;
            if (n > 0) Fx.I.FireRing(Radius, n);
            if (Random.value < dt * Radius * 2f)
            {
                float a = Random.value * Mathf.PI * 2f;
                Fx.I.Ember(new Vector3(Mathf.Cos(a) * Radius, 0.6f, Mathf.Sin(a) * Radius));
            }

            foreach (var f in GameManager.I.Fighters.Active)
            {
                if (!f.IsAlive) continue;
                float r = new Vector2(f.Pos.x, f.Pos.z).magnitude;
                if (r > Radius)
                {
                    float before = f.hp;
                    f.hp -= rc.ringDps * dt;
                    if (Random.value < dt * 4f) Fx.I.FireBurst(f.Pos);
                    if (Random.value < dt * 0.5f) GameManager.I.Emotes?.Show(f, "fire");
                    if (f.hp <= 0f && before > 0f) { f.hp = 0f; f.Die(f.lastHitBy != null && now - f.lastHitTime < 4f ? f.lastHitBy : null, MoveKind.None); }
                }
            }
        }
    }
}
