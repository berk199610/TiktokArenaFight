# Arena Dövüşü — notes for AI coding assistants (Claude, Codex/ChatGPT)

The user (Berk) writes Turkish and does not code: answer in Turkish, plain language, do the technical work yourself.

## What this is
TikTok LIVE survivor brawl in Unity 6.3 LTS (6000.3.28f1, URP). Viewers join as fighters via comment/like/gift;
fighters fight with utility AI; gifts trigger 8 special moves. Rules: `docs/Arena-Dovusu-Tasarim.pdf`
(text: `docs/DESIGN.md`). Decisions/ambiguities: `docs/TASARIM-NOTLARI.md`. Ask Berk before changing a game rule;
numbers live in `game/Assets/StreamingAssets/config.json` (hot-reloaded, no magic numbers in code).

## Layout
- `game/Assets/_Game/Scripts/` (asmdef `Arena.Game`, namespace `Arena`), folders follow DESIGN §10.3:
  Core (GameManager = only scene object, builds everything; GameConfig; EventRouter; MeshKit/Mats),
  Viewers (registry, roster rules, activity/intelligence, JSON persistence, bot names),
  Fighters (Fighter, Brain, Motor, Combat, Animation, Personality, CostumeCatalog, Accessories, FighterSystem pool),
  SpecialMoves (one class per gift move + SpecialMoveRunner), Gifts, World (ArenaWorld, SpawnGate, FireRing, SpatialGrid),
  CameraRig, UI (Hud, Nameplates, LeaderboardPanel = in-world scoreboard), Net (BridgeClient), Dev (DevKeys + TrafficSim + AutoTest), FX.
- `game/Assets/_Game/Editor/ArenaSetup.cs`: `Arena > Kurulum` — humanoid rigs, UAL clip import (loops, in-place,
  hit Animation Events), custom humanoid clips authored with HumanPoseHandler (Kick, SpinKick, Flex, Slam,
  TornadoSpin, Fall), Animator Controller (base layer + upper-body Hit layer), materials, Main scene, player settings.
  Re-run it after changing import/animator code. State names in `FighterAnimation` must match `CreateAnimator`.
- UI icons and emoji faces are painted from code (`UI/Icons.cs`); scoreboard = `UI/LeaderboardPanel.cs` (camera-facing, `Arena/UIOnTop` shader); hit reaction by force / poise in `Fighter.TakeHit`; gear levels in `Fighters/Gear.cs`; emotes in `UI/EmoteSystem.cs`; fighters on layer 8 with x-ray RenderObjects features (set up by `ArenaSetup.ConfigureRenderer`).
- `game/Assets/_Private/` (gitignored): licensed stickman pack + arena model (Resources/Stickman, Resources/ArenaModel).
- `bridge/` Node ESM (`ws` and `tiktok-live-connector`), port 8788 (8787 is the football project's bridge).
  Real TikTok events are normalized in `src/tiktok-events.js`; streak updates send only newly received quantities.
  `tools/node/node.exe --test bridge/test/tiktok-events.test.js bridge/test/bridge.test.js` validates the bridge.
- `tools/node/` portable Node (gitignored). `BASLAT.bat` starts bridge + panel + game.

## Commands (Git Bash)
```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.28f1/Editor/Unity.exe"
"$UNITY" -batchmode -quit -projectPath "C:/Projeler/TiktokArenaFight/game" -executeMethod Arena.EditorTools.BuildScript.SetupAndBuild -logFile _test-output/build.log
"$UNITY" -batchmode -quit -projectPath "C:/Projeler/TiktokArenaFight/game" -executeMethod Arena.EditorTools.BuildScript.BuildWindows -logFile _test-output/build.log
# visual checks (screenshots to the folder, then quits)
"Builds/Windows/Arena Dovusu.exe" -screen-width 608 -screen-height 1080 -screen-fullscreen 0 -autotest 60 -shots C:/Projeler/TiktokArenaFight/_test-output/auto
"Builds/Windows/Arena Dovusu.exe" -screen-width 608 -screen-height 1080 -screen-fullscreen 0 -gallery -shots C:/Projeler/TiktokArenaFight/_test-output/gallery
"Builds/Windows/Arena Dovusu.exe" -screen-width 608 -screen-height 1080 -screen-fullscreen 0 -gifts -shots C:/Projeler/TiktokArenaFight/_test-output/gifts
tools/node/node.exe bridge/src/server.js
curl -s -X POST localhost:8788/api/sim -d '{"action":"viewers","value":40}'
```
If Berk has the project open in the Unity editor, batch mode fails (project lock). Then work in the build copy
`C:\Projeler\TiktokArenaFight-build\game` (outside the repo): edit code in the main project, sync with
`robocopy <main>\game\Assets <build>\game\Assets /E /XO` (**/XO**, otherwise the main project's older generated
assets overwrite the freshly generated controller / renderer), run setup + build there, test the exe in
`TiktokArenaFight-build\Builds\Windows`, then copy generated assets back (`_Game/Resources/Anim`,
`_Game/Art/Animations/Custom`, `_Game/Resources/Materials`, `_Private/Resources/Materials`, `Settings`) and the build to
`Builds\Windows`. `-autotest` logs attacks / hits / brain decisions every 5 s: if `applies` stays 0 the brains are starved.

Shader references must match the main project's shader `.meta` GUIDs after copying generated materials back.
Mismatched `M_XRay` references previously produced magenta boots despite valid model materials. Use
`Arena.EditorTools.BuildScript.RepairMaterialsAndBuild` to rebind these references without rebuilding the scene.
Runtime regression: `-review -test-data <isolated-folder> -bridge-url ws://127.0.0.1:<test-port>/game`.
Wait for the review fixture and socket before injecting the `BridgeReview` 30-coin simulator gift.
The latest user decisions in `docs/TASARIM-NOTLARI.md` supersede older PDF rules (plain starter, TOP 5,
physical signs, sixty-second death cooldown). Always isolate automated tests from real viewer records.

While a stream is running the live game blocks a second copy (single instance) and must not be closed. Build a
side-by-side copy with `-executeMethod Arena.EditorTools.BuildScript.BuildWindows -testBuild -buildPath <dir>/ArenaTest.exe`
(own product name, no single-instance lock), point its `StreamingAssets/config.json` bridge url to an unused port and run
it with `-test-data <dir>` so the real viewer records are untouched. Release: `-buildPath "<dir>/Arena Dovusu.exe"`, then
copy to `Builds/Windows` and to the desktop folder `OneDrive/Desktop/TiktokLive/TikTokLiveArenaFight` (only the game files).
Gear added at runtime is attached with the idle-pose bone data from `Accessories.CaptureRest` (otherwise it floats).

## Conventions
- C# 9 max. No per-frame allocations in hot paths (static lists, pools). Brains think every 0.2–0.4 s, staggered.
- Runtime materials are copies of `Resources/Materials/*` made by setup (shaders Arena/FxAdditive, Arena/FxAlpha);
  never toggle shader keywords at runtime.
- The stickman FBX bones are scaled x100: measure meshes via `Accessories.Measure`, not raw BakeMesh.
- Public repo: only CC0 / OFL / own work. Licensed files go to `game/Assets/_Private/`.
- Portrait 1080x1920: keep TikTok's top ~9 % and bottom-left comment area free of important UI.
