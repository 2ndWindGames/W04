using UnityEngine;

/// <summary>A brief bloom animation; geometry stays attached to its garden until reset.</summary>
public sealed class SobokGardenBloom : MonoBehaviour
{
    private float began;
    private float size = 1;

    public void Initialize(float targetSize)
    {
        size = targetSize;
        began = Time.unscaledTime;
        transform.localScale = Vector3.one * .02f;
    }

    private void Update()
    {
        float progress = Mathf.Clamp01((Time.unscaledTime - began) / .55f);
        transform.localScale = Vector3.one * (Mathf.SmoothStep(.02f, size, progress));
        if (progress >= 1) enabled = false;
    }
}
