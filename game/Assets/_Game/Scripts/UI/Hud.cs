using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arena
{
    /// <summary>
    /// Screen text, kept minimal (doc §1 second golden rule, §8.3, §9): top strip "Kalan · süre · Giriş açık/Son savaş",
    /// gift guide (seven tiers + "Yorum veya beğeni = katıl"), event lines (max 3, 4 s), winner banner.
    /// Nothing is put in TikTok's own areas (top ~9 %, bottom-left comments).
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        RectTransform root;
        Text strip, join, winnerTitle, winnerSub;
        Image stripBg, winnerBg;
        readonly List<(Text t, Image bg, float until)> feed = new List<(Text, Image, float)>();
        RectTransform feedRoot;
        float nextStrip;
        static GameConfig Cfg => GameConfig.Current;
        const string JoinOpen = "Yorum veya\nbeğeni = katıl";
        const string JoinClosed = "Giriş kapalı\nhediye = gir";

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
            Ui.PlaceTop(stripBg.rectTransform, 0, 176, 700, 64);
            strip = Ui.Text(stripBg.transform, "Strip", "", 44, Color.white, TextAnchor.MiddleCenter, Ui.FontBlack);
            Ui.Stretch(strip.rectTransform);

            feedRoot = Ui.Rect(root, "Feed");
            Ui.PlaceTop(feedRoot, 0, 690, 900, 220);

            BuildGiftGuide();

            winnerBg = Ui.Image(root, "WinnerBg", Ui.Rounded, new Color(0.08f, 0.05f, 0.02f, 0.75f));
            Ui.PlaceTop(winnerBg.rectTransform, 0, 620, 900, 230);
            winnerTitle = Ui.Text(winnerBg.transform, "Title", "", 86, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, Ui.FontBlack);
            Ui.PlaceTop(winnerTitle.rectTransform, 0, 20, 880, 110);
            winnerSub = Ui.Text(winnerBg.transform, "Sub", "", 54, Color.white, TextAnchor.MiddleCenter, Ui.FontBold);
            Ui.PlaceTop(winnerSub.rectTransform, 0, 140, 880, 70);
            winnerBg.gameObject.SetActive(false);

            // EventSystem is not needed: nothing on screen is clickable (the dev panel uses IMGUI)
        }

        void BuildGiftGuide()
        {
            var tiers = Cfg.gifts.tiers;
            var guide = Ui.Rect(root, "GiftGuide");
            guide.anchorMin = guide.anchorMax = new Vector2(1f, 0.5f);
            guide.pivot = new Vector2(1f, 0.5f);
            guide.anchoredPosition = new Vector2(-10, -170);
            guide.sizeDelta = new Vector2(236, tiers.Length * 56 + 100);
            var bg = Ui.Image(guide, "Bg", Ui.Rounded, new Color(0.05f, 0.04f, 0.08f, 0.5f));
            Ui.Stretch(bg.rectTransform);
            Color[] colors =
            {
                new Color(1f, 0.55f, 0.3f), new Color(0.85f, 0.6f, 0.35f), new Color(0.6f, 0.8f, 1f), new Color(0.5f, 0.9f, 1f),
                new Color(1f, 0.85f, 0.3f), new Color(1f, 0.45f, 0.95f), new Color(0.75f, 0.75f, 1f),
            };
            for (int i = 0; i < tiers.Length; i++)
            {
                float y = -10 - i * 56;
                var row = Ui.Rect(guide, "Tier" + i);
                row.anchorMin = row.anchorMax = new Vector2(0, 1);
                row.pivot = new Vector2(0, 1);
                row.anchoredPosition = new Vector2(10, y);
                row.sizeDelta = new Vector2(220, 54);
                var coin = Ui.Image(row, "Coin", Ui.Coin, Color.white);
                coin.rectTransform.anchorMin = coin.rectTransform.anchorMax = new Vector2(0, 0.5f);
                coin.rectTransform.anchoredPosition = new Vector2(20, 0);
                coin.rectTransform.sizeDelta = new Vector2(34, 34);
                var amount = Ui.Text(row, "Amount", tiers[i].minCoins >= 1000 ? (tiers[i].minCoins / 1000) + "B" : tiers[i].minCoins.ToString(), 34, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleLeft, Ui.FontBlack);
                amount.rectTransform.anchorMin = amount.rectTransform.anchorMax = new Vector2(0, 0.5f);
                amount.rectTransform.pivot = new Vector2(0, 0.5f);
                amount.rectTransform.anchoredPosition = new Vector2(42, 0);
                amount.rectTransform.sizeDelta = new Vector2(60, 50);
                amount.fontSize = 30;
                var word = Ui.Text(row, "Word", tiers[i].label, 32, colors[i % colors.Length], TextAnchor.MiddleLeft, Ui.FontBold);
                word.rectTransform.anchorMin = word.rectTransform.anchorMax = new Vector2(0, 0.5f);
                word.rectTransform.pivot = new Vector2(0, 0.5f);
                word.rectTransform.anchoredPosition = new Vector2(100, 0);
                word.rectTransform.sizeDelta = new Vector2(120, 50);
            }
            // "Yorum veya beğeni = katıl" next to the gift guide (doc §9)
            join = Ui.Text(guide, "Join", JoinOpen, 30, new Color(1f, 0.92f, 0.7f), TextAnchor.MiddleCenter, Ui.FontBold);
            join.rectTransform.anchorMin = join.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            join.rectTransform.pivot = new Vector2(0.5f, 0f);
            join.rectTransform.anchoredPosition = new Vector2(0, 10);
            join.rectTransform.sizeDelta = new Vector2(220, 76);
        }

        /// <summary>Event line: only gift moves and gifters' knock-outs, e.g. "Ahmet34 TORNADO!" (doc §9).</summary>
        public void Feed(string text, Color color)
        {
            if (feed.Count >= 3)
            {
                Destroy(feed[0].bg.gameObject);
                feed.RemoveAt(0);
            }
            var bg = Ui.Image(feedRoot, "Line", Ui.Rounded, new Color(0, 0, 0, 0.5f));
            var t = Ui.Text(bg.transform, "T", text, 40, color, TextAnchor.MiddleCenter, Ui.FontBlack);
            Ui.Stretch(t.rectTransform);
            float w = Mathf.Min(880, t.preferredWidth + 60);
            bg.rectTransform.sizeDelta = new Vector2(w, 60);
            feed.Add((t, bg, Time.time + 4f));
            Layout();
        }

        void Layout()
        {
            for (int i = 0; i < feed.Count; i++)
            {
                var rt = feed[i].bg.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0, -i * 64);
            }
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            float now = Time.time;
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                float left = feed[i].until - now;
                if (left <= 0f) { Destroy(feed[i].bg.gameObject); feed.RemoveAt(i); Layout(); continue; }
                float a = Mathf.Clamp01(left / 0.5f);
                feed[i].t.color = Ui.WithAlpha(feed[i].t.color, a);
                feed[i].bg.color = new Color(0, 0, 0, 0.5f * a);
            }

            if (now >= nextStrip)
            {
                nextStrip = now + 0.2f;
                int alive = gm.Fighters.AliveCount;
                string phase;
                switch (gm.State)
                {
                    case RoundState.Prep: phase = "Hazırlık"; break;
                    case RoundState.Battle: phase = gm.CommentEntryOpen ? "Giriş açık" : "Savaş"; break;
                    case RoundState.Final: phase = "SON SAVAŞ"; break;
                    default: phase = "Kazanan"; break;
                }
                float t = gm.State == RoundState.Prep ? Mathf.Max(0, Cfg.round.prepSec - (now - gm.StateStart)) : gm.RoundElapsed;
                int mm = Mathf.FloorToInt(t / 60f), ss = Mathf.FloorToInt(t % 60f);
                strip.text = $"Kalan: {alive}   {mm:00}:{ss:00}   {phase}";
                strip.color = gm.State == RoundState.Final ? new Color(1f, 0.55f, 0.25f) : Color.white;
                join.text = gm.CommentEntryOpen ? JoinOpen : JoinClosed;
                if (gm.Paused) strip.text = "DURAKLATILDI";
            }

            bool win = gm.State == RoundState.Winner && gm.WinnerText != null;
            if (winnerBg.gameObject.activeSelf != win) winnerBg.gameObject.SetActive(win);
            if (win)
            {
                winnerTitle.text = gm.WinnerText;
                winnerSub.text = gm.WinnerSub;
                float pulse = 1f + Mathf.Sin(now * 4f) * 0.03f;
                winnerBg.rectTransform.localScale = Vector3.one * pulse;
            }
        }
    }
}
