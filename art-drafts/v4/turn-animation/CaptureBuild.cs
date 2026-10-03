using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace DeepFeast.EditorTools
{
    public static class CaptureBuild
    {
        public static void Run()
        {
            Build.Setup();
            var shader = Resources.Load<Shader>("Shaders/FishSwim");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new System.InvalidOperationException("Fish shader compilation failed.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = "Builds/Mac/CaptureReview.app",
                target = BuildTarget.StandaloneOSX, options = BuildOptions.None
            });
            Debug.Log($"[TurnReview] build: {report.summary.result}, errors={report.summary.totalErrors}.");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
