using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>A self-contained, one-button stacking game. Attach to an empty scene object.</summary>
public sealed class StackGame : MonoBehaviour
{
    [SerializeField] private Material blockMaterial;
    private bool ready;
    private bool startupFailed;
    private const float Height = 0.35f;
    private const float Travel = 3.7f;
    private Transform tower;
    private Transform top;
    private Transform moving;
    private Camera view;
    private Material material;
    private MaterialPropertyBlock tint;
    private int score;
    private int best;
    private int axis;
    private int points;
    private int energy;
    private readonly int[] rankings = new int[5];
    private int Multiplier => 1 + energy / 2;
    private float phase;
    private float restartAt;
    private float perfectUntil;
    private bool gameOver;
    private SobokAudio sound;
    private SobokSky sky;
    private static readonly Color[] Palette =
    {
        new Color(0.72f, 0.65f, 0.77f), new Color(0.66f, 0.74f, 0.65f),
        new Color(0.94f, 0.72f, 0.59f), new Color(0.95f, 0.84f, 0.58f),
        new Color(0.66f, 0.77f, 0.80f)
    };

    private void Awake()
    {
        try
        {
            InitializeGame();
            ready = true;
            Debug.Log("[SOBOK] Game initialized. Scene=" + gameObject.scene.name +
                ", version=" + Application.version + ", shader=" + material.shader.name);
        }
        catch (System.Exception exception)
        {
            startupFailed = true;
            Debug.LogError("[SOBOK] Game initialization failed on " + Application.platform);
            Debug.LogException(exception, this);
        }
    }

    private void InitializeGame()
    {
        // A serialized material keeps its shader in Android builds; Shader.Find alone does not.
        var template = blockMaterial != null ? blockMaterial : Resources.Load<Material>("SobokBlock");
        if (template == null || template.shader == null)
            throw new System.InvalidOperationException("SobokBlock material or its shader is missing from the build.");
        material = new Material(template);
        material.SetFloat("_Smoothness", 0);
        material.SetFloat("_Metallic", 0);
        sound = gameObject.AddComponent<SobokAudio>();
        tint = new MaterialPropertyBlock();
        view = Camera.main;
        if (view == null)
        {
            var cameraObject = new GameObject("Stack Camera", typeof(Camera), typeof(AudioListener));
            view = cameraObject.GetComponent<Camera>();
        }
        view.orthographic = true;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = new Color(0.96f, 0.93f, 0.86f);
        view.transform.rotation = Quaternion.Euler(32, 45, 0);
        sky = gameObject.AddComponent<SobokSky>();
        sky.Initialize(view);
        best = PlayerPrefs.GetInt("StackGame.Best", 0);
        for (int i = 0; i < rankings.Length; i++)
            rankings[i] = PlayerPrefs.GetInt("StackGame.Rank." + i, 0);
        Restart();
    }

    private void Restart()
    {
        if (tower != null) Destroy(tower.gameObject);
        tower = new GameObject("Tower").transform;
        tower.SetParent(transform, false);
        score = 0;
        points = 0;
        energy = 0;
        sky.SetFloor(0);
        gameOver = false;
        perfectUntil = 0;
        top = Block("Base", Vector3.zero, new Vector3(3, Height, 3), 0);
        Spawn();
        FollowCamera(true);
    }

    private Transform Block(string label, Vector3 position, Vector3 size, int level)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = label;
        block.transform.SetParent(tower, false);
        block.transform.position = position;
        block.transform.localScale = size;
        var renderer = block.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        tint.SetColor("_BaseColor", Palette[level % Palette.Length]);
        renderer.SetPropertyBlock(tint);
        return block.transform;
    }

    private void Spawn()
    {
        axis = score % 2 == 0 ? 0 : 2;
        phase = 0;
        var position = top.position + Vector3.up * Height;
        position[axis] -= Travel;
        moving = Block("Block " + (score + 1), position, top.localScale, score + 1);
    }

    private void Update()
    {
        if (!ready) return;
        bool pressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
        if (sound.IsSoundPointer()) pressed = false;

        if (gameOver)
        {
            if (pressed && Time.unscaledTime >= restartAt) Restart();
        }
        else
        {
            bool chargeKey = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            bool chargeTap = pressed && IsChargePointer();
            if (chargeKey || chargeTap) Overcharge();
            // Process placement at the last displayed position before advancing movement.
            if (pressed && !chargeTap && !chargeKey) Place();
            if (!gameOver)
            {
                phase += Time.deltaTime * Mathf.Min(1.7f + score * 0.025f, 2.5f);
                var position = moving.position;
                position[axis] = top.position[axis] + Mathf.PingPong(phase, Travel * 2) - Travel;
                moving.position = position;
            }
        }
        FollowCamera(false);
        sound.SetNight(sky.NightAmount);
    }

    private void Place()
    {
        float offset = moving.position[axis] - top.position[axis];
        float width = top.localScale[axis];
        float overlap = width - Mathf.Abs(offset);
        if (overlap <= 0)
        {
            Fall(moving);
            moving = null;
            gameOver = true;
            SaveRanking();
            restartAt = Time.unscaledTime + 0.6f;
            return;
        }

        if (Mathf.Abs(offset) <= Mathf.Min(0.09f, width * 0.15f))
        {
            var position = moving.position;
            position[axis] = top.position[axis];
            moving.position = position;
            perfectUntil = Time.unscaledTime + 0.7f;
            energy = Mathf.Min(6, energy + 1);
            sound.Placement(true);
            Illuminate(moving);
        }
        else
        {
            sound.Placement(false);
            var size = moving.localScale;
            var position = moving.position;
            var cutSize = size;
            cutSize[axis] = Mathf.Abs(offset);
            var cutPosition = position;
            cutPosition[axis] += Mathf.Sign(offset) * overlap * 0.5f;
            Fall(Block("Offcut", cutPosition, cutSize, score + 1));
            size[axis] = overlap;
            position[axis] -= offset * 0.5f;
            moving.localScale = size;
            moving.position = position;
        }

        top = moving;
        score++;
        sky.SetFloor(score);
        points += 10 * Multiplier;
        if (points > best)
        {
            best = points;
            PlayerPrefs.SetInt("StackGame.Best", best);
        }
        Spawn();
    }

    private Rect ChargeRect => new Rect(Screen.width * 0.5f - 110 * HudScale,
        Screen.height - 135 * HudScale, 220 * HudScale, 45 * HudScale);
    private float HudScale => Mathf.Min(Screen.width / 480f, Screen.height / 720f);

    private bool IsChargePointer()
    {
        Vector2 position;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            position = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            position = Mouse.current.position.ReadValue();
        else return false;
        position.y = Screen.height - position.y;
        return ChargeRect.Contains(position);
    }

    private void Overcharge()
    {
        if (energy < 3) return;
        if (top.localScale.x >= 3 && top.localScale.z >= 3) return;
        energy -= 3;
        var size = top.localScale;
        size.x = Mathf.Min(3, size.x + 0.6f);
        size.z = Mathf.Min(3, size.z + 0.6f);
        top.localScale = size;
        moving.localScale = size;
        sound.Restore();
        Illuminate(top);
    }

    private void Illuminate(Transform block)
    {
        // A brief cream ribbon acknowledges a careful placement, then fades away.
        var band = Block("Little light", block.position + Vector3.up * (Height * 0.38f),
            new Vector3(1, 0.035f, 1), score + 1);
        band.SetParent(block, true);
        band.localScale = new Vector3(1.015f, 0.1f, 1.015f);
        Destroy(band.GetComponent<Collider>());
        var renderer = band.GetComponent<Renderer>();
        renderer.GetPropertyBlock(tint);
        tint.SetColor("_BaseColor", new Color(1, 0.93f, 0.72f));
        renderer.SetPropertyBlock(tint);
        band.gameObject.AddComponent<SobokFade>();
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
            PlayerPrefs.SetInt("StackGame.Rank." + i, rankings[i]);
        }
        PlayerPrefs.Save();
    }

    private void Fall(Transform block)
    {
        // Debris is visual only; it must never knock the tower over.
        block.GetComponent<Collider>().enabled = false;
        var body = block.gameObject.AddComponent<Rigidbody>();
        body.angularVelocity = new Vector3(1.5f, 0.6f, 1);
        block.gameObject.AddComponent<SobokFade>();
        Destroy(block.gameObject, 3);
    }

    private void FollowCamera(bool snap)
    {
        view.orthographicSize = Mathf.Max(5.5f, 5.2f / Mathf.Max(view.aspect, 0.3f));
        var focus = top.position + Vector3.up * 0.7f;
        var target = focus - view.transform.forward * 18;
        view.transform.position = snap ? target : Vector3.Lerp(view.transform.position, target, 1 - Mathf.Exp(-5 * Time.deltaTime));
    }

    private void OnGUI()
    {
        if (!ready)
        {
            if (startupFailed)
                GUI.Box(new Rect(20, Screen.height * .45f, Screen.width - 40, 70),
                    "SOBOK could not start. Please restart the app.");
            return;
        }
        // Scale a small HUD consistently for desktop and phone resolutions.
        float scale = Mathf.Min(Screen.width / 480f, Screen.height / 720f);
        var previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        var label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
        label.normal.textColor = sky.Ink;
        GUI.Label(new Rect(0, 28, width, 30), "S O B O K", label);
        label.fontSize = 64;
        GUI.Label(new Rect(0, 60, width, 85), score.ToString(), label);
        label.fontSize = 16;
        GUI.Label(new Rect(0, 145, width, 30), sky.TimeLabel + "  /  one quiet moment at a time", label);
        label.fontSize = 22;
        if (gameOver)
        {
            label.fontSize = 17;
            GUI.Label(new Rect(0, 195, width, 30), "Little memories  /  " + points + " points", label);
            for (int i = 0; i < rankings.Length; i++)
                GUI.Label(new Rect(0, 228 + i * 28, width, 28), (i + 1) + ".   " + rankings[i], label);
            label.normal.textColor = new Color(.96f,.94f,.88f);
            GUI.Label(new Rect(0, height - 160, width, 40), "A little pause.", label);
            label.fontSize = 17;
            GUI.Label(new Rect(0, height - 112, width, 35), "Tap when you are ready to begin again", label);
        }
        else
        {
            if (Time.unscaledTime < perfectUntil)
                GUI.Label(new Rect(0, 180, width, 35), "Just right.", label);
            var charge = ChargeRect;
            charge = new Rect(charge.x / scale, charge.y / scale, charge.width / scale, charge.height / scale);
            var oldColor = GUI.color;
            GUI.color = Color.Lerp(new Color(.84f,.86f,.78f), new Color(.23f,.29f,.38f), sky.NightAmount);
            GUI.DrawTexture(charge, Texture2D.whiteTexture);
            GUI.color = oldColor;
            label.fontSize = 16;
            bool fullWidth = top.localScale.x >= 3 && top.localScale.z >= 3;
            GUI.Label(charge, fullWidth ? "Room to spare" : energy >= 3 ? "Restore width [E]  /  3 lights" : "Gathering light  " + energy + "/3", label);
            label.fontSize = 14;
            label.normal.textColor = new Color(.96f,.94f,.88f);
            GUI.Label(new Rect(0, height - 172, width, 28), "Lights " + energy + "/6   ·   Score x" + Multiplier, label);
            label.fontSize = 17;
            label.normal.textColor = new Color(.96f,.94f,.88f);
            GUI.Label(new Rect(0, height - 75, width, 35), "Tap / Space to place  ·  M for sound", label);
        }
        label.fontSize = 11;
        label.normal.textColor = new Color(.92f,.91f,.86f);
        GUI.Label(new Rect(12, height - 30, width - 24, 22),
            "© 2026 SECONDWINDGAMES · CREATED BY MANGPENG", label);
        GUI.matrix = previousMatrix;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        PlayerPrefs.Save();
        if (material != null) Destroy(material);
    }
}
