# Arena Dövüşü — notes for AI coding assistants (Claude, Codex/ChatGPT)

The user (Berk) writes Turkish and does not code: answer in Turkish, plain language, do the technical work yourself.

## What this is
TikTok LIVE survivor brawl in Unity 6.3 LTS (6000.3.28f1, URP). Viewers join as fighters via comment/like/gift;
fighters fight with utility AI; gifts trigger 7 special moves. Rules: `docs/Arena-Dovusu-Tasarim.pdf`
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
- `game/Assets/_Private/` (gitignored): licensed stickman pack + arena model (Resources/Stickman, Resources/ArenaModel).
- `bridge/` Node ESM (only dep `ws`), port 8788 (8787 is the football project's bridge). `src/tiktok.js` is a stub
  (Aşama 5: read the current tiktok-live-connector docs, do not guess field names; count gift streaks once).
- `tools/node/` portable Node (gitignored). `BASLAT.bat` starts bridge + panel + game.

## Commands (Git Bash)
```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.28f1/Editor/Unity.exe"
"$UNITY" -batchmode -quit -projectPath "C:/Projeler/TiktokArenaFight/game" -executeMethod Arena.EditorTools.BuildScript.SetupAndBuild -logFile _test-output/build.log
"$UNITY" -batchmode -quit -projectPath "C:/Projeler/TiktokArenaFight/game" -executeMethod Arena.EditorTools.BuildScript.BuildWindows -logFile _test-output/build.log
# visual checks (screenshots to the folder, then quits)
Builds/Windows/ArenaDovusu.exe -screen-width 608 -screen-height 1080 -screen-fullscreen 0 -autotest 60 -shots C:/Projeler/TiktokArenaFight/_test-output/auto
Builds/Windows/ArenaDovusu.exe -screen-width 608 -screen-height 1080 -screen-fullscreen 0 -gallery -shots C:/Projeler/TiktokArenaFight/_test-output/gallery
Builds/Windows/ArenaDovusu.exe -screen-width 608 -screen-height 1080 -screen-fullscreen 0 -gifts -shots C:/Projeler/TiktokArenaFight/_test-output/gifts
tools/node/node.exe bridge/src/server.js
curl -s -X POST localhost:8788/api/sim -d '{"action":"viewers","value":40}'
```
If Berk has the project open in the Unity editor, batch mode fails (project lock): ask him to close Unity, or read
`%LOCALAPPDATA%\Unity\Editor\Editor.log` for compile errors.

## Conventions
- C# 9 max. No per-frame allocations in hot paths (static lists, pools). Brains think every 0.2–0.4 s, staggered.
- Runtime materials are copies of `Resources/Materials/*` made by setup (shaders Arena/FxAdditive, Arena/FxAlpha);
  never toggle shader keywords at runtime.
- The stickman FBX bones are scaled x100: measure meshes via `Accessories.Measure`, not raw BakeMesh.
- Public repo: only CC0 / OFL / own work. Licensed files go to `game/Assets/_Private/`.
- Portrait 1080x1920: keep TikTok's top ~9 % and bottom-left comment area free of important UI.
