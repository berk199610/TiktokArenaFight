using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arena
{
    /// <summary>
    /// Test without TikTok (doc §11 Aşama 1): keyboard fake events + a small panel (F1).
    ///   C = yorum, L = beğeni, 1-7 = hediye kademeleri, N = yeni izleyici, V = +10 izleyici,
    ///   T = kalabalık botu aç/kapa, E = turu bitir, P = duraklat, F1 = panel.
    ///   Fare ile bir karaktere tıkla = o karakter seçilir (olaylar ona gider). Boşluğa tıkla = "Berk (sen)".
    ///   Ctrl+Shift+R 3 saniye basılı = skor tablosunu sıfırla (doc §9).
    /// </summary>
    public class DevKeys : MonoBehaviour
    {
        public static DevKeys I { get; private set; }
        const string TestId = "dev:berk";
        const string TestName = "Berk";
        string selectedId = TestId;
        bool panel;
        public bool traffic;
        float resetHeld;
        int newSerial;
        TrafficSim sim;
        float fpsAvg = 60f;

        void Awake()
        {
            I = this;
            sim = new TrafficSim();
            panel = Application.isEditor;
        }

        void Send(string type, int coins = 0, int count = 1, string id = null, string name = null)
        {
            id ??= selectedId;
            var v = GameManager.I.Registry.Find(id);
            name ??= v != null ? v.nickname : (id == TestId ? TestName : id);
            GameManager.I.Router.Handle(new ViewerEvent { type = type, userId = id, nickname = name, coins = coins, count = count, giftName = coins > 0 ? "Test" : null, text = "test" });
        }

        void Update()
        {
            fpsAvg = Mathf.Lerp(fpsAvg, 1f / Mathf.Max(0.001f, Time.unscaledDeltaTime), 0.05f);
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.cKey.wasPressedThisFrame) Send("comment");
                if (kb.lKey.wasPressedThisFrame) Send("like", 0, 1);
                for (int i = 0; i < Mathf.Min(9, GameConfig.Current.gifts.tiers.Length); i++)
                {
                    var key = kb[(Key)((int)Key.Digit1 + i)];
                    if (key.wasPressedThisFrame) Send("gift", GiftResolver.CoinsForTier(i), 1);
                }
                if (kb.nKey.wasPressedThisFrame) NewViewers(1);
                if (kb.vKey.wasPressedThisFrame) NewViewers(10);
                if (kb.tKey.wasPressedThisFrame) traffic = !traffic;
                if (kb.eKey.wasPressedThisFrame) GameManager.I.Command("endRound", null);
                if (kb.pKey.wasPressedThisFrame) GameManager.I.Command("pause", null);
                if (kb.f1Key.wasPressedThisFrame) panel = !panel;
                bool combo = (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) && kb.rKey.isPressed;
                resetHeld = combo ? resetHeld + Time.unscaledDeltaTime : 0f;
                if (resetHeld >= 3f) { resetHeld = -999f; GameManager.I.Command("resetScores", null); GameManager.I.Hud.Feed("Skor tablosu sıfırlandı", Color.white); }
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !OverPanel(mouse.position.ReadValue())) Pick(mouse.position.ReadValue());
            if (traffic) sim.Tick(Time.deltaTime);
        }

        public void NewViewers(int n)
        {
            for (int i = 0; i < n; i++)
            {
                newSerial++;
                string id = "dev:viewer" + newSerial;
                Send("comment", 0, 1, id, TrafficSim.RandomName(newSerial + 1000));
            }
        }

        bool OverPanel(Vector2 screen)
        {
            if (!panel) return false;
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            var guiPos = new Vector2(screen.x, Screen.height - screen.y) / scale;
            return panelRect.Contains(guiPos);
        }

        void Pick(Vector2 screen)
        {
            var cam = GameManager.I.Cam.cam;
            Fighter best = null;
            float bd = 70f * Screen.height / 1080f;
            foreach (var f in GameManager.I.Fighters.Active)
            {
                if (!f.IsAlive) continue;
                var sp = cam.WorldToScreenPoint(f.transform.position + Vector3.up * 1f);
                if (sp.z <= 0) continue;
                float d = Vector2.Distance(screen, sp);
                if (d < bd) { bd = d; best = f; }
            }
            selectedId = best != null ? best.viewer.userId : TestId;
            if (best != null) best.nameVisibleUntil = Time.time + 3f;
        }

        // ------------------------------------------------------------------ IMGUI panel
        Rect panelRect = new Rect(10, 10, 300, 500);
        GUIStyle box, btn, lbl;

        void OnGUI()
        {
            if (!panel) return;
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (btn == null)
            {
                btn = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
                lbl = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                box = new GUIStyle(GUI.skin.box);
            }
            var gm = GameManager.I;
            var v = gm.Registry.Find(selectedId);
            string who = v != null ? v.DisplayName : TestName;
            GUI.Box(panelRect, "", box);
            GUILayout.BeginArea(new Rect(panelRect.x + 8, panelRect.y + 6, panelRect.width - 16, panelRect.height - 12));
            GUILayout.Label($"<b>Test paneli</b> (F1 gizle)   FPS {fpsAvg:0}", lbl);
            GUILayout.Label($"Tur {gm.RoundNo} · {gm.State} · Arenada {gm.Fighters.AliveCount} · Sıra {gm.Roster.QueueCount}", lbl);
            GUILayout.Label($"Köprü: {(gm.Bridge != null && gm.Bridge.Connected ? "<color=#7f7>bağlı</color>" : "<color=#f99>yok</color>")}", lbl);
            string state = v?.fighter != null && v.fighter.IsAlive ? $"can {v.fighter.hp:0} · zekâ %{v.fighter.intelligence * 100:0}" : v != null ? v.state.ToString() : "henüz girmedi";
            GUILayout.Label($"Seçili: <b>{who}</b> ({state})", lbl);
            GUILayout.Label("Karaktere tıkla = seç, boşluğa tıkla = Berk", lbl);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yorum (C)", btn)) Send("comment");
            if (GUILayout.Button("Beğeni (L)", btn)) Send("like", 0, 1);
            GUILayout.EndHorizontal();
            var tiers = GameConfig.Current.gifts.tiers;
            for (int i = 0; i < tiers.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 2, tiers.Length); j++)
                    if (GUILayout.Button($"{j + 1}) {tiers[j].label} ({tiers[j].minCoins})", btn)) Send("gift", tiers[j].minCoins, 1);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 izleyici (N)", btn)) NewViewers(1);
            if (GUILayout.Button("+10 izleyici (V)", btn)) NewViewers(10);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(traffic ? "Kalabalık: AÇIK (T)" : "Kalabalık: kapalı (T)", btn)) traffic = !traffic;
            if (GUILayout.Button("Turu bitir (E)", btn)) gm.Command("endRound", null);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(gm.Paused ? "Devam (P)" : "Duraklat (P)", btn)) gm.Command("pause", null);
            if (GUILayout.Button("Skoru sıfırla", btn)) gm.Command("resetScores", null);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }

    /// <summary>Crowd bot: a chosen number of fake viewers producing realistic traffic (doc §10.2 simulator).</summary>
    public class TrafficSim
    {
        readonly List<string> ids = new List<string>();
        float acc;
        static readonly string[] first = { "ahmet", "mehmet", "ayse", "fatma", "zeynep", "emre", "burak", "elif", "deniz", "cem", "selin", "berk", "ece", "mert", "irem", "kaan", "naz", "efe", "derin", "arda" };
        static readonly string[] tail = { "34", "06", "35", "_tr", ".x", "_01", "07", "_kral", ".official", "61", "42", "_gs", "_fb", "16" };

        public static string RandomName(int seed)
        {
            var r = new System.Random(seed * 7919);
            return first[r.Next(first.Length)] + tail[r.Next(tail.Length)];
        }

        public void Tick(float dt)
        {
            var cfg = GameConfig.Current.dev;
            while (ids.Count < cfg.trafficViewers) ids.Add("sim:" + ids.Count);
            acc += dt * cfg.trafficEventsPerSec;
            while (acc >= 1f)
            {
                acc -= 1f;
                int i = Random.Range(0, ids.Count);
                string id = ids[i];
                string name = RandomName(i);
                float roll = Random.value;
                var e = new ViewerEvent { userId = id, nickname = name, count = 1 };
                if (roll < 0.55f) { e.type = "like"; e.count = Random.Range(1, 6); }
                else if (roll < 0.9f) { e.type = "comment"; e.text = "hadi!"; }
                else
                {
                    e.type = "gift";
                    float g = Random.value;
                    e.coins = g < 0.55f ? 1 : g < 0.75f ? 5 : g < 0.87f ? 30 : g < 0.94f ? 100 : g < 0.98f ? 300 : g < 0.995f ? 1000 : 5000;
                    e.giftName = "Sim";
                }
                GameManager.I.Router.Handle(e);
            }
        }
    }
}
