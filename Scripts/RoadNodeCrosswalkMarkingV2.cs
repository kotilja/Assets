using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class RoadNodeCrosswalkMarkingV2 : MonoBehaviour
{
    [SerializeField] private RoadNodeV2 node;

    [Header("Visual")]
    [SerializeField] private Color stripeColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private float stripeWidth = 0.08f;
    [SerializeField] private int stripesPerArm = 6;
    [SerializeField] private float crosswalkInset = 0.08f;
    [SerializeField] private int sortingOrder = 18;
    [SerializeField] private float zOffset = -0.03f;

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

        if (node == null || !node.IsIntersection)
        {
            ClearVisuals();
            return;
        }

        List<(Vector3 a, Vector3 b)> segments = BuildCrosswalkSegments();
        EnsureLineCount(segments.Count);

        for (int i = 0; i < segments.Count; i++)
            ApplyLine(lineRenderers[i], segments[i].a, segments[i].b);

        for (int i = segments.Count; i < lineRenderers.Count; i++)
        {
            if (lineRenderers[i] != null)
                lineRenderers[i].enabled = false;
        }
    }

    private List<(Vector3 a, Vector3 b)> BuildCrosswalkSegments()
    {
        List<(Vector3 a, Vector3 b)> segments = new List<(Vector3 a, Vector3 b)>();

        if (node == null || node.ConnectedSegments == null)
            return segments;

        for (int i = 0; i < node.ConnectedSegments.Count; i++)
        {
            RoadSegmentV2 segment = node.ConnectedSegments[i];
            if (segment == null)
                continue;

            if (!TryGetCrosswalkEndpoints(segment, out Vector3 leftExit, out Vector3 rightExit))
                continue;

            Vector3 axis = (rightExit - leftExit).normalized;
            if (axis.sqrMagnitude < 0.0001f)
                continue;

            Vector3 normal = new Vector3(-axis.y, axis.x, 0f);
            float span = Vector3.Distance(leftExit, rightExit);
            float stripeSpan = Mathf.Max(segment.SidewalkWidth * 0.9f, segment.LaneWidth * 0.75f);
            float halfLength = Mathf.Max(0.01f, stripeSpan * 0.5f);
            int count = Mathf.Clamp(stripesPerArm, 5, 8);
            float inset = Mathf.Clamp(segment.SidewalkWidth * 0.25f + crosswalkInset * 0.25f, 0.05f, 0.12f);
            float usableSpan = Mathf.Max(0.01f, span - inset * 2f);

            for (int j = 0; j < count; j++)
            {
                float t = count == 1 ? 0.5f : j / (float)(count - 1);
                float along = inset + usableSpan * t;
                Vector3 stripeCenter = leftExit + axis * along;
                Vector3 a = stripeCenter - normal * halfLength;
                Vector3 b = stripeCenter + normal * halfLength;
                a.z = zOffset;
                b.z = zOffset;
                segments.Add((a, b));
            }
        }

        return segments;
    }

    private bool TryGetCrosswalkEndpoints(RoadSegmentV2 segment, out Vector3 leftExit, out Vector3 rightExit)
    {
        leftExit = Vector3.zero;
        rightExit = Vector3.zero;

        if (segment == null || node == null)
            return false;

        List<Vector3> leftPolyline = segment.GetLeftSidewalkPolylineWorld();
        List<Vector3> rightPolyline = segment.GetRightSidewalkPolylineWorld();

        if (leftPolyline == null || rightPolyline == null || leftPolyline.Count < 2 || rightPolyline.Count < 2)
            return false;

        bool isStartNode = segment.StartNode == node;
        bool isEndNode = segment.EndNode == node;
        if (!isStartNode && !isEndNode)
            return false;

        leftExit = isStartNode ? leftPolyline[0] : leftPolyline[leftPolyline.Count - 1];
        rightExit = isStartNode ? rightPolyline[0] : rightPolyline[rightPolyline.Count - 1];

        Vector3 armDirection = GetArmDirection(segment);
        if (armDirection.sqrMagnitude > 0.0001f)
        {
            float inset = Mathf.Max(0.04f, segment.JunctionInset * 0.9f + crosswalkInset);
            Vector3 shift = -armDirection.normalized * inset;
            leftExit += shift;
            rightExit += shift;
        }

        leftExit.z = zOffset;
        rightExit.z = zOffset;
        return true;
    }

    private Vector3 GetArmDirection(RoadSegmentV2 segment)
    {
        if (segment == null || node == null)
            return Vector3.zero;

        Vector3 direction = Vector3.zero;

        if (segment.StartNode == node && segment.EndNode != null)
            direction = segment.EndNode.transform.position - node.transform.position;
        else if (segment.EndNode == node && segment.StartNode != null)
            direction = segment.StartNode.transform.position - node.transform.position;

        direction.z = 0f;
        return direction.normalized;
    }

    private void ApplyLine(LineRenderer renderer, Vector3 a, Vector3 b)
    {
        if (renderer == null)
            return;

        renderer.gameObject.name = "CrosswalkStripe";
        renderer.enabled = true;
        renderer.positionCount = 2;
        renderer.startWidth = stripeWidth;
        renderer.endWidth = stripeWidth;
        renderer.startColor = stripeColor;
        renderer.endColor = stripeColor;
        renderer.sortingOrder = sortingOrder;
        renderer.SetPosition(0, a);
        renderer.SetPosition(1, b);
    }

    private void EnsureLineCount(int targetCount)
    {
        while (lineRenderers.Count < targetCount)
        {
            GameObject go = new GameObject($"CrosswalkStripe_{lineRenderers.Count}");
            go.transform.SetParent(linesRoot != null ? linesRoot : transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            LineRenderer renderer = go.AddComponent<LineRenderer>();
            ConfigureRenderer(renderer);
            lineRenderers.Add(renderer);
        }
    }

    private void EnsureRoot()
    {
        if (linesRoot != null)
            return;

        Transform existing = transform.Find("Crosswalk_Stripes");
        if (existing != null)
        {
            linesRoot = existing;
            return;
        }

        GameObject root = new GameObject("Crosswalk_Stripes");
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        linesRoot = root.transform;
    }

    private void ConfigureRenderer(LineRenderer renderer)
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
        renderer.numCapVertices = 0;
        renderer.numCornerVertices = 0;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.alignment = LineAlignment.TransformZ;
    }
}
