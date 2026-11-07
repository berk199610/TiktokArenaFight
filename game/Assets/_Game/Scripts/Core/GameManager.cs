using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    public enum RoundState { Prep, Battle, Final, Winner }

    /// <summary>
    /// The only object in the scene. Builds every system from code and runs the round flow (doc §9):
    /// Hazırlık (10 s) -> Savaş (comment/like entry open the first 90 s) -> Son savaş (fire ring) -> Kazanan (8 s).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public PersistenceService Persistence;
        public ViewerRegistry Registry;
        public RosterManager Roster;
        public ActivityMonitor Activity;
        public EventRouter Router;
        public FighterSystem Fighters;
        public ArenaWorld World;
        public ArenaCamera Cam;
        public FireRing Ring;
        public Hud Hud;
        public Nameplates Names;
        public EmoteSystem Emotes;
        public LeaderboardPanel Board;
        public RespawnMarkers Graves;
        public BridgeClient Bridge;

        public RoundState State { get; private set; }
        public float StateStart { get; private set; }
        public float BattleStart { get; private set; }
        public int RoundNo { get; private set; }
        public bool Paused;
        public Viewer TopGifter { get; private set; }
        public string WinnerText { get; private set; }
        public string WinnerName { get; private set; }
        public bool WinnerIsBot { get; private set; }
        public string WinnerSub { get; private set; }

        int peakAlive;
        bool top10Awarded;
        Fighter lastDied;
        float lastConfettiAt;
        float nextRankLook;
        static GameConfig Cfg => GameConfig.Current;

        public float SafeRadius => Ring != null && Ring.Active ? Ring.Radius : Cfg.arena.radius;
        public bool CommentEntryOpen => State == RoundState.Prep || (State == RoundState.Battle && Time.time - BattleStart < Cfg.roster.commentEntryWindowSec);
        public bool GiftEntryOpen => State == RoundState.Prep || State == RoundState.Battle;
        public float RoundElapsed => State == RoundState.Prep ? 0f : Time.time - BattleStart;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            GameConfig.Load();
            GiftCatalog.Rebuild(Cfg);
            GameConfig.Reloaded += () => GiftCatalog.Rebuild(Cfg);

            var fxGo = new GameObject("Fx");
            fxGo.transform.SetParent(transform, false);
            fxGo.AddComponent<Fx>().Init();

            Persistence = new PersistenceService();
            Registry = new ViewerRegistry(Persistence);
            Roster = new RosterManager(Registry);
            Activity = new ActivityMonitor();
            Router = new EventRouter(Registry, Roster, Persistence);
            World = new ArenaWorld(transform);
            Fighters = new FighterSystem(transform);
            Ring = new FireRing(World.root);

            var camGo = new GameObject("ArenaCamera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<ArenaCamera>();
            Cam.Init();

            // paint every icon once at start (no hitch mid-stream)
            foreach (var n in new[] { "fist", "bat", "shovel", "spin", "wave", "giant", "thunder", "tornado", "crown", "star", "swords", "skull", "heart", "comment", "gift", "coin", "arrowUp", "arrowDown", "dot", "person", "fire", "sos",
                                      "angry", "tired", "sneaky", "laugh", "cool", "scared", "dizzy", "love", "starstruck", "happy" })
                Icons.Get(n);
            Board = LeaderboardPanel.Create(transform);
            StadiumSigns.Create(World.root);
            Graves = new GameObject("RespawnMarkers").AddComponent<RespawnMarkers>();
            Graves.transform.SetParent(World.root, false);
            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(transform, false);
            Hud = uiGo.AddComponent<Hud>();
            Hud.Init();
            Names = uiGo.AddComponent<Nameplates>();
            Names.Init(Hud.Canvas);
            Emotes = uiGo.AddComponent<EmoteSystem>();
            Emotes.Init();

            string bridgeUrl = Cfg.bridge.url;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-bridge-url") bridgeUrl = args[i + 1];
            Bridge = new BridgeClient(bridgeUrl, Cfg.bridge.reconnectSec);
            gameObject.AddComponent<DevKeys>();
            AutoTest.MaybeStart(gameObject);
        }

        void Start()
        {
            StartPrep();
        }

        void OnApplicationQuit()
        {
            Persistence.Save();
            Bridge?.Dispose();
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float now = Time.time;
            GameConfig.HotReloadTick();
            PumpBridge();
            RefreshRankLooks();
            if (Paused) return;

            Roster.Tick(now, dt);
            Activity.Tick(now, dt);
            Fighters.Tick(dt, now);
            Ring.Tick(dt, now);
            foreach (var g in World.gates) g.Tick(now);
            RoundTick(now);
            UpdateGifterLooks();
            Persistence.Tick();
        }

        // ------------------------------------------------------------------ round flow
        void StartPrep()
        {
            RoundNo++;
            State = RoundState.Prep;
            StateStart = Time.time;
            Fighters.DespawnAll();
            Ring.Stop();
            peakAlive = 0;
            top10Awarded = false;
            TopGifter = null;
            WinnerText = null;
            lastDied = null;
            foreach (var v in Registry.All) { v.roundCoins = 0; v.roundKills = 0; }
            Cam.Frozen = false;
            Roster.BuildPrepList(Time.time);
        }

        void RoundTick(float now)
        {
            int alive = Fighters.AliveCount;
            peakAlive = Mathf.Max(peakAlive, alive);
            switch (State)
            {
                case RoundState.Prep:
                    if (now - StateStart >= Cfg.round.prepSec)
                    {
                        State = RoundState.Battle;
                        StateStart = now;
                        BattleStart = now;
                    }
                    break;
                case RoundState.Battle:
                {
                    float t = now - BattleStart;
                    bool entryClosed = t >= Cfg.roster.commentEntryWindowSec;
                    bool fewLeft = entryClosed && peakAlive > Cfg.round.finalAtAlive && alive <= Cfg.round.finalAtAlive;
                    bool lonely = t > 25f && alive <= 1 && Roster.PrepRemaining == 0 && Roster.QueueCount == 0 && !CanBotsFill();
                    if (t >= Cfg.round.finalAfterSec || fewLeft || lonely) StartFinal(now);
                    break;
                }
                case RoundState.Final:
                    if (!top10Awarded && alive <= 10) AwardTop10();
                    if (alive <= 1) StartWinner(now);
                    break;
                case RoundState.Winner:
                    if (now - lastConfettiAt > 1.5f && WinnerFighter != null) { lastConfettiAt = now; Fx.I.Confetti(WinnerFighter.Pos, 60); }
                    if (now - StateStart >= Cfg.round.winnerSec) StartPrep();
                    break;
            }
        }

        bool CanBotsFill()
        {
            Roster.Count(out int real, out int bots);
            return real + bots < Cfg.bots.fillWhenTotalBelow && real <= Cfg.bots.noNewBotsAboveReal;
        }

        void StartFinal(float now)
        {
            State = RoundState.Final;
            StateStart = now;
            Ring.Begin(now);
            if (Fighters.AliveCount <= 10) AwardTop10();
        }

        void AwardTop10()
        {
            top10Awarded = true;
            foreach (var f in Fighters.Active)
                if (f.IsAlive && f.viewer?.record != null) { f.viewer.record.points += Cfg.points.top10; }
            Persistence.MarkDirty();
            Board?.MarkDirty();
        }

        public Fighter WinnerFighter { get; private set; }

        void StartWinner(float now)
        {
            State = RoundState.Winner;
            StateStart = now;
            lastConfettiAt = now;
            Ring.Stop();
            Fighter w = null;
            foreach (var f in Fighters.Active) if (f.IsAlive) { w = f; break; }
            if (w == null) w = lastDied;
            WinnerFighter = w;
            if (w == null) { WinnerText = "DRAW"; WinnerName = "DRAW"; WinnerIsBot = false; WinnerSub = ""; return; }
            var v = w.viewer;
            WinnerName = v.DisplayName;
            WinnerIsBot = v.isBot;
            if (v.isBot)
            {
                WinnerText = "BOT WINS";
                WinnerSub = "";
            }
            else
            {
                v.record.wins++;
                v.record.points += Cfg.points.win;
                Persistence.MarkDirty();
                Persistence.Save();
                WinnerText = "WINNER";
                WinnerSub = "+" + Cfg.points.win + "   " + Ui.Thousands(v.record.points) + " PTS";
            }
            if (w.IsAlive) w.Victory();
            Fx.I.Confetti(w.Pos);
            Board?.MarkDirty();
        }

        // ------------------------------------------------------------------ events from systems
        public void OnFighterEntered(Fighter f)
        {
            f.nameVisibleUntil = Time.time + Cfg.names.showSec;
            Graves?.Remove(f.viewer);
        }

        public void OnFighterKilled(Fighter victim, Fighter killer, MoveKind move)
        {
            float now = Time.time;
            lastDied = victim;
            Roster.OnFighterDied(victim, now);
            if (!victim.IsBot) Graves?.Show(victim.viewer, victim.Pos, now);
            var vr = victim.viewer?.record;
            if (vr != null && !victim.IsBot) vr.deaths++;
            if (killer != null && killer != victim && killer.viewer != null && killer.viewer.record != null)
            {
                var kr = killer.viewer.record;
                killer.viewer.roundKills++;
                kr.kills++;
                kr.points += Cfg.points.kill;
                // killing the boss (big gifter) is worth more (user request)
                bool boss = victim.viewer != null && (victim.viewer == TopGifter || victim.viewer.roundCoins >= Cfg.points.bossMinRoundCoins);
                if (boss) kr.points += Cfg.points.bossKill;
                Persistence.MarkDirty();
                Board?.OnKill(killer.viewer, boss);
                if (killer.IsGifter || boss)
                    Hud?.FeedIcon(boss ? "crown" : "skull", boss ? $"{Ui.Short(killer.Name, 14)} KO BOSS!" : $"{Ui.Short(killer.Name, 12)} KO {Ui.Short(victim.Name, 12)}", boss ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.85f, 0.3f));
            }
        }

        public void OnGift(Viewer v, MoveKind move, int totalCoins)
        {
            // crown: the top gifter of this round (doc §4)
            if (TopGifter == null || v.roundCoins > TopGifter.roundCoins) TopGifter = v;
            Board?.MarkDirty();
            Cam?.OnGift(v.fighter, totalCoins);
        }

        void UpdateGifterLooks()
        {
            foreach (var f in Fighters.Active)
            {
                if (!f.IsAlive || f.state == FState.Victory) continue;
                f.SetGifterLook(f.IsGifter, f.viewer == TopGifter && TopGifter != null);
            }
        }

        static bool RanksBefore(ViewerRecord a, ViewerRecord b)
        {
            if (b == null) return true;
            if (a.points != b.points) return a.points > b.points;
            if (a.kills != b.kills) return a.kills > b.kills;
            return string.Compare(a.nickname, b.nickname, System.StringComparison.CurrentCulture) < 0;
        }

        void RefreshRankLooks()
        {
            if (Time.time < nextRankLook) return;
            nextRankLook = Time.time + 0.75f;
            ViewerRecord first = null, second = null, third = null;
            foreach (var r in ScoreRecords())
            {
                if (r.points <= 0 || string.IsNullOrEmpty(r.nickname)) continue;
                if (RanksBefore(r, first)) { third = second; second = first; first = r; }
                else if (RanksBefore(r, second)) { third = second; second = r; }
                else if (RanksBefore(r, third)) third = r;
            }
            foreach (var f in Fighters.Active)
            {
                if (!f.IsAlive) continue;
                var r = f.viewer?.record;
                Gear.SetRank(f, r != null && r == first ? 1 : r != null && r == second ? 2 : r != null && r == third ? 3 : 0);
            }
        }

        // ------------------------------------------------------------------ bridge + streamer commands
        readonly List<string> msgBuffer = new List<string>();

        void PumpBridge()
        {
            if (Bridge == null) return;
            int n = 0;
            while (n++ < 200 && Bridge.TryDequeue(out var msg))
            {
                try
                {
                    // bridge formats: {t:"evs", list:[event...]} batches, {t:"cmd", cmd, arg} commands, or a single event
                    var probe = JsonUtility.FromJson<BridgeMsg>(msg);
                    if (probe != null && !string.IsNullOrEmpty(probe.cmd)) { Command(probe.cmd, probe.arg); continue; }
                    if (probe != null && probe.list != null && probe.list.Length > 0) { foreach (var ev in probe.list) Router.Handle(ev); continue; }
                    var e = JsonUtility.FromJson<ViewerEvent>(msg);
                    if (e != null && !string.IsNullOrEmpty(e.type)) Router.Handle(e);
                }
                catch (System.Exception ex) { Debug.LogWarning("[Arena] Köprü mesajı okunamadı: " + ex.Message); }
            }
        }

        [System.Serializable] class BridgeMsg { public string t; public string cmd; public string arg; public ViewerEvent[] list; }

        /// <summary>Streamer panel commands (doc §10.2): pause, end round, reset scoreboard, delete all records, kick user.</summary>
        public void Command(string cmd, string arg)
        {
            switch (cmd)
            {
                case "pause": Paused = !Paused; break;
                case "endRound": if (State == RoundState.Battle || State == RoundState.Prep) StartFinal(Time.time); else if (State == RoundState.Final) ForceWinner(); break;
                case "resetScores": Persistence.ResetScores(); foreach (var r in Registry.BotRecords) { r.points = 0; r.kills = 0; r.wins = 0; } Board?.MarkDirty(); break;
                case "deleteAll": Persistence.DeleteAll(); Board?.MarkDirty(); break;
                case "kick":
                {
                    var v = Registry.Find(arg);
                    if (v != null) Roster.Kick(v);
                    break;
                }
                case "screenshot": ScreenCapture.CaptureScreenshot(arg); break;
            }
        }

        void ForceWinner()
        {
            // keep the healthiest one
            Fighter best = null;
            foreach (var f in Fighters.Active) if (f.IsAlive && (best == null || f.hp > best.hp)) best = f;
            foreach (var f in Fighters.Active) if (f.IsAlive && f != best) f.Die(null, MoveKind.None);
        }

        public IEnumerable<ViewerRecord> ScoreRecords()
        {
            foreach (var r in Persistence.All) yield return r;
            foreach (var r in Registry.BotRecords) yield return r;
        }
    }
}
