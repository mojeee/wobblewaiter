using UnityEngine;

namespace WobbleWaiter
{
    // A Kenney mini character with its legacy animations, optionally wearing a chef hat or a cap.
    public class Character
    {
        public const float Height = 1.47f;

        public GameObject Root;
        public Transform Head;
        Animation anim;
        string current;

        public static Character Make(string file, string hat = null)
        {
            var c = new Character();
            c.Root = Looks.Fit(Looks.Prefab("Chars/character-" + file), Height, 1, false, out _);
            Looks.Toonify(c.Root, 0.0035f);
            c.anim = c.Root.GetComponentInChildren<Animation>();
            if (c.anim != null) foreach (AnimationState st in c.anim) st.wrapMode = WrapMode.Loop;
            c.Head = Looks.FindDeep(c.Root.transform, "head");
            if (hat != null && c.Head != null) c.AddHat(hat);
            return c;
        }

        void AddHat(string hat)
        {
            var g = new GameObject("hat").transform;
            g.position = Head.position + Vector3.up * (Height * 0.49f);
            if (hat == "chef")
            {
                var white = Looks.Toon("#FFFFFF", 0.003f);
                Looks.Primitive(PrimitiveType.Cylinder, white, g, new Vector3(0, 0.055f, 0), new Vector3(0.33f, 0.055f, 0.33f));
                Looks.Primitive(PrimitiveType.Sphere, white, g, new Vector3(0, 0.17f, 0), new Vector3(0.42f, 0.26f, 0.42f));
            }
            else
            {
                var red = Looks.Toon("#E8463B", 0.003f);
                Looks.Primitive(PrimitiveType.Cylinder, red, g, new Vector3(0, 0.02f, 0), new Vector3(0.48f, 0.03f, 0.48f));
                Looks.Primitive(PrimitiveType.Cube, red, g, new Vector3(0, 0.0f, 0.27f), new Vector3(0.24f, 0.02f, 0.18f));
            }
            g.SetParent(Head, true);
        }

        public void Play(string clip, float fade = 0.2f, float speed = 1f)
        {
            if (anim == null) return;
            var st = anim[clip];
            if (st == null) return;
            st.speed = speed;
            if (current == clip) return;
            if (current == null || fade <= 0) anim.Play(clip); else anim.CrossFade(clip, fade);
            current = clip;
        }

        public void Seek(float t)
        {
            if (anim == null || current == null) return;
            var st = anim[current];
            if (st != null) st.time = t;
        }

        // freeze a clip at a moment, for still pictures like the app icon
        public void Pose(string clip, float t)
        {
            if (anim == null || anim[clip] == null) return;
            anim.Play(clip);
            var st = anim[clip];
            st.time = t; st.enabled = true; st.weight = 1;
            anim.Sample();
            st.enabled = false;
        }

        public void SetVisible(bool on)
        {
            foreach (var r in Root.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }
    }
}
