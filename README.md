# Wobble Waiter (Unity 6, Android)

A cartoon 3D runner: a waiter runs along a winding path through a diner with a tray on his head.
Tilt the phone to steer, catch food from the chefs, grab coins, dodge boxes, buckets and wet floors.
Turns and sharp steering push the stack sideways, so steer smoothly.

## 1. First time: activate Unity (you must do this yourself)

Unity needs a free license before it can open any project.

1. Open **Unity Hub**.
2. Sign in with your Unity account (top left), or create a free one.
3. Go to **Settings → Licenses → Add → Get a free personal license**.

Android Build Support (SDK, NDK, OpenJDK) is already installed for Unity **6000.4.10f1**.

## 2. Open the project

1. Unity Hub → **Projects → Add → Add project from disk** → choose `Desktop/WobbleWaiter`.
2. Open it with Unity **6000.4.10f1**. The first import takes a few minutes.
3. The project sets itself up on first open: it creates `Assets/Scenes/Main.unity`, adds it to the build,
   and sets portrait mode, the package name and the Android settings.
4. Press **Play**. Steer with the ← → keys or by clicking the left or right half of the Game view.
   Tip: in the Game view, pick a phone size such as 1080x1920 (portrait).

## 3. Put it on your Android phone

1. On the phone: **Settings → About phone →** tap **Build number** 7 times, then turn on **USB debugging** in Developer options.
2. Connect the phone with a USB cable.
3. In Unity: **File → Build Profiles → Android → Switch Platform** (first time only).
4. Menu **Wobble Waiter → Build Android APK**. The file appears in `Builds/WobbleWaiter.apk`.
5. Or use **File → Build And Run** to install it on the connected phone directly.

For Google Play, use **Wobble Waiter → Build Google Play Bundle (AAB)**. You also need a signing key
(**Project Settings → Player → Android → Publishing Settings → Keystore Manager**). Keep the key and its password safe:
you need the same key for every update.

Before publishing, change the package name `com.wobblewaiter.game` (in `Assets/Editor/ProjectSetup.cs`)
to your own, for example `com.yourname.wobblewaiter`. It can never change after the first release.

## 4. How the code is organized

| File | What it does |
|---|---|
| `Assets/Scripts/Boot.cs` | Starts the game. The scene is empty on purpose; everything is created from code. |
| `Assets/Scripts/Game.cs` | Main game: steering (tilt, touch, keys), tray physics, catching, obstacles, score, game over. |
| `Assets/Scripts/Track.cs` | The winding path: straight start, then turns of 36°–60° with short straights between them. |
| `Assets/Scripts/World.cs` | Builds the restaurant in 6 m pieces ahead of the waiter: carpet, walls, tables, kitchens, lamps, chefs, coins, obstacles. |
| `Assets/Scripts/Food.cs` | The 10 foods, their sizes, and the googly eyes. |
| `Assets/Scripts/Character.cs` | Kenney characters with their animations, plus chef hats and caps. |
| `Assets/Scripts/Looks.cs` | Cartoon materials and generated textures (floor, carpet, walls, glow). |
| `Assets/Scripts/Ui.cs` | HUD, start screen, game-over card, settings buttons, test-ad screen. |
| `Assets/Scripts/Sfx.cs` | Sound effects and the background tune, generated in code (no audio files). |
| `Assets/Scripts/Ads.cs` | All money features go through here. See `MONETIZATION.md`. |
| `Assets/Resources/Shaders/` | Cartoon shader with ink outlines, glow and unlit shaders (Built-in render pipeline). |
| `Assets/Resources/Models/` | Free CC0 models by Kenney (kenney.nl): characters, food, furniture. |

The food on the tray uses **2D physics** (left/right and up/down on the tray). The world is 3D, but this keeps tall
stacks stable and is cheap for low-end phones.

## 5. Low-end phones

The start screen has a **Graphics: FULL / LIGHT** button. LIGHT turns off shadows, outlines, guests, table food,
light beams and dust, and runs at 30 FPS. Phones with less than 3 GB of RAM start in LIGHT automatically.

## 6. Things to tune

- Difficulty: `Game.cs` → `FixedUpdate` (speed `3.0f + distance * 0.012f`, max `6.2f`) and `Kick(...)` strengths in `HitObstacle`.
- How much turns push the food: `latG ... * 0.95f` in `FixedUpdate`.
- Tilt sensitivity: `TiltFull` (smaller = more sensitive) and `TiltDead` at the top of `Game.cs`.
- What appears on the path: `World.Fill`.
- If a model faces the wrong way after import, rotate it in `Character.Make` / `World.Furn`.

## 7. Not checked yet

The C# code compiles against Unity 6000.4.10f1's own libraries with no errors or warnings, but it has not been
run inside the Unity editor yet (no license was active). Things to look at on first Play:
the shaders compiling, the characters facing forward, and the animation names (`sprint`, `sit`, `holding-both`,
`emote-yes`, `emote-no`, `interact-right`, `idle`) matching the imported clips.

## Credits

3D models: Kenney (kenney.nl), CC0 — free for commercial use. See `Assets/ThirdParty/Kenney-License.txt`.
