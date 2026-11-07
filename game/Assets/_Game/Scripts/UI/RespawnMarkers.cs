using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arena
{
    /// <summary>A small stone at the death position. Its bar uses the roster's actual return time.</summary>
    public class RespawnMarkers : MonoBehaviour
    {
        class Marker
        {
            public Viewer viewer;
            public Transform root;
            public RectTransform face;
            public Image fill, gift;
            public Text name;
            public float showAt;
        }

        readonly List<Marker> active = new List<Marker>();
        readonly Stack<Marker> pool = new Stack<Marker>();
        public int Count => active.Count;

        public void Show(Viewer viewer, Vector3 position, float now)
        {
            if (viewer == null || viewer.isBot) return;
            Remove(viewer);
            var m = pool.Count > 0 ? pool.Pop() : Build();
            m.viewer = viewer;
            m.root.position = new Vector3(position.x, 0.025f, position.z);
            m.name.text = Ui.Short(viewer.DisplayName, 11);
            m.fill.fillAmount = 0;
            m.showAt = now + GameConfig.Current.death.lieSec;
            m.root.gameObject.SetActive(false);
            active.Add(m);
        }

        public void Remove(Viewer viewer)
        {
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].viewer == viewer) Release(i);
        }

        void Release(int i)
        {
            var m = active[i];
            m.root.gameObject.SetActive(false);
            m.viewer = null;
            active.RemoveAt(i);
            pool.Push(m);
        }

        Marker Build()
        {
            var cfg = GameConfig.Current.death;
            float w = cfg.markerWidth, h = cfg.markerHeight;
            var rt = new GameObject("ReturnStone").transform;
            rt.SetParent(transform, false);
            var stone = Mats.Lit(new Color(0.34f, 0.38f, 0.43f));
            MeshKit.Part("Foot", MeshKit.Cube, Mats.Lit(new Color(0.2f, 0.24f, 0.29f)), rt,
                new Vector3(0, h * 0.06f, 0), Vector3.zero, new Vector3(w * 1.18f, h * 0.12f, w * 0.48f));
            MeshKit.Part("Stone", MeshKit.Cube, stone, rt,
                new Vector3(0, h * 0.41f, 0), Vector3.zero, new Vector3(w, h * 0.66f, w * 0.28f));
            MeshKit.Part("Arch", MeshKit.Sphere, stone, rt,
                new Vector3(0, h * 0.75f, 0), Vector3.zero, new Vector3(w, h * 0.48f, w * 0.28f));

            var face = Ui.Rect(rt, "ReturnSign");
            face.sizeDelta = new Vector2(260, 260);
            face.localPosition = new Vector3(0, h * 0.5f, -w * 0.15f);
            face.localScale = Vector3.one * (w * 0.95f / 260f);
            var canvas = face.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var label = Ui.Text(face, "Name", "", 42, Color.white, TextAnchor.MiddleCenter, Ui.FontBold, false);
            label.rectTransform.anchoredPosition = new Vector2(0, 62);
            label.rectTransform.sizeDelta = new Vector2(250, 60);
            var track = Ui.Image(face, "ReturnBar", Ui.Rounded, new Color(0.07f, 0.1f, 0.14f));
            track.rectTransform.anchoredPosition = new Vector2(0, 4);
            track.rectTransform.sizeDelta = new Vector2(224, 28);
            var fill = Ui.Image(track.transform, "Progress", null, new Color(0.35f, 0.85f, 0.72f));
            Ui.Stretch(fill.rectTransform, 3);
            fill.sprite = TextureSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            var gift = Ui.Image(face, "Gift", Icons.Get("gift"), new Color(1f, 0.82f, 0.28f));
            gift.rectTransform.anchoredPosition = new Vector2(-47, -61);
            gift.rectTransform.sizeDelta = new Vector2(55, 55);
            var arrow = Ui.Text(face, "Instant", ">", 56, Color.white, TextAnchor.MiddleCenter, Ui.FontBlack, false);
            arrow.rectTransform.anchoredPosition = new Vector2(4, -62);
            arrow.rectTransform.sizeDelta = new Vector2(38, 58);
            var person = Ui.Image(face, "Return", Icons.Get("person"), new Color(0.4f, 1f, 0.72f));
            person.rectTransform.anchoredPosition = new Vector2(51, -61);
            person.rectTransform.sizeDelta = new Vector2(48, 48);
            return new Marker { root = rt, face = face, fill = fill, gift = gift, name = label };
        }

        static Sprite white;
        static Sprite TextureSprite()
        {
            if (white == null) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));
            return white;
        }

        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm?.Cam?.cam == null) return;
            var cam = gm.Cam.cam.transform;
            float now = Time.time;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var m = active[i];
                var v = m.viewer;
                if (v.fighter != null && v.fighter.IsAlive || v.state == ViewerState.Watching)
                {
                    Release(i);
                    continue;
                }
                m.root.gameObject.SetActive(now >= m.showAt);
                var dir = m.root.position - cam.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.001f) m.root.rotation = Quaternion.LookRotation(dir);
                // Keep the engraving on the upright front face instead of tilting it into the stone.
                m.face.localRotation = Quaternion.identity;
                m.fill.fillAmount = Mathf.Clamp01((now - v.diedAt) / Mathf.Max(0.1f, v.deadUntil - v.diedAt));
                m.gift.color = gm.GiftEntryOpen ? new Color(1f, 0.82f, 0.28f) : new Color(0.55f, 0.55f, 0.55f);
            }
        }
    }
}
