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

        public static void MaybeStart(GameObject host)
        {
            var args = System.Environment.GetCommandLineArgs();
            string mode = null, dir = null; float secs = 60f;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autotest") { mode = "auto"; if (i + 1 < args.Length && float.TryParse(args[i + 1], out var s)) secs = s; }
                if (args[i] == "-gallery") mode = "gallery";
                if (args[i] == "-gifts") mode = "gifts";
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
            else yield return Auto();
            Application.Quit();
        }

        void Shot(string name)
        {
            var p = Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(p);
            Debug.Log("[AutoTest] shot " + p);
        }

        IEnumerator Auto()
        {
            DevKeys.I.traffic = true;
            float start = Time.time, nextShot = start + 4f, nextLog = start + 5f;
            int frames = 0; float worst = 0f; int shot = 0;
            while (Time.time - start < seconds)
            {
                frames++;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                if (Time.time >= nextShot) { nextShot += 6f; Shot($"auto_{shot++:00}"); }
                if (Time.time >= nextLog)
                {
                    Debug.Log($"[AutoTest] t={Time.time - start:0} fps={frames / 5f:0.0} worstMs={worst * 1000f:0} alive={GameManager.I.Fighters.AliveCount} state={GameManager.I.State}");
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
            int[] states = { FighterAnimation.Loco, FighterAnimation.Punch1, FighterAnimation.Kick, FighterAnimation.SpinKick, FighterAnimation.Flex, FighterAnimation.Slam, FighterAnimation.HitBig, FighterAnimation.Fall, FighterAnimation.Lying, FighterAnimation.GetUp, FighterAnimation.Dizzy, FighterAnimation.Dance, FighterAnimation.Tornado, FighterAnimation.Bat, FighterAnimation.Death };
            string[] names = { "idle", "punch", "kick", "spinkick", "flex", "slam", "hitbig", "fall", "lying", "getup", "dizzy", "dance", "tornado", "bat", "death" };
            for (int s = 0; s < states.Length; s++)
            {
                foreach (var f in fighters) f.anim.Play(states[s], 0.05f);
                float d = states[s] == FighterAnimation.Kick ? 0.36f : states[s] == FighterAnimation.Slam ? 0.6f : states[s] == FighterAnimation.Fall ? 0.55f : states[s] == FighterAnimation.Death ? 1.5f : 0.4f;
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
                    if (f != null && f.specials.Current != null && f.specials.Current.kind.ToString() == tiers[i].move) break;
                    yield return null;
                }
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
