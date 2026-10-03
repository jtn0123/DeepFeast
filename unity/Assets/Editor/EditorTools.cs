using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepFeast.EditorTools
{
    /// <summary>
    /// One-click project setup and builds. Also usable headless:
    ///   Unity -batchmode -quit -projectPath . -executeMethod DeepFeast.EditorTools.Build.Setup
    ///   Unity -batchmode -quit -projectPath . -executeMethod DeepFeast.EditorTools.Build.Mac
    ///   Unity -batchmode -quit -projectPath . -executeMethod DeepFeast.EditorTools.Build.WebGL
    /// </summary>
    public static class Build
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Deep Feast/Setup Project")]
        public static void Setup()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "jtn0123";
            PlayerSettings.productName = "Deep Feast";
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            var code = "/Applications/Visual Studio Code.app";
            if (Directory.Exists(code)) EditorPrefs.SetString("kScriptsDefaultApp", code);

            AssetDatabase.SaveAssets();
            Debug.Log("[DeepFeast] project setup done");
        }

        [MenuItem("Deep Feast/Build macOS")]
        public static void Mac() => Run(BuildTarget.StandaloneOSX, "Builds/Mac/DeepFeast.app");

        [MenuItem("Deep Feast/Build WebGL")]
        public static void WebGL() => Run(BuildTarget.WebGL, "Builds/WebGL");

        static void Run(BuildTarget target, string path)
        {
            Setup();
            ArtValidation.Check();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[DeepFeast] build {target}: {s.result} errors={s.totalErrors} size={s.totalSize / 1048576f:0.0}MB time={s.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}

namespace DeepFeast.EditorTools
{
    /// <summary>Import settings for the cut-out concept sprites (tools/cut_concepts.py).</summary>
    public sealed class ConceptArtImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Concept/") || assetPath.Contains("/Concept/atlas-")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 2;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
