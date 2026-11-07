using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arena
{
    /// <summary>
    /// Three small, physical placards in the rear spectator area. Symbols explain the rules without sentences:
    /// heart +1 / comment +5, and a 100+ coin gift fills health / any gift brings a fighter back.
    /// Their positions never follow the camera; only the sign faces rotate.
    /// </summary>
    public class StadiumSigns : MonoBehaviour
    {
        const float W = 340f, H = 300f, WidthMetres = 2.1f;
        readonly List<Transform> faces = new List<Transform>(3);
        Text likeValue, commentValue, fullCoins;
        readonly Text[] tierValues = new Text[8];
        readonly Image[] tierCoins = new Image[8], tierMoves = new Image[8];
        readonly RectTransform[] tierCells = new RectTransform[8];

        public static StadiumSigns Create(Transform parent)
        {
            var go = new GameObject("StadiumSigns");
            go.transform.SetParent(parent, false);
            var signs = go.AddComponent<StadiumSigns>();
            signs.Build();
            return signs;
        }

        void Build()
        {
            var world = GameManager.I.World;
            var bc = GameConfig.Current.board;
            // The faces sit just ahead of the stone/cloth frontage, with a small gap either side of TOP 5.
            var back = world.CamForwardFlat * (GameConfig.Current.arena.radius + bc.distanceBehind - 0.1f);
            var left = Face("LikeCommentPlacard", back - world.CamRightFlat * 3.9f + Vector3.up * bc.height,
                new Color(0.95f, 0.35f, 0.4f));
            Icon(left, "Heart", "heart", -91, 70, 102);
            likeValue = Label(left, "LikeHp", "", 69, 70, 77, 144, 96);
            Icon(left, "Comment", "comment", -91, -67, 102);
            commentValue = Label(left, "CommentHp", "", 69, -67, 77, 144, 96);
            Divider(left);

            // The hanging Flags mesh projects well inside the nominal wall radius. Place this face on a
            // longer fixed terrace bracket: its whole rectangle clears the cloth across camera movement.
            // Checked against the actual model meshes at 44 camera poses (25 points per face).
            var giftAnchor = world.CamForwardFlat * 8.3f + world.CamRightFlat * 3.9f + Vector3.up * bc.height;
            var right = Face("GiftPlacard", giftAnchor, new Color(0.95f, 0.76f, 0.28f), worldWidth: 1.9f);
            Icon(right, "GiftHealth", "gift", -112, 65, 78);
            Label(right, "HealthArrow", ">", -28, 66, 57, 50, 75);
            Icon(right, "HealthHeart", "heart", 61, 77, 64);
            var bar = Ui.Image(right, "FullHealthBar", Ui.Rounded, new Color(0.18f, 0.85f, 0.42f));
            Place(bar.rectTransform, 70, 29, 118, 23);
            var coin = Ui.Image(right, "FullHealthCoin", Icons.Get("coin"), Color.white);
            Place(coin.rectTransform, -32, 120, 28, 28);
            fullCoins = Label(right, "FullHealthThreshold", "", 44, 120, 36, 116, 42);
            Divider(right);
            Icon(right, "GiftRevive", "gift", -112, -80, 78);
            Label(right, "ReviveArrow", ">", -28, -80, 57, 50, 75);
            Icon(right, "FighterReturns", "person", 69, -77, 81);
            var rise = Ui.Image(right, "InstantReturn", Icons.Get("arrowUp"), new Color(0.3f, 1f, 0.6f));
            Place(rise.rectTransform, 126, -77, 41, 54);

            BuildFollowBanner(world);
            UpdateValues();
            GameConfig.Reloaded += UpdateValues;
        }

        /// <summary>
        /// "FOLLOW · NEW GAME EVERY DAY" strip sitting on top of the TOP 5 board (the back of the arena), with the
        /// follow icon so it reads without English.
        /// </summary>
        void BuildFollowBanner(ArenaWorld world)
        {
            var board = GameManager.I != null ? GameManager.I.Board : null;
            if (board == null) return;
            var strip = Ui.Rect(board.transform, "FollowBanner");
            strip.anchorMin = strip.anchorMax = new Vector2(0.5f, 1f);
            strip.pivot = new Vector2(0.5f, 0f);
            strip.anchoredPosition = new Vector2(0, 16);
            strip.sizeDelta = new Vector2(700, 84);
            var rim = Ui.Image(strip, "Rim", Ui.Rounded, new Color(1f, 0.8f, 0.25f));
            Ui.Stretch(rim.rectTransform);
            var bg = Ui.Image(strip, "Bg", Ui.Rounded, new Color(0.85f, 0.12f, 0.35f));
            Ui.Stretch(bg.rectTransform, 4);
            var icon = Ui.Image(strip, "FollowIcon", Icons.Get("follow"), Color.white);
            Place(icon.rectTransform, -300, 0, 70, 70);
            var big = Ui.Text(strip, "Follow", "FOLLOW", 52, Color.white, TextAnchor.MiddleLeft, Ui.FontBlack);
            Place(big.rectTransform, -150, 0, 200, 80);
            var small = Ui.Text(strip, "Daily", "NEW GAME EVERY DAY", 34, new Color(1f, 0.92f, 0.6f), TextAnchor.MiddleLeft, Ui.FontBlack);
            Place(small.rectTransform, 105, 0, 330, 80);
            followStrip = strip;
        }

        RectTransform followStrip;

        void BuildGiftMoves(ArenaWorld world)
        {
            // A separate corner fixture becomes visible as the camera follows a fight to this side.
            // Keeping it in world space avoids a permanent eight-row legend over the fighters.
            var pos = world.CamForwardFlat * 10f - world.CamRightFlat * 6.4f + Vector3.up * 1.3f;
            var guide = Face("GiftMovesPlacard", pos, new Color(0.4f, 0.75f, 1f), 450, 420, 2.4f);
            Icon(guide, "GiftHeader", "gift", -24, 176, 48);
            Icon(guide, "MovesHeader", "fist", 30, 176, 48);
            var headerLine = Ui.Image(guide, "HeaderLine", null, new Color(1f, 1f, 1f, 0.14f));
            Place(headerLine.rectTransform, 0, 143, 414, 2);
            for (int i = 0; i < tierCells.Length; i++)
            {
                // Read down each column: 1, 5, 30, 100 / 300, 1K, 2.5K, 5K.
                int col = i / 4, row = i % 4;
                var cell = Ui.Rect(guide, "GiftTier" + i);
                Place(cell, col == 0 ? -109 : 109, 100 - row * 84, 218, 76);
                tierCells[i] = cell;
                tierCoins[i] = Ui.Image(cell, "Coin", Icons.Get("coin"), Color.white);
                tierCoins[i].preserveAspect = true;
                Place(tierCoins[i].rectTransform, -83, 0, 31, 31);
                tierValues[i] = Label(cell, "Amount", "", -31, 0, 43, 76, 66);
                tierValues[i].color = new Color(1f, 0.85f, 0.35f);
                Label(cell, "Arrow", ">", 20, 0, 36, 28, 60);
                tierMoves[i] = Ui.Image(cell, "Move", Icons.Get("fist"), Color.white);
                Place(tierMoves[i].rectTransform, 69, 0, 65, 65);
            }
            var columnLine = Ui.Image(guide, "ColumnLine", null, new Color(1f, 1f, 1f, 0.1f));
            Place(columnLine.rectTransform, 0, -28, 2, 320);
        }

        RectTransform Face(string name, Vector3 position, Color accent, float width = W, float height = H, float worldWidth = WidthMetres)
        {
            var rt = Ui.Rect(transform, name);
            var canvas = rt.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 0;
            rt.sizeDelta = new Vector2(width, height);
            rt.position = position;
            rt.localScale = Vector3.one * (worldWidth / width);
            BuildFrame(rt, width, height, accent);
            var world = GameManager.I.World;
            BuildStand(transform, position, world.CamForwardFlat, world.CamRightFlat, worldWidth);

            var bg = Ui.Image(rt, "SignFace", Ui.Rounded, new Color(0.045f, 0.06f, 0.075f));
            Ui.Stretch(bg.rectTransform);
            var rim = Ui.Image(rt, "Accent", null, accent);
            Place(rim.rectTransform, 0, height * 0.5f - 8, width - 12, 8);
            faces.Add(rt);
            return rt;
        }

        /// <summary>A shallow metal enclosure and side rails make the world canvas an actual stadium fixture.</summary>
        public static void BuildFrame(Transform face, float width, float height, Color accent)
        {
            var metal = Mats.Lit(new Color(0.1f, 0.12f, 0.14f));
            var edge = Mats.Metal(Color.Lerp(new Color(0.3f, 0.3f, 0.28f), accent, 0.2f));
            MeshKit.Part("MetalEnclosure", MeshKit.Cube, metal, face,
                new Vector3(0, 0, 21), Vector3.zero, new Vector3(width + 24, height + 24, 32));
            MeshKit.Part("LeftRail", MeshKit.Cube, edge, face,
                new Vector3(-width * 0.5f - 7, 0, 3), Vector3.zero, new Vector3(12, height + 18, 24));
            MeshKit.Part("RightRail", MeshKit.Cube, edge, face,
                new Vector3(width * 0.5f + 7, 0, 3), Vector3.zero, new Vector3(12, height + 18, 24));
            MeshKit.Part("TopRail", MeshKit.Cube, edge, face,
                new Vector3(0, height * 0.5f + 7, 3), Vector3.zero, new Vector3(width + 24, 12, 24));
            MeshKit.Part("BottomRail", MeshKit.Cube, edge, face,
                new Vector3(0, -height * 0.5f - 7, 3), Vector3.zero, new Vector3(width + 24, 12, 24));
        }

        /// <summary>Upright fixed supports touch the arena floor while the face is free to turn.</summary>
        public static void BuildStand(Transform parent, Vector3 center, Vector3 forward, Vector3 right, float width)
        {
            var mount = new GameObject("StadiumFixtureStand").transform;
            mount.SetParent(parent, false);
            var metal = Mats.Lit(new Color(0.11f, 0.12f, 0.13f));
            float height = Mathf.Max(0.3f, center.y);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 basePoint = center.WithY(0) + right * (i * width * 0.32f) + forward * 0.38f;
                MeshKit.Part("Upright" + i, MeshKit.Cube, metal, mount,
                    basePoint + Vector3.up * (height * 0.5f), Vector3.zero, new Vector3(0.09f, height, 0.09f));
                MeshKit.Part("GroundFoot" + i, MeshKit.Cube, metal, mount,
                    basePoint + Vector3.up * 0.045f, new Vector3(0, GameConfig.Current.camera.yawDeg, 0),
                    new Vector3(0.34f, 0.09f, 0.48f));
            }
            MeshKit.Part("BackBrace", MeshKit.Cube, metal, mount,
                center + forward * 0.38f, new Vector3(0, GameConfig.Current.camera.yawDeg, 0),
                new Vector3(width * 0.72f, 0.1f, 0.1f));
            // A rear arm connects the fixed stand to the stone terrace, rather than leaving it floating.
            var flatCenter = center.WithY(0);
            var stonePoint = flatCenter.normalized * (GameConfig.Current.arena.radius + 0.7f) + Vector3.up * center.y;
            var start = center + forward * 0.38f;
            var arm = stonePoint - start;
            if (arm.sqrMagnitude > 0.01f)
                MeshKit.Part("StoneMountingArm", MeshKit.Cube, metal, mount, (start + stonePoint) * 0.5f,
                    Quaternion.LookRotation(arm, Vector3.up).eulerAngles, new Vector3(0.11f, 0.11f, arm.magnitude));
        }

        static void Icon(Transform parent, string name, string icon, float x, float y, float size)
        {
            var img = Ui.Image(parent, name, Icons.Get(icon), Color.white);
            Place(img.rectTransform, x, y, size, size);
        }

        static Text Label(Transform parent, string name, string value, float x, float y, int size, float width, float height)
        {
            var label = Ui.Text(parent, name, value, size, Color.white, TextAnchor.MiddleCenter, Ui.FontBlack, false);
            Place(label.rectTransform, x, y, width, height);
            return label;
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        static void Divider(Transform parent)
        {
            var line = Ui.Image(parent, "Divider", null, new Color(1f, 1f, 1f, 0.14f));
            Place(line.rectTransform, 0, 0, W - 36, 2);
        }

        void UpdateValues()
        {
            var hc = GameConfig.Current.health;
            likeValue.text = "+" + hc.likeHp.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            commentValue.text = "+" + hc.commentHp.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            fullCoins.text = hc.giftFullHpCoins + "+";
            var tiers = GameConfig.Current.gifts.tiers;
            for (int i = 0; i < tierCells.Length; i++)
            {
                if (tierCells[i] == null) continue;
                bool visible = tiers != null && i < tiers.Length;
                tierCells[i].gameObject.SetActive(visible);
                if (!visible) continue;
                int coins = tiers[i].minCoins;
                tierValues[i].text = coins >= 1000
                    ? (coins / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "K"
                    : coins.ToString();
                var picture = GiftResolver.Picture(tiers[i].icon);
                tierCoins[i].sprite = picture != null ? picture : Icons.Get("coin");
                System.Enum.TryParse(tiers[i].move, out MoveKind move);
                tierMoves[i].sprite = Icons.ForMove(move);
            }
        }

        void LateUpdate()
        {
            var cam = GameManager.I != null ? GameManager.I.Cam : null;
            if (cam == null) return;
            foreach (var face in faces)
                face.rotation = Quaternion.LookRotation(face.position - cam.transform.position, Vector3.up);
            if (followStrip != null) followStrip.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 2f) * 0.025f);
        }

        void OnDestroy() => GameConfig.Reloaded -= UpdateValues;
    }
}
