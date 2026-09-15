using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Build the five-game comparison player using an isolated preview profile.</summary>
public static class SobokArcadePreviewBuild
{
    public static void Build()
    {
        string output = System.Environment.GetEnvironmentVariable("SOBOK_PREVIEW_EXE");
        if (string.IsNullOrEmpty(output)) throw new BuildFailedException("SOBOK_PREVIEW_EXE is required.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.defaultScreenWidth = 600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        PlayerSettings.productName = "SOBOK Five Games Preview";
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            target = BuildTarget.StandaloneWindows64,
            locationPathName = output,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Five-game build failed: " + report.summary.result);
    }
}
