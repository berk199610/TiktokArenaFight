using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Calm 2.5D camera (doc §8.2, user request): looks at the arena from a corner, ~42° down; follows the average
    /// ground position (x/z) of all fighters very smoothly (speed-limited, no sudden acceleration), never more than
    /// 5 m from the centre. Zooms out a little when the fight spreads, in when it bunches up, and slowly "breathes"
    /// in and out on its own. Big gifts nudge it gently toward the gifter. No cuts, no shake.
    /// </summary>
    public class ArenaCamera : MonoBehaviour
    {
        public Camera cam;
        Vector3 focus, focusVel;
        float dist, distVel, wantDist;
        Fighter nudge;
        float nudgeUntil;
        float giftZoomUntil;
        public Vector3 FocusPoint => focus;
        public bool Frozen;

        public void Init()
        {
            cam = gameObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.06f);
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 140f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            gameObject.tag = "MainCamera";
            var data = gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            gameObject.AddComponent<AudioListener>();
            var c = GameConfig.Current.camera;
            dist = wantDist = (c.minDistance + c.maxDistance) * 0.5f;
            focus = Vector3.zero;
            Apply();
        }

        public void NudgeToward(Fighter f, bool big)
        {
            if (!big || f == null) return;
            nudge = f;
            nudgeUntil = Time.time + 3.5f;
        }

        public void OnGift(Fighter f, int coins)
        {
            var c = GameConfig.Current.camera;
            if (coins >= c.giftNudgeMinCoins) NudgeToward(f, true);
            if (coins >= c.giftZoomMinCoins) giftZoomUntil = Time.time + c.giftZoomSec;
        }

        void LateUpdate()
        {
            if (Frozen) { Apply(); return; }
            var c = GameConfig.Current.camera;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            var list = GameManager.I != null ? GameManager.I.Fighters.Active : null;
            Vector3 sum = Vector3.zero;
            float w = 0f;
            if (list != null)
            {
                foreach (var f in list)
                {
                    if (!f.IsAlive || f.state == FState.Entering) continue;
                    float k = 1f;
                    if (f == nudge && Time.time < nudgeUntil) k = c.giftNudgeWeight;
                    sum += new Vector3(f.Pos.x, 0, f.Pos.z) * k;
                    w += k;
                }
            }
            Vector3 target = w > 0 ? sum / w : Vector3.zero;
            if (target.magnitude > c.maxOffsetFromCenter) target = target.normalized * c.maxOffsetFromCenter;
            var flatFwd = Quaternion.Euler(0, c.yawDeg, 0) * Vector3.forward;
            float along = Vector3.Dot(target, flatFwd);
            float clamped = Mathf.Clamp(along, -c.maxOffsetForward, c.maxOffsetForward);
            target += flatFwd * (clamped - along);

            // spread of the fight -> distance (+ slow breathing in and out)
            float spread = 0f;
            if (list != null && w > 0)
            {
                float s2 = 0f; int n = 0;
                foreach (var f in list)
                {
                    if (!f.IsAlive || f.state == FState.Entering) continue;
                    s2 += (new Vector3(f.Pos.x, 0, f.Pos.z) - target).sqrMagnitude;
                    n++;
                }
                spread = n > 0 ? Mathf.Sqrt(s2 / n) : 0f;
            }
            float breath = Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(4f, c.breathPeriodSec)) * c.breathAmp;
            float near = Mathf.Min(c.maxDistance, c.minDistance + c.restingDistanceOffset);
            float giftZoom = Mathf.Clamp01((giftZoomUntil - Time.time) / Mathf.Max(0.1f, c.giftZoomSec)) * c.giftZoomAmount;
            float goal = Mathf.Lerp(near, c.maxDistance, Mathf.Clamp01(spread / Mathf.Max(0.1f, c.spreadForMax))) + breath - giftZoom;
            goal = Mathf.Clamp(goal, c.minDistance, c.maxDistance + c.breathAmp);
            // the goal itself moves slowly, so the zoom never jumps
            wantDist = Mathf.MoveTowards(wantDist, goal, dt * c.maxZoomSpeed);

            float smooth = Mathf.Max(0.05f, c.followSec * 0.45f);
            focus = Vector3.SmoothDamp(focus, target, ref focusVel, smooth, c.maxFocusSpeed, dt);
            dist = Mathf.SmoothDamp(dist, wantDist, ref distVel, Mathf.Max(0.3f, c.zoomSec * 0.5f), c.maxZoomSpeed, dt);
            Apply();
        }

        void Apply()
        {
            var c = GameConfig.Current.camera;
            var rot = Quaternion.Euler(c.pitchDeg, c.yawDeg, 0);
            var fwd = rot * Vector3.forward;
            var flat = new Vector3(fwd.x, 0, fwd.z).normalized;
            var look = focus + flat * c.lookAhead + Vector3.up * 0.8f;
            transform.SetPositionAndRotation(look - fwd * dist, rot);
            cam.fieldOfView = c.fov;
        }

        /// <summary>Is the point on screen (with a margin)? Used by the brains' camera rule (doc §5.3).</summary>
        public bool InView(Vector3 p)
        {
            var v = cam.WorldToViewportPoint(p + Vector3.up);
            return v.z > 0f && v.x > 0.06f && v.x < 0.94f && v.y > 0.08f && v.y < 0.6f;
        }
    }
}
