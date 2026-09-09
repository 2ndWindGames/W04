using UnityEngine;

/// <summary>Camera-relative illustrated landscape, one complete day every forty floors.</summary>
public sealed class SobokSky : MonoBehaviour
{
    private Camera view;
    private Transform backdrop;
    private Material paint;
    private float targetPhase;
    private float phase;
    public float NightAmount => Mathf.Max(0, 1 - Mathf.Abs(Mathf.Repeat(phase, 4) - 2));
    public Color Ink => Color.Lerp(new Color(.26f,.25f,.34f), new Color(.95f,.92f,.85f), NightAmount);
    public string TimeLabel
    {
        get
        {
            string[] names = { "Daylight", "Sunset", "Starlight", "Dawn" };
            return names[Mathf.FloorToInt(Mathf.Repeat(phase + .5f, 4))];
        }
    }

    public void Initialize(Camera camera)
    {
        view = camera;
        var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "Illustrated sky";
        Destroy(panel.GetComponent<Collider>());
        backdrop = panel.transform;
        backdrop.SetParent(view.transform, false);
        backdrop.localPosition = new Vector3(0, 0, Mathf.Min(80, view.farClipPlane * .8f));
        paint = new Material(Resources.Load<Shader>("SobokSky"));
        panel.GetComponent<Renderer>().sharedMaterial = paint;
        panel.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        panel.GetComponent<Renderer>().receiveShadows = false;
    }

    public void SetFloor(int floor) => targetPhase = floor / 10f;

    private void LateUpdate()
    {
        if (paint == null) return;
        phase = Mathf.MoveTowards(phase, targetPhase, Time.deltaTime * .25f);
        paint.SetFloat("_Phase", phase);
        paint.SetFloat("_Aspect", view.aspect);
        backdrop.localScale = new Vector3(view.orthographicSize * 2 * view.aspect, view.orthographicSize * 2, 1);
    }

    private void OnDestroy()
    {
        if (backdrop != null) Destroy(backdrop.gameObject);
        if (paint != null) Destroy(paint);
    }
}
