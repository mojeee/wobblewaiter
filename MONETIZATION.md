# Making money with Wobble Waiter

## The plan in short

| Way to earn | Where it is in the game | Status |
|---|---|---|
| **Rewarded ad: Continue** | Game-over card → "CONTINUE – watch an ad" (once per run, you get 1 heart back) | Built, using test ads |
| **Rewarded ad: Double coins** | Game-over card → "DOUBLE COINS – watch an ad" | Built, using test ads |
| **Interstitial ad** | Between rounds, at most every 3rd round and 2 minutes apart, never in the first rounds | Built, using test ads |
| **"Remove Ads" purchase** ($1.99–$2.99) | Turns off interstitials. Rewarded ads stay, because players choose them | Flag ready (`Ads.RemoveAdsOwned`), store not added yet |
| **Coins → shop** (hats, tray colors, waiters) | Coins are already saved (`ww_coins`) | Shop not built yet |
| **Coin packs** (small purchases) | Shop | Later |

Rewarded ads earn the most in casual games and players like them because they choose to watch.
Keep interstitials rare: too many ads make players delete the game.

## Right now: test ads

`Assets/Scripts/Ads.cs` uses `TestAds`, which shows a pretend ad screen for 2–3 seconds.
Every money button works with it, so you can test the whole flow before signing up anywhere.

## Step 1: choose an ad network

I recommend **Unity LevelPlay** (Unity's own ad mediation). It shows ads from many networks
(Unity Ads, Google AdMob, Meta, AppLovin and others) and picks the one that pays most for each ad.

The other good choice is **Google AdMob** alone (the Google Mobile Ads Unity plugin). It's simpler,
but with only one network the earnings are usually lower.

## Step 2: sign up (you do this, it needs your own account and bank details)

1. Create a Unity account at unity.com, then open **Unity Cloud → Monetization (LevelPlay)**.
2. Add your app: platform **Android**, your package name (for example `com.yourname.wobblewaiter`).
3. Create ad units: one **Rewarded** and one **Interstitial**. Copy the **App Key** and the ad unit IDs.
4. Add your payment details and tax form so you can be paid.

## Step 3: add the SDK to the project

1. In Unity: **Window → Package Manager → Unity Registry** → install **Ads Mediation (LevelPlay)**.
2. Follow its setup window: it adds the Android dependencies and asks for the App Key.

## Step 4: connect it to the game (one small file)

Make a new file `Assets/Scripts/LevelPlayAds.cs` with a class that implements `IAdProvider`:

- `Init()`: start LevelPlay with your App Key and load one rewarded and one interstitial ad.
- `RewardedReady` / `InterstitialReady`: return whether an ad is loaded.
- `ShowRewarded(done)`: show the rewarded ad. Call `done(true)` only when LevelPlay reports the reward. Call `done(false)` if it failed or was closed early. Then load the next ad.
- `ShowInterstitial(done)`: show it, call `done()` when it closes or fails, then load the next one.

Then in `Ads.cs` → `CreateProvider` change `return new TestAds(ui);` to `return new LevelPlayAds();`.
The rest of the game doesn't change.

Use the exact method names from the LevelPlay documentation for the version you install, because they change
between SDK versions. While testing, turn on **test mode** in the LevelPlay dashboard. Never click your own real
ads, or the account can be banned.

## Step 5: "Remove Ads" in-app purchase

1. Package Manager → install **In-App Purchasing**.
2. Google Play Console → your app → **Monetize → Products → In-app products** → create `remove_ads` (non-consumable).
3. Add a "Remove Ads" button (the start screen is a good place) that buys `remove_ads` with Unity IAP.
4. When the purchase succeeds (or is restored), set `Ads.RemoveAdsOwned = true;`.

## Important rules for Google Play

- **Children:** if your target audience in the Play Console includes children under 13, you must follow the
  **Families Policy**:
  - Use only ad networks certified for families. LevelPlay and AdMob can do this when set up for child-directed apps.
  - Mark the app as child-directed in the ad SDK, so ads aren't personalized.
  - Don't collect personal data.

  Ads for kids pay less, but these rules are required. If your audience is "13 and older", you can show normal ads.
- **Privacy policy:** an app with ads needs a privacy policy link in the Play Console (a simple web page is enough).
- **Data safety form:** in the Play Console, list that the ad SDK collects device IDs and usage data.
- **Consent in Europe and the UK:** show a consent popup (GDPR). LevelPlay and AdMob have one built in that you turn on.
- **Ad IDs:** keep your real App Key and ad unit IDs out of public places such as GitHub.

## About "a link to ads"

Banner links or "click here" ads inside the game are not how mobile games earn money. Ad networks pay you for
ads they show through their SDK, and they place and count the ads themselves. If you mean promoting your *other*
apps, LevelPlay and AdMob both support free cross-promotion campaigns for that.

## How much can it earn?

It depends on the players' countries and how long they play. As a rough guide for casual games:

- A rewarded ad view earns about **$0.005–$0.03**.
- 1,000 daily players who each watch 2–3 rewarded ads a day might earn around **$10–$60 per day**.

Getting players, through TikTok clips of funny crashes and the Share button, matters more than the ad setup.
