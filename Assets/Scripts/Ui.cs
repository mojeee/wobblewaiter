using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WobbleWaiter
{
    // All screens, built in code: HUD, start menu, pause, settings, game-over card, toasts and the test-ad overlay.
    public class Ui : MonoBehaviour
    {
        static readonly Color Ink = Looks.Hex("#1B2A29"), Gold = Looks.Hex("#FFC93C"),
            Teal = Looks.Hex("#1E7D72"), Red = Looks.Hex("#E8463B"), Sub = Looks.Hex("#566A67"), Soft = Looks.Hex("#F1F5F4"),
            Glass = new Color(0.106f, 0.165f, 0.161f, 0.72f);

        public Action OnPlay, OnContinue, OnDouble, OnSettingsChanged, OnPause, OnResume, OnHome, OnCalibrate;

        Font font;
        Sprite round, circle, heart, star, fade;
        RectTransform canvas;
        Text stackNum, bestNum, scoreNum, overCarried, overScore, overBest, startBest, walletText, adCount, adLabel, toast;
        readonly Image[] hearts = new Image[3], stars = new Image[3];
        Image dangerL, dangerR, dangerT, dangerB, hintL, hintR;
        GameObject hud, startScreen, overScreen, pauseScreen, settingsScreen, adScreen, continueBtn, doubleBtn, pauseBtn;
        Text steerBtn, vibBtn, gfxBtn, autoZeroBtn;
        readonly List<(Text t, float born, Vector2 from, float life)> pops = new List<(Text, float, Vector2, float)>();
        readonly List<RectTransform> breathing = new List<RectTransform>();
        int lastLives = 3;
        float heartPulse = -1, toastUntil;

        public void Build()
        {
            font = Resources.Load<Font>("Fonts/LilitaOne");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            round = MakeSprite(64, (x, y) => RoundRect(x, y, 64, 28), 30);
            circle = MakeSprite(64, (x, y) => Disc(x, y, 64), 0);
            heart = MakeSprite(64, Heart, 0);
            star = MakeSprite(64, Star, 0);
            fade = MakeFade();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(es);
            }
            var cgo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            var cv = cgo.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 10;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1080, 1920); sc.matchWidthOrHeight = 0.5f;
            canvas = (RectTransform)cgo.transform;
            var safe = MakeRect("Safe", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();

            BuildHud(safe);
            BuildStart(canvas);
            BuildOver(canvas);
            BuildPause(canvas);
            BuildSettings(canvas);
            BuildAd(canvas);
            toast = Label("Toast", canvas, "", 44, Color.white, new Vector2(0.5f, 0), new Vector2(0, 220), new Vector2(900, 90), TextAnchor.MiddleCenter);
            AddOutline(toast.gameObject, 4, Ink);
            ShowScreen(start: false, over: false, hudOn: false);
        }

        // ---------- HUD ----------
        void BuildHud(RectTransform safe)
        {
            hud = MakeRect("HUD", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var ht = (RectTransform)hud.transform;
            var red = new Color(0.91f, 0.27f, 0.23f, 0f);
            dangerL = Img("DangerL", ht, fade, red, new Vector2(0, 0), new Vector2(0.34f, 1), Vector2.zero, Vector2.zero);
            dangerR = Img("DangerR", ht, fade, red, new Vector2(0.66f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            dangerR.rectTransform.localScale = new Vector3(-1, 1, 1);
            dangerT = Img("DangerTop", ht, fade, red, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(600, 2400));
            dangerT.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            dangerB = Img("DangerBottom", ht, fade, red, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 300), new Vector2(600, 2400));
            dangerB.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);

            var stack = Img("Stack", ht, round, Color.white, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -40), new Vector2(330, 130));
            stack.rectTransform.pivot = new Vector2(0, 1);
            AddShadow(stack.gameObject, 8);
            for (int i = 0; i < 3; i++)
            {
                var c = new[] { "#FFC93C", "#F59AC0", "#FFFFFF" }[i];
                var bar = Img("Layer", stack.rectTransform, round, Looks.Hex(c), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(68, 30 - i * 26), new Vector2(64 + i * 12, 22));
                AddOutline(bar.gameObject, 3);
            }
            stackNum = Label("StackNum", stack.rectTransform, "0", 80, Ink, new Vector2(0, 0.5f), new Vector2(200, 2), new Vector2(120, 110), TextAnchor.MiddleCenter);
            Label("BestLbl", stack.rectTransform, "BEST", 26, Sub, new Vector2(0, 0.5f), new Vector2(285, 24), new Vector2(90, 34), TextAnchor.MiddleCenter);
            bestNum = Label("BestNum", stack.rectTransform, "0", 44, Ink, new Vector2(0, 0.5f), new Vector2(285, -14), new Vector2(90, 50), TextAnchor.MiddleCenter);

            var pill = Img("Score", ht, round, Glass, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(30, -62), new Vector2(270, 90));
            Img("ScoreStar", pill.rectTransform, star, Gold, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(60, 60));
            scoreNum = Label("ScoreNum", pill.rectTransform, "0", 52, Color.white, new Vector2(0.5f, 0.5f), new Vector2(26, 2), new Vector2(190, 86), TextAnchor.MiddleCenter);
            for (int i = 0; i < 3; i++)
            {
                hearts[i] = Img("Heart", ht, heart, Looks.Hex("#FF4D5A"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-50 + i * 80, -160), new Vector2(70, 70));
                AddOutline(hearts[i].gameObject, 4, Color.white);
            }
            var pb = MakeButton(ht, "", Glass, Color.white, Vector2.zero, new Vector2(120, 120), 40, () => OnPause?.Invoke());
            pb.anchorMin = pb.anchorMax = new Vector2(1, 1); pb.anchoredPosition = new Vector2(-96, -100);
            pb.GetComponent<Image>().sprite = circle; pb.GetComponent<Image>().type = Image.Type.Simple;
            foreach (int s in new[] { -1, 1 }) Img("Bar", pb, round, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(s * 16, 0), new Vector2(20, 54));
            pauseBtn = pb.gameObject;

            hintL = Img("HintL", ht, circle, new Color(1, 1, 1, 0.22f), new Vector2(0, 0), new Vector2(0, 0), new Vector2(110, 230), new Vector2(170, 170));
            hintR = Img("HintR", ht, circle, new Color(1, 1, 1, 0.22f), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-110, 230), new Vector2(170, 170));
            Label("L", hintL.rectTransform, "<", 100, Color.white, new Vector2(0.5f, 0.5f), new Vector2(-4, 6), new Vector2(160, 160), TextAnchor.MiddleCenter);
            Label("R", hintR.rectTransform, ">", 100, Color.white, new Vector2(0.5f, 0.5f), new Vector2(4, 6), new Vector2(160, 160), TextAnchor.MiddleCenter);
        }

        public void SetHud(int stackCount, int bestStack, int score, int lives)
        {
            stackNum.text = stackCount.ToString();
            bestNum.text = bestStack.ToString();
            scoreNum.text = score.ToString();
            if (lives < lastLives) heartPulse = Time.unscaledTime;
            lastLives = lives;
            for (int i = 0; i < 3; i++) hearts[i].color = i < lives ? Looks.Hex("#FF4D5A") : new Color(0.1f, 0.16f, 0.16f, 0.3f);
        }

        public void SetDanger(float left, float right, float front, float back)
        {
            dangerL.color = new Color(0.91f, 0.27f, 0.23f, left * 0.6f);
            dangerR.color = new Color(0.91f, 0.27f, 0.23f, right * 0.6f);
            dangerT.color = new Color(0.91f, 0.27f, 0.23f, front * 0.55f);
            dangerB.color = new Color(0.91f, 0.27f, 0.23f, back * 0.55f);
        }

        public void SetHints(bool show, bool left, bool right)
        {
            hintL.gameObject.SetActive(show || left); hintR.gameObject.SetActive(show || right);
            hintL.color = left ? new Color(1f, 0.79f, 0.24f, 0.75f) : new Color(1, 1, 1, 0.22f);
            hintR.color = right ? new Color(1f, 0.79f, 0.24f, 0.75f) : new Color(1, 1, 1, 0.22f);
        }

        public enum PopStyle { Big, Small, Banner }

        public void Pop(string text, PopStyle style)
        {
            Color fill = style == PopStyle.Big ? Gold : Color.white;
            Color edge = style == PopStyle.Small ? Teal : style == PopStyle.Banner ? Red : Ink;
            int size = style == PopStyle.Small ? 70 : 110;
            Vector2 at = style == PopStyle.Small ? new Vector2(80, 380) : style == PopStyle.Banner ? new Vector2(0, 520) : new Vector2(0, -60);
            var t = Label("Pop", (RectTransform)hud.transform, text, size, fill, new Vector2(0.5f, 0.5f), at, new Vector2(1000, 180), TextAnchor.MiddleCenter);
            AddOutline(t.gameObject, 8, edge);
            t.rectTransform.localRotation = Quaternion.Euler(0, 0, -5);
            pops.Add((t, Time.unscaledTime, at, style == PopStyle.Small ? 0.8f : 1.0f));
        }

        public void Toast(string text)
        {
            toast.text = text;
            toastUntil = Time.unscaledTime + 2f;
            toast.transform.SetAsLastSibling();
        }

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var p = pops[i];
                float a = (now - p.born) / p.life;
                if (a >= 1) { Destroy(p.t.gameObject); pops.RemoveAt(i); continue; }
                float s = a < 0.18f ? Mathf.Lerp(0.3f, 1.2f, a / 0.18f) : Mathf.Lerp(1.2f, 1f, (a - 0.18f) * 3);
                p.t.rectTransform.localScale = Vector3.one * s;
                p.t.rectTransform.anchoredPosition = p.from + new Vector2(0, a * 140);
                var c = p.t.color; c.a = a < 0.7f ? 1 : 1 - (a - 0.7f) / 0.3f; p.t.color = c;
            }
            if (heartPulse > 0)
            {
                float k = (now - heartPulse) / 0.5f;
                int i = Mathf.Clamp(lastLives, 0, 2);
                hearts[i].rectTransform.localScale = Vector3.one * (k < 1 ? Mathf.Lerp(1.6f, 1f, k) : 1f);
                if (k >= 1) heartPulse = -1;
            }
            float b = 1 + Mathf.Sin(now * 4.5f) * 0.04f;
            foreach (var btn in breathing) if (btn.gameObject.activeInHierarchy) btn.localScale = Vector3.one * b;
            var tc = toast.color; tc.a = Mathf.Clamp01((toastUntil - now) * 3); toast.color = tc;
            if (adScreen.activeSelf) TickAd();
        }

        // ---------- start ----------
        void BuildStart(RectTransform root)
        {
            startScreen = Dim("Start", root).gameObject;
            var r = (RectTransform)startScreen.transform;
            var hero = Resources.Load<Texture2D>("Art/Hero");
            if (hero != null)
            {
                var hi = MakeRect("Hero", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 560), new Vector2(560, 560)).gameObject.AddComponent<RawImage>();
                hi.texture = hero; hi.raycastTarget = false;
            }
            var title = Label("Title", r, "Wobble\n<color=#FFFFFF>Waiter</color>", 200, Gold, new Vector2(0.5f, 0.5f), new Vector2(0, hero != null ? 170 : 430), new Vector2(1000, 440), TextAnchor.MiddleCenter);
            title.supportRichText = true; title.lineSpacing = 0.8f;
            AddOutline(title.gameObject, 12, Ink);
            AddShadow(title.gameObject, 14, new Color(0.1f, 0.06f, 0.03f, 0.45f));
            title.rectTransform.localRotation = Quaternion.Euler(0, 0, 4);
            var sub = Label("Sub", r, "Tilt to steer and balance. Catch the food, grab the coins, dodge the mess!", 44, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, hero != null ? -80 : 140), new Vector2(820, 150), TextAnchor.MiddleCenter);
            AddOutline(sub.gameObject, 3, Ink);
            startBest = Label("Best", r, "", 50, Gold, new Vector2(0.5f, 0.5f), new Vector2(0, hero != null ? -190 : 20), new Vector2(600, 70), TextAnchor.MiddleCenter);
            AddOutline(startBest.gameObject, 4, Ink);
            var play = MakeButton(r, "PLAY", Gold, Ink, new Vector2(0, hero != null ? -360 : -150), new Vector2(580, 200), 110, () => OnPlay?.Invoke());
            breathing.Add(play);
            walletText = Label("Wallet", r, "", 44, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, hero != null ? -530 : -330), new Vector2(700, 64), TextAnchor.MiddleCenter);
            AddOutline(walletText.gameObject, 3, Ink);
            MakeButton(r, "SETTINGS", Glass, Color.white, new Vector2(0, hero != null ? -680 : -500), new Vector2(460, 120), 54, ShowSettings);
        }

        public void ShowStart(int record, int wallet)
        {
            startBest.text = record > 0 ? "BEST  " + record : "";
            walletText.text = "Coins: " + wallet;
            ShowScreen(start: true, over: false, hudOn: false);
        }

        // ---------- pause ----------
        void BuildPause(RectTransform root)
        {
            pauseScreen = Dim("Pause", root).gameObject;
            var r = (RectTransform)pauseScreen.transform;
            var t = Label("Paused", r, "PAUSED", 150, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 420), new Vector2(900, 200), TextAnchor.MiddleCenter);
            AddOutline(t.gameObject, 10, Ink);
            breathing.Add(MakeButton(r, "RESUME", Gold, Ink, new Vector2(0, 120), new Vector2(560, 190), 96, () => OnResume?.Invoke()));
            MakeButton(r, "SETTINGS", Glass, Color.white, new Vector2(0, -90), new Vector2(460, 130), 58, ShowSettings);
            MakeButton(r, "HOME", Glass, Color.white, new Vector2(0, -250), new Vector2(460, 130), 58, () => OnHome?.Invoke());
            pauseScreen.SetActive(false);
        }

        public void ShowPause(bool on)
        {
            pauseScreen.SetActive(on);
            pauseBtn.SetActive(!on && hud.activeSelf);
            if (on) pauseScreen.transform.SetAsLastSibling();
        }

        // ---------- settings ----------
        void BuildSettings(RectTransform root)
        {
            var dim = Dim("Settings", root);
            settingsScreen = dim.gameObject;
            dim.GetComponent<Image>().raycastTarget = true;
            var card = Img("Card", dim, round, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(940, 1500));
            AddShadow(card.gameObject, 14);
            var c = card.rectTransform;
            Label("Title", c, "SETTINGS", 96, Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 640), new Vector2(800, 130), TextAnchor.MiddleCenter);

            float y = 490;
            Row(c, "Steering", y); steerBtn = Choice(c, y, () => { Settings.Tilt = !Settings.Tilt; Refresh(); });
            y -= 150; Row(c, "Tilt sensitivity", y); MakeSlider(c, new Vector2(240, y), 340, 0.5f, 2f, Settings.TiltSensitivity, v => { Settings.TiltSensitivity = v; OnSettingsChanged?.Invoke(); });
            y -= 150; MakeButton(c, "SET STRAIGHT POSITION", Teal, Color.white, new Vector2(-150, y), new Vector2(560, 110), 40, () => OnCalibrate?.Invoke());
            autoZeroBtn = MakeButton(c, "", Glass, Color.white, new Vector2(300, y), new Vector2(240, 110), 40, () => { Settings.TiltAutoZero = !Settings.TiltAutoZero; Refresh(); }).GetComponentInChildren<Text>();
            Label("Hint", c, "Hold the phone the way you like to play, then tap. AUTO does this each time a run starts.", 30, Sub, new Vector2(0.5f, 0.5f), new Vector2(0, y - 95), new Vector2(820, 80), TextAnchor.MiddleCenter);
            y -= 230; Row(c, "Music", y); MakeSlider(c, new Vector2(240, y), 340, 0f, 1f, Settings.MusicVolume, v => { Settings.MusicVolume = v; OnSettingsChanged?.Invoke(); });
            y -= 140; Row(c, "Sounds", y); MakeSlider(c, new Vector2(240, y), 340, 0f, 1f, Settings.SoundVolume, v => { Settings.SoundVolume = v; OnSettingsChanged?.Invoke(); });
            y -= 140; Row(c, "Vibration", y); vibBtn = Choice(c, y, () => { Settings.Vibration = !Settings.Vibration; Refresh(); });
            y -= 140; Row(c, "Graphics", y); gfxBtn = Choice(c, y, () => { Settings.Light = !Settings.Light; Refresh(); });
            MakeButton(c, "BACK", Gold, Ink, new Vector2(0, -630), new Vector2(460, 150), 76, CloseSettings);
            settingsScreen.SetActive(false);
            Refresh();
        }

        void Row(RectTransform c, string label, float y) =>
            Label(label, c, label, 50, Ink, new Vector2(0.5f, 0.5f), new Vector2(-200, y), new Vector2(460, 80), TextAnchor.MiddleLeft);

        Text Choice(RectTransform c, float y, Action onClick) =>
            MakeButton(c, "", Teal, Color.white, new Vector2(240, y), new Vector2(340, 110), 48, onClick).GetComponentInChildren<Text>();

        void Refresh()
        {
            steerBtn.text = Settings.Tilt ? "TILT" : "TOUCH";
            autoZeroBtn.text = Settings.TiltAutoZero ? "AUTO: ON" : "AUTO: OFF";
            vibBtn.text = Settings.Vibration ? "ON" : "OFF";
            gfxBtn.text = Settings.Light ? "LIGHT" : "FULL";
            OnSettingsChanged?.Invoke();
        }

        public void ShowSettings()
        {
            settingsScreen.SetActive(true);
            settingsScreen.transform.SetAsLastSibling();
        }

        public void CloseSettings() => settingsScreen.SetActive(false);

        // the back button closes the settings first; returns false when nothing was open
        public bool CloseTopPanel()
        {
            if (adScreen.activeSelf) return true;
            if (settingsScreen.activeSelf) { CloseSettings(); return true; }
            return false;
        }

        // ---------- game over ----------
        void BuildOver(RectTransform root)
        {
            overScreen = Dim("Over", root).gameObject;
            var r = (RectTransform)overScreen.transform;
            var card = Img("Card", r, round, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(880, 1060));
            AddShadow(card.gameObject, 14);
            var c = card.rectTransform;
            var ribbon = Img("Ribbon", c, round, Red, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 10), new Vector2(460, 140));
            ribbon.rectTransform.localRotation = Quaternion.Euler(0, 0, 3);
            AddShadow(ribbon.gameObject, 7, new Color(0.55f, 0.15f, 0.12f, 1));
            Label("Oops", ribbon.rectTransform, "OOPS!", 100, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(440, 130), TextAnchor.MiddleCenter);
            for (int i = 0; i < 3; i++)
            {
                float big = i == 1 ? 170 : 130;
                stars[i] = Img("Star", c, star, Soft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 175, i == 1 ? 330 : 315), new Vector2(big, big));
            }
            string[] labels = { "CARRIED", "SCORE", "BEST" };
            var vals = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var box = Img("Stat", c, round, Soft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 270, 120), new Vector2(250, 160));
                Label("L", box.rectTransform, labels[i], 30, Sub, new Vector2(0.5f, 0.5f), new Vector2(0, 42), new Vector2(240, 40), TextAnchor.MiddleCenter);
                vals[i] = Label("V", box.rectTransform, "0", 70, Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(240, 86), TextAnchor.MiddleCenter);
            }
            overCarried = vals[0]; overScore = vals[1]; overBest = vals[2];
            breathing.Add(MakeButton(c, "PLAY AGAIN", Gold, Ink, new Vector2(0, -110), new Vector2(620, 160), 80, () => OnPlay?.Invoke()));
            continueBtn = MakeButton(c, "CONTINUE  -  watch an ad", Teal, Color.white, new Vector2(0, -285), new Vector2(720, 120), 46, () => OnContinue?.Invoke()).gameObject;
            doubleBtn = MakeButton(c, "DOUBLE COINS  -  watch an ad", Looks.Hex("#3D8BD6"), Color.white, new Vector2(0, -425), new Vector2(720, 120), 46, () => OnDouble?.Invoke()).gameObject;
            MakeButton(r, "HOME", Glass, Color.white, new Vector2(0, -620), new Vector2(380, 120), 56, () => OnHome?.Invoke());
        }

        public void ShowOver(int carried, int score, int record, int starCount, bool canContinue, bool canDouble)
        {
            overCarried.text = carried.ToString(); overScore.text = score.ToString(); overBest.text = record.ToString();
            for (int i = 0; i < 3; i++) stars[i].color = i < starCount ? Gold : Soft;
            continueBtn.SetActive(canContinue);
            doubleBtn.SetActive(canDouble);
            ShowScreen(start: false, over: true, hudOn: true);
        }

        public void HideDouble() => doubleBtn.SetActive(false);

        public void ShowScreen(bool start, bool over, bool hudOn)
        {
            startScreen.SetActive(start);
            overScreen.SetActive(over);
            hud.SetActive(hudOn);
            pauseBtn.SetActive(hudOn && !over);
        }

        public bool PointerOverButton()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++) if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
                return false;
            }
            return es.IsPointerOverGameObject();
        }

        // ---------- test ad overlay (stands in for a real ad network until one is added) ----------
        Action adDone;
        float adEnds;

        void BuildAd(RectTransform root)
        {
            adScreen = Img("Ad", root, round, new Color(0.05f, 0.07f, 0.07f, 0.96f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            adScreen.GetComponent<Image>().raycastTarget = true;
            var r = (RectTransform)adScreen.transform;
            adLabel = Label("AdLabel", r, "", 64, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(900, 300), TextAnchor.MiddleCenter);
            adCount = Label("AdCount", r, "", 150, Gold, new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(400, 200), TextAnchor.MiddleCenter);
            Label("AdNote", r, "This is a pretend ad for testing.\nA real ad network fills this space in the released game.", 36, Sub, new Vector2(0.5f, 0.5f), new Vector2(0, -320), new Vector2(900, 140), TextAnchor.MiddleCenter);
            adScreen.SetActive(false);
        }

        public void ShowTestAd(string label, float seconds, Action done)
        {
            adLabel.text = label;
            adEnds = Time.unscaledTime + seconds;
            adDone = done;
            adScreen.SetActive(true);
            adScreen.transform.SetAsLastSibling();
        }

        void TickAd()
        {
            float left = adEnds - Time.unscaledTime;
            adCount.text = Mathf.CeilToInt(Mathf.Max(0, left)).ToString();
            if (left > 0) return;
            adScreen.SetActive(false);
            var d = adDone; adDone = null; d?.Invoke();
        }

        // ---------- building blocks ----------
        RectTransform Dim(string name, RectTransform root)
        {
            var img = Img(name, root, fade, new Color(0.106f, 0.165f, 0.161f, 0.55f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            img.sprite = null;
            return img.rectTransform;
        }

        static RectTransform MakeRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        Image Img(string name, RectTransform parent, Sprite sp, Color c, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var rt = MakeRect(name, parent, aMin, aMax, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp; img.color = c; img.raycastTarget = false;
            if (sp == round) img.type = Image.Type.Sliced;
            return img;
        }

        Text Label(string name, RectTransform parent, string text, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align)
        {
            var rt = MakeRect(name, parent, anchor, anchor, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.text = text; t.fontSize = size; t.color = c; t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        RectTransform MakeButton(RectTransform parent, string text, Color bg, Color fg, Vector2 pos, Vector2 size, int fontSize, Action onClick)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var img = Img("Button", parent, round, bg, mid, mid, pos, size);
            img.raycastTarget = true;
            AddShadow(img.gameObject, 9, new Color(bg.r * 0.7f, bg.g * 0.6f, bg.b * 0.5f, Mathf.Max(bg.a, 0.6f)));
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => { Settings.ClickSound?.Invoke(); onClick(); });
            Label("Text", img.rectTransform, text, fontSize, fg, mid, new Vector2(0, 4), size - new Vector2(30, 10), TextAnchor.MiddleCenter);
            return img.rectTransform;
        }

        void MakeSlider(RectTransform parent, Vector2 pos, float width, float min, float max, float value, Action<float> onChange)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var root = MakeRect("Slider", parent, mid, mid, pos, new Vector2(width, 80));
            var bg = Img("Track", root, round, Soft, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(0, 26));
            bg.raycastTarget = true;
            var fillArea = MakeRect("Fill Area", root, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-30, 26));
            var fill = Img("Fill", fillArea, round, Gold, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(30, 0));
            var handleArea = MakeRect("Handle Area", root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-60, 0));
            var handle = Img("Handle", handleArea, circle, Teal, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(70, 70));
            handle.raycastTarget = true;
            AddShadow(handle.gameObject, 5);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.minValue = min; s.maxValue = max; s.value = value;
            s.onValueChanged.AddListener(v => onChange(v));
        }

        static void AddOutline(GameObject go, float d, Color? c = null)
        {
            var col = c ?? new Color(0.106f, 0.165f, 0.161f, 1);
            var o = go.AddComponent<Outline>(); o.effectColor = col; o.effectDistance = new Vector2(d, -d);
            var o2 = go.AddComponent<Outline>(); o2.effectColor = col; o2.effectDistance = new Vector2(-d, d);
        }

        static void AddShadow(GameObject go, float d, Color? c = null)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = c ?? new Color(0.24f, 0.12f, 0.04f, 0.35f);
            s.effectDistance = new Vector2(0, -d);
        }

        // ---------- generated sprites ----------
        static Sprite MakeSprite(int n, Func<int, int, float> alpha, int border)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(alpha(x, y)));
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        static Sprite MakeFade()
        {
            var t = new Texture2D(64, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 4; y++) for (int x = 0; x < 64; x++) t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(1 - x / 63f, 1.5f)));
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 64, 4), new Vector2(0.5f, 0.5f));
        }

        static float RoundRect(int x, int y, int n, float r)
        {
            float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
            return r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) + 0.5f;
        }

        static float Disc(int x, int y, int n) => n / 2f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) + 0.5f;

        static float Heart(int px, int py)
        {
            float x = (px - 31.5f) / 26f, y = (py - 28f) / 26f;
            float a = x * x + y * y - 1;
            return a * a * a - x * x * y * y * y < 0 ? 1 : 0;
        }

        static float Star(int px, int py)
        {
            var p = new Vector2(px - 31.5f, py - 29f);
            bool inside = false;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                Vector2 a = StarPoint(i), b = StarPoint(j);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? 1 : 0;
        }

        static Vector2 StarPoint(int i)
        {
            float ang = Mathf.PI / 2 + i * Mathf.PI / 5, r = i % 2 == 0 ? 31f : 13f;
            return new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
        }
    }

    // Keeps the HUD clear of the phone's notch and rounded corners.
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == last || Screen.width == 0) return;
            last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    // Player choices kept between sessions.
    public static class Settings
    {
        public static Action ClickSound;
        public static bool Tilt { get => PlayerPrefs.GetInt("ww_tilt", SystemInfo.supportsAccelerometer ? 1 : 0) == 1; set => PlayerPrefs.SetInt("ww_tilt", value ? 1 : 0); }
        public static bool TiltAutoZero { get => PlayerPrefs.GetInt("ww_tilt_auto", 1) == 1; set => PlayerPrefs.SetInt("ww_tilt_auto", value ? 1 : 0); }
        public static float TiltSensitivity { get => PlayerPrefs.GetFloat("ww_tilt_sens", 1f); set => PlayerPrefs.SetFloat("ww_tilt_sens", value); }
        public static bool Light { get => PlayerPrefs.GetInt("ww_light", SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 3000 ? 1 : 0) == 1; set => PlayerPrefs.SetInt("ww_light", value ? 1 : 0); }
        public static float SoundVolume { get => PlayerPrefs.GetFloat("ww_sound_vol", 1f); set => PlayerPrefs.SetFloat("ww_sound_vol", value); }
        public static float MusicVolume { get => PlayerPrefs.GetFloat("ww_music_vol", 0.6f); set => PlayerPrefs.SetFloat("ww_music_vol", value); }
        public static bool Vibration { get => PlayerPrefs.GetInt("ww_vibrate", 1) == 1; set => PlayerPrefs.SetInt("ww_vibrate", value ? 1 : 0); }
    }
}
