using UnityEngine;

// Shrinking opaque debris works over every illustrated sky without a colored halo.
public sealed class SobokFade : MonoBehaviour
{
    private Renderer surface;
    private MaterialPropertyBlock properties;
    private Color original;
    private float elapsed;
    private Vector3 initialScale;

    private void Awake()
    {
        surface = GetComponent<Renderer>();
        properties = new MaterialPropertyBlock();
        surface.GetPropertyBlock(properties);
        original = properties.GetColor("_BaseColor");
        initialScale = transform.localScale;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float blend = Mathf.SmoothStep(0, 1, elapsed / 1.2f);
        transform.localScale = initialScale * (1 - blend);
        properties.SetColor("_BaseColor", original);
        surface.SetPropertyBlock(properties);
        if (elapsed >= 1.2f) Destroy(gameObject);
    }
}
