using UnityEngine;

// Dither-free opaque fading to the warm backdrop, followed by removal.
public sealed class SobokFade : MonoBehaviour
{
    private Renderer surface;
    private MaterialPropertyBlock properties;
    private Color original;
    private float elapsed;

    private void Awake()
    {
        surface = GetComponent<Renderer>();
        properties = new MaterialPropertyBlock();
        surface.GetPropertyBlock(properties);
        original = properties.GetColor("_BaseColor");
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float blend = Mathf.SmoothStep(0, 1, elapsed / 1.2f);
        properties.SetColor("_BaseColor", Color.Lerp(original, new Color(0.96f, 0.93f, 0.86f), blend));
        surface.SetPropertyBlock(properties);
        if (elapsed >= 1.2f) Destroy(gameObject);
    }
}
