using System.Collections;
using System.IO;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Command-line test runs of the built game (no clicking needed):
    ///   -autotest 60 -shots C:/out      crowd traffic for 60 s, a screenshot every 6 s, FPS in the log, then quit
    ///   -gallery -shots C:/out          all costumes in a row, each animation once, screenshots, then quit
    ///   -gifts -shots C:/out            every gift move one after another on one character, screenshots
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        string dir;
        float seconds = 60f;
        string mode;
        int failures;

        public static void MaybeStart(GameObject host)
        {
            var args = System.Environment.GetCommandLineArgs();
            string mode = null, dir = null; float secs = 60f;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autotest") { mode = "auto"; if (i + 1 < args.Length && float.TryParse(args[i + 1], out var s)) secs = s; }
                if (args[i] == "-gallery") mode = "gallery";
                if (args[i] == "-gifts") mode = "gifts";
                if (args[i] == "-review") mode = "review";
                if (args[i] == "-shots" && i + 1 < args.Length) dir = args[i + 1];
            }
            if (mode == null) return;
            var t = host.AddComponent<AutoTest>();
            t.mode = mode; t.seconds = secs; t.dir = dir ?? Path.Combine(Application.persistentDataPath, "shots");
            Directory.CreateDirectory(t.dir);
        }

        IEnumerator Start()
        {
            yield return null;
            if (mode == "gallery") yield return Gallery();
            else if (mode == "gifts") yield return Gifts();
            else if (mode == "review") yield return Review();
            else yield return Auto();
            Application.Quit(failures > 0 ? 1 : 0);
        }

        void Shot(string name)
        {
            var p = Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(p);
            Debug.Log("[AutoTest] shot " + p);
        }

        void Check(bool pass, string description)
        {
            if (pass) Debug.Log("[Review] PASS " + description);
            else { failures++; Debug.LogError("[Review] FAIL " + description); }
        }

        Viewer Event(string id, string nickname, string type, int coins = 0, int count = 1)
        {
            GameManager.I.Router.Handle(new ViewerEvent { type = type, userId = "review:" + id, nickname = nickname, coins = coins, count = count, giftName = coins > 0 ? "Review" : null });
            return GameManager.I.Registry.Find("review:" + id);
        }

        void Pose(Viewer v, float x, float z)
        {
            var gm = GameManager.I;
            var p = gm.World.CamRightFlat * x + gm.World.CamForwardFlat * z;
            var f = v.fighter;
            f.state = FState.Idle;
            f.motor.Place(p, Quaternion.LookRotation(-gm.World.CamForwardFlat).eulerAngles.y);
            f.anim.Play(FighterAnimation.Loco, 0.05f);
            f.nameVisibleUntil = Time.time + 999;
        }

        // Deterministic player-level regression test, isolated with -test-data and -bridge-url.
        IEnumerator Review()
        {
            var gm = GameManager.I;
            var cfg = GameConfig.Current;
            cfg.round.prepSec = 0;
            cfg.bots.fillWhenTotalBelow = 0;
            DevKeys.I.traffic = false;
            yield return null;
            gm.Paused = true;
            gm.Fighters.DespawnAll();
            var plain = Event("plain", "Starter", "comment");
            Pose(plain, -2.8f, -1.4f);
            Check(plain.fighter != null && Gear.SupportLevel(plain) == 0 && Gear.GiftLevel(plain) == 0 && plain.fighter.gearParts.Count == 0, "newcomer starts plain");
            var support = Event("support", "Supporter", "like");
            for (int i = 0; i < cfg.levels.commentsL2; i++) Event("support", "Supporter", "comment");
            Pose(support, -0.9f, -1.4f);
            Check(Gear.SupportLevel(support) == 2 && support.fighter.gearSupport == 2, "comments unlock supporter outfit");
            var hero = Event("hero", "Hero", "comment");
            for (int i = 1; i < cfg.levels.commentsL3; i++) Event("hero", "Hero", "comment");
            Pose(hero, 0.9f, -1.4f);
            for (int i = 0; i < cfg.levels.giftCoins.Length; i++)
            {
                var v = Event("gift" + i, "Gift " + (i + 1), "gift", cfg.levels.giftCoins[i]);
                Pose(v, (i - 2) * 1.65f, 2.4f);
                Check(Gear.GiftLevel(v) == i + 1 && v.fighter.gearGift == i + 1, "gift outfit stage " + (i + 1));
                v.record.points = (i + 1) * 10;
            }
            gm.Board.MarkDirty();
            yield return new WaitForSeconds(3);
            Shot("review_00_progression");
            int unsupported = 0;
            foreach (var f in gm.Fighters.Active)
                foreach (var r in f.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m == null || m.shader == null || !m.shader.isSupported || m.shader.name.Contains("InternalError")) unsupported++;
            Check(unsupported == 0, "all model and gear material shaders supported");
            Vector3 boardPos = gm.Board.transform.position;
            yield return new WaitForSeconds(3);
            Check(Vector3.Distance(boardPos, gm.Board.transform.position) < 0.001f, "scoreboard stays fixed while camera moves");

            var fast = Event("fast", "Gift Return", "comment");
            Pose(fast, 2.7f, -1.4f);
            fast.fighter.Die(null, MoveKind.None);
            Event("fast", "Gift Return", "comment");
            Check(fast.state == ViewerState.Dead, "comment cannot bypass death cooldown");
            Check(Mathf.Abs(fast.deadUntil - fast.diedAt - 60) < 0.1f, "death cooldown is sixty seconds");
            yield return new WaitForSeconds(2.5f);
            if (fast.fighter != null) fast.fighter.Tick(0.01f, Time.time);
            yield return new WaitForSeconds(1);
            Shot("review_01_gravestone");
            Check(gm.Graves.Count == 1, "death places a gravestone");
            Event("fast", "Gift Return", "gift", 1);
            Check(fast.fighter != null && fast.fighter.IsAlive && gm.Graves.Count == 0, "gift revives immediately and removes stone");
            Check(fast.fighter.specials.Busy || fast.fighter.specials.Queued > 0, "revival keeps the gift move");
            Pose(fast, 2.7f, -1.4f);
            var waiting = Event("wait", "Free Return", "comment");
            Pose(waiting, 0, 0.2f);
            waiting.fighter.Die(null, MoveKind.None);
            float until = waiting.deadUntil;
            yield return new WaitForSeconds(3);
            if (waiting.fighter != null) waiting.fighter.Tick(0.01f, Time.time);
            yield return new WaitForSeconds(1);
            Shot("review_02_return_bar");
            while (Time.time < until - 0.1f)
            {
                gm.Roster.Tick(Time.time, Time.deltaTime);
                yield return null;
            }
            Check(waiting.fighter == null || !waiting.fighter.IsAlive, "free return cannot happen before sixty seconds");
            yield return new WaitForSeconds(0.2f);
            gm.Roster.Tick(Time.time, 0.02f);
            Check(waiting.fighter != null && waiting.fighter.IsAlive && gm.Graves.Count == 0, "free player automatically returns after sixty seconds");
            Shot("review_03_free_return");
            var external = gm.Registry.Find("manual:BridgeReview");
            Check(gm.Bridge.Connected && external != null && external.streamCoins == 30 && external.fighter != null && external.fighter.IsAlive, "HTTP bridge gift reaches real Unity player");
            waiting.fighter.Die(null, MoveKind.None);
            float nextReturn = waiting.deadUntil;
            gm.Command("endRound", null);
            var finalGift = Event("final", "Final Gift", "gift", 30);
            Check(finalGift.pendingGifts.Count == 1 && (finalGift.fighter == null || !finalGift.fighter.IsAlive), "final phase defers gift without losing it");
            gm.Command("endRound", null);
            cfg.round.winnerSec = 0.01f;
            gm.Paused = false;
            yield return new WaitForSeconds(3);
            Check(waiting.state == ViewerState.Dead && waiting.deadUntil == nextReturn && (waiting.fighter == null || !waiting.fighter.IsAlive), "new round preserves unfinished death cooldown");
            Check(finalGift.fighter != null && finalGift.fighter.IsAlive && finalGift.pendingGifts.Count == 0, "deferred gift enters in next round");
            Debug.Log("[Review] completed failures=" + failures);
            yield return new WaitForSeconds(1);
        }

        IEnumerator Auto()
        {
            DevKeys.I.traffic = true;
            bool final = System.Environment.CommandLine.Contains("-final");
            bool finalDone = false;
            float start = Time.time, nextShot = start + 4f, nextLog = start + 5f;
            int frames = 0; float worst = 0f; int shot = 0;
            while (Time.time - start < seconds)
            {
                frames++;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                if (final && !finalDone && Time.time - start > 22f) { finalDone = true; GameManager.I.Command("endRound", null); }
                if (Time.time >= nextShot) { nextShot += 6f; Shot($"auto_{shot++:00}"); }
                if (Time.time >= nextLog)
                {
                    Debug.Log($"[AutoTest] t={Time.time - start:0} fps={frames / 5f:0.0} worstMs={worst * 1000f:0} alive={GameManager.I.Fighters.AliveCount} state={GameManager.I.State} attacks={FighterCombat.AttacksStarted} hits={FighterCombat.HitsLanded} thinks={FighterBrain.Thinks} decides={FighterBrain.Decides} applies={FighterBrain.Applies} rejected={FighterBrain.Rejected}");
                    if (GameManager.I.Fighters.Active.Count > 0) { var f0 = GameManager.I.Fighters.Active[0]; Debug.Log($"[AutoTest] f0 {f0.Name} st={f0.state} plan={f0.brain.plan} intel={f0.intelligence:0.00} hp={f0.hp:0} target={(f0.brain.target != null ? f0.brain.target.Name : "-")}"); }
                    var states = new System.Collections.Generic.Dictionary<string, int>();
                    foreach (var f in GameManager.I.Fighters.Active)
                    {
                        string k = f.state + "/" + f.brain.plan;
                        states[k] = states.TryGetValue(k, out int c) ? c + 1 : 1;
                    }
                    var sb = new System.Text.StringBuilder("[AutoTest] states:");
                    foreach (var kv in states) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
                    Debug.Log(sb.ToString());
                    frames = 0; worst = 0f; nextLog += 5f;
                }
                yield return null;
            }
        }

        IEnumerator Gallery()
        {
            var gm = GameManager.I;
            gm.Paused = true;
            gm.Fighters.DespawnAll();
            gm.Hud.Canvas.enabled = false;
            yield return null;
            int n = CostumeCatalog.Count;
            var fighters = new Fighter[n];
            var right = gm.World.CamRightFlat;
            var fwd = gm.World.CamForwardFlat;
            for (int i = 0; i < n; i++)
            {
                var v = gm.Registry.GetOrCreate("gallery:" + i, CostumeCatalog.All[i].name, null);
                v.costumeId = i;
                int row = i / 5, col = i % 5;
                var pos = right * ((col - 2) * 1.6f) + fwd * (row * 2.2f - 2f);
                var f = gm.Fighters.Spawn(v, pos, pos - fwd, Time.time);
                f.state = FState.Victory;
                f.motor.Place(pos, Quaternion.LookRotation(-fwd).eulerAngles.y);
                f.nameVisibleUntil = Time.time + 999f;
                fighters[i] = f;
            }
            gm.Cam.Frozen = true;
            yield return new WaitForSeconds(0.5f);
            // overview of the whole arena from high above
            var c = GameConfig.Current.camera;
            float oldMin = c.minDistance, oldMax = c.maxDistance, oldPitch = c.pitchDeg;
            gm.Cam.Frozen = false; c.minDistance = c.maxDistance = 55f; c.pitchDeg = 60f;
            yield return new WaitForSeconds(2.5f);
            Shot("gallery_overview");
            c.minDistance = oldMin; c.maxDistance = oldMax; c.pitchDeg = oldPitch;
            yield return new WaitForSeconds(2.5f);
            gm.Cam.Frozen = true;
            int[] states = { FighterAnimation.Loco, FighterAnimation.Punch1, FighterAnimation.Kick, FighterAnimation.StaggerL, FighterAnimation.StaggerR, FighterAnimation.FallL, FighterAnimation.FallR, FighterAnimation.Fall, FighterAnimation.GetUp, FighterAnimation.DeathFwd, FighterAnimation.DeathSide, FighterAnimation.DeathKnees, FighterAnimation.Death, FighterAnimation.Cocky, FighterAnimation.Tornado };
            string[] names = { "idle", "punch", "kick", "staggerL", "staggerR", "fallL", "fallR", "fall", "getup", "deathFwd", "deathSide", "deathKnees", "death", "cocky", "tornado" };
            for (int s = 0; s < states.Length; s++)
            {
                foreach (var f in fighters) f.anim.Play(states[s], 0.05f);
                int st = states[s];
                float d = st == FighterAnimation.Kick ? 0.36f : (st == FighterAnimation.StaggerL || st == FighterAnimation.StaggerR) ? 0.3f : (st == FighterAnimation.Fall || st == FighterAnimation.FallL || st == FighterAnimation.FallR) ? 0.55f : (st == FighterAnimation.Death || st == FighterAnimation.DeathFwd || st == FighterAnimation.DeathSide || st == FighterAnimation.DeathKnees) ? 1.6f : 0.4f;
                yield return new WaitForSeconds(d);
                Shot("gallery_" + s.ToString("00") + "_" + names[s]);
                yield return new WaitForSeconds(0.25f);
            }
        }

        IEnumerator Gifts()
        {
            var gm = GameManager.I;
            DevKeys.I.NewViewers(14);
            yield return new WaitForSeconds(gm.State == RoundState.Prep ? 12f : 3f);
            var tiers = GameConfig.Current.gifts.tiers;
            var ev = new ViewerEvent { type = "comment", userId = "dev:berk", nickname = "Berk" };
            gm.Router.Handle(ev);
            yield return new WaitForSeconds(3f);
            for (int i = 0; i < tiers.Length; i++)
            {
                gm.Router.Handle(new ViewerEvent { type = "gift", userId = "dev:berk", nickname = "Berk", coins = tiers[i].minCoins, count = 1, giftName = "Test" });
                // wait until the move really runs (one move at a time; the previous one may still be active)
                float waitUntil = Time.time + 40f;
                Fighter f = null;
                while (Time.time < waitUntil)
                {
                    var v = gm.Registry.Find("dev:berk");
                    f = v?.fighter;
                    if (f != null && tiers[i].move == "Giant" && f.specials.Giant != null) break;
                    if (f != null && f.specials.Current != null && f.specials.Current.kind.ToString() == tiers[i].move) break;
                    yield return null;
                }
                Check(f != null && (tiers[i].move == "Giant" ? f.specials.Giant != null : f.specials.Current != null && f.specials.Current.kind.ToString() == tiers[i].move), "gift move runs " + tiers[i].move);
                for (int k = 0; k < 4; k++)
                {
                    yield return new WaitForSeconds(k == 0 ? 0.35f : 0.7f);
                    Shot($"gift_{i}_{tiers[i].move}_{k}");
                }
                // end long buffs early so the next move can start (test only)
                if (f != null && f.specials.Current != null && (i == 1 || i == 2 || i == 5)) f.specials.Current.endsAt = Time.time + 0.5f;
                yield return new WaitForSeconds(1.5f);
            }
        }
    }
}
