using UnityEngine;

/// <summary>A fifteen-layer keepsake: each placement grows flowers around the tower.</summary>
public sealed class SobokGardenGame : SobokClassicGame
{
    private int flowers;
    private MaterialPropertyBlock flowerTint;
    protected override int TargetFloors => 15;
    protected override string ModeTitle => "S O B O K / G A R D E N";
    protected override string SavePrefix => "Sobok.Garden";
    public override int FlowerCount => flowers;

    protected override void OnRunReset()
    {
        flowers = 0;
        if (flowerTint == null) flowerTint = new MaterialPropertyBlock();
    }

    protected override void OnStacked(Transform previous, Transform placed, bool precise)
    {
        int count = precise ? 4 : 2;
        // Flowers sit on the visible outer edge of the previous layer, keeping their
        // blossoms visible even when a perfectly aligned layer covers its top face.
        int edgeAxis = Floors % 2 == 1 ? 0 : 2;
        float strip = previous.localScale[edgeAxis] - placed.localScale[edgeAxis];
        float previousEdge = previous.position[edgeAxis] - previous.localScale[edgeAxis] * .5f;
        float newEdge = placed.position[edgeAxis] - placed.localScale[edgeAxis] * .5f;
        float edge = strip > .14f && newEdge - previousEdge > .08f
            ? (previousEdge + newEdge) * .5f : previousEdge - .075f;
        int alongAxis = edgeAxis == 0 ? 2 : 0;
        float span = Mathf.Max(.12f, previous.localScale[alongAxis] - .25f);
        for (int i = 0; i < count; i++)
        {
            Vector3 position = previous.position + Vector3.up * (LayerHeight * .5f + .015f);
            position[edgeAxis] = edge;
            position[alongAxis] += (count == 1 ? 0 : i / (float)(count - 1) - .5f) * span;
            Bloom(position, precise ? new Color(1, .83f, .85f) : Palette[(Floors + 2) % Palette.Length], .8f + (i % 2) * .12f);
        }
    }

    protected override void OnGardenFinished()
    {
        // A small bouquet on top gives the finite run an unmistakable finished state.
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.PI * 2 / 5;
            Vector3 position = TopBlock.position + Vector3.up * (LayerHeight * .5f + .015f);
            position.x += Mathf.Cos(angle) * Mathf.Min(.5f, TopBlock.localScale.x * .3f);
            position.z += Mathf.Sin(angle) * Mathf.Min(.5f, TopBlock.localScale.z * .3f);
            Bloom(position, new Color(1, .9f, .62f), 1.25f);
        }
    }

    private void Bloom(Vector3 position, Color color, float scale)
    {
        flowers++;
        Transform bloom = new GameObject("Flower " + flowers).transform;
        bloom.SetParent(tower, false);
        bloom.position = position;
        FlowerPart(bloom, "Moss cushion", PrimitiveType.Sphere, Vector3.zero, new Vector3(.16f, .035f, .16f), new Color(.47f, .57f, .35f));
        FlowerPart(bloom, "Stem", PrimitiveType.Cylinder, new Vector3(0, .085f, 0), new Vector3(.026f, .085f, .026f), new Color(.34f, .48f, .3f));
        Transform leaf = FlowerPart(bloom, "Leaf", PrimitiveType.Sphere, new Vector3(.045f, .075f, 0), new Vector3(.13f, .04f, .07f), new Color(.47f, .61f, .36f));
        leaf.localRotation = Quaternion.Euler(0, 0, 25);
        for (int p = 0; p < 5; p++)
        {
            float angle = p * Mathf.PI * 2 / 5;
            Transform petal = FlowerPart(bloom, "Petal", PrimitiveType.Sphere,
                new Vector3(Mathf.Cos(angle) * .065f, .19f, Mathf.Sin(angle) * .065f), new Vector3(.13f, .04f, .09f), color);
            petal.localRotation = Quaternion.Euler(0, -p * 72, 0);
        }
        FlowerPart(bloom, "Golden center", PrimitiveType.Sphere, new Vector3(0, .21f, 0), new Vector3(.07f, .045f, .07f), new Color(1, .73f, .27f));
        bloom.gameObject.AddComponent<SobokGardenBloom>().Initialize(scale);
    }

    private Transform FlowerPart(Transform parent, string title, PrimitiveType shape, Vector3 position, Vector3 size, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = title;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.GetComponent<Collider>().enabled = false;
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        flowerTint.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(flowerTint);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return part.transform;
    }
}
