using System;
using System.Collections;
using System.IO;
using UnityEngine;

public sealed class SobokShare : MonoBehaviour
{
    public bool Busy { get; private set; }
    public bool Capturing { get; private set; }
    public string Status { get; private set; }

    public void Share(int points, int walls)
    {
        Share(points, walls, "walls cleared");
    }

    public void Share(int points, int progress, string unit)
    {
        if (!Busy) StartCoroutine(CaptureAndShare(points, progress, unit));
    }

    private IEnumerator CaptureAndShare(int points, int progress, string unit)
    {
        Busy = Capturing = true;
        Status = "";
        // Capture the final rendered frame, including the score and background.
        yield return new WaitForEndOfFrame();
        Texture2D screenshot = null;
        try
        {
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            string directory = Path.Combine(Application.temporaryCachePath, "sobok-share");
            Directory.CreateDirectory(directory);
            // Keep recent files available to receiving apps; expire only old captures.
            foreach (string old in Directory.GetFiles(directory, "SOBOK-*.png"))
            {
                if (File.GetLastWriteTimeUtc(old) < DateTime.UtcNow.AddDays(-7))
                {
                    try { File.Delete(old); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            string path = Path.Combine(directory, "SOBOK-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(path, screenshot.EncodeToPNG());
#if UNITY_ANDROID && !UNITY_EDITOR
            string caption = "SOBOK · " + progress + " " + unit + " · " + points + " points";
            OpenAndroidShare(path, caption);
#else
            Status = "Screenshot saved. Android opens the share menu.";
            Debug.Log("[SOBOK] Screenshot saved: " + path);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(path);
#endif
#endif
        }
        catch (Exception exception)
        {
            Status = "Could not share. Please try again.";
            Debug.LogException(exception, this);
        }
        finally
        {
            if (screenshot != null) Destroy(screenshot);
            Capturing = false;
        }
        // Consume the release of the share tap before allowing a restart.
        yield return new WaitForSecondsRealtime(.75f);
        Busy = false;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void OpenAndroidShare(string path, string caption)
    {
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var provider = new AndroidJavaClass("androidx.core.content.FileProvider"))
        using (var file = new AndroidJavaObject("java.io.File", path))
        using (var uri = provider.CallStatic<AndroidJavaObject>("getUriForFile", activity,
            activity.Call<string>("getPackageName") + ".sobok.share", file))
        using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
        using (var intentClass = new AndroidJavaClass("android.content.Intent"))
        using (var clipClass = new AndroidJavaClass("android.content.ClipData"))
        using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
        using (var clip = clipClass.CallStatic<AndroidJavaObject>("newUri", resolver, "SOBOK", uri))
        {
            intent.Call<AndroidJavaObject>("setType", "image/png").Dispose();
            intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri).Dispose();
            intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", caption).Dispose();
            // Android Intent.setClipData returns void; requesting an object fails JNI method lookup.
            intent.Call("setClipData", clip);
            intent.Call<AndroidJavaObject>("addFlags", 1).Dispose();
            using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share SOBOK"))
                activity.Call("startActivity", chooser);
        }
    }
#endif
}
