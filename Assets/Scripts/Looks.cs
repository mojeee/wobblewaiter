using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WobbleWaiter
{
    // Materials, generated textures and model fitting shared by the whole game.
    public static class Looks
    {
        static Shader toon, glow, unlit;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        public static Texture2D Checker, Carpet, Wall, GlowDot, Beam, Window, Sign, White;

        public static void Init()
        {
            toon = Shader.Find("WobbleWaiter/Toon");
            glow = Shader.Find("WobbleWaiter/Glow");
            unlit = Shader.Find("WobbleWaiter/Unlit");
            SetOutlines(true);
            BuildTextures();
        }

        public static void SetOutlines(bool on) => Shader.SetGlobalFloat("_WW_OutlineScale", on ? 1f : 0f);

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static GameObject Prefab(string path)
        {
            if (!prefabs.TryGetValue(path, out var p))
            {
                p = Resources.Load<GameObject>("Models/" + path);
                if (p == null) Debug.LogError("Wobble Waiter: missing model Resources/Models/" + path);
                prefabs[path] = p;
            }
            return p;
        }

        public static Material Toon(Color c, Texture tex = null, float outline = 0.0032f)
        {
            string key = "t" + ColorUtility.ToHtmlStringRGBA(c) + (tex ? tex.GetHashCode().ToString() : "-") + outline.ToString("F4");
            if (!cache.TryGetValue(key, out var m))
            {
                m = new Material(toon) { color = c, mainTexture = tex };
                m.SetFloat("_Outline", outline);
                cache[key] = m;
            }
            return m;
        }

        public static Material Toon(string hex, float outline = 0.0032f) => Toon(Hex(hex), null, outline);

        public static Material Glow(Texture tex, Color c)
        {
            string key = "g" + ColorUtility.ToHtmlStringRGBA(c) + tex.GetHashCode();
            if (!cache.TryGetValue(key, out var m)) cache[key] = m = new Material(glow) { color = c, mainTexture = tex };
            return m;
        }

        public static Material Unlit(Color c, Texture tex = null)
        {
            string key = "u" + ColorUtility.ToHtmlStringRGBA(c) + (tex ? tex.GetHashCode().ToString() : "-");
            if (!cache.TryGetValue(key, out var m)) cache[key] = m = new Material(unlit) { color = c, mainTexture = tex ? tex : White };
            return m;
        }

        // swap every imported material for the cartoon one, keeping the model's own colors and texture
        public static void Toonify(GameObject root, float outline = 0.0032f)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials;
                var dst = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var m = src[i];
                    Texture tex = m != null && m.HasProperty("_MainTex") ? m.mainTexture : null;
                    if (tex == null && m != null && m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
                    Color col = m != null && m.HasProperty("_Color") ? m.color : Color.white;
                    col.a = 1;
                    dst[i] = Toon(col, tex, outline);
                }
                r.sharedMaterials = dst;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }

        public static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // Instantiates a model inside a wrapper so its footprint is centered and it stands on y = 0
        // (or is centered on y), scaled so its size along `axis` (0 = x, 1 = y) equals `target` meters.
        public static GameObject Fit(GameObject prefab, float target, int axis, bool centerY, out Vector3 size)
        {
            var wrap = new GameObject(prefab ? prefab.name : "missing");
            if (prefab == null) { size = Vector3.one * 0.2f; return wrap; }
            var inst = Object.Instantiate(prefab, wrap.transform);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
            var b = RendererBounds(inst);
            float cur = b.size[axis];
            float sc = cur > 1e-5f ? target / cur : 1f;
            inst.transform.localScale = Vector3.one * sc;
            b = RendererBounds(inst);
            size = b.size;
            inst.transform.localPosition = new Vector3(-b.center.x, centerY ? -b.center.y : -b.min.y, -b.center.z);
            return wrap;
        }

        public static GameObject Primitive(PrimitiveType type, Material m, Transform parent, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Kill(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            return go;
        }

        // Destroy that also works in the editor (the icon maker builds objects outside Play mode)
        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        static Texture2D Tex(int w, int h, System.Func<int, int, Color> f, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = wrap, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        static void BuildTextures()
        {
            Color cream = Hex("#FFF4E4"), ink = Hex("#2E3A45");
            White = Tex(4, 4, (x, y) => Color.white);
            Checker = Tex(64, 64, (x, y) => ((x < 32) == (y < 32)) ? ink : cream);
            Color red = Hex("#D8433A"), gold = Hex("#FFC93C"), diamond = Hex("#E86A4F");
            Carpet = Tex(64, 128, (x, y) =>
            {
                if (x < 4 || x > 59) return gold;
                int cy = y % 32 - 16, cx = x - 32;
                return Mathf.Abs(cx) + Mathf.Abs(cy) < 9 ? diamond : red;
            });
            Color s1 = Hex("#FFD9A8"), s2 = Hex("#FFE8C6"), trim = Hex("#35B3A3"), panel = Hex("#24978A"), line = Hex("#1E8075");
            Wall = Tex(128, 128, (x, y) =>
            {
                float v = y / 128f;
                if (v > 0.38f) return (x / 16) % 2 == 0 ? s1 : s2;
                if (v > 0.34f) return trim;
                int px = x % 32;
                bool border = (px < 3 || px > 28 || y < 6 || y > 38);
                return border ? line : panel;
            });
            GlowDot = Tex(64, 64, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                float a = Mathf.Clamp01(1 - d);
                a = a * a * (0.5f + 0.5f * Mathf.Clamp01(1 - d * 3));
                return new Color(1f, 0.85f, 0.6f, 1) * a;
            }, TextureWrapMode.Clamp);
            Beam = Tex(4, 64, (x, y) => Color.white * Mathf.Pow(y / 63f, 1.6f) * 0.14f, TextureWrapMode.Clamp);
            Color sky1 = Hex("#6CC4F5"), sky2 = Hex("#D6F2FD"), grass = Hex("#77C26A");
            Window = Tex(32, 40, (x, y) =>
            {
                if (y < 6) return grass;
                bool cloud = Vector2.Distance(new Vector2(x, y), new Vector2(12, 28)) < 5 || Vector2.Distance(new Vector2(x, y), new Vector2(18, 29)) < 6;
                return cloud ? Color.white : Color.Lerp(sky2, sky1, y / 40f);
            }, TextureWrapMode.Clamp);
            Color yel = Hex("#FFC93C"), dark = Hex("#1B2A29");
            Sign = Tex(32, 32, (x, y) =>
            {
                float cx = Mathf.Abs(x - 15.5f);
                bool tri = y > 5 && y < 27 && cx < (27 - y) * 0.55f;
                bool bang = cx < 1.6f && ((y > 12 && y < 22) || (y > 7 && y < 10));
                return tri && !bang ? dark : yel;
            }, TextureWrapMode.Clamp);
        }
    }
}
