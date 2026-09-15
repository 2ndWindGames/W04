using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>The original endless stacking game, also used by the finite flower garden.</summary>
public class SobokClassicGame : MonoBehaviour
{
    protected const float LayerHeight = .35f;
    private const float Travel = 3.7f;
    protected Transform tower;
    private Transform top;
    private Transform moving;
    protected Material material;
    private MaterialPropertyBlock tint;
    private Camera view;
    private SobokAudio sound;
    private SobokSky sky;
    private SobokShare sharing;
    private SobokShareButton shareButton;
    private bool ready;
    private bool startupFailed;
    private int score;
    private int points;
    private int best;
    private int energy;
    private int axis;
    private int perfectPlacements;
    private readonly int[] rankings = new int[5];
    private float phase;
    private float restartAt;
    private float perfectUntil;
    private bool gameOver;
    private bool completed;
    protected static readonly Color[] Palette = {
        new Color(.72f, .65f, .77f), new Color(.66f, .74f, .65f),
        new Color(.94f, .72f, .59f), new Color(.95f, .84f, .58f),
        new Color(.66f, .77f, .80f)
    };
    protected virtual int TargetFloors => 0;
    protected virtual string ModeTitle => "S O B O K / O R I G I N A L";
    protected virtual string SavePrefix => "StackGame";
    public virtual int FlowerCount => 0;
    public bool Ready => ready;
    public int Floors => score;
    public int Points => points;
    public int Energy => energy;
    public int PerfectPlacements => perfectPlacements;
    public bool GameOver => gameOver;
    public bool Completed => completed;
    public Transform MovingBlock => moving;
    public Transform TopBlock => top;
    public int MovementAxis => axis;
    private int Multiplier => 1 + energy / 2;
    private float HudScale => Mathf.Max(.1f, Mathf.Min(Screen.width / 480f, Screen.height / 720f));
    private Rect ChargeRect => new Rect(Screen.width * .5f - 110 * HudScale,
        Screen.height - 127 * HudScale, 220 * HudScale, 42 * HudScale);
    private Rect ShareRect => new Rect(Screen.width * .5f - 25 * HudScale,
        Screen.height - 204 * HudScale, 50 * HudScale, 50 * HudScale);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool PreviewFrozen { get; set; }
#endif

    protected virtual void Awake()
    {
        try
        {
            Material source = Resources.Load<Material>("SobokBlock");
            if (source == null) throw new System.InvalidOperationException("SobokBlock material is missing.");
            material = new Material(source);
            material.SetFloat("_Smoothness", 0);
            material.SetFloat("_Metallic", 0);
            tint = new MaterialPropertyBlock();
            sound = gameObject.AddComponent<SobokAudio>();
            sharing = gameObject.AddComponent<SobokShare>();
            var buttonRoot = new GameObject("Share button", typeof(RectTransform));
            buttonRoot.transform.SetParent(transform, false);
            shareButton = buttonRoot.AddComponent<SobokShareButton>();
            shareButton.Initialize(() => {
                if ((gameOver || completed) && !sharing.Busy)
                    sharing.Share(points, score, "floors");
            });
            view = Camera.main;
            if (view == null)
            {
                view = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                view.tag = "MainCamera";
            }
            view.orthographic = true;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.96f, .93f, .86f);
            view.nearClipPlane = .1f;
            view.farClipPlane = 200;
            view.transform.rotation = Quaternion.Euler(32, 45, 0);
            sky = gameObject.AddComponent<SobokSky>();
            sky.Initialize(view);
            best = PlayerPrefs.GetInt(SavePrefix + ".Best", 0);
            for (int i = 0; i < rankings.Length; i++)
                rankings[i] = PlayerPrefs.GetInt(SavePrefix + ".Rank." + i, 0);
            RestartRun();
            ready = true;
        }
        catch (System.Exception exception)
        {
            startupFailed = true;
            Debug.LogException(exception, this);
        }
    }

    public void RestartRun()
    {
        if (material == null) return;
        if (tower != null)
        {
            tower.gameObject.SetActive(false);
            Destroy(tower.gameObject);
        }
        tower = new GameObject("Stacked layers").transform;
        tower.SetParent(transform, false);
        score = points = energy = perfectPlacements = 0;
        phase = perfectUntil = restartAt = 0;
        gameOver = completed = false;
        sky.SetFloor(0);
        OnRunReset();
        top = Block("Foundation", Vector3.zero, new Vector3(3, LayerHeight, 3), 0);
        Spawn();
        FollowCamera(true);
    }

    protected virtual void OnRunReset() { }
    protected virtual void OnStacked(Transform previous, Transform placed, bool precise) { }
    protected virtual void OnGardenFinished() { }

    protected Transform Block(string title, Vector3 position, Vector3 size, int level)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = title;
        cube.transform.SetParent(tower, false);
        cube.transform.position = position;
        cube.transform.localScale = size;
        cube.GetComponent<Collider>().enabled = false;
        var renderer = cube.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        tint.SetColor("_BaseColor", Palette[level % Palette.Length]);
        renderer.SetPropertyBlock(tint);
        return cube.transform;
    }

    private void Spawn()
    {
        axis = score % 2 == 0 ? 0 : 2;
        phase = 0;
        Vector3 position = top.position + Vector3.up * LayerHeight;
        position[axis] -= Travel;
        moving = Block("Layer " + (score + 1), position, top.localScale, score + 1);
    }

    protected virtual void Update()
    {
        if (!ready) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (PreviewFrozen) return;
#endif
        bool blocked = SobokArcadeHub.BlocksGameplayInput();
        bool pressed = !blocked && ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame));
        if (sound.IsSoundPointer()) pressed = false;
        if (gameOver || completed)
        {
            if (!sharing.Busy && pressed && !IsPointerInside(ShareRect) && Time.unscaledTime >= restartAt)
                RestartRun();
        }
        else
        {
            bool restoreKey = !blocked && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            bool restoreTap = pressed && IsPointerInside(ChargeRect);
            if (restoreKey || restoreTap) RestoreWidth();
            if (pressed && !restoreKey && !restoreTap) PlaceBlock();
            if (!gameOver && !completed && moving != null)
            {
                phase += Time.deltaTime * Mathf.Min(1.7f + score * .025f, 2.5f);
                Vector3 position = moving.position;
                position[axis] = top.position[axis] + Mathf.PingPong(phase, Travel * 2) - Travel;
                moving.position = position;
            }
        }
        FollowCamera(false);
        sound.SetNight(sky.NightAmount);
    }

    public void PlaceBlock()
    {
        if (gameOver || completed || moving == null) return;
        float offset = moving.position[axis] - top.position[axis];
        float width = top.localScale[axis];
        float overlap = width - Mathf.Abs(offset);
        if (overlap <= 0)
        {
            Fall(moving);
            moving = null;
            gameOver = true;
            SaveRanking();
            restartAt = Time.unscaledTime + .6f;
            return;
        }
        bool precise = Mathf.Abs(offset) <= Mathf.Min(.09f, width * .15f);
        if (precise)
        {
            Vector3 position = moving.position;
            position[axis] = top.position[axis];
            moving.position = position;
            perfectUntil = Time.unscaledTime + .7f;
            energy = Mathf.Min(6, energy + 1);
            perfectPlacements++;
            Illuminate(moving);
        }
        else
        {
            Vector3 size = moving.localScale;
            Vector3 position = moving.position;
            Vector3 cutSize = size;
            cutSize[axis] = Mathf.Abs(offset);
            Vector3 cutPosition = position;
            cutPosition[axis] += Mathf.Sign(offset) * overlap * .5f;
            Fall(Block("Offcut", cutPosition, cutSize, score + 1));
            size[axis] = overlap;
            position[axis] -= offset * .5f;
            moving.localScale = size;
            moving.position = position;
        }
        sound.Placement(precise);
        Transform previous = top;
        top = moving;
        moving = null;
        score++;
        points += 10 * Multiplier;
        sky.SetFloor(score);
        OnStacked(previous, top, precise);
        if (points > best)
        {
            best = points;
            PlayerPrefs.SetInt(SavePrefix + ".Best", best);
        }
        if (TargetFloors > 0 && score >= TargetFloors)
        {
            completed = true;
            restartAt = Time.unscaledTime + .8f;
            OnGardenFinished();
            sound.Restore();
            SaveRanking();
            FollowCamera(true);
            return;
        }
        Spawn();
    }

    public void RestoreWidth()
    {
        if (gameOver || completed || moving == null || energy < 3) return;
        if (top.localScale.x >= 3 && top.localScale.z >= 3) return;
        energy -= 3;
        Vector3 size = top.localScale;
        size.x = Mathf.Min(3, size.x + .6f);
        size.z = Mathf.Min(3, size.z + .6f);
        top.localScale = size;
        moving.localScale = size;
        sound.Restore();
        Illuminate(top);
    }

    private void Illuminate(Transform block)
    {
        Transform band = Block("Little light", block.position + Vector3.up * (LayerHeight * .38f),
            Vector3.one, score + 1);
        band.SetParent(block, true);
        band.localScale = new Vector3(1.015f, .1f, 1.015f);
        tint.SetColor("_BaseColor", new Color(1, .93f, .72f));
        band.GetComponent<Renderer>().SetPropertyBlock(tint);
        band.gameObject.AddComponent<SobokFade>();
    }

    private void Fall(Transform block)
    {
        block.GetComponent<Collider>().enabled = false;
        var body = block.gameObject.AddComponent<Rigidbody>();
        body.angularVelocity = new Vector3(1.5f, .6f, 1);
        block.gameObject.AddComponent<SobokFade>();
        Destroy(block.gameObject, 3);
    }

    private void SaveRanking()
    {
        int candidate = points;
        for (int i = 0; i < rankings.Length; i++)
        {
            if (candidate > rankings[i])
            {
                int previous = rankings[i];
                rankings[i] = candidate;
                candidate = previous;
            }
            PlayerPrefs.SetInt(SavePrefix + ".Rank." + i, rankings[i]);
        }
        if (completed) PlayerPrefs.SetInt(SavePrefix + ".Completed", PlayerPrefs.GetInt(SavePrefix + ".Completed", 0) + 1);
        PlayerPrefs.Save();
    }

    public void FollowCamera(bool snap)
    {
        if (view == null || top == null) return;
        float size = Mathf.Max(5.5f, 5.2f / Mathf.Max(view.aspect, .3f));
        Vector3 focus = top.position + Vector3.up * .7f;
        if (TargetFloors > 0)
        {
            // Keep the flowers and the full, finite garden in view as it grows.
            focus = new Vector3(top.position.x * .4f, Mathf.Max(1.7f, score * LayerHeight * .5f), top.position.z * .4f);
            size = Mathf.Max(5.8f, 4.6f / Mathf.Max(view.aspect, .3f));
        }
        view.orthographicSize = size;
        Vector3 target = focus - view.transform.forward * 18;
        view.transform.position = snap ? target : Vector3.Lerp(view.transform.position, target, 1 - Mathf.Exp(-5 * Time.deltaTime));
    }

    private static bool IsPointerInside(Rect rect)
    {
        Vector2 position;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            position = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            position = Mouse.current.position.ReadValue();
        else return false;
        position.y = Screen.height - position.y;
        return rect.Contains(position);
    }

    protected virtual void LateUpdate()
    {
        if (ready) shareButton.Refresh(ShareRect, sky.NightAmount, (gameOver || completed) && !sharing.Capturing, sharing.Busy);
    }

    protected virtual void OnGUI()
    {
        if (!ready)
        {
            if (startupFailed) GUI.Box(new Rect(20, Screen.height * .45f, Screen.width - 40, 70), "SOBOK could not start. Please return to the menu.");
            return;
        }
        float scale = HudScale;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        var label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
        label.normal.textColor = sky.Ink;
        GUI.Label(new Rect(0, 62, width, 30), ModeTitle, label);
        label.fontSize = completed ? 34 : 54;
        string headline = completed ? "Your garden is here." : TargetFloors > 0 ? score + " / " + TargetFloors : score.ToString();
        GUI.Label(new Rect(0, 92, width, 70), headline, label);
        label.fontSize = 15;
        GUI.Label(new Rect(0, 159, width, 30), TargetFloors > 0
            ? FlowerCount + " flowers  /  " + perfectPlacements + " perfect placements"
            : points + " points  /  best " + best, label);
        label.fontSize = 17;
        if (!gameOver && !completed && Time.unscaledTime < perfectUntil)
            GUI.Label(new Rect(0, 191, width, 28), TargetFloors > 0 ? "A little bloom." : "Just right.", label);
        if (gameOver || completed)
        {
            if (!sharing.Capturing && !string.IsNullOrEmpty(sharing.Status))
            {
                label.fontSize = 12;
                GUI.Label(new Rect(12, height - 246, width - 24, 36), sharing.Status, label);
            }
            if (TargetFloors == 0)
            {
                label.fontSize = 15;
                GUI.Label(new Rect(0, 209, width, 28), "Your five best runs", label);
                for (int i = 0; i < rankings.Length; i++)
                    GUI.Label(new Rect(0, 239 + i * 25, width, 25), (i + 1) + ".   " + rankings[i], label);
            }
            Panel(new Rect(18, height - 142, width - 36, 100));
            label.normal.textColor = new Color(1, .91f, .69f);
            label.fontSize = 22;
            GUI.Label(new Rect(0, height - 134, width, 40), completed ? "Made by you, one layer at a time." : "A little pause.", label);
            label.fontSize = 15;
            label.normal.textColor = new Color(.96f, .94f, .88f);
            GUI.Label(new Rect(0, height - 87, width, 32), completed ? "Tap to grow another garden" : "Tap to begin again", label);
        }
        else
        {
            Panel(new Rect(18, height - 178, width - 36, 136));
            label.fontSize = 14;
            label.normal.textColor = new Color(.96f, .94f, .88f);
            GUI.Label(new Rect(0, height - 174, width, 32), "Lights " + energy + "/6   /   Score x" + Multiplier, label);
            Rect charge = ChargeRect;
            charge = new Rect(charge.x / scale, charge.y / scale, charge.width / scale, charge.height / scale);
            Color oldColor = GUI.color;
            GUI.color = new Color(.83f, .86f, .75f);
            GUI.DrawTexture(charge, Texture2D.whiteTexture);
            GUI.color = oldColor;
            label.fontSize = 14;
            label.normal.textColor = new Color(.18f, .23f, .26f);
            bool fullWidth = top.localScale.x >= 3 && top.localScale.z >= 3;
            GUI.Label(charge, fullWidth ? "Room to spare" : energy >= 3 ? "Restore width [E] / 3 lights" : "Gathering light " + energy + "/3", label);
            label.fontSize = 14;
            label.normal.textColor = new Color(.96f, .94f, .88f);
            GUI.Label(new Rect(0, height - 80, width, 30), "Tap / Space to place", label);
        }
        label.fontSize = 10;
        label.normal.textColor = new Color(.94f, .92f, .86f);
        GUI.Label(new Rect(12, height - 29, width - 24, 22), "SECONDWINDGAMES / MANGPENG", label);
        GUI.matrix = previousMatrix;
    }

    private static void Panel(Rect rect)
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(.12f, .18f, .22f, .9f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    protected virtual void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
    protected virtual void OnDestroy()
    {
        PlayerPrefs.Save();
        if (material != null) Destroy(material);
    }
}
