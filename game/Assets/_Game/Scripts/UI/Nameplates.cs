using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arena
{
    /// <summary>
    /// Name + small health bar above every head (doc §7.4) and the status signs: red pulsing "+" at low HP
    /// (shaking at very low HP, §3), "zzz" when dull and "!" when woken (§5.5), floating "+1" heals.
    /// Gifters' names are always visible and gold; others show for 3 s after interacting or being hit;
    /// with 20 or fewer characters everyone's name is shown. Pooled screen-space elements.
    /// </summary>
    public class Nameplates : MonoBehaviour
    {
        class Plate
        {
            public RectTransform root;
            public Text name;
            public Image barBg, bar;
            public Text status;
            public Image plus;
            public Image level;
            public Text levelText;
        }

        class Floater { public Text t; public Vector3 world; public float start; }
        class WordPop { public Text t; public Vector3 world; public float start, scale, rot; }
        readonly List<WordPop> words = new List<WordPop>();
        readonly Stack<Text> wordPool = new Stack<Text>();

        RectTransform layer;
        Canvas canvas;
        readonly Dictionary<Fighter, Plate> plates = new Dictionary<Fighter, Plate>();
        readonly Stack<Plate> pool = new Stack<Plate>();
        readonly List<Fighter> remove = new List<Fighter>();
        readonly List<Floater> floaters = new List<Floater>();
        readonly Stack<Text> floaterPool = new Stack<Text>();
        static GameConfig Cfg => GameConfig.Current;

        public void Init(Canvas hudCanvas)
        {
            var go = new GameObject("NameCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 1f;
            layer = (RectTransform)go.transform;
        }

        Plate Make()
        {
            if (pool.Count > 0) { var p = pool.Pop(); p.root.gameObject.SetActive(true); return p; }
            var pl = new Plate();
            pl.root = Ui.Rect(layer, "Plate");
            pl.root.sizeDelta = new Vector2(220, 80);
            pl.root.anchorMin = pl.root.anchorMax = Vector2.zero;
            pl.root.pivot = new Vector2(0.5f, 0f);
            pl.name = Ui.Text(pl.root, "Name", "", 30, Color.white, TextAnchor.LowerCenter, Ui.FontBold);
            pl.name.rectTransform.anchorMin = pl.name.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            pl.name.rectTransform.pivot = new Vector2(0.5f, 0f);
            pl.name.rectTransform.anchoredPosition = new Vector2(0, 16);
            pl.name.rectTransform.sizeDelta = new Vector2(220, 36);
            pl.barBg = Ui.Image(pl.root, "BarBg", Ui.Rounded, new Color(0, 0, 0, 0.65f));
            var bb = pl.barBg.rectTransform;
            bb.anchorMin = bb.anchorMax = new Vector2(0.5f, 0f);
            bb.pivot = new Vector2(0.5f, 0f);
            bb.anchoredPosition = new Vector2(0, 2);
            bb.sizeDelta = new Vector2(86, 13);
            pl.bar = Ui.Image(pl.barBg.transform, "Bar", null, Color.green);
            var br = pl.bar.rectTransform;
            br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 1);
            br.pivot = new Vector2(0, 0.5f);
            br.offsetMin = new Vector2(2, 2); br.offsetMax = new Vector2(-2, -2);
            pl.status = Ui.Text(pl.root, "Status", "", 40, Color.white, TextAnchor.LowerCenter, Ui.FontBlack);
            pl.status.rectTransform.anchorMin = pl.status.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            pl.status.rectTransform.pivot = new Vector2(0.5f, 0f);
            pl.status.rectTransform.anchoredPosition = new Vector2(0, 50);
            pl.status.rectTransform.sizeDelta = new Vector2(120, 50);
            pl.plus = Ui.Image(pl.root, "Plus", Ui.Plus, new Color(1f, 0.15f, 0.15f));
            pl.plus.rectTransform.anchorMin = pl.plus.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            pl.plus.rectTransform.pivot = new Vector2(0.5f, 0f);
            pl.plus.rectTransform.anchoredPosition = new Vector2(0, 52);
            pl.plus.rectTransform.sizeDelta = new Vector2(38, 38);
            pl.level = Ui.Image(pl.root, "Level", Ui.Circle, new Color(0.25f, 0.55f, 1f));
            pl.level.rectTransform.anchorMin = pl.level.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            pl.level.rectTransform.sizeDelta = new Vector2(34, 34);
            pl.levelText = Ui.Text(pl.level.transform, "N", "", 24, Color.white, TextAnchor.MiddleCenter, Ui.FontBlack);
            Ui.Stretch(pl.levelText.rectTransform);
            return pl;
        }

        public void Float(Fighter f, string text, Color c)
        {
            if (f == null || !f.IsAlive) return;
            Text t;
            if (floaterPool.Count > 0) { t = floaterPool.Pop(); t.gameObject.SetActive(true); }
            else
            {
                t = Ui.Text(layer, "Float", "", 40, c, TextAnchor.MiddleCenter, Ui.FontBlack);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = Vector2.zero;
                t.rectTransform.sizeDelta = new Vector2(160, 50);
            }
            t.text = text;
            t.color = c;
            var jitter = new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f));
            floaters.Add(new Floater { t = t, world = f.transform.position + Vector3.up * (f.HeadHeight + 0.2f) + jitter, start = Time.time });
        }

        /// <summary>Comic hit word ("POW!", "BAM!", "K.O.!") that pops out, tilts and fades (max 6 at once).</summary>
        public void Word(Vector3 world, string text, Color c, float scale)
        {
            if (words.Count >= 4) return;
            Text t;
            if (wordPool.Count > 0) { t = wordPool.Pop(); t.gameObject.SetActive(true); }
            else
            {
                t = Ui.Text(layer, "Word", "", 64, c, TextAnchor.MiddleCenter, Ui.FontBlack);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = Vector2.zero;
                t.rectTransform.sizeDelta = new Vector2(320, 90);
                var o = t.GetComponent<Outline>();
                if (o != null) { o.effectDistance = new Vector2(4, -4); o.effectColor = new Color(0.1f, 0.05f, 0.1f, 1f); }
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectDistance = new Vector2(5, -6);
                sh.effectColor = new Color(0, 0, 0, 0.6f);
            }
            t.text = text;
            t.color = c;
            words.Add(new WordPop { t = t, world = world + Random.insideUnitSphere * 0.3f, start = Time.time, scale = scale, rot = Random.Range(-14f, 14f) });
        }

        Vector2 ToCanvas(Vector3 screen)
        {
            float k = layer.rect.height / Mathf.Max(1f, Screen.height);
            return new Vector2(screen.x * k, screen.y * k);
        }

        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Cam == null) return;
            var cam = gm.Cam.cam;
            float now = Time.time;
            int alive = gm.Fighters.AliveCount;
            bool allNames = alive <= Cfg.names.alwaysShowAtOrBelow;

            remove.Clear();
            foreach (var kv in plates) if (!kv.Key.active || kv.Key.state == FState.Dead || kv.Key.state == FState.Ejected) remove.Add(kv.Key);
            foreach (var f in remove) { var p = plates[f]; p.root.gameObject.SetActive(false); pool.Push(p); plates.Remove(f); }

            foreach (var f in gm.Fighters.Active)
            {
                if (!f.IsAlive) continue;
                if (!plates.TryGetValue(f, out var p)) { p = Make(); plates[f] = p; }
                var world = f.transform.position + Vector3.up * (f.HeadHeight + 0.12f);
                var sp = cam.WorldToScreenPoint(world);
                if (sp.z <= 0f || LeaderboardPanel.Covers(sp)) { p.root.gameObject.SetActive(false); continue; }
                if (!p.root.gameObject.activeSelf) p.root.gameObject.SetActive(true);
                p.root.anchoredPosition = ToCanvas(sp);

                bool gifter = f.IsGifter;
                bool showName = gifter || allNames || now < f.nameVisibleUntil || f.state == FState.Victory;
                p.name.enabled = showName;
                if (showName)
                {
                    string n = Ui.Short(f.Name, 16);
                    if (p.name.text != n) p.name.text = n;
                    p.name.color = gifter ? new Color(1f, 0.82f, 0.2f) : f.IsBot ? new Color(0.85f, 0.85f, 0.9f) : Color.white;
                    p.name.fontSize = gifter ? 34 : 30;
                }
                // level badge: blue = comments / likes (max 3), gold = gifts (4-7), purple = legend (8)
                int lvl = Gear.DisplayLevel(f.viewer);
                bool showLvl = showName && lvl > 0;
                p.level.enabled = showLvl;
                p.levelText.enabled = showLvl;
                if (showLvl)
                {
                    string ls = lvl.ToString();
                    if (p.levelText.text != ls) p.levelText.text = ls;
                    p.level.color = lvl >= 8 ? new Color(0.7f, 0.35f, 1f) : lvl >= 4 ? new Color(1f, 0.72f, 0.12f) : new Color(0.22f, 0.55f, 1f);
                    p.levelText.color = lvl >= 4 && lvl < 8 ? new Color(0.25f, 0.13f, 0.02f) : Color.white;
                    float nameW = Mathf.Min(p.name.preferredWidth, 220f);
                    p.level.rectTransform.anchoredPosition = new Vector2(-nameW * 0.5f - 22f, 34f);
                    p.level.rectTransform.localScale = Vector3.one * (lvl >= 4 ? 1.12f : 1f);
                }
                float hp01 = Mathf.Clamp01(f.Hp01);
                p.bar.rectTransform.anchorMax = new Vector2(hp01, 1);
                p.bar.color = hp01 > 0.5f ? Color.Lerp(new Color(1f, 0.85f, 0.1f), new Color(0.2f, 0.9f, 0.25f), (hp01 - 0.5f) * 2f)
                                          : Color.Lerp(new Color(0.95f, 0.15f, 0.1f), new Color(1f, 0.85f, 0.1f), hp01 * 2f);
                if (now < f.hurtFlashUntil) p.bar.color = Color.white;

                // status sign: "!" just woke > "+" low HP > "zzz" dull
                bool woke = now - f.wokeAt < 1.2f;
                bool low = f.hp < Cfg.health.lowHp;
                bool dull = f.state == FState.Dizzy;
                p.plus.enabled = !woke && low;
                if (p.plus.enabled)
                {
                    float pulse = 1f + Mathf.Sin(now * 9f) * 0.18f;
                    p.plus.rectTransform.localScale = Vector3.one * pulse;
                    float shake = f.hp < Cfg.health.veryLowHp ? Mathf.Sin(now * 55f) * 5f : 0f;
                    p.plus.rectTransform.anchoredPosition = new Vector2(shake, showName ? 54 : 22);
                }
                string st = woke ? "!" : (!low && dull) ? "zzz" : "";
                if (p.status.text != st) p.status.text = st;
                p.status.color = woke ? new Color(1f, 0.9f, 0.2f) : new Color(0.7f, 0.85f, 1f);
                p.status.rectTransform.anchoredPosition = new Vector2(dull ? Mathf.Sin(now * 2f) * 6f : 0, (showName ? 50 : 18) + (woke ? Mathf.Abs(Mathf.Sin(now * 10f)) * 8f : 0));
            }

            for (int i = words.Count - 1; i >= 0; i--)
            {
                var w = words[i];
                float age = now - w.start;
                if (age >= 0.85f) { w.t.gameObject.SetActive(false); wordPool.Push(w.t); words.RemoveAt(i); continue; }
                var sp = cam.WorldToScreenPoint(w.world + Vector3.up * age * 0.6f);
                w.t.rectTransform.anchoredPosition = ToCanvas(sp);
                float pop = age < 0.12f ? Mathf.Lerp(0.2f, 1.35f, age / 0.12f) : age < 0.22f ? Mathf.Lerp(1.35f, 1f, (age - 0.12f) / 0.1f) : 1f;
                w.t.rectTransform.localScale = Vector3.one * pop * w.scale * 0.75f;
                w.t.rectTransform.localRotation = Quaternion.Euler(0, 0, w.rot);
                w.t.color = Ui.WithAlpha(w.t.color, age > 0.6f ? 1f - (age - 0.6f) / 0.25f : 1f);
            }

            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var fl = floaters[i];
                float k = (now - fl.start) / 0.9f;
                if (k >= 1f) { fl.t.gameObject.SetActive(false); floaterPool.Push(fl.t); floaters.RemoveAt(i); continue; }
                var sp = cam.WorldToScreenPoint(fl.world + Vector3.up * k * 0.9f);
                fl.t.rectTransform.anchoredPosition = ToCanvas(sp);
                fl.t.color = Ui.WithAlpha(fl.t.color, 1f - k * k);
            }
        }
    }
}
