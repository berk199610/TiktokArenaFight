using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Every icon and emoji face of the game is painted from code (no image files): gift moves (fist, bat, shovel,
    /// spin, wave, giant, thunder, tornado), crown, star, swords, skull, heart, comment, gift box, arrows and
    /// emoji faces for the reactions. Anti-aliased by super-sampling, dark outline, cached as sprites.
    /// </summary>
    public static class Icons
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        const int Size = 128;

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            var p = new Painter(Size);
            Draw(name, p);
            s = p.ToSprite(name);
            cache[name] = s;
            return s;
        }

        public static Sprite ForMove(MoveKind k)
        {
            switch (k)
            {
                case MoveKind.PowerPunch: return Get("fist");
                case MoveKind.Bat: return Get("bat");
                case MoveKind.Shovel: return Get("shovel");
                case MoveKind.SpinKick: return Get("spin");
                case MoveKind.Shockwave: return Get("wave");
                case MoveKind.Giant: return Get("giant");
                case MoveKind.Thunder: return Get("thunder");
                case MoveKind.Tornado: return Get("tornado");
                default: return Get("star");
            }
        }

        // ------------------------------------------------------------------ painter
        public class Painter
        {
            readonly int n;
            readonly Color[] px;
            const int SS = 3;
            public static readonly Color Ink = new Color(0.1f, 0.07f, 0.12f, 1f);

            public Painter(int size) { n = size; px = new Color[size * size]; }

            delegate float Sdf(float x, float y); // signed distance in 0..1 units, <0 inside

            void Fill(Sdf sdf, Color c, float minX, float minY, float maxX, float maxY)
            {
                int x0 = Mathf.Max(0, (int)(minX * n) - 2), x1 = Mathf.Min(n - 1, (int)(maxX * n) + 2);
                int y0 = Mathf.Max(0, (int)(minY * n) - 2), y1 = Mathf.Min(n - 1, (int)(maxY * n) + 2);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int inside = 0;
                        for (int sy = 0; sy < SS; sy++)
                            for (int sx = 0; sx < SS; sx++)
                            {
                                float u = (x + (sx + 0.5f) / SS) / n, v = (y + (sy + 0.5f) / SS) / n;
                                if (sdf(u, v) <= 0f) inside++;
                            }
                        if (inside == 0) continue;
                        float a = c.a * inside / (float)(SS * SS);
                        var d = px[y * n + x];
                        float outA = a + d.a * (1 - a);
                        if (outA <= 0f) continue;
                        var rgb = (new Color(c.r, c.g, c.b) * a + new Color(d.r, d.g, d.b) * d.a * (1 - a)) / outA;
                        px[y * n + x] = new Color(rgb.r, rgb.g, rgb.b, outA);
                    }
            }

            // shapes (pass o > 0 to draw a bigger version = outline)
            public void Circle(float cx, float cy, float r, Color c, float o = 0f)
            {
                float R = r + o;
                Fill((x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - R, c, cx - R, cy - R, cx + R, cy + R);
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color c, float o = 0f)
            {
                float RX = rx + o, RY = ry + o;
                Fill((x, y) => { float dx = (x - cx) / RX, dy = (y - cy) / RY; return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(RX, RY); }, c, cx - RX, cy - RY, cx + RX, cy + RY);
            }

            public void Capsule(float ax, float ay, float bx, float by, float r, Color c, float o = 0f) => Capsule2(ax, ay, bx, by, r, r, c, o);

            /// <summary>Capsule with a different radius at each end (tapered).</summary>
            public void Capsule2(float ax, float ay, float bx, float by, float ra, float rb, Color c, float o = 0f)
            {
                float RA = ra + o, RB = rb + o;
                float minX = Mathf.Min(ax - RA, bx - RB), maxX = Mathf.Max(ax + RA, bx + RB);
                float minY = Mathf.Min(ay - RA, by - RB), maxY = Mathf.Max(ay + RA, by + RB);
                Fill((x, y) =>
                {
                    float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
                    float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
                    float dx = pax - bax * h, dy = pay - bay * h;
                    return Mathf.Sqrt(dx * dx + dy * dy) - Mathf.Lerp(RA, RB, h);
                }, c, minX, minY, maxX, maxY);
            }

            public void RoundRect(float x0, float y0, float x1, float y1, float rad, Color c, float o = 0f)
            {
                float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, hx = (x1 - x0) * 0.5f + o, hy = (y1 - y0) * 0.5f + o, rr = rad + o;
                Fill((x, y) =>
                {
                    float qx = Mathf.Abs(x - cx) - hx + rr, qy = Mathf.Abs(y - cy) - hy + rr;
                    float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
                    return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - rr;
                }, c, cx - hx, cy - hy, cx + hx, cy + hy);
            }

            public void Ring(float cx, float cy, float r, float w, Color c, float a0 = 0f, float a1 = 360f, float o = 0f)
            {
                float R = r + w + o;
                Fill((x, y) =>
                {
                    float dx = x - cx, dy = y - cy;
                    float ang = Mathf.Repeat(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, 360f);
                    bool inArc = a1 - a0 >= 360f || (a0 <= a1 ? ang >= a0 && ang <= a1 : ang >= a0 || ang <= a1);
                    if (!inArc) return 1f;
                    return Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r) - (w + o);
                }, c, cx - R, cy - R, cx + R, cy + R);
            }

            public void Poly(Vector2[] p, Color c, float o = 0f)
            {
                float minX = 1, minY = 1, maxX = 0, maxY = 0;
                foreach (var q in p) { minX = Mathf.Min(minX, q.x); minY = Mathf.Min(minY, q.y); maxX = Mathf.Max(maxX, q.x); maxY = Mathf.Max(maxY, q.y); }
                Fill((x, y) =>
                {
                    // distance to polygon edges, sign from even-odd rule
                    float d = float.MaxValue; bool inside = false;
                    for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                    {
                        var a = p[j]; var b = p[i];
                        float ex = b.x - a.x, ey = b.y - a.y, wx = x - a.x, wy = y - a.y;
                        float h = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                        float dx = wx - ex * h, dy = wy - ey * h;
                        d = Mathf.Min(d, dx * dx + dy * dy);
                        if (((a.y > y) != (b.y > y)) && (x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)) inside = !inside;
                    }
                    return (inside ? -1f : 1f) * Mathf.Sqrt(d) - o;
                }, c, minX - o, minY - o, maxX + o, maxY + o);
            }

            public Sprite ToSprite(string name)
            {
                var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "icon_" + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                t.SetPixels(px);
                t.Apply(true, true);
                return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);
        static Vector2[] Star(float cx, float cy, float ro, float ri, int pts = 5, float rotDeg = 90f)
        {
            var a = new Vector2[pts * 2];
            for (int i = 0; i < pts * 2; i++)
            {
                float ang = (rotDeg + i * 180f / pts) * Mathf.Deg2Rad;
                float r = i % 2 == 0 ? ro : ri;
                a[i] = V(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }
            return a;
        }

        // ------------------------------------------------------------------ drawings
        static void Draw(string name, Painter p)
        {
            var ink = Painter.Ink;
            const float O = 0.035f; // outline width
            Color gold = new Color(1f, 0.8f, 0.2f), goldD = new Color(0.85f, 0.55f, 0.1f);
            Color skin = new Color(1f, 0.78f, 0.55f), yellow = new Color(1f, 0.85f, 0.25f);
            switch (name)
            {
                case "fist":
                {
                    // fingers + palm + thumb, with speed lines
                    p.RoundRect(0.26f, 0.22f, 0.76f, 0.62f, 0.1f, ink, O);
                    for (int i = 0; i < 4; i++) p.Circle(0.33f + i * 0.125f, 0.62f, 0.075f, ink, O);
                    p.Capsule(0.24f, 0.45f, 0.52f, 0.38f, 0.075f, ink, O);
                    p.RoundRect(0.26f, 0.22f, 0.76f, 0.62f, 0.1f, skin);
                    for (int i = 0; i < 4; i++) p.Circle(0.33f + i * 0.125f, 0.62f, 0.075f, skin);
                    for (int i = 0; i < 3; i++) p.Capsule(0.39f + i * 0.125f, 0.56f, 0.39f + i * 0.125f, 0.66f, 0.008f, new Color(0.75f, 0.5f, 0.35f));
                    p.Capsule(0.24f, 0.45f, 0.52f, 0.38f, 0.075f, new Color(1f, 0.72f, 0.5f));
                    p.Capsule(0.05f, 0.75f, 0.2f, 0.75f, 0.02f, Color.white);
                    p.Capsule(0.03f, 0.6f, 0.17f, 0.6f, 0.02f, Color.white);
                    p.Capsule(0.06f, 0.45f, 0.18f, 0.45f, 0.02f, Color.white);
                    break;
                }
                case "bat":
                    p.Capsule2(0.2f, 0.18f, 0.8f, 0.82f, 0.05f, 0.11f, ink, O);
                    p.Capsule2(0.2f, 0.18f, 0.8f, 0.82f, 0.05f, 0.11f, new Color(0.78f, 0.52f, 0.28f));
                    p.Capsule2(0.2f, 0.18f, 0.36f, 0.35f, 0.055f, 0.06f, new Color(0.2f, 0.18f, 0.22f));
                    p.Capsule2(0.45f, 0.5f, 0.75f, 0.8f, 0.02f, 0.04f, new Color(0.9f, 0.68f, 0.42f));
                    break;
                case "shovel":
                    p.Capsule(0.22f, 0.82f, 0.55f, 0.45f, 0.045f, ink, O);
                    p.Poly(new[] { V(0.5f, 0.5f), V(0.62f, 0.38f), V(0.86f, 0.2f), V(0.8f, 0.12f), V(0.62f, 0.14f), V(0.4f, 0.38f) }, ink, O);
                    p.Capsule(0.22f, 0.82f, 0.55f, 0.45f, 0.045f, new Color(0.72f, 0.48f, 0.25f));
                    p.Capsule(0.15f, 0.9f, 0.3f, 0.75f, 0.035f, new Color(0.25f, 0.2f, 0.2f));
                    p.Poly(new[] { V(0.5f, 0.5f), V(0.62f, 0.38f), V(0.86f, 0.2f), V(0.8f, 0.12f), V(0.62f, 0.14f), V(0.4f, 0.38f) }, new Color(0.75f, 0.8f, 0.86f));
                    break;
                case "spin":
                    p.Ring(0.5f, 0.5f, 0.3f, 0.07f, ink, 20f, 320f, O);
                    p.Poly(new[] { V(0.83f, 0.36f), V(0.62f, 0.33f), V(0.78f, 0.58f) }, ink, O);
                    p.Ring(0.5f, 0.5f, 0.3f, 0.07f, new Color(0.45f, 0.9f, 1f), 20f, 320f);
                    p.Poly(new[] { V(0.83f, 0.36f), V(0.62f, 0.33f), V(0.78f, 0.58f) }, new Color(0.45f, 0.9f, 1f));
                    p.Capsule(0.4f, 0.5f, 0.6f, 0.5f, 0.05f, ink, O);
                    p.Capsule(0.4f, 0.5f, 0.6f, 0.5f, 0.05f, Color.white);
                    break;
                case "wave":
                    for (int i = 0; i < 3; i++)
                    {
                        float r = 0.16f + i * 0.13f;
                        p.Ring(0.5f, 0.2f, r, 0.035f, ink, 10f, 170f, O);
                        p.Ring(0.5f, 0.2f, r, 0.035f, Color.Lerp(yellow, new Color(1f, 0.5f, 0.2f), i / 2f), 10f, 170f);
                    }
                    p.RoundRect(0.08f, 0.12f, 0.92f, 0.18f, 0.03f, ink, O);
                    p.RoundRect(0.08f, 0.12f, 0.92f, 0.18f, 0.03f, new Color(0.6f, 0.45f, 0.3f));
                    break;
                case "giant":
                    // big muscle arm (flex)
                    p.Capsule(0.25f, 0.25f, 0.55f, 0.3f, 0.12f, ink, O);
                    p.Capsule(0.55f, 0.3f, 0.62f, 0.68f, 0.11f, ink, O);
                    p.Circle(0.42f, 0.38f, 0.15f, ink, O);
                    p.Circle(0.62f, 0.76f, 0.12f, ink, O);
                    var arm = new Color(1f, 0.55f, 0.85f);
                    p.Capsule(0.25f, 0.25f, 0.55f, 0.3f, 0.12f, arm);
                    p.Capsule(0.55f, 0.3f, 0.62f, 0.68f, 0.11f, arm);
                    p.Circle(0.42f, 0.38f, 0.15f, new Color(1f, 0.62f, 0.9f));
                    p.Circle(0.62f, 0.76f, 0.12f, arm);
                    p.Circle(0.38f, 0.43f, 0.05f, new Color(1f, 0.85f, 0.95f));
                    break;
                case "thunder":
                {
                    var bolt = new[] { V(0.58f, 0.95f), V(0.25f, 0.5f), V(0.47f, 0.5f), V(0.36f, 0.05f), V(0.78f, 0.58f), V(0.55f, 0.58f), V(0.7f, 0.95f) };
                    p.Poly(bolt, ink, O);
                    p.Poly(bolt, yellow);
                    p.Poly(new[] { V(0.58f, 0.88f), V(0.38f, 0.56f), V(0.5f, 0.56f) }, new Color(1f, 0.97f, 0.7f));
                    break;
                }
                case "tornado":
                    for (int i = 0; i < 5; i++)
                    {
                        float y = 0.82f - i * 0.15f, rx = 0.38f - i * 0.065f, x = 0.5f + Mathf.Sin(i * 1.3f) * 0.05f;
                        p.Ellipse(x, y, rx, 0.07f, ink, O);
                    }
                    for (int i = 0; i < 5; i++)
                    {
                        float y = 0.82f - i * 0.15f, rx = 0.38f - i * 0.065f, x = 0.5f + Mathf.Sin(i * 1.3f) * 0.05f;
                        p.Ellipse(x, y, rx, 0.07f, Color.Lerp(new Color(0.8f, 0.85f, 0.95f), new Color(0.55f, 0.6f, 0.75f), i / 4f));
                    }
                    break;
                case "crown":
                {
                    var c = new[] { V(0.12f, 0.25f), V(0.88f, 0.25f), V(0.92f, 0.72f), V(0.7f, 0.5f), V(0.5f, 0.82f), V(0.3f, 0.5f), V(0.08f, 0.72f) };
                    p.Poly(c, ink, O);
                    p.Circle(0.08f, 0.74f, 0.06f, ink, O); p.Circle(0.5f, 0.85f, 0.06f, ink, O); p.Circle(0.92f, 0.74f, 0.06f, ink, O);
                    p.Poly(c, gold);
                    p.RoundRect(0.12f, 0.2f, 0.88f, 0.34f, 0.03f, goldD);
                    p.Circle(0.08f, 0.74f, 0.06f, gold); p.Circle(0.5f, 0.85f, 0.06f, gold); p.Circle(0.92f, 0.74f, 0.06f, gold);
                    p.Circle(0.5f, 0.45f, 0.07f, new Color(0.9f, 0.15f, 0.25f));
                    break;
                }
                case "star":
                    p.Poly(Star(0.5f, 0.48f, 0.44f, 0.19f), ink, O);
                    p.Poly(Star(0.5f, 0.48f, 0.44f, 0.19f), gold);
                    p.Poly(Star(0.5f, 0.52f, 0.22f, 0.09f), new Color(1f, 0.93f, 0.55f));
                    break;
                case "swords":
                    foreach (var s in new[] { -1, 1 })
                    {
                        float ax = 0.5f - 0.32f * s, bx = 0.5f + 0.3f * s;
                        p.Capsule(ax, 0.84f, bx, 0.2f, 0.05f, ink, O);
                    }
                    foreach (var s in new[] { -1, 1 })
                    {
                        float ax = 0.5f - 0.32f * s, bx = 0.5f + 0.3f * s;
                        p.Capsule(ax, 0.84f, bx, 0.2f, 0.045f, new Color(0.85f, 0.88f, 0.95f));
                        p.Capsule(bx - 0.12f * s, 0.32f, bx + 0.02f * s, 0.18f, 0.03f, goldD);
                        p.Capsule(bx - 0.08f * s, 0.16f, bx + 0.08f * s, 0.34f, 0.025f, gold);
                    }
                    break;
                case "skull":
                    p.Circle(0.5f, 0.56f, 0.32f, ink, O);
                    p.RoundRect(0.32f, 0.16f, 0.68f, 0.4f, 0.06f, ink, O);
                    p.Circle(0.5f, 0.56f, 0.32f, new Color(0.96f, 0.95f, 0.92f));
                    p.RoundRect(0.32f, 0.16f, 0.68f, 0.4f, 0.06f, new Color(0.96f, 0.95f, 0.92f));
                    p.Circle(0.38f, 0.55f, 0.08f, ink); p.Circle(0.62f, 0.55f, 0.08f, ink);
                    p.Poly(new[] { V(0.5f, 0.45f), V(0.45f, 0.36f), V(0.55f, 0.36f) }, ink);
                    for (int i = 0; i < 3; i++) p.RoundRect(0.39f + i * 0.08f, 0.17f, 0.43f + i * 0.08f, 0.28f, 0.01f, ink);
                    break;
                case "heart":
                    DrawHeart(p, 0.5f, 0.48f, 0.4f, new Color(1f, 0.25f, 0.4f), O);
                    break;
                case "comment":
                    p.RoundRect(0.1f, 0.3f, 0.9f, 0.88f, 0.16f, ink, O);
                    p.Poly(new[] { V(0.25f, 0.34f), V(0.18f, 0.1f), V(0.45f, 0.32f) }, ink, O);
                    p.RoundRect(0.1f, 0.3f, 0.9f, 0.88f, 0.16f, Color.white);
                    p.Poly(new[] { V(0.25f, 0.34f), V(0.18f, 0.1f), V(0.45f, 0.32f) }, Color.white);
                    for (int i = 0; i < 3; i++) p.Circle(0.3f + i * 0.2f, 0.59f, 0.055f, new Color(0.3f, 0.6f, 1f));
                    break;
                case "gift":
                    p.RoundRect(0.14f, 0.1f, 0.86f, 0.62f, 0.05f, ink, O);
                    p.RoundRect(0.1f, 0.56f, 0.9f, 0.74f, 0.04f, ink, O);
                    p.Circle(0.38f, 0.82f, 0.1f, ink, O); p.Circle(0.62f, 0.82f, 0.1f, ink, O);
                    p.RoundRect(0.14f, 0.1f, 0.86f, 0.62f, 0.05f, new Color(0.95f, 0.25f, 0.4f));
                    p.RoundRect(0.1f, 0.56f, 0.9f, 0.74f, 0.04f, new Color(1f, 0.35f, 0.5f));
                    p.RoundRect(0.44f, 0.1f, 0.56f, 0.74f, 0.01f, gold);
                    p.Ring(0.38f, 0.82f, 0.07f, 0.035f, gold); p.Ring(0.62f, 0.82f, 0.07f, 0.035f, gold);
                    break;
                case "coin":
                    p.Circle(0.5f, 0.5f, 0.42f, ink, O);
                    p.Circle(0.5f, 0.5f, 0.42f, goldD);
                    p.Circle(0.5f, 0.5f, 0.32f, gold);
                    p.Ring(0.5f, 0.5f, 0.2f, 0.04f, new Color(1f, 0.93f, 0.6f));
                    break;
                case "arrowUp":
                    p.Poly(new[] { V(0.5f, 0.9f), V(0.1f, 0.35f), V(0.9f, 0.35f) }, ink, O);
                    p.Poly(new[] { V(0.5f, 0.9f), V(0.1f, 0.35f), V(0.9f, 0.35f) }, new Color(0.3f, 1f, 0.45f));
                    break;
                case "arrowDown":
                    p.Poly(new[] { V(0.5f, 0.1f), V(0.1f, 0.65f), V(0.9f, 0.65f) }, ink, O);
                    p.Poly(new[] { V(0.5f, 0.1f), V(0.1f, 0.65f), V(0.9f, 0.65f) }, new Color(1f, 0.35f, 0.35f));
                    break;
                case "dot":
                    p.Circle(0.5f, 0.5f, 0.32f, ink, O);
                    p.Circle(0.5f, 0.5f, 0.32f, Color.white);
                    break;
                case "person":
                    p.Circle(0.5f, 0.72f, 0.16f, ink, O);
                    p.RoundRect(0.26f, 0.1f, 0.74f, 0.52f, 0.16f, ink, O);
                    p.Circle(0.5f, 0.72f, 0.16f, Color.white);
                    p.RoundRect(0.26f, 0.1f, 0.74f, 0.52f, 0.16f, Color.white);
                    break;
                case "fire":
                    DrawFlame(p, O);
                    break;
                case "sos":
                    p.Circle(0.5f, 0.5f, 0.42f, ink, O);
                    p.Circle(0.5f, 0.5f, 0.42f, Color.white);
                    p.RoundRect(0.42f, 0.2f, 0.58f, 0.8f, 0.03f, new Color(0.95f, 0.15f, 0.2f));
                    p.RoundRect(0.2f, 0.42f, 0.8f, 0.58f, 0.03f, new Color(0.95f, 0.15f, 0.2f));
                    break;
                default:
                    DrawFace(name, p, O);
                    break;
            }
        }

        static void DrawHeart(Painter p, float cx, float cy, float s, Color c, float O)
        {
            var ink = Painter.Ink;
            var tri = new[] { V(cx - s * 0.98f, cy + s * 0.12f), V(cx + s * 0.98f, cy + s * 0.12f), V(cx, cy - s * 0.95f) };
            if (O > 0)
            {
                p.Circle(cx - s * 0.48f, cy + s * 0.28f, s * 0.52f, ink, O);
                p.Circle(cx + s * 0.48f, cy + s * 0.28f, s * 0.52f, ink, O);
                p.Poly(tri, ink, O);
            }
            p.Circle(cx - s * 0.48f, cy + s * 0.28f, s * 0.52f, c);
            p.Circle(cx + s * 0.48f, cy + s * 0.28f, s * 0.52f, c);
            p.Poly(tri, c);
            p.Circle(cx - s * 0.55f, cy + s * 0.42f, s * 0.15f, new Color(1f, 1f, 1f, 0.6f));
        }

        static void DrawFlame(Painter p, float O)
        {
            var outer = new[] { V(0.5f, 0.95f), V(0.68f, 0.68f), V(0.82f, 0.45f), V(0.78f, 0.2f), V(0.6f, 0.06f), V(0.4f, 0.06f), V(0.22f, 0.2f), V(0.2f, 0.45f), V(0.34f, 0.62f), V(0.4f, 0.5f) };
            p.Poly(outer, Painter.Ink, O);
            p.Poly(outer, new Color(1f, 0.4f, 0.1f));
            p.Poly(new[] { V(0.5f, 0.68f), V(0.64f, 0.42f), V(0.62f, 0.2f), V(0.5f, 0.1f), V(0.38f, 0.2f), V(0.36f, 0.4f) }, new Color(1f, 0.82f, 0.25f));
        }

        /// <summary>Emoji faces: yellow round face + features per mood.</summary>
        static void DrawFace(string mood, Painter p, float O)
        {
            var ink = Painter.Ink;
            Color face = new Color(1f, 0.82f, 0.28f);
            if (mood == "angry") face = new Color(1f, 0.45f, 0.3f);
            if (mood == "scared") face = new Color(0.85f, 0.9f, 1f);
            if (mood == "tired") face = new Color(0.95f, 0.85f, 0.45f);
            p.Circle(0.5f, 0.5f, 0.42f, ink, O);
            p.Circle(0.5f, 0.5f, 0.42f, face);
            p.Circle(0.42f, 0.62f, 0.2f, new Color(1f, 1f, 1f, 0.18f));
            Color white = Color.white, red = new Color(0.85f, 0.15f, 0.2f);
            switch (mood)
            {
                case "angry":
                    p.Capsule(0.27f, 0.68f, 0.43f, 0.6f, 0.03f, ink);
                    p.Capsule(0.73f, 0.68f, 0.57f, 0.6f, 0.03f, ink);
                    p.Circle(0.37f, 0.52f, 0.05f, ink); p.Circle(0.63f, 0.52f, 0.05f, ink);
                    p.Ring(0.5f, 0.2f, 0.17f, 0.025f, ink, 45f, 135f);
                    break;
                case "tired":
                    p.Capsule(0.28f, 0.55f, 0.43f, 0.52f, 0.025f, ink);
                    p.Capsule(0.72f, 0.55f, 0.57f, 0.52f, 0.025f, ink);
                    p.Ellipse(0.5f, 0.3f, 0.09f, 0.06f, ink);
                    p.Circle(0.8f, 0.72f, 0.07f, new Color(0.4f, 0.75f, 1f));
                    p.Poly(new[] { V(0.8f, 0.86f), V(0.74f, 0.73f), V(0.86f, 0.73f) }, new Color(0.4f, 0.75f, 1f));
                    break;
                case "sneaky":
                    p.Capsule(0.3f, 0.58f, 0.44f, 0.58f, 0.04f, white);
                    p.Capsule(0.56f, 0.58f, 0.7f, 0.58f, 0.04f, white);
                    p.Circle(0.42f, 0.58f, 0.03f, ink); p.Circle(0.68f, 0.58f, 0.03f, ink);
                    p.Capsule(0.28f, 0.63f, 0.46f, 0.64f, 0.02f, ink); p.Capsule(0.54f, 0.64f, 0.72f, 0.63f, 0.02f, ink);
                    p.Ring(0.56f, 0.4f, 0.15f, 0.025f, ink, 210f, 300f);
                    break;
                case "laugh":
                    p.Ring(0.36f, 0.56f, 0.07f, 0.025f, ink, 20f, 160f);
                    p.Ring(0.64f, 0.56f, 0.07f, 0.025f, ink, 20f, 160f);
                    p.Poly(new[] { V(0.25f, 0.42f), V(0.75f, 0.42f), V(0.62f, 0.2f), V(0.38f, 0.2f) }, ink);
                    p.Ellipse(0.5f, 0.27f, 0.09f, 0.05f, new Color(1f, 0.4f, 0.45f));
                    p.Circle(0.2f, 0.5f, 0.06f, new Color(0.4f, 0.8f, 1f)); p.Circle(0.8f, 0.5f, 0.06f, new Color(0.4f, 0.8f, 1f));
                    break;
                case "cool":
                    p.RoundRect(0.2f, 0.5f, 0.47f, 0.66f, 0.06f, ink);
                    p.RoundRect(0.53f, 0.5f, 0.8f, 0.66f, 0.06f, ink);
                    p.RoundRect(0.4f, 0.6f, 0.6f, 0.64f, 0.01f, ink);
                    p.Ring(0.5f, 0.38f, 0.16f, 0.025f, ink, 210f, 330f);
                    break;
                case "scared":
                    p.Circle(0.36f, 0.56f, 0.09f, white); p.Circle(0.64f, 0.56f, 0.09f, white);
                    p.Ring(0.36f, 0.56f, 0.09f, 0.015f, ink); p.Ring(0.64f, 0.56f, 0.09f, 0.015f, ink);
                    p.Circle(0.36f, 0.55f, 0.035f, ink); p.Circle(0.64f, 0.55f, 0.035f, ink);
                    p.Ellipse(0.5f, 0.28f, 0.08f, 0.1f, ink);
                    p.Circle(0.82f, 0.74f, 0.06f, new Color(0.4f, 0.75f, 1f));
                    break;
                case "dizzy":
                    foreach (var cx in new[] { 0.36f, 0.64f })
                    {
                        p.Capsule(cx - 0.06f, 0.5f, cx + 0.06f, 0.62f, 0.02f, ink);
                        p.Capsule(cx - 0.06f, 0.62f, cx + 0.06f, 0.5f, 0.02f, ink);
                    }
                    p.Ring(0.43f, 0.3f, 0.07f, 0.02f, ink, 0f, 180f);
                    p.Ring(0.57f, 0.3f, 0.07f, 0.02f, ink, 180f, 360f);
                    break;
                case "love":
                    DrawHeart(p, 0.36f, 0.56f, 0.1f, red, 0f);
                    DrawHeart(p, 0.64f, 0.56f, 0.1f, red, 0f);
                    p.Ring(0.5f, 0.38f, 0.16f, 0.025f, ink, 210f, 330f);
                    break;
                case "starstruck":
                    p.Poly(Star(0.36f, 0.56f, 0.1f, 0.045f), new Color(1f, 0.55f, 0.1f));
                    p.Poly(Star(0.64f, 0.56f, 0.1f, 0.045f), new Color(1f, 0.55f, 0.1f));
                    p.Poly(new[] { V(0.28f, 0.38f), V(0.72f, 0.38f), V(0.6f, 0.22f), V(0.4f, 0.22f) }, ink);
                    break;
                case "happy":
                default:
                    p.Circle(0.37f, 0.57f, 0.05f, ink); p.Circle(0.63f, 0.57f, 0.05f, ink);
                    p.Ring(0.5f, 0.38f, 0.16f, 0.03f, ink, 200f, 340f);
                    break;
            }
        }
    }
}
