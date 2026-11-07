using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arena
{
    /// <summary>
    /// Emoji reactions above the characters (user request): angry when beaten, "HELP!" at low HP, scared when
    /// fleeing, sneaky when hunting a weak one, laughing / cool after a knock-down, dizzy, tired, star eyes on a gift,
    /// love on likes. A bubble pops in with a springy scale, bobs, then shrinks away. Rate-limited per character and
    /// in total so the screen never gets crowded.
    /// </summary>
    public class EmoteSystem : MonoBehaviour
    {
        class Bubble
        {
            public RectTransform root;
            public Image bg, icon;
            public Text label;
            public Fighter f;
            public float start, until;
        }

        RectTransform layer;
        readonly List<Bubble> active = new List<Bubble>();
        readonly Stack<Bubble> pool = new Stack<Bubble>();
        static GameConfig Cfg => GameConfig.Current;

        public void Init()
        {
            var go = new GameObject("EmoteCanvas");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 1f;
            layer = (RectTransform)go.transform;
        }

        Bubble Make()
        {
            if (pool.Count > 0) { var p = pool.Pop(); p.root.gameObject.SetActive(true); return p; }
            var b = new Bubble();
            b.root = Ui.Rect(layer, "Emote");
            b.root.anchorMin = b.root.anchorMax = Vector2.zero;
            b.root.pivot = new Vector2(0.5f, 0f);
            b.root.sizeDelta = new Vector2(84, 84);
            b.bg = Ui.Image(b.root, "Bg", Ui.Circle, new Color(1f, 1f, 1f, 0.92f));
            Ui.Stretch(b.bg.rectTransform);
            var tail = Ui.Image(b.bg.transform, "Tail", Ui.Circle, new Color(1f, 1f, 1f, 0.92f));
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            tail.rectTransform.sizeDelta = new Vector2(18, 18);
            tail.rectTransform.anchoredPosition = new Vector2(0, -6);
            b.icon = Ui.Image(b.root, "Icon", null, Color.white);
            Ui.Stretch(b.icon.rectTransform, 8);
            b.label = Ui.Text(b.root, "Label", "", 30, new Color(1f, 0.25f, 0.25f), TextAnchor.MiddleCenter, Ui.FontBlack);
            b.label.rectTransform.anchorMin = b.label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            b.label.rectTransform.anchoredPosition = new Vector2(0, 16);
            b.label.rectTransform.sizeDelta = new Vector2(160, 40);
            return b;
        }

        /// <summary>Show a reaction. Moods: angry, tired, sneaky, laugh, cool, scared, dizzy, love, starstruck, happy, sos, fire, heart, comment.</summary>
        public void Show(Fighter f, string mood, bool force = false)
        {
            if (f == null || !f.IsAlive || string.IsNullOrEmpty(mood)) return;
            float now = Time.time;
            var ec = Cfg.emotes;
            if (!force && now < f.emoteCooldownUntil) return;
            if (!force && active.Count >= ec.maxActive) return;
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].f == f) Release(i);
            f.emoteCooldownUntil = now + ec.cooldownSec * Random.Range(0.8f, 1.4f);
            var b = Make();
            b.f = f;
            b.start = now;
            b.until = now + ec.showSec;
            b.icon.sprite = Icons.Get(mood);
            b.label.text = mood == "sos" ? "HELP!" : mood == "tired" ? "..." : "";
            b.label.color = mood == "sos" ? new Color(1f, 0.3f, 0.3f) : Color.white;
            b.root.localScale = Vector3.zero;
            active.Add(b);
        }

        void Release(int i)
        {
            var b = active[i];
            b.root.gameObject.SetActive(false);
            b.f = null;
            pool.Push(b);
            active.RemoveAt(i);
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.9f, c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Cam == null) return;
            var cam = gm.Cam.cam;
            float now = Time.time;
            float k = layer.rect.height / Mathf.Max(1f, Screen.height);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var b = active[i];
                if (b.f == null || !b.f.active || b.f.state == FState.Ejected || now >= b.until) { Release(i); continue; }
                var sp = cam.WorldToScreenPoint(b.f.transform.position + Vector3.up * (b.f.HeadHeight + 0.15f));
                if (sp.z <= 0f || LeaderboardPanel.Covers(sp)) { b.root.localScale = Vector3.zero; continue; }
                float age = now - b.start, left = b.until - now;
                float s = age < 0.28f ? EaseOutBack(age / 0.28f) : left < 0.22f ? left / 0.22f : 1f + Mathf.Sin(age * 6f) * 0.04f;
                b.root.localScale = Vector3.one * Mathf.Max(0f, s);
                b.root.anchoredPosition = new Vector2(sp.x * k, sp.y * k + 92f + Mathf.Sin(age * 4f) * 4f);
                b.root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(age * 5f) * 5f);
            }
        }
    }
}
