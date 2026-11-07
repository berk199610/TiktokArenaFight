using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Movement without physics (doc §10.4): steering toward a goal, separation from neighbours (nobody walks
    /// into anybody, §5.3), knock-back that decays, flights after super hits, bounce off the arena wall (§8.1).
    /// </summary>
    public class FighterMotor
    {
        readonly Fighter f;
        public Vector3 pos;
        public Vector3 vel;
        public Vector3 push;
        public float y, vy;
        public bool airborne;
        public bool ignoreWall;
        public float yaw;

        Vector3 moveDir;
        float moveSpeed;
        bool hasFace;
        Vector3 facePoint;

        static readonly List<Fighter> neighbours = new List<Fighter>(32);

        public FighterMotor(Fighter f) { this.f = f; }

        public float HorizontalSpeed => airborne ? 0f : vel.magnitude;

        public void Place(Vector3 p, float yawDeg)
        {
            pos = new Vector3(p.x, 0, p.z);
            vel = Vector3.zero; push = Vector3.zero;
            y = 0; vy = 0; airborne = false; ignoreWall = false;
            yaw = yawDeg;
            moveDir = Vector3.zero; moveSpeed = 0; hasFace = false;
            f.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        }

        public void Move(Vector3 dir, float speed)
        {
            dir.y = 0;
            moveDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
            moveSpeed = speed;
        }

        public void Stop() { moveDir = Vector3.zero; moveSpeed = 0; }

        public void FaceTo(Vector3 p) { hasFace = true; facePoint = p; }
        public void ClearFace() { hasFace = false; }
        public void SnapYaw(float y) { yaw = y; }

        public void AddPush(Vector3 v)
        {
            if (f.giant) return;
            v.y = 0;
            push += v;
        }

        public void Launch(Vector3 v)
        {
            airborne = true;
            push = new Vector3(v.x, 0, v.z);
            vy = v.y;
            y = Mathf.Max(y, 0.01f);
            vel = Vector3.zero;
        }

        public void Tick(float dt)
        {
            var cfg = GameConfig.Current;
            if (airborne)
            {
                pos += push * dt;
                vy -= cfg.combat.gravity * dt;
                y += vy * dt;
                if (y <= 0f && vy < 0f)
                {
                    y = 0f; vy = 0f; airborne = false;
                    push *= 0.35f;
                    f.OnLanded();
                }
            }
            else
            {
                var desired = moveDir * moveSpeed;
                vel = Vector3.MoveTowards(vel, desired, 16f * dt);

                // separation: characters never overlap, always a little gap (doc §5.3)
                Vector3 sep = Vector3.zero;
                float sr = cfg.ai.separationRadius * f.sizeMul;
                if (f.state != FState.Dead)
                {
                    GameManager.I.Fighters.Query(pos, sr * 1.5f, neighbours, f);
                    for (int i = 0; i < neighbours.Count; i++)
                    {
                        var n = neighbours[i];
                        if (n.state == FState.Dead || n.motor.airborne) continue;
                        var d = pos - n.motor.pos; d.y = 0;
                        float dist = d.magnitude;
                        float r = (sr + cfg.ai.separationRadius * n.sizeMul) * 0.5f;
                        if (dist < r)
                        {
                            if (dist < 0.001f) { d = new Vector3(Random.value - 0.5f, 0, Random.value - 0.5f); dist = 0.001f; }
                            float k = (r - dist) / r;
                            if (n.giant && !f.giant) k *= 2f;
                            sep += d / dist * k;
                        }
                    }
                }
                if (f.giant) sep *= 0.3f;
                pos += (vel + push + sep * cfg.ai.separationStrength) * dt;
                push *= Mathf.Exp(-6f * dt);
                if (push.sqrMagnitude < 0.0004f) push = Vector3.zero;
            }

            // arena wall: bounce back, never fall out (doc §8.1)
            if (!ignoreWall)
            {
                float limit = cfg.arena.radius - 0.5f;
                float r = new Vector2(pos.x, pos.z).magnitude;
                if (r > limit)
                {
                    var n = new Vector3(pos.x, 0, pos.z) / r;
                    pos = new Vector3(n.x * limit, 0, n.z * limit);
                    float radial = Vector3.Dot(push, n);
                    if (radial > 0f) push -= n * radial * (1f + cfg.combat.wallBounce);
                    float rv = Vector3.Dot(vel, n);
                    if (rv > 0f) vel -= n * rv;
                }
            }

            // facing
            if (f.spinSpeed != 0f) yaw += f.spinSpeed * dt;
            else if (!airborne)
            {
                Vector3 look = Vector3.zero;
                if (hasFace) { look = facePoint - pos; look.y = 0; }
                else if (vel.sqrMagnitude > 0.05f) look = vel;
                if (look.sqrMagnitude > 0.0001f)
                {
                    float target = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                    yaw = Mathf.MoveTowardsAngle(yaw, target, cfg.ai.turnSpeedDeg * dt);
                }
            }
            f.transform.SetPositionAndRotation(new Vector3(pos.x, y, pos.z), Quaternion.Euler(0, yaw, 0));
        }

        public float FacingAngleTo(Vector3 p)
        {
            var d = p - pos; d.y = 0;
            if (d.sqrMagnitude < 0.0001f) return 0f;
            float target = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(yaw, target));
        }
    }
}
