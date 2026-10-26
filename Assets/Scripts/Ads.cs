using System;
using UnityEngine;

namespace WobbleWaiter
{
    // Everything that earns money goes through here, so the game code never talks to an ad SDK directly.
    // Today it uses TestAds (a pretend ad screen). To earn real money, add an ad network
    // (see MONETIZATION.md) and return it from CreateProvider().
    public interface IAdProvider
    {
        void Init();
        bool RewardedReady { get; }
        bool InterstitialReady { get; }
        void ShowRewarded(Action<bool> done);   // done(true) only when the player watched to the end
        void ShowInterstitial(Action done);
    }

    public class Ads
    {
        // Interstitials are the ads that interrupt; keep them rare so players stay.
        const int RunsBetweenInterstitials = 3;
        const float MinSecondsBetweenInterstitials = 120f;

        readonly IAdProvider provider;
        int runsSinceInterstitial;
        float lastInterstitial = -999f;

        public Ads(Ui ui)
        {
            provider = CreateProvider(ui);
            provider.Init();
        }

        static IAdProvider CreateProvider(Ui ui)
        {
            // Swap this for your real provider, e.g. `return new LevelPlayAds();`
            return new TestAds(ui);
        }

        public static bool RemoveAdsOwned
        {
            get => PlayerPrefs.GetInt("ww_noads", 0) == 1;
            set => PlayerPrefs.SetInt("ww_noads", value ? 1 : 0);
        }

        public bool RewardedReady => provider.RewardedReady;

        public void ShowRewarded(Action<bool> done)
        {
            if (!provider.RewardedReady) { done(false); return; }
            PauseGame(true);
            provider.ShowRewarded(ok => { PauseGame(false); done(ok); });
        }

        // Call between rounds. Shows an ad only every few rounds, never in the first rounds,
        // never too soon after the last one, and never for players who bought "Remove Ads".
        public void BetweenRounds(Action then)
        {
            runsSinceInterstitial++;
            bool show = !RemoveAdsOwned
                && runsSinceInterstitial >= RunsBetweenInterstitials
                && Time.realtimeSinceStartup - lastInterstitial > MinSecondsBetweenInterstitials
                && provider.InterstitialReady;
            if (!show) { then(); return; }
            runsSinceInterstitial = 0;
            lastInterstitial = Time.realtimeSinceStartup;
            PauseGame(true);
            provider.ShowInterstitial(() => { PauseGame(false); then(); });
        }

        static float savedScale = 1f;
        static void PauseGame(bool pause)
        {
            if (pause) { savedScale = Time.timeScale; Time.timeScale = 0f; AudioListener.pause = true; }
            else { Time.timeScale = savedScale <= 0 ? 1f : savedScale; AudioListener.pause = false; }
        }
    }

    // A pretend ad so every money feature can be tested before signing up with an ad network.
    public class TestAds : IAdProvider
    {
        readonly Ui ui;
        public TestAds(Ui ui) { this.ui = ui; }
        public void Init() { }
        public bool RewardedReady => true;
        public bool InterstitialReady => true;
        public void ShowRewarded(Action<bool> done) => ui.ShowTestAd("TEST REWARDED AD\nWatch to the end to get your reward", 3f, () => done(true));
        public void ShowInterstitial(Action done) => ui.ShowTestAd("TEST AD\nbetween rounds", 2f, done);
    }
}
