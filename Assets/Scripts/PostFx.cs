using UnityEngine;

namespace WobbleWaiter
{
    // Bloom, warm grade and vignette on the game camera. Turned off in LIGHT graphics.
    [RequireComponent(typeof(Camera))]
    public class PostFx : MonoBehaviour
    {
        public float threshold = 0.93f, intensity = 0.28f, vignette = 0.85f, saturation = 1.1f, contrast = 1.05f;
        public Color tint = new Color(1.03f, 1.0f, 0.95f);
        Material mat;

        void Awake()
        {
            var sh = Shader.Find("WobbleWaiter/PostFx");
            if (sh == null || !sh.isSupported) { enabled = false; return; }
            mat = new Material(sh);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null) { Graphics.Blit(src, dst); return; }
            mat.SetFloat("_Threshold", threshold);
            mat.SetFloat("_Intensity", intensity);
            mat.SetFloat("_Vignette", vignette);
            mat.SetFloat("_Saturation", saturation);
            mat.SetFloat("_Contrast", contrast);
            mat.SetVector("_Tint", tint);
            int w = Mathf.Max(8, src.width / 4), h = Mathf.Max(8, src.height / 4);
            var fmt = src.format;
            var a = RenderTexture.GetTemporary(w, h, 0, fmt);
            var b = RenderTexture.GetTemporary(w / 2, h / 2, 0, fmt);
            var c = RenderTexture.GetTemporary(w / 4, h / 4, 0, fmt);
            Graphics.Blit(src, a, mat, 0);
            Graphics.Blit(a, b, mat, 1);
            Graphics.Blit(b, c, mat, 1);
            Graphics.Blit(c, b, mat, 2);
            Graphics.Blit(b, a, mat, 2);
            mat.SetTexture("_Bloom", a);
            Graphics.Blit(src, dst, mat, 3);
            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
            RenderTexture.ReleaseTemporary(c);
        }
    }
}
