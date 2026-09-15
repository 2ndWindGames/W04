#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

/// <summary>Opt-in integration checks and real player captures for the isolated breach preview.</summary>
public sealed class SobokBreachPreview : MonoBehaviour
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private StackGame game;
    private string output;
    private int checks;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sobok-breach-check") < 0) return;
        // Integration checks must never touch the production PlayerPrefs profile.
        if (Application.productName != "SOBOK Breach Preview")
        {
            Debug.LogError("Breach checks require the isolated SOBOK Breach Preview player.");
            Application.Quit(2);
            return;
        }
        new GameObject("Breach integration checks").AddComponent<SobokBreachPreview>();
    }

    private IEnumerator Start()
    {
        output = Environment.GetEnvironmentVariable("SOBOK_BREACH_CAPTURE");
        if (string.IsNullOrEmpty(output)) { Application.Quit(2); yield break; }
        try { Directory.CreateDirectory(output); }
        catch (Exception e) { Fail(e); yield break; }
        yield return null;
        game = FindFirstObjectByType<StackGame>();
        try
        {
            Require(game != null && Get<bool>("ready"), "Game initializes");
            Set("previewFrozen", true);
            game.enabled = false;
            CheckBuilding();
            CheckCleanBreach();
            CheckPartialBreach(true);
            CheckPartialBreach(false);
            CheckFatalBreach();
            CheckSupportContinuity();
            CheckResetFromEveryPhase();
            Call("Restart");
        }
        catch (Exception e) { Fail(e); yield break; }

        // Flush deferred destruction before rendering the actual gameplay scene.
        yield return null;
        try
        {
            BuildFive();
            Call("FollowCamera", true);
            game.enabled = true;
        }
        catch (Exception e) { Fail(e); yield break; }
        yield return new WaitForEndOfFrame();
        if (!SaveScreenshot("breach-ready.png")) yield break;
        try
        {
            Call("Fire");
            Set("shotStartedAt", Time.unscaledTime - .65f);
            Call("TickFlight", Time.unscaledTime);
            Require(State != "GameOver", "The first generated wall is survivable by a centered tower");
            Call("FollowCamera", true);
        }
        catch (Exception e) { Fail(e); yield break; }
        yield return new WaitForEndOfFrame();
        if (!SaveScreenshot("breach-impact.png")) yield break;
        try
        {
            Call("TickFlight", Get<float>("shotStartedAt") + 1.2f);
            if (State == "Salvage")
            {
                float started = Get<float>("salvageStartedAt");
                Call("TickSalvage", started + .4f, true);
                Call("TickSalvage", started + 2f, false);
            }
            Require(State == "Result", "Visible impact completes with a surviving tower");
            Call("FollowCamera", true);
        }
        catch (Exception e) { Fail(e); yield break; }
        yield return new WaitForEndOfFrame();
        if (!SaveScreenshot("breach-result.png")) yield break;
        try
        {
            File.WriteAllText(Path.Combine(output, "checks.txt"), "PASS: " + checks +
                " assertions covering placement overlap, preserved slab shape, the five-block launch gate, " +
                "clean/partial/fatal wall intersections, support continuity, one-time impact scoring, salvage catch/timeout, " +
                "recovered material use, continued stacking on the surviving tower, restart from every phase, " +
                "and three real player screenshots.\n");
        }
        catch (Exception e) { Fail(e); yield break; }
        Application.Quit(0);
    }

    private void CheckBuilding()
    {
        Call("Restart");
        Require(State == "Building" && Slabs.Count == 1 && Get<int>("placedThisRound") == 0,
            "A new run starts on the base with no placed blocks");
        Transform first = Get<Transform>("moving");
        Call("Fire");
        Call("ContinueRun");
        Require(State == "Building" && Get<Transform>("moving") == first,
            "Premature launch and continue inputs do not skip building");
        float originalWidth = first.localScale.x;
        Place(.7f);
        Near(Slabs[Slabs.Count - 1].localScale.x, originalWidth, "Offset placement preserves the whole slab for shaping");
        Near(Slabs[Slabs.Count - 1].position.x, .7f, "Offset placement preserves the chosen horizontal position");
        Require(Get<int>("placedThisRound") == 1 && State == "Building", "One placed slab advances building once");
        for (int i = 1; i < 5; i++) Place(0);
        Require(State == "Ready" && Get<int>("placedThisRound") == 5 && Slabs.Count == 6,
            "Exactly five placements prepare launch");
        Require(Get<Transform>("moving") == null, "No extra moving slab remains while ready");
        Call("Place");
        Call("ContinueRun");
        Require(State == "Ready" && Slabs.Count == 6, "Ready waits for an explicit launch");
        Call("Restart");
        Place(5f);
        Require(State == "GameOver" && Get<int>("placedThisRound") == 0,
            "A fully unsupported placement ends the run");
    }

    private void CheckCleanBreach()
    {
        PrepareWall(0f, 4f);
        int beforePoints = Get<int>("points");
        Call("Fire");
        Require(State == "Launching", "Launch begins from ready");
        float started = Get<float>("shotStartedAt");
        Call("Fire");
        Near(Get<float>("shotStartedAt"), started, "Repeated launch input cannot restart flight");
        Call("TickFlight", started + 1.2f);
        Require(State == "Result" && Get<int>("wallsCleared") == 1, "A clean tower clears the wall without salvage");
        Require(Slabs.Count == 6, "A clean breach keeps all tower layers");
        Require(Get<int>("points") > beforePoints, "Surviving a wall awards points");
        foreach (Transform slab in Slabs) Near(slab.localScale.x, 3f, "Clean breach preserves slab width");
        int awarded = Get<int>("points");
        Call("TickFlight", started + 2f);
        Call("Fire");
        Require(Get<int>("wallsCleared") == 1 && Get<int>("points") == awarded,
            "A resolved impact cannot award a second wall or score");
        int retained = Slabs.Count;
        Call("ContinueRun");
        Require(State == "Building" && Slabs.Count == retained && Get<int>("placedThisRound") == 0,
            "Continue keeps the surviving tower and starts another five placements");
        Require(Get<Transform>("moving") != null, "Continuing spawns the next block");
        BuildFive();
        Require(Slabs.Count == retained + 5 && State == "Ready", "The next wall uses five new tower layers");
        Require(Get<float[]>("wallCenters").Length == Get<float[]>("wallWidths").Length,
            "Next wall centers and widths describe matching rows");
    }

    private void CheckPartialBreach(bool catchPiece)
    {
        PrepareWall(0f, 1.8f);
        Call("Fire");
        float launched = Get<float>("shotStartedAt");
        Call("TickFlight", launched + 1.2f);
        Require(State == "Salvage" && Get<int>("wallsCleared") == 1,
            "A partial breach enters salvage after clearing the wall");
        Require(Slabs.Count == 6, "A centered partial breach keeps every tower layer");
        // The foundation may sit below the cutout; every placed layer must fit its row.
        for (int i = 1; i < Slabs.Count; i++)
        {
            Near(Slabs[i].localScale.x, 1.8f, "A placed slab is clipped to the wall opening");
            Near(Slabs[i].position.x, 0f, "Centered clipping preserves the slab center");
        }
        int points = Get<int>("points");
        Call("TickFlight", launched + 2f);
        Require(Get<int>("points") == points && Get<int>("wallsCleared") == 1,
            "Partial breach scores once even if flight is ticked again");
        float started = Get<float>("salvageStartedAt");
        float beforeSpare = Get<float>("spareWidth");
        if (catchPiece)
        {
            Call("TickSalvage", started + .4f, true);
            Call("TickSalvage", started + 2f, false);
            Require(Get<float>("spareWidth") > beforeSpare, "Catching debris recovers material for the next block");
            float earned = Get<float>("spareWidth");
            Call("TickSalvage", started + 2.1f, true);
            Near(Get<float>("spareWidth"), earned, "Repeated salvage input does not duplicate recovered material");
        }
        else
        {
            Call("TickSalvage", started + 1.3f, false);
            Near(Get<float>("spareWidth"), beforeSpare, "Salvage timeout grants no recovered material");
        }
        Require(State == "Result", "Salvage resolves to the wall result");
        Require(Get<int>("points") == points && Slabs.Count == 6,
            "Salvage does not remove surviving layers or change the impact score");
        float previousWidth = Slabs[Slabs.Count - 1].localScale.x;
        Call("ContinueRun");
        float incomingWidth = Get<Transform>("moving").localScale.x;
        if (catchPiece)
        {
            Require(incomingWidth > previousWidth && incomingWidth <= 3.001f,
                "Recovered material broadens the next incoming block, capped at the initial width");
            Near(Get<float>("spareWidth"), 0f, "Spawning consumes recovered material once");
        }
        else Near(incomingWidth, previousWidth, "Skipping recovery continues with the surviving width");
        Require(State == "Building" && Slabs.Count == 6, "Salvage continue resumes on the preserved tower");
    }

    private void CheckFatalBreach()
    {
        PrepareWall(10f, 1f);
        int beforePoints = Get<int>("points");
        Call("Fire");
        float launched = Get<float>("shotStartedAt");
        Call("TickFlight", launched + 1.2f);
        Require(State == "GameOver", "A tower with no surviving opening ends the run");
        Require(Get<int>("wallsCleared") == 0 && Get<int>("points") == beforePoints,
            "A fatal wall awards neither a clear nor points");
        Call("ContinueRun");
        Require(State == "GameOver", "Continue cannot revive a destroyed tower");
        Call("TickFlight", launched + 2f);
        Require(Get<int>("wallsCleared") == 0 && Get<int>("points") == beforePoints,
            "A repeated fatal impact remains scoreless");
    }

    private void CheckSupportContinuity()
    {
        PrepareWall(0f, 1.8f);
        float[] centers = Get<float[]>("wallCenters");
        float[] widths = Get<float[]>("wallWidths");
        // The middle row has no opening under the tower. Any rows above it must fall too.
        Require(centers.Length >= 5, "The wall has a cutout row for every placed slab");
        int unsupportedRow = centers.Length / 2;
        centers[unsupportedRow] = 10f;
        widths[unsupportedRow] = 1f;
        Call("Fire");
        Call("TickFlight", Get<float>("shotStartedAt") + 1.2f);
        Require(State != "GameOver" && Slabs.Count > 1 && Slabs.Count < 6,
            "A missing middle layer removes unsupported upper layers while preserving the lower tower");
        for (int i = 1; i < Slabs.Count; i++)
        {
            Transform below = Slabs[i - 1];
            Transform above = Slabs[i];
            float overlap = Mathf.Min(below.position.x + below.localScale.x * .5f, above.position.x + above.localScale.x * .5f)
                - Mathf.Max(below.position.x - below.localScale.x * .5f, above.position.x - above.localScale.x * .5f);
            Require(overlap >= .119f, "Every surviving upper slab has enough horizontal support");
            Near(above.position.y - below.position.y, .45f, "Surviving layers remain vertically continuous");
        }
    }

    private void CheckResetFromEveryPhase()
    {
        Call("Restart");
        AssertReset("Building");
        BuildFive();
        AssertReset("Ready");
        PrepareWall(0f, 1.8f);
        Call("Fire");
        AssertReset("Launching");
        PrepareWall(0f, 1.8f);
        Call("Fire");
        Call("TickFlight", Get<float>("shotStartedAt") + 1.2f);
        AssertReset("Salvage");
        PrepareWall(0f, 1.8f);
        Call("Fire");
        Call("TickFlight", Get<float>("shotStartedAt") + 1.2f);
        Call("TickSalvage", Get<float>("salvageStartedAt") + .4f, true);
        Require(State == "Returning" || State == "Result", "A caught piece returns or finishes salvage");
        AssertReset(State);
        PrepareWall(0f, 4f);
        Call("Fire");
        Call("TickFlight", Get<float>("shotStartedAt") + 1.2f);
        AssertReset("Result");
        Call("Restart");
        Place(5f);
        AssertReset("GameOver");
    }

    private void AssertReset(string from)
    {
        Require(State == from, "Reset test starts in " + from);
        Call("Restart");
        Require(State == "Building" && Slabs.Count == 1 && Get<Transform>("moving") != null,
            "Restart from " + from + " restores the base and moving block");
        Require(Get<int>("placedThisRound") == 0 && Get<int>("wallsCleared") == 0 && Get<int>("points") == 0,
            "Restart from " + from + " clears run progress");
        Near(Get<float>("spareWidth"), 0f, "Restart from " + from + " clears spare material");
        Near(Slabs[0].localScale.x, 3f, "Restart from " + from + " restores the foundation width");
    }

    private void PrepareWall(float center, float width)
    {
        Call("Restart");
        BuildFive();
        float[] centers = Get<float[]>("wallCenters");
        float[] widths = Get<float[]>("wallWidths");
        Require(centers.Length == widths.Length && centers.Length >= 5, "Wall cutout arrays contain matching rows");
        for (int i = 0; i < centers.Length; i++) { centers[i] = center; widths[i] = width; }
    }

    private void BuildFive()
    {
        for (int i = 0; i < 5; i++) Place(0f);
        Require(State == "Ready", "Five placements reach ready");
    }

    private void Place(float offset)
    {
        Transform moving = Get<Transform>("moving");
        Require(moving != null, "A moving block exists before placement");
        Transform top = Slabs[Slabs.Count - 1];
        moving.position = top.position + new Vector3(offset, .45f, 0f);
        Call("Place");
    }

    private List<Transform> Slabs => Get<List<Transform>>("slabs");
    private string State => Get<object>("state").ToString();
    private T Get<T>(string name) => (T)typeof(StackGame).GetField(name, Private).GetValue(game);
    private void Set(string name, object value) => typeof(StackGame).GetField(name, Private).SetValue(game, value);
    private void Call(string name, params object[] args) => typeof(StackGame).GetMethod(name, Private).Invoke(game, args);
    private void Near(float value, float expected, string message) => Require(Mathf.Abs(value - expected) < .001f,
        message + " (expected " + expected + ", got " + value + ")");
    private void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private bool SaveScreenshot(string name)
    {
        Texture2D texture = null;
        try
        {
            texture = ScreenCapture.CaptureScreenshotAsTexture();
            if (texture == null) throw new Exception("Screen capture unavailable. Run a window with -force-d3d11.");
            byte[] bytes = texture.EncodeToPNG();
            if (bytes == null || bytes.Length == 0) throw new Exception("Screenshot PNG encoding failed.");
            File.WriteAllBytes(Path.Combine(output, name), bytes);
            return true;
        }
        catch (Exception e) { Fail(e); return false; }
        finally { if (texture != null) Destroy(texture); }
    }

    private void Fail(Exception exception)
    {
        try { File.WriteAllText(Path.Combine(output, "checks.txt"), "FAIL after " + checks + " assertions: " + exception); }
        catch (Exception outputException) { Debug.LogException(outputException); }
        Debug.LogException(exception);
        Application.Quit(1);
    }
}
#endif
