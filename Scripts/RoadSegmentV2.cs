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
    [SerializeField] private bool isCurved = false;
    [SerializeField] private Vector3 curveControlPoint = Vector3.zero;
    [SerializeField] private int curveSampleCount = 16;

    [Header("Visuals")]
    [SerializeField] private Color roadColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color forwardLaneColor = new Color(0.75f, 0.9f, 1f, 1f);
    [SerializeField] private Color backwardLaneColor = new Color(1f, 0.85f, 0.75f, 1f);
    [SerializeField] private bool showLaneArrows = true;
    [SerializeField] private Color stopLineColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private float stopLineWidth = 0.12f;
    [SerializeField] private int stopLineSortingOrder = 20;

    [Header("Sidewalks")]
    [SerializeField] private bool generateSidewalks = true;
    [SerializeField] private bool leftSidewalk = true;
    [SerializeField] private bool rightSidewalk = true;
    [SerializeField] private float sidewalkWidth = 0.32f;
    [SerializeField] private float sidewalkOffset = 0f;
    [SerializeField] private Color sidewalkColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private int sidewalkSortingOrder = 4;

    [Header("Lane markings")]
    [SerializeField] private Color laneMarkingColor = new Color(0.85f, 0.92f, 1f, 0.95f);
    [SerializeField] private Color centerSeparatorColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private float laneMarkingWidth = 0.05f;
    [SerializeField] private float centerDoubleLineSpacing = 0.12f;
    [SerializeField] private float laneMarkingDashLength = 0.42f;
    [SerializeField] private float laneMarkingGapLength = 0.24f;
    [SerializeField] private int laneMarkingSortingOrder = 12;
    [SerializeField] private int centerSeparatorSortingOrder = 13;

    [Header("Arrow visuals")]
    [SerializeField] private float arrowLengthScale = 0.7f;
    [SerializeField] private float arrowHeightScale = 0.35f;
    [SerializeField] private int arrowSortingOrder = 50;
    [SerializeField] private float arrowZOffset = -0.1f;
    [SerializeField] private float arrowAngleOffset = 180f;
    [SerializeField] private float arrowPositionT = 0.78f;
    [SerializeField] private Color guidanceArrowColor = new Color(1f, 1f, 1f, 0.95f);

    [Header("Lane change")]
    [SerializeField] private bool allowLaneChanges = true;
    [SerializeField] private float noLaneChangeNearStart = 0.9f;
    [SerializeField] private float noLaneChangeNearEnd = 1.4f;
    [SerializeField] private float laneChangeStep = 1.6f;
    [SerializeField] private float laneChangeLength = 0.9f;

    private LineRenderer roadRenderer;
    private LineRenderer leftSidewalkRenderer;
    private LineRenderer rightSidewalkRenderer;
    private Material cachedMaterial;
    private LineRenderer forwardStopLineRenderer;
    private LineRenderer backwardStopLineRenderer;

    private Transform lanesRoot;
    private readonly List<RoadLaneV2> laneVisuals = new List<RoadLaneV2>();
    private readonly List<SpriteRenderer> arrowRenderers = new List<SpriteRenderer>();
    private readonly List<RoadLaneDataV2> laneData = new List<RoadLaneDataV2>();

    private static readonly Dictionary<int, Sprite> cachedArrowSprites = new Dictionary<int, Sprite>();

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

    public bool IsCurved => isCurved;
    public Vector3 CurveControlPoint => curveControlPoint;
    public bool GenerateSidewalks => generateSidewalks;
    public bool HasLeftSidewalk => generateSidewalks && leftSidewalk;
    public bool HasRightSidewalk => generateSidewalks && rightSidewalk;
    public float SidewalkWidth => sidewalkWidth;
    public float SidewalkOffset => sidewalkOffset;

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

    public void SetCurve(Vector3 controlPoint)
    {
        isCurved = true;
        curveControlPoint = new Vector3(controlPoint.x, controlPoint.y, 0f);
        RefreshVisual();
    }

    public void ClearCurve()
    {
        isCurved = false;
        curveControlPoint = Vector3.zero;
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

        List<Vector3> centerPolyline = BuildCenterPolyline();
        if (centerPolyline.Count < 2)
            return;

        float startRoadWidth = GetNodeVisualRoadWidth(startNode, TotalRoadWidth);
        float endRoadWidth = GetNodeVisualRoadWidth(endNode, TotalRoadWidth);

        roadRenderer.positionCount = centerPolyline.Count;
        roadRenderer.startWidth = startRoadWidth;
        roadRenderer.endWidth = endRoadWidth;
        roadRenderer.startColor = roadColor;
        roadRenderer.endColor = roadColor;

        for (int i = 0; i < centerPolyline.Count; i++)
            roadRenderer.SetPosition(i, centerPolyline[i]);

        RefreshSidewalkVisuals(centerPolyline);
        RebuildLaneVisualsAndData(centerPolyline);
    }

    public void RefreshDirectionArrows()
    {
        if (!showLaneArrows)
        {
            for (int i = 0; i < arrowRenderers.Count; i++)
            {
                if (arrowRenderers[i] != null)
                    arrowRenderers[i].enabled = false;
            }

            return;
        }

        int count = Mathf.Min(laneData.Count, arrowRenderers.Count);

        for (int i = 0; i < count; i++)
        {
            RoadLaneDataV2 lane = laneData[i];
            SpriteRenderer arrowRenderer = arrowRenderers[i];

            if (lane == null || arrowRenderer == null)
                continue;

            int movementMask = GetLaneMovementMask(lane);

            if (movementMask == 0)
            {
                arrowRenderer.enabled = false;
                continue;
            }

            UpdateArrowVisual(
                arrowRenderer,
                lane.start,
                lane.end,
                guidanceArrowColor,
                $"Arrow_{lane.direction}_{lane.localLaneIndex}",
                GetArrowSprite(movementMask)
            );
        }

        for (int i = count; i < arrowRenderers.Count; i++)
        {
            if (arrowRenderers[i] != null)
                arrowRenderers[i].enabled = false;
        }
    }

    private int GetLaneMovementMask(RoadLaneDataV2 lane)
    {
        if (lane == null)
            return 0;

        if (!HasRealIntersectionAtEitherEnd(lane))
            return 0;

        int mask = 0;

        for (int i = 0; i < lane.outgoingConnections.Count; i++)
        {
            RoadLaneConnectionV2 connection = lane.outgoingConnections[i];
            if (connection == null || !connection.IsValid)
                continue;

            if (connection.connectionKind != RoadLaneConnectionV2.ConnectionKind.Junction)
                continue;

            switch (connection.movementType)
            {
                case RoadLaneConnectionV2.MovementType.Straight:
                    mask |= 1;
                    break;

                case RoadLaneConnectionV2.MovementType.Left:
                    mask |= 2;
                    break;

                case RoadLaneConnectionV2.MovementType.Right:
                    mask |= 4;
                    break;
            }
        }

        return mask;
    }

    private bool HasRealIntersectionAtEitherEnd(RoadLaneDataV2 lane)
    {
        if (lane == null)
            return false;

        return IsRealIntersectionNode(lane.fromNode) || IsRealIntersectionNode(lane.toNode);
    }

    private bool IsRealIntersectionNode(RoadNodeV2 node)
    {
        return node != null && node.IsIntersection;
    }


    private void RebuildLaneVisualsAndData(List<Vector3> centerPolyline)
    {
        EnsureLaneObjectsCount(GetLaneMarkingCount());
        EnsureArrowObjectsCount(TotalLaneCount);
        EnsureLaneDataCount(TotalLaneCount);

        float segmentLength = GetPolylineLength(centerPolyline);
        if (segmentLength < 0.0001f)
            return;

        float startCut = GetNodeCutDistance(startNode, segmentLength);
        float endCut = GetNodeCutDistance(endNode, segmentLength);
        float startRoadWidth = GetNodeVisualRoadWidth(startNode, TotalRoadWidth);
        float endRoadWidth = GetNodeVisualRoadWidth(endNode, TotalRoadWidth);

        int laneCounter = 0;
        int globalLaneIdSeed = id * 100;

        float startRightEdgeCenter = -startRoadWidth * 0.5f + laneWidth * 0.5f;
        float endRightEdgeCenter = -endRoadWidth * 0.5f + laneWidth * 0.5f;
        float startLeftEdgeCenter = startRoadWidth * 0.5f - laneWidth * 0.5f;
        float endLeftEdgeCenter = endRoadWidth * 0.5f - laneWidth * 0.5f;

        for (int i = 0; i < forwardLanes; i++)
        {
            float startOffset = startRightEdgeCenter + i * laneWidth;
            float endOffset = endRightEdgeCenter + i * laneWidth;

            List<Vector3> lanePolyline = BuildTrimmedTaperedOffsetPolyline(centerPolyline, startOffset, endOffset, startCut, endCut);
            if (lanePolyline.Count < 2)
                continue;

            if (arrowRenderers[laneCounter] != null)
                arrowRenderers[laneCounter].enabled = false;

            RoadLaneDataV2 lane = laneData[laneCounter];
            lane.laneId = globalLaneIdSeed + laneCounter;
            lane.ownerSegment = this;
            lane.localLaneIndex = i;
            lane.direction = RoadLaneV2.LaneDirection.Forward;
            lane.start = lanePolyline[0];
            lane.end = lanePolyline[lanePolyline.Count - 1];
            lane.fromNode = startNode;
            lane.toNode = endNode;
            lane.sampledPoints.Clear();
            lane.sampledPoints.AddRange(lanePolyline);
            lane.outgoingConnections.Clear();
            lane.incomingConnections.Clear();

            laneCounter++;
        }

        for (int i = 0; i < backwardLanes; i++)
        {
            float startOffset = startLeftEdgeCenter - i * laneWidth;
            float endOffset = endLeftEdgeCenter - i * laneWidth;

            List<Vector3> lanePolyline = BuildTrimmedTaperedOffsetPolyline(centerPolyline, startOffset, endOffset, startCut, endCut);
            if (lanePolyline.Count < 2)
                continue;

            if (arrowRenderers[laneCounter] != null)
                arrowRenderers[laneCounter].enabled = false;

            RoadLaneDataV2 lane = laneData[laneCounter];
            lane.laneId = globalLaneIdSeed + laneCounter;
            lane.ownerSegment = this;
            lane.localLaneIndex = i;
            lane.direction = RoadLaneV2.LaneDirection.Backward;
            lane.start = lanePolyline[lanePolyline.Count - 1];
            lane.end = lanePolyline[0];
            lane.fromNode = endNode;
            lane.toNode = startNode;
            lane.sampledPoints.Clear();

            for (int j = lanePolyline.Count - 1; j >= 0; j--)
                lane.sampledPoints.Add(lanePolyline[j]);

            lane.outgoingConnections.Clear();
            lane.incomingConnections.Clear();

            laneCounter++;
        }

        UpdateLaneMarkings(centerPolyline, startCut, endCut, startRoadWidth, endRoadWidth);
        UpdateStopLineVisuals(centerPolyline, startCut, endCut);
    }

    private int GetLaneMarkingCount()
    {
        int count = 0;

        if (forwardLanes > 1)
            count += forwardLanes - 1;

        if (forwardLanes > 0 && backwardLanes > 0)
            count += UsesDoubleCenterSeparator() ? 2 : 1;

        if (backwardLanes > 1)
            count += backwardLanes - 1;

        return Mathf.Max(0, count);
    }

    private void UpdateLaneMarkings(
    List<Vector3> centerPolyline,
    float startCut,
    float endCut,
    float startRoadWidth,
    float endRoadWidth)
    {
        int markingIndex = 0;

        float markingStartCut = GetLaneMarkingCutDistance(startNode, startCut);
        float markingEndCut = GetLaneMarkingCutDistance(endNode, endCut);

        float startRestricted = Mathf.Max(0f, noLaneChangeNearStart);
        float endRestricted = Mathf.Max(0f, noLaneChangeNearEnd);

        for (int i = 0; i < forwardLanes - 1; i++)
        {
            float startOffset = -startRoadWidth * 0.5f + (i + 1f) * laneWidth;
            float endOffset = -endRoadWidth * 0.5f + (i + 1f) * laneWidth;

            List<Vector3> markingPolyline = BuildTrimmedTaperedOffsetPolyline(
                centerPolyline,
                startOffset,
                endOffset,
                markingStartCut,
                markingEndCut
            );

            RoadLaneV2 marking = laneVisuals[markingIndex];
            marking.Initialize(i, RoadLaneV2.LaneDirection.Forward);
            marking.UpdateRestrictedDashedPolylineVisual(
                markingPolyline,
                laneMarkingWidth,
                laneMarkingColor,
                laneMarkingSortingOrder,
                startRestricted,
                endRestricted,
                laneMarkingDashLength,
                laneMarkingGapLength
            );

            markingIndex++;
        }

        if (forwardLanes > 0 && backwardLanes > 0)
        {
            float baseStartCenterOffset = -startRoadWidth * 0.5f + forwardLanes * laneWidth;
            float baseEndCenterOffset = -endRoadWidth * 0.5f + forwardLanes * laneWidth;

            if (UsesDoubleCenterSeparator())
            {
                float halfSpacing = Mathf.Max(laneMarkingWidth * 1.5f, centerDoubleLineSpacing * 0.5f);

                List<Vector3> firstCenterPolyline = BuildTrimmedTaperedOffsetPolyline(
                    centerPolyline,
                    baseStartCenterOffset - halfSpacing,
                    baseEndCenterOffset - halfSpacing,
                    markingStartCut,
                    markingEndCut
                );

                RoadLaneV2 firstMarking = laneVisuals[markingIndex];
                firstMarking.Initialize(0, RoadLaneV2.LaneDirection.Forward);
                firstMarking.UpdatePolylineVisual(
                    firstCenterPolyline,
                    laneMarkingWidth,
                    centerSeparatorColor,
                    centerSeparatorSortingOrder
                );

                markingIndex++;

                List<Vector3> secondCenterPolyline = BuildTrimmedTaperedOffsetPolyline(
                    centerPolyline,
                    baseStartCenterOffset + halfSpacing,
                    baseEndCenterOffset + halfSpacing,
                    markingStartCut,
                    markingEndCut
                );

                RoadLaneV2 secondMarking = laneVisuals[markingIndex];
                secondMarking.Initialize(1, RoadLaneV2.LaneDirection.Forward);
                secondMarking.UpdatePolylineVisual(
                    secondCenterPolyline,
                    laneMarkingWidth,
                    centerSeparatorColor,
                    centerSeparatorSortingOrder
                );

                markingIndex++;
            }
            else
            {
                List<Vector3> centerPolylineMarking = BuildTrimmedTaperedOffsetPolyline(
                    centerPolyline,
                    baseStartCenterOffset,
                    baseEndCenterOffset,
                    markingStartCut,
                    markingEndCut
                );

                RoadLaneV2 marking = laneVisuals[markingIndex];
                marking.Initialize(0, RoadLaneV2.LaneDirection.Forward);
                marking.UpdatePolylineVisual(
                    centerPolylineMarking,
                    laneMarkingWidth,
                    centerSeparatorColor,
                    centerSeparatorSortingOrder
                );

                markingIndex++;
            }
        }

        for (int i = 0; i < backwardLanes - 1; i++)
        {
            float startOffset = startRoadWidth * 0.5f - (i + 1f) * laneWidth;
            float endOffset = endRoadWidth * 0.5f - (i + 1f) * laneWidth;

            List<Vector3> markingPolyline = BuildTrimmedTaperedOffsetPolyline(
                centerPolyline,
                startOffset,
                endOffset,
                markingStartCut,
                markingEndCut
            );

            RoadLaneV2 marking = laneVisuals[markingIndex];
            marking.Initialize(i, RoadLaneV2.LaneDirection.Backward);
            marking.UpdateRestrictedDashedPolylineVisual(
                markingPolyline,
                laneMarkingWidth,
                laneMarkingColor,
                laneMarkingSortingOrder,
                startRestricted,
                endRestricted,
                laneMarkingDashLength,
                laneMarkingGapLength
            );

            markingIndex++;
        }

        for (int i = markingIndex; i < laneVisuals.Count; i++)
        {
            if (laneVisuals[i] != null)
                laneVisuals[i].Hide();
        }
    }

    private float GetLaneMarkingCutDistance(RoadNodeV2 node, float baseCut)
    {
        float cut = Mathf.Max(0f, baseCut);

        if (node == null || !node.IsIntersection)
            return cut;

        float extraIntersectionClearance = stopLineOffset + Mathf.Max(laneWidth * 0.75f, 0.35f);
        return Mathf.Max(cut, extraIntersectionClearance);
    }

    private bool UsesDoubleCenterSeparator()
    {
        return forwardLanes >= 2 && backwardLanes >= 2;
    }

    private void UpdateStopLineVisuals(
    List<Vector3> centerPolyline,
    float startCut,
    float endCut)
    {
        EnsureStopLineRenderers();

        bool showForward = forwardLanes > 0 && endNode != null && endNode.ConnectedSegments.Count > 2;
        bool showBackward = backwardLanes > 0 && startNode != null && startNode.ConnectedSegments.Count > 2;

        List<RoadLaneDataV2> forwardDriving = GetDrivingLanes(startNode, endNode);
        List<RoadLaneDataV2> backwardDriving = GetDrivingLanes(endNode, startNode);

        Vector3 endTangent = GetPolylineDirectionAtEnd(centerPolyline);
        Vector3 startTangent = GetPolylineDirectionAtStart(centerPolyline);

        Vector3 endNormal = new Vector3(-endTangent.y, endTangent.x, 0f);
        Vector3 startNormal = new Vector3(-startTangent.y, startTangent.x, 0f);

        UpdateStopLineRendererFromLanes(
            forwardStopLineRenderer,
            showForward,
            "StopLine_Forward",
            forwardDriving,
            endNormal,
            forwardLanes * laneWidth
        );

        UpdateStopLineRendererFromLanes(
            backwardStopLineRenderer,
            showBackward,
            "StopLine_Backward",
            backwardDriving,
            startNormal,
            backwardLanes * laneWidth
        );
    }

    private void UpdateStopLineRendererFromLanes(
    LineRenderer renderer,
    bool visible,
    string objectName,
    List<RoadLaneDataV2> lanes,
    Vector3 normal,
    float carriageWidth)
    {
        if (renderer == null)
            return;

        renderer.gameObject.name = objectName;
        renderer.enabled = visible;

        if (!visible || lanes == null || lanes.Count == 0 || carriageWidth <= 0.01f)
            return;

        Vector3 center = Vector3.zero;
        int count = 0;

        for (int i = 0; i < lanes.Count; i++)
        {
            RoadLaneDataV2 lane = lanes[i];
            if (lane == null)
                continue;

            center += lane.end;
            count++;
        }

        if (count == 0)
            return;

        center /= count;

        Vector3 halfSpan = normal.normalized * (carriageWidth * 0.5f);

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
        if (node == null)
            return 0f;

        if (node.ConnectedSegments == null || node.ConnectedSegments.Count <= 2)
            return 0f;

        float cut = junctionInset;
        float maxOtherHalfWidth = 0f;

        for (int i = 0; i < node.ConnectedSegments.Count; i++)
        {
            RoadSegmentV2 other = node.ConnectedSegments[i];

            if (other == null || other == this)
                continue;

            maxOtherHalfWidth = Mathf.Max(maxOtherHalfWidth, other.TotalRoadWidth * 0.5f);

            Vector3 thisDir = GetDirectionAwayFromNode(node);
            Vector3 otherDir = other.GetDirectionAwayFromNode(node);

            if (thisDir.sqrMagnitude < 0.0001f || otherDir.sqrMagnitude < 0.0001f)
                continue;

            float angle = Vector3.Angle(thisDir, otherDir);
            if (angle > 90f)
                angle = 180f - angle;

            if (angle < 8f)
                continue;

            float sinAngle = Mathf.Sin(angle * Mathf.Deg2Rad);
            if (sinAngle < 0.0001f)
                continue;

            float thisHalfWidth = TotalRoadWidth * 0.5f;
            float otherHalfWidth = other.TotalRoadWidth * 0.5f;
            float acuteAngleCut = (thisHalfWidth + otherHalfWidth) / sinAngle;
            cut = Mathf.Max(cut, acuteAngleCut + laneWidth * 0.25f);
        }

        if (maxOtherHalfWidth > 0f)
            cut = Mathf.Max(cut, maxOtherHalfWidth + laneWidth * 0.25f);

        float maxAllowedCut = Mathf.Max(0f, segmentLength - Mathf.Max(laneWidth * 0.75f, 0.2f));
        return Mathf.Clamp(cut, 0f, maxAllowedCut);
    }

    private Vector3 GetDirectionAwayFromNode(RoadNodeV2 node)
    {
        if (node == null)
            return Vector3.zero;

        List<Vector3> centerPolyline = BuildCenterPolyline();
        if (centerPolyline == null || centerPolyline.Count < 2)
            return Vector3.zero;

        Vector3 dir;

        if (node == startNode)
            dir = centerPolyline[1] - centerPolyline[0];
        else if (node == endNode)
            dir = centerPolyline[centerPolyline.Count - 2] - centerPolyline[centerPolyline.Count - 1];
        else
            return Vector3.zero;

        dir.z = 0f;
        return dir.normalized;
    }

    public List<Vector3> GetCenterPolylineWorld()
    {
        return BuildCenterPolyline();
    }

    public List<Vector3> GetLeftSidewalkPolylineWorld()
    {
        if (!HasLeftSidewalk)
            return new List<Vector3>();

        List<Vector3> centerPolyline = BuildCenterPolyline();
        float startRoadWidth = GetNodeVisualRoadWidth(startNode, TotalRoadWidth);
        float endRoadWidth = GetNodeVisualRoadWidth(endNode, TotalRoadWidth);
        float segmentLength = GetPolylineLength(centerPolyline);
        float startCut = GetNodeCutDistance(startNode, segmentLength);
        float endCut = GetNodeCutDistance(endNode, segmentLength);
        float startOffset = startRoadWidth * 0.5f + sidewalkWidth * 0.5f;
        float endOffset = endRoadWidth * 0.5f + sidewalkWidth * 0.5f;
        List<Vector3> sidewalkPolyline = BuildTaperedOffsetPolyline(centerPolyline, startOffset, endOffset);
        ExtendPolylineForSimpleJoints(sidewalkPolyline, startNode, endNode, true);
        return TrimPolyline(sidewalkPolyline, startCut, endCut);
    }

    public List<Vector3> GetRightSidewalkPolylineWorld()
    {
        if (!HasRightSidewalk)
            return new List<Vector3>();

        List<Vector3> centerPolyline = BuildCenterPolyline();
        float startRoadWidth = GetNodeVisualRoadWidth(startNode, TotalRoadWidth);
        float endRoadWidth = GetNodeVisualRoadWidth(endNode, TotalRoadWidth);
        float segmentLength = GetPolylineLength(centerPolyline);
        float startCut = GetNodeCutDistance(startNode, segmentLength);
        float endCut = GetNodeCutDistance(endNode, segmentLength);
        float startOffset = -(startRoadWidth * 0.5f + sidewalkWidth * 0.5f);
        float endOffset = -(endRoadWidth * 0.5f + sidewalkWidth * 0.5f);
        List<Vector3> sidewalkPolyline = BuildTaperedOffsetPolyline(centerPolyline, startOffset, endOffset);
        ExtendPolylineForSimpleJoints(sidewalkPolyline, startNode, endNode, false);
        return TrimPolyline(sidewalkPolyline, startCut, endCut);
    }

    private List<Vector3> BuildCenterPolyline()
    {
        List<Vector3> points = new List<Vector3>();

        if (startNode == null || endNode == null)
            return points;

        Vector3 start = startNode.transform.position;
        Vector3 end = endNode.transform.position;

        if (!isCurved)
        {
            points.Add(start);
            points.Add(end);
            return points;
        }

        int samples = Mathf.Clamp(curveSampleCount, 4, 32);

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            points.Add(EvaluateQuadraticBezier(start, curveControlPoint, end, t));
        }

        return points;
    }

    private Vector3 EvaluateQuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private float GetNodeVisualRoadWidth(RoadNodeV2 node, float fallbackWidth)
    {
        if (node == null)
            return fallbackWidth;

        if (node.ConnectedSegments == null || node.ConnectedSegments.Count != 2)
            return Mathf.Max(0.01f, fallbackWidth);

        float bestWidth = fallbackWidth;

        for (int i = 0; i < node.ConnectedSegments.Count; i++)
        {
            RoadSegmentV2 segment = node.ConnectedSegments[i];
            if (segment == null)
                continue;

            bestWidth = Mathf.Max(bestWidth, segment.TotalRoadWidth);
        }

        return Mathf.Max(0.01f, bestWidth);
    }

    private float GetPolylineLength(List<Vector3> polyline)
    {
        float length = 0f;

        if (polyline == null)
            return length;

        for (int i = 0; i < polyline.Count - 1; i++)
            length += Vector3.Distance(polyline[i], polyline[i + 1]);

        return length;
    }

    private List<Vector3> BuildOffsetPolyline(List<Vector3> centerPolyline, float offset)
    {
        List<Vector3> result = new List<Vector3>();

        if (centerPolyline == null || centerPolyline.Count < 2)
            return result;

        for (int i = 0; i < centerPolyline.Count; i++)
        {
            Vector3 tangent;

            if (i == 0)
                tangent = (centerPolyline[1] - centerPolyline[0]).normalized;
            else if (i == centerPolyline.Count - 1)
                tangent = (centerPolyline[i] - centerPolyline[i - 1]).normalized;
            else
                tangent = (centerPolyline[i + 1] - centerPolyline[i - 1]).normalized;

            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.right;

            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);
            result.Add(centerPolyline[i] + normal * offset);
        }

        return result;
    }

    private List<Vector3> TrimPolyline(List<Vector3> points, float startTrim, float endTrim)
    {
        List<Vector3> result = new List<Vector3>();

        if (points == null || points.Count < 2)
            return result;

        float totalLength = GetPolylineLength(points);
        float from = Mathf.Clamp(startTrim, 0f, totalLength);
        float to = Mathf.Clamp(totalLength - endTrim, 0f, totalLength);

        if (to <= from + 0.001f)
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

            if (segEnd < from)
            {
                accumulated = segEnd;
                continue;
            }

            if (segStart > to)
                break;

            float localFrom = Mathf.Clamp(from - segStart, 0f, segLength);
            float localTo = Mathf.Clamp(to - segStart, 0f, segLength);

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

    private List<Vector3> BuildTrimmedOffsetPolyline(
        List<Vector3> centerPolyline,
        float offset,
        float startCut,
        float endCut)
    {
        List<Vector3> offsetPolyline = BuildOffsetPolyline(centerPolyline, offset);
        return TrimPolyline(offsetPolyline, startCut, endCut);
    }

    private List<Vector3> BuildTaperedOffsetPolyline(
        List<Vector3> centerPolyline,
        float startOffset,
        float endOffset)
    {
        List<Vector3> result = new List<Vector3>();

        if (centerPolyline == null || centerPolyline.Count < 2)
            return result;

        float totalLength = GetPolylineLength(centerPolyline);
        if (totalLength < 0.0001f)
            return result;

        float accumulated = 0f;

        for (int i = 0; i < centerPolyline.Count; i++)
        {
            Vector3 tangent;

            if (i == 0)
                tangent = (centerPolyline[1] - centerPolyline[0]).normalized;
            else if (i == centerPolyline.Count - 1)
                tangent = (centerPolyline[i] - centerPolyline[i - 1]).normalized;
            else
                tangent = (centerPolyline[i + 1] - centerPolyline[i - 1]).normalized;

            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.right;

            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);
            float t = totalLength > 0.0001f
                ? (i == centerPolyline.Count - 1 ? 1f : Mathf.Clamp01(accumulated / totalLength))
                : 0f;
            float offset = Mathf.Lerp(startOffset, endOffset, t);
            result.Add(centerPolyline[i] + normal * offset);

            if (i < centerPolyline.Count - 1)
                accumulated += Vector3.Distance(centerPolyline[i], centerPolyline[i + 1]);
        }

        return result;
    }

    private List<Vector3> BuildTrimmedTaperedOffsetPolyline(
        List<Vector3> centerPolyline,
        float startOffset,
        float endOffset,
        float startCut,
        float endCut)
    {
        List<Vector3> offsetPolyline = BuildTaperedOffsetPolyline(centerPolyline, startOffset, endOffset);
        return TrimPolyline(offsetPolyline, startCut, endCut);
    }

    private void ExtendPolylineForSimpleJoints(
        List<Vector3> polyline,
        RoadNodeV2 polylineStartNode,
        RoadNodeV2 polylineEndNode,
        bool isLeftSidewalk)
    {
        if (polyline == null || polyline.Count < 2)
            return;

        float overlap = Mathf.Max(0.05f, sidewalkWidth * 0.25f);

        if (IsSimpleJointNode(polylineStartNode))
        {
            if (!TrySnapSidewalkJointPoint(polylineStartNode, isLeftSidewalk, out Vector3 snappedPoint))
            {
                Vector3 dir = GetPolylineDirectionAtStart(polyline);
                polyline[0] -= dir * overlap;
            }
            else
            {
                polyline[0] = snappedPoint;
            }
        }

        if (IsSimpleJointNode(polylineEndNode))
        {
            if (!TrySnapSidewalkJointPoint(polylineEndNode, isLeftSidewalk, out Vector3 snappedPoint))
            {
                Vector3 dir = GetPolylineDirectionAtEnd(polyline);
                polyline[polyline.Count - 1] += dir * overlap;
            }
            else
            {
                polyline[polyline.Count - 1] = snappedPoint;
            }
        }
    }

    private bool TryApplySimpleJointSidewalkConnection(
        RoadNodeV2 jointNode,
        bool isLeftSidewalk,
        bool atStart,
        out List<Vector3> roundedPoints,
        out Vector3 snappedPoint)
    {
        roundedPoints = null;
        snappedPoint = Vector3.zero;

        if (!TryGetSidewalkLineAtNode(jointNode, isLeftSidewalk, out Vector3 thisPoint, out Vector3 thisDirection))
            return false;

        if (IsInnerSidewalkAtSimpleJoint(jointNode, thisPoint))
            return TrySnapSidewalkJointPoint(jointNode, isLeftSidewalk, out snappedPoint);

        return TryBuildRoundedSidewalkJoint(jointNode, isLeftSidewalk, atStart, out roundedPoints);
    }

    private bool TryBuildRoundedSidewalkJoint(
        RoadNodeV2 jointNode,
        bool isLeftSidewalk,
        bool atStart,
        out List<Vector3> roundedPoints)
    {
        roundedPoints = new List<Vector3>();

        if (!IsSimpleJointNode(jointNode))
            return false;

        RoadSegmentV2 otherSegment = null;

        for (int i = 0; i < jointNode.ConnectedSegments.Count; i++)
        {
            RoadSegmentV2 candidate = jointNode.ConnectedSegments[i];
            if (candidate != null && candidate != this)
            {
                otherSegment = candidate;
                break;
            }
        }

        if (otherSegment == null)
            return false;

        if (!TryGetSidewalkLineAtNode(jointNode, isLeftSidewalk, out Vector3 thisPoint, out Vector3 thisDirection))
            return false;

        if (!otherSegment.TryGetMatchingSidewalkLineAtNode(
            jointNode,
            thisPoint,
            thisDirection,
            preferOuterJoint: true,
            out Vector3 otherPoint,
            out Vector3 otherDirection))
            return false;

        float thisRadius = Vector3.Distance(jointNode.transform.position, thisPoint);
        float otherRadius = Vector3.Distance(jointNode.transform.position, otherPoint);
        float radius = Mathf.Max(thisRadius, otherRadius);
        if (radius <= 0.0001f)
            return false;

        bool hasIntersection = TryGetLineIntersection(thisPoint, thisDirection, otherPoint, otherDirection, out Vector3 intersection);
        if (hasIntersection)
        {
            float intersectionRadius = Vector3.Distance(jointNode.transform.position, intersection);
            if (intersectionRadius <= radius + sidewalkWidth * 0.35f)
                return false;
        }

        Vector3 fromVector = (thisPoint - jointNode.transform.position).normalized;
        Vector3 toVector = (otherPoint - jointNode.transform.position).normalized;

        if (fromVector.sqrMagnitude < 0.0001f || toVector.sqrMagnitude < 0.0001f)
            return false;

        float fromAngle = Mathf.Atan2(fromVector.y, fromVector.x) * Mathf.Rad2Deg;
        float toAngle = Mathf.Atan2(toVector.y, toVector.x) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(fromAngle, toAngle);

        if (Mathf.Abs(delta) < 5f)
            return false;

        float midAngle = fromAngle + delta * 0.5f;

        int arcSteps = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(delta) / 12f), 2, 5);

        if (atStart)
            AddArcPoints(roundedPoints, jointNode.transform.position, radius, midAngle, fromAngle, arcSteps);
        else
            AddArcPoints(roundedPoints, jointNode.transform.position, radius, fromAngle, midAngle, arcSteps);

        return roundedPoints.Count >= 2;
    }

    private bool TrySnapSidewalkJointPoint(
        RoadNodeV2 jointNode,
        bool isLeftSidewalk,
        out Vector3 snappedPoint)
    {
        snappedPoint = Vector3.zero;

        if (!IsSimpleJointNode(jointNode))
            return false;

        RoadSegmentV2 otherSegment = null;

        for (int i = 0; i < jointNode.ConnectedSegments.Count; i++)
        {
            RoadSegmentV2 candidate = jointNode.ConnectedSegments[i];
            if (candidate != null && candidate != this)
            {
                otherSegment = candidate;
                break;
            }
        }

        if (otherSegment == null)
            return false;

        if (!TryGetSidewalkLineAtNode(jointNode, isLeftSidewalk, out Vector3 thisPoint, out Vector3 thisDirection))
            return false;

        if (!otherSegment.TryGetMatchingSidewalkLineAtNode(
            jointNode,
            thisPoint,
            thisDirection,
            preferOuterJoint: false,
            out Vector3 otherPoint,
            out Vector3 otherDirection))
            return false;

        return TryGetSimpleJointSharedPoint(
            jointNode,
            thisPoint,
            thisDirection,
            otherPoint,
            otherDirection,
            out snappedPoint);
    }

    private bool TryGetMatchingSidewalkLineAtNode(
        RoadNodeV2 jointNode,
        Vector3 preferredPoint,
        Vector3 preferredDirection,
        bool preferOuterJoint,
        out Vector3 point,
        out Vector3 direction)
    {
        point = Vector3.zero;
        direction = Vector3.zero;

        bool hasLeft = TryGetSidewalkLineAtNode(jointNode, true, out Vector3 leftPoint, out Vector3 leftDir);
        bool hasRight = TryGetSidewalkLineAtNode(jointNode, false, out Vector3 rightPoint, out Vector3 rightDir);

        if (!hasLeft && !hasRight)
            return false;

        if (hasLeft && !hasRight)
        {
            point = leftPoint;
            direction = leftDir;
            return true;
        }

        if (!hasLeft && hasRight)
        {
            point = rightPoint;
            direction = rightDir;
            return true;
        }

        bool preferredIsInner = IsPointInsideSimpleJointWedge(jointNode, preferredPoint);
        bool leftIsInner = IsPointInsideSimpleJointWedge(jointNode, leftPoint);
        bool rightIsInner = IsPointInsideSimpleJointWedge(jointNode, rightPoint);

        if (leftIsInner != rightIsInner)
        {
            bool needInner = preferredIsInner;

            if (leftIsInner == needInner)
            {
                point = leftPoint;
                direction = leftDir;
                return true;
            }

            point = rightPoint;
            direction = rightDir;
            return true;
        }

        float leftScore = EvaluateSidewalkJointCandidate(
            jointNode,
            preferredPoint,
            preferredDirection,
            leftPoint,
            leftDir,
            preferOuterJoint);

        float rightScore = EvaluateSidewalkJointCandidate(
            jointNode,
            preferredPoint,
            preferredDirection,
            rightPoint,
            rightDir,
            preferOuterJoint);

        if (preferOuterJoint)
        {
            if (leftScore >= rightScore)
            {
                point = leftPoint;
                direction = leftDir;
            }
            else
            {
                point = rightPoint;
                direction = rightDir;
            }
        }
        else
        {
            if (leftScore <= rightScore)
            {
                point = leftPoint;
                direction = leftDir;
            }
            else
            {
                point = rightPoint;
                direction = rightDir;
            }
        }

        return true;
    }

    private bool TryGetSimpleJointSharedPoint(
        RoadNodeV2 jointNode,
        Vector3 thisPoint,
        Vector3 thisDirection,
        Vector3 otherPoint,
        Vector3 otherDirection,
        out Vector3 sharedPoint)
    {
        sharedPoint = Vector3.zero;

        if (jointNode == null)
            return false;

        bool isInnerSide = IsPointInsideSimpleJointWedge(jointNode, thisPoint);

        float thisRadius = Vector3.Distance(jointNode.transform.position, thisPoint);
        float otherRadius = Vector3.Distance(jointNode.transform.position, otherPoint);
        float maxExpectedRadius = Mathf.Max(thisRadius, otherRadius) + sidewalkWidth * 0.75f;

        if (TryGetLineIntersection(thisPoint, thisDirection, otherPoint, otherDirection, out Vector3 intersection))
        {
            float intersectionRadius = Vector3.Distance(jointNode.transform.position, intersection);
            if (intersectionRadius <= maxExpectedRadius)
            {
                sharedPoint = intersection;
                sharedPoint.z = 0f;
                return true;
            }
        }

        if (isInnerSide)
            return false;

        sharedPoint = (thisPoint + otherPoint) * 0.5f;
        sharedPoint.z = 0f;
        return true;
    }

    private float EvaluateSidewalkJointCandidate(
        RoadNodeV2 jointNode,
        Vector3 preferredPoint,
        Vector3 preferredDirection,
        Vector3 candidatePoint,
        Vector3 candidateDirection,
        bool preferOuterJoint)
    {
        if (jointNode == null)
            return preferOuterJoint ? float.MinValue : float.MaxValue;

        float preferredAngle = GetAngleAroundNode(jointNode, preferredPoint);
        float candidateAngle = GetAngleAroundNode(jointNode, candidatePoint);
        float angleDelta = Mathf.Abs(Mathf.DeltaAngle(preferredAngle, candidateAngle));
        float pointDistance = Vector3.Distance(preferredPoint, candidatePoint);

        float directionPenalty = 0f;
        if (preferredDirection.sqrMagnitude > 0.0001f && candidateDirection.sqrMagnitude > 0.0001f)
        {
            float dirDot = Vector3.Dot(preferredDirection.normalized, candidateDirection.normalized);
            directionPenalty = (1f - Mathf.Clamp(dirDot, -1f, 1f)) * 10f;
        }

        return angleDelta + pointDistance * 5f + directionPenalty;
    }

    private float GetAngleAroundNode(RoadNodeV2 jointNode, Vector3 sidewalkPoint)
    {
        if (jointNode == null)
            return 0f;

        Vector3 toSidewalk = sidewalkPoint - jointNode.transform.position;
        toSidewalk.z = 0f;

        if (toSidewalk.sqrMagnitude < 0.0001f)
            return 0f;

        return Mathf.Atan2(toSidewalk.y, toSidewalk.x) * Mathf.Rad2Deg;
    }

    private bool IsPointInsideSimpleJointWedge(RoadNodeV2 jointNode, Vector3 point)
    {
        if (!IsSimpleJointNode(jointNode))
            return false;

        RoadSegmentV2 first = null;
        RoadSegmentV2 second = null;

        for (int i = 0; i < jointNode.ConnectedSegments.Count; i++)
        {
            RoadSegmentV2 candidate = jointNode.ConnectedSegments[i];
            if (candidate == null)
                continue;

            if (first == null)
                first = candidate;
            else if (second == null && candidate != first)
                second = candidate;
        }

        if (first == null || second == null)
            return false;

        if (!TryGetRoadDirectionAwayFromNode(first, jointNode, out Vector3 firstDir))
            return false;

        if (!TryGetRoadDirectionAwayFromNode(second, jointNode, out Vector3 secondDir))
            return false;

        float angleA = Mathf.Atan2(firstDir.y, firstDir.x) * Mathf.Rad2Deg;
        float angleB = Mathf.Atan2(secondDir.y, secondDir.x) * Mathf.Rad2Deg;
        float pointAngle = GetAngleAroundNode(jointNode, point);
        float deltaAB = Mathf.DeltaAngle(angleA, angleB);
        float deltaAP = Mathf.DeltaAngle(angleA, pointAngle);

        if (Mathf.Abs(deltaAB) < 1f)
            return false;

        if (deltaAB > 0f)
            return deltaAP > 0f && deltaAP < deltaAB;

        return deltaAP < 0f && deltaAP > deltaAB;
    }

    private bool IsInnerSidewalkAtSimpleJoint(RoadNodeV2 jointNode, Vector3 sidewalkPoint)
    {
        return IsPointInsideSimpleJointWedge(jointNode, sidewalkPoint);
    }

    private bool TryGetRoadDirectionAwayFromNode(RoadSegmentV2 segment, RoadNodeV2 node, out Vector3 direction)
    {
        direction = Vector3.zero;

        if (segment == null || node == null)
            return false;

        List<Vector3> centerPolyline = segment.BuildCenterPolyline();
        if (centerPolyline == null || centerPolyline.Count < 2)
            return false;

        if (segment.StartNode == node)
            direction = (centerPolyline[1] - centerPolyline[0]).normalized;
        else if (segment.EndNode == node)
            direction = (centerPolyline[centerPolyline.Count - 2] - centerPolyline[centerPolyline.Count - 1]).normalized * -1f;

        direction.z = 0f;
        return direction.sqrMagnitude > 0.0001f;
    }

    private bool TryGetSidewalkLineAtNode(
        RoadNodeV2 jointNode,
        bool isLeftSidewalk,
        out Vector3 point,
        out Vector3 direction)
    {
        point = Vector3.zero;
        direction = Vector3.zero;

        List<Vector3> polyline = isLeftSidewalk
            ? BuildSidewalkPolylineForSide(true)
            : BuildSidewalkPolylineForSide(false);

        if (polyline == null || polyline.Count < 2)
            return false;

        if (jointNode == startNode)
        {
            point = polyline[0];
            direction = -(polyline[1] - polyline[0]).normalized;
            return direction.sqrMagnitude > 0.0001f;
        }

        if (jointNode == endNode)
        {
            point = polyline[polyline.Count - 1];
            direction = (polyline[polyline.Count - 1] - polyline[polyline.Count - 2]).normalized;
            return direction.sqrMagnitude > 0.0001f;
        }

        return false;
    }

    private List<Vector3> BuildSidewalkPolylineForSide(bool isLeftSidewalk)
    {
        if (isLeftSidewalk && !HasLeftSidewalk)
            return new List<Vector3>();

        if (!isLeftSidewalk && !HasRightSidewalk)
            return new List<Vector3>();

        List<Vector3> centerPolyline = BuildCenterPolyline();
        float startRoadWidth = GetNodeVisualRoadWidth(startNode, TotalRoadWidth);
        float endRoadWidth = GetNodeVisualRoadWidth(endNode, TotalRoadWidth);

        float startOffset = isLeftSidewalk
            ? startRoadWidth * 0.5f + sidewalkWidth * 0.5f
            : -(startRoadWidth * 0.5f + sidewalkWidth * 0.5f);

        float endOffset = isLeftSidewalk
            ? endRoadWidth * 0.5f + sidewalkWidth * 0.5f
            : -(endRoadWidth * 0.5f + sidewalkWidth * 0.5f);

        return BuildTaperedOffsetPolyline(centerPolyline, startOffset, endOffset);
    }

    private bool TryGetLineIntersection(
        Vector3 pointA,
        Vector3 dirA,
        Vector3 pointB,
        Vector3 dirB,
        out Vector3 intersection)
    {
        intersection = Vector3.zero;

        Vector2 p = new Vector2(pointA.x, pointA.y);
        Vector2 r = new Vector2(dirA.x, dirA.y);
        Vector2 q = new Vector2(pointB.x, pointB.y);
        Vector2 s = new Vector2(dirB.x, dirB.y);

        float cross = r.x * s.y - r.y * s.x;
        if (Mathf.Abs(cross) <= 0.0001f)
            return false;

        Vector2 qp = q - p;
        float t = (qp.x * s.y - qp.y * s.x) / cross;
        Vector2 hit = p + r * t;

        intersection = new Vector3(hit.x, hit.y, 0f);
        return true;
    }

    private void AddArcPoints(
        List<Vector3> points,
        Vector3 center,
        float radius,
        float fromAngle,
        float toAngle,
        int steps)
    {
        if (points == null)
            return;

        float delta = Mathf.DeltaAngle(fromAngle, toAngle);
        int count = Mathf.Max(1, steps);

        for (int i = 0; i <= count; i++)
        {
            float t = i / (float)count;
            float angle = fromAngle + delta * t;
            Vector3 point = center + AngleToDirection(angle) * radius;
            point.z = 0f;

            if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], point) > 0.01f)
                points.Add(point);
        }
    }

    private Vector3 AngleToDirection(float angleDegrees)
    {
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians), 0f);
    }

    private bool IsSimpleJointNode(RoadNodeV2 node)
    {
        return node != null && node.ConnectedSegments != null && node.ConnectedSegments.Count == 2;
    }

    private Vector3 GetPolylineDirectionAtStart(List<Vector3> polyline)
    {
        if (polyline == null || polyline.Count < 2)
            return Vector3.right;

        Vector3 dir = (polyline[1] - polyline[0]).normalized;
        return dir.sqrMagnitude < 0.0001f ? Vector3.right : dir;
    }

    private Vector3 GetPolylineDirectionAtEnd(List<Vector3> polyline)
    {
        if (polyline == null || polyline.Count < 2)
            return Vector3.right;

        Vector3 dir = (polyline[polyline.Count - 1] - polyline[polyline.Count - 2]).normalized;
        return dir.sqrMagnitude < 0.0001f ? Vector3.right : dir;
    }

    private void EnsureLaneDataCount(int targetCount)
    {
        while (laneData.Count < targetCount)
            laneData.Add(new RoadLaneDataV2());

        if (laneData.Count > targetCount)
            laneData.RemoveRange(targetCount, laneData.Count - targetCount);
    }

    private void RefreshSidewalkVisuals(List<Vector3> centerPolyline)
    {
        EnsureSidewalkRenderers();

        if (!generateSidewalks || centerPolyline == null || centerPolyline.Count < 2)
        {
            if (leftSidewalkRenderer != null)
                leftSidewalkRenderer.enabled = false;

            if (rightSidewalkRenderer != null)
                rightSidewalkRenderer.enabled = false;

            return;
        }

        float startRoadWidth = GetNodeVisualRoadWidth(startNode, TotalRoadWidth);
        float endRoadWidth = GetNodeVisualRoadWidth(endNode, TotalRoadWidth);

        if (leftSidewalk)
        {
            float startOffset = startRoadWidth * 0.5f + sidewalkWidth * 0.5f;
            float endOffset = endRoadWidth * 0.5f + sidewalkWidth * 0.5f;
            List<Vector3> leftPolyline = BuildTaperedOffsetPolyline(centerPolyline, startOffset, endOffset);
            ExtendPolylineForSimpleJoints(leftPolyline, startNode, endNode, true);
            ApplySidewalkPolyline(leftSidewalkRenderer, leftPolyline, "Sidewalk_Left");
        }
        else if (leftSidewalkRenderer != null)
        {
            leftSidewalkRenderer.enabled = false;
        }

        if (rightSidewalk)
        {
            float startOffset = -(startRoadWidth * 0.5f + sidewalkWidth * 0.5f);
            float endOffset = -(endRoadWidth * 0.5f + sidewalkWidth * 0.5f);
            List<Vector3> rightPolyline = BuildTaperedOffsetPolyline(centerPolyline, startOffset, endOffset);
            ExtendPolylineForSimpleJoints(rightPolyline, startNode, endNode, false);
            ApplySidewalkPolyline(rightSidewalkRenderer, rightPolyline, "Sidewalk_Right");
        }
        else if (rightSidewalkRenderer != null)
        {
            rightSidewalkRenderer.enabled = false;
        }
    }

    private void ApplySidewalkPolyline(LineRenderer renderer, List<Vector3> polyline, string objectName)
    {
        if (renderer == null)
            return;

        if (polyline == null || polyline.Count < 2)
        {
            renderer.enabled = false;
            return;
        }

        renderer.gameObject.name = objectName;
        renderer.enabled = true;
        renderer.positionCount = polyline.Count;
        renderer.startWidth = sidewalkWidth;
        renderer.endWidth = sidewalkWidth;
        renderer.startColor = sidewalkColor;
        renderer.endColor = sidewalkColor;
        renderer.sortingOrder = sidewalkSortingOrder;

        for (int i = 0; i < polyline.Count; i++)
            renderer.SetPosition(i, polyline[i]);
    }

    private void EnsureSidewalkRenderers()
    {
        if (leftSidewalkRenderer == null)
        {
            Transform existing = transform.Find("Sidewalk_Left");
            if (existing != null)
                leftSidewalkRenderer = existing.GetComponent<LineRenderer>();

            if (leftSidewalkRenderer == null)
            {
                GameObject go = new GameObject("Sidewalk_Left");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                leftSidewalkRenderer = go.AddComponent<LineRenderer>();
            }
        }

        if (rightSidewalkRenderer == null)
        {
            Transform existing = transform.Find("Sidewalk_Right");
            if (existing != null)
                rightSidewalkRenderer = existing.GetComponent<LineRenderer>();

            if (rightSidewalkRenderer == null)
            {
                GameObject go = new GameObject("Sidewalk_Right");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                rightSidewalkRenderer = go.AddComponent<LineRenderer>();
            }
        }

        ConfigureSidewalkRenderer(leftSidewalkRenderer);
        ConfigureSidewalkRenderer(rightSidewalkRenderer);
    }

    private void ConfigureSidewalkRenderer(LineRenderer renderer)
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
        renderer.numCapVertices = 4;
        renderer.numCornerVertices = 2;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.alignment = LineAlignment.TransformZ;
    }

    private void UpdateArrowVisual(
    SpriteRenderer arrowRenderer,
    Vector3 from,
    Vector3 to,
    Color color,
    string objectName,
    Sprite sprite)
    {
        if (arrowRenderer == null)
            return;

        arrowRenderer.gameObject.name = objectName;
        arrowRenderer.enabled = showLaneArrows && sprite != null;

        if (!arrowRenderer.enabled)
            return;

        arrowRenderer.sprite = sprite;
        arrowRenderer.color = color;
        arrowRenderer.sortingOrder = arrowSortingOrder;

        Vector3 direction = (to - from).normalized;
        Vector3 center = Vector3.Lerp(from, to, Mathf.Clamp01(arrowPositionT));

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + arrowAngleOffset - 180f;

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
        renderer.numCapVertices = 4;
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
        roadRenderer.numCapVertices = 6;
        roadRenderer.numCornerVertices = 2;
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

    private static Sprite GetArrowSprite(int movementMask)
    {
        if (movementMask == 0)
            return null;

        if (cachedArrowSprites.TryGetValue(movementMask, out Sprite cached) && cached != null)
            return cached;

        const int width = 64;
        const int height = 64;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color white = Color.white;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, clear);
        }

        if ((movementMask & 1) != 0)
            DrawStraightArrow(texture, white);

        if ((movementMask & 2) != 0)
            DrawLeftArrow(texture, white);

        if ((movementMask & 4) != 0)
            DrawRightArrow(texture, white);

        texture.Apply();

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            64f
        );

        cachedArrowSprites[movementMask] = sprite;
        return sprite;
    }

    private static void DrawStraightArrow(Texture2D texture, Color color)
    {
        DrawLine(texture, 8, 32, 54, 32, 3, color);
        DrawArrowHead(texture, 54, 32, 1, 0, 8, color);
    }

    private static void DrawLeftArrow(Texture2D texture, Color color)
    {
        DrawLine(texture, 8, 32, 34, 32, 3, color);
        DrawLine(texture, 34, 32, 34, 54, 3, color);
        DrawArrowHead(texture, 34, 54, 0, 1, 8, color);
    }

    private static void DrawRightArrow(Texture2D texture, Color color)
    {
        DrawLine(texture, 8, 32, 34, 32, 3, color);
        DrawLine(texture, 34, 32, 34, 10, 3, color);
        DrawArrowHead(texture, 34, 10, 0, -1, 8, color);
    }

    private static void DrawArrowHead(Texture2D texture, int x, int y, int dirX, int dirY, int size, Color color)
    {
        if (dirX == 1 && dirY == 0)
        {
            DrawLine(texture, x, y, x - size, y + size / 2, 3, color);
            DrawLine(texture, x, y, x - size, y - size / 2, 3, color);
        }
        else if (dirX == 0 && dirY == 1)
        {
            DrawLine(texture, x, y, x - size / 2, y - size, 3, color);
            DrawLine(texture, x, y, x + size / 2, y - size, 3, color);
        }
        else if (dirX == 0 && dirY == -1)
        {
            DrawLine(texture, x, y, x - size / 2, y + size, 3, color);
            DrawLine(texture, x, y, x + size / 2, y + size, 3, color);
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, int thickness, Color color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            DrawThickPixel(texture, x0, y0, thickness, color);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;

            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void DrawThickPixel(Texture2D texture, int x, int y, int radius, Color color)
    {
        for (int oy = -radius; oy <= radius; oy++)
        {
            for (int ox = -radius; ox <= radius; ox++)
            {
                if (ox * ox + oy * oy > radius * radius)
                    continue;

                int px = x + ox;
                int py = y + oy;

                if (px < 0 || px >= texture.width || py < 0 || py >= texture.height)
                    continue;

                texture.SetPixel(px, py, color);
            }
        }
    }
}
