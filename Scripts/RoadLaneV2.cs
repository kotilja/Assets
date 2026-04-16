using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class RoadLaneV2 : MonoBehaviour
{
    public enum LaneDirection
    {
        Forward,
        Backward
    }

    [SerializeField] private int laneIndex;
    [SerializeField] private LaneDirection direction;
    [SerializeField] private float width = 0.08f;

    private readonly List<LineRenderer> segmentRenderers = new List<LineRenderer>();
    private Material cachedMaterial;

    public int LaneIndex => laneIndex;
    public LaneDirection Direction => direction;

    public void Initialize(int newLaneIndex, LaneDirection newDirection)
    {
        laneIndex = newLaneIndex;
        direction = newDirection;
        gameObject.name = $"LaneMarking_{direction}_{laneIndex}";
    }

    public void UpdateVisual(
        Vector3 start,
        Vector3 end,
        float newWidth,
        Color color,
        int sortingOrder)
    {
        width = Mathf.Max(0.02f, newWidth);

        List<Vector3> points = new List<Vector3> { start, end };
        UpdatePolylineVisual(points, width, color, sortingOrder);
    }

    public void UpdatePolylineVisual(
        List<Vector3> points,
        float newWidth,
        Color color,
        int sortingOrder)
    {
        width = Mathf.Max(0.02f, newWidth);

        if (points == null || points.Count < 2)
        {
            Hide();
            return;
        }

        EnsureSegmentRendererCount(points.Count - 1);

        int rendererIndex = 0;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];

            if (Vector3.Distance(a, b) < 0.01f)
                continue;

            ApplySegment(segmentRenderers[rendererIndex], a, b, width, color, sortingOrder);
            rendererIndex++;
        }

        for (int i = rendererIndex; i < segmentRenderers.Count; i++)
        {
            if (segmentRenderers[i] != null)
                segmentRenderers[i].enabled = false;
        }
    }

    public void UpdateRestrictedDashedVisual(
        Vector3 start,
        Vector3 end,
        float newWidth,
        Color color,
        int sortingOrder,
        float solidStartLength,
        float solidEndLength,
        float dashLength,
        float gapLength)
    {
        width = Mathf.Max(0.02f, newWidth);

        List<Vector3> points = new List<Vector3> { start, end };
        UpdateRestrictedDashedPolylineVisual(
            points,
            width,
            color,
            sortingOrder,
            solidStartLength,
            solidEndLength,
            dashLength,
            gapLength
        );
    }

    public void UpdateRestrictedDashedPolylineVisual(
        List<Vector3> points,
        float newWidth,
        Color color,
        int sortingOrder,
        float solidStartLength,
        float solidEndLength,
        float dashLength,
        float gapLength)
    {
        width = Mathf.Max(0.02f, newWidth);

        if (points == null || points.Count < 2)
        {
            Hide();
            return;
        }

        float totalLength = GetPolylineLength(points);
        if (totalLength < 0.0001f)
        {
            Hide();
            return;
        }

        float startSolid = Mathf.Clamp(solidStartLength, 0f, totalLength);
        float endSolid = Mathf.Clamp(solidEndLength, 0f, totalLength - startSolid);

        float dashedStart = startSolid;
        float dashedEnd = totalLength - endSolid;

        List<List<Vector3>> visibleRanges = new List<List<Vector3>>();

        if (dashedEnd <= dashedStart + 0.02f)
        {
            List<Vector3> full = ExtractPolylineRange(points, 0f, totalLength);
            if (full.Count >= 2)
                visibleRanges.Add(full);
        }
        else
        {
            if (startSolid > 0.02f)
            {
                List<Vector3> startRange = ExtractPolylineRange(points, 0f, startSolid);
                if (startRange.Count >= 2)
                    visibleRanges.Add(startRange);
            }

            float dash = Mathf.Max(0.05f, dashLength);
            float gap = Mathf.Max(0.02f, gapLength);

            for (float d = dashedStart; d < dashedEnd - 0.02f; d += dash + gap)
            {
                float a = d;
                float b = Mathf.Min(d + dash, dashedEnd);

                if (b - a <= 0.02f)
                    continue;

                List<Vector3> dashRange = ExtractPolylineRange(points, a, b);
                if (dashRange.Count >= 2)
                    visibleRanges.Add(dashRange);
            }

            if (endSolid > 0.02f)
            {
                List<Vector3> endRange = ExtractPolylineRange(points, totalLength - endSolid, totalLength);
                if (endRange.Count >= 2)
                    visibleRanges.Add(endRange);
            }
        }

        if (visibleRanges.Count == 0)
        {
            Hide();
            return;
        }

        int requiredRenderers = 0;
        for (int i = 0; i < visibleRanges.Count; i++)
            requiredRenderers += Mathf.Max(0, visibleRanges[i].Count - 1);

        EnsureSegmentRendererCount(requiredRenderers);

        int rendererIndex = 0;

        for (int i = 0; i < visibleRanges.Count; i++)
        {
            List<Vector3> range = visibleRanges[i];

            for (int j = 0; j < range.Count - 1; j++)
            {
                Vector3 a = range[j];
                Vector3 b = range[j + 1];

                if (Vector3.Distance(a, b) < 0.01f)
                    continue;

                ApplySegment(segmentRenderers[rendererIndex], a, b, width, color, sortingOrder);
                rendererIndex++;
            }
        }

        for (int i = rendererIndex; i < segmentRenderers.Count; i++)
        {
            if (segmentRenderers[i] != null)
                segmentRenderers[i].enabled = false;
        }
    }

    public void Hide()
    {
        for (int i = 0; i < segmentRenderers.Count; i++)
        {
            if (segmentRenderers[i] != null)
                segmentRenderers[i].enabled = false;
        }
    }

    private void Awake()
    {
        RebuildRendererCache();
    }

    private void OnValidate()
    {
        RebuildRendererCache();

        for (int i = 0; i < segmentRenderers.Count; i++)
        {
            if (segmentRenderers[i] != null)
                ConfigureLineRenderer(segmentRenderers[i]);
        }
    }

    private float GetPolylineLength(List<Vector3> points)
    {
        float length = 0f;

        if (points == null)
            return length;

        for (int i = 0; i < points.Count - 1; i++)
            length += Vector3.Distance(points[i], points[i + 1]);

        return length;
    }

    private List<Vector3> ExtractPolylineRange(List<Vector3> points, float fromDistance, float toDistance)
    {
        List<Vector3> result = new List<Vector3>();

        if (points == null || points.Count < 2)
            return result;

        float totalLength = GetPolylineLength(points);
        fromDistance = Mathf.Clamp(fromDistance, 0f, totalLength);
        toDistance = Mathf.Clamp(toDistance, 0f, totalLength);

        if (toDistance <= fromDistance + 0.001f)
            return result;

        float accumulated = 0f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
            float segLength = Vector3.Distance(a, b);

            if (segLength < 0.0001f)
                continue;

            float segStart = accumulated;
            float segEnd = accumulated + segLength;

            if (segEnd < fromDistance)
            {
                accumulated = segEnd;
                continue;
            }

            if (segStart > toDistance)
                break;

            float localFrom = Mathf.Clamp(fromDistance - segStart, 0f, segLength);
            float localTo = Mathf.Clamp(toDistance - segStart, 0f, segLength);

            if (localTo <= localFrom + 0.0001f)
            {
                accumulated = segEnd;
                continue;
            }

            Vector3 dir = (b - a) / segLength;
            Vector3 p0 = a + dir * localFrom;
            Vector3 p1 = a + dir * localTo;

            if (result.Count == 0 || Vector3.Distance(result[result.Count - 1], p0) > 0.005f)
                result.Add(p0);

            if (Vector3.Distance(result[result.Count - 1], p1) > 0.005f)
                result.Add(p1);

            accumulated = segEnd;
        }

        return result;
    }

    private void ApplySegment(
        LineRenderer renderer,
        Vector3 start,
        Vector3 end,
        float lineWidth,
        Color color,
        int sortingOrder)
    {
        if (renderer == null)
            return;

        ConfigureLineRenderer(renderer);

        renderer.enabled = true;
        renderer.positionCount = 2;
        renderer.SetPosition(0, start);
        renderer.SetPosition(1, end);
        renderer.startWidth = lineWidth;
        renderer.endWidth = lineWidth;
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.sortingOrder = sortingOrder;
    }

    private void RebuildRendererCache()
    {
        segmentRenderers.Clear();

        LineRenderer[] renderers = GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                segmentRenderers.Add(renderers[i]);
        }
    }

    private void EnsureSegmentRendererCount(int targetCount)
    {
        RebuildRendererCache();

        while (segmentRenderers.Count < targetCount)
        {
            GameObject child = new GameObject($"Part_{segmentRenderers.Count}");
            child.transform.SetParent(transform);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            LineRenderer renderer = child.AddComponent<LineRenderer>();
            ConfigureLineRenderer(renderer);
            segmentRenderers.Add(renderer);
        }
    }

    private void ConfigureLineRenderer(LineRenderer renderer)
    {
        if (renderer == null)
            return;

        if (cachedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            cachedMaterial = new Material(shader);
            cachedMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        renderer.sharedMaterial = cachedMaterial;
        renderer.useWorldSpace = true;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.alignment = LineAlignment.TransformZ;
        renderer.numCapVertices = 2;
        renderer.numCornerVertices = 2;
    }
}