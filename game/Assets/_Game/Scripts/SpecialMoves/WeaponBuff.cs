using UnityEngine;

namespace Arena
{
    /// <summary>
    /// 5-29 coins Sopa (bat): range +50 %, damage x2 for 15 s. 30-99 coins Kürek (shovel): wide half-circle swing,
    /// 60 damage to everyone in front, 20 s (doc §4). The basic attacks are replaced while the weapon is held.
    /// </summary>
    public class WeaponBuff : SpecialMove
    {
        readonly MoveKind weaponKind;
        TrailRenderer trail;

        public WeaponBuff(MoveKind k) { weaponKind = k; }

        protected override void Begin(int count, float now)
        {
            duration = weaponKind == MoveKind.Bat ? Cfg.moves.bat.durationSec : Cfg.moves.shovel.durationSec;
            endsAt = now + Mathf.Min(Cfg.moves.stackMaxSec, duration * Mathf.Max(1, count));
            f.EquipWeapon(weaponKind);
            if (f.weaponObj != null)
            {
                // swing trail at the tip ("sopa ve kürekte savurma izi")
                var tip = new GameObject("Tip").transform;
                tip.SetParent(f.weaponObj, false);
                var far = f.weaponObj.Find(weaponKind == MoveKind.Bat ? "Bat" : "Blade");
                tip.position = far != null ? far.position + far.up * (far.lossyScale.y * (weaponKind == MoveKind.Bat ? 1f : 0.5f)) : f.weaponObj.position;
                trail = tip.gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.18f;
                trail.minVertexDistance = 0.05f;
                trail.widthMultiplier = 0.3f;
                trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
                trail.sharedMaterial = Mats.Fx(true, FxTex.Soft, weaponKind == MoveKind.Bat ? new Color(1f, 0.8f, 0.4f, 0.8f) : new Color(0.7f, 0.9f, 1f, 0.8f));
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        public override void Tick(float dt, float now)
        {
            if (f.weapon != weaponKind) finished = true;
            if (trail != null) trail.emitting = f.state == FState.Attacking;
        }

        public override void End()
        {
            if (f.weapon == weaponKind) f.DropWeapon();
        }
    }
}
