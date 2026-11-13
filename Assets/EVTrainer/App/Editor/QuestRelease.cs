using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EVTrainer.App.Editor
{
    /// <summary>
    /// Release setup for the Quest: player settings (name, company, version, self-made icon, minimal splash) and two
    /// build entries. Release: no Development Build, so the debug panel, joint spheres and info logs are compiled out.
    /// Development: profiler connection and script debugging for on-device profiling.
    /// </summary>
    public static class QuestRelease
    {
        public const string ProductName = "EV Service Trainer";
        public const string CompanyName = "Brijesh Kumar Yadav";
        public const string Version = "1.0.0";
        public const int VersionCode = 1;
        private const string IconPath = "Assets/EVTrainer/App/Branding/AppIcon.png";
        private const string BuildFolder = "Builds";

        [MenuItem("EV Trainer/Release/Apply Release Player Settings")]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = VersionCode;

            // Minimal splash: no Unity logo, plain black, as short as allowed.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = Color.black;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;

            // Info logs carry no stack traces (release filters them out anyway); warnings and errors keep theirs.
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

            var icon = BuildIcon();
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Application);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EVTrainer] Player settings: {ProductName} {Version} ({VersionCode}) by {CompanyName}, icon {IconPath}, splash off.");
        }

        [MenuItem("EV Trainer/Release/Build Quest Release APK")]
        public static void BuildRelease() => Build(development: false);

        [MenuItem("EV Trainer/Release/Build Quest Development APK (profiling)")]
        public static void BuildDevelopment() => Build(development: true);

        public static BuildReport Build(bool development)
        {
            ApplyPlayerSettings();
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = development;
            Directory.CreateDirectory(BuildFolder);
            string file = Path.Combine(BuildFolder, $"EVServiceTrainer-{Version}{(development ? "-dev" : "")}.apk");
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            var options = development
                ? BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging
                : BuildOptions.None;
            // XR and Input System packages add their settings to the preloaded assets during a build and leave them
            // there; put the list back so a build never changes the project settings.
            var preloaded = PlayerSettings.GetPreloadedAssets();
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    locationPathName = file,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = options,
                });
            }
            finally
            {
                PlayerSettings.SetPreloadedAssets(preloaded);
                EditorUserBuildSettings.development = false; // the editor default stays "release"
                AssetDatabase.SaveAssets();
            }
            var summary = report.summary;
            long bytes = File.Exists(file) ? new FileInfo(file).Length : 0;
            Debug.Log($"[EVTrainer] {(development ? "Development" : "Release")} build {summary.result}: {file} " +
                      $"{bytes / (1024f * 1024f):0.0} MB in {summary.totalTime.TotalMinutes:0.0} min, {summary.totalErrors} errors.");
            return report;
        }

        /// <summary>Self-made 512 px app icon: a teal battery with a lightning bolt on a dark rounded tile.</summary>
        private static Texture2D BuildIcon()
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var tile = new Color(0.11f, 0.13f, 0.17f, 1f);
            var teal = new Color(0.05f, 0.75f, 0.72f, 1f);
            var amber = new Color(1f, 0.78f, 0.2f, 1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                // Tile: rounded square (radius 96).
                float d = RoundedBox(p - new Vector2(256f, 256f), new Vector2(256f, 256f), 96f);
                var c = tile;
                c.a = Mathf.Clamp01(0.5f - d);
                // Battery: outline 22 px, body 300 x 190, terminal nub on the right.
                float body = RoundedBox(p - new Vector2(240f, 256f), new Vector2(150f, 95f), 26f);
                float outline = Mathf.Abs(body) - 11f;
                float nub = RoundedBox(p - new Vector2(408f, 256f), new Vector2(18f, 40f), 8f);
                float battery = Mathf.Min(outline, nub);
                c = Color.Lerp(c, teal, Mathf.Clamp01(0.5f - battery) * c.a);
                // Lightning bolt inside the battery.
                float bolt = Bolt(p);
                c = Color.Lerp(c, amber, Mathf.Clamp01(0.5f - bolt) * c.a);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
            File.WriteAllBytes(IconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(IconPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        /// <summary>Signed distance to a rounded box (centre at the origin, half size, corner radius), in pixels.</summary>
        private static float RoundedBox(Vector2 p, Vector2 half, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(radius, radius);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        /// <summary>Signed distance to a lightning bolt: two slanted bars joined in the middle.</summary>
        private static float Bolt(Vector2 p)
        {
            float upper = Segment(p, new Vector2(268f, 330f), new Vector2(222f, 250f)) - 17f;
            float middle = Segment(p, new Vector2(222f, 250f), new Vector2(262f, 262f)) - 17f;
            float lower = Segment(p, new Vector2(262f, 262f), new Vector2(214f, 182f)) - 17f;
            return Mathf.Min(upper, Mathf.Min(middle, lower));
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - a - ab * h).magnitude;
        }
    }
}
