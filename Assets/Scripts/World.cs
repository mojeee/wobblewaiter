using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WobbleWaiter
{
    public class Colleague { public int chunk, side, state; public float s; public string kind; public Character ch; public GameObject item, zone; }
    public class Pickup { public int chunk; public float s, u, phase; public string kind; public GameObject root, food; public bool taken; }
    public class Coin { public int chunk; public float s, u; public Transform t; public bool taken; }
    public class Obstacle { public int chunk; public float s, u, w; public string type; public GameObject g; public bool hit; }

    // Builds the restaurant in 6 m chunks along the track, a little ahead of the waiter,
    // and removes chunks once they are behind the camera.
    public class World
    {
        public const float RoomHeight = 4.8f, WallU = 5.2f, Chunk = 6f, Lane = 0.9f;

        readonly Track track;
        readonly Transform root;
        readonly Dictionary<int, GameObject> chunks = new Dictionary<int, GameObject>();
        readonly Dictionary<int, List<Mesh>> chunkMeshes = new Dictionary<int, List<Mesh>>();
        public readonly List<Colleague> Colleagues = new List<Colleague>();
        public readonly List<Pickup> Pickups = new List<Pickup>();
        public readonly List<Coin> Coins = new List<Coin>();
        public readonly List<Obstacle> Obstacles = new List<Obstacle>();
        readonly List<GameObject> decor = new List<GameObject>();
        readonly List<GameObject> beams = new List<GameObject>();
        readonly List<Transform> billboards = new List<Transform>();
        readonly List<Transform> fans = new List<Transform>();

        Material carpet, wall, rail, baseboard, wood, zone, ring, coin, puddle, glow, beam, window, frame, sign, box, neonGlowPink, neonGlowBlue;
        Mesh cone;
        Font font;
        Transform floor, ceiling;
        ParticleSystem dust;
        bool light;
        int given, nextSide = 1;

        static readonly (string name, string hat)[] Crew =
        {
            ("female-e", "chef"), ("male-e", "chef"), ("male-a", "cap"), ("female-d", null), ("male-f", "cap"), ("female-b", "chef")
        };
        static readonly string[] Guests = { "female-a", "female-c", "female-f", "male-b", "female-b", "male-a", "female-d", "male-f" };
        static readonly string[] NeonWords = { "DINER", "EAT", "YUM!", "OPEN", "TASTY" };

        public World(Track track)
        {
            this.track = track;
            root = new GameObject("Restaurant").transform;
            carpet = Looks.Toon(Color.white, Looks.Carpet, 0f);
            wall = Looks.Toon(Color.white, Looks.Wall, 0f);
            rail = Looks.Toon("#9A6543", 0f);
            baseboard = Looks.Toon("#1E6E64", 0f);
            wood = Looks.Toon("#8A5A3B", 0.003f);
            zone = Looks.Glow(Looks.GlowDot, new Color(1f, 0.85f, 0.35f, 1));
            ring = Looks.Unlit(Looks.Hex("#FFE07A"));
            coin = Looks.Toon("#FFC93C", 0.002f);
            puddle = Looks.Unlit(Looks.Hex("#8FD3F7"));
            glow = Looks.Glow(Looks.GlowDot, new Color(1f, 0.92f, 0.75f, 1));
            neonGlowPink = Looks.Glow(Looks.GlowDot, new Color(1f, 0.3f, 0.6f, 1));
            neonGlowBlue = Looks.Glow(Looks.GlowDot, new Color(0.3f, 0.85f, 1f, 1));
            beam = Looks.Glow(Looks.Beam, new Color(1f, 0.85f, 0.62f, 1));
            window = Looks.Unlit(Color.white, Looks.Window);
            frame = Looks.Toon("#FFFFFF", 0.003f);
            sign = Looks.Toon(Color.white, Looks.Sign, 0.002f);
            box = Looks.Toon("#3D8BD6", 0.003f);
            cone = MakeCone(0.2f, 1.25f, 24);
            font = Resources.Load<Font>("Fonts/LilitaOne");

            var f = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Looks.Kill(f.GetComponent<Collider>());
            f.name = "Floor"; floor = f.transform; floor.localScale = Vector3.one * 8;
            var fm = new Material(Looks.Toon(Color.white, Looks.Checker, 0f)) { mainTextureScale = new Vector2(40, 40) };
            f.GetComponent<Renderer>().sharedMaterial = fm;
            var c = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Looks.Kill(c.GetComponent<Collider>());
            c.name = "Ceiling"; ceiling = c.transform; ceiling.localScale = Vector3.one * 8; ceiling.rotation = Quaternion.Euler(180, 0, 0);
            c.GetComponent<Renderer>().sharedMaterial = Looks.Unlit(Looks.Hex("#E7C39A"));
            c.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            BuildDust();
        }

        // ---------- per frame ----------
        public void Follow(Vector3 waiterPos, Camera cam, float speed)
        {
            floor.position = new Vector3(Mathf.Round(cam.transform.position.x / 2) * 2, 0, Mathf.Round(cam.transform.position.z / 2) * 2);
            ceiling.position = new Vector3(floor.position.x, RoomHeight, floor.position.z);
            var camRot = cam.transform.rotation;
            for (int i = billboards.Count - 1; i >= 0; i--)
            {
                if (billboards[i] == null) { billboards.RemoveAt(i); continue; }
                billboards[i].rotation = camRot;
            }
            for (int i = fans.Count - 1; i >= 0; i--)
            {
                if (fans[i] == null) { fans.RemoveAt(i); continue; }
                fans[i].Rotate(0, Time.deltaTime * 160f, 0, Space.Self);
            }
            var fwd = cam.transform.forward; fwd.y = 0;
            dust.transform.position = waiterPos + fwd.normalized * 6 + Vector3.up * 2.2f;
            dust.transform.rotation = Quaternion.LookRotation(fwd.sqrMagnitude > 0 ? fwd : Vector3.forward);
        }

        public void SetLight(bool on)
        {
            light = on;
            foreach (var d in decor) if (d) d.SetActive(!on);
            foreach (var b in beams) if (b) b.SetActive(!on);
            dust.gameObject.SetActive(!on);
        }

        // ---------- chunks ----------
        public void Clear()
        {
            foreach (var n in new List<int>(chunks.Keys)) Drop(n);
            given = 0; nextSide = 1;
        }

        public void UpdateChunks(float s, bool playing)
        {
            int first = Mathf.FloorToInt((s - 14) / Chunk), last = Mathf.FloorToInt((s + 46) / Chunk);
            foreach (var n in new List<int>(chunks.Keys)) if (n < first) Drop(n);
            for (int n = Mathf.Max(0, first); n <= last; n++) if (!chunks.ContainsKey(n)) Build(n, playing);
        }

        void Drop(int n)
        {
            if (!chunks.TryGetValue(n, out var go)) return;
            Object.Destroy(go);
            foreach (var m in chunkMeshes[n]) Object.Destroy(m);
            chunks.Remove(n); chunkMeshes.Remove(n);
            Colleagues.RemoveAll(c => c.chunk == n);
            Pickups.RemoveAll(c => c.chunk == n);
            Coins.RemoveAll(c => c.chunk == n);
            Obstacles.RemoveAll(c => c.chunk == n);
            decor.RemoveAll(d => d == null || d.transform.IsChildOf(go.transform));
            beams.RemoveAll(d => d == null || d.transform.IsChildOf(go.transform));
        }

        void Build(int n, bool playing)
        {
            float s0 = n * Chunk, s1 = s0 + Chunk, mid = s0 + Chunk / 2;
            track.Extend(s1 + 2);
            var g = new GameObject("Chunk " + n);
            g.transform.SetParent(root, false);
            chunks[n] = g;
            var meshes = chunkMeshes[n] = new List<Mesh>();
            var t = g.transform;
            Ribbon(t, meshes, s0, s1, -1.45f, 1.45f, 0.006f, 0.006f, carpet, false, true);
            Ribbon(t, meshes, s0, s1, WallU, WallU, 0, RoomHeight, wall, false, false);
            Ribbon(t, meshes, s0, s1, -WallU, -WallU, 0, RoomHeight, wall, true, false);
            // a wooden chair rail and a dark baseboard give the walls some depth
            foreach (int side in new[] { -1, 1 })
            {
                float u = side * (WallU - 0.04f);
                Ribbon(t, meshes, s0, s1, u, u, 1.6f, 1.72f, rail, side < 0, false);
                Ribbon(t, meshes, s0, s1, u, u, 0f, 0.16f, baseboard, side < 0, false);
            }
            // ceiling beams across the room
            foreach (float bs in new[] { s0 + 1.5f, s0 + 4.5f })
            {
                var b = Looks.Primitive(PrimitiveType.Cube, wood, t, Vector3.zero, new Vector3(WallU * 2, 0.22f, 0.3f));
                b.transform.position = track.Place(bs, 0, RoomHeight - 0.11f);
                b.transform.rotation = Quaternion.Euler(0, track.At(bs).h * Mathf.Rad2Deg, 0);
            }
            if (n % 3 == 1) Fan(t, mid);
            else if (n % 2 == 0) Lamp(t, s0, 0, false);

            foreach (int side in new[] { -1, 1 })
            {
                int kind = (n * 7 + (side > 0 ? 3 : 0)) % 5;
                if (kind == 0) Kitchen(t, mid, side);
                else if (kind == 2) Booth(t, mid, side);
                else Dining(t, mid, side);
                WallDecor(t, n, s0 + 1, side);
                var plant = Furn("pottedPlant", 1.35f, t);
                plant.transform.position = track.Place(s1 - 0.4f, side * 4.6f);
            }
            if (playing && n >= 2) Fill(n, t, s0);
        }

        GameObject Furn(string name, float size, Transform parent, int axis = 1)
        {
            var go = Looks.Fit(Looks.Prefab("Furniture/" + name), size, axis, false, out _);
            Looks.Toonify(go, 0.0028f);
            go.transform.SetParent(parent, true);
            return go;
        }

        void FaceTo(Transform t, Vector3 target)
        {
            var d = target - t.position; d.y = 0;
            if (d.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(d);
        }

        void FacePath(Transform t, float s) => FaceTo(t, track.Place(s, 0, t.position.y));

        void Seat(Transform parent, Vector3 at, Vector3 lookAt)
        {
            if (Random.value > 0.65f) return;
            var guest = Character.Make(Guests[Random.Range(0, Guests.Length)]);
            guest.Root.transform.position = at;
            FaceTo(guest.Root.transform, lookAt);
            guest.Root.transform.SetParent(parent, true);
            guest.Play("sit", 0); guest.Seek(Random.value * 2);
            decor.Add(guest.Root); guest.Root.SetActive(!light);
        }

        void TableFood(Transform parent, Vector3 at)
        {
            string[] onTable = { "plate", "burger", "cake", "coffee", "fries", "soda", "donut", "pancakes" };
            var food = Food.Build(onTable[Random.Range(0, onTable.Length)]);
            food.transform.position = at;
            food.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            food.transform.SetParent(parent, true);
            decor.Add(food); food.SetActive(!light);
        }

        void Dining(Transform parent, float s, int side)
        {
            float u = side * 3.1f;
            var tp = track.Place(s, u);
            var rug = Furn("rugRound", 2.1f, parent, 0); rug.transform.position = tp + Vector3.up * 0.005f;
            var table = Furn("tableRound", 0.84f, parent); table.transform.position = tp;
            foreach (float ds in new[] { -1.05f, 1.05f })
            {
                var cp = track.Place(s + ds, u);
                var chair = Furn("chairRounded", 1.05f, parent); chair.transform.position = cp; FaceTo(chair.transform, tp);
                var away = cp - tp; away.y = 0; away.Normalize();
                Seat(parent, cp + away * 0.05f + Vector3.up * 0.08f, tp);
            }
            TableFood(parent, track.Place(s + 0.25f, u + 0.2f, 0.94f));
            TableFood(parent, track.Place(s - 0.2f, u - 0.25f, 0.94f));
            Lamp(parent, s, u, true);
        }

        // a sofa against the wall with a table in front of it
        void Booth(Transform parent, float s, int side)
        {
            var sofa = Furn("loungeSofa", 1.0f, parent);
            sofa.transform.position = track.Place(s, side * 4.55f);
            FacePath(sofa.transform, s);
            var tp = track.Place(s, side * 3.35f);
            var table = Furn("tableRound", 0.84f, parent); table.transform.position = tp;
            foreach (float ds in new[] { -0.5f, 0.5f }) Seat(parent, track.Place(s + ds, side * 4.4f, 0.12f), tp);
            TableFood(parent, track.Place(s + 0.2f, side * 3.35f, 0.94f));
            TableFood(parent, track.Place(s - 0.25f, side * 3.25f, 0.94f));
            Lamp(parent, s, side * 3.4f, true);
        }

        void Kitchen(Transform parent, float s, int side)
        {
            foreach (float ds in new[] { -1f, 0f, 1f })
            {
                var bar = Furn("kitchenBar", 0.97f, parent); bar.transform.position = track.Place(s + ds, side * 2.95f);
                FacePath(bar.transform, s + ds);
            }
            var stove = Furn("kitchenStove", 1.04f, parent); stove.transform.position = track.Place(s + 0.6f, side * 4.3f);
            FacePath(stove.transform, s + 0.6f);
            var upper = Furn("kitchenCabinetUpper", 0.75f, parent); upper.transform.position = track.Place(s + 0.6f, side * 4.65f, 1.9f);
            FacePath(upper.transform, s + 0.6f);
            var fridge = Furn("kitchenFridgeLarge", 2.6f, parent); fridge.transform.position = track.Place(s - 1.5f, side * 4.4f);
            FacePath(fridge.transform, s - 1.5f);
            var coffee = Furn("kitchenCoffeeMachine", 0.55f, parent); coffee.transform.position = track.Place(s - 0.9f, side * 3.0f, 0.97f);
            FacePath(coffee.transform, s - 0.9f);
            var toaster = Furn("toaster", 0.32f, parent); toaster.transform.position = track.Place(s + 0.05f, side * 3.05f, 0.97f);
            FacePath(toaster.transform, s);
            var blender = Furn("kitchenBlender", 0.48f, parent); blender.transform.position = track.Place(s + 1.1f, side * 3.1f, 0.97f);
            FacePath(blender.transform, s + 1.1f);
            string[] onCounter = { "cake", "burger", "pancakes", "turkey" };
            foreach (float ds in new[] { -0.4f, 0.55f })
            {
                var food = Food.Build(onCounter[Random.Range(0, onCounter.Length)]);
                food.transform.position = track.Place(s + ds, side * 2.9f, 1.08f);
                food.transform.SetParent(parent, true);
                decor.Add(food); food.SetActive(!light);
            }
            Lamp(parent, s, side * 3.2f, true);
        }

        // windows, menu boards, TVs, neon signs, bookcases and floor lamps along the walls
        void WallDecor(Transform parent, int n, float s, int side)
        {
            int pick = (n * 3 + (side > 0 ? 1 : 0)) % 4;
            if (pick == 0) WallWindow(parent, s, side);
            else if (pick == 1) Neon(parent, s, side, NeonWords[(n + (side > 0 ? 2 : 0)) % NeonWords.Length]);
            else if (pick == 2)
            {
                var shelf = Looks.Primitive(PrimitiveType.Cube, wood, parent, Vector3.zero, new Vector3(1.1f, 0.06f, 0.4f));
                shelf.transform.position = track.Place(s, side * (WallU - 0.22f), 2.05f); FacePath(shelf.transform, s);
                var tv = Furn("televisionVintage", 0.62f, parent); tv.transform.position = track.Place(s, side * (WallU - 0.28f), 2.08f);
                FacePath(tv.transform, s);
                var radio = Furn("radio", 0.25f, parent); radio.transform.position = track.Place(s + 0.45f, side * (WallU - 0.25f), 2.08f);
                FacePath(radio.transform, s);
            }
            else
            {
                var bc = Furn("bookcaseOpenLow", 0.95f, parent); bc.transform.position = track.Place(s, side * (WallU - 0.3f));
                FacePath(bc.transform, s);
                var p = Furn(Random.value < 0.5f ? "plantSmall1" : "plantSmall2", 0.45f, parent);
                p.transform.position = track.Place(s + 0.25f, side * (WallU - 0.3f), 0.95f);
                var lamp = Furn("lampRoundFloor", 1.7f, parent); lamp.transform.position = track.Place(s - 0.9f, side * (WallU - 0.35f));
                var halo = Looks.Primitive(PrimitiveType.Quad, glow, parent, Vector3.zero, Vector3.one * 1.1f);
                halo.transform.position = track.Place(s - 0.9f, side * (WallU - 0.35f), 1.6f);
                billboards.Add(halo.transform);
                var rack = Furn("coatRackStanding", 1.75f, parent); rack.transform.position = track.Place(s + 1.1f, side * (WallU - 0.35f));
            }
        }

        void Neon(Transform parent, float s, int side, string word)
        {
            var at = track.Place(s, side * (WallU - 0.06f), 3.25f);
            var center = track.Place(s, 0, 3.25f);
            bool pink = word.Length % 2 == 0;
            var halo = Looks.Primitive(PrimitiveType.Quad, pink ? neonGlowPink : neonGlowBlue, parent, Vector3.zero, new Vector3(2.6f, 1.3f, 1));
            halo.transform.position = at + (center - at).normalized * 0.02f;
            FaceTo(halo.transform, center); halo.transform.Rotate(0, 180, 0);
            if (font == null) return;
            var go = new GameObject("neon " + word);
            go.transform.SetParent(parent, false);
            go.transform.position = at + (center - at).normalized * 0.05f;
            FaceTo(go.transform, center);
            go.transform.Rotate(0, 180, 0);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font; tm.text = word; tm.fontSize = 96; tm.characterSize = 0.065f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.color = pink ? new Color(1f, 0.55f, 0.8f) : new Color(0.55f, 0.95f, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = NeonMaterial();
        }

        Material neonMat;
        Material NeonMaterial()
        {
            if (neonMat == null)
            {
                font.RequestCharactersInTexture(string.Concat(NeonWords), 96);
                neonMat = new Material(Shader.Find("WobbleWaiter/Neon")) { mainTexture = font.material.mainTexture };
                Font.textureRebuilt += f => { if (f == font && neonMat != null) neonMat.mainTexture = font.material.mainTexture; };
            }
            return neonMat;
        }

        void Fan(Transform parent, float s)
        {
            var fan = Furn("ceilingFan", 0.55f, parent);
            fan.transform.position = track.Place(s, 0, RoomHeight - 0.55f);
            fans.Add(fan.transform);
        }

        void Lamp(Transform parent, float s, float u, bool withBeam)
        {
            var lamp = Furn("lampSquareCeiling", 0.85f, parent);
            var p = track.Place(s, u, RoomHeight - 0.85f);
            lamp.transform.position = p;
            float by = RoomHeight - 0.8f;
            var halo = Looks.Primitive(PrimitiveType.Quad, glow, parent, Vector3.zero, Vector3.one * 1.7f);
            halo.transform.position = new Vector3(p.x, by, p.z);
            billboards.Add(halo.transform);
            if (!withBeam) return;
            var b = new GameObject("beam", typeof(MeshFilter), typeof(MeshRenderer));
            b.GetComponent<MeshFilter>().sharedMesh = cone;
            var br = b.GetComponent<MeshRenderer>(); br.sharedMaterial = beam; br.shadowCastingMode = ShadowCastingMode.Off;
            b.transform.SetParent(parent, true);
            float h = by - 0.2f;
            b.transform.position = new Vector3(p.x, by - h / 2, p.z);
            b.transform.localScale = new Vector3(1, h, 1);
            beams.Add(b); b.SetActive(!light);
        }

        void WallWindow(Transform parent, float s, int side)
        {
            var center = track.Place(s, 0, 2.6f);
            var at = track.Place(s, side * (WallU - 0.06f), 2.6f);
            var fr = Looks.Primitive(PrimitiveType.Cube, frame, parent, Vector3.zero, new Vector3(1.35f, 1.15f, 0.08f));
            fr.transform.position = at; FaceTo(fr.transform, center);
            var pane = Looks.Primitive(PrimitiveType.Quad, window, parent, Vector3.zero, new Vector3(1.2f, 1.0f, 1));
            pane.transform.position = at + (center - at).normalized * 0.05f;
            FaceTo(pane.transform, center);
            pane.transform.Rotate(0, 180, 0);
        }

        // ---------- gameplay things on the path ----------
        void Fill(int n, Transform parent, float s0)
        {
            float sA = s0 + 1.5f, sB = s0 + 4.5f;
            int lane() => Random.Range(-1, 2);
            if (s0 < 30)
            {
                AddColleague(n, parent, sA, nextSide); nextSide = -nextSide;
                if (Random.value < 0.6f) AddCoins(n, parent, sB, lane() * Lane);
                return;
            }
            float roll = Random.value;
            int side() => Random.value < 0.5f ? -1 : 1;
            if (roll < 0.34f) { AddColleague(n, parent, sA, side()); AddObstacle(n, parent, sB); }
            else if (roll < 0.6f) { AddPickup(n, parent, sA, lane() * Lane); AddCoins(n, parent, sB, lane() * Lane); }
            else if (roll < 0.82f) { AddObstacle(n, parent, sA); AddColleague(n, parent, sB, side()); }
            else { AddCoins(n, parent, sA, side() * Lane); AddObstacle(n, parent, sB + 0.5f); }
        }

        void AddColleague(int n, Transform parent, float s, int side)
        {
            var crew = Crew[Random.Range(0, Crew.Length)];
            var ch = Character.Make(crew.name, crew.hat);
            ch.Root.transform.position = track.Place(s, side * 2.05f);
            FaceTo(ch.Root.transform, track.Place(s - 2.5f, 0));
            ch.Root.transform.SetParent(parent, true);
            string kind = given < 3 ? "plate" : Food.Random();
            given++;
            var item = Food.Build(kind);
            item.transform.SetParent(ch.Root.transform, false);
            item.transform.localPosition = new Vector3(0, 0.72f + Food.Get(kind).size.y / 2, 0.42f);
            ch.Play("holding-both", 0); ch.Seek(Random.value);
            var z = Looks.Primitive(PrimitiveType.Quad, zone, parent, Vector3.zero, Vector3.one * 1.5f);
            z.transform.position = track.Place(s - 0.6f, side * 0.95f, 0.015f);
            z.transform.rotation = Quaternion.Euler(90, 0, 0);
            Colleagues.Add(new Colleague { chunk = n, s = s, side = side, kind = kind, ch = ch, item = item, zone = z });
        }

        void AddPickup(int n, Transform parent, float s, float u)
        {
            string kind = Food.Random();
            var g = new GameObject("pickup");
            g.transform.SetParent(parent, false);
            g.transform.position = track.Place(s, u, 1.1f);
            var food = Food.Build(kind);
            food.transform.SetParent(g.transform, false);
            var halo = Looks.Primitive(PrimitiveType.Quad, glow, g.transform, new Vector3(0, -0.1f, 0), Vector3.one * 0.9f);
            billboards.Add(halo.transform);
            var r = Looks.Primitive(PrimitiveType.Cylinder, ring, g.transform, new Vector3(0, -0.36f, 0), new Vector3(0.62f, 0.01f, 0.62f));
            r.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Pickups.Add(new Pickup { chunk = n, s = s, u = u, kind = kind, root = g, food = food, phase = Random.value * 6 });
        }

        void AddCoins(int n, Transform parent, float s, float u)
        {
            for (int i = 0; i < 5; i++)
            {
                float cs = s + i * 0.7f - 1.4f;
                var holder = new GameObject("coin").transform;
                holder.SetParent(parent, false);
                holder.position = track.Place(cs, u, 0.9f);
                var disc = Looks.Primitive(PrimitiveType.Cylinder, coin, holder, Vector3.zero, new Vector3(0.32f, 0.022f, 0.32f));
                disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
                Coins.Add(new Coin { chunk = n, s = cs, u = u, t = holder });
            }
        }

        void AddObstacle(int n, Transform parent, float s)
        {
            string[] types = { "box", "box", "puddle", "bucket", "bump" };
            string type = types[Random.Range(0, types.Length)];
            int lane = Random.Range(-1, 2);
            float u = lane * Lane, w;
            var g = new GameObject(type);
            if (type == "box")
            {
                Furn("cardboardBoxClosed", 0.75f, g.transform);
                if (Random.value < 0.5f) { var b = Furn("cardboardBoxClosed", 0.55f, g.transform); b.transform.localPosition = new Vector3(0.05f, 0.75f, 0); b.transform.localRotation = Quaternion.Euler(0, 30, 0); }
                w = 0.85f;
            }
            else if (type == "bucket")
            {
                Looks.Primitive(PrimitiveType.Cylinder, box, g.transform, new Vector3(0, 0.21f, 0), new Vector3(0.5f, 0.21f, 0.5f));
                Looks.Primitive(PrimitiveType.Cylinder, Looks.Toon("#9ED8F5", 0f), g.transform, new Vector3(0, 0.42f, 0), new Vector3(0.46f, 0.01f, 0.46f));
                var stick = Looks.Primitive(PrimitiveType.Cylinder, Looks.Toon("#C98B3C", 0.002f), g.transform, new Vector3(0.18f, 0.75f, 0), new Vector3(0.04f, 0.7f, 0.04f));
                stick.transform.localRotation = Quaternion.Euler(0, 0, -20);
                w = 0.65f;
            }
            else if (type == "puddle")
            {
                var p = Looks.Primitive(PrimitiveType.Cylinder, puddle, g.transform, new Vector3(0, 0.012f, 0), new Vector3(1.5f, 0.004f, 1.05f));
                p.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var signRoot = new GameObject("sign").transform;
                signRoot.SetParent(g.transform, false);
                signRoot.localPosition = new Vector3(lane == 1 ? -0.95f : 0.95f, 0, 0);
                foreach (int d in new[] { -1, 1 })
                {
                    var board = Looks.Primitive(PrimitiveType.Cube, sign, signRoot, new Vector3(0, 0.3f, d * 0.1f), new Vector3(0.42f, 0.62f, 0.02f));
                    board.transform.localRotation = Quaternion.Euler(d * 18, 0, 0);
                }
                w = 1.25f;
            }
            else
            {
                var roll = Looks.Primitive(PrimitiveType.Cylinder, Looks.Toon("#E8A817", 0.0025f), g.transform, new Vector3(0, 0.09f, 0), new Vector3(0.18f, 1.45f, 0.18f));
                roll.transform.localRotation = Quaternion.Euler(0, 0, 90);
                u = 0; w = 9;
            }
            g.transform.SetParent(parent, false);
            g.transform.position = track.Place(s, u);
            g.transform.rotation = Quaternion.Euler(0, track.At(s).h * Mathf.Rad2Deg, 0);
            Obstacles.Add(new Obstacle { chunk = n, s = s, u = u, w = w, type = type, g = g });
        }

        // ---------- meshes ----------
        void Ribbon(Transform parent, List<Mesh> owned, float s0, float s1, float u0, float u1, float y0, float y1, Material m, bool flip, bool floorUv)
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            int row = 0;
            for (float s = s0; s <= s1 + 1e-4f; s += 0.5f, row++)
            {
                verts.Add(track.Place(s, u0, y0)); verts.Add(track.Place(s, u1, y1));
                if (floorUv) { uvs.Add(new Vector2(0, s / 2)); uvs.Add(new Vector2(1, s / 2)); }
                else { uvs.Add(new Vector2(s / 3, y0 / RoomHeight)); uvs.Add(new Vector2(s / 3, y1 / RoomHeight)); }
                if (row == 0) continue;
                int i = (row - 1) * 2;
                if (!flip) tris.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 });
                else tris.AddRange(new[] { i, i + 1, i + 2, i + 1, i + 3, i + 2 });
            }
            var mesh = new Mesh { name = "ribbon" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            owned.Add(mesh);
            var go = new GameObject("ribbon", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.receiveShadows = true;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Mesh MakeCone(float top, float bottom, int seg)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
                v.Add(new Vector3(cx * bottom, -0.5f, cz * bottom)); uv.Add(new Vector2(i / (float)seg, 0));
                v.Add(new Vector3(cx * top, 0.5f, cz * top)); uv.Add(new Vector2(i / (float)seg, 1));
                if (i == 0) continue;
                int k = (i - 1) * 2;
                t.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
            }
            var m = new Mesh { name = "beam" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }

        void BuildDust()
        {
            var go = new GameObject("Dust");
            dust = go.AddComponent<ParticleSystem>();
            var main = dust.main;
            main.startLifetime = 6f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 200;
            main.startColor = new Color(1f, 0.95f, 0.8f, 0.8f);
            var em = dust.emission; em.rateOverTime = 30;
            var sh = dust.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(8, 4, 20);
            var noise = dust.noise; noise.enabled = true; noise.strength = 0.15f; noise.frequency = 0.3f;
            var col = dust.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = glow;
        }
    }
}
