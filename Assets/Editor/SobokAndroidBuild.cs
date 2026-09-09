using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class SobokAndroidBuild
{
    public static void BuildRelease()
    {
        string password = Environment.GetEnvironmentVariable("SOBOK_KEYSTORE_PASSWORD");
        if (string.IsNullOrEmpty(password))
            throw new BuildFailedException("SOBOK_KEYSTORE_PASSWORD is required.");
        string keyPassword = Environment.GetEnvironmentVariable("SOBOK_KEY_PASSWORD") ?? password;
        string keystore = Path.GetFullPath("user.keystore");
        if (!File.Exists(keystore)) throw new BuildFailedException("Missing root user.keystore.");
        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length == 0) throw new BuildFailedException("No enabled build scenes.");
        string output = Path.GetFullPath(Path.Combine("Builds", "Android",
            "SOBOK-" + PlayerSettings.bundleVersion + "-" + PlayerSettings.Android.bundleVersionCode + ".aab"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        bool previousBundle = EditorUserBuildSettings.buildAppBundle;
        try
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = password;
            PlayerSettings.Android.keyaliasPass = keyPassword;
            EditorUserBuildSettings.buildAppBundle = true;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Android release build failed. See build log.");
            UnityEngine.Debug.Log("[SOBOK] Signed release bundle: " + output);
        }
        finally
        {
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            EditorUserBuildSettings.buildAppBundle = previousBundle;
        }
    }
}
