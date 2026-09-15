#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

/// <summary>Opt-in player integration checks for the isolated five-game comparison build.</summary>
public sealed class SobokArcadePreview : MonoBehaviour
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private SobokArcadeHub hub;
    private string output;
    private int checks;
    private readonly List<string> results = new List<string>();
    private readonly Dictionary<string,int?> savedPrefs = new Dictionary<string,int?>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sobok-arcade-check") < 0) return;
        if (Application.productName != "SOBOK Five Games Preview")
        {
            Debug.LogError("Arcade checks require the isolated SOBOK Five Games Preview player.");
            Application.Quit(2);
            return;
        }
        new GameObject("Five-game integration checks").AddComponent<SobokArcadePreview>();
    }

    private IEnumerator Start()
    {
        output = Environment.GetEnvironmentVariable("SOBOK_ARCADE_CAPTURE");
        if (string.IsNullOrEmpty(output)) { Debug.LogError("SOBOK_ARCADE_CAPTURE is required."); Application.Quit(2); yield break; }
        if (!Attempt(() => { Directory.CreateDirectory(output); CapturePrefs(); })) yield break;
        yield return null;
        yield return null;
        hub = FindFirstObjectByType<SobokArcadeHub>();
        if (!Attempt(() => {
            Require(hub != null && hub.IsMenuOpen && hub.ActiveMode == null, "Player opens the five-game menu");
            CheckOwnership(-1);
        })) yield break;
        yield return new WaitForEndOfFrame();
        if (!SaveScreenshot("00-menu.png")) yield break;

        string[] captures = { "01-original.png", "02-garden.png", "03-breach.png", "04-bridge.png", "05-battle.png" };
        for (int mode = 0; mode < 5; mode++)
        {
            hub.SelectMode(mode);
            yield return null;
            yield return null;
            yield return null;
            if (!Attempt(() => {
                CheckOwnership(mode);
                Freeze(hub.ActiveMode);
                switch (mode)
                {
                    case 0: CheckClassic((SobokClassicGame)hub.ActiveMode); break;
                    case 1: CheckGarden((SobokGardenGame)hub.ActiveMode); break;
                    case 2: CheckBreach((StackGame)hub.ActiveMode); break;
                    case 3: CheckBridge((SobokBridgeGame)hub.ActiveMode); break;
                    case 4: CheckBattle((SobokBattleGame)hub.ActiveMode); break;
                }
            })) yield break;
            // Destroy() is deferred; flush old round geometry before capturing the live renderer.
            yield return null;
            yield return null;
            if (mode == 1) yield return new WaitForSecondsRealtime(.65f);
            yield return new WaitForEndOfFrame();
            if (!SaveScreenshot(captures[mode])) yield break;
            if (!Attempt(() => CheckOwnership(mode))) yield break;

            hub.ShowMenu();
            yield return null;
            yield return null;
            yield return null;
            if (!Attempt(() => CheckOwnership(-1))) yield break;
        }

        // Switching directly between modes uses the same cleanup path as returning through the menu.
        hub.SelectMode(3);
        yield return null; yield return null; yield return null;
        if (!Attempt(() => CheckOwnership(3))) yield break;
        hub.SelectMode(0);
        yield return null; yield return null; yield return null;
        if (!Attempt(() => CheckOwnership(0))) yield break;
        hub.ShowMenu();
        yield return null; yield return null; yield return null;
        if (!Attempt(() => {
            CheckOwnership(-1);
            RestorePrefs();
            File.WriteAllText(Path.Combine(output,"checks.txt"), "PASS: " + checks + " assertions\n" + string.Join("\n",results) +
                "\nSix real player screenshots captured. Five modes returned to the menu without duplicate active audio, skies, cameras, or game controllers.\n");
            Debug.Log("[SOBOK ARCADE CHECK] PASS: " + checks + " assertions; screenshots: " + output);
        })) yield break;
        Application.Quit(0);
    }

    private void CheckClassic(SobokClassicGame game)
    {
        Require(game.Ready && !game.GameOver && game.Floors == 0, "Original initializes an empty run");
        ClassicPlace(game,.6f);
        Near(game.TopBlock.localScale.x,2.4f,"Original clips the overhang");
        Require(game.Floors == 1 && game.Energy == 0,"Imperfect placement adds a floor without perfect energy");
        for (int i=0;i<3;i++) ClassicPlace(game,0);
        Require(game.Energy == 3 && game.PerfectPlacements == 3,"Perfect placements build restoration energy");
        game.RestoreWidth();
        Near(game.TopBlock.localScale.x,3,"Restoration widens the surviving tower");
        Near(game.MovingBlock.localScale.x,3,"Restoration also updates the next block");
        Require(game.Energy == 0,"Restoration consumes three energy");
        int floors = game.Floors;
        ClassicPlace(game,10);
        Require(game.GameOver && game.MovingBlock == null && game.Floors == floors,"A complete miss ends the original run without adding a floor");
        hub.RestartCurrent();
        Require(game.Floors == 0 && game.Points == 0 && game.Energy == 0 && !game.GameOver,"Hub retry resets original progress");
        for (int i=0;i<8;i++) ClassicPlace(game,0);
        Require(game.Floors == 8 && game.Points > 80,"Original supports continued stacking and score multipliers");
        game.FollowCamera(true);
        results.Add("Original: clipping, perfect placements, paid width restoration, loss, hub retry, endless score.");
    }

    private void CheckGarden(SobokGardenGame game)
    {
        Require(game.Ready && game.FlowerCount == 0,"Garden initializes without leftover flowers");
        ClassicPlace(game,.6f);
        Require(game.FlowerCount == 2,"Imperfect garden placement grows two flowers");
        hub.RestartCurrent();
        Require(game.Floors == 0 && game.FlowerCount == 0,"Garden retry clears flowers and floors");
        for (int i=0;i<15;i++) ClassicPlace(game,0);
        Require(game.Completed && !game.GameOver && game.Floors == 15,"Garden completes at fifteen floors");
        Require(game.FlowerCount == 65 && game.PerfectPlacements == 15,"Fifteen perfect layers grow sixty flowers and a five-flower bouquet");
        Require(game.MovingBlock == null,"Completed garden stops spawning blocks");
        game.PlaceBlock();
        Require(game.Floors == 15 && game.FlowerCount == 65,"Completed garden cannot accidentally add another layer");
        game.FollowCamera(true);
        results.Add("Garden: imperfect blooms, retry cleanup, fifteen-floor finish, sixty-five flowers, completed input gate.");
    }

    private void CheckBreach(StackGame game)
    {
        Require(Get<bool>(game,"ready") && State(game) == "Building","Breach initializes in building mode");
        Call(game,"Fire");
        Require(State(game) == "Building","Breach cannot fire before five placements");
        BuildBreachFive(game);
        Require(State(game) == "Ready" && Get<Transform>(game,"moving") == null,"Five slabs unlock the breach launch");
        var slabs = Get<List<Transform>>(game,"slabs");
        Require(slabs.Count == 6,"Breach tower includes the foundation and five placed slabs");
        float[] centers = Get<float[]>(game,"wallCenters"), widths = Get<float[]>(game,"wallWidths");
        for (int i=0;i<centers.Length;i++) { centers[i]=0; widths[i]=1.8f; }
        Call(game,"Fire");
        Require(State(game) == "Launching","Breach launch starts flight");
        float started = Get<float>(game,"shotStartedAt");
        Call(game,"TickFlight",started+.65f);
        Require(Get<int>(game,"wallsCleared") == 1 && slabs.Count == 6,"Partial breach preserves every supported layer and clears one wall");
        Near(slabs[5].localScale.x,1.8f,"Wall clips the tower to the opening width");
        int points = Get<int>(game,"points");
        Call(game,"TickFlight",started+.8f);
        Require(Get<int>(game,"points") == points && Get<int>(game,"wallsCleared") == 1,"Breach impact is scored once");
        Call(game,"TickFlight",started+1.2f);
        Require(State(game) == "Salvage","Cut material opens the reclaim phase");
        float salvage = Get<float>(game,"salvageStartedAt");
        Call(game,"TickSalvage",salvage+.4f,true);
        Require(State(game) == "Returning" && Get<float>(game,"spareWidth") > 0,"Timed catch reclaims useful material");
        Call(game,"TickSalvage",salvage+2f,false);
        Require(State(game) == "Result","Reclaim animation finishes with the surviving tower");
        Call(game,"ContinueRun");
        Require(State(game) == "Building" && Get<Transform>(game,"moving").localScale.x > 1.8f,"Reclaimed material broadens the next block");
        hub.RestartCurrent();
        Require(Get<int>(game,"wallsCleared") == 0 && Get<int>(game,"points") == 0 && Get<List<Transform>>(game,"slabs").Count == 1,"Hub retry resets the breach run");
        BuildBreachFive(game);
        Call(game,"FollowCamera",true);
        results.Add("Breach: five-placement launch gate, real width clipping, one-time scoring, reclaim timing, material reuse, hub retry.");
    }

    private void CheckBridge(SobokBridgeGame game)
    {
        Require(Get<bool>(game,"ready") && State(game) == "Building","Bridge initializes in building mode");
        Call(game,"TipTower");
        Require(State(game) == "Building","Empty tower cannot tip");
        BridgePlace(game);
        Call(game,"TipTower");
        Call(game,"TickMotion",Get<float>(game,"motionStartedAt")+.9f);
        Require(State(game) == "Falling","A short tower leaves the traveler short of the island");
        Call(game,"TickMotion",Get<float>(game,"motionStartedAt")+3f);
        Require(State(game) == "GameOver","Short-bridge fall ends the run");
        hub.RestartCurrent();
        Require(State(game) == "Building" && Get<int>(game,"crossings") == 0,"Hub retry restarts a bridge run");
        Transform moving = Get<Transform>(game,"moving");
        moving.localPosition += Vector3.forward*10;
        Call(game,"PlaceBlock");
        Require(State(game) == "GameOver","A missed slab can collapse the bridge build");
        game.RestartRun();
        float previousGap = 0;
        for (int round=0;round<5;round++)
        {
            float gap = Get<float>(game,"gap");
            Require(gap > previousGap,"Bridge gaps increase between crossings");
            previousGap = gap;
            int count = Mathf.CeilToInt((gap+.2f)/.55f);
            for (int i=0;i<count;i++) BridgePlace(game);
            Call(game,"TipTower");
            Call(game,"TickMotion",Get<float>(game,"motionStartedAt")+.9f);
            Require(State(game) == "Crossing","A tower that reaches the island becomes a traversable bridge");
            Transform tower = Get<Transform>(game,"tower");
            Require(Vector3.Dot(tower.up,Vector3.right) > .99f,"Actual tower geometry rotates ninety degrees toward the island");
            Call(game,"TickMotion",Get<float>(game,"motionStartedAt")+2.5f);
            Require(Get<int>(game,"crossings") == round+1 && Get<int>(game,"points") == (round+1)*150,"Gold landing awards one crossing and the precision bonus");
            Require(State(game) == (round==4 ? "Won" : "RoundComplete"),"Bridge progression stops at the correct round state");
            if (round<4) Call(game,"BuildRound");
        }
        hub.RestartCurrent();
        Require(Get<int>(game,"points") == 0 && Get<List<Transform>>(game,"layers").Count == 0,"Retry after bridge victory clears the previous tower and score");
        for (int i=0;i<6;i++) BridgePlace(game);
        Call(game,"TipTower");
        Call(game,"TickMotion",Get<float>(game,"motionStartedAt")+.9f);
        Call(game,"TickMotion",Get<float>(game,"motionStartedAt")+1.2f);
        Call(game,"FollowCamera",true);
        results.Add("Bridge: empty launch gate, actual shortfall and stack loss, rotated bridge geometry, five increasing gold crossings, victory and retry.");
    }

    private void CheckBattle(SobokBattleGame game)
    {
        Require(Get<bool>(game,"ready") && game.PlayerHealth == 5 && game.EnemyHealth == 8,"Battle starts with full health and the first enemy");
        game.Fire();
        Require(game.State == SobokBattleGame.BattleState.Building,"Battle cannot attack before five attempts");
        for (int turn=0;turn<5;turn++)
        {
            BattleStack(game,false);
            Require(game.State == SobokBattleGame.BattleState.Ready && game.VolleyPower == 0 && game.Attempts == 5,"Misses consume all five attempts without creating damage");
            game.Fire();
            game.TickBattle(Get<float>(game,"attackStartedAt")+2);
            Require(game.State == SobokBattleGame.BattleState.Counterattack && game.EnemyHealth == 8,"An undamaged enemy counterattacks an empty volley");
            float counter = Get<float>(game,"counterStartedAt");
            game.TickBattle(counter+.7f);
            Require(game.PlayerHealth == 4-turn,"Counterattack removes player health once");
            game.TickBattle(counter+.85f);
            Require(game.PlayerHealth == 4-turn,"Repeated counter frames cannot duplicate damage");
            game.TickBattle(counter+1.2f);
            Require(game.State == (turn==4 ? SobokBattleGame.BattleState.GameOver : SobokBattleGame.BattleState.Result),"Battle counterattack reaches result or death");
            if (turn<4) game.ContinueRun();
        }
        hub.RestartCurrent();
        Require(game.PlayerHealth == 5 && game.Opponent == 1 && game.Score == 0,"Hub retry resets battle health, opponents, and score");
        for (int enemy=1;enemy<=3;enemy++)
        {
            BattleStack(game,true);
            Require(game.VolleyPower == 15,"Five perfect battle slabs make fifteen damage");
            game.Fire();
            game.TickBattle(Get<float>(game,"attackStartedAt")+2);
            Require(game.EnemyHealth == 0 && game.PlayerHealth == 5,"A perfect volley defeats the enemy before it can counterattack");
            Require(game.State == (enemy==3 ? SobokBattleGame.BattleState.Victory : SobokBattleGame.BattleState.Result),"Battle advances through three opponents to victory");
            int score = game.Score;
            game.TickBattle(Get<float>(game,"attackStartedAt")+3);
            Require(game.Score == score,"Finished battle volleys cannot score twice");
            if (enemy<3) { game.ContinueRun(); Require(game.Opponent == enemy+1,"Battle creates the next enemy"); }
        }
        hub.RestartCurrent();
        Require(game.State == SobokBattleGame.BattleState.Building && game.Opponent == 1,"Battle victory can restart through the shared toolbar");
        BattleStack(game,true);
        Call(game,"UpdateCamera",true);
        results.Add("Battle: five-attempt gate, lost shots, repeated counterattack immunity, health depletion, perfect volleys, three-enemy victory, retry.");
    }

    private void CheckOwnership(int mode)
    {
        Require(!hub.IsSwitching && hub.SelectedMode == mode,"Hub finishes switching to selection " + mode);
        Require(hub.IsMenuOpen == (mode<0),"Menu visibility matches the selected mode");
        Require(FindObjectsByType<SobokAudio>(FindObjectsSortMode.None).Length == 1,"Exactly one active sound controller remains");
        Require(FindObjectsByType<SobokSky>(FindObjectsSortMode.None).Length == 1,"Exactly one active sky controller remains");
        Require(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1,"Mode switches reuse the single camera");
        Require(FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length == 2,"Only the selected music and effect sources remain");
        Require(FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length <= 1,"Mode switching does not duplicate event systems");
        int backdrops = 0;
        foreach (Transform item in Camera.main.GetComponentsInChildren<Transform>()) if (item.name == "Illustrated sky") backdrops++;
        Require(backdrops == 1,"Only one illustrated sky backdrop remains attached to the camera");
        int controllers = 0;
        foreach (MonoBehaviour behavior in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (behavior is SobokClassicGame || behavior is StackGame || behavior is SobokBridgeGame || behavior is SobokBattleGame)
            {
                Require(behavior == hub.ActiveMode && behavior.isActiveAndEnabled,"Only the selected game controller remains enabled");
                controllers++;
            }
        Require(controllers == (mode<0 ? 0 : 1),"Old game controllers are removed on mode changes");
    }

    private static void ClassicPlace(SobokClassicGame game,float offset)
    {
        Vector3 position = game.MovingBlock.position;
        position[game.MovementAxis] = game.TopBlock.position[game.MovementAxis]+offset;
        game.MovingBlock.position = position;
        game.PlaceBlock();
    }

    private static void BuildBreachFive(StackGame game)
    {
        for (int i=0;i<5;i++)
        {
            Transform moving = Get<Transform>(game,"moving");
            Vector3 position = moving.localPosition;
            position.x = 0;
            moving.localPosition = position;
            Call(game,"Place");
        }
    }

    private static void BridgePlace(SobokBridgeGame game)
    {
        var layers = Get<List<Transform>>(game,"layers");
        Transform moving = Get<Transform>(game,"moving");
        Vector3 position = moving.localPosition;
        position.z = layers.Count > 0 ? layers[layers.Count-1].localPosition.z : 0;
        moving.localPosition = position;
        Call(game,"PlaceBlock");
    }

    private static void BattleStack(SobokBattleGame game,bool perfect)
    {
        for (int i=0;i<5;i++)
        {
            Transform moving = Get<Transform>(game,"moving");
            var stack = Get<List<Transform>>(game,"stack");
            Vector3 position = moving.localPosition;
            position.x = stack[stack.Count-1].localPosition.x + (perfect ? 0 : 10);
            moving.localPosition = position;
            game.Place();
        }
    }

    private static void Freeze(MonoBehaviour game)
    {
        if (game is SobokClassicGame classic) classic.PreviewFrozen = true;
        else Field(game,"previewFrozen").SetValue(game,true);
    }

    private static FieldInfo Field(object instance,string name)
    {
        for (Type type=instance.GetType();type!=null;type=type.BaseType)
        {
            FieldInfo field = type.GetField(name,Members);
            if (field!=null) return field;
        }
        throw new MissingFieldException(instance.GetType().Name,name);
    }
    private static T Get<T>(object instance,string name) => (T)Field(instance,name).GetValue(instance);
    private static string State(object instance) => Get<object>(instance,"state").ToString();
    private static void Call(object instance,string name,params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(name,Members);
        if (method==null) throw new MissingMethodException(instance.GetType().Name,name);
        method.Invoke(instance,args);
    }
    private void Near(float actual,float expected,string message) => Require(Mathf.Abs(actual-expected)<.001f,message+" ("+actual+" vs "+expected+")");
    private void Require(bool condition,string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private bool Attempt(Action action)
    {
        try { action(); return true; }
        catch (Exception exception) { Fail(exception); return false; }
    }
    private bool SaveScreenshot(string name)
    {
        Texture2D texture = null;
        try
        {
            texture = ScreenCapture.CaptureScreenshotAsTexture();
            if (texture==null) throw new Exception("Screen capture unavailable. Launch a visible -force-d3d11 player.");
            byte[] bytes = texture.EncodeToPNG();
            if (bytes==null || bytes.Length==0) throw new Exception("PNG encoding failed.");
            File.WriteAllBytes(Path.Combine(output,name),bytes);
            return true;
        }
        catch (Exception exception) { Fail(exception); return false; }
        finally { if (texture!=null) Destroy(texture); }
    }
    private void CapturePrefs()
    {
        var keys = new List<string> { "StackGame.Best", "Sobok.Garden.Best", "Sobok.Garden.Completed", "Sobok.Breach.Best" };
        for (int i=0;i<5;i++) { keys.Add("StackGame.Rank."+i); keys.Add("Sobok.Garden.Rank."+i); }
        foreach (string key in keys) savedPrefs[key] = PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
    }
    private void RestorePrefs()
    {
        foreach (var entry in savedPrefs)
        {
            if (entry.Value.HasValue) PlayerPrefs.SetInt(entry.Key,entry.Value.Value);
            else PlayerPrefs.DeleteKey(entry.Key);
        }
        PlayerPrefs.Save();
    }
    private void Fail(Exception exception)
    {
        try { RestorePrefs(); }
        catch (Exception prefsException) { Debug.LogException(prefsException); }
        try { if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output,"checks.txt"),"FAIL after "+checks+" assertions: "+exception); }
        catch (Exception writeException) { Debug.LogException(writeException); }
        Debug.LogException(exception);
        Application.Quit(1);
    }
}
#endif
