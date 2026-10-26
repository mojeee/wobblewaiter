using System.Collections.Generic;
using UnityEngine;

namespace WobbleWaiter
{
    // The food that can land on the tray. Sizes come from the models, scaled to a fixed width.
    public static class Food
    {
        public class Def
        {
            public string kind, file;
            public float width, weight;
            public bool eyes;
            public Vector3 size;
        }

        public static readonly List<Def> All = new List<Def>
        {
            new Def { kind = "plate",    file = "plate-dinner",    width = 0.39f, weight = 3f,   eyes = false },
            new Def { kind = "burger",   file = "burger-cheese",   width = 0.25f, weight = 2f,   eyes = true },
            new Def { kind = "cake",     file = "cake-birthday",   width = 0.35f, weight = 1.4f, eyes = true },
            new Def { kind = "coffee",   file = "cup-coffee",      width = 0.17f, weight = 1.6f, eyes = true },
            new Def { kind = "soda",     file = "soda",            width = 0.13f, weight = 1.2f, eyes = true },
            new Def { kind = "fries",    file = "fries",           width = 0.15f, weight = 1.4f, eyes = true },
            new Def { kind = "donut",    file = "donut-sprinkles", width = 0.20f, weight = 1.4f, eyes = true },
            new Def { kind = "sundae",   file = "sundae",          width = 0.15f, weight = 0.8f, eyes = true },
            new Def { kind = "pancakes", file = "pancakes",        width = 0.26f, weight = 1f,   eyes = true },
            new Def { kind = "turkey",   file = "turkey",          width = 0.43f, weight = 0.6f, eyes = true },
        };

        static readonly Dictionary<string, Def> byKind = new Dictionary<string, Def>();
        static float totalWeight;
        static Material eyeWhite, eyeBlack;

        public static void Init()
        {
            if (byKind.Count > 0) return;
            eyeWhite = Looks.Toon(Color.white, null, 0f);
            eyeBlack = Looks.Toon(Looks.Hex("#1B1B1B"), null, 0f);
            foreach (var d in All)
            {
                byKind[d.kind] = d;
                totalWeight += d.weight;
                var probe = Looks.Fit(Looks.Prefab("Food/" + d.file), d.width, 0, true, out d.size);
                Looks.Kill(probe);
            }
        }

        public static Def Get(string kind) => byKind[kind];

        public static string Random()
        {
            float t = UnityEngine.Random.value * totalWeight;
            foreach (var d in All) { t -= d.weight; if (t <= 0) return d.kind; }
            return "plate";
        }

        public static GameObject Build(string kind)
        {
            var d = byKind[kind];
            var g = Looks.Fit(Looks.Prefab("Food/" + d.file), d.width, 0, true, out _);
            Looks.Toonify(g, 0.0025f);
            if (d.eyes)
            {
                float r = Mathf.Clamp(d.size.y * 0.16f, 0.012f, 0.026f);
                var eyes = new GameObject("eyes").transform;
                eyes.SetParent(g.transform, false);
                eyes.localPosition = new Vector3(0, d.size.y * 0.12f, -(d.size.z / 2 + r * 0.2f));
                foreach (int s in new[] { -1, 1 })
                {
                    Looks.Primitive(PrimitiveType.Sphere, eyeWhite, eyes, new Vector3(s * r * 1.15f, 0, 0), Vector3.one * r * 2);
                    Looks.Primitive(PrimitiveType.Sphere, eyeBlack, eyes, new Vector3(s * r * 1.15f, 0, -r * 0.6f), Vector3.one * r * 1.1f);
                }
            }
            return g;
        }

        public static GameObject BrokenPlate()
        {
            var g = Looks.Fit(Looks.Prefab("Food/plate-broken"), Get("plate").width * 1.2f, 0, false, out _);
            Looks.Toonify(g, 0.0025f);
            return g;
        }
    }
}
