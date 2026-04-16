using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class RoadNodeKeepClearMarkingV2 : MonoBehaviour
{
    [SerializeField] private RoadNodeV2 node;

    [Header("Visual")]
    [SerializeField] private Color markingColor = new Color(1f, 0.92f, 0.1f, 0.95f);
    [SerializeField] private float lineWidth = 0.045f;
    [SerializeField] private float boxInset = 0.08f;
    [SerializeField] private float diagonalSpacing = 0.30f;
    [SerializeField] private int sortingOrder = 60;
    [SerializeField] private float zOffset = -0.02f;

    private readonly List<LineRenderer> lineRenderers = new List<LineRenderer>();
    private Transform linesRoot;

    private static Material cachedMaterial;

    public void SyncFromNode()
    {
        if (node == null)
            node = GetComponent<RoadNodeV2>();

        RefreshVisual();
    }

    public void ClearVisuals()
    {
        EnsureRoot();

        for (int i = 0; i < lineRenderers.Count; i++)
        {
            if (lineRenderers[i] != null)
                lineRenderers[i].enabled = false;
        }
    }

    private void Awake()
    {
        if (node == null)
            node = GetComponent<RoadNodeV2>();

        RefreshVisual();
    }

    private void OnEnable()
    {
        RefreshVisual();
    }

    private void OnValidate()
    {
        if (node == null)
            node = GetComponent<RoadNodeV2>();

        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (node == null)
            node = GetComponent<RoadNodeV2>();

        EnsureRoot();
        CompensateParentScale();

        if (node == null || !node.IsIntersection || !node.KeepIntersectionClear)
        {
            ClearVisuals();
            return;
        }

        GetNodeHalfExtents(node, out float halfX, out float halfY);

        float xMin = node.transform.position.x - halfX + boxInset;
        float xMax = node.transform.position.x + halfX - boxInset;
        float yMin = node.transform.position.y - halfY + boxInset;
        float yMax = node.transform.position.y + halfY - boxInset;

        if (xMax <= xMin + 0.05f || yMax <= yMin + 0.05f)
        {
            ClearVisuals();
            return;
        }

        List<(Vector3 a, Vector3 b)> segments = new List<(Vector3 a, Vector3 b)>();

        Vector3 bl = new Vector3(xMin, yMin, zOffset);
        Vector3 br = new Vector3(xMax, yMin, zOffset);
        Vector3 tr = new Vector3(xMax, yMax, zOffset);
        Vector3 tl = new Vector3(xMin, yMax, zOffset);

        segments.Add((bl, br));
        segments.Add((br, tr));
        segments.Add((tr, tl));
        segments.Add((tl, bl));

        BuildDiagonalSegmentsPositive(xMin, xMax, yMin, yMax, diagonalSpacing, zOffset, segments);
        BuildDiagonalSegmentsNegative(xMin, xMax, yMin, yMax, diagonalSpacing, zOffset, segments);

        EnsureLineCount(segments.Count);

        for (int i = 0; i < segments.Count; i++)
            ApplyLine(lineRenderers[i], segments[i].a, segments[i].b);

        for (int i = segments.Count; i < lineRenderers.Count; i++)
        {
            if (lineRenderers[i] != null)
                lineRenderers[i].enabled = false;
        }
    }

    private void BuildDiagonalSegmentsPositive(
        float xMin,
        float xMax,
        float yMin,
        float yMax,
        float spacing,
        float z,
        List<(Vector3 a, Vector3 b)> segments)
    {
        float cMin = yMin - xMax;
        float cMax = yMax - xMin;

        for (float c = cMin; c <= cMax; c += Mathf.Max(0.08f, spacing))
        {
            if (TryGetPositiveSlopeSegment(xMin, xMax, yMin, yMax, c, z, out Vector3 a, out Vector3 b))
                segments.Add((a, b));
        }
    }

    private void BuildDiagonalSegmentsNegative(
        float xMin,
        float xMax,
        float yMin,
        float yMax,
        float spacing,
        float z,
        List<(Vector3 a, Vector3 b)> segments)
    {
        float cMin = yMin + xMin;
        float cMax = yMax + xMax;

        for (float c = cMin; c <= cMax; c += Mathf.Max(0.08f, spacing))
        {
            if (TryGetNegativeSlopeSegment(xMin, xMax, yMin, yMax, c, z, out Vector3 a, out Vector3 b))
                segments.Add((a, b));
        }
    }

    private bool TryGetPositiveSlopeSegment(
        float xMin,
        float xMax,
        float yMin,
        float yMax,
        float c,
        float z,
        out Vector3 a,
        out Vector3 b)
    {
        List<Vector3> points = new List<Vector3>();

        TryAddUnique(points, new Vector3(xMin, xMin + c, z), xMin, xMax, yMin, yMax);
        TryAddUnique(points, new Vector3(xMax, xMax + c, z), xMin, xMax, yMin, yMax);
        TryAddUnique(points, new Vector3(yMin - c, yMin, z), xMin, xMax, yMin, yMax);
        TryAddUnique(points, new Vector3(yMax - c, yMax, z), xMin, xMax, yMin, yMax);

        return ExtractSegment(points, out a, out b);
    }

    private bool TryGetNegativeSlopeSegment(
        float xMin,
        float xMax,
        float yMin,
        float yMax,
        float c,
        float z,
        out Vector3 a,
        out Vector3 b)
    {
        List<Vector3> points = new List<Vector3>();

        TryAddUnique(points, new Vector3(xMin, -xMin + c, z), xMin, xMax, yMin, yMax);
        TryAddUnique(points, new Vector3(xMax, -xMax + c, z), xMin, xMax, yMin, yMax);
        TryAddUnique(points, new Vector3(c - yMin, yMin, z), xMin, xMax, yMin, yMax);
        TryAddUnique(points, new Vector3(c - yMax, yMax, z), xMin, xMax, yMin, yMax);

        return ExtractSegment(points, out a, out b);
    }

    private void TryAddUnique(
        List<Vector3> points,
        Vector3 candidate,
        float xMin,
        float xMax,
        float yMin,
        float yMax)
    {
        const float eps = 0.001f;

        if (candidate.x < xMin - eps || candidate.x > xMax + eps || candidate.y < yMin - eps || candidate.y > yMax + eps)
            return;

        for (int i = 0; i < points.Count; i++)
        {
            if (Vector3.Distance(points[i], candidate) < 0.01f)
                return;
        }

        points.Add(candidate);
    }

    private bool ExtractSegment(List<Vector3> points, out Vector3 a, out Vector3 b)
    {
        a = Vector3.zero;
        b = Vector3.zero;

        if (points.Count < 2)
            return false;

        float bestDistance = -1f;

        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                float d = Vector3.Distance(points[i], points[j]);
                if (d > bestDistance)
                {
                    bestDistance = d;
                    a = points[i];
                    b = points[j];
                }
            }
        }

        return bestDistance > 0.02f;
    }

    private void GetNodeHalfExtents(RoadNodeV2 targetNode, out float halfX, out float halfY)
    {
        halfX = 0.3f;
        halfY = 0.3f;

        if (targetNode == null)
            return;

        for (int i = 0; i < targetNode.ConnectedSegments.Count; i++)
        {
            RoadSegmentV2 segment = targetNode.ConnectedSegments[i];
            if (segment == null || segment.StartNode == null || segment.EndNode == null)
                continue;

            Vector3 dir = (segment.EndNode.transform.position - segment.StartNode.transform.position).normalized;
            bool isHorizontal = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y);

            if (isHorizontal)
                halfY = Mathf.Max(halfY, segment.TotalRoadWidth * 0.5f);
            else
                halfX = Mathf.Max(halfX, segment.TotalRoadWidth * 0.5f);
        }
    }

    private void EnsureRoot()
    {
        if (linesRoot == null)
        {
            Transform existing = transform.Find("KeepClearMarking");
            if (existing != null)
            {
                linesRoot = existing;
            }
            else
            {
                GameObject root = new GameObject("KeepClearMarking");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                linesRoot = root.transform;
            }
        }

        if (lineRenderers.Count == 0)
        {
            LineRenderer[] existingRenderers = linesRoot.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < existingRenderers.Length; i++)
                lineRenderers.Add(existingRenderers[i]);
        }
    }

    private void CompensateParentScale()
    {
        if (linesRoot == null)
            return;

        Vector3 parentScale = transform.lossyScale;

        float sx = Mathf.Abs(parentScale.x) > 0.0001f ? 1f / parentScale.x : 1f;
        float sy = Mathf.Abs(parentScale.y) > 0.0001f ? 1f / parentScale.y : 1f;
        float sz = Mathf.Abs(parentScale.z) > 0.0001f ? 1f / parentScale.z : 1f;

        linesRoot.localScale = new Vector3(sx, sy, sz);
    }

    private void EnsureLineCount(int count)
    {
        EnsureRoot();

        while (lineRenderers.Count < count)
        {
            GameObject child = new GameObject($"Line_{lineRenderers.Count}");
            child.transform.SetParent(linesRoot, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            LineRenderer lr = child.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lr);
            lineRenderers.Add(lr);
        }
    }

    private void ApplyLine(LineRenderer lr, Vector3 a, Vector3 b)
    {
        if (lr == null)
            return;

        ConfigureLineRenderer(lr);

        lr.enabled = true;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = markingColor;
        lr.endColor = markingColor;
        lr.sortingOrder = sortingOrder;
    }

    private void ConfigureLineRenderer(LineRenderer lr)
    {
        if (lr == null)
            return;

        if (cachedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            cachedMaterial = new Material(shader);
            cachedMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        lr.sharedMaterial = cachedMaterial;
        lr.useWorldSpace = true;
        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.TransformZ;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.sortingOrder = sortingOrder;
    }
}