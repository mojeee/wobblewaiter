using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WobbleWaiter
{
    // Wobble Waiter: run along a winding path through a diner with a tray on your head.
    // Tilt the phone left/right to steer and tip the tray sideways, forward/back to tip it front and back.
    // Catch food, grab coins and dodge the mess.
    public class Game : MonoBehaviour
    {
        enum State { Ready, Play, Dying, Over }

        const float Edge = 1.4f;              // how far the waiter can move off the middle of the carpet
        const float TrayRadius = 0.6f;
        const float MaxTrayTilt = 24f;        // degrees
        static readonly Vector3 Lab = new Vector3(0, -200, 0);   // where the tray physics runs, away from everything else

        State state = State.Ready;
        bool paused;
        Track track;
        World world;
        Ui ui;
        Sfx sfx;
        Ads ads;
        Camera cam;
        Light sun;
        PostFx post;
        Character waiter;
        Transform stackRoot, trayVis;
        Rigidbody trayRb;
        PhysicsMaterial foodMat;
        TiltInput tilt;

        class Item { public Rigidbody rb; public BoxCollider col; public Transform vis, eyes; public string kind; public float squash, born; }
        class Flight { public Transform t; public string kind; public Vector3 from; public Quaternion q0; public float lx, lz, k; }
        class Debris { public Transform t; public string kind; public Vector3 v; public float spin, half; public bool landed; }
        class Shard { public Transform t; public Vector3 v, spin; public float life; }

        readonly List<Item> items = new List<Item>();
        readonly List<Flight> flights = new List<Flight>();
        readonly List<Debris> debris = new List<Debris>();
        readonly List<Shard> shards = new List<Shard>();

        float sW, uW, uVel, uSmoothVel, latG, lonG, camH, speed, prevSpeed, distance, phase, stumble, simTime;
        float trayRoll, trayPitch, trayRollVel, trayPitchVel;
        float steer, slowUntil, shake, dyingAt, playStart, lastBack = -9;
        int lives, onTray, best, catches, coinsRun, record, wallet;
        bool continued;

        int Score => catches * 10 + coinsRun * 5 + Mathf.FloorToInt(distance);

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;
            Time.fixedDeltaTime = 1f / 60f;
            Physics.defaultSolverIterations = 14;
            Physics.defaultSolverVelocityIterations = 4;
            Physics.sleepThreshold = 0.002f;
            tilt = new TiltInput();
            record = PlayerPrefs.GetInt("ww_best", 0);
            wallet = PlayerPrefs.GetInt("ww_coins", 0);

            Looks.Init();
            Food.Init();
            BuildScene();

            sfx = gameObject.AddComponent<Sfx>();
            Settings.ClickSound = () => sfx.Click();
            ui = gameObject.AddComponent<Ui>();
            ui.OnPlay = OnPlayPressed;
            ui.OnContinue = OnContinuePressed;
            ui.OnDouble = OnDoublePressed;
            ui.OnPause = Pause;
            ui.OnResume = Resume;
            ui.OnHome = GoHome;
            ui.OnCalibrate = () => { tilt.Calibrate(); ui.Toast("Straight position saved"); };
            ui.OnSettingsChanged = ApplySettings;
            ui.Build();
            ads = new Ads(ui);

            track = new Track();
            world = new World(track);
            BuildWaiter();
            BuildTrayPhysics();
            BuildShards();
            ApplySettings();
            NewRun(false);
            ui.ShowStart(record, wallet);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-wwselftest") >= 0) { Application.runInBackground = true; StartCoroutine(SelfTest()); }
        }

        // Started with -wwselftest: plays a short round by itself, saves screenshots next to the app, then quits.
        // Used to check a build without a phone.
        float testSteer = float.NaN;
        System.Collections.IEnumerator SelfTest()
        {
            string dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "SelfTest");
            System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "1-start.png"));
            yield return new WaitForSecondsRealtime(1.5f);
            ui.ShowSettings();
            yield return new WaitForSecondsRealtime(1f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "2-settings.png"));
            yield return new WaitForSecondsRealtime(1f);
            ui.CloseSettings();
            StartRun();
            float[] pattern = { 0, 1, 1, -1, -1, 0, 1, -1, 0, 0, -1, 1 };
            for (int i = 0; i < pattern.Length; i++)
            {
                testSteer = pattern[i];
                yield return new WaitForSecondsRealtime(1.5f);
                if (i == 3 || i == 7 || i == 11) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, (i / 4 + 3) + "-play.png"));
            }
            Pause();
            yield return new WaitForSecondsRealtime(1f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "6-pause.png"));
            yield return new WaitForSecondsRealtime(1f);
            Resume();
            testSteer = float.NaN;
            ShowOver();
            yield return new WaitForSecondsRealtime(1.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "7-over.png"));
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log($"Wobble Waiter self-test: state={state} distance={distance:F0} onTray={onTray} best={best} lives={lives} score={Score} coins={coinsRun} items={items.Count}");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        // ---------- setup ----------
        void BuildScene()
        {
            var fog = Looks.Hex("#EDB984");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = 8;
            RenderSettings.fogEndDistance = 32;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Looks.Hex("#9A7C6A");
            RenderSettings.skybox = null;

            cam = new GameObject("Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = fog;
            cam.fieldOfView = 56;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 60;
            cam.tag = "MainCamera";
            post = cam.gameObject.AddComponent<PostFx>();

            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Looks.Hex("#FFDDB0");
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.5f;
            sun.transform.rotation = Quaternion.Euler(55, -35, 0);
        }

        void BuildWaiter()
        {
            waiter = Character.Make("male-d");
            stackRoot = new GameObject("Stack").transform;
            trayVis = new GameObject("Tray").transform;
            trayVis.SetParent(stackRoot, false);
            Looks.Primitive(PrimitiveType.Cylinder, Looks.Toon("#DDE5EA", 0.003f), trayVis, new Vector3(0, -0.02f, 0), new Vector3(TrayRadius * 2, 0.02f, TrayRadius * 2));
            Looks.Primitive(PrimitiveType.Cylinder, Looks.Toon("#AEBBC4", 0.003f), trayVis, new Vector3(0, -0.035f, 0), new Vector3(TrayRadius * 2 + 0.05f, 0.012f, TrayRadius * 2 + 0.05f));
        }

        // The food lives in its own little physics world at `Lab`, in the tray's frame: x = right, y = up, z = forward.
        // Turns, braking and speeding up are applied as a sideways or forward push on gravity.
        void BuildTrayPhysics()
        {
            foodMat = new PhysicsMaterial("food") { dynamicFriction = 0.55f, staticFriction = 0.7f, bounciness = 0.02f, frictionCombine = PhysicsMaterialCombine.Average };
            var t = new GameObject("tray physics");
            t.transform.position = Lab;
            trayRb = t.AddComponent<Rigidbody>();
            trayRb.isKinematic = true;
            trayRb.interpolation = RigidbodyInterpolation.Interpolate;
            var disc = new GameObject("disc");
            disc.transform.SetParent(t.transform, false);
            disc.transform.localPosition = new Vector3(0, -0.02f, 0);
            var mc = disc.AddComponent<MeshCollider>();
            mc.sharedMesh = CylinderMesh();
            mc.convex = true;
            disc.transform.localScale = new Vector3(TrayRadius * 2, 0.02f, TrayRadius * 2);
            mc.sharedMaterial = foodMat;
            AddLipRing(t.transform, 20);
        }

        void AddLipRing(Transform t, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2;
                var seg = new GameObject("lip");
                seg.transform.SetParent(t, false);
                seg.transform.localPosition = new Vector3(Mathf.Cos(a) * (TrayRadius - 0.01f), 0.04f, Mathf.Sin(a) * (TrayRadius - 0.01f));
                seg.transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
                var b = seg.AddComponent<BoxCollider>();
                b.size = new Vector3(0.03f, 0.08f, TrayRadius * 2 * Mathf.PI / count + 0.01f);
                b.sharedMaterial = foodMat;
            }
        }

        static Mesh CylinderMesh()
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var m = g.GetComponent<MeshFilter>().sharedMesh;
            Destroy(g);
            return m;
        }

        void BuildShards()
        {
            string[] cols = { "#FFFFFF", "#FFC93C", "#E8463B", "#7BC86C", "#8B5A3C" };
            for (int i = 0; i < 60; i++)
            {
                var c = Looks.Primitive(PrimitiveType.Cube, Looks.Toon(cols[i % cols.Length], 0f), null, Vector3.zero, Vector3.one * 0.045f);
                c.SetActive(false);
                shards.Add(new Shard { t = c.transform });
            }
        }

        void ApplySettings()
        {
            if (sfx != null) { sfx.SoundVolume = Settings.SoundVolume; sfx.MusicVolume = Settings.MusicVolume; }
            bool lightGfx = Settings.Light;
            Looks.SetOutlines(!lightGfx);
            QualitySettings.shadows = lightGfx ? ShadowQuality.Disable : ShadowQuality.All;
            if (sun != null) sun.shadows = lightGfx ? LightShadows.None : LightShadows.Soft;
            if (post != null) post.enabled = !lightGfx;
            Application.targetFrameRate = lightGfx ? 30 : 60;
            world?.SetLight(lightGfx);
        }

        // ---------- runs ----------
        void NewRun(bool playing)
        {
            foreach (var it in items) { Destroy(it.rb.gameObject); Destroy(it.vis.gameObject); }
            foreach (var f in flights) Destroy(f.t.gameObject);
            foreach (var d in debris) Destroy(d.t.gameObject);
            items.Clear(); flights.Clear(); debris.Clear();
            foreach (var s in shards) { s.life = 0; s.t.gameObject.SetActive(false); }

            lives = 3; onTray = 0; best = 0; catches = 0; coinsRun = 0; distance = 0; simTime = 0;
            sW = 6; uW = 0; uVel = 0; uSmoothVel = 0; latG = 0; lonG = 0; camH = 0; stumble = 0;
            trayRoll = trayPitch = trayRollVel = trayPitchVel = 0;
            continued = false; paused = false;
            Time.timeScale = 1;
            world.Clear();
            track.Reset();
            world.UpdateChunks(sW, playing);
            world.SetLight(Settings.Light);
            speed = prevSpeed = playing ? 2.6f : 1.8f;
            Physics.gravity = new Vector3(0, -9.81f, 0);
            trayRb.position = Lab; trayRb.rotation = Quaternion.identity;
            if (!playing)
            {
                float y = 0.02f;
                foreach (var k in new[] { "plate", "burger", "cake", "coffee" })
                {
                    var h = Food.Get(k).size.y;
                    AddItem(k, new Vector3(Random.Range(-0.05f, 0.05f), y + h / 2, Random.Range(-0.05f, 0.05f)), null);
                    y += h + 0.01f;
                }
            }
            waiter.Play("sprint", 0);
            ui.SetHud(0, 0, 0, 3);
        }

        void OnPlayPressed()
        {
            if (state == State.Ready) { StartRun(); return; }
            if (state == State.Over) ads.BetweenRounds(StartRun);
        }

        void StartRun()
        {
            NewRun(true);
            state = State.Play;
            playStart = Time.unscaledTime;
            if (Settings.TiltAutoZero) tilt.Calibrate();
            ui.ShowScreen(start: false, over: false, hudOn: true);
            sfx.RunStart();
        }

        void OnContinuePressed()
        {
            if (state != State.Over || continued) return;
            ads.ShowRewarded(ok =>
            {
                if (!ok) return;
                continued = true;
                lives = 1;
                state = State.Play;
                speed = 2.4f;
                playStart = Time.unscaledTime;
                ui.ShowScreen(start: false, over: false, hudOn: true);
                ui.SetHud(onTray, best, Score, lives);
                ui.Pop("KEEP GOING!", Ui.PopStyle.Big);
            });
        }

        void OnDoublePressed()
        {
            ads.ShowRewarded(ok =>
            {
                if (!ok) return;
                wallet += coinsRun;
                PlayerPrefs.SetInt("ww_coins", wallet);
                ui.HideDouble();
                ui.Pop("COINS x2!", Ui.PopStyle.Big);
                sfx.Jingle(true);
            });
        }

        void ShowOver()
        {
            state = State.Over;
            int score = Score;
            if (score > record) { record = score; PlayerPrefs.SetInt("ww_best", record); }
            wallet += coinsRun;
            PlayerPrefs.SetInt("ww_coins", wallet);
            PlayerPrefs.Save();
            int stars = score >= 700 ? 3 : score >= 350 ? 2 : score >= 120 ? 1 : 0;
            ui.ShowOver(best, score, record, stars, !continued && ads.RewardedReady, coinsRun > 0 && ads.RewardedReady);
            sfx.Jingle(false);
        }

        // ---------- pause, back and home ----------
        void Pause()
        {
            if (state != State.Play || paused) return;
            paused = true;
            Time.timeScale = 0;
            AudioListener.pause = true;
            ui.ShowPause(true);
        }

        void Resume()
        {
            if (!paused) return;
            paused = false;
            Time.timeScale = 1;
            AudioListener.pause = false;
            ui.ShowPause(false);
            if (Settings.TiltAutoZero) tilt.Calibrate();
            playStart = Time.unscaledTime - 2f;
        }

        void GoHome()
        {
            paused = false;
            AudioListener.pause = false;
            ui.ShowPause(false);
            NewRun(false);
            state = State.Ready;
            ui.ShowStart(record, wallet);
        }

        void OnApplicationPause(bool goingAway) { if (goingAway) Pause(); }

        // the phone's back button: close what's open, pause a run, or leave from the start screen
        void HandleBack()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            sfx.Back();
            if (ui.CloseTopPanel()) return;
            if (state == State.Play) { if (paused) Resume(); else Pause(); return; }
            if (state == State.Over) { GoHome(); return; }
            if (Time.unscaledTime - lastBack < 2f) { Application.Quit(); return; }
            lastBack = Time.unscaledTime;
            ui.Toast("Press back again to exit");
        }

        // ---------- items on the tray ----------
        Item AddItem(string kind, Vector3 local, Transform vis)
        {
            var d = Food.Get(kind);
            var go = new GameObject("food " + kind);
            go.transform.position = Lab + local;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Clamp(d.size.x * d.size.z * 8f, 0.2f, 1.2f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.maxDepenetrationVelocity = 1f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.2f;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Max(0.04f, d.size.x * 0.95f), Mathf.Max(0.03f, d.size.y * 0.95f), Mathf.Max(0.04f, d.size.z * 0.95f));
            box.sharedMaterial = foodMat;
            rb.linearVelocity = new Vector3(0, -0.3f, 0);
            if (vis == null) vis = Food.Build(kind).transform;
            vis.SetParent(stackRoot, false);
            var item = new Item { rb = rb, col = box, vis = vis, eyes = vis.Find("eyes"), kind = kind, born = simTime };
            go.AddComponent<ItemBody>().OnHit = v =>
            {
                if (v < 0.5f) return;
                item.squash = Mathf.Min(1, v / 2.4f);
                if (simTime - item.born > 0.15f) sfx.Land(v / 2f);
            };
            items.Add(item);
            return item;
        }

        Vector3 LocalOf(Rigidbody rb) => rb.position - Lab;

        float StackTop()
        {
            float top = 0.03f;
            foreach (var it in items)
            {
                var p = LocalOf(it.rb);
                if (new Vector2(p.x, p.z).magnitude < 0.9f) top = Mathf.Max(top, it.col.bounds.max.y - Lab.y);
            }
            return top;
        }

        void Fly(Transform t, string kind)
        {
            t.SetParent(null, true);
            flights.Add(new Flight { t = t, kind = kind, from = t.position, q0 = t.rotation, lx = Random.Range(-0.06f, 0.06f), lz = Random.Range(-0.05f, 0.05f) });
            sfx.Whoosh();
        }

        void Kick(float strength, float side, float forward)
        {
            foreach (var it in items)
            {
                float dir = side == 0 ? Random.Range(-1f, 1f) : side;
                it.rb.linearVelocity += new Vector3(dir * Random.Range(0.6f, 1.3f) * strength, Random.Range(0.8f, 1.6f) * strength, (forward + Random.Range(-0.4f, 0.4f)) * strength);
                it.rb.angularVelocity += Random.insideUnitSphere * 6f * strength;
            }
            trayRollVel += Random.Range(-40f, 40f) * strength;
            trayPitchVel += Random.Range(-30f, 30f) * strength;
        }

        void LoseItem(int i)
        {
            var it = items[i];
            items.RemoveAt(i);
            var v = stackRoot.TransformDirection(it.rb.linearVelocity) + Track.Forward(track.At(sW).h) * speed * 0.85f;
            Destroy(it.rb.gameObject);
            it.vis.SetParent(null, true);
            debris.Add(new Debris { t = it.vis, kind = it.kind, v = v, spin = Random.Range(-360f, 360f), half = Food.Get(it.kind).size.y / 2 });
        }

        void Crash(Debris d)
        {
            d.landed = true;
            var pos = d.t.position;
            if (d.kind == "plate")
            {
                Destroy(d.t.gameObject);
                var broken = Food.BrokenPlate().transform;
                broken.position = new Vector3(pos.x, 0.01f, pos.z);
                broken.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                d.t = broken;
            }
            else
            {
                d.t.position = new Vector3(pos.x, Mathf.Min(d.half, 0.06f) + 0.01f, pos.z);
                d.t.rotation = Quaternion.Euler(0, Random.Range(0, 360f), Random.value < 0.5f ? 80 : -80);
                d.t.localScale = new Vector3(1.1f, 0.7f, 1.1f);
            }
            Burst(new Vector3(pos.x, 0.1f, pos.z), Settings.Light ? 8 : 20);
            sfx.Smash(d.kind == "plate" || d.kind == "coffee");
            if (state != State.Play && state != State.Dying) return;
            lives = Mathf.Max(0, lives - 1);
            shake = 0.06f;
            SlowMo(0.5f);
            string[] words = { "OOPS!", "CRASH!", "SPLAT!", "WHOOPS!" };
            ui.Pop(words[Random.Range(0, words.Length)], Ui.PopStyle.Big);
            Vibrate();
            ui.SetHud(onTray, best, Score, lives);
            if (lives == 0 && state == State.Play) { state = State.Dying; dyingAt = Time.unscaledTime; SlowMo(1.1f); }
        }

        static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Settings.Vibration) Handheld.Vibrate();
#endif
        }

        void SlowMo(float seconds)
        {
            slowUntil = Time.unscaledTime + seconds;
            if (Time.timeScale > 0) Time.timeScale = 0.32f;
        }

        void Burst(Vector3 at, int count)
        {
            int made = 0;
            foreach (var s in shards)
            {
                if (made >= count) break;
                if (s.life > 0) continue;
                s.t.position = at; s.t.gameObject.SetActive(true);
                s.t.localScale = Vector3.one * 0.045f * Random.Range(0.6f, 1.4f);
                s.v = new Vector3(Random.Range(-2.4f, 2.4f), Random.Range(1.8f, 4.2f), Random.Range(-2.4f, 2.4f));
                s.spin = new Vector3(Random.Range(-500f, 500f), Random.Range(-500f, 500f), 0);
                s.life = Random.Range(0.9f, 1.4f);
                made++;
            }
        }

        void HitObstacle(Obstacle o)
        {
            o.hit = true;
            switch (o.type)
            {
                case "puddle":
                    ui.Pop("SLIP!", Ui.PopStyle.Big); Kick(0.55f, Random.value < 0.5f ? -1 : 1, 0); uVel += Random.Range(-2.5f, 2.5f);
                    sfx.Slip();
                    break;
                case "bump":
                    ui.Pop("BUMP!", Ui.PopStyle.Small); Kick(0.3f, 0, 0.3f);
                    sfx.Thud(0.6f);
                    break;
                default:
                    ui.Pop("OUCH!", Ui.PopStyle.Big); Kick(0.8f, uW > o.u ? 1 : -1, 0.8f); speed *= 0.55f; stumble = 1;
                    Burst(track.Place(o.s, o.u, 0.5f), 12);
                    if (o.type == "box") o.g.transform.Rotate(0, 0, uW > o.u ? -28 : 28);
                    sfx.Thud(1f);
                    Vibrate();
                    break;
            }
            shake = 0.08f;
        }

        // ---------- input ----------
        // touch and keys move the waiter at a steady speed; tilt sets where he is on the carpet
        float ReadTouchSteer()
        {
            float s = 0;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) s -= 1;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) s += 1;
            if (!ui.PointerOverButton())
            {
                if (Input.touchCount > 0)
                {
                    for (int i = 0; i < Input.touchCount; i++)
                    {
                        var t = Input.GetTouch(i);
                        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
                        s += t.position.x < Screen.width * 0.5f ? -1 : 1;
                    }
                }
                else if (Input.GetMouseButton(0)) s += Input.mousePosition.x < Screen.width * 0.5f ? -1 : 1;
            }
            return Mathf.Clamp(s, -1, 1);
        }

        bool UsingTilt => Settings.Tilt && SystemInfo.supportsAccelerometer && float.IsNaN(testSteer);

        void Update()
        {
            HandleBack();
            tilt.Update(Time.unscaledDeltaTime);
            steer = state == State.Play && !paused ? (float.IsNaN(testSteer) ? ReadTouchSteer() : testSteer) : 0;
            if (!paused && Time.timeScale > 0 && Time.timeScale < 1 && Time.unscaledTime > slowUntil) Time.timeScale = 1;
            if (state == State.Dying && Time.unscaledTime - dyingAt > 1.7f) ShowOver();
            if (state == State.Play) ui.SetHud(onTray, best, Score, lives);
            bool early = state == State.Play && Time.unscaledTime - playStart < 3f && !UsingTilt;
            ui.SetHints(early, steer < -0.2f, steer > 0.2f);
            bool live = state == State.Play && !paused;
            float side = live ? Mathf.Clamp01((Mathf.Abs(TipSide()) - 0.2f) / 0.35f) : 0;
            float front = live ? Mathf.Clamp01((Mathf.Abs(TipFront()) - 0.2f) / 0.35f) : 0;
            ui.SetDanger(TipSide() < 0 ? side : 0, TipSide() > 0 ? side : 0, TipFront() > 0 ? front : 0, TipFront() < 0 ? front : 0);
        }

        // how hard the food is being pushed to the right / forward, counting both the tray tilt and the run
        float TipSide() => latG - Mathf.Sin(trayRoll * Mathf.Deg2Rad);
        float TipFront() => lonG + Mathf.Sin(trayPitch * Mathf.Deg2Rad);

        // ---------- simulation ----------
        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            simTime += dt;
            bool playing = state == State.Play;

            float targetSpeed = state == State.Ready ? 1.8f : playing ? Mathf.Min(6.2f, 3.0f + distance * 0.012f) : 0;
            prevSpeed = speed;
            speed = Mathf.MoveTowards(speed, targetSpeed, (speed < targetSpeed ? 0.7f : 3f) * dt);
            sW += speed * dt;
            if (playing) distance += speed * dt;
            phase += speed * dt * 2.5f;
            stumble *= 0.95f;
            if (speed > 0.5f && Mathf.Repeat(phase, Mathf.PI) < Mathf.Repeat(phase - speed * dt * 2.5f, Mathf.PI)) sfx.Step(speed);

            // steering
            float prevVel = uVel;
            if (playing && UsingTilt)
            {
                float target = tilt.Steer(Settings.TiltSensitivity) * Edge;
                uW = Mathf.SmoothDamp(uW, target, ref uSmoothVel, 0.11f, 6f, dt);
                uVel = uSmoothVel;
            }
            else
            {
                uVel += (steer * 3.2f - uVel) * Mathf.Min(1, dt * 8);
                uW += uVel * dt;
                uSmoothVel = uVel;
            }
            if (Mathf.Abs(uW) > Edge) { uW = Mathf.Clamp(uW, -Edge, Edge); uVel = 0; uSmoothVel = 0; }
            float steerAcc = (uVel - prevVel) / dt;

            // pushes on the food: turns and steering sideways, braking and speeding up front/back
            float k = track.At(sW).k;
            float targetLat = Mathf.Clamp((speed * speed * k - steerAcc * 0.3f) / 9.81f, -0.75f, 0.75f);
            float targetLon = Mathf.Clamp(-(speed - prevSpeed) / dt / 9.81f, -0.6f, 0.6f);
            latG = Mathf.Lerp(latG, targetLat, Mathf.Min(1, dt * 6));
            lonG = Mathf.Lerp(lonG, targetLon, Mathf.Min(1, dt * 8));
            Physics.gravity = new Vector3(latG * 0.95f * 9.81f, -9.81f, lonG * 0.8f * 9.81f);

            // tray tilt: the player tips it with the phone; without tilt the waiter keeps it level against the push
            float lvl = Mathf.Min(1, onTray / 12f + distance / 500f), sw = Mathf.Min(1, speed / 2.5f);
            float drift = state == State.Ready ? 0 : (Mathf.Sin(simTime * 1.7f) * 3f + Mathf.Sin(simTime * 3.1f + 1) * 2f) * (0.3f + lvl);
            float rollTarget, pitchTarget;
            if (playing && UsingTilt)
            {
                rollTarget = -tilt.Roll * Settings.TiltSensitivity * 0.6f + latG * 10f + drift * 0.5f;
                pitchTarget = tilt.Pitch * Settings.TiltSensitivity * 0.6f + drift * 0.4f;
            }
            else
            {
                rollTarget = latG * 20f + drift;
                pitchTarget = -lonG * 15f + drift * 0.5f;
            }
            trayRoll = Mathf.SmoothDamp(trayRoll, Mathf.Clamp(rollTarget, -MaxTrayTilt, MaxTrayTilt), ref trayRollVel, 0.1f, Mathf.Infinity, dt);
            trayPitch = Mathf.SmoothDamp(trayPitch, Mathf.Clamp(pitchTarget, -MaxTrayTilt, MaxTrayTilt), ref trayPitchVel, 0.1f, Mathf.Infinity, dt);
            var bob = new Vector3(Mathf.Sin(phase * 0.5f) * (0.017f + 0.045f * lvl) * sw, -Mathf.Abs(Mathf.Sin(phase)) * (0.011f + 0.023f * lvl) * sw, 0);
            trayRb.MovePosition(Lab + bob);
            trayRb.MoveRotation(Quaternion.Euler(trayPitch, 0, trayRoll));

            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                it.squash *= 0.86f;
                var p = LocalOf(it.rb);
                if (p.y < -0.5f || new Vector2(p.x - bob.x, p.z).magnitude > 1.3f) LoseItem(i);
            }

            world.UpdateChunks(sW, playing);
            CheckWorld(dt, playing);
            StepFlights(dt);
            StepDebris(dt);

            int n = 0;
            foreach (var it in items)
            {
                var p = LocalOf(it.rb);
                if (simTime - it.born > 0.5f && p.y > -0.05f && new Vector2(p.x - bob.x, p.z).magnitude < 0.75f && it.rb.linearVelocity.magnitude < 0.85f) n++;
            }
            if (n != onTray)
            {
                onTray = n;
                if (playing && onTray > best)
                {
                    best = onTray;
                    string word = best == 5 ? "GREAT!" : best == 10 ? "AMAZING!" : best == 15 ? "SUPER STACK!" : best == 20 ? "LEGEND!" : null;
                    if (word != null) { ui.Pop(word, Ui.PopStyle.Banner); sfx.Milestone(); }
                }
            }
            shake *= 0.88f;
        }

        void CheckWorld(float dt, bool playing)
        {
            foreach (var c in world.Colleagues)
            {
                if (c.state != 0 || !playing || sW < c.s - 1.4f) continue;
                if (uW * c.side > 0.3f)
                {
                    c.state = 1;
                    Fly(c.item.transform, c.kind);
                    c.ch.Play(Random.value < 0.5f ? "emote-yes" : "interact-right", 0.15f);
                }
                else
                {
                    c.state = 2;
                    c.ch.Play("emote-no", 0.15f);
                    ui.Pop("MISSED", Ui.PopStyle.Small);
                    sfx.Miss();
                }
                c.zone.SetActive(false);
            }
            foreach (var p in world.Pickups)
            {
                if (p.taken) continue;
                p.phase += dt * 3;
                p.food.transform.localPosition = new Vector3(0, Mathf.Sin(p.phase) * 0.08f, 0);
                p.food.transform.Rotate(0, dt * 115, 0);
                if (playing && Mathf.Abs(sW - p.s) < 0.5f && Mathf.Abs(uW - p.u) < 0.55f)
                {
                    p.taken = true;
                    Fly(p.food.transform, p.kind);
                    p.root.SetActive(false);
                }
            }
            foreach (var c in world.Coins)
            {
                if (c.taken) continue;
                c.t.Rotate(0, dt * 230, 0);
                if (playing && Mathf.Abs(sW - c.s) < 0.4f && Mathf.Abs(uW - c.u) < 0.5f)
                {
                    c.taken = true; c.t.gameObject.SetActive(false); coinsRun++; sfx.Coin();
                }
            }
            foreach (var o in world.Obstacles)
                if (!o.hit && playing && Mathf.Abs(sW - o.s) < 0.35f && Mathf.Abs(uW - o.u) < o.w / 2 + 0.22f) HitObstacle(o);
        }

        void StepFlights(float dt)
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                f.k = Mathf.Min(1, f.k + dt / 0.5f);
                var d = Food.Get(f.kind);
                var to = stackRoot.TransformPoint(new Vector3(f.lx, StackTop() + d.size.y / 2 + 0.05f, f.lz));
                var mid = (f.from + to) * 0.5f + Vector3.up * 0.75f;
                float e = f.k;
                f.t.position = (1 - e) * (1 - e) * f.from + 2 * (1 - e) * e * mid + e * e * to;
                f.t.rotation = Quaternion.Slerp(f.q0, stackRoot.rotation, Mathf.Min(1, e * 1.3f));
                if (f.k < 1) continue;
                flights.RemoveAt(i);
                var local = stackRoot.InverseTransformPoint(f.t.position);
                AddItem(f.kind, local, f.t);
                if (state == State.Play) { catches++; ui.Pop("+10", Ui.PopStyle.Small); }
            }
        }

        void StepDebris(float dt)
        {
            var me = track.Place(sW, uW);
            for (int i = debris.Count - 1; i >= 0; i--)
            {
                var d = debris[i];
                if (!d.landed)
                {
                    d.v.y -= 9.81f * dt;
                    d.t.position += d.v * dt;
                    d.t.Rotate(0, 0, d.spin * dt);
                    if (d.t.position.y <= d.half) Crash(d);
                }
                else if ((d.t.position - me).sqrMagnitude > 900) { Destroy(d.t.gameObject); debris.RemoveAt(i); }
            }
            foreach (var s in shards)
            {
                if (s.life <= 0) continue;
                s.life -= dt;
                s.v.y -= 9.81f * dt;
                s.t.position += s.v * dt;
                if (s.t.position.y < 0.02f) { s.t.position = new Vector3(s.t.position.x, 0.02f, s.t.position.z); s.v = new Vector3(s.v.x * 0.6f, -s.v.y * 0.35f, s.v.z * 0.6f); }
                s.t.Rotate(s.spin * dt);
                if (s.life < 0.3f) s.t.localScale *= 0.9f;
                if (s.life <= 0) s.t.gameObject.SetActive(false);
            }
        }

        // ---------- per frame visuals ----------
        void LateUpdate()
        {
            if (state == State.Play || state == State.Ready) waiter.Play("sprint", 0.2f, Mathf.Max(0.5f, speed / 4.2f));
            else if (state == State.Over) waiter.Play("idle", 0.3f);

            float h = track.At(sW).h;
            var pos = track.Place(sW, uW);
            waiter.Root.transform.position = pos;
            float yaw = h + Mathf.Atan2(uVel, Mathf.Max(1, speed));
            waiter.Root.transform.rotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, latG * 7f + Mathf.Sin(Time.time * 30) * 9f * stumble);

            var head = waiter.Head != null ? waiter.Head.position : pos + Vector3.up * Character.Height * 0.5f;
            stackRoot.position = head + Vector3.up * (Character.Height * 0.49f + 0.05f);
            stackRoot.rotation = Quaternion.Euler(0, h * Mathf.Rad2Deg, 0);

            var tt = trayRb.transform;
            trayVis.localPosition = tt.position - Lab;
            trayVis.localRotation = tt.rotation;
            bool scared = Mathf.Abs(TipSide()) > 0.3f || Mathf.Abs(TipFront()) > 0.3f || state == State.Dying;
            foreach (var it in items)
            {
                var bt = it.rb.transform;
                it.vis.localPosition = bt.position - Lab;
                it.vis.localRotation = bt.rotation;
                float s = it.squash;
                it.vis.localScale = new Vector3(1 + 0.2f * s, 1 - 0.2f * s, 1 + 0.2f * s);
                if (it.eyes != null) it.eyes.localScale = Vector3.one * (scared ? 1.35f : 1f);
            }

            // the camera follows from behind and eases into the turns; tall phones get a little more room
            float aspect = (float)Screen.height / Mathf.Max(1, Screen.width);
            float back = 4.6f + Mathf.Clamp(aspect - 1.78f, 0, 0.6f) * 1.6f;
            cam.fieldOfView = 56 + Mathf.Clamp(aspect - 1.78f, 0, 0.6f) * 8f;
            camH = Mathf.LerpAngle(camH * Mathf.Rad2Deg, h * Mathf.Rad2Deg, Mathf.Min(1, Time.deltaTime * 3.5f)) * Mathf.Deg2Rad;
            var f = Track.Forward(camH); var r = Track.Right(camH);
            var b = track.Place(sW, uW * 0.55f);
            cam.transform.position = b - f * back + r * 0.3f + Vector3.up * (3.8f + Mathf.Abs(Mathf.Cos(phase)) * 0.02f) + Random.insideUnitSphere * shake;
            cam.transform.LookAt(b + f * 3.2f + Vector3.up * 1.7f);
            cam.transform.Rotate(0, 0, -latG * 4.5f, Space.Self);

            world.Follow(pos, cam, speed);
        }
    }

    // Reads the phone's tilt with smoothing. Roll = left/right, Pitch = top away from you (degrees, from the saved straight position).
    public class TiltInput
    {
        Vector3 acc = new Vector3(0, -0.7f, -0.7f);
        float zeroRoll, zeroPitch;
        bool have;

        public float Roll { get; private set; }
        public float Pitch { get; private set; }

        public TiltInput()
        {
            zeroRoll = PlayerPrefs.GetFloat("ww_zero_roll", 0);
            zeroPitch = PlayerPrefs.GetFloat("ww_zero_pitch", 40);
        }

        public void Update(float dt)
        {
            if (!SystemInfo.supportsAccelerometer) return;
            var raw = Input.acceleration;
            // smooth out shaky readings without adding much delay (~50 ms)
            acc = have ? Vector3.Lerp(acc, raw, 1 - Mathf.Exp(-dt / 0.05f)) : raw;
            have = true;
            float roll = Mathf.Asin(Mathf.Clamp(acc.x / Mathf.Max(0.2f, acc.magnitude), -1, 1)) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(-acc.z, -acc.y) * Mathf.Rad2Deg;
            Roll = roll - zeroRoll;
            Pitch = pitch - zeroPitch;
        }

        public void Calibrate()
        {
            if (!have) return;
            zeroRoll += Roll; zeroPitch += Pitch;
            Roll = 0; Pitch = 0;
            PlayerPrefs.SetFloat("ww_zero_roll", zeroRoll);
            PlayerPrefs.SetFloat("ww_zero_pitch", zeroPitch);
        }

        // -1..1 steering: a 4° dead zone, full steering at 28° (less at higher sensitivity), softer near the middle
        public float Steer(float sensitivity)
        {
            float full = 28f / Mathf.Max(0.3f, sensitivity);
            float a = Mathf.Abs(Roll) - 4f;
            if (a <= 0) return 0;
            float x = Mathf.Clamp01(a / (full - 4f));
            return Mathf.Sign(Roll) * Mathf.Pow(x, 1.35f);
        }
    }

    // Reports bumps on a food item so it can squash and make a sound.
    public class ItemBody : MonoBehaviour
    {
        public System.Action<float> OnHit;
        void OnCollisionEnter(Collision c) => OnHit?.Invoke(c.relativeVelocity.magnitude);
    }
}
