using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>Procedural meshes (cone, torus, flat ring, disc) + Unity primitive meshes without colliders, all cached.</summary>
    public static class MeshKit
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        public static Mesh Primitive(PrimitiveType t)
        {
            string key = "prim" + t;
            if (cache.TryGetValue(key, out var m)) return m;
            var go = GameObject.CreatePrimitive(t);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            cache[key] = m;
            return m;
        }

        public static Mesh Sphere => Primitive(PrimitiveType.Sphere);
        public static Mesh Cube => Primitive(PrimitiveType.Cube);
        public static Mesh Cylinder => Primitive(PrimitiveType.Cylinder);
        public static Mesh Capsule => Primitive(PrimitiveType.Capsule);
        public static Mesh Quad => Primitive(PrimitiveType.Quad);

        /// <summary>Frustum along +Y from y=0 (radius r0) to y=1 (radius r1). r1=0 gives a cone.</summary>
        public static Mesh Cone(float r0, float r1, int seg = 16, bool capped = true)
        {
            string key = $"cone{r0:0.###}_{r1:0.###}_{seg}_{capped}";
            if (cache.TryGetValue(key, out var m)) return m;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            float slope = (r0 - r1);
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var nn = new Vector3(dir.x, slope, dir.z).normalized;
                v.Add(dir * r0); n.Add(nn); uv.Add(new Vector2(i / (float)seg, 0));
                v.Add(dir * r1 + Vector3.up); n.Add(nn); uv.Add(new Vector2(i / (float)seg, 1));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tri.AddRange(new[] { a, b, c, c, b, d });
            }
            if (capped)
            {
                int center = v.Count; v.Add(Vector3.zero); n.Add(Vector3.down); uv.Add(new Vector2(0.5f, 0.5f));
                int start = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r0); n.Add(Vector3.down); uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                }
                for (int i = 0; i < seg; i++) tri.AddRange(new[] { center, start + i, start + i + 1 });
                if (r1 > 0.001f)
                {
                    int c2 = v.Count; v.Add(Vector3.up); n.Add(Vector3.up); uv.Add(new Vector2(0.5f, 0.5f));
                    int s2 = v.Count;
                    for (int i = 0; i <= seg; i++)
                    {
                        float a = i / (float)seg * Mathf.PI * 2f;
                        v.Add(new Vector3(Mathf.Cos(a) * r1, 1, Mathf.Sin(a) * r1)); n.Add(Vector3.up); uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                    }
                    for (int i = 0; i < seg; i++) tri.AddRange(new[] { c2, s2 + i + 1, s2 + i });
                }
            }
            m = new Mesh { name = key };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            cache[key] = m;
            return m;
        }

        /// <summary>Torus in the XZ plane. R = ring radius, r = tube radius.</summary>
        public static Mesh Torus(float R, float r, int seg = 24, int side = 8)
        {
            string key = $"torus{R:0.###}_{r:0.###}_{seg}_{side}";
            if (cache.TryGetValue(key, out var m)) return m;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int j = 0; j <= side; j++)
                {
                    float b = j / (float)side * Mathf.PI * 2f;
                    var nn = dir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    v.Add(dir * R + nn * r); n.Add(nn); uv.Add(new Vector2(i / (float)seg, j / (float)side));
                }
            }
            int stride = side + 1;
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                {
                    int a = i * stride + j, b = a + stride, c = a + 1, d = b + 1;
                    tri.AddRange(new[] { a, c, b, b, c, d });
                }
            m = new Mesh { name = key };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            cache[key] = m;
            return m;
        }

        /// <summary>Flat annulus in the XZ plane, u = around, v = inner(0)->outer(1). For ground rings and shock waves.</summary>
        public static Mesh FlatRing(float inner, float outer, int seg = 64)
        {
            string key = $"ring{inner:0.###}_{outer:0.###}_{seg}";
            if (cache.TryGetValue(key, out var m)) return m;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(dir * inner); uv.Add(new Vector2(i / (float)seg * 4f, 0));
                v.Add(dir * outer); uv.Add(new Vector2(i / (float)seg * 4f, 1));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tri.AddRange(new[] { a, c, b, b, c, d });
            }
            m = new Mesh { name = key };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            cache[key] = m;
            return m;
        }

        /// <summary>Open cylinder wall (no caps) along +Y, u around, v up. For light pillars, fire walls, tornado.</summary>
        public static Mesh Tube(float r0, float r1, int seg = 32)
        {
            string key = $"tube{r0:0.###}_{r1:0.###}_{seg}";
            if (cache.TryGetValue(key, out var m)) return m;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(dir * r0); uv.Add(new Vector2(i / (float)seg, 0));
                v.Add(dir * r1 + Vector3.up); uv.Add(new Vector2(i / (float)seg, 1));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tri.AddRange(new[] { a, b, c, c, b, d });
            }
            m = new Mesh { name = key };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            cache[key] = m;
            return m;
        }

        public static GameObject Part(string name, Mesh mesh, Material mat, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 localScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }

    /// <summary>Material copies from Resources/Materials, cached per color.</summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> bases = new Dictionary<string, Material>();

        static Material Base(string name)
        {
            if (bases.TryGetValue(name, out var m)) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                Debug.LogError("[Arena] Materyal yok: Resources/Materials/" + name + " (Arena > Kurulum çalıştırılmadı mı?)");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }
            bases[name] = m;
            return m;
        }

        static string Key(string b, Color c) => b + ColorUtility.ToHtmlStringRGBA(c);

        public static Material Lit(Color c) => Get("M_Lit", c, "_BaseColor");
        public static Material Metal(Color c) => Get("M_Metal", c, "_BaseColor");
        public static Material Unlit(Color c) => Get("M_Unlit", c, "_BaseColor");

        public static Material Emissive(Color c, float intensity = 2f)
        {
            string k = Key("M_LitEmissive", c) + intensity;
            if (cache.TryGetValue(k, out var m)) return m;
            m = new Material(Base("M_LitEmissive"));
            m.SetColor("_BaseColor", c);
            m.SetColor("_EmissionColor", c * intensity);
            cache[k] = m;
            return m;
        }

        static Material Get(string baseName, Color c, string prop)
        {
            string k = Key(baseName, c);
            if (cache.TryGetValue(k, out var m)) return m;
            m = new Material(Base(baseName));
            m.SetColor(prop, c);
            cache[k] = m;
            return m;
        }

        /// <summary>A fresh effect material (not shared) so its color can be animated.</summary>
        public static Material FxNew(bool additive, Texture tex, Color c)
        {
            var m = new Material(Base(additive ? "M_FxAdd" : "M_FxAlpha"));
            m.SetTexture("_MainTex", tex);
            m.SetColor("_Color", c);
            return m;
        }

        public static Material Fx(bool additive, Texture tex, Color c)
        {
            string k = (additive ? "add" : "alpha") + (tex ? tex.name : "") + ColorUtility.ToHtmlStringRGBA(c);
            if (cache.TryGetValue(k, out var m)) return m;
            m = FxNew(additive, tex, c);
            cache[k] = m;
            return m;
        }
    }
}
