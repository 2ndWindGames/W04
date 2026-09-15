using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Five timing placements become a volley against three progressively tougher towers.</summary>
public sealed class SobokBattleGame : MonoBehaviour
{
    public enum BattleState { Building, Ready, Attacking, Counterattack, Result, Victory, GameOver }
    public const int BlocksPerTurn = 5;
    private const float BlockHeight = .43f;
    private const float BaseWidth = 1.7f;
    private const float PlayerX = -2.15f;
    private const float EnemyX = 2.15f;
    private static readonly int[] EnemyHitPoints = { 8, 11, 14 };
    private static readonly int[] CounterDamage = { 1, 2, 2 };
    private BattleState state;
    private bool ready;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool previewFrozen;
#endif
    private Camera view;
    private Material material;
    private MaterialPropertyBlock tint;
    private SobokAudio sound;
    private SobokSky sky;
    private Transform arena;
    private Transform playerTower;
    private Transform enemyTower;
    private Transform effects;
    private Transform moving;
    private Transform enemyShot;
    private readonly List<Transform> stack = new List<Transform>();
    private readonly List<int> charges = new List<int>();
    private readonly List<Transform> enemyBricks = new List<Transform>();
    private readonly List<Shot> shots = new List<Shot>();
    private int playerHealth;
    private int enemyHealth;
    private int opponent;
    private int attempts;
    private int turn;
    private int points;
    private int lastDamage;
    private int lastPower;
    private float phase;
    private float attackStartedAt;
    private float counterStartedAt;
    private float nextInputAt;
    private float impactAt = -10;
    private float feedbackUntil;
    private string feedback = "";
    private bool counterResolved;
    private Vector3 cameraPosition;

    public BattleState State => state;
    public int PlayerHealth => playerHealth;
    public int EnemyHealth => enemyHealth;
    public int Opponent => opponent + 1;
    public int Attempts => attempts;
    public int VolleyPower { get { int total = 0; foreach (int damage in charges) total += damage; return total; } }
    public int Score => points;

    private sealed class Shot
    {
        public Transform body;
        public Vector3 origin;
        public Vector3 target;
        public int damage;
        public float delay;
        public bool impacted;
    }

    private void Awake()
    {
        tint = new MaterialPropertyBlock();
        var source = Resources.Load<Material>("SobokBlock");
        if (source == null) { Debug.LogError("Tower Battle needs Resources/SobokBlock."); enabled = false; return; }
        material = new Material(source);
        view = Camera.main;
        if (view == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            view = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }
        view.orthographic = true;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = new Color(.82f, .83f, .86f);
        view.transform.rotation = Quaternion.Euler(12, 0, 0);
        sound = gameObject.AddComponent<SobokAudio>();
        sky = gameObject.AddComponent<SobokSky>();
        sky.Initialize(view);
        RestartRun();
        ready = true;
    }

    private Transform Root(string title, Transform parent = null)
    {
        var item = new GameObject(title).transform;
        item.SetParent(parent != null ? parent : transform, false);
        return item;
    }

    private Transform Box(string title, Transform parent, Vector3 position, Vector3 size, Color color)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = title;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = size;
        item.GetComponent<Collider>().enabled = false;
        var renderer = item.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        tint.Clear();
        tint.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(tint);
        return item.transform;
    }

    public void RestartRun()
    {
        if (arena != null) Destroy(arena.gameObject);
        arena = Root("Tower Battle Arena");
        effects = Root("Battle Fragments", arena);
        playerTower = Root("Your Ammunition Tower", arena);
        playerTower.localPosition = new Vector3(PlayerX, .3f, 0);
        enemyTower = Root("Enemy Tower", arena);
        enemyTower.localPosition = new Vector3(EnemyX, .3f, 0);
        Box("Your Platform", arena, new Vector3(PlayerX, -.12f, 0), new Vector3(2.6f, .55f, 2.5f), new Color(.34f, .52f, .55f));
        Box("Enemy Platform", arena, new Vector3(EnemyX, -.12f, 0), new Vector3(2.6f, .55f, 2.5f), new Color(.53f, .37f, .46f));
        Box("Battle Field", arena, new Vector3(0, -.48f, 0), new Vector3(11, .15f, 4), new Color(.55f, .59f, .65f));
        // Direction markers make the volley's route readable before the first attack.
        for (int i = 0; i < 4; i++)
            Box("Volley Route", arena, new Vector3(-.7f + i * .45f, -.385f, .4f), new Vector3(.2f, .02f, .08f), new Color(1, .82f, .46f));
        stack.Clear(); charges.Clear(); enemyBricks.Clear(); shots.Clear();
        moving = enemyShot = null;
        playerHealth = 5;
        opponent = 0;
        points = 0;
        turn = 0;
        lastDamage = lastPower = 0;
        impactAt = -10;
        feedback = "";
        CreateEnemy();
        StartTurn();
        UpdateCamera(true);
    }

    private void CreateEnemy()
    {
        foreach (var brick in enemyBricks) if (brick != null) Destroy(brick.gameObject);
        enemyBricks.Clear();
        enemyHealth = EnemyHitPoints[opponent];
        for (int i = 0; i < enemyHealth; i++)
        {
            Color color = i % 2 == 0 ? new Color(.76f, .38f, .39f) : new Color(.91f, .56f, .46f);
            float width = 1.75f - (i / 4) * .1f;
            enemyBricks.Add(Box("Enemy HP " + (i + 1), enemyTower, new Vector3(0, i * .32f, 0), new Vector3(width, .3f, 1.5f), color));
        }
        sky.SetFloor(opponent * 9);
    }

    private void StartTurn()
    {
        foreach (var block in stack) if (block != null) Destroy(block.gameObject);
        foreach (var shot in shots) if (shot.body != null) Destroy(shot.body.gameObject);
        stack.Clear(); charges.Clear(); shots.Clear();
        if (enemyShot != null) Destroy(enemyShot.gameObject);
        enemyShot = null;
        stack.Add(Box("Tower Base", playerTower, Vector3.zero, new Vector3(BaseWidth, BlockHeight, 1.55f), new Color(.32f, .51f, .56f)));
        attempts = 0;
        turn++;
        state = BattleState.Building;
        nextInputAt = Time.unscaledTime + .16f;
        Spawn();
    }

    private void Spawn()
    {
        Transform top = stack[stack.Count - 1];
        phase = 0;
        moving = Box("Shot " + (attempts + 1), playerTower,
            new Vector3(top.localPosition.x - 1.65f, stack.Count * BlockHeight, 0),
            new Vector3(top.localScale.x, BlockHeight * .94f, 1.55f), new Color(.56f, .79f, .77f));
    }

    private void Update()
    {
        if (!ready || SobokArcadeHub.BlocksGameplayInput()) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (previewFrozen) return;
#endif
        bool tapped = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) tapped = true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) tapped = true;
        if (sound.IsSoundPointer() || Time.unscaledTime < nextInputAt) tapped = false;
        if (state == BattleState.Building)
        {
            if (tapped) Place();
            if (moving != null)
            {
                phase += Time.deltaTime * (1.65f + opponent * .28f + attempts * .09f);
                var position = moving.localPosition;
                position.x = stack[stack.Count - 1].localPosition.x + Mathf.PingPong(phase, 3.3f) - 1.65f;
                moving.localPosition = position;
            }
        }
        else if (state == BattleState.Ready && tapped) Fire();
        else if (state == BattleState.Attacking || state == BattleState.Counterattack) TickBattle(Time.unscaledTime);
        else if (state == BattleState.Result && tapped) ContinueRun();
        else if ((state == BattleState.GameOver || state == BattleState.Victory) && tapped) RestartRun();
        UpdateCamera(false);
        sound.SetNight(sky.NightAmount);
    }

    public void Place()
    {
        if (state != BattleState.Building || moving == null) return;
        Transform top = stack[stack.Count - 1];
        float width = top.localScale.x;
        float offset = moving.localPosition.x - top.localPosition.x;
        bool perfect = Mathf.Abs(offset) <= .065f;
        float overlap = width - Mathf.Abs(offset);
        attempts++;
        if (overlap < .12f)
        {
            Fragment(moving.position, moving.localScale, new Color(.61f, .69f, .70f), -1);
            Destroy(moving.gameObject);
            feedback = "MISSED — SHOT LOST";
            sound.Breach(.3f);
        }
        else
        {
            if (perfect) overlap = width;
            float center = perfect ? top.localPosition.x : (moving.localPosition.x + top.localPosition.x) * .5f;
            if (!perfect)
            {
                float cutWidth = width - overlap;
                float cutX = moving.localPosition.x + Mathf.Sign(offset) * overlap * .5f;
                Fragment(playerTower.position + new Vector3(cutX, moving.localPosition.y, 0), new Vector3(cutWidth, BlockHeight, 1.55f), new Color(.64f, .77f, .74f), Mathf.Sign(offset));
            }
            moving.localPosition = new Vector3(center, moving.localPosition.y, 0);
            moving.localScale = new Vector3(overlap, BlockHeight * .94f, 1.55f);
            int power = perfect ? 3 : Mathf.Clamp(Mathf.CeilToInt(overlap / BaseWidth * 2), 1, 2);
            charges.Add(power);
            stack.Add(moving);
            tint.Clear();
            tint.SetColor("_BaseColor", perfect ? new Color(1, .82f, .42f) : new Color(.52f, .76f, .74f));
            moving.GetComponent<Renderer>().SetPropertyBlock(tint);
            feedback = perfect ? "PERFECT  +3 POWER" : "SHOT READY  +" + power + " POWER";
            sound.Placement(perfect);
        }
        feedbackUntil = Time.unscaledTime + 1.2f;
        moving = null;
        if (attempts >= BlocksPerTurn) state = BattleState.Ready;
        else Spawn();
    }

    public void Fire()
    {
        if (state != BattleState.Ready) return;
        state = BattleState.Attacking;
        attackStartedAt = Time.unscaledTime;
        lastPower = VolleyPower;
        lastDamage = 0;
        shots.Clear();
        for (int i = stack.Count - 1; i >= 1; i--)
        {
            Transform block = stack[i];
            block.SetParent(effects, true);
            shots.Add(new Shot {
                body = block, origin = block.position,
                target = enemyTower.position + new Vector3(0, Mathf.Max(0, enemyHealth - 1) * .32f, 0),
                damage = charges[i - 1], delay = (stack.Count - 1 - i) * .18f
            });
        }
        // Only the foundation remains after the ammunition has been fired.
        stack.RemoveRange(1, stack.Count - 1);
        sound.Launch();
    }

    public void TickBattle(float now)
    {
        if (state == BattleState.Attacking)
        {
            float elapsed = now - attackStartedAt;
            foreach (Shot shot in shots)
            {
                if (shot.impacted || elapsed < shot.delay) continue;
                shot.target = enemyTower.position + new Vector3(0, Mathf.Max(0, enemyHealth - 1) * .32f, 0);
                float t = Mathf.Clamp01((elapsed - shot.delay) / .52f);
                if (shot.body != null)
                {
                    shot.body.position = Vector3.Lerp(shot.origin, shot.target, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .65f;
                    shot.body.rotation = Quaternion.Euler(0, 0, t * -50);
                }
                if (t >= 1)
                {
                    shot.impacted = true;
                    HitEnemy(shot.damage);
                    if (shot.body != null)
                    {
                        Fragment(shot.body.position, shot.body.localScale * .55f, new Color(1, .83f, .46f), 1);
                        Destroy(shot.body.gameObject);
                    }
                }
            }
            float finish = Mathf.Max(0, shots.Count - 1) * .18f + 1f;
            if (elapsed >= finish)
            {
                if (enemyHealth <= 0)
                {
                    points += 100;
                    state = opponent == EnemyHitPoints.Length - 1 ? BattleState.Victory : BattleState.Result;
                    nextInputAt = now + .3f;
                    sound.Restore();
                }
                else BeginCounterattack(now);
            }
        }
        else if (state == BattleState.Counterattack)
        {
            float age = now - counterStartedAt;
            float t = Mathf.Clamp01(age / .65f);
            if (enemyShot != null)
            {
                var origin = enemyTower.position + Vector3.up * Mathf.Max(.5f, enemyHealth * .2f);
                var target = playerTower.position + Vector3.up * .2f;
                enemyShot.position = Vector3.Lerp(origin, target, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .9f;
                enemyShot.Rotate(new Vector3(160, 190, 210) * Time.deltaTime);
            }
            if (age >= .65f && !counterResolved)
            {
                counterResolved = true;
                playerHealth = Mathf.Max(0, playerHealth - CounterDamage[opponent]);
                if (enemyShot != null)
                {
                    Fragment(enemyShot.position, Vector3.one * .5f, new Color(.92f, .4f, .38f), -1);
                    Destroy(enemyShot.gameObject);
                }
                impactAt = Time.unscaledTime;
                sound.Breach(0);
            }
            if (age >= 1.05f)
            {
                state = playerHealth <= 0 ? BattleState.GameOver : BattleState.Result;
                nextInputAt = now + .25f;
            }
        }
    }

    private void HitEnemy(int damage)
    {
        int actual = Mathf.Min(enemyHealth, damage);
        enemyHealth -= actual;
        lastDamage += actual;
        points += actual * 10;
        for (int i = 0; i < actual; i++)
        {
            int index = enemyBricks.Count - 1;
            Transform brick = enemyBricks[index];
            enemyBricks.RemoveAt(index);
            Fragment(brick.position, brick.localScale * .75f, new Color(.85f, .43f, .40f), 1);
            Destroy(brick.gameObject);
        }
        if (actual > 0)
        {
            impactAt = Time.unscaledTime;
            feedback = "HIT  -" + actual + " ENEMY HP";
            feedbackUntil = Time.unscaledTime + .8f;
            sound.Breach(.4f);
        }
    }

    private void BeginCounterattack(float now)
    {
        state = BattleState.Counterattack;
        counterStartedAt = now;
        counterResolved = false;
        enemyShot = Box("Enemy Counter Shot", effects, effects.InverseTransformPoint(enemyTower.position + Vector3.up),
            Vector3.one * .6f, new Color(.96f, .38f, .34f));
        sound.Launch();
    }

    public void ContinueRun()
    {
        if (state != BattleState.Result) return;
        if (enemyHealth <= 0)
        {
            opponent++;
            CreateEnemy();
        }
        StartTurn();
    }

    private void Fragment(Vector3 position, Vector3 size, Color color, float direction)
    {
        var piece = Box("Battle Debris", effects, effects.InverseTransformPoint(position), size, color);
        var body = piece.gameObject.AddComponent<Rigidbody>();
        body.linearVelocity = new Vector3(direction * 2.3f, 2.2f, 1.5f);
        body.angularVelocity = new Vector3(2, 3, direction * 4);
        Destroy(piece.gameObject, 2.5f);
    }

    private void UpdateCamera(bool snap)
    {
        view.orthographicSize = Mathf.Max(5.6f, 4.8f / Mathf.Max(.35f, view.aspect));
        // Keep the tallest enemy below the health/power HUD in a portrait window.
        cameraPosition = new Vector3(0, 3.55f, 0) - view.transform.forward * 22;
        float shake = Mathf.Clamp01(1 - (Time.unscaledTime - impactAt) / .2f) * .10f;
        view.transform.position = cameraPosition + (snap ? Vector3.zero : new Vector3(Mathf.Sin(Time.unscaledTime * 85), Mathf.Cos(Time.unscaledTime * 73), 0) * shake);
    }

    private void OnGUI()
    {
        if (!ready) return;
        float scale = Mathf.Max(.1f, Mathf.Min(Screen.width / 480f, Screen.height / 720f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        var center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        center.normal.textColor = sky.Ink;
        GUI.Label(new Rect(12, 60, width - 24, 26), "T O W E R   B A T T L E", center);
        center.fontSize = 30;
        center.fontStyle = FontStyle.Bold;
        string headline = state == BattleState.Victory ? "ALL 3 TOWERS DEFEATED" : state == BattleState.GameOver ? "YOUR TOWER FELL" : "ENEMY " + (opponent + 1) + " / 3";
        GUI.Label(new Rect(8, 91, width - 16, 44), headline, center);
        center.fontStyle = FontStyle.Normal;
        center.fontSize = 13;
        GUI.Label(new Rect(8, 136, width - 16, 26), "TURN " + turn + "    •    " + points + " PTS    •    COUNTER HIT " + CounterDamage[opponent], center);
        DrawHealth(new Rect(24, 174, width * .5f - 36, 42), "YOUR HP", playerHealth, 5, new Color(.26f, .57f, .57f));
        DrawHealth(new Rect(width * .5f + 12, 174, width * .5f - 36, 42), "ENEMY HP", enemyHealth, EnemyHitPoints[opponent], new Color(.75f, .29f, .31f));
        center.fontSize = 15;
        string status = state == BattleState.Building || state == BattleState.Ready
            ? "SHOTS " + attempts + "/5    •    VOLLEY " + VolleyPower + " DAMAGE"
            : state == BattleState.Counterattack ? "ENEMY COUNTERATTACK!" : "VOLLEY " + lastPower + "    •    DEALT " + lastDamage + " DAMAGE";
        GUI.Label(new Rect(10, 225, width - 20, 30), status, center);
        if (Time.unscaledTime < feedbackUntil && state != BattleState.Result && state != BattleState.Victory && state != BattleState.GameOver)
        {
            center.fontSize = 13;
            GUI.Label(new Rect(10, 257, width - 20, 26), feedback, center);
        }
        Color old = GUI.color;
        GUI.color = new Color(.10f, .15f, .22f, .95f);
        GUI.DrawTexture(new Rect(18, height - 133, width - 36, 102), Texture2D.whiteTexture);
        GUI.color = old;
        center.fontStyle = FontStyle.Bold;
        center.fontSize = 22;
        center.normal.textColor = new Color(1, .84f, .49f);
        string action = state == BattleState.Building ? "TAP TO STACK AMMO" : state == BattleState.Ready ? "TAP TO FIRE THE TOWER" :
            state == BattleState.Attacking ? "VOLLEY IN FLIGHT" : state == BattleState.Counterattack ? "INCOMING!" :
            state == BattleState.Result ? (enemyHealth <= 0 ? "TAP FOR THE NEXT ENEMY" : "TAP TO REBUILD") : "TAP TO PLAY AGAIN";
        GUI.Label(new Rect(22, height - 126, width - 44, 36), action, center);
        center.fontStyle = FontStyle.Normal;
        center.fontSize = 13;
        center.normal.textColor = new Color(.89f, .91f, .95f);
        string hint = state == BattleState.Building ? "Perfect = 3 damage. Wide = 2. Narrow = 1.\nA miss wastes one of your five shots." :
            state == BattleState.Ready ? "Your " + charges.Count + " blocks become " + VolleyPower + " damage.\nAn enemy that survives will fire back." :
            state == BattleState.Attacking ? "Each stacked block becomes a projectile." :
            state == BattleState.Counterattack ? "Enemy still has " + enemyHealth + " HP. Your next volley matters." :
            state == BattleState.Victory ? "Three opponents cleared with " + playerHealth + " HP remaining!" :
            state == BattleState.GameOver ? "Line up wider blocks and earn perfect shots." :
            enemyHealth <= 0 ? "Tower defeated! Your remaining HP carries forward." : "Enemy damage persists. Finish it with the next tower.";
        GUI.Label(new Rect(26, height - 86, width - 52, 44), hint, center);
        center.fontSize = 10;
        center.normal.textColor = sky.Ink;
        GUI.Label(new Rect(10, height - 25, width - 20, 18), "CLICK / SPACE TO PLAY    •    ESC TO CHOOSE A GAME", center);
        GUI.matrix = previous;
    }

    private void DrawHealth(Rect rect, string label, int health, int maximum, Color color)
    {
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12, fontStyle = FontStyle.Bold };
        style.normal.textColor = sky.Ink;
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 20), label + "  " + health + " / " + maximum, style);
        Color old = GUI.color;
        GUI.color = new Color(.15f, .18f, .23f, .2f);
        GUI.DrawTexture(new Rect(rect.x, rect.y + 25, rect.width, 9), Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y + 25, rect.width * Mathf.Clamp01((float)health / maximum), 9), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
