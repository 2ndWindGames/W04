using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A solid wall around a stepped aperture, with one opening interval per tower layer.</summary>
public sealed class SobokBreachWall : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly Color WallColor = new Color(.10f, .23f, .29f);
    private static readonly Color SideColor = new Color(.07f, .16f, .23f);
    private static readonly Color EdgeColor = new Color(1f, .85f, .49f);
    private static readonly Color FaceColor = new Color(.98f, .95f, .79f);

    private readonly List<Renderer> pieces = new List<Renderer>();
    private readonly List<Color> pieceColors = new List<Color>();
    private MaterialPropertyBlock properties;
    private Transform ownedGeometry;
    private Vector2[] openings = Array.Empty<Vector2>();
    private Material sharedPaint;
    private float lastImpact = -1;

    public int RowCount => openings.Length;

    /// <summary>Returns the minimum and maximum local X for a row's clear opening.</summary>
    public Vector2 Opening(int row)
    {
        if (row < 0 || row >= openings.Length)
            throw new ArgumentOutOfRangeException(nameof(row));
        return openings[row];
    }

    public void Build(float[] centers, float[] widths, float layerHeight, Material material, float depth = .45f)
    {
        if (properties == null) properties = new MaterialPropertyBlock();
        if (centers == null || widths == null || centers.Length == 0 || centers.Length != widths.Length)
            throw new ArgumentException("The wall requires matching, nonempty center and width arrays.");
        if (!Finite(layerHeight) || layerHeight <= 0 || !Finite(depth) || depth <= 0)
            throw new ArgumentOutOfRangeException(nameof(layerHeight), "Layer height and depth must be positive.");
        if (material == null) throw new ArgumentNullException(nameof(material));

        var nextOpenings = new Vector2[centers.Length];
        float extent = 5;
        for (int i = 0; i < centers.Length; i++)
        {
            if (!Finite(centers[i]) || !Finite(widths[i]) || widths[i] <= 0)
                throw new ArgumentException("Opening centers must be finite and widths must be positive.");
            nextOpenings[i] = new Vector2(centers[i] - widths[i] * .5f, centers[i] + widths[i] * .5f);
            extent = Mathf.Max(extent, Mathf.Abs(nextOpenings[i].x) + 1.4f, Mathf.Abs(nextOpenings[i].y) + 1.4f);
        }

        ClearGeometry();
        openings = nextOpenings;
        sharedPaint = material;
        ownedGeometry = new GameObject("Breach wall geometry").transform;
        ownedGeometry.SetParent(transform, false);

        float bottom = -layerHeight * .5f;
        float top = (openings.Length - .5f) * layerHeight;
        float trim = Mathf.Min(.065f, layerHeight * .16f);
        float front = -depth * .5f - .012f;

        for (int row = 0; row < openings.Length; row++)
        {
            Vector2 hole = openings[row];
            float y = row * layerHeight;
            AddBox("Left wall " + row, new Vector3((hole.x - extent) * .5f, y, 0),
                new Vector3(hole.x + extent, layerHeight, depth), WallColor);
            AddBox("Right wall " + row, new Vector3((hole.y + extent) * .5f, y, 0),
                new Vector3(extent - hole.y, layerHeight, depth), WallColor);

            // Every trim lies on solid wall, leaving the gameplay aperture unobstructed.
            AddBox("Left aperture edge " + row, new Vector3(hole.x - trim * .5f, y, front),
                new Vector3(trim, layerHeight, .035f), FaceColor);
            AddBox("Right aperture edge " + row, new Vector3(hole.y + trim * .5f, y, front),
                new Vector3(trim, layerHeight, .035f), FaceColor);

            // Small row ticks help players compare the opening against their five blocks.
            AddBox("Left layer tick " + row, new Vector3(hole.x - .24f, y, front),
                new Vector3(.19f, trim * .55f, .025f), EdgeColor);
            AddBox("Right layer tick " + row, new Vector3(hole.y + .24f, y, front),
                new Vector3(.19f, trim * .55f, .025f), EdgeColor);

            if (row > 0)
            {
                Vector2 below = openings[row - 1];
                float boundary = y - layerHeight * .5f;
                AddStep("Left step " + row, below.x, hole.x, boundary,
                    below.x > hole.x ? -1 : 1, trim, front);
                AddStep("Right step " + row, below.y, hole.y, boundary,
                    below.y < hole.y ? -1 : 1, trim, front);
            }
        }

        AddBox("Lower beam", new Vector3(0, bottom - .21f, 0), new Vector3(extent * 2, .42f, depth), SideColor);
        AddBox("Upper beam", new Vector3(0, top + .36f, 0), new Vector3(extent * 2, .72f, depth), SideColor);
        AddBox("Aperture sill", new Vector3((openings[0].x + openings[0].y) * .5f, bottom - trim * .5f, front),
            new Vector3(openings[0].y - openings[0].x, trim, .035f), FaceColor);
        Vector2 highest = openings[openings.Length - 1];
        AddBox("Aperture lintel", new Vector3((highest.x + highest.y) * .5f, top + trim * .5f, front),
            new Vector3(highest.y - highest.x, trim, .035f), FaceColor);

        float fullHeight = top - bottom + 1.14f;
        float midY = (top + bottom + .30f) * .5f;
        AddBox("Left outer rim", new Vector3(-extent + .08f, midY, front), new Vector3(.16f, fullHeight, .07f), EdgeColor);
        AddBox("Right outer rim", new Vector3(extent - .08f, midY, front), new Vector3(.16f, fullHeight, .07f), EdgeColor);
        AddBox("Upper gold bar", new Vector3(0, top + .65f, front), new Vector3(extent * 2, .06f, .035f), EdgeColor);

        // A single diamond reads as a destination marker without adding instructional text.
        Transform marker = AddBox("Gate marker", new Vector3((highest.x + highest.y) * .5f, top + .37f, front - .04f),
            new Vector3(.18f, .18f, .04f), EdgeColor);
        marker.localRotation = Quaternion.Euler(0, 0, 45);
        lastImpact = -1;
        SetImpact(0);
    }

    /// <summary>Flash the solid frame on contact. The caller animates amount back to zero.</summary>
    public void SetImpact(float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (Mathf.Approximately(lastImpact, amount)) return;
        lastImpact = amount;
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] == null) continue;
            Color color = Color.Lerp(pieceColors[i], FaceColor, amount * .72f);
            properties.Clear();
            properties.SetColor(BaseColorId, color);
            properties.SetColor(ColorId, color);
            pieces[i].SetPropertyBlock(properties);
        }
    }

    private void AddStep(string label, float below, float above, float y, float solidSide, float trim, float front)
    {
        float span = Mathf.Abs(above - below);
        if (span < .0001f) return;
        AddBox(label, new Vector3((below + above) * .5f, y + solidSide * trim * .5f, front),
            new Vector3(span, trim, .035f), FaceColor);
    }

    private Transform AddBox(string label, Vector3 position, Vector3 scale, Color color)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = label;
        piece.transform.SetParent(ownedGeometry, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = scale;
        Collider collider = piece.GetComponent<Collider>();
        collider.enabled = false;
        if (Application.isPlaying) Destroy(collider);
        else DestroyImmediate(collider);
        Renderer render = piece.GetComponent<Renderer>();
        render.sharedMaterial = sharedPaint;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        pieces.Add(render);
        pieceColors.Add(color);
        return piece.transform;
    }

    private void ClearGeometry()
    {
        if (ownedGeometry != null)
        {
            ownedGeometry.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(ownedGeometry.gameObject);
            else DestroyImmediate(ownedGeometry.gameObject);
        }
        ownedGeometry = null;
        pieces.Clear();
        pieceColors.Clear();
    }

    private void OnDestroy() => ClearGeometry();
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
