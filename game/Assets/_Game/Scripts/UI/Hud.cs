using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arena
{
    /// <summary>
    /// Screen text, few and simple English words + icons so everyone understands (user request):
    /// top strip (alive · time · JOIN OPEN / FINAL FIGHT), "JOIN NOW!" countdown while a round gets ready, gift guide
    /// (gift picture or coin + amount -> move icon), "comment / like = JOIN", event lines (max 3, 4 s), winner banner.
    /// Nothing in TikTok's own areas (top ~9 %, bottom-left comments).
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        RectTransform root;
        Text strip, joinText, winnerTitle, winnerName, winnerSub, lobbyTitle, lobbyCount;
        Image stripBg, winnerBg, joinIcon1, joinIcon2, winnerCrown;
        RectTransform lobby;
        readonly List<(RectTransform rt, Text t, Image bg, Image icon, float until)> feed = new List<(RectTransform, Text, Image, Image, float)>();
        RectTransform feedRoot;
        float nextStrip;
        static GameConfig Cfg => GameConfig.Current;

        public void Init()
        {
            var go = new GameObject("HudCanvas");
            go.transform.SetParent(transform, false);
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 1f;
            root = (RectTransform)go.transform;

            // top strip just below TikTok's own top area (~9 %)
            stripBg = Ui.Image(root, "StripBg", Ui.Rounded, new Color(0.05f, 0.04f, 0.08f, 0.62f));
            Ui.PlaceTop(stripBg.rectTransform, 0, 176, 640, 62);
            strip = Ui.Text(stripBg.transform, "Strip", "", 42, Color.white, TextAnchor.MiddleCenter, Ui.FontBlack);
            Ui.Stretch(strip.rectTransform);

            feedRoot = Ui.Rect(root, "Feed");
            Ui.PlaceTop(feedRoot, 0, 700, 900, 220);

            BuildGiftGuide();
            // The permanent gift key now lives on a stadium sign, leaving the fighting area clear.
            root.Find("GiftGuide").gameObject.SetActive(false);
            BuildLobby();
            BuildWinner();
        }

        static string Amount(int c) => c >= 1000 ? (c % 1000 == 0 ? (c / 1000) + "K" : (c / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "K") : c.ToString();

        void BuildGiftGuide()
        {
            var tiers = Cfg.gifts.tiers;
            var guide = Ui.Rect(root, "GiftGuide");
            guide.anchorMin = guide.anchorMax = new Vector2(1f, 0.5f);
            guide.pivot = new Vector2(1f, 0.5f);
            guide.anchoredPosition = new Vector2(-10, -160);
            float rowH = 58f;
            guide.sizeDelta = new Vector2(196, tiers.Length * rowH + 110);
            var bg = Ui.Image(guide, "Bg", Ui.Rounded, new Color(0.05f, 0.04f, 0.08f, 0.55f));
            Ui.Stretch(bg.rectTransform);
            for (int i = 0; i < tiers.Length; i++)
            {
                var row = Ui.Rect(guide, "Tier" + i);
                row.anchorMin = row.anchorMax = new Vector2(0, 1);
                row.pivot = new Vector2(0, 1);
                row.anchoredPosition = new Vector2(8, -8 - i * rowH);
                row.sizeDelta = new Vector2(180, rowH);
                // the streamer's own gift picture (StreamingAssets/GiftIcons/<icon>) or a coin
                var pic = GiftResolver.Picture(tiers[i].icon);
                var gift = Ui.Image(row, "Gift", pic != null ? pic : Icons.Get("coin"), Color.white);
                gift.preserveAspect = true;
                gift.rectTransform.anchorMin = gift.rectTransform.anchorMax = new Vector2(0, 0.5f);
                gift.rectTransform.anchoredPosition = new Vector2(24, 0);
                gift.rectTransform.sizeDelta = new Vector2(pic != null ? 46 : 36, pic != null ? 46 : 36);
                var amount = Ui.Text(row, "Amount", Amount(tiers[i].minCoins), 32, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleLeft, Ui.FontBlack);
                amount.rectTransform.anchorMin = amount.rectTransform.anchorMax = new Vector2(0, 0.5f);
                amount.rectTransform.pivot = new Vector2(0, 0.5f);
                amount.rectTransform.anchoredPosition = new Vector2(50, 0);
                amount.rectTransform.sizeDelta = new Vector2(70, 50);
                var arrow = Ui.Text(row, "Arrow", "›", 40, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter, Ui.FontBlack, false);
                arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0, 0.5f);
                arrow.rectTransform.anchoredPosition = new Vector2(122, 2);
                arrow.rectTransform.sizeDelta = new Vector2(24, 50);
                System.Enum.TryParse(tiers[i].move, out MoveKind mk);
                var move = Ui.Image(row, "Move", Icons.ForMove(mk), Color.white);
                move.rectTransform.anchorMin = move.rectTransform.anchorMax = new Vector2(0, 0.5f);
                move.rectTransform.anchoredPosition = new Vector2(156, 0);
                move.rectTransform.sizeDelta = new Vector2(50, 50);
            }
            // "comment / like = JOIN" next to the gift guide (doc §9)
            var join = Ui.Rect(guide, "Join");
            join.anchorMin = join.anchorMax = new Vector2(0.5f, 0f);
            join.pivot = new Vector2(0.5f, 0f);
            join.anchoredPosition = new Vector2(0, 10);
            join.sizeDelta = new Vector2(190, 90);
            joinIcon1 = Ui.Image(join, "Comment", Icons.Get("comment"), Color.white);
            joinIcon1.rectTransform.anchoredPosition = new Vector2(-62, 18);
            joinIcon1.rectTransform.sizeDelta = new Vector2(44, 44);
            joinIcon2 = Ui.Image(join, "Heart", Icons.Get("heart"), Color.white);
            joinIcon2.rectTransform.anchoredPosition = new Vector2(-14, 18);
            joinIcon2.rectTransform.sizeDelta = new Vector2(42, 42);
            joinText = Ui.Text(join, "Text", "JOIN", 34, new Color(1f, 0.92f, 0.7f), TextAnchor.MiddleLeft, Ui.FontBlack);
            joinText.rectTransform.anchoredPosition = new Vector2(62, 18);
            joinText.rectTransform.sizeDelta = new Vector2(100, 44);
            var eq = Ui.Text(join, "Eq", "=", 34, Color.white, TextAnchor.MiddleCenter, Ui.FontBlack);
            eq.rectTransform.anchoredPosition = new Vector2(22, 18);
            eq.rectTransform.sizeDelta = new Vector2(30, 44);
        }

        void BuildLobby()
        {
            lobby = Ui.Rect(root, "Lobby");
            Ui.PlaceTop(lobby, 0, 760, 900, 300);
            var bg = Ui.Image(lobby, "Bg", Ui.Rounded, new Color(0.05f, 0.03f, 0.1f, 0.6f));
            Ui.Stretch(bg.rectTransform);
            lobbyTitle = Ui.Text(lobby, "Title", "JOIN NOW!", 110, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, Ui.FontBlack);
            Ui.PlaceTop(lobbyTitle.rectTransform, 0, 10, 880, 130);
            var c = Ui.Image(lobby, "Comment", Icons.Get("comment"), Color.white);
            c.rectTransform.anchorMin = c.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            c.rectTransform.anchoredPosition = new Vector2(-170, -200);
            c.rectTransform.sizeDelta = new Vector2(80, 80);
            var h = Ui.Image(lobby, "Heart", Icons.Get("heart"), Color.white);
            h.rectTransform.anchorMin = h.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            h.rectTransform.anchoredPosition = new Vector2(-80, -200);
            h.rectTransform.sizeDelta = new Vector2(76, 76);
            var g = Ui.Image(lobby, "Gift", Icons.Get("gift"), Color.white);
            g.rectTransform.anchorMin = g.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            g.rectTransform.anchoredPosition = new Vector2(10, -200);
            g.rectTransform.sizeDelta = new Vector2(76, 76);
            lobbyCount = Ui.Text(lobby, "Count", "", 80, Color.white, TextAnchor.MiddleCenter, Ui.FontBlack);
            lobbyCount.rectTransform.anchorMin = lobbyCount.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            lobbyCount.rectTransform.anchoredPosition = new Vector2(170, -200);
            lobbyCount.rectTransform.sizeDelta = new Vector2(200, 100);
            lobby.gameObject.SetActive(false);
        }

        void BuildWinner()
        {
            winnerBg = Ui.Image(root, "WinnerBg", Ui.Rounded, new Color(0.08f, 0.05f, 0.02f, 0.78f));
            Ui.PlaceTop(winnerBg.rectTransform, 0, 760, 900, 300);
            winnerCrown = Ui.Image(winnerBg.transform, "Crown", Icons.Get("crown"), Color.white);
            winnerCrown.rectTransform.anchorMin = winnerCrown.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            winnerCrown.rectTransform.anchoredPosition = new Vector2(0, -10);
            winnerCrown.rectTransform.sizeDelta = new Vector2(120, 120);
            winnerTitle = Ui.Text(winnerBg.transform, "Title", "WINNER", 54, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, Ui.FontBlack);
            Ui.PlaceTop(winnerTitle.rectTransform, 0, 60, 880, 70);
            winnerName = Ui.Text(winnerBg.transform, "Name", "", 90, Color.white, TextAnchor.MiddleCenter, Ui.FontBlack);
            Ui.PlaceTop(winnerName.rectTransform, 0, 125, 880, 100);
            winnerSub = Ui.Text(winnerBg.transform, "Sub", "", 48, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter, Ui.FontBold);
            Ui.PlaceTop(winnerSub.rectTransform, 0, 225, 880, 60);
            winnerBg.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ event lines
        public void Feed(string text, Color color) => AddLine(null, text, color);
        public void FeedIcon(string icon, string text, Color color) => AddLine(Icons.Get(icon), text, color);
        public void FeedMove(string who, MoveKind k) => AddLine(Icons.ForMove(k), $"{Ui.Short(who, 14)}  {GiftResolver.Label(k)}!", new Color(1f, 0.85f, 0.3f));

        void AddLine(Sprite icon, string text, Color color)
        {
            if (feed.Count >= 3)
            {
                Destroy(feed[0].rt.gameObject);
                feed.RemoveAt(0);
            }
            var rt = Ui.Rect(feedRoot, "Line");
            var bg = Ui.Image(rt, "Bg", Ui.Rounded, new Color(0, 0, 0, 0.5f));
            Ui.Stretch(bg.rectTransform);
            Image img = null;
            float iconW = 0f;
            if (icon != null)
            {
                img = Ui.Image(rt, "Icon", icon, Color.white);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                img.rectTransform.anchoredPosition = new Vector2(34, 0);
                img.rectTransform.sizeDelta = new Vector2(50, 50);
                iconW = 56f;
            }
            var t = Ui.Text(rt, "T", text, 40, color, TextAnchor.MiddleCenter, Ui.FontBlack);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(iconW + 10, 0); t.rectTransform.offsetMax = new Vector2(-14, 0);
            float w = Mathf.Min(900, t.preferredWidth + 40 + iconW);
            rt.sizeDelta = new Vector2(w, 60);
            rt.localScale = Vector3.one * 0.6f;
            feed.Add((rt, t, bg, img, Time.time + 4f));
            Layout();
        }

        void Layout()
        {
            for (int i = 0; i < feed.Count; i++)
            {
                var rt = feed[i].rt;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0, -i * 66);
            }
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            float now = Time.time;
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                var (rt, t, bg, icon, until) = feed[i];
                float left = until - now;
                if (left <= 0f) { Destroy(rt.gameObject); feed.RemoveAt(i); Layout(); continue; }
                float age = 4f - left;
                rt.localScale = Vector3.one * (age < 0.15f ? Mathf.Lerp(0.6f, 1.08f, age / 0.15f) : age < 0.25f ? Mathf.Lerp(1.08f, 1f, (age - 0.15f) / 0.1f) : 1f);
                float a = Mathf.Clamp01(left / 0.5f);
                t.color = Ui.WithAlpha(t.color, a);
                bg.color = new Color(0, 0, 0, 0.5f * a);
                if (icon != null) icon.color = new Color(1, 1, 1, a);
            }

            if (now >= nextStrip)
            {
                nextStrip = now + 0.2f;
                int alive = gm.Fighters.AliveCount;
                string phase;
                switch (gm.State)
                {
                    case RoundState.Prep: phase = "GET READY"; break;
                    case RoundState.Battle: phase = gm.CommentEntryOpen ? "JOIN OPEN" : "FIGHT!"; break;
                    case RoundState.Final: phase = "FINAL FIGHT"; break;
                    default: phase = "WINNER"; break;
                }
                float t = gm.State == RoundState.Prep ? Mathf.Max(0, Cfg.round.prepSec - (now - gm.StateStart)) : gm.RoundElapsed;
                int mm = Mathf.FloorToInt(t / 60f), ss = Mathf.FloorToInt(t % 60f);
                strip.text = $"{alive} ALIVE    {mm}:{ss:00}    {phase}";
                strip.color = gm.State == RoundState.Final ? new Color(1f, 0.55f, 0.25f) : Color.white;
                bool open = gm.CommentEntryOpen;
                joinIcon1.enabled = open;
                joinIcon2.sprite = Icons.Get(open ? "heart" : "gift");
                if (gm.Paused) strip.text = "PAUSED";
            }

            // round preparation: "JOIN NOW!" with countdown
            bool prep = gm.State == RoundState.Prep;
            if (lobby.gameObject.activeSelf != prep) lobby.gameObject.SetActive(prep);
            if (prep)
            {
                float left = Mathf.Max(0f, Cfg.round.prepSec - (now - gm.StateStart));
                lobbyCount.text = Mathf.CeilToInt(left).ToString();
                lobbyTitle.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(now * 5f) * 0.04f);
                lobbyCount.rectTransform.localScale = Vector3.one * (1f + Mathf.Clamp01(1f - (left % 1f) * 4f) * 0.25f);
            }

            bool win = gm.State == RoundState.Winner && gm.WinnerText != null;
            if (winnerBg.gameObject.activeSelf != win) winnerBg.gameObject.SetActive(win);
            if (win)
            {
                winnerTitle.text = gm.WinnerIsBot ? "BOT WINS" : "WINNER";
                winnerName.text = Ui.Short(gm.WinnerName ?? "", 16);
                winnerSub.text = gm.WinnerSub;
                float pulse = 1f + Mathf.Sin(now * 4f) * 0.03f;
                winnerBg.rectTransform.localScale = Vector3.one * pulse;
                winnerCrown.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(now * 3f) * 8f);
            }
        }
    }
}
