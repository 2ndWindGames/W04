using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Build a silhouette, launch it through a wall, and keep the surviving tower.</summary>
public sealed class StackGame : MonoBehaviour
{
    [SerializeField] private Material blockMaterial;
    private const float Height = .45f;
    private const float Depth = 2.3f;
    private const float WallZ = 7f;
    private const float ContactZ = WallZ - .225f - Depth * .5f;
    private const int LayersPerWall = 5;
    private enum RunState { Building, Ready, Launching, Salvage, Returning, Result, GameOver }
    private RunState state;
    private bool ready;
    private bool startupFailed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool previewFrozen;
#endif
    private Camera view;
    private Material material;
    private MaterialPropertyBlock tint;
    private Transform tower;
    private Transform moving;
    private Transform scenery;
    private Transform effects;
    private Transform targetGuide;
    private SobokBreachWall wall;
    private readonly List<Transform> slabs = new List<Transform>();
    private readonly List<Transform> fragments = new List<Transform>();
    private readonly List<Vector3> fragmentStarts = new List<Vector3>();
    private readonly List<Vector3> fragmentScales = new List<Vector3>();
    private float[] wallCenters;
    private float[] wallWidths;
    private int placedThisRound;
    private int wallsCleared;
    private int points;
    private int best;
    private float phase;
    private float shotStartedAt;
    private float salvageStartedAt;
    private float returnStartedAt;
    private float resultReadyAt;
    private float spareWidth;
    private float lostMaterial;
    private float lastKeptPercent = 100;
    private bool impactResolved;
    private float impactAt = -10;
    private SobokAudio sound;
    private SobokSky sky;
    private SobokShare share;
    private SobokShareButton shareButton;
    private static readonly Color[] Palette = {
        new Color(.72f, .65f, .77f), new Color(.66f, .74f, .65f),
        new Color(.94f, .72f, .59f), new Color(.95f, .84f, .58f),
        new Color(.66f, .77f, .80f)
    };
    private float HudScale => Mathf.Max(.1f, Mathf.Min(Screen.width / 480f, Screen.height / 720f));
    private Rect ShareRect => new Rect(Screen.width * .5f - 25 * HudScale, Screen.height - 190 * HudScale, 50 * HudScale, 50 * HudScale);

    private void Awake()
    {
        try
        {
            Material source = blockMaterial != null ? blockMaterial : Resources.Load<Material>("SobokBlock");
            if (source == null) throw new System.InvalidOperationException("SobokBlock material is missing.");
            material = new Material(source);
            tint = new MaterialPropertyBlock();
            material.SetFloat("_Smoothness", 0);
            material.SetFloat("_Metallic", 0);
            sound = gameObject.AddComponent<SobokAudio>();
            share = gameObject.AddComponent<SobokShare>();
            shareButton = Root("Share button").gameObject.AddComponent<SobokShareButton>();
            shareButton.Initialize(() => {
                if (state == RunState.GameOver && !share.Busy)
                    share.Share(points, wallsCleared);
            });
            view = Camera.main;
            if (view == null) view = new GameObject("Main Camera").AddComponent<Camera>();
            view.orthographic = true;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.78f, .85f, .84f);
            sky = gameObject.AddComponent<SobokSky>();
            sky.Initialize(view);
            best = PlayerPrefs.GetInt("Sobok.Breach.Best", 0);
            Restart();
            ready = true;
            Debug.Log("SOBOK breach prototype ready.");
        }
        catch (System.Exception exception)
        {
            startupFailed = true;
            Debug.LogException(exception, this);
        }
    }

    private Transform Root(string title)
    {
        Transform result = new GameObject(title).transform;
        result.SetParent(transform, false);
        return result;
    }

    private void Restart()
    {
        if (tower != null) Destroy(tower.gameObject);
        if (scenery != null) Destroy(scenery.gameObject);
        if (effects != null) Destroy(effects.gameObject);
        if (targetGuide != null) Destroy(targetGuide.gameObject);
        tower = Root("Tower projectile");
        scenery = Root("Breach course");
        effects = Root("Sheared fragments");
        slabs.Clear();
        fragments.Clear();
        fragmentStarts.Clear();
        fragmentScales.Clear();
        moving = null;
        targetGuide = null;
        wall = null;
        placedThisRound = wallsCleared = points = 0;
        spareWidth = lostMaterial = phase = 0;
        impactAt = -10;
        impactResolved = false;
        lastKeptPercent = 100;
        slabs.Add(Box("Foundation", tower, Vector3.zero, new Vector3(3, Height, Depth), Palette[0]));
        Box("Launch runway", scenery, new Vector3(0, -.33f, 4.5f), new Vector3(4.6f, .12f, 14), new Color(.36f, .47f, .48f));
        for (int i = 0; i < 7; i++)
            Box("Runway light", scenery, new Vector3(0, -.257f, i * 1.7f), new Vector3(.06f, .02f, .55f), new Color(1, .85f, .49f));
        sky.SetFloor(0);
        BuildNextWall();
        state = RunState.Building;
        Spawn();
        FollowCamera(true);
    }

    public void RestartRun() => Restart();

    private Transform Box(string title, Transform parent, Vector3 position, Vector3 size, Color color)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = title;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = size;
        cube.GetComponent<Collider>().enabled = false;
        Renderer renderer = cube.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        tint.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(tint);
        return cube.transform;
    }

    private void BuildNextWall()
    {
        if (wall != null) Destroy(wall.gameObject);
        int rows = slabs.Count + LayersPerWall;
        wallCenters = new float[rows];
        wallWidths = new float[rows];
        for (int i = 0; i < slabs.Count; i++)
        {
            wallCenters[i] = slabs[i].localPosition.x;
            wallWidths[i] = slabs[i].localScale.x + .08f;
        }
        float[] offsets = { 0, .35f, -.3f, .45f, -.15f };
        float[] firstWidths = { 2.6f, 2.3f, 2.5f, 2.2f, 2.4f };
        Transform top = slabs[slabs.Count - 1];
        for (int j = 0; j < LayersPerWall; j++)
        {
            int row = slabs.Count + j;
            wallCenters[row] = top.localPosition.x + offsets[(j + wallsCleared) % LayersPerWall];
            wallWidths[row] = wallsCleared == 0 ? firstWidths[j]
                : Mathf.Max(.8f, Mathf.Min(2.6f, top.localScale.x + .25f) - .1f * ((j + wallsCleared) % 3));
        }
        Transform root = new GameObject("Wall " + (wallsCleared + 1)).transform;
        root.SetParent(scenery, false);
        root.localPosition = new Vector3(0, 0, WallZ);
        wall = root.gameObject.AddComponent<SobokBreachWall>();
        wall.Build(wallCenters, wallWidths, Height, material);
    }

    private void Spawn()
    {
        phase = 0;
        Transform top = slabs[slabs.Count - 1];
        float width = Mathf.Min(3, Mathf.Max(1.2f, top.localScale.x) + spareWidth);
        spareWidth = 0;
        moving = Box("Layer " + slabs.Count, tower, new Vector3(top.localPosition.x - 3.8f, slabs.Count * Height, 0),
            new Vector3(width, Height, Depth), Palette[slabs.Count % Palette.Length]);
        ShowTarget();
    }

    private void ShowTarget()
    {
        if (targetGuide != null) Destroy(targetGuide.gameObject);
        targetGuide = Root("Current opening outline");
        int row = slabs.Count;
        targetGuide.localPosition = new Vector3(wallCenters[row], row * Height + Height * .5f + .025f, 0);
        Color gold = new Color(1, .85f, .49f);
        float width = wallWidths[row];
        Box("Front edge", targetGuide, new Vector3(0, 0, -Depth * .5f - .06f), new Vector3(width, .025f, .035f), gold);
        Box("Back edge", targetGuide, new Vector3(0, 0, Depth * .5f + .06f), new Vector3(width, .025f, .035f), gold);
        Box("Left edge", targetGuide, new Vector3(-width * .5f, 0, 0), new Vector3(.035f, .025f, Depth + .12f), gold);
        Box("Right edge", targetGuide, new Vector3(width * .5f, 0, 0), new Vector3(.035f, .025f, Depth + .12f), gold);
    }

    private void Update()
    {
        if (!ready) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (previewFrozen) return;
#endif
        bool tapped = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
        if (sound.IsSoundPointer() || SobokArcadeHub.BlocksGameplayInput()) tapped = false;
        float now = Time.unscaledTime;
        switch (state)
        {
            case RunState.Building:
                if (tapped) Place();
                if (state == RunState.Building && moving != null)
                {
                    phase += Time.deltaTime * Mathf.Min(2.2f + wallsCleared * .12f, 3.5f);
                    float center = slabs[slabs.Count - 1].localPosition.x;
                    moving.localPosition = new Vector3(center + Mathf.PingPong(phase, 7.6f) - 3.8f, moving.localPosition.y, 0);
                }
                break;
            case RunState.Ready:
                if (tapped) Fire();
                break;
            case RunState.Launching:
                TickFlight(now);
                break;
            case RunState.Salvage:
            case RunState.Returning:
                TickSalvage(now, tapped);
                break;
            case RunState.Result:
                if (tapped && now >= resultReadyAt) ContinueRun();
                break;
            case RunState.GameOver:
                if (!share.Busy && tapped && !IsSharePointer() && now >= resultReadyAt) Restart();
                break;
        }
        FollowCamera(false);
        sound.SetNight(sky.NightAmount);
        if (wall != null) wall.SetImpact(Mathf.Clamp01(1 - (now - impactAt) / .3f));
    }

    private void Place()
    {
        if (state != RunState.Building || moving == null) return;
        Transform top = slabs[slabs.Count - 1];
        float left = moving.localPosition.x - moving.localScale.x * .5f;
        float right = moving.localPosition.x + moving.localScale.x * .5f;
        float overlap = Mathf.Min(right, top.localPosition.x + top.localScale.x * .5f)
            - Mathf.Max(left, top.localPosition.x - top.localScale.x * .5f);
        if (overlap < .12f)
        {
            Debris(moving.position, moving.localScale, Palette[slabs.Count % Palette.Length]);
            Destroy(moving.gameObject);
            moving = null;
            EndRun();
            return;
        }
        int row = slabs.Count;
        bool fits = Mathf.Abs(moving.localPosition.x - wallCenters[row]) + moving.localScale.x * .5f <= wallWidths[row] * .5f;
        sound.Placement(fits);
        slabs.Add(moving);
        moving = null;
        placedThisRound++;
        if (targetGuide != null) Destroy(targetGuide.gameObject);
        if (placedThisRound == LayersPerWall) state = RunState.Ready;
        else Spawn();
    }

    private void Fire()
    {
        if (state != RunState.Ready) return;
        state = RunState.Launching;
        shotStartedAt = Time.unscaledTime;
        impactResolved = false;
        lostMaterial = 0;
        sound.Launch();
    }

    private void TickFlight(float now)
    {
        if (state != RunState.Launching) return;
        float age = Mathf.Max(0, now - shotStartedAt);
        // Pull back, accelerate into the front face, then hold the impact briefly.
        float z = age < .14f ? -.22f * Mathf.Sin(age / .14f * Mathf.PI * .5f)
            : age < .48f ? Mathf.Lerp(-.22f, ContactZ, Mathf.Pow((age - .14f) / .34f, 2))
            : age < .58f ? ContactZ
            : Mathf.Lerp(ContactZ, 10.5f, Mathf.Clamp01((age - .58f) / .57f));
        tower.localPosition = new Vector3(0, 0, z);
        if (age >= .48f && !impactResolved) ResolveImpact(now);
        if (state == RunState.GameOver) return;
        if (age >= 1.15f)
        {
            salvageStartedAt = now;
            state = lostMaterial > .001f ? RunState.Salvage : RunState.Result;
            resultReadyAt = now + .25f;
        }
    }

    private void ResolveImpact(float now)
    {
        if (impactResolved || state != RunState.Launching) return;
        impactResolved = true;
        impactAt = now;
        float before = 0, after = 0;
        bool disconnected = false;
        var kept = new List<Transform>();
        for (int i = 0; i < slabs.Count; i++)
        {
            Transform slab = slabs[i];
            float left = slab.localPosition.x - slab.localScale.x * .5f;
            float right = slab.localPosition.x + slab.localScale.x * .5f;
            before += slab.localScale.x;
            float clipLeft = Mathf.Max(left, wallCenters[i] - wallWidths[i] * .5f);
            float clipRight = Mathf.Min(right, wallCenters[i] + wallWidths[i] * .5f);
            if (!disconnected && kept.Count > 0)
            {
                Transform support = kept[kept.Count - 1];
                float contact = Mathf.Min(clipRight, support.localPosition.x + support.localScale.x * .5f)
                    - Mathf.Max(clipLeft, support.localPosition.x - support.localScale.x * .5f);
                if (contact < .12f) disconnected = true;
            }
            if (disconnected || clipRight - clipLeft < .12f)
            {
                disconnected = true;
                Debris(new Vector3(slab.localPosition.x, slab.localPosition.y, ContactZ), slab.localScale, Palette[i % Palette.Length]);
                Destroy(slab.gameObject);
                continue;
            }
            CutFragment(left, clipLeft, slab, i);
            CutFragment(clipRight, right, slab, i);
            slab.localPosition = new Vector3((clipLeft + clipRight) * .5f, slab.localPosition.y, 0);
            slab.localScale = new Vector3(clipRight - clipLeft, Height, Depth);
            after += slab.localScale.x;
            kept.Add(slab);
        }
        slabs.Clear();
        slabs.AddRange(kept);
        lostMaterial = Mathf.Max(0, before - after);
        lastKeptPercent = before > 0 ? 100 * after / before : 0;
        sound.Breach(lastKeptPercent / 100);
        if (slabs.Count == 0) { EndRun(); return; }
        wallsCleared++;
        points += 100 + Mathf.RoundToInt(lastKeptPercent);
        best = Mathf.Max(best, points);
        PlayerPrefs.SetInt("Sobok.Breach.Best", best);
        PlayerPrefs.Save();
        sky.SetFloor(wallsCleared * 6);
        wall.SetImpact(1);
    }

    private void CutFragment(float left, float right, Transform slab, int row)
    {
        if (right - left < .005f) return;
        Debris(new Vector3((left + right) * .5f, slab.localPosition.y, ContactZ),
            new Vector3(right - left, Height, Depth), Palette[row % Palette.Length]);
    }

    private void Debris(Vector3 position, Vector3 size, Color color)
    {
        Transform piece = Box("Sheared block", effects, position, size, color);
        Rigidbody body = piece.gameObject.AddComponent<Rigidbody>();
        float side = position.x >= 0 ? 1 : -1;
        body.linearDamping = .65f;
        body.linearVelocity = new Vector3(side * (1.6f + .4f * (fragments.Count % 3)), 4.8f + .6f * (fragments.Count % 2), 1.2f);
        body.angularVelocity = new Vector3(2, side * 3, 2);
        fragments.Add(piece);
    }

    private void TickSalvage(float now, bool tapped)
    {
        if (state == RunState.Salvage)
        {
            float age = now - salvageStartedAt;
            if (tapped && age >= .2f && age <= .7f)
            {
                spareWidth = Mathf.Min(.8f, lostMaterial * .25f);
                fragmentStarts.Clear();
                fragmentScales.Clear();
                foreach (Transform piece in fragments)
                {
                    fragmentStarts.Add(piece.position);
                    fragmentScales.Add(piece.localScale);
                    piece.GetComponent<Rigidbody>().isKinematic = true;
                }
                state = RunState.Returning;
                returnStartedAt = now;
                sound.Restore();
            }
            else if (tapped || age >= 1.1f)
            {
                state = RunState.Result;
                resultReadyAt = now + .2f;
            }
        }
        if (state != RunState.Returning) return;
        float t = Mathf.Clamp01((now - returnStartedAt) / .35f);
        Vector3 target = tower.position + slabs[slabs.Count - 1].localPosition + Vector3.up * .7f;
        for (int i = 0; i < fragments.Count; i++)
        {
            fragments[i].position = Vector3.Lerp(fragmentStarts[i], target, Mathf.SmoothStep(0, 1, t));
            fragments[i].localScale = fragmentScales[i] * (1 - t);
        }
        if (t >= 1)
        {
            ClearFragments();
            state = RunState.Result;
            resultReadyAt = now + .2f;
        }
    }

    private void ContinueRun()
    {
        if (state != RunState.Result || slabs.Count == 0) return;
        tower.localPosition = Vector3.zero;
        placedThisRound = 0;
        lostMaterial = 0;
        ClearFragments();
        BuildNextWall();
        state = RunState.Building;
        Spawn();
        FollowCamera(true);
    }

    private void ClearFragments()
    {
        foreach (Transform piece in fragments) if (piece != null) Destroy(piece.gameObject);
        fragments.Clear();
        fragmentStarts.Clear();
        fragmentScales.Clear();
    }

    private void EndRun()
    {
        state = RunState.GameOver;
        resultReadyAt = Time.unscaledTime + .8f;
        if (targetGuide != null) Destroy(targetGuide.gameObject);
        PlayerPrefs.Save();
    }

    private void FollowCamera(bool snap)
    {
        float height = (wallCenters == null ? 6 : wallCenters.Length) * Height;
        float size = Mathf.Max(6.2f, height * .62f + 3, 4.6f / Mathf.Max(.3f, view.aspect));
        view.orthographicSize = snap ? size : Mathf.Lerp(view.orthographicSize, size, Time.deltaTime * 4);
        view.transform.rotation = Quaternion.Euler(22, -18, 0);
        bool passed = state == RunState.Result || state == RunState.Salvage || state == RunState.Returning;
        if (wall != null && passed)
        {
            // Retract the cleared gate so the surviving silhouette is visible.
            float destination = -height - 1;
            Vector3 gatePosition = wall.transform.localPosition;
            gatePosition.y = snap ? destination : Mathf.MoveTowards(gatePosition.y, destination, (height + 1) * Time.unscaledDeltaTime * 6);
            wall.transform.localPosition = gatePosition;
            if (gatePosition.y <= destination) wall.gameObject.SetActive(false);
        }
        Vector3 focus = new Vector3(0, height * .42f + .5f, passed ? 6.5f : 3.4f);
        Vector3 position = focus - view.transform.forward * 22;
        float shock = Mathf.Clamp01(1 - (Time.unscaledTime - impactAt) / .22f);
        if (!snap) position += view.transform.right * (Mathf.Sin(Time.unscaledTime * 95) * shock * .09f);
        view.transform.position = snap ? position : Vector3.Lerp(view.transform.position, position, Time.deltaTime * 7);
    }

    private bool IsSharePointer()
    {
        Vector2 pointer;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            pointer = Touchscreen.current.primaryTouch.startPosition.ReadValue();
        else if (Mouse.current != null) pointer = Mouse.current.position.ReadValue();
        else return false;
        pointer.y = Screen.height - pointer.y;
        return state == RunState.GameOver && ShareRect.Contains(pointer);
    }

    private void LateUpdate()
    {
        if (ready && shareButton != null)
            shareButton.Refresh(ShareRect, sky.NightAmount, state == RunState.GameOver && !share.Capturing, share.Busy);
    }

    private void OnGUI()
    {
        if (!ready)
        {
            if (startupFailed) GUI.Label(new Rect(20, 20, 500, 80), "SOBOK could not start. See the player log.");
            return;
        }
        float scale = HudScale;
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float width = Screen.width / scale, height = Screen.height / scale;
        var text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
        text.normal.textColor = sky.Ink;
        GUI.Label(new Rect(0, 59, width, 27), "S O B O K   /   B R E A C H", text);
        string headline = state == RunState.Building ? placedThisRound + " / 5"
            : state == RunState.Ready ? "READY TO FIRE"
            : state == RunState.GameOver ? "RUN ENDED"
            : state == RunState.Launching ? "BREAK THROUGH"
            : "WALL " + wallsCleared + " CLEARED";
        text.fontSize = 32;
        GUI.Label(new Rect(0, 87, width, 44), headline, text);
        text.fontSize = 14;
        int shownWall = state == RunState.Building || state == RunState.Ready || (state == RunState.Launching && !impactResolved)
            ? wallsCleared + 1 : wallsCleared;
        GUI.Label(new Rect(0, 130, width, 25), "WALL " + shownWall.ToString("00") + "     " + points + " PTS     BEST " + best, text);
        text.fontSize = 13;
        if (state == RunState.Building || state == RunState.Ready)
            GUI.Label(new Rect(0, 155, width, 26), "Read the opening. Shape your tower.", text);
        if (share.Capturing) { GUI.matrix = previous; return; }
        if (state == RunState.GameOver && !string.IsNullOrEmpty(share.Status))
            GUI.Label(new Rect(24, height - 224, width - 48, 28), share.Status, text);
        Color previousColor = GUI.color;
        if (state == RunState.Salvage)
        {
            float timerWidth = width - 60;
            GUI.color = new Color(.12f, .21f, .25f, .8f);
            GUI.DrawTexture(new Rect(30, height - 145, timerWidth, 8), Texture2D.whiteTexture);
            GUI.color = new Color(1, .85f, .49f);
            GUI.DrawTexture(new Rect(30 + timerWidth * (.2f / 1.1f), height - 145, timerWidth * (.5f / 1.1f), 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float timer = Mathf.Clamp01((Time.unscaledTime - salvageStartedAt) / 1.1f);
            GUI.DrawTexture(new Rect(28 + timerWidth * timer, height - 149, 4, 16), Texture2D.whiteTexture);
        }
        GUI.color = new Color(.08f, .14f, .19f, .94f);
        GUI.DrawTexture(new Rect(18, height - 128, width - 36, 100), Texture2D.whiteTexture);
        GUI.color = previousColor;
        string action;
        string detail;
        switch (state)
        {
            case RunState.Building:
                action = "TAP TO STACK";
                detail = "Gold outline = opening. Keep each block overlapping.";
                break;
            case RunState.Ready:
                action = "TAP TO LAUNCH";
                detail = "The wall shears off everything outside the opening.";
                break;
            case RunState.Launching:
                action = "Hold on...";
                detail = "Your entire tower is the projectile.";
                break;
            case RunState.Salvage:
                float age = Time.unscaledTime - salvageStartedAt;
                action = age >= .2f && age <= .7f ? "TAP TO RECLAIM" : "Catch the fragments...";
                detail = "One timed tap. Reclaimed pieces feed the next block.";
                break;
            case RunState.Returning:
                action = "RECLAIMED";
                detail = "Extra material for your next block.";
                break;
            case RunState.Result:
                action = "TAP FOR THE NEXT WALL";
                detail = Mathf.RoundToInt(lastKeptPercent) + "% kept. " + (spareWidth > 0 ? "Next block boosted by reclaimed material." : "Keep the survivors. Build five more blocks.");
                break;
            default:
                action = "TAP TO TRY AGAIN";
                detail = wallsCleared + " walls cleared   /   " + points + " points";
                break;
        }
        text.normal.textColor = new Color(1, .86f, .55f);
        text.fontSize = 22;
        GUI.Label(new Rect(20, height - 119, width - 40, 37), action, text);
        text.fontSize = 13;
        text.wordWrap = true;
        text.normal.textColor = new Color(.86f, .90f, .92f);
        GUI.Label(new Rect(32, height - 80, width - 64, 43), detail, text);
        text.normal.textColor = sky.Ink;
        text.fontSize = 10;
        GUI.Label(new Rect(0, height - 25, width, 22), "TAP / SPACE  TO PLAY       M  SOUND", text);
        GUI.matrix = previous;
    }

    private void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
