using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace WobbleWaiter.EditorTools
{
    // Renders the app icon and the start-screen picture from the game's own models:
    // the waiter with a tall wobbly stack of food on his tray.
    public static class ArtMaker
    {
        const string IconPath = "Assets/Art/AppIcon.png";
        const string HeroPath = "Assets/Resources/Art/Hero.png";
        static readonly Vector3 Stage = new Vector3(0, 500, 0);

        [MenuItem("Wobble Waiter/Make App Icon and Logo")]
        public static void MakeArt()
        {
            Looks.Init();
            Food.Init();
            var root = new GameObject("ArtStage");
            root.transform.position = Stage;
            try
            {
                BuildScene(root.transform, out var cam);
                Shader.SetGlobalFloat("_WW_OutlineScale", 2.2f);
                var hero = Render(cam, 1024, transparent: true);
                Directory.CreateDirectory(Path.GetDirectoryName(HeroPath));
                File.WriteAllBytes(HeroPath, hero.EncodeToPNG());

                var icon = Compose(hero);
                Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
                File.WriteAllBytes(IconPath, icon.EncodeToPNG());
                Shader.SetGlobalFloat("_WW_OutlineScale", 1f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.ImportAsset(HeroPath);
            AssetDatabase.ImportAsset(IconPath);
            foreach (var p in new[] { HeroPath, IconPath })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = p == IconPath;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 1024;
                ti.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log("Wobble Waiter: app icon and logo made -> " + IconPath + ", " + HeroPath);
        }

        static void BuildScene(Transform root, out Camera cam)
        {
            var light = new GameObject("Key").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.25f; light.color = new Color(1f, 0.93f, 0.82f);
            light.transform.SetParent(root, false); light.transform.rotation = Quaternion.Euler(35, 150, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.55f, 0.5f);
            RenderSettings.fog = false;

            var waiter = Character.Make("male-d");
            waiter.Root.transform.SetParent(root, false);
            waiter.Root.transform.localRotation = Quaternion.Euler(0, 160, 0);
            waiter.Pose("idle", 0.3f);

            var head = waiter.Head != null ? waiter.Head.position : root.position + Vector3.up * Character.Height * 0.5f;
            var tray = new GameObject("Tray").transform;
            tray.SetParent(root, true);
            tray.position = head + Vector3.up * (Character.Height * 0.43f);
            tray.rotation = Quaternion.Euler(-4, 160, -6);
            tray.localScale = Vector3.one * 1.3f;
            Looks.Primitive(PrimitiveType.Cylinder, Looks.Toon("#DDE5EA", 0.003f), tray, new Vector3(0, -0.02f, 0), new Vector3(1.2f, 0.02f, 1.2f));
            Looks.Primitive(PrimitiveType.Cylinder, Looks.Toon("#AEBBC4", 0.003f), tray, new Vector3(0, -0.035f, 0), new Vector3(1.25f, 0.012f, 1.25f));

            // a tall, slightly crooked stack
            string[] stack = { "plate", "burger", "cake", "pancakes", "donut", "coffee" };
            float y = 0.0f, x = 0;
            for (int i = 0; i < stack.Length; i++)
            {
                var d = Food.Get(stack[i]);
                var f = Food.Build(stack[i]).transform;
                f.SetParent(tray, false);
                x += (i % 2 == 0 ? 1 : -1) * 0.035f * i;
                f.localPosition = new Vector3(x, y + d.size.y / 2, 0);
                f.localRotation = Quaternion.Euler(0, 160 + i * 25, (i % 2 == 0 ? 1 : -1) * (3 + i * 2.5f));
                y += d.size.y * 0.97f;
            }

            cam = new GameObject("ArtCam").AddComponent<Camera>();
            cam.transform.SetParent(root, false);
            var top = tray.position + Vector3.up * y;
            var center = (root.position + top) * 0.5f + Vector3.up * 0.1f;
            float height = (top.y - root.position.y) * 1.05f + 0.3f;
            cam.fieldOfView = 24;
            float dist = height * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f;
            // in front of the waiter, a little to the side and a little above, so his face shows
            var facing = waiter.Root.transform.forward;
            var dir = Quaternion.AngleAxis(-22, Vector3.up) * facing;
            cam.transform.position = center + dir * dist + Vector3.up * dist * 0.14f;
            cam.transform.LookAt(center);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 100;
        }

        static Texture2D Render(Camera cam, int size, bool transparent)
        {
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            return tex;
        }

        // warm sunburst background with a soft shadow under the character
        static Texture2D Compose(Texture2D hero)
        {
            int n = hero.width;
            var outTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            Color inner = new Color(1f, 0.86f, 0.42f), outer = new Color(0.98f, 0.45f, 0.24f), ray = new Color(1f, 0.93f, 0.6f);
            var hp = hero.GetPixels();
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - n * 0.5f) / n, dy = (y - n * 0.42f) / n;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    var bg = Color.Lerp(inner, outer, Mathf.SmoothStep(0, 1, r * 1.6f));
                    if (Mathf.Repeat(ang * 12 / (2 * Mathf.PI), 1) < 0.5f) bg = Color.Lerp(bg, ray, 0.28f * (1 - Mathf.Clamp01(r * 1.4f)));
                    // shadow: the hero's alpha, shifted down-right and softened
                    int sx = Mathf.Clamp(x - n / 60, 0, n - 1), sy = Mathf.Clamp(y + n / 40, 0, n - 1);
                    float sh = hp[sy * n + sx].a * 0.25f;
                    bg = Color.Lerp(bg, new Color(0.45f, 0.18f, 0.08f), sh);
                    // character scaled to 92% and centred
                    float u = (x - n * 0.04f) / (n * 0.92f), v = (y - n * 0.02f) / (n * 0.92f);
                    var c = u >= 0 && u < 1 && v >= 0 && v < 1 ? hero.GetPixelBilinear(u, v) : Color.clear;
                    var col = Color.Lerp(bg, new Color(c.r, c.g, c.b), c.a);
                    col.a = 1;
                    px[y * n + x] = col;
                }
            outTex.SetPixels(px);
            outTex.Apply();
            return outTex;
        }
    }
}
