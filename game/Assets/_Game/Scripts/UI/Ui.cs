using UnityEngine;
using UnityEngine.UI;

namespace Arena
{
    /// <summary>Tiny uGUI toolkit: procedural sprites, fonts (Barlow Condensed, OFL) and element factories.</summary>
    public static class Ui
    {
        static Sprite rounded, circle, soft, coin, plus;
        static Font fontBlack, fontBold;

        public static Font FontBlack => fontBlack != null ? fontBlack : (fontBlack = LoadFont("BarlowCondensed-ExtraBold"));
        public static Font FontBold => fontBold != null ? fontBold : (fontBold = LoadFont("BarlowCondensed-Bold"));

        static Font LoadFont(string name)
        {
            var f = Resources.Load<Font>("Fonts/" + name);
            if (f == null) f = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 32);
            return f;
        }

        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(64, 22));
        public static Sprite Circle => circle != null ? circle : (circle = Make(64, (x, y, r) => Mathf.Clamp01(r - Vector2.Distance(new Vector2(x, y), new Vector2(r, r)))));
        public static Sprite Soft => soft != null ? soft : (soft = Make(64, (x, y, r) => { float d = Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) / r; return Mathf.Clamp01(1 - d * d); }));
        public static Sprite Coin => coin != null ? coin : (coin = MakeCoin(64));
        public static Sprite Plus => plus != null ? plus : (plus = Make(64, (x, y, r) =>
        {
            float ax = Mathf.Abs(x - r), ay = Mathf.Abs(y - r);
            bool on = (ax < r * 0.22f && ay < r * 0.78f) || (ay < r * 0.22f && ax < r * 0.78f);
            return on ? 1f : 0f;
        }));

        static Sprite Make(int size, System.Func<float, float, float, float> alpha)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    t.SetPixel(x, y, new Color(1, 1, 1, alpha(x + 0.5f, y + 0.5f, r)));
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        static Sprite MakeCoin(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    var c = d < r * 0.72f ? new Color(1f, 0.82f, 0.2f) : new Color(0.95f, 0.62f, 0.1f);
                    if (d < r * 0.5f && d > r * 0.36f) c = new Color(1f, 0.93f, 0.55f);
                    c.a = Mathf.Clamp01(r - d);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Text Text(Transform parent, string name, string value, int size, Color color, TextAnchor anchor, Font font = null, bool outline = true)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font != null ? font : FontBold;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.text = value;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, 0.85f);
                o.effectDistance = new Vector2(2, -2);
            }
            return t;
        }

        public static void Stretch(RectTransform rt, float pad = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        /// <summary>Place by centre in reference pixels (1080x1920), y measured from the top of the screen.</summary>
        public static void PlaceTop(RectTransform rt, float centerX, float topY, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(centerX, -topY);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        /// <summary>Turkish-correct upper case (i -> İ, ı -> I).</summary>
        public static string UpperTr(string s) => string.IsNullOrEmpty(s) ? s : s.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();

        public static string Thousands(long v) => v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');

        public static string Short(string name, int max = 14) => string.IsNullOrEmpty(name) ? "?" : name.Length <= max ? name : name.Substring(0, max - 1) + "…";
    }
}
