using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One entry point for five independently playable SOBOK experiments.</summary>
public sealed class SobokArcadeHub : MonoBehaviour
{
    private static SobokArcadeHub instance;
    private GameObject content;
    private MonoBehaviour activeMode;
    private SobokSky menuSky;
    private bool switching;
    private int blockedThroughFrame;
    private Texture2D circle;
    public int SelectedMode { get; private set; } = -1;
    public MonoBehaviour ActiveMode => activeMode;
    public bool IsMenuOpen => SelectedMode < 0 && !switching;
    public bool IsSwitching => switching;
    private float Scale => Mathf.Max(.1f, Mathf.Min(Screen.width / 480f, Screen.height / 720f));
    private Rect MenuRect => new Rect(12 * Scale, 12 * Scale, 78 * Scale, 34 * Scale);
    private Rect RetryRect => new Rect(98 * Scale, 12 * Scale, 78 * Scale, 34 * Scale);
    private static readonly string[] Titles = { "ORIGINAL STACK", "FLOWER GARDEN", "WALL BREACH", "TOWER BRIDGE", "TOWER BATTLE" };
    private static readonly string[] Descriptions = {
        "Find the rhythm. Stack higher. Chase your best.",
        "Grow a blooming garden, one careful layer at a time.",
        "Build five blocks. Fire your tower through the wall.",
        "Stack, tip your tower, and walk across the gap.",
        "Build your attack. Knock down the rival tower."
    };
    private static readonly string[] Goals = { "ENDLESS", "15 FLOORS", "ENDLESS", "5 CROSSINGS", "3 RIVALS" };
    private static readonly Color[] Accents = {
        new Color(.56f,.48f,.65f), new Color(.38f,.57f,.41f), new Color(.23f,.43f,.50f),
        new Color(.74f,.53f,.28f), new Color(.66f,.36f,.38f)
    };

    private void Awake()
    {
        instance = this;
        circle = Resources.Load<Texture2D>("UI/circle");
        // The earlier breach-only check/build entry point remains usable.
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sobok-breach-check") >= 0) CreateMode(2);
        else CreateMenu();
    }

    private void CreateMenu()
    {
        SelectedMode = -1;
        activeMode = null;
        content = new GameObject("SOBOK selection");
        content.transform.SetParent(transform, false);
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
        }
        camera.orthographic = true;
        camera.orthographicSize = 6;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.transform.SetPositionAndRotation(new Vector3(0, 4, -20), Quaternion.identity);
        menuSky = content.AddComponent<SobokSky>();
        menuSky.Initialize(camera);
        menuSky.SetFloor(0);
        content.AddComponent<SobokAudio>();
        Debug.Log("[SOBOK] Five-game selection ready.");
    }

    private void CreateMode(int mode)
    {
        SelectedMode = mode;
        menuSky = null;
        content = new GameObject(Titles[mode]);
        content.transform.SetParent(transform, false);
        switch (mode)
        {
            case 0: activeMode = content.AddComponent<SobokClassicGame>(); break;
            case 1: activeMode = content.AddComponent<SobokGardenGame>(); break;
            case 2: activeMode = content.AddComponent<StackGame>(); break;
            case 3: activeMode = content.AddComponent<SobokBridgeGame>(); break;
            case 4: activeMode = content.AddComponent<SobokBattleGame>(); break;
        }
        blockedThroughFrame = Time.frameCount + 1;
        Debug.Log("[SOBOK] Selected " + Titles[mode]);
    }

    public void SelectMode(int mode)
    {
        if (switching || mode < 0 || mode >= Titles.Length) return;
        StartCoroutine(SwitchContent(mode));
    }

    public void ShowMenu()
    {
        if (switching || IsMenuOpen) return;
        StartCoroutine(SwitchContent(-1));
    }

    private IEnumerator SwitchContent(int mode)
    {
        switching = true;
        activeMode = null;
        if (content != null)
        {
            content.SetActive(false);
            Destroy(content);
        }
        // Give each mode a frame to dispose of its sky, audio, UI and runtime materials.
        yield return null;
        if (mode < 0) CreateMenu();
        else CreateMode(mode);
        blockedThroughFrame = Time.frameCount + 1;
        switching = false;
    }

    public void RestartCurrent()
    {
        if (activeMode == null || switching) return;
        SobokShare share = content.GetComponent<SobokShare>();
        if (share != null && share.Busy) return;
        blockedThroughFrame = Time.frameCount + 1;
        activeMode.SendMessage("RestartRun", SendMessageOptions.RequireReceiver);
    }

    public static bool BlocksGameplayInput()
    {
        if (instance == null) return false;
        if (instance.switching || instance.IsMenuOpen || Time.frameCount <= instance.blockedThroughFrame) return true;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame)) return true;
        Vector2 point;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            point = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            point = Mouse.current.position.ReadValue();
        else return false;
        point.y = Screen.height - point.y;
        return instance.MenuRect.Contains(point) || instance.RetryRect.Contains(point);
    }

    private void Update()
    {
        if (switching || Keyboard.current == null) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard.escapeKey.wasPressedThisFrame) ShowMenu();
        else if (!IsMenuOpen && keyboard.rKey.wasPressedThisFrame) RestartCurrent();
        else if (IsMenuOpen)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) SelectMode(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) SelectMode(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) SelectMode(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) SelectMode(3);
            else if (keyboard.digit5Key.wasPressedThisFrame) SelectMode(4);
        }
    }

    private void OnGUI()
    {
        if (activeMode != null)
        {
            SobokShare share = content.GetComponent<SobokShare>();
            if (share != null && share.Capturing) return;
        }
        Matrix4x4 previous = GUI.matrix;
        float scale = Scale;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float width = Screen.width / scale, height = Screen.height / scale;
        GUI.depth = -100;
        var text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
        text.normal.textColor = new Color(.16f,.23f,.28f);
        if (switching)
        {
            GUI.Label(new Rect(0, height * .45f, width, 50), "One moment...", text);
        }
        else if (!IsMenuOpen)
        {
            if (Button(new Rect(12, 12, 78, 34), "MENU", new Color(.95f,.94f,.87f), text)) ShowMenu();
            if (Button(new Rect(98, 12, 78, 34), "RETRY", new Color(.95f,.94f,.87f), text)) RestartCurrent();
        }
        else
        {
            float columnWidth = Mathf.Min(width - 36, 580);
            float left = (width - columnWidth) * .5f;
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(0, 24, width, 42), "S O B O K", text);
            text.fontStyle = FontStyle.Normal;
            text.fontSize = 15;
            GUI.Label(new Rect(0, 72, width, 28), "FIVE WAYS TO STACK", text);
            text.fontSize = 12;
            GUI.Label(new Rect(0, 101, width, 25), "Choose a game. Find your favorite.", text);
            float first = 145;
            float cardHeight = Mathf.Min(88, (height - 217) / 5);
            for (int i = 0; i < Titles.Length; i++)
            {
                var rect = new Rect(left, first + i * (cardHeight + 10), columnWidth, cardHeight);
                Color fill = Color.Lerp(new Color(.98f,.97f,.92f), Accents[i], .08f);
                Fill(rect, fill);
                Fill(new Rect(rect.x, rect.y, 5, rect.height), Accents[i]);
                Mark(i, new Rect(rect.x + 17, rect.y + 19, 44, 49), Accents[i]);
                text.alignment = TextAnchor.UpperLeft;
                text.fontSize = 19;
                text.fontStyle = FontStyle.Bold;
                text.normal.textColor = Accents[i] * .75f + new Color(0,0,0,.25f);
                GUI.Label(new Rect(rect.x + 76, rect.y + 12, rect.width - 140, 27), Titles[i], text);
                text.fontStyle = FontStyle.Normal;
                text.fontSize = 12;
                text.normal.textColor = new Color(.28f,.32f,.33f);
                text.wordWrap = true;
                GUI.Label(new Rect(rect.x + 76, rect.y + 40, rect.width - 104, 38), Descriptions[i], text);
                text.alignment = TextAnchor.UpperRight;
                text.fontSize = 10;
                GUI.Label(new Rect(rect.xMax - 92, rect.y + 17, 77, 20), Goals[i], text);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) SelectMode(i);
            }
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 11;
            text.normal.textColor = new Color(.28f,.34f,.35f);
            GUI.Label(new Rect(0, height - 35, width, 24), "1 - 5  SELECT     /     ESC  MENU     /     R  RETRY", text);
        }
        GUI.matrix = previous;
        GUI.depth = 0;
    }

    private static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static bool Button(Rect rect, string title, Color color, GUIStyle text)
    {
        Fill(rect, color);
        GUI.Label(rect, title, text);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    private void Mark(int mode, Rect box, Color color)
    {
        if (mode == 3)
        {
            Fill(new Rect(box.x, box.y + 31, 10, 18), color);
            Fill(new Rect(box.xMax - 10, box.y + 31, 10, 18), color);
            for (int i = 0; i < 5; i++) Fill(new Rect(box.x + 7 + i * 7, box.y + 26, 6, 8), color);
            return;
        }
        if (mode == 2)
        {
            Fill(new Rect(box.x, box.y + 5, 9, 40), color);
            Fill(new Rect(box.xMax - 9, box.y + 5, 9, 40), color);
            Fill(new Rect(box.x, box.y, box.width, 8), color);
        }
        int bars = mode == 1 ? 3 : 5;
        for (int i = 0; i < bars; i++)
        {
            float inset = i * 2;
            Fill(new Rect(box.x + inset + (mode == 2 ? 9 : 0), box.yMax - 7 - i * 9,
                box.width - inset * 2 - (mode == 2 ? 18 : 0), 6), color);
        }
        if (mode == 1 && circle != null)
        {
            Color previous = GUI.color;
            GUI.color = new Color(.87f,.60f,.47f);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2 / 5;
                GUI.DrawTexture(new Rect(box.center.x - 6 + Mathf.Cos(angle) * 8, box.y + 6 + Mathf.Sin(angle) * 8, 12, 12), circle);
            }
            GUI.color = new Color(1,.87f,.51f);
            GUI.DrawTexture(new Rect(box.center.x - 5, box.y + 7, 10, 10), circle);
            GUI.color = previous;
        }
        if (mode == 4)
        {
            Fill(new Rect(box.xMax - 1, box.y + 1, 5, 18), new Color(.96f,.79f,.44f));
            Fill(new Rect(box.xMax - 8, box.y + 7, 19, 5), new Color(.96f,.79f,.44f));
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
