using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Arena
{
    /// <summary>
    /// The scoreboard stands IN the arena, behind the fight and facing the camera (user request): big enough to read
    /// on a phone but not huge. Top players by points with knock-out counts. Live: rows slide when the order
    /// changes, flash and pop "+10" on a knock-out, #1 wears a crown, the round's top gifter glows gold.
    /// </summary>
    public class LeaderboardPanel : MonoBehaviour
    {
        class Row
        {
            public RectTransform rt;
            public Image bg;
            public Text rank, name, kills, points, pop;
            public string userId;
            public float y, targetY;
            public float flashUntil, popUntil;
            public int lastPoints = -1;
        }

        readonly List<Row> rows = new List<Row>();
        RectTransform panel;
        bool dirty = true;
        float nextRefresh;
        Image crown;
        const float W = 1000f, RowH = 72f, Top = 150f;
        static GameConfig Cfg => GameConfig.Current;

        public static LeaderboardPanel Create(Transform anchor)
        {
            var go = new GameObject("Scoreboard");
            go.transform.SetParent(anchor, false);
            var lp = go.AddComponent<LeaderboardPanel>();
            lp.Build();
            return lp;
        }

        public void MarkDirty() => dirty = true;

        public void OnKill(Viewer killer, bool boss)
        {
            dirty = true;
            foreach (var r in rows)
                if (r.userId == killer.userId)
                {
                    r.flashUntil = Time.time + 0.8f;
                    r.popUntil = Time.time + 1.2f;
                    r.pop.text = "+" + (Cfg.points.kill + (boss ? Cfg.points.bossKill : 0));
                }
        }

        void Build()
        {
            var bc = Cfg.board;
            int n = Mathf.Clamp(bc.rows, 3, 10);
            float H = Top + n * RowH + 30f;

            // 3D frame: two wooden posts and a dark board behind the canvas so it reads as an object in the arena
            float s = 0.006f * bc.scale;
            var wood = Mats.Lit(new Color(0.35f, 0.22f, 0.12f));
            var gold = Mats.Metal(new Color(1f, 0.78f, 0.3f));
            float wM = W * s, hM = H * s;
            transform.localPosition = new Vector3(0, hM * 0.5f, 0); // anchor = bottom edge of the board
            MeshKit.Part("Back", MeshKit.Cube, Mats.Lit(new Color(0.09f, 0.07f, 0.1f)), transform, new Vector3(0, 0, 0.06f), Vector3.zero, new Vector3(wM + 0.3f, hM + 0.3f, 0.1f));
            MeshKit.Part("FrameTop", MeshKit.Cube, gold, transform, new Vector3(0, hM * 0.5f + 0.15f, 0.02f), Vector3.zero, new Vector3(wM + 0.45f, 0.14f, 0.16f));
            MeshKit.Part("FrameBottom", MeshKit.Cube, gold, transform, new Vector3(0, -hM * 0.5f - 0.15f, 0.02f), Vector3.zero, new Vector3(wM + 0.45f, 0.14f, 0.16f));
            float ground = Cfg.board.height + hM * 0.5f;   // distance from board centre down to the floor
            float postLen = ground + hM * 0.5f + 0.3f;
            MeshKit.Part("PostL", MeshKit.Cube, wood, transform, new Vector3(-wM * 0.5f - 0.1f, (hM * 0.5f + 0.3f - ground) * 0.5f, 0.1f), Vector3.zero, new Vector3(0.3f, postLen, 0.3f));
            MeshKit.Part("PostR", MeshKit.Cube, wood, transform, new Vector3(wM * 0.5f + 0.1f, (hM * 0.5f + 0.3f - ground) * 0.5f, 0.1f), Vector3.zero, new Vector3(0.3f, postLen, 0.3f));
            var lamp = new GameObject("BoardLight").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = new Vector3(0, hM * 0.5f + 0.8f, -1.6f);
            lamp.type = LightType.Point; lamp.range = 7f; lamp.intensity = 2f; lamp.color = new Color(1f, 0.85f, 0.6f);

            var cgo = new GameObject("BoardCanvas");
            cgo.transform.SetParent(transform, false);
            // the anchor looks away from the camera = the canvas front faces the camera
            cgo.transform.localPosition = new Vector3(0, 0, -0.01f);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            panel = (RectTransform)cgo.transform;
            panel.sizeDelta = new Vector2(W, H);
            panel.localScale = Vector3.one * s;

            var bg = Ui.Image(panel, "Bg", Ui.Rounded, new Color(0.12f, 0.08f, 0.16f, 0.96f));
            Ui.Stretch(bg.rectTransform);
            var title = Ui.Text(panel, "Title", "SKOR TABLOSU", 74, new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleCenter, Ui.FontBlack);
            Ui.PlaceTop(title.rectTransform, 0, 10, W, 84);
            var hName = Ui.Text(panel, "HName", "OYUNCU", 30, new Color(0.75f, 0.7f, 0.85f), TextAnchor.MiddleLeft, Ui.FontBold, false);
            Ui.PlaceTop(hName.rectTransform, -150, 98, 400, 40);
            var hK = Ui.Text(panel, "HKills", "DEVİRME", 30, new Color(0.75f, 0.7f, 0.85f), TextAnchor.MiddleCenter, Ui.FontBold, false);
            Ui.PlaceTop(hK.rectTransform, 220, 98, 160, 40);
            var hP = Ui.Text(panel, "HPoints", "PUAN", 30, new Color(0.75f, 0.7f, 0.85f), TextAnchor.MiddleCenter, Ui.FontBold, false);
            Ui.PlaceTop(hP.rectTransform, 390, 98, 160, 40);

            for (int i = 0; i < n; i++)
            {
                var r = new Row();
                r.rt = Ui.Rect(panel, "Row" + i);
                r.rt.anchorMin = r.rt.anchorMax = new Vector2(0.5f, 1f);
                r.rt.pivot = new Vector2(0.5f, 1f);
                r.rt.sizeDelta = new Vector2(W - 40, RowH - 8);
                r.y = r.targetY = Top + i * RowH;
                r.rt.anchoredPosition = new Vector2(0, -r.y);
                r.bg = Ui.Image(r.rt, "Bg", Ui.Rounded, new Color(1, 1, 1, i % 2 == 0 ? 0.07f : 0.03f));
                Ui.Stretch(r.bg.rectTransform);
                r.rank = Ui.Text(r.rt, "Rank", (i + 1).ToString(), 48, Color.white, TextAnchor.MiddleCenter, Ui.FontBlack);
                Place(r.rank.rectTransform, -430, 70);
                r.name = Ui.Text(r.rt, "Name", "", 46, Color.white, TextAnchor.MiddleLeft, Ui.FontBold);
                Place(r.name.rectTransform, -150, 440);
                r.kills = Ui.Text(r.rt, "Kills", "", 46, new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleCenter, Ui.FontBlack);
                Place(r.kills.rectTransform, 220, 160);
                r.points = Ui.Text(r.rt, "Points", "", 48, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter, Ui.FontBlack);
                Place(r.points.rectTransform, 390, 180);
                r.pop = Ui.Text(r.rt, "Pop", "", 44, new Color(0.5f, 1f, 0.5f), TextAnchor.MiddleLeft, Ui.FontBlack);
                Place(r.pop.rectTransform, 520, 120);
                rows.Add(r);
            }
            crown = Ui.Image(panel, "Crown", null, new Color(1f, 0.8f, 0.2f));
            crown.enabled = false;
        }

        static void Place(RectTransform rt, float x, float w)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = new Vector2(w, RowH);
        }

        void Refresh()
        {
            var gm = GameManager.I;
            var top = gm.ScoreRecords()
                .Where(r => r.points > 0 || r.kills > 0)
                .OrderByDescending(r => r.points).ThenByDescending(r => r.kills).ThenBy(r => r.nickname)
                .Take(rows.Count).ToList();

            // keep each player's row object so it can slide to its new rank
            var free = new List<Row>(rows);
            var assigned = new Row[top.Count];
            for (int i = 0; i < top.Count; i++)
            {
                var r = free.FirstOrDefault(x => x.userId == top[i].userId);
                if (r != null) { assigned[i] = r; free.Remove(r); }
            }
            for (int i = 0; i < top.Count; i++)
            {
                if (assigned[i] == null) { assigned[i] = free[0]; free.RemoveAt(0); assigned[i].userId = top[i].userId; assigned[i].lastPoints = -1; assigned[i].y = Top + (rows.Count + 0.5f) * RowH; }
                var r = assigned[i];
                var rec = top[i];
                r.targetY = Top + i * RowH;
                r.rank.text = (i + 1).ToString();
                r.rank.color = i == 0 ? new Color(1f, 0.82f, 0.25f) : i == 1 ? new Color(0.85f, 0.88f, 0.95f) : i == 2 ? new Color(0.9f, 0.6f, 0.35f) : Color.white;
                r.name.text = Ui.Short(rec.nickname, 18);
                var v = gm.Registry.Find(rec.userId);
                bool topGifter = v != null && v == gm.TopGifter;
                r.name.color = topGifter ? new Color(1f, 0.82f, 0.2f) : v != null && v.isBot ? new Color(0.8f, 0.8f, 0.88f) : Color.white;
                r.kills.text = rec.kills.ToString();
                r.points.text = Ui.Thousands(rec.points);
                if (r.lastPoints >= 0 && rec.points > r.lastPoints) r.flashUntil = Mathf.Max(r.flashUntil, Time.time + 0.6f);
                r.lastPoints = rec.points;
                r.rt.gameObject.SetActive(true);
            }
            foreach (var r in free) { r.rt.gameObject.SetActive(false); r.userId = null; }
        }

        void Update()
        {
            float now = Time.time;
            if (dirty && now >= nextRefresh)
            {
                dirty = false;
                nextRefresh = now + 0.5f;
                Refresh();
            }
            foreach (var r in rows)
            {
                if (!r.rt.gameObject.activeSelf) continue;
                r.y = Mathf.Lerp(r.y, r.targetY, 1f - Mathf.Exp(-8f * Time.deltaTime));
                r.rt.anchoredPosition = new Vector2(0, -r.y);
                float flash = Mathf.Clamp01((r.flashUntil - now) / 0.8f);
                var gm = GameManager.I;
                var v = r.userId != null ? gm.Registry.Find(r.userId) : null;
                bool glow = v != null && v == gm.TopGifter;
                float baseA = glow ? 0.18f + Mathf.Sin(now * 3f) * 0.06f : 0.06f;
                r.bg.color = Color.Lerp(glow ? new Color(1f, 0.75f, 0.2f, baseA) : new Color(1, 1, 1, baseA), new Color(1f, 0.85f, 0.3f, 0.55f), flash);
                r.rt.localScale = Vector3.one * (1f + flash * 0.04f);
                float pop = Mathf.Clamp01((r.popUntil - now) / 1.2f);
                r.pop.enabled = pop > 0f;
                if (pop > 0f)
                {
                    r.pop.color = new Color(0.5f, 1f, 0.5f, pop);
                    r.pop.rectTransform.anchoredPosition = new Vector2(520, (1f - pop) * 30f);
                }
            }
        }
    }
}
