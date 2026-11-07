using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Arena
{
    /// <summary>
    /// A physical TOP 5 stadium board. Its stand stays in the arena; only its face turns toward the camera.
    /// #1 gold with a small crown, #2 silver, #3 bronze. Live: rows slide when the order
    /// changes with green / red arrows, flash and pop "+10" on a knock-out, a green dot shows who is fighting right
    /// now, a gift box marks gifters, and a new leader gets a "NEW LEADER!" moment. Simple English + icons.
    /// </summary>
    public class LeaderboardPanel : MonoBehaviour
    {
        class Row
        {
            public RectTransform rt;
            public Image bg, badge, crown, arrow, dot, gift;
            public Text rank, name, kills, points, pop;
            public string userId;
            public float y, targetY, h;
            public float flashUntil, popUntil, arrowUntil;
            public int lastPoints = -1;
            public int rankIdx;
        }

        const float W = 760f, Header = 78f, RowH = 44f, Row1H = 58f, Pad = 14f;
        static readonly Color Gold = new Color(1f, 0.8f, 0.25f), Silver = new Color(0.82f, 0.86f, 0.93f), Bronze = new Color(0.86f, 0.56f, 0.3f);

        readonly List<Row> rows = new List<Row>();
        readonly Dictionary<string, int> lastRank = new Dictionary<string, int>();
        RectTransform panel;
        Text title;
        Image titleCrown;
        readonly Vector3[] screenCorners = new Vector3[4];
        Rect screenBounds;
        bool boundsVisible;
        bool dirty = true;
        float nextRefresh, newLeaderUntil;
        string leaderId;
        string defaultTitle;
        float H;
        static GameConfig Cfg => GameConfig.Current;

        public static LeaderboardPanel Create(Transform parent)
        {
            var go = new GameObject("Scoreboard", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var lp = go.AddComponent<LeaderboardPanel>();
            lp.Build();
            return lp;
        }

        public void MarkDirty() => dirty = true;

        /// <summary>Is this screen point (pixels) on the scoreboard? Name tags and emotes there are hidden.</summary>
        public static bool Covers(Vector3 screen)
        {
            var board = GameManager.I != null ? GameManager.I.Board : null;
            return board != null && board.boundsVisible && board.screenBounds.Contains(new Vector2(screen.x, screen.y));
        }

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

        // Default world-space UI respects depth. The stadium wall can genuinely occlude the screen.
        T M<T>(T g) where T : Graphic => g;

        void Build()
        {
            int n = Mathf.Clamp(Cfg.board.rows, 1, 5);
            defaultTitle = "TOP " + n;
            H = Pad + Header + Row1H + (n - 1) * RowH + Pad;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 0;
            panel = (RectTransform)transform;
            panel.sizeDelta = new Vector2(W, H);
            transform.localScale = Vector3.one * (6f * Cfg.board.scale / W);
            transform.position = GameManager.I.World.boardAnchor.position;
            StadiumSigns.BuildFrame(transform, W, H, Gold);
            var world = GameManager.I.World;
            StadiumSigns.BuildStand(world.root, transform.position, world.CamForwardFlat, world.CamRightFlat, 6f * Cfg.board.scale);

            // frame: gold rim, dark glassy inside, soft top shine
            var rim = M(Ui.Image(panel, "Rim", Ui.Rounded, Gold));
            Ui.Stretch(rim.rectTransform);
            var inner = M(Ui.Image(panel, "Inner", Ui.Rounded, new Color(0.08f, 0.06f, 0.14f, 0.95f)));
            Ui.Stretch(inner.rectTransform, 5);
            var shine = M(Ui.Image(panel, "Shine", Ui.Rounded, new Color(1f, 1f, 1f, 0.05f)));
            shine.rectTransform.anchorMin = new Vector2(0, 0.55f); shine.rectTransform.anchorMax = Vector2.one;
            shine.rectTransform.offsetMin = new Vector2(8, 0); shine.rectTransform.offsetMax = new Vector2(-8, -8);

            titleCrown = M(Ui.Image(panel, "TitleCrown", Icons.Get("crown"), Color.white));
            PlaceTL(titleCrown.rectTransform, 22, Pad + 8, 60, 60);
            title = M(Ui.Text(panel, "Title", defaultTitle, 56, Gold, TextAnchor.MiddleLeft, Ui.FontBlack));
            PlaceTL(title.rectTransform, 92, Pad + 4, 420, 66);
            var killIcon = M(Ui.Image(panel, "KillsIcon", Icons.Get("swords"), Color.white));
            PlaceCenter(killIcon.rectTransform, 220, -(Pad + Header * 0.5f), 40, 40);
            var ptsIcon = M(Ui.Image(panel, "PointsIcon", Icons.Get("star"), Color.white));
            PlaceCenter(ptsIcon.rectTransform, 318, -(Pad + Header * 0.5f), 42, 42);
            var line = M(Ui.Image(panel, "Line", null, new Color(1f, 0.8f, 0.25f, 0.7f)));
            PlaceTL(line.rectTransform, 18, Pad + Header - 6, W - 36, 3);

            for (int i = 0; i < n; i++)
            {
                var r = new Row { rankIdx = i, h = i == 0 ? Row1H : RowH };
                r.rt = Ui.Rect(panel, "Row" + i);
                r.rt.anchorMin = r.rt.anchorMax = new Vector2(0.5f, 1f);
                r.rt.pivot = new Vector2(0.5f, 1f);
                r.rt.sizeDelta = new Vector2(W - 28, r.h - 4);
                r.y = r.targetY = SlotY(i);
                r.rt.anchoredPosition = new Vector2(0, -r.y);
                r.bg = M(Ui.Image(r.rt, "Bg", Ui.Rounded, Color.clear));
                Ui.Stretch(r.bg.rectTransform);

                float bsz = i == 0 ? 48 : 38;
                r.arrow = M(Ui.Image(r.rt, "Arrow", Icons.Get("arrowUp"), Color.white));
                PlaceRow(r.arrow.rectTransform, -W * 0.5f + 34, 22, 22);
                r.badge = M(Ui.Image(r.rt, "Badge", Ui.Circle, Color.white));
                PlaceRow(r.badge.rectTransform, -W * 0.5f + 74, bsz, bsz);
                r.rank = M(Ui.Text(r.badge.transform, "Rank", (i + 1).ToString(), i == 0 ? 34 : 28, new Color(0.12f, 0.08f, 0.15f), TextAnchor.MiddleCenter, Ui.FontBlack, false));
                Ui.Stretch(r.rank.rectTransform);
                r.dot = M(Ui.Image(r.badge.transform, "Alive", Icons.Get("dot"), new Color(0.3f, 1f, 0.4f)));
                r.dot.rectTransform.anchorMin = r.dot.rectTransform.anchorMax = new Vector2(1f, 0f);
                r.dot.rectTransform.sizeDelta = new Vector2(18, 18);
                r.dot.rectTransform.anchoredPosition = new Vector2(-2, 4);
                if (i == 0)
                {
                    // a small crown sitting on the #1 badge, slightly tilted
                    r.crown = M(Ui.Image(r.rt, "Crown", Icons.Get("crown"), Color.white));
                    PlaceRow(r.crown.rectTransform, -W * 0.5f + 88, 34, 34);
                    r.crown.rectTransform.anchoredPosition += new Vector2(0, 26);
                    r.crown.rectTransform.localRotation = Quaternion.Euler(0, 0, -18);
                }
                r.name = M(Ui.Text(r.rt, "Name", "", i == 0 ? 40 : 33, Color.white, TextAnchor.MiddleLeft, Ui.FontBold));
                r.name.rectTransform.anchorMin = r.name.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                r.name.rectTransform.pivot = new Vector2(0f, 0.5f);
                r.name.rectTransform.anchoredPosition = new Vector2(-W * 0.5f + 108, 0);
                r.name.rectTransform.sizeDelta = new Vector2(330, r.h);
                r.gift = M(Ui.Image(r.rt, "Gift", Icons.Get("gift"), Color.white));
                PlaceRow(r.gift.rectTransform, 140, 28, 28);
                r.kills = M(Ui.Text(r.rt, "Kills", "", i == 0 ? 38 : 32, new Color(1f, 0.65f, 0.55f), TextAnchor.MiddleCenter, Ui.FontBlack));
                PlaceRow(r.kills.rectTransform, 220, 90, r.h);
                r.points = M(Ui.Text(r.rt, "Points", "", i == 0 ? 40 : 33, Gold, TextAnchor.MiddleCenter, Ui.FontBlack));
                PlaceRow(r.points.rectTransform, 318, 120, r.h);
                r.pop = M(Ui.Text(r.rt, "Pop", "", 30, new Color(0.5f, 1f, 0.5f), TextAnchor.MiddleCenter, Ui.FontBlack));
                PlaceRow(r.pop.rectTransform, 318, 90, r.h);
                r.rt.gameObject.SetActive(false);
                rows.Add(r);
            }
        }

        float SlotY(int i) => Pad + Header + (i == 0 ? 0 : Row1H + (i - 1) * RowH);

        void PlaceTL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void PlaceCenter(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void PlaceRow(RectTransform rt, float x, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = new Vector2(w, h);
        }

        void Refresh()
        {
            var gm = GameManager.I;
            var top = gm.ScoreRecords()
                .Where(r => !string.IsNullOrEmpty(r.nickname))
                .OrderByDescending(r => r.points).ThenByDescending(r => r.kills).ThenBy(r => r.nickname)
                .Take(rows.Count).ToList();
            float now = Time.time;

            // new leader moment
            string newLeader = top.Count > 0 ? top[0].userId : null;
            if (newLeader != null && leaderId != null && newLeader != leaderId)
            {
                newLeaderUntil = now + 2.6f;
                gm.Hud?.FeedIcon("crown", $"{top[0].nickname} NEW LEADER!", Gold);
            }
            leaderId = newLeader;

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (i >= top.Count) { r.rt.gameObject.SetActive(false); r.userId = null; continue; }
                var rec = top[i];
                bool wasShown = r.rt.gameObject.activeSelf;
                if (r.userId != rec.userId) { r.lastPoints = -1; }
                r.userId = rec.userId;
                r.rt.gameObject.SetActive(true);

                // slide from the old position when the rank changed
                if (lastRank.TryGetValue(rec.userId, out int old) && old != i)
                {
                    r.y = SlotY(Mathf.Min(old, rows.Count - 1));
                    r.arrow.sprite = Icons.Get(old > i ? "arrowUp" : "arrowDown");
                    r.arrowUntil = now + 3f;
                }
                else if (!wasShown) r.y = SlotY(i) + 30f;
                lastRank[rec.userId] = i;
                r.targetY = SlotY(i);

                var badgeCol = i == 0 ? Gold : i == 1 ? Silver : i == 2 ? Bronze : new Color(0.3f, 0.28f, 0.4f);
                r.badge.color = badgeCol;
                r.rank.color = i < 3 ? new Color(0.12f, 0.08f, 0.15f) : Color.white;
                r.name.text = Ui.Short(rec.nickname, i == 0 ? 15 : 17);
                var v = gm.Registry.Find(rec.userId);
                r.name.color = i == 0 ? new Color(1f, 0.9f, 0.55f) : i == 1 ? new Color(0.92f, 0.95f, 1f) : i == 2 ? new Color(1f, 0.82f, 0.62f) : (v != null && v.isBot ? new Color(0.78f, 0.78f, 0.86f) : Color.white);
                bool fighting = v != null && v.fighter != null && v.fighter.IsAlive;
                r.dot.color = fighting ? new Color(0.3f, 1f, 0.4f) : new Color(0.45f, 0.45f, 0.5f);
                bool gifter = v != null && v.streamCoins > 0;
                r.gift.enabled = gifter;
                if (gifter)
                {
                    float nameW = Mathf.Min(r.name.preferredWidth, 320f);
                    r.gift.rectTransform.anchoredPosition = new Vector2(-W * 0.5f + 108 + nameW + 22, 0);
                }
                r.kills.text = rec.kills.ToString();
                r.points.text = Ui.Thousands(rec.points);
                if (r.lastPoints >= 0 && rec.points > r.lastPoints) r.flashUntil = Mathf.Max(r.flashUntil, now + 0.6f);
                r.lastPoints = rec.points;
            }
        }

        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Cam == null) return;
            float now = Time.time;
            if (dirty && now >= nextRefresh)
            {
                dirty = false;
                nextRefresh = now + 0.5f;
                Refresh();
            }

            // A fixed stadium location, not a viewport ray. Following/zooming now creates real parallax.
            var cam = gm.Cam.cam;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
            transform.localScale = Vector3.one * (6f * Cfg.board.scale / W);
            panel.GetWorldCorners(screenCorners);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            boundsVisible = true;
            for (int j = 0; j < screenCorners.Length; j++)
            {
                var p = cam.WorldToScreenPoint(screenCorners[j]);
                boundsVisible &= p.z > 0f;
                minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
            }
            screenBounds = Rect.MinMaxRect(minX, minY, maxX, maxY);

            // Keep the existing leader celebration, in the physical display.
            bool leaderMoment = now < newLeaderUntil;
            title.text = leaderMoment ? "NEW LEADER!" : defaultTitle;
            title.color = leaderMoment ? Color.Lerp(Gold, Color.white, 0.5f + 0.5f * Mathf.Sin(now * 12f)) : Gold;
            titleCrown.rectTransform.localRotation = Quaternion.Euler(0, 0, leaderMoment ? Mathf.Sin(now * 10f) * 14f : Mathf.Sin(now * 1.5f) * 4f);
            titleCrown.rectTransform.localScale = Vector3.one * (leaderMoment ? 1.15f + Mathf.Sin(now * 12f) * 0.1f : 1f);

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (!r.rt.gameObject.activeSelf) continue;
                r.y = Mathf.Lerp(r.y, r.targetY, 1f - Mathf.Exp(-7f * Time.deltaTime));
                r.rt.anchoredPosition = new Vector2(0, -r.y);
                float flash = Mathf.Clamp01((r.flashUntil - now) / 0.8f);
                var baseCol = i == 0 ? new Color(1f, 0.78f, 0.2f, 0.3f + Mathf.Sin(now * 2.5f) * 0.05f)
                            : i == 1 ? new Color(0.8f, 0.85f, 0.95f, 0.2f)
                            : i == 2 ? new Color(0.85f, 0.55f, 0.3f, 0.22f)
                            : new Color(1f, 1f, 1f, i % 2 == 0 ? 0.06f : 0.03f);
                r.bg.color = Color.Lerp(baseCol, new Color(1f, 0.9f, 0.4f, 0.6f), flash);
                r.rt.localScale = Vector3.one * (1f + flash * 0.03f);
                float pop = Mathf.Clamp01((r.popUntil - now) / 1.2f);
                r.pop.enabled = pop > 0f;
                if (pop > 0f)
                {
                    r.pop.color = new Color(0.5f, 1f, 0.5f, pop);
                    r.pop.rectTransform.anchoredPosition = new Vector2(318, 16f + (1f - pop) * 22f);
                }
                r.arrow.enabled = now < r.arrowUntil;
                if (r.crown != null) r.crown.rectTransform.localRotation = Quaternion.Euler(0, 0, -18f + Mathf.Sin(now * 2f) * 4f);
            }
        }
    }
}
