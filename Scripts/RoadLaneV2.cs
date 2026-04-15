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
        DrawSegments(start, end, width, color, sortingOrder, 0f, 0f, 0f, 0f, false);
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

        DrawSegments(
            start,
            end,
            width,
            color,
            sortingOrder,
            solidStartLength,
            solidEndLength,
            dashLength,
            gapLength,
            true
        );
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

    private void DrawSegments(
        Vector3 start,
        Vector3 end,
        float lineWidth,
        Color color,
        int sortingOrder,
        float solidStartLength,
        float solidEndLength,
        float dashLength,
        float gapLength,
        bool useDashed)
    {
        Vector3 delta = end - start;
        float totalLength = delta.magnitude;

        if (totalLength < 0.0001f)
        {
            Hide();
            return;
        }

        if (!useDashed)
        {
            EnsureSegmentRendererCount(1);
            ApplySegment(segmentRenderers[0], start, end, lineWidth, color, sortingOrder);

            for (int i = 1; i < segmentRenderers.Count; i++)
            {
                if (segmentRenderers[i] != null)
                    segmentRenderers[i].enabled = false;
            }

            return;
        }

        Vector3 dir = delta / totalLength;

        float startSolid = Mathf.Clamp(solidStartLength, 0f, totalLength);
        float endSolid = Mathf.Clamp(solidEndLength, 0f, totalLength - startSolid);

        float dashedStart = startSolid;
        float dashedEnd = totalLength - endSolid;

        List<Vector2> ranges = new List<Vector2>();

        if (dashedEnd <= dashedStart + 0.02f)
        {
            ranges.Add(new Vector2(0f, totalLength));
        }
        else
        {
            if (startSolid > 0.02f)
                ranges.Add(new Vector2(0f, startSolid));

            float dash = Mathf.Max(0.05f, dashLength);
            float gap = Mathf.Max(0.02f, gapLength);

            for (float d = dashedStart; d < dashedEnd - 0.02f; d += dash + gap)
            {
                float a = d;
                float b = Mathf.Min(d + dash, dashedEnd);

                if (b - a > 0.02f)
                    ranges.Add(new Vector2(a, b));
            }

            if (endSolid > 0.02f)
                ranges.Add(new Vector2(totalLength - endSolid, totalLength));
        }

        if (ranges.Count == 0)
        {
            Hide();
            return;
        }

        EnsureSegmentRendererCount(ranges.Count);

        for (int i = 0; i < ranges.Count; i++)
        {
            Vector3 a = start + dir * ranges[i].x;
            Vector3 b = start + dir * ranges[i].y;
            ApplySegment(segmentRenderers[i], a, b, lineWidth, color, sortingOrder);
        }

        for (int i = ranges.Count; i < segmentRenderers.Count; i++)
        {
            if (segmentRenderers[i] != null)
                segmentRenderers[i].enabled = false;
        }
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