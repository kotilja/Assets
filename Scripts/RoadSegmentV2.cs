using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class RoadSegmentV2 : MonoBehaviour
{
    [SerializeField] private int id;
    [SerializeField] private RoadNodeV2 startNode;
    [SerializeField] private RoadNodeV2 endNode;

    [Header("Road settings")]
    [SerializeField] private int forwardLanes = 1;
    [SerializeField] private int backwardLanes = 1;
    [SerializeField] private float laneWidth = 0.6f;
    [SerializeField] private float speedLimit = 3f;
    [SerializeField] private float junctionInset = 0.35f;
    [SerializeField] private float stopLineOffset = 0.18f;

    [Header("Visuals")]
    [SerializeField] private Color roadColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color forwardLaneColor = new Color(0.75f, 0.9f, 1f, 1f);
    [SerializeField] private Color backwardLaneColor = new Color(1f, 0.85f, 0.75f, 1f);
    [SerializeField] private float laneLineWidth = 0.08f;
    [SerializeField] private bool showLaneArrows = true;
    [SerializeField] private Color stopLineColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private float stopLineWidth = 0.12f;
    [SerializeField] private int stopLineSortingOrder = 20;

    [Header("Arrow visuals")]
    [SerializeField] private float arrowLengthScale = 0.7f;
    [SerializeField] private float arrowHeightScale = 0.35f;
    [SerializeField] private int arrowSortingOrder = 50;
    [SerializeField] private float arrowZOffset = -0.1f;
    [SerializeField] private float arrowAngleOffset = 180f;

    [Header("Lane change")]
    [SerializeField] private bool allowLaneChanges = true;
    [SerializeField] private float noLaneChangeNearStart = 0.9f;
    [SerializeField] private float noLaneChangeNearEnd = 1.4f;
    [SerializeField] private float laneChangeStep = 1.6f;
    [SerializeField] private float laneChangeLength = 0.9f;

    private LineRenderer roadRenderer;
    private Material cachedMaterial;
    private LineRenderer forwardStopLineRenderer;
    private LineRenderer backwardStopLineRenderer;

    private Transform lanesRoot;
    private readonly List<RoadLaneV2> laneVisuals = new List<RoadLaneV2>();
    private readonly List<SpriteRenderer> arrowRenderers = new List<SpriteRenderer>();
    private readonly List<RoadLaneDataV2> laneData = new List<RoadLaneDataV2>();

    private static Sprite cachedArrowSprite;

    public int Id => id;
    public RoadNodeV2 StartNode => startNode;
    public RoadNodeV2 EndNode => endNode;
    public int ForwardLanes => forwardLanes;
    public int BackwardLanes => backwardLanes;
    public float LaneWidth => laneWidth;
    public float SpeedLimit => speedLimit;
    public IReadOnlyList<RoadLaneDataV2> LaneData => laneData;
    public float JunctionInset => junctionInset;
    public bool AllowLaneChanges => allowLaneChanges;
    public float NoLaneChangeNearStart => noLaneChangeNearStart;
    public float NoLaneChangeNearEnd => noLaneChangeNearEnd;
    public float LaneChangeStep => laneChangeStep;
    public float LaneChangeLength => laneChangeLength;

    public bool IsOneWay => backwardLanes <= 0;
    public int TotalLaneCount => Mathf.Max(1, forwardLanes + Mathf.Max(0, backwardLanes));
    public float TotalRoadWidth => TotalLaneCount * laneWidth;
    public float StopLineOffset => stopLineOffset;

    public void Initialize(
        int newId,
        RoadNodeV2 newStartNode,
        RoadNodeV2 newEndNode,
        int newForwardLanes,
        int newBackwardLanes,
        float newLaneWidth,
        float newSpeedLimit)
    {
        id = newId;
        startNode = newStartNode;
        endNode = newEndNode;
        forwardLanes = Mathf.Max(1, newForwardLanes);
        backwardLanes = Mathf.Max(0, newBackwardLanes);
        laneWidth = Mathf.Max(0.2f, newLaneWidth);
        speedLimit = Mathf.Max(0.1f, newSpeedLimit);

        gameObject.name = $"RoadSegment_{id}";
        RegisterToNodes();
        RefreshVisual();
    }

    private void Awake()
    {
        RegisterToNodes();
    }

    private void Start()
    {
        RefreshVisual();
    }

    private void OnValidate()
{
#if UNITY_EDITOR
    if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
    {
        UnityEditor.EditorApplication.delayCall += DelayedRefreshVisual;
    }
#else
    RefreshVisual();
#endif
}

#if UNITY_EDITOR
private void DelayedRefreshVisual()
{
    if (this == null)
        return;

    RefreshVisual();
}
#endif

private void OnDestroy()
{
    UnregisterFromNodes();
}

    private void RegisterToNodes()
    {
        if (startNode != null)
            startNode.RegisterSegment(this);

        if (endNode != null)
            endNode.RegisterSegment(this);
    }

    private void UnregisterFromNodes()
    {
        if (startNode != null)
            startNode.UnregisterSegment(this);

        if (endNode != null)
            endNode.UnregisterSegment(this);
    }

    public void RefreshVisual()
    {
        if (startNode == null || endNode == null)
            return;

        EnsureRoadRenderer();
        EnsureLanesRoot();

        Vector3 start = startNode.transform.position;
        Vector3 end = endNode.transform.position;

        roadRenderer.positionCount = 2;
        roadRenderer.SetPosition(0, start);
        roadRenderer.SetPosition(1, end);
        roadRenderer.startWidth = TotalRoadWidth;
        roadRenderer.endWidth = TotalRoadWidth;
        roadRenderer.startColor = roadColor;
        roadRenderer.endColor = roadColor;

        RebuildLaneVisualsAndData(start, end);
    }

    private void RebuildLaneVisualsAndData(Vector3 start, Vector3 end)
    {
        EnsureLaneObjectsCount(TotalLaneCount);
        EnsureArrowObjectsCount(TotalLaneCount);
        EnsureLaneDataCount(TotalLaneCount);

        Vector3 segmentVector = end - start;
        float segmentLength = segmentVector.magnitude;
        if (segmentLength < 0.0001f)
            return;

        Vector3 direction = segmentVector / segmentLength;
        Vector3 normal = new Vector3(-direction.y, direction.x, 0f);

        float startCut = GetNodeCutDistance(startNode, segmentLength);
        float endCut = GetNodeCutDistance(endNode, segmentLength);

        int laneCounter = 0;
        int globalLaneIdSeed = id * 100;

        // Правостороннее движение:
        // forward (start -> end) располагаем справа от направления
        // backward (end -> start) располагаем слева от направления start -> end

        float rightEdgeCenter = -TotalRoadWidth * 0.5f + laneWidth * 0.5f;
        float leftEdgeCenter = TotalRoadWidth * 0.5f - laneWidth * 0.5f;

        for (int i = 0; i < forwardLanes; i++)
        {
            float offset = rightEdgeCenter + i * laneWidth;
            Vector3 rawLaneStart = start + normal * offset;
            Vector3 rawLaneEnd = end + normal * offset;

            Vector3 laneStart = rawLaneStart + direction * startCut;
            Vector3 laneEnd = rawLaneEnd - direction * endCut;

            RoadLaneV2 laneVisual = laneVisuals[laneCounter];
            laneVisual.Initialize(i, RoadLaneV2.LaneDirection.Forward);
            laneVisual.UpdateVisual(
                laneStart,
                laneEnd,
                laneLineWidth,
                forwardLaneColor,
                10
            );

            UpdateArrowVisual(
                arrowRenderers[laneCounter],
                laneStart,
                laneEnd,
                forwardLaneColor,
                $"Arrow_Forward_{i}"
            );

            RoadLaneDataV2 lane = laneData[laneCounter];
            lane.laneId = globalLaneIdSeed + laneCounter;
            lane.ownerSegment = this;
            lane.localLaneIndex = i;
            lane.direction = RoadLaneV2.LaneDirection.Forward;
            lane.start = laneStart;
            lane.end = laneEnd;
            lane.fromNode = startNode;
            lane.toNode = endNode;
            lane.outgoingConnections.Clear();
            lane.incomingConnections.Clear();

            laneCounter++;
        }

        for (int i = 0; i < backwardLanes; i++)
        {
            float offset = leftEdgeCenter - i * laneWidth;
            Vector3 rawStart = start + normal * offset;
            Vector3 rawEnd = end + normal * offset;

            Vector3 trimmedForwardStart = rawStart + direction * startCut;
            Vector3 trimmedForwardEnd = rawEnd - direction * endCut;

            RoadLaneV2 laneVisual = laneVisuals[laneCounter];
            laneVisual.Initialize(i, RoadLaneV2.LaneDirection.Backward);
            laneVisual.UpdateVisual(
                trimmedForwardEnd,
                trimmedForwardStart,
                laneLineWidth,
                backwardLaneColor,
                10
            );

            UpdateArrowVisual(
                arrowRenderers[laneCounter],
                trimmedForwardEnd,
                trimmedForwardStart,
                backwardLaneColor,
                $"Arrow_Backward_{i}"
            );

            RoadLaneDataV2 lane = laneData[laneCounter];
            lane.laneId = globalLaneIdSeed + laneCounter;
            lane.ownerSegment = this;
            lane.localLaneIndex = i;
            lane.direction = RoadLaneV2.LaneDirection.Backward;
            lane.start = trimmedForwardEnd;
            lane.end = trimmedForwardStart;
            lane.fromNode = endNode;
            lane.toNode = startNode;
            lane.outgoingConnections.Clear();
            lane.incomingConnections.Clear();

            laneCounter++;
        }

        UpdateStopLineVisuals(
            start,
            end,
            direction,
            normal,
            startCut,
            endCut,
            rightEdgeCenter,
            leftEdgeCenter
        );
    }

    private void UpdateStopLineVisuals(
    Vector3 start,
    Vector3 end,
    Vector3 direction,
    Vector3 normal,
    float startCut,
    float endCut,
    float rightEdgeCenter,
    float leftEdgeCenter)
    {
        EnsureStopLineRenderers();

        bool showForward = forwardLanes > 0 && endNode != null && endNode.ConnectedSegments.Count > 2;
        bool showBackward = backwardLanes > 0 && startNode != null && startNode.ConnectedSegments.Count > 2;

        float forwardCenterOffset = rightEdgeCenter + (forwardLanes - 1) * laneWidth * 0.5f;
        float backwardCenterOffset = leftEdgeCenter - (backwardLanes - 1) * laneWidth * 0.5f;

        float forwardWidth = forwardLanes * laneWidth;
        float backwardWidth = backwardLanes * laneWidth;

        Vector3 forwardBase = end - direction * (endCut + stopLineOffset);
        Vector3 backwardBase = start + direction * (startCut + stopLineOffset);

        UpdateStopLineRenderer(
            forwardStopLineRenderer,
            showForward,
            "StopLine_Forward",
            forwardBase,
            normal,
            forwardCenterOffset,
            forwardWidth
        );

        UpdateStopLineRenderer(
            backwardStopLineRenderer,
            showBackward,
            "StopLine_Backward",
            backwardBase,
            normal,
            backwardCenterOffset,
            backwardWidth
        );
    }

    private void UpdateStopLineRenderer(
    LineRenderer renderer,
    bool visible,
    string objectName,
    Vector3 basePoint,
    Vector3 normal,
    float carriageCenterOffset,
    float carriageWidth)
    {
        if (renderer == null)
            return;

        renderer.gameObject.name = objectName;
        renderer.enabled = visible;

        if (!visible || carriageWidth <= 0.01f)
            return;

        Vector3 center = basePoint + normal * carriageCenterOffset;
        Vector3 halfSpan = normal * (carriageWidth * 0.5f);

        Vector3 a = center - halfSpan;
        Vector3 b = center + halfSpan;

        a.z = 0f;
        b.z = 0f;

        renderer.positionCount = 2;
        renderer.SetPosition(0, a);
        renderer.SetPosition(1, b);
        renderer.startWidth = stopLineWidth;
        renderer.endWidth = stopLineWidth;
        renderer.startColor = stopLineColor;
        renderer.endColor = stopLineColor;
        renderer.sortingOrder = stopLineSortingOrder;
    }

    public bool TryGetDrivingLane(RoadNodeV2 fromNode, RoadNodeV2 toNode, out RoadLaneDataV2 lane)
{
    lane = null;

    List<RoadLaneDataV2> candidates = GetDrivingLanes(fromNode, toNode);
    if (candidates.Count == 0)
        return false;

    lane = candidates[0];
    return true;
}

    public List<RoadLaneDataV2> GetDrivingLanes(RoadNodeV2 fromNode, RoadNodeV2 toNode)
{
    List<RoadLaneDataV2> result = new List<RoadLaneDataV2>();

    if (fromNode == null || toNode == null)
        return result;

    for (int i = 0; i < laneData.Count; i++)
    {
        RoadLaneDataV2 lane = laneData[i];
        if (lane == null)
            continue;

        if (lane.fromNode == fromNode && lane.toNode == toNode)
            result.Add(lane);
    }

    return result;
}

    private RoadLaneDataV2 GetFirstLaneByDirection(RoadLaneV2.LaneDirection direction)
    {
        for (int i = 0; i < laneData.Count; i++)
        {
            RoadLaneDataV2 lane = laneData[i];

            if (lane == null)
                continue;

            if (lane.direction == direction)
                return lane;
        }

        return null;
    }

    private float GetNodeCutDistance(RoadNodeV2 node, float segmentLength)
    {
        float cut = junctionInset;

        if (node != null)
        {
            float maxOtherHalfWidth = 0f;

            for (int i = 0; i < node.ConnectedSegments.Count; i++)
            {
                RoadSegmentV2 other = node.ConnectedSegments[i];

                if (other == null || other == this)
                    continue;

                maxOtherHalfWidth = Mathf.Max(maxOtherHalfWidth, other.TotalRoadWidth * 0.5f);
            }

            if (maxOtherHalfWidth > 0f)
            {
                cut = Mathf.Max(cut, maxOtherHalfWidth + laneWidth * 0.25f);
            }
        }

        return Mathf.Clamp(cut, 0f, segmentLength * 0.45f);
    }

    private void EnsureLaneDataCount(int targetCount)
    {
        while (laneData.Count < targetCount)
            laneData.Add(new RoadLaneDataV2());

        if (laneData.Count > targetCount)
            laneData.RemoveRange(targetCount, laneData.Count - targetCount);
    }

    private void UpdateArrowVisual(SpriteRenderer arrowRenderer, Vector3 from, Vector3 to, Color color, string objectName)
    {
        if (arrowRenderer == null)
            return;

        arrowRenderer.gameObject.name = objectName;
        arrowRenderer.enabled = showLaneArrows;

        if (!showLaneArrows)
            return;

        arrowRenderer.sprite = GetArrowSprite();
        arrowRenderer.color = color;
        arrowRenderer.sortingOrder = arrowSortingOrder;

        Vector3 center = Vector3.Lerp(from, to, 0.5f);
        Vector3 direction = (to - from).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + arrowAngleOffset;

        arrowRenderer.transform.position = new Vector3(center.x, center.y, arrowZOffset);
        arrowRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        float laneBasedLength = Mathf.Max(arrowLengthScale, laneWidth * 0.9f);
        float laneBasedHeight = Mathf.Max(arrowHeightScale, laneWidth * 0.45f);
        arrowRenderer.transform.localScale = new Vector3(laneBasedLength, laneBasedHeight, 1f);
    }

    private void EnsureLaneObjectsCount(int targetCount)
    {
        while (laneVisuals.Count < targetCount)
        {
            GameObject laneObject = new GameObject();
            laneObject.transform.SetParent(lanesRoot);
            laneObject.transform.localPosition = Vector3.zero;
            laneObject.transform.localRotation = Quaternion.identity;

            RoadLaneV2 lane = laneObject.AddComponent<RoadLaneV2>();
            laneVisuals.Add(lane);
        }

        for (int i = 0; i < laneVisuals.Count; i++)
        {
            bool active = i < targetCount;
            if (laneVisuals[i] != null)
                laneVisuals[i].gameObject.SetActive(active);
        }
    }

    private void EnsureArrowObjectsCount(int targetCount)
    {
        while (arrowRenderers.Count < targetCount)
        {
            GameObject arrowObject = new GameObject("Arrow");
            arrowObject.transform.SetParent(lanesRoot);
            arrowObject.transform.localPosition = Vector3.zero;
            arrowObject.transform.localRotation = Quaternion.identity;

            SpriteRenderer renderer = arrowObject.AddComponent<SpriteRenderer>();
            arrowRenderers.Add(renderer);
        }

        for (int i = 0; i < arrowRenderers.Count; i++)
        {
            bool active = i < targetCount;
            if (arrowRenderers[i] != null)
                arrowRenderers[i].gameObject.SetActive(active);
        }
    }

    private void EnsureStopLineRenderers()
    {
        if (forwardStopLineRenderer == null)
        {
            Transform existing = transform.Find("StopLine_Forward");
            if (existing != null)
                forwardStopLineRenderer = existing.GetComponent<LineRenderer>();

            if (forwardStopLineRenderer == null)
            {
                GameObject go = new GameObject("StopLine_Forward");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                forwardStopLineRenderer = go.AddComponent<LineRenderer>();
            }
        }

        if (backwardStopLineRenderer == null)
        {
            Transform existing = transform.Find("StopLine_Backward");
            if (existing != null)
                backwardStopLineRenderer = existing.GetComponent<LineRenderer>();

            if (backwardStopLineRenderer == null)
            {
                GameObject go = new GameObject("StopLine_Backward");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                backwardStopLineRenderer = go.AddComponent<LineRenderer>();
            }
        }

        ConfigureStopLineRenderer(forwardStopLineRenderer);
        ConfigureStopLineRenderer(backwardStopLineRenderer);
    }

    private void ConfigureStopLineRenderer(LineRenderer renderer)
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

    private void EnsureRoadRenderer()
    {
        if (roadRenderer == null)
            roadRenderer = GetComponent<LineRenderer>();

        if (roadRenderer == null)
            roadRenderer = gameObject.AddComponent<LineRenderer>();

        if (cachedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            cachedMaterial = new Material(shader);
            cachedMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        roadRenderer.sharedMaterial = cachedMaterial;
        roadRenderer.useWorldSpace = true;
        roadRenderer.numCapVertices = 4;
        roadRenderer.numCornerVertices = 4;
        roadRenderer.textureMode = LineTextureMode.Stretch;
        roadRenderer.alignment = LineAlignment.TransformZ;
        roadRenderer.sortingOrder = 5;
    }

    private void EnsureLanesRoot()
    {
        if (lanesRoot != null)
            return;

        Transform existing = transform.Find("Lanes");
        if (existing != null)
        {
            lanesRoot = existing;
            return;
        }

        GameObject root = new GameObject("Lanes");
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        lanesRoot = root.transform;
    }

    private static Sprite GetArrowSprite()
    {
        if (cachedArrowSprite != null)
            return cachedArrowSprite;

        Texture2D texture = new Texture2D(32, 16, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color white = Color.white;

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 32; x++)
                texture.SetPixel(x, y, clear);
        }

        for (int x = 0; x < 22; x++)
        {
            for (int y = 6; y <= 9; y++)
                texture.SetPixel(x, y, white);
        }

        for (int x = 20; x < 32; x++)
        {
            int half = x - 20;
            int minY = Mathf.Max(0, 8 - half);
            int maxY = Mathf.Min(15, 8 + half);

            for (int y = minY; y <= maxY; y++)
                texture.SetPixel(x, y, white);
        }

        texture.Apply();

        cachedArrowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            32f
        );

        return cachedArrowSprite;
    }
}