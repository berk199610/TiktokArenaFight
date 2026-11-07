using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Calm 2.5D camera (doc §8.2, user request): looks at the arena from a corner, ~45° down; follows the average
    /// ground position (x/z) of all fighters, smoothly (reaches in ~1.5 s), never more than 5 m from the centre.
    /// Pulls back a little when the fight spreads out, closer when it bunches up. Big gifts nudge it gently toward
    /// the gifter. No cuts, no shake.
    /// </summary>
    public class ArenaCamera : MonoBehaviour
    {
        public Camera cam;
        Vector3 focus, focusVel;
        float dist, distVel;
        Fighter nudge;
        float nudgeUntil;
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
            dist = (c.minDistance + c.maxDistance) * 0.5f;
            focus = Vector3.zero;
            Apply();
        }

        public void NudgeToward(Fighter f, bool big)
        {
            if (!big || f == null) return;
            nudge = f;
            nudgeUntil = Time.time + 3.5f;
        }

        void LateUpdate()
        {
            if (Frozen) { Apply(); return; }
            var c = GameConfig.Current.camera;
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
            // along the viewing direction the camera moves less, so the scoreboard behind the arena stays in its screen band
            var flatFwd = Quaternion.Euler(0, c.yawDeg, 0) * Vector3.forward;
            float along = Vector3.Dot(target, flatFwd);
            float clamped = Mathf.Clamp(along, -c.maxOffsetForward, c.maxOffsetForward);
            target += flatFwd * (clamped - along);

            // spread -> distance
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
            float wantDist = Mathf.Lerp(c.minDistance, c.maxDistance, Mathf.Clamp01(spread / Mathf.Max(0.1f, c.spreadForMax)));

            float smooth = Mathf.Max(0.05f, c.followSec * 0.35f);
            focus = Vector3.SmoothDamp(focus, target, ref focusVel, smooth, Mathf.Infinity, Time.deltaTime);
            dist = Mathf.SmoothDamp(dist, wantDist, ref distVel, smooth * 1.5f, Mathf.Infinity, Time.deltaTime);
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
            return v.z > 0f && v.x > 0.06f && v.x < 0.94f && v.y > 0.08f && v.y < 0.86f;
        }
    }
}
