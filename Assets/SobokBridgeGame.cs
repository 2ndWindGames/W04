using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Stack a real tower, tip it into a bridge, then walk across five gaps.</summary>
public sealed class SobokBridgeGame : MonoBehaviour
{
    private const float LayerHeight = .55f;
    private const int Goal = 5;
    private enum RunState { Building, Tipping, Crossing, Falling, RoundComplete, Won, GameOver }
    private RunState state;
    private bool ready;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool previewFrozen;
#endif
    private Camera view;
    private Material material;
    private MaterialPropertyBlock tint;
    private SobokAudio sound;
    private SobokSky sky;
    private Transform world;
    private Transform tower;
    private Transform moving;
    private Transform pawn;
    private readonly List<Transform> layers = new List<Transform>();
    private float phase;
    private float gap;
    private float motionStartedAt;
    private float inputReadyAt;
    private float fallEdge;
    private float fallLane;
    private int crossings;
    private int points;
    private bool accurateLanding;
    private string result;
    private float BridgeLength => layers.Count * LayerHeight;
    private float HudScale => Mathf.Max(.1f, Mathf.Min(Screen.width / 480f, Screen.height / 720f));
    private Rect TipRect => new Rect(Screen.width / HudScale * .5f - 104, Screen.height / HudScale - 113, 208, 48);
    private static readonly Color[] Palette = {
        new Color(.72f,.65f,.77f), new Color(.66f,.74f,.65f), new Color(.94f,.72f,.59f),
        new Color(.95f,.84f,.58f), new Color(.66f,.77f,.80f)
    };

    private void Awake()
    {
        tint = new MaterialPropertyBlock();
        var source = Resources.Load<Material>("SobokBlock");
        if (source == null) { Debug.LogError("Bridge mode requires Resources/SobokBlock."); enabled = false; return; }
        material = new Material(source);
        material.SetFloat("_Smoothness", 0);
        material.SetFloat("_Metallic", 0);
        view = Camera.main;
        if (view == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            view = cameraObject.AddComponent<Camera>();
        }
        view.orthographic = true;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.transform.rotation = Quaternion.Euler(20, -25, 0);
        var audioObject = new GameObject("Bridge sound");
        audioObject.transform.SetParent(transform, false);
        sound = audioObject.AddComponent<SobokAudio>();
        var skyObject = new GameObject("Bridge sky");
        skyObject.transform.SetParent(transform, false);
        sky = skyObject.AddComponent<SobokSky>();
        sky.Initialize(view);
        ready = true;
        RestartRun();
    }

    public void RestartRun()
    {
        if (!ready) return;
        crossings = points = 0;
        BuildRound();
    }

    private Transform Root(string label, Transform parent)
    {
        var item = new GameObject(label).transform;
        item.SetParent(parent, false);
        return item;
    }

    private Transform Box(string label, Transform parent, Vector3 position, Vector3 size, Color color, PrimitiveType shape = PrimitiveType.Cube)
    {
        var item = GameObject.CreatePrimitive(shape);
        item.name = label;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = size;
        var collider = item.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        item.GetComponent<Renderer>().sharedMaterial = material;
        tint.Clear();
        tint.SetColor("_BaseColor", color);
        item.GetComponent<Renderer>().SetPropertyBlock(tint);
        return item.transform;
    }

    private void BuildRound()
    {
        if (world != null) { world.gameObject.SetActive(false); Destroy(world.gameObject); }
        world = Root("Bridge crossing " + (crossings + 1), transform);
        tower = Root("Tower pivot", world);
        layers.Clear();
        moving = null;
        gap = 2.65f + crossings * .65f;
        phase = 0;
        accurateLanding = false;
        result = "";
        state = RunState.Building;
        inputReadyAt = Time.time + .2f;
        Box("Departure island", world, new Vector3(-1.25f,-.7f,0), new Vector3(2.5f,1.4f,3), new Color(.47f,.60f,.57f));
        Box("Departure grass", world, new Vector3(-1.25f,.025f,0), new Vector3(2.5f,.05f,3), new Color(.74f,.81f,.65f));
        Box("Destination island", world, new Vector3(gap+.95f,-.7f,0), new Vector3(1.9f,1.4f,3), new Color(.47f,.60f,.57f));
        Box("Destination grass", world, new Vector3(gap+.95f,.025f,0), new Vector3(1.9f,.05f,3), new Color(.74f,.81f,.65f));
        Box("Gold landing zone", world, new Vector3(gap+.65f,.065f,0), new Vector3(1,.04f,2.65f), new Color(.96f,.78f,.38f));
        Box("Landing center stripe", world, new Vector3(gap+.65f,.09f,0), new Vector3(.04f,.02f,2.65f), new Color(1,.95f,.79f));
        Box("Gap ruler", world, new Vector3(gap*.5f,-.24f,-1.7f), new Vector3(gap,.045f,.045f), new Color(.36f,.43f,.51f));
        for (int i = 0; i <= 10; i++)
            Box("Ruler mark", world, new Vector3(gap*i/10f,-.24f,-1.7f), new Vector3(.035f,.16f,.035f), new Color(.36f,.43f,.51f));
        Box("Pivot foot", world, new Vector3(-.06f,.04f,0), new Vector3(.4f,.08f,2.2f), new Color(.94f,.76f,.42f));
        Box("Flag pole", world, new Vector3(gap+1.5f,.9f,.95f), new Vector3(.05f,1.8f,.05f), new Color(.93f,.9f,.79f));
        Box("Flag", world, new Vector3(gap+1.72f,1.56f,.95f), new Vector3(.45f,.32f,.07f), Palette[crossings%Palette.Length]);
        pawn = Root("Traveler", world);
        Box("Coat", pawn, new Vector3(0,.22f,0), new Vector3(.26f,.4f,.25f), new Color(.96f,.88f,.68f));
        Box("Head", pawn, new Vector3(0,.55f,0), new Vector3(.29f,.29f,.29f), new Color(.99f,.94f,.83f), PrimitiveType.Sphere);
        Box("Backpack", pawn, new Vector3(-.15f,.28f,0), new Vector3(.12f,.25f,.23f), new Color(.61f,.48f,.42f));
        pawn.localPosition = new Vector3(-1.35f,.05f,0);
        SpawnBlock();
        sky.SetFloor(crossings*5);
        FollowCamera(true);
    }

    private void SpawnBlock()
    {
        float center = layers.Count > 0 ? layers[layers.Count-1].localPosition.z : 0;
        float width = layers.Count > 0 ? layers[layers.Count-1].localScale.z : 1.9f;
        phase = 0;
        moving = Box("Moving bridge slab", tower, new Vector3(0,(layers.Count+.5f)*LayerHeight,center-2),
            new Vector3(.7f,LayerHeight,width), Palette[layers.Count%Palette.Length]);
    }

    private void Update()
    {
        if (!ready) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (previewFrozen) return;
#endif
        bool blocked = SobokArcadeHub.BlocksGameplayInput();
        bool tapped = !blocked && Pressed() && !sound.IsSoundPointer() && !IsTipPointer();
        bool entered = !blocked && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
        if (state == RunState.Building)
        {
            if (entered) TipTower();
            else if (tapped && Time.time >= inputReadyAt) PlaceBlock();
            if (state == RunState.Building && moving != null)
            {
                phase += Time.deltaTime * (2.1f + crossings*.18f);
                float center = layers.Count > 0 ? layers[layers.Count-1].localPosition.z : 0;
                var p = moving.localPosition;
                p.z = center + Mathf.PingPong(phase,4)-2;
                moving.localPosition = p;
            }
        }
        else if ((state == RunState.RoundComplete || state == RunState.Won || state == RunState.GameOver)
            && tapped && Time.time >= inputReadyAt)
        {
            if (state == RunState.RoundComplete) BuildRound();
            else RestartRun();
        }
        TickMotion(Time.time);
        FollowCamera(false);
        sound.SetNight(sky.NightAmount);
    }

    private static bool Pressed()
    {
        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
    }

    private bool IsTipPointer()
    {
        if (state != RunState.Building) return false;
        Vector2 p;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            p = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed) p = Mouse.current.position.ReadValue();
        else return false;
        p.y = Screen.height-p.y;
        return TipRect.Contains(p/HudScale);
    }

    private void PlaceBlock()
    {
        if (state != RunState.Building || moving == null) return;
        float supportCenter = layers.Count > 0 ? layers[layers.Count-1].localPosition.z : 0;
        float supportWidth = layers.Count > 0 ? layers[layers.Count-1].localScale.z : 1.9f;
        float left = Mathf.Max(supportCenter-supportWidth*.5f, moving.localPosition.z-moving.localScale.z*.5f);
        float right = Mathf.Min(supportCenter+supportWidth*.5f, moving.localPosition.z+moving.localScale.z*.5f);
        if (right-left < .18f)
        {
            result = "The slab missed. Keep an overlap when stacking.";
            state = RunState.GameOver;
            inputReadyAt = Time.time+.65f;
            sound.Breach(.25f);
            return;
        }
        bool perfect = Mathf.Abs(moving.localPosition.z-supportCenter) < .1f;
        if (perfect) { left = supportCenter-supportWidth*.5f; right = supportCenter+supportWidth*.5f; }
        moving.localPosition = new Vector3(0,moving.localPosition.y,(left+right)*.5f);
        moving.localScale = new Vector3(.7f,LayerHeight,right-left);
        layers.Add(moving);
        moving = null;
        sound.Placement(perfect);
        SpawnBlock();
    }

    private void TipTower()
    {
        if (state != RunState.Building || layers.Count == 0) return;
        if (moving != null) { moving.gameObject.SetActive(false); Destroy(moving.gameObject); moving = null; }
        motionStartedAt = Time.time;
        state = RunState.Tipping;
        accurateLanding = BridgeLength >= gap+.15f && BridgeLength <= gap+1.15f;
        sound.Launch();
    }

    private void TickMotion(float now)
    {
        float age = Mathf.Max(0,now-motionStartedAt);
        if (state == RunState.Tipping)
        {
            float t = Mathf.Clamp01(age/.85f);
            tower.localRotation = Quaternion.Euler(0,0,-90*Mathf.SmoothStep(0,1,t));
            if (t < 1) return;
            motionStartedAt = now;
            state = BridgeLength >= gap+.08f ? RunState.Crossing : RunState.Falling;
            fallEdge = Mathf.Max(.1f,BridgeLength-.1f);
            fallLane = layers[layers.Count-1].localPosition.z;
            sound.Breach(state == RunState.Crossing ? .35f : .15f);
            return;
        }
        if (state == RunState.Crossing)
        {
            float t = Mathf.Clamp01(age/2.4f);
            float x = Mathf.Lerp(-1.35f,gap+1.25f,t);
            pawn.localPosition = new Vector3(x,WalkHeight(x)+Mathf.Abs(Mathf.Sin(age*12))*.04f,LaneAt(x));
            if (t >= 1)
            {
                crossings++;
                points += accurateLanding ? 150 : 100;
                result = accurateLanding ? "Gold landing! +150" : "Across safely! +100";
                state = crossings >= Goal ? RunState.Won : RunState.RoundComplete;
                inputReadyAt = now+.45f;
                sound.Restore();
            }
            return;
        }
        if (state == RunState.Falling)
        {
            if (age < 1.8f)
            {
                float x = Mathf.Lerp(-1.35f,fallEdge,age/1.8f);
                pawn.localPosition = new Vector3(x,WalkHeight(x),LaneAt(x));
            }
            else
            {
                float fall = age-1.8f;
                pawn.localPosition = new Vector3(fallEdge+fall*.6f,.35f-fall*fall*4,fallLane);
                pawn.localRotation = Quaternion.Euler(0,0,-fall*60);
                if (fall >= 1.15f)
                {
                    result = "Bridge too short by " + Mathf.Max(0,gap+.08f-BridgeLength).ToString("0.00") + " m. Add another slab.";
                    state = RunState.GameOver;
                    inputReadyAt = now+.4f;
                }
            }
        }
    }

    private float WalkHeight(float x)
    {
        if (x < 0) return Mathf.Lerp(.05f,.35f,Mathf.Clamp01((x+.55f)/.55f));
        if (x > gap) return Mathf.Lerp(.35f,.05f,Mathf.Clamp01((x-gap)/.5f));
        return .35f;
    }

    private float LaneAt(float x)
    {
        if (layers.Count == 0 || x <= 0) return 0;
        if (x >= gap) return Mathf.Lerp(layers[layers.Count-1].localPosition.z,0,Mathf.Clamp01((x-gap)/.7f));
        float layer = x/LayerHeight-.5f;
        int a = Mathf.Clamp(Mathf.FloorToInt(layer),0,layers.Count-1);
        int b = Mathf.Min(a+1,layers.Count-1);
        return Mathf.Lerp(layers[a].localPosition.z,layers[b].localPosition.z,Mathf.Clamp01(layer-a));
    }

    private void FollowCamera(bool snap)
    {
        float height = state == RunState.Building ? BridgeLength+LayerHeight : 2;
        float size = Mathf.Max(5.2f,(gap+4.7f)/(Mathf.Max(.3f,view.aspect)*1.85f),height*.72f+2.3f);
        var focus = new Vector3((gap-.4f)*.5f,Mathf.Max(1.15f,height*.3f),0);
        view.transform.rotation = Quaternion.Euler(20,-25,0);
        Vector3 position = focus-view.transform.forward*22;
        view.orthographicSize = snap ? size : Mathf.Lerp(view.orthographicSize,size,Time.deltaTime*3);
        view.transform.position = snap ? position : Vector3.Lerp(view.transform.position,position,Time.deltaTime*4);
    }

    private void OnGUI()
    {
        if (!ready) return;
        float scale = HudScale;
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1));
        float width = Screen.width/scale, height = Screen.height/scale;
        var label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
        label.normal.textColor = new Color(.24f,.27f,.32f);
        GUI.Label(new Rect(16,58,width-32,30),"04  /  TOWER BRIDGE",label);
        label.fontSize = 31;
        label.fontStyle = FontStyle.Bold;
        string headline = state == RunState.Won ? "ALL FIVE CROSSED" : state == RunState.GameOver ? "CROSSING ENDED" : "CROSSING " + Mathf.Min(crossings+1,Goal) + " / " + Goal;
        GUI.Label(new Rect(8,88,width-16,46),headline,label);
        label.fontStyle = FontStyle.Normal;
        label.fontSize = 16;
        GUI.Label(new Rect(12,137,width-24,28),"GAP " + gap.ToString("0.00") + " m     TOWER " + BridgeLength.ToString("0.00") + " m     " + points + " PTS",label);
        label.fontSize = 13;
        string hint = state == RunState.Building ? (layers.Count == 0 ? "Stack a tower, then tip it toward the next island." : BridgeLength < gap+.08f ? "Too short. Stack " + Mathf.CeilToInt((gap+.08f-BridgeLength)/LayerHeight) + " more slab(s) to reach." : "Long enough! Gold zone: " + (gap+.15f).ToString("0.00") + " - " + (gap+1.15f).ToString("0.00") + " m.") : "The tower you built becomes the bridge you cross.";
        GUI.Label(new Rect(12,166,width-24,28),hint,label);
        GUI.color = new Color(.1f,.16f,.2f,.94f);
        GUI.DrawTexture(new Rect(16,height-163,width-32,145),Texture2D.whiteTexture);
        GUI.color = Color.white;
        label.normal.textColor = new Color(.98f,.93f,.81f);
        label.fontSize = 20;
        string action = state == RunState.Building ? "TAP TO STACK" : state == RunState.Tipping ? "TIP THE TOWER" : state == RunState.Crossing ? "CROSSING..." : state == RunState.Falling ? "THE BRIDGE DOESN'T REACH" : state == RunState.RoundComplete ? "TAP FOR THE NEXT GAP" : "TAP TO TRY AGAIN";
        GUI.Label(new Rect(20,height-157,width-40,37),action,label);
        if (state == RunState.Building)
        {
            bool oldEnabled = GUI.enabled;
            GUI.enabled = layers.Count > 0;
            var button = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            GUI.backgroundColor = new Color(.96f,.8f,.43f);
            if (GUI.Button(TipRect,"TIP INTO A BRIDGE",button) && !SobokArcadeHub.BlocksGameplayInput()) TipTower();
            GUI.backgroundColor = Color.white;
            GUI.enabled = oldEnabled;
            label.fontSize = 12;
            GUI.Label(new Rect(20,height-62,width-40,35),"Tap / Space: stack     Enter / button: tip\nA longer bridge still works. Gold landings earn +50.",label);
        }
        else
        {
            label.fontSize = 14;
            label.wordWrap = true;
            GUI.Label(new Rect(32,height-113,width-64,70),state == RunState.Won ? "Five islands connected!\n" + points + " points - replay for five gold landings." : string.IsNullOrEmpty(result) ? "Keep the tower wide enough to survive stacking.\nReach the far island to continue." : result,label);
        }
        GUI.color = oldColor;
        GUI.matrix = oldMatrix;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
