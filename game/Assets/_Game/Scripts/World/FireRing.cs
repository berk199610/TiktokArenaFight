using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Final battle (doc §9): a ring of fire closes in from the edge (12 m -> 4 m in 60 s); outside it everyone
    /// loses 30 HP per second. Guarantees the round ends.
    /// </summary>
    public class FireRing
    {
        readonly Transform wall;
        readonly Material wallMat;
        public float Radius { get; private set; }
        public bool Active { get; private set; }
        float startedAt;

        public FireRing(Transform parent)
        {
            wallMat = Mats.FxNew(true, FxTex.Flame, new Color(1f, 0.45f, 0.1f, 1f) * 1.6f);
            wallMat.SetVector("_Scroll", new Vector4(0.15f, -0.9f, 0, 0));
            wallMat.SetTextureScale("_MainTex", new Vector2(14f, 1f));
            wall = MeshKit.Part("FireWall", MeshKit.Tube(1f, 1f, 96), wallMat, parent, Vector3.zero, Vector3.zero, Vector3.one).transform;
            wall.gameObject.SetActive(false);
            Radius = GameConfig.Current.round.ringStartRadius;
        }

        public void Begin(float now)
        {
            Active = true;
            startedAt = now;
            Radius = GameConfig.Current.round.ringStartRadius;
            wall.gameObject.SetActive(true);
        }

        public void Stop()
        {
            Active = false;
            Radius = GameConfig.Current.round.ringStartRadius;
            wall.gameObject.SetActive(false);
        }

        public void Tick(float dt, float now)
        {
            if (!Active) return;
            var rc = GameConfig.Current.round;
            float k = Mathf.Clamp01((now - startedAt) / rc.ringShrinkSec);
            Radius = Mathf.Lerp(rc.ringStartRadius, rc.ringEndRadius, k);
            // sudden death after the ring has stopped for a while (two survivors hiding in the middle)
            float over = now - startedAt - rc.ringShrinkSec - 45f;
            if (over > 0f) Radius = Mathf.Max(0.5f, rc.ringEndRadius - over * 0.1f);
            wall.localScale = new Vector3(Radius, 1.2f + Mathf.Sin(now * 9f) * 0.08f, Radius);
            Fx.I.FireRing(Radius, Mathf.CeilToInt(Radius * 1.2f));

            foreach (var f in GameManager.I.Fighters.Active)
            {
                if (!f.IsAlive) continue;
                float r = new Vector2(f.Pos.x, f.Pos.z).magnitude;
                if (r > Radius)
                {
                    float before = f.hp;
                    f.hp -= rc.ringDps * dt;
                    if (Random.value < dt * 4f) Fx.I.FireBurst(f.Pos);
                    if (f.hp <= 0f && before > 0f) { f.hp = 0f; f.Die(f.lastHitBy != null && now - f.lastHitTime < 4f ? f.lastHitBy : null, MoveKind.None); }
                }
            }
        }
    }
}
