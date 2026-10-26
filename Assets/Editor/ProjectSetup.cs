using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WobbleWaiter.EditorTools
{
    // Runs once when the project is first opened: makes the main scene, adds it to the build,
    // and sets the Android player settings for a portrait phone game.
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string DoneFlag = "ProjectSettings/WobbleWaiterSetup.done";

        static ProjectSetup()
        {
            if (File.Exists(DoneFlag)) return;
            EditorApplication.delayCall += () => { Run(); File.WriteAllText(DoneFlag, "ok"); };
        }

        [MenuItem("Wobble Waiter/Set Up Project Again")]
        public static void Run()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (EditorSceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);

            PlayerSettings.companyName = "WobbleWaiter";
            PlayerSettings.productName = "Wobble Waiter";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.SplashScreen.show = false;

            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.wobblewaiter.game");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.bundleVersionCode = 1;

            QualitySettings.shadowDistance = 22f;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.antiAliasing = 2;
            AssetDatabase.SaveAssets();
            Debug.Log("Wobble Waiter: project set up. Press Play, or use Wobble Waiter > Build Android APK.");
        }

        [MenuItem("Wobble Waiter/Build Android APK")]
        public static void BuildAndroid()
        {
            Run();
            Directory.CreateDirectory("Builds");
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/WobbleWaiter.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            Debug.Log("Wobble Waiter: build " + report.summary.result + " -> Builds/WobbleWaiter.apk");
        }

        // A quick Mac build used to check the game runs (start it with -wwselftest to get screenshots).
        public static void BuildMacTest()
        {
            Run();
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/MacTest/WobbleWaiter.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
            Debug.Log("Wobble Waiter: mac test build " + report.summary.result);
        }

        [MenuItem("Wobble Waiter/Build Google Play Bundle (AAB)")]
        public static void BuildBundle()
        {
            Run();
            Directory.CreateDirectory("Builds");
            EditorUserBuildSettings.buildAppBundle = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/WobbleWaiter.aab",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            EditorUserBuildSettings.buildAppBundle = false;
            Debug.Log("Wobble Waiter: bundle " + report.summary.result + " -> Builds/WobbleWaiter.aab");
        }
    }
}
