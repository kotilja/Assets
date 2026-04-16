using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class RoadNetworkV2 : MonoBehaviour
{
    [System.Serializable]
    private class NewSegmentSplitPoint
    {
        public float t;
        public RoadNodeV2 node;
    }

    [System.Serializable]
    private class ExistingSegmentIntersection
    {
        public RoadSegmentV2 segment;
        public RoadNodeV2 node;
        public float existingT;
        public float newSegmentT;
    }

    [System.Serializable]
private class ManualLaneConnectionProfile
{
    public int fromLaneId;
    public List<int> toLaneIds = new List<int>();
}

    [SerializeField] private Transform nodesRoot;
    [SerializeField] private Transform segmentsRoot;

    [SerializeField] private List<RoadNodeV2> nodes = new List<RoadNodeV2>();
    [SerializeField] private List<RoadSegmentV2> segments = new List<RoadSegmentV2>();

    [Header("Generated lane graph")]
    [SerializeField] private List<RoadLaneDataV2> allLanes = new List<RoadLaneDataV2>();
    [SerializeField] private List<RoadLaneConnectionV2> allConnections = new List<RoadLaneConnectionV2>();

    [Header("Manual lane connections")]
    [SerializeField] private List<ManualLaneConnectionProfile> manualLaneConnectionProfiles = new List<ManualLaneConnectionProfile>();



    [Header("Connection generation")]
    [SerializeField] private float straightAngleThreshold = 30f;
    [SerializeField] private float maxTurnAngle = 140f;

    [Header("Intersection handling")]
    [SerializeField] private float intersectionSnapDistance = 0.05f;
    [SerializeField] private float intersectionEpsilon = 0.001f;
    [SerializeField] private float mergeStraightAngleThreshold = 10f;

    [SerializeField] private int nextNodeId = 1;
    [SerializeField] private int nextSegmentId = 1;

    public IReadOnlyList<RoadNodeV2> Nodes => nodes;
    public IReadOnlyList<RoadSegmentV2> Segments => segments;
    public IReadOnlyList<RoadLaneDataV2> AllLanes => allLanes;
    public IReadOnlyList<RoadLaneConnectionV2> AllConnections => allConnections;

    private void Start()
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        CleanupNulls();

        foreach (RoadSegmentV2 segment in segments)
        {
            if (segment != null)
                segment.RefreshVisual();
        }

        SyncNodeSignals();
        RebuildLaneGraph();
        CleanupNulls();

        foreach (RoadSegmentV2 segment in segments)
        {
            if (segment != null)
                segment.RefreshDirectionArrows();
        }
    }

    private void SyncNodeSignals()
    {
        foreach (RoadNodeV2 node in nodes)
        {
            if (node == null)
                continue;

            bool shouldHaveSignal = node.UsesTrafficLight;
            RoadNodeSignalV2 signal = node.GetComponent<RoadNodeSignalV2>();

            if (shouldHaveSignal)
            {
                if (signal == null)
                {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    signal = Undo.AddComponent<RoadNodeSignalV2>(node.gameObject);
                else
                    signal = node.gameObject.AddComponent<RoadNodeSignalV2>();
#else
                    signal = node.gameObject.AddComponent<RoadNodeSignalV2>();
#endif
                }

                if (signal != null)
                    signal.SyncFromNode();
            }
            else
            {
                if (signal == null)
                    continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.DestroyObjectImmediate(signal);
            else
                Destroy(signal);
#else
                Destroy(signal);
#endif
            }
        }
    }

    public void RebuildLaneGraph()
    {
        allLanes.Clear();
        allConnections.Clear();

        Dictionary<RoadNodeV2, List<RoadLaneDataV2>> incomingByNode = new Dictionary<RoadNodeV2, List<RoadLaneDataV2>>();
        Dictionary<RoadNodeV2, List<RoadLaneDataV2>> outgoingByNode = new Dictionary<RoadNodeV2, List<RoadLaneDataV2>>();

        foreach (RoadSegmentV2 segment in segments)
        {
            if (segment == null)
                continue;

            foreach (RoadLaneDataV2 lane in segment.LaneData)
            {
                if (lane == null)
                    continue;

                allLanes.Add(lane);

                if (!incomingByNode.ContainsKey(lane.toNode))
                    incomingByNode[lane.toNode] = new List<RoadLaneDataV2>();

                if (!outgoingByNode.ContainsKey(lane.fromNode))
                    outgoingByNode[lane.fromNode] = new List<RoadLaneDataV2>();

                incomingByNode[lane.toNode].Add(lane);
                outgoingByNode[lane.fromNode].Add(lane);

                lane.outgoingConnections.Clear();
                lane.incomingConnections.Clear();
            }
        }

        foreach (RoadSegmentV2 segment in segments)
        {
            if (segment != null)
                BuildLaneChangeConnectionsForSegment(segment);
        }

        foreach (RoadNodeV2 node in nodes)
{
    if (node == null)
        continue;

    if (!incomingByNode.TryGetValue(node, out List<RoadLaneDataV2> incoming))
        continue;

    if (!outgoingByNode.TryGetValue(node, out List<RoadLaneDataV2> outgoing))
        continue;

    foreach (RoadLaneDataV2 inLane in incoming)
        BuildBestConnectionsForIncomingLane(node, inLane, outgoing);
}

ApplyManualLaneConnectionProfiles();
    }

    private void BuildLaneChangeConnectionsForSegment(RoadSegmentV2 segment)
    {
        if (segment == null || !segment.AllowLaneChanges)
            return;

        BuildLaneChangeConnectionsForDirection(
            segment.GetDrivingLanes(segment.StartNode, segment.EndNode),
            segment
        );

        BuildLaneChangeConnectionsForDirection(
            segment.GetDrivingLanes(segment.EndNode, segment.StartNode),
            segment
        );
    }

    private void BuildLaneChangeConnectionsForDirection(
        List<RoadLaneDataV2> lanes,
        RoadSegmentV2 segment)
    {
        if (lanes == null || lanes.Count < 2)
            return;

        for (int i = 0; i < lanes.Count - 1; i++)
        {
            RoadLaneDataV2 rightLane = lanes[i];
            RoadLaneDataV2 leftLane = lanes[i + 1];

            BuildBidirectionalLaneChangeSamples(rightLane, leftLane, segment);
        }
    }

    private void BuildBidirectionalLaneChangeSamples(
        RoadLaneDataV2 a,
        RoadLaneDataV2 b,
        RoadSegmentV2 segment)
    {
        if (a == null || b == null || segment == null)
            return;

        float laneLength = Vector3.Distance(a.start, a.end);
        if (laneLength < 0.5f)
            return;

        float startDistance = Mathf.Max(0f, segment.NoLaneChangeNearStart);
        float endDistance = Mathf.Max(startDistance, laneLength - segment.NoLaneChangeNearEnd);

        if (endDistance <= startDistance)
            return;

        float step = Mathf.Max(0.4f, segment.LaneChangeStep);

        for (float d = startDistance; d <= endDistance; d += step)
        {
            AddLaneChangeConnection(a, b, d, segment.LaneChangeLength, true);
            AddLaneChangeConnection(b, a, d, segment.LaneChangeLength, false);
        }
    }

    private void AddLaneChangeConnection(
        RoadLaneDataV2 fromLane,
        RoadLaneDataV2 toLane,
        float fromDistance,
        float laneChangeLength,
        bool toLeft)
    {
        if (fromLane == null || toLane == null)
            return;

        float laneLength = Vector3.Distance(fromLane.start, fromLane.end);
        float toDistance = Mathf.Clamp(fromDistance + Mathf.Max(0.2f, laneChangeLength), 0f, laneLength);

        Vector3 fromDir = fromLane.DirectionVector.normalized;
        Vector3 toDir = toLane.DirectionVector.normalized;

        Vector3 p0 = fromLane.start + fromDir * fromDistance;
        Vector3 p2 = toLane.start + toDir * toDistance;
        Vector3 p1 = Vector3.Lerp(p0, p2, 0.5f);

        RoadLaneConnectionV2 connection = new RoadLaneConnectionV2
        {
            fromLane = fromLane,
            toLane = toLane,
            connectionKind = RoadLaneConnectionV2.ConnectionKind.LaneChange,
            movementType = toLeft
                ? RoadLaneConnectionV2.MovementType.LaneChangeLeft
                : RoadLaneConnectionV2.MovementType.LaneChangeRight,
            fromDistanceOnLane = fromDistance,
            toDistanceOnLane = toDistance,
            turnScore = 0f
        };

        connection.curvePoints.Add(p0);
        connection.curvePoints.Add(p1);
        connection.curvePoints.Add(p2);

        allConnections.Add(connection);
        fromLane.outgoingConnections.Add(connection);
        toLane.incomingConnections.Add(connection);
    }

    private void BuildBestConnectionsForIncomingLane(
    RoadNodeV2 node,
    RoadLaneDataV2 incomingLane,
    List<RoadLaneDataV2> outgoingLanes)
{
    if (incomingLane == null || outgoingLanes == null || outgoingLanes.Count == 0)
        return;

    List<RoadLaneDataV2> straightCandidates = new List<RoadLaneDataV2>();
    List<RoadLaneDataV2> leftCandidates = new List<RoadLaneDataV2>();
    List<RoadLaneDataV2> rightCandidates = new List<RoadLaneDataV2>();

    Vector3 inDirection = incomingLane.DirectionVector.normalized;

    foreach (RoadLaneDataV2 outgoingLane in outgoingLanes)
    {
        if (outgoingLane == null)
            continue;

        if (incomingLane.ownerSegment == outgoingLane.ownerSegment)
            continue;

        Vector3 outDirection = outgoingLane.DirectionVector.normalized;
        float angle = Vector3.SignedAngle(inDirection, outDirection, Vector3.forward);
        float absAngle = Mathf.Abs(angle);

        if (absAngle > maxTurnAngle)
            continue;

        if (absAngle <= straightAngleThreshold)
        {
            straightCandidates.Add(outgoingLane);
        }
        else if (angle > 0f)
        {
            leftCandidates.Add(outgoingLane);
        }
        else
        {
            rightCandidates.Add(outgoingLane);
        }
    }

    SortLaneCandidatesByIndex(straightCandidates);
    SortLaneCandidatesByIndex(leftCandidates);
    SortLaneCandidatesByIndex(rightCandidates);

    int incomingLaneCount = GetDirectionalLaneCount(incomingLane);
    int incomingIndex = incomingLane.localLaneIndex;

    RoadLaneDataV2 bestStraight = GetLaneByRelativePosition(
        straightCandidates,
        incomingIndex,
        incomingLaneCount
    );
    AddConnectionIfUnique(node, incomingLane, bestStraight);

    RoadLaneDataV2 bestRight = GetRightTurnCandidate(
        rightCandidates,
        incomingIndex,
        incomingLaneCount
    );
    AddConnectionIfUnique(node, incomingLane, bestRight);

    RoadLaneDataV2 bestLeft = GetLeftTurnCandidate(
        leftCandidates,
        incomingIndex,
        incomingLaneCount
    );
    AddConnectionIfUnique(node, incomingLane, bestLeft);
}

private void SortLaneCandidatesByIndex(List<RoadLaneDataV2> candidates)
{
    if (candidates == null)
        return;

    candidates.Sort((a, b) =>
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        return a.localLaneIndex.CompareTo(b.localLaneIndex);
    });
}

private RoadLaneDataV2 GetLaneByRelativePosition(
    List<RoadLaneDataV2> candidates,
    int incomingIndex,
    int incomingLaneCount)
{
    if (candidates == null || candidates.Count == 0)
        return null;

    if (candidates.Count == 1 || incomingLaneCount <= 1)
        return candidates[0];

    float t = Mathf.Clamp01((float)incomingIndex / Mathf.Max(1, incomingLaneCount - 1));
    int targetIndex = Mathf.RoundToInt(t * (candidates.Count - 1));
    targetIndex = Mathf.Clamp(targetIndex, 0, candidates.Count - 1);

    return candidates[targetIndex];
}

private int GetTurnLaneAllowance(int incomingLaneCount, int outgoingLaneCount)
{
    if (incomingLaneCount <= 0 || outgoingLaneCount <= 0)
        return 0;

    // Для 1-2 полос используем только одну крайнюю полосу под поворот.
    if (incomingLaneCount <= 2 || outgoingLaneCount <= 2)
        return 1;

    // Для 3+ полос можно разрешить две крайние полосы,
    // но только если и на выезде тоже есть запас по полосам.
    return 2;
}

private RoadLaneDataV2 GetRightTurnCandidate(
    List<RoadLaneDataV2> candidates,
    int incomingIndex,
    int incomingLaneCount)
{
    if (candidates == null || candidates.Count == 0)
        return null;

    int allowance = GetTurnLaneAllowance(incomingLaneCount, candidates.Count);
    if (allowance <= 0)
        return null;

    if (incomingIndex >= allowance)
        return null;

    if (allowance == 1)
        return candidates[0];

    float t = Mathf.Clamp01((float)incomingIndex / (allowance - 1));
    int targetIndex = Mathf.RoundToInt(t * (Mathf.Min(candidates.Count, allowance) - 1));
    targetIndex = Mathf.Clamp(targetIndex, 0, candidates.Count - 1);

    return candidates[targetIndex];
}

private RoadLaneDataV2 GetLeftTurnCandidate(
    List<RoadLaneDataV2> candidates,
    int incomingIndex,
    int incomingLaneCount)
{
    if (candidates == null || candidates.Count == 0)
        return null;

    int allowance = GetTurnLaneAllowance(incomingLaneCount, candidates.Count);
    if (allowance <= 0)
        return null;

    int firstEligibleIncomingIndex = incomingLaneCount - allowance;
    if (incomingIndex < firstEligibleIncomingIndex)
        return null;

    if (allowance == 1)
        return candidates[candidates.Count - 1];

    int eligibleIndex = incomingIndex - firstEligibleIncomingIndex;
    int firstEligibleOutgoingIndex = Mathf.Max(0, candidates.Count - allowance);

    float t = Mathf.Clamp01((float)eligibleIndex / (allowance - 1));
    int targetOffset = Mathf.RoundToInt(t * (allowance - 1));
    int targetIndex = firstEligibleOutgoingIndex + targetOffset;
    targetIndex = Mathf.Clamp(targetIndex, 0, candidates.Count - 1);

    return candidates[targetIndex];
}

    private int GetDirectionalLaneCount(RoadLaneDataV2 lane)
    {
        if (lane == null || lane.ownerSegment == null)
            return 1;

        List<RoadLaneDataV2> lanes = lane.ownerSegment.GetDrivingLanes(lane.fromNode, lane.toNode);
        return Mathf.Max(1, lanes.Count);
    }

    
    private void AddConnectionIfUnique(RoadNodeV2 node, RoadLaneDataV2 fromLane, RoadLaneDataV2 toLane)
    {
        if (node == null || fromLane == null || toLane == null)
            return;

        foreach (RoadLaneConnectionV2 existing in fromLane.outgoingConnections)
        {
            if (existing != null && existing.toLane == toLane)
                return;
        }

        float turnScore = CalculateTurnScore(fromLane, toLane);
        RoadLaneConnectionV2.MovementType movementType = GetMovementType(turnScore);

        if (!node.AllowsMovement(fromLane.ownerSegment, movementType))
            return;

        RoadLaneConnectionV2 connection = new RoadLaneConnectionV2
        {
            fromLane = fromLane,
            toLane = toLane,
            connectionKind = RoadLaneConnectionV2.ConnectionKind.Junction,
            junctionNode = node,
            junctionPoint = node.transform.position,
            turnScore = turnScore,
            movementType = movementType,
            curvePoints = BuildConnectionCurvePoints(fromLane, toLane, node.transform.position)
        };

        allConnections.Add(connection);
        fromLane.outgoingConnections.Add(connection);
        toLane.incomingConnections.Add(connection);
    }
public bool HasManualConnectionsForLane(int fromLaneId)
{
    ManualLaneConnectionProfile profile = GetManualProfile(fromLaneId);
    return profile != null && profile.toLaneIds.Count > 0;
}

public bool HasManualConnection(int fromLaneId, int toLaneId)
{
    ManualLaneConnectionProfile profile = GetManualProfile(fromLaneId);
    return profile != null && profile.toLaneIds.Contains(toLaneId);
}

public void ToggleManualLaneConnection(int fromLaneId, int toLaneId)
{
    if (fromLaneId <= 0 || toLaneId <= 0)
        return;

    ManualLaneConnectionProfile profile = GetOrCreateManualProfile(fromLaneId);

    if (profile.toLaneIds.Contains(toLaneId))
        profile.toLaneIds.Remove(toLaneId);
    else
        profile.toLaneIds.Add(toLaneId);

    if (profile.toLaneIds.Count == 0)
        manualLaneConnectionProfiles.Remove(profile);

    RefreshAll();
}

public void ClearManualConnectionsForLane(int fromLaneId)
{
    ManualLaneConnectionProfile profile = GetManualProfile(fromLaneId);
    if (profile == null)
        return;

    manualLaneConnectionProfiles.Remove(profile);
    RefreshAll();
}

public RoadLaneDataV2 FindLaneById(int laneId)
{
    for (int i = 0; i < allLanes.Count; i++)
    {
        RoadLaneDataV2 lane = allLanes[i];
        if (lane != null && lane.laneId == laneId)
            return lane;
    }

    return null;
}

private ManualLaneConnectionProfile GetManualProfile(int fromLaneId)
{
    for (int i = 0; i < manualLaneConnectionProfiles.Count; i++)
    {
        ManualLaneConnectionProfile profile = manualLaneConnectionProfiles[i];
        if (profile != null && profile.fromLaneId == fromLaneId)
            return profile;
    }

    return null;
}

private ManualLaneConnectionProfile GetOrCreateManualProfile(int fromLaneId)
{
    ManualLaneConnectionProfile profile = GetManualProfile(fromLaneId);
    if (profile != null)
        return profile;

    profile = new ManualLaneConnectionProfile
    {
        fromLaneId = fromLaneId
    };

    manualLaneConnectionProfiles.Add(profile);
    return profile;
}

private void ApplyManualLaneConnectionProfiles()
{
    for (int i = 0; i < manualLaneConnectionProfiles.Count; i++)
    {
        ManualLaneConnectionProfile profile = manualLaneConnectionProfiles[i];
        if (profile == null)
            continue;

        RoadLaneDataV2 fromLane = FindLaneById(profile.fromLaneId);
        if (fromLane == null)
            continue;

        RemoveJunctionConnectionsFromLane(fromLane);

        for (int j = 0; j < profile.toLaneIds.Count; j++)
        {
            RoadLaneDataV2 toLane = FindLaneById(profile.toLaneIds[j]);
            if (toLane == null)
                continue;

            if (fromLane.toNode == null || fromLane.toNode != toLane.fromNode)
                continue;

            AddManualConnection(fromLane.toNode, fromLane, toLane);
        }
    }
}

private void RemoveJunctionConnectionsFromLane(RoadLaneDataV2 fromLane)
{
    if (fromLane == null)
        return;

    for (int i = fromLane.outgoingConnections.Count - 1; i >= 0; i--)
    {
        RoadLaneConnectionV2 connection = fromLane.outgoingConnections[i];
        if (connection == null)
            continue;

        if (connection.connectionKind != RoadLaneConnectionV2.ConnectionKind.Junction)
            continue;

        fromLane.outgoingConnections.RemoveAt(i);

        if (connection.toLane != null)
            connection.toLane.incomingConnections.Remove(connection);

        allConnections.Remove(connection);
    }
}

private void AddManualConnection(RoadNodeV2 node, RoadLaneDataV2 fromLane, RoadLaneDataV2 toLane)
{
    if (node == null || fromLane == null || toLane == null)
        return;

    foreach (RoadLaneConnectionV2 existing in fromLane.outgoingConnections)
    {
        if (existing != null && existing.toLane == toLane)
            return;
    }

    float turnScore = CalculateTurnScore(fromLane, toLane);
    RoadLaneConnectionV2.MovementType movementType = GetMovementType(turnScore);

    RoadLaneConnectionV2 connection = new RoadLaneConnectionV2
    {
        fromLane = fromLane,
        toLane = toLane,
        connectionKind = RoadLaneConnectionV2.ConnectionKind.Junction,
        isManual = true,
        junctionNode = node,
        junctionPoint = node.transform.position,
        turnScore = turnScore,
        movementType = movementType,
        curvePoints = BuildConnectionCurvePoints(fromLane, toLane, node.transform.position)
    };

    allConnections.Add(connection);
    fromLane.outgoingConnections.Add(connection);
    toLane.incomingConnections.Add(connection);
}


    private float CalculateTurnScore(RoadLaneDataV2 fromLane, RoadLaneDataV2 toLane)
    {
        Vector3 a = fromLane.DirectionVector.normalized;
        Vector3 b = toLane.DirectionVector.normalized;
        return Vector3.SignedAngle(a, b, Vector3.forward);
    }

    private RoadLaneConnectionV2.MovementType GetMovementType(float turnScore)
{
    float absAngle = Mathf.Abs(turnScore);

    if (absAngle <= straightAngleThreshold)
        return RoadLaneConnectionV2.MovementType.Straight;

    return turnScore > 0f
        ? RoadLaneConnectionV2.MovementType.Left
        : RoadLaneConnectionV2.MovementType.Right;
}

    private List<Vector3> BuildConnectionCurvePoints(
    RoadLaneDataV2 fromLane,
    RoadLaneDataV2 toLane,
    Vector3 junctionPoint)
    {
        List<Vector3> points = new List<Vector3>();

        if (fromLane == null || toLane == null)
            return points;

        Vector3 p0 = fromLane.end;
        Vector3 p2 = toLane.start;

        Vector3 inDir = fromLane.DirectionVector.normalized;
        Vector3 outDir = toLane.DirectionVector.normalized;

        float signedAngle = Vector3.SignedAngle(inDir, outDir, Vector3.forward);
        float absAngle = Mathf.Abs(signedAngle);

        if (absAngle < 20f || Vector3.Distance(p0, p2) < 0.15f)
        {
            points.Add(p0);
            AddPointIfFar(points, Vector3.Lerp(p0, p2, 0.5f));
            AddPointIfFar(points, p2);
            return points;
        }

        if (TryGetAxisAlignedTurnCorner(p0, inDir, p2, outDir, out Vector3 corner))
        {
            float laneSize = 0.6f;

            if (fromLane.ownerSegment != null && toLane.ownerSegment != null)
                laneSize = Mathf.Min(fromLane.ownerSegment.LaneWidth, toLane.ownerSegment.LaneWidth);

            float distToCornerA = Vector3.Distance(p0, corner);
            float distToCornerB = Vector3.Distance(corner, p2);
            float maxInset = Mathf.Min(distToCornerA, distToCornerB) * 0.45f;

            float inset = Mathf.Clamp(laneSize * 0.75f, 0.08f, maxInset);

            Vector3 entry = Vector3.MoveTowards(corner, p0, inset);
            Vector3 exit = Vector3.MoveTowards(corner, p2, inset);

            points.Add(p0);
            AddPointIfFar(points, entry);

            AddQuadraticBezierSamples(points, entry, corner, exit, 4);

            AddPointIfFar(points, p2);
            return points;
        }

        points.Add(p0);
        AddPointIfFar(points, Vector3.Lerp(p0, p2, 0.5f));
        AddPointIfFar(points, p2);
        return points;
    }

    private void AddQuadraticBezierSamples(
    List<Vector3> points,
    Vector3 p0,
    Vector3 p1,
    Vector3 p2,
    int sampleCount)
    {
        if (points == null)
            return;

        int samples = Mathf.Max(2, sampleCount);

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            AddPointIfFar(points, EvaluateQuadraticBezier(p0, p1, p2, t));
        }
    }

    private bool TryGetAxisAlignedTurnCorner(
    Vector3 fromPoint,
    Vector3 fromDir,
    Vector3 toPoint,
    Vector3 toDir,
    out Vector3 corner)
    {
        corner = Vector3.zero;

        Vector3 candidate1 = new Vector3(toPoint.x, fromPoint.y, 0f);
        Vector3 candidate2 = new Vector3(fromPoint.x, toPoint.y, 0f);

        bool c1Valid = IsValidAxisCorner(fromPoint, fromDir, candidate1, toPoint, toDir);
        bool c2Valid = IsValidAxisCorner(fromPoint, fromDir, candidate2, toPoint, toDir);

        if (c1Valid && !c2Valid)
        {
            corner = candidate1;
            return true;
        }

        if (c2Valid && !c1Valid)
        {
            corner = candidate2;
            return true;
        }

        if (c1Valid && c2Valid)
        {
            float d1 = Vector3.Distance(fromPoint, candidate1) + Vector3.Distance(candidate1, toPoint);
            float d2 = Vector3.Distance(fromPoint, candidate2) + Vector3.Distance(candidate2, toPoint);

            corner = d1 <= d2 ? candidate1 : candidate2;
            return true;
        }

        return false;
    }

    private bool IsValidAxisCorner(
        Vector3 fromPoint,
        Vector3 fromDir,
        Vector3 corner,
        Vector3 toPoint,
        Vector3 toDir)
    {
        Vector3 firstLeg = corner - fromPoint;
        Vector3 secondLeg = toPoint - corner;

        if (firstLeg.sqrMagnitude < 0.0001f || secondLeg.sqrMagnitude < 0.0001f)
            return false;

        firstLeg.Normalize();
        secondLeg.Normalize();

        float dot1 = Vector3.Dot(firstLeg, fromDir.normalized);
        float dot2 = Vector3.Dot(secondLeg, toDir.normalized);

        return dot1 > 0.9f && dot2 > 0.9f;
    }

    private void AddPointIfFar(List<Vector3> points, Vector3 point)
    {
        point.z = 0f;

        if (points.Count == 0)
        {
            points.Add(point);
            return;
        }

        if (Vector3.Distance(points[points.Count - 1], point) > 0.01f)
            points.Add(point);
    }

    private bool TryGetTurnCorner(Vector3 fromPoint, Vector3 fromDir, Vector3 toPoint, Vector3 toDir, out Vector3 corner)
    {
        corner = Vector3.zero;

        Vector2 p = new Vector2(fromPoint.x, fromPoint.y);
        Vector2 r = new Vector2(fromDir.x, fromDir.y);

        Vector2 q = new Vector2(toPoint.x, toPoint.y);
        Vector2 s = new Vector2(-toDir.x, -toDir.y);

        float rxs = r.x * s.y - r.y * s.x;
        if (Mathf.Abs(rxs) < 0.0001f)
            return false;

        Vector2 qp = q - p;
        float t = (qp.x * s.y - qp.y * s.x) / rxs;
        float u = (qp.x * r.y - qp.y * r.x) / rxs;

        if (t < 0f || u < 0f)
            return false;

        Vector2 result = p + r * t;
        corner = new Vector3(result.x, result.y, 0f);
        return true;
    }

    private Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    private Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
{
    float u = 1f - t;
    return
        u * u * u * p0 +
        3f * u * u * t * p1 +
        3f * u * t * t * p2 +
        t * t * t * p3;
}

    public RoadNodeV2 GetOrCreateNodeNear(Vector3 position, float snapDistance)
    {
        position.z = 0f;

        RoadNodeV2 nearestNode = GetNearestNode(position, snapDistance);

        if (nearestNode != null)
            return nearestNode;

        return CreateNode(position);
    }

    public RoadNodeV2 GetNearestIntersectionNode(Vector3 position, float maxDistance)
    {
        position.z = 0f;

        float bestDistance = maxDistance;
        RoadNodeV2 bestNode = null;

        foreach (RoadNodeV2 node in nodes)
        {
            if (node == null)
                continue;

            if (node.ConnectedSegments.Count <= 2)
                continue;

            float distance = Vector3.Distance(node.transform.position, position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                bestNode = node;
            }
        }

        return bestNode;
    }

    public bool TryGetNearestPointOnSegment(
    Vector3 position,
    float maxDistance,
    out Vector3 snappedPoint,
    out RoadSegmentV2 snappedSegment)
    {
        position.z = 0f;

        snappedPoint = position;
        snappedSegment = null;

        float bestDistance = maxDistance;

        foreach (RoadSegmentV2 segment in segments)
        {
            if (segment == null || segment.StartNode == null || segment.EndNode == null)
                continue;

            List<Vector3> polyline = segment.GetCenterPolylineWorld();
            if (polyline == null || polyline.Count < 2)
                continue;

            Vector3 candidate = ProjectPointOntoPolyline(position, polyline);
            float distance = Vector3.Distance(position, candidate);

            if (distance <= bestDistance)
            {
                bestDistance = distance;
                snappedPoint = candidate;
                snappedSegment = segment;
            }
        }

        return snappedSegment != null;
    }

    private Vector3 ProjectPointOntoSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;

        if (ab.sqrMagnitude < 0.0001f)
            return a;

        float t = Vector3.Dot(point - a, ab) / Vector3.Dot(ab, ab);
        t = Mathf.Clamp01(t);

        Vector3 projected = a + ab * t;
        projected.z = 0f;
        return projected;
    }

    public RoadNodeV2 CreateNode(Vector3 position)
    {
        EnsureRoots();
        CleanupNulls();

        GameObject nodeObject = new GameObject();
        nodeObject.transform.SetParent(nodesRoot);
        nodeObject.transform.position = new Vector3(position.x, position.y, 0f);

        RoadNodeV2 node = nodeObject.AddComponent<RoadNodeV2>();
        node.Initialize(nextNodeId++);
        nodes.Add(node);

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(nodeObject, "Create Road Node");
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif

        return node;
    }

    public RoadSegmentV2 CreateSegment(
    RoadNodeV2 startNode,
    RoadNodeV2 endNode,
    int forwardLanes,
    int backwardLanes,
    float laneWidth,
    float speedLimit)
    {
        if (startNode == null || endNode == null)
            return null;

        if (startNode == endNode)
            return null;

        EnsureRoots();
        CleanupNulls();

        RoadSegmentV2 existingDirect = FindExistingSegment(startNode, endNode);
        if (existingDirect != null)
            return existingDirect;

        List<Vector3> newPolyline = new List<Vector3>
    {
        startNode.transform.position,
        endNode.transform.position
    };

        List<ExistingSegmentIntersection> foundIntersections = new List<ExistingSegmentIntersection>();
        List<NewSegmentSplitPoint> splitPoints = new List<NewSegmentSplitPoint>
    {
        new NewSegmentSplitPoint { t = 0f, node = startNode },
        new NewSegmentSplitPoint { t = 1f, node = endNode }
    };

        CollectIntersectionsForNewPolyline(
            newPolyline,
            startNode,
            endNode,
            foundIntersections,
            splitPoints
        );

        SplitIntersectedExistingSegments(foundIntersections);

        splitPoints.Sort((x, y) => x.t.CompareTo(y.t));

        RoadSegmentV2 firstCreated = null;

        for (int i = 0; i < splitPoints.Count - 1; i++)
        {
            RoadNodeV2 fromNode = splitPoints[i].node;
            RoadNodeV2 toNode = splitPoints[i + 1].node;

            if (fromNode == null || toNode == null || fromNode == toNode)
                continue;

            RoadSegmentV2 created = CreateSegmentRaw(
                fromNode,
                toNode,
                forwardLanes,
                backwardLanes,
                laneWidth,
                speedLimit
            );

            if (firstCreated == null && created != null)
                firstCreated = created;
        }

        RefreshAll();
        return firstCreated;
    }

    if (first.IsCurved || second.IsCurved)
    return false;

    private void AddSplitPoint(List<NewSegmentSplitPoint> splitPoints, float t, RoadNodeV2 node)
    {
        if (splitPoints == null || node == null)
            return;

        foreach (NewSegmentSplitPoint existing in splitPoints)
        {
            if (existing == null)
                continue;

            if (existing.node == node)
                return;

            if (Mathf.Abs(existing.t - t) <= intersectionEpsilon)
                return;
        }

        splitPoints.Add(new NewSegmentSplitPoint
        {
            t = Mathf.Clamp01(t),
            node = node
        });
    }

    private RoadNodeV2 ResolveIntersectionNode(
        RoadSegmentV2 existingSegment,
        Vector3 point,
        float existingT,
        RoadNodeV2 newStartNode,
        RoadNodeV2 newEndNode,
        float newT)
    {
        if (existingSegment == null)
            return null;

        if (newT <= intersectionEpsilon)
            return newStartNode;

        if (newT >= 1f - intersectionEpsilon)
            return newEndNode;

        if (existingT <= intersectionEpsilon)
            return existingSegment.StartNode;

        if (existingT >= 1f - intersectionEpsilon)
            return existingSegment.EndNode;

        return GetOrCreateNodeNear(point, intersectionSnapDistance);
    }

    private void SplitExistingSegment(RoadSegmentV2 segment, RoadNodeV2 splitNode, float splitT)
    {
        if (segment == null || splitNode == null)
            return;

        if (segment.StartNode == splitNode || segment.EndNode == splitNode)
            return;

        RoadNodeV2 oldStart = segment.StartNode;
        RoadNodeV2 oldEnd = segment.EndNode;

        int oldForward = segment.ForwardLanes;
        int oldBackward = segment.BackwardLanes;
        float oldLaneWidth = segment.LaneWidth;
        float oldSpeedLimit = segment.SpeedLimit;

        if (!segment.IsCurved)
        {
            DeleteSegmentInternal(segment, deleteOrphanNodes: false);

            CreateSegmentRaw(oldStart, splitNode, oldForward, oldBackward, oldLaneWidth, oldSpeedLimit);
            CreateSegmentRaw(splitNode, oldEnd, oldForward, oldBackward, oldLaneWidth, oldSpeedLimit);

            DeleteNodeIfOrphaned(oldStart);
            DeleteNodeIfOrphaned(oldEnd);
            return;
        }

        Vector3 p0 = oldStart.transform.position;
        Vector3 p1 = segment.CurveControlPoint;
        Vector3 p2 = oldEnd.transform.position;

        float t = Mathf.Clamp(splitT, intersectionEpsilon, 1f - intersectionEpsilon);

        SplitQuadratic(
            p0, p1, p2, t,
            out Vector3 left0, out Vector3 left1, out Vector3 left2,
            out Vector3 right0, out Vector3 right1, out Vector3 right2
        );

        DeleteSegmentInternal(segment, deleteOrphanNodes: false);

        RoadSegmentV2 first = CreateSegmentRaw(oldStart, splitNode, oldForward, oldBackward, oldLaneWidth, oldSpeedLimit);
        if (first != null && !IsQuadraticEffectivelyStraight(left0, left1, left2))
            first.SetCurve(left1);

        RoadSegmentV2 second = CreateSegmentRaw(splitNode, oldEnd, oldForward, oldBackward, oldLaneWidth, oldSpeedLimit);
        if (second != null && !IsQuadraticEffectivelyStraight(right0, right1, right2))
            second.SetCurve(right1);

        DeleteNodeIfOrphaned(oldStart);
        DeleteNodeIfOrphaned(oldEnd);
    }

    private RoadSegmentV2 CreateSegmentRaw(
        RoadNodeV2 startNode,
        RoadNodeV2 endNode,
        int forwardLanes,
        int backwardLanes,
        float laneWidth,
        float speedLimit)
    {
        if (startNode == null || endNode == null)
            return null;

        if (startNode == endNode)
            return null;

        RoadSegmentV2 existingSegment = FindExistingSegment(startNode, endNode);
        if (existingSegment != null)
            return existingSegment;

        GameObject segmentObject = new GameObject();
        segmentObject.transform.SetParent(segmentsRoot);
        segmentObject.transform.position = Vector3.zero;

        RoadSegmentV2 segment = segmentObject.AddComponent<RoadSegmentV2>();
        segment.Initialize(
            nextSegmentId++,
            startNode,
            endNode,
            forwardLanes,
            backwardLanes,
            laneWidth,
            speedLimit
        );

        if (!segments.Contains(segment))
            segments.Add(segment);

        startNode.RegisterSegment(segment);
        endNode.RegisterSegment(segment);

#if UNITY_EDITOR
Undo.RegisterCreatedObjectUndo(segmentObject, "Create Road Segment");
#endif

        return segment;
    }

    public RoadSegmentV2 CreateCurvedSegment(
    RoadNodeV2 startNode,
    RoadNodeV2 endNode,
    Vector3 controlPoint,
    int forwardLanes,
    int backwardLanes,
    float laneWidth,
    float speedLimit)
    {
        if (startNode == null || endNode == null)
            return null;

        if (startNode == endNode)
            return null;

        EnsureRoots();
        CleanupNulls();

        RoadSegmentV2 existingDirect = FindExistingSegment(startNode, endNode);
        if (existingDirect != null)
            return existingDirect;

        Vector3 p0 = startNode.transform.position;
        Vector3 p1 = new Vector3(controlPoint.x, controlPoint.y, 0f);
        Vector3 p2 = endNode.transform.position;

        List<Vector3> newPolyline = BuildQuadraticPolyline(p0, p1, p2, 24);

        List<ExistingSegmentIntersection> foundIntersections = new List<ExistingSegmentIntersection>();
        List<NewSegmentSplitPoint> splitPoints = new List<NewSegmentSplitPoint>
    {
        new NewSegmentSplitPoint { t = 0f, node = startNode },
        new NewSegmentSplitPoint { t = 1f, node = endNode }
    };

        CollectIntersectionsForNewPolyline(
            newPolyline,
            startNode,
            endNode,
            foundIntersections,
            splitPoints
        );

        SplitIntersectedExistingSegments(foundIntersections);

        splitPoints.Sort((x, y) => x.t.CompareTo(y.t));

        RoadSegmentV2 firstCreated = null;

        for (int i = 0; i < splitPoints.Count - 1; i++)
        {
            RoadNodeV2 fromNode = splitPoints[i].node;
            RoadNodeV2 toNode = splitPoints[i + 1].node;

            if (fromNode == null || toNode == null || fromNode == toNode)
                continue;

            float t0 = splitPoints[i].t;
            float t1 = splitPoints[i + 1].t;

            if (t1 <= t0 + intersectionEpsilon)
                continue;

            if (!TryGetQuadraticSubCurve(p0, p1, p2, t0, t1, out Vector3 subP0, out Vector3 subP1, out Vector3 subP2))
                continue;

            RoadSegmentV2 created = CreateSegmentRaw(
                fromNode,
                toNode,
                forwardLanes,
                backwardLanes,
                laneWidth,
                speedLimit
            );

            if (created == null)
                continue;

            if (!IsQuadraticEffectivelyStraight(subP0, subP1, subP2))
                created.SetCurve(subP1);

            if (firstCreated == null)
                firstCreated = created;
        }

        RefreshAll();
        return firstCreated;
    }

    ppublic bool DeleteNearestSegmentAtPoint(Vector3 point, float pickDistance)
    {
        CleanupNulls();

        RoadSegmentV2 bestSegment = null;
        float bestDistance = float.MaxValue;

        foreach (RoadSegmentV2 segment in segments)
        {
            if (segment == null || segment.StartNode == null || segment.EndNode == null)
                continue;

            List<Vector3> polyline = segment.GetCenterPolylineWorld();
            if (polyline == null || polyline.Count < 2)
                continue;

            float distance = DistancePointToPolyline(point, polyline);
            float allowedDistance = Mathf.Max(pickDistance, segment.TotalRoadWidth * 0.5f + 0.15f);

            if (distance <= allowedDistance && distance < bestDistance)
            {
                bestDistance = distance;
                bestSegment = segment;
            }
        }

        if (bestSegment == null)
            return false;

        DeleteSegment(bestSegment);
        return true;
    }

    public void DeleteSegment(RoadSegmentV2 segment)
    {
        if (segment == null)
            return;

        RoadNodeV2 startNode = segment.StartNode;
        RoadNodeV2 endNode = segment.EndNode;

        DeleteSegmentInternal(segment, deleteOrphanNodes: true);

        TryCollapsePassThroughNode(startNode);
        if (endNode != startNode)
            TryCollapsePassThroughNode(endNode);

        RefreshAll();

#if UNITY_EDITOR
    EditorUtility.SetDirty(this);
    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private void DeleteSegmentInternal(RoadSegmentV2 segment, bool deleteOrphanNodes)
{
    if (segment == null)
        return;

    RoadNodeV2 startNode = segment.StartNode;
    RoadNodeV2 endNode = segment.EndNode;

    segments.Remove(segment);

    if (startNode != null)
        startNode.UnregisterSegment(segment);

    if (endNode != null)
        endNode.UnregisterSegment(segment);

#if UNITY_EDITOR
    Undo.DestroyObjectImmediate(segment.gameObject);
#else
    Destroy(segment.gameObject);
#endif

    if (deleteOrphanNodes)
    {
        DeleteNodeIfOrphaned(startNode);
        DeleteNodeIfOrphaned(endNode);
    }
}

private void DeleteNodeIfOrphaned(RoadNodeV2 node)
{
    if (node == null)
        return;

    if (node.ConnectedSegments.Count > 0)
        return;

    nodes.Remove(node);

#if UNITY_EDITOR
    Undo.DestroyObjectImmediate(node.gameObject);
#else
    Destroy(node.gameObject);
#endif
}

    private bool TryCollapsePassThroughNode(RoadNodeV2 node)
    {
        if (node == null)
            return false;

        if (node.ConnectedSegments.Count != 2)
            return false;

        RoadSegmentV2 first = node.ConnectedSegments[0];
        RoadSegmentV2 second = node.ConnectedSegments[1];

        if (first == null || second == null || first == second)
            return false;

        if (first.IsCurved || second.IsCurved)
            return false;

        RoadNodeV2 firstOther = GetOtherNode(first, node);
        RoadNodeV2 secondOther = GetOtherNode(second, node);

        if (firstOther == null || secondOther == null)
            return false;

        if (firstOther == secondOther)
            return false;

        if (!CanMergeSegmentsThroughNode(node, first, second, firstOther, secondOther))
            return false;

        int forwardLanes = GetLaneCountAlong(first, firstOther, node);
        int backwardLanes = GetLaneCountAlong(first, node, firstOther);

        float laneWidth = Mathf.Min(first.LaneWidth, second.LaneWidth);
        float speedLimit = Mathf.Min(first.SpeedLimit, second.SpeedLimit);

        DeleteSegmentInternal(first, deleteOrphanNodes: false);
        DeleteSegmentInternal(second, deleteOrphanNodes: false);

        if (nodes.Contains(node))
            nodes.Remove(node);

#if UNITY_EDITOR
    Undo.DestroyObjectImmediate(node.gameObject);
#else
        Destroy(node.gameObject);
#endif

        RoadSegmentV2 merged = CreateSegmentRaw(
            firstOther,
            secondOther,
            forwardLanes,
            backwardLanes,
            laneWidth,
            speedLimit
        );

        return merged != null;
    }
    private RoadNodeV2 GetOtherNode(RoadSegmentV2 segment, RoadNodeV2 node)
    {
        if (segment == null || node == null)
            return null;

        if (segment.StartNode == node)
            return segment.EndNode;

        if (segment.EndNode == node)
            return segment.StartNode;

        return null;
    }

    private bool CanMergeSegmentsThroughNode(
        RoadNodeV2 centerNode,
        RoadSegmentV2 first,
        RoadSegmentV2 second,
        RoadNodeV2 firstOther,
        RoadNodeV2 secondOther)
    {
        if (centerNode == null || first == null || second == null || firstOther == null || secondOther == null)
            return false;

        Vector3 dirA = (centerNode.transform.position - firstOther.transform.position).normalized;
        Vector3 dirB = (secondOther.transform.position - centerNode.transform.position).normalized;

        float angle = Vector3.Angle(dirA, dirB);
        if (angle > mergeStraightAngleThreshold)
            return false;

        int forwardA = GetLaneCountAlong(first, firstOther, centerNode);
        int forwardB = GetLaneCountAlong(second, centerNode, secondOther);

        int backwardA = GetLaneCountAlong(first, centerNode, firstOther);
        int backwardB = GetLaneCountAlong(second, secondOther, centerNode);

        if (forwardA != forwardB)
            return false;

        if (backwardA != backwardB)
            return false;

        if (Mathf.Abs(first.LaneWidth - second.LaneWidth) > 0.001f)
            return false;

        if (Mathf.Abs(first.SpeedLimit - second.SpeedLimit) > 0.001f)
            return false;

        return true;
    }

    private int GetLaneCountAlong(RoadSegmentV2 segment, RoadNodeV2 fromNode, RoadNodeV2 toNode)
    {
        if (segment == null || fromNode == null || toNode == null)
            return 0;

        if (segment.StartNode == fromNode && segment.EndNode == toNode)
            return segment.ForwardLanes;

        if (segment.EndNode == fromNode && segment.StartNode == toNode)
            return segment.BackwardLanes;

        return 0;
    }

    private bool TryGetSegmentIntersection(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        out Vector3 intersection,
        out float tAB,
        out float tCD)
    {
        intersection = Vector3.zero;
        tAB = 0f;
        tCD = 0f;

        Vector2 p = new Vector2(a.x, a.y);
        Vector2 r = new Vector2(b.x - a.x, b.y - a.y);

        Vector2 q = new Vector2(c.x, c.y);
        Vector2 s = new Vector2(d.x - c.x, d.y - c.y);

        float rxs = Cross(r, s);
        float qMinusPCrossR = Cross(q - p, r);

        if (Mathf.Abs(rxs) <= intersectionEpsilon)
            return false;

        float t = Cross(q - p, s) / rxs;
        float u = qMinusPCrossR / rxs;

        if (t < -intersectionEpsilon || t > 1f + intersectionEpsilon)
            return false;

        if (u < -intersectionEpsilon || u > 1f + intersectionEpsilon)
            return false;

        tAB = Mathf.Clamp01(t);
        tCD = Mathf.Clamp01(u);
        intersection = a + (b - a) * tAB;
        intersection.z = 0f;

        return true;
    }

    private void CollectIntersectionsForNewPolyline(
    List<Vector3> newPolyline,
    RoadNodeV2 newStartNode,
    RoadNodeV2 newEndNode,
    List<ExistingSegmentIntersection> foundIntersections,
    List<NewSegmentSplitPoint> splitPoints)
    {
        if (newPolyline == null || newPolyline.Count < 2)
            return;

        List<RoadSegmentV2> snapshot = new List<RoadSegmentV2>(segments);

        Vector3 a = newStartNode.transform.position;
        Vector3 b = newEndNode.transform.position;

        foreach (RoadSegmentV2 segment in snapshot)
        {
            if (segment == null || segment.StartNode == null || segment.EndNode == null)
                continue;

            List<Vector3> existingPolyline = segment.GetCenterPolylineWorld();
            if (existingPolyline == null || existingPolyline.Count < 2)
                continue;

            if (!TryGetPolylineIntersection(newPolyline, existingPolyline, out Vector3 point, out float tNew, out float tExisting))
                continue;

            Vector3 c = segment.StartNode.transform.position;
            Vector3 d = segment.EndNode.transform.position;

            bool isSameStart = Vector3.Distance(point, a) <= intersectionSnapDistance;
            bool isSameEnd = Vector3.Distance(point, b) <= intersectionSnapDistance;
            bool isExistingStart = Vector3.Distance(point, c) <= intersectionSnapDistance;
            bool isExistingEnd = Vector3.Distance(point, d) <= intersectionSnapDistance;

            if ((isSameStart || isSameEnd) && (isExistingStart || isExistingEnd))
                continue;

            RoadNodeV2 intersectionNode = ResolveIntersectionNode(
                segment,
                point,
                tExisting,
                newStartNode,
                newEndNode,
                tNew
            );

            if (intersectionNode == null)
                continue;

            foundIntersections.Add(new ExistingSegmentIntersection
            {
                segment = segment,
                node = intersectionNode,
                existingT = tExisting,
                newSegmentT = tNew
            });

            AddSplitPoint(splitPoints, tNew, intersectionNode);
        }
    }

    private void SplitIntersectedExistingSegments(List<ExistingSegmentIntersection> foundIntersections)
    {
        HashSet<RoadSegmentV2> alreadySplit = new HashSet<RoadSegmentV2>();

        foreach (ExistingSegmentIntersection intersection in foundIntersections)
        {
            if (intersection == null || intersection.segment == null || intersection.node == null)
                continue;

            if (alreadySplit.Contains(intersection.segment))
                continue;

            bool interiorExisting =
                intersection.existingT > intersectionEpsilon &&
                intersection.existingT < 1f - intersectionEpsilon;

            if (!interiorExisting)
                continue;

            SplitExistingSegment(intersection.segment, intersection.node, intersection.existingT);
            alreadySplit.Add(intersection.segment);
        }
    }

    private bool TryGetPolylineIntersection(
        List<Vector3> firstPolyline,
        List<Vector3> secondPolyline,
        out Vector3 intersection,
        out float firstT,
        out float secondT)
    {
        intersection = Vector3.zero;
        firstT = 0f;
        secondT = 0f;

        if (firstPolyline == null || secondPolyline == null)
            return false;

        if (firstPolyline.Count < 2 || secondPolyline.Count < 2)
            return false;

        bool found = false;
        float bestFirstT = float.MaxValue;
        int firstSegmentCount = firstPolyline.Count - 1;
        int secondSegmentCount = secondPolyline.Count - 1;

        for (int i = 0; i < firstSegmentCount; i++)
        {
            Vector3 a0 = firstPolyline[i];
            Vector3 a1 = firstPolyline[i + 1];

            for (int j = 0; j < secondSegmentCount; j++)
            {
                Vector3 b0 = secondPolyline[j];
                Vector3 b1 = secondPolyline[j + 1];

                if (!TryGetSegmentIntersection(a0, a1, b0, b1, out Vector3 point, out float localTA, out float localTB))
                    continue;

                float globalTA = (i + localTA) / Mathf.Max(1, firstSegmentCount);
                float globalTB = (j + localTB) / Mathf.Max(1, secondSegmentCount);

                if (!found || globalTA < bestFirstT)
                {
                    found = true;
                    bestFirstT = globalTA;
                    intersection = point;
                    firstT = Mathf.Clamp01(globalTA);
                    secondT = Mathf.Clamp01(globalTB);
                }
            }
        }

        return found;
    }

    private List<Vector3> BuildQuadraticPolyline(Vector3 p0, Vector3 p1, Vector3 p2, int sampleCount)
    {
        List<Vector3> points = new List<Vector3>();
        int samples = Mathf.Clamp(sampleCount, 4, 64);

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            points.Add(EvaluateQuadraticBezier(p0, p1, p2, t));
        }

        return points;
    }

    private Vector3 ProjectPointOntoPolyline(Vector3 point, List<Vector3> polyline)
    {
        Vector3 bestPoint = point;
        float bestDistance = float.MaxValue;

        if (polyline == null || polyline.Count < 2)
            return point;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            Vector3 candidate = ProjectPointOntoSegment(point, polyline[i], polyline[i + 1]);
            float distance = Vector3.Distance(point, candidate);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = candidate;
            }
        }

        return bestPoint;
    }

    private float DistancePointToPolyline(Vector3 point, List<Vector3> polyline)
    {
        float bestDistance = float.MaxValue;

        if (polyline == null || polyline.Count < 2)
            return bestDistance;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            float distance = DistancePointToSegment(point, polyline[i], polyline[i + 1]);
            if (distance < bestDistance)
                bestDistance = distance;
        }

        return bestDistance;
    }

    private void SplitQuadratic(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        float t,
        out Vector3 left0,
        out Vector3 left1,
        out Vector3 left2,
        out Vector3 right0,
        out Vector3 right1,
        out Vector3 right2)
    {
        Vector3 p01 = Vector3.Lerp(p0, p1, t);
        Vector3 p12 = Vector3.Lerp(p1, p2, t);
        Vector3 p012 = Vector3.Lerp(p01, p12, t);

        left0 = p0;
        left1 = p01;
        left2 = p012;

        right0 = p012;
        right1 = p12;
        right2 = p2;
    }

    private bool TryGetQuadraticSubCurve(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        float t0,
        float t1,
        out Vector3 sub0,
        out Vector3 sub1,
        out Vector3 sub2)
    {
        sub0 = p0;
        sub1 = p1;
        sub2 = p2;

        t0 = Mathf.Clamp01(t0);
        t1 = Mathf.Clamp01(t1);

        if (t1 <= t0 + 0.0001f)
            return false;

        if (t0 <= 0f && t1 >= 1f)
            return true;

        if (t0 <= 0f)
        {
            SplitQuadratic(p0, p1, p2, t1,
                out sub0, out sub1, out sub2,
                out _, out _, out _);
            return true;
        }

        if (t1 >= 1f)
        {
            SplitQuadratic(p0, p1, p2, t0,
                out _, out _, out _,
                out sub0, out sub1, out sub2);
            return true;
        }

        SplitQuadratic(p0, p1, p2, t1,
            out Vector3 left0, out Vector3 left1, out Vector3 left2,
            out _, out _, out _);

        float localT = t0 / t1;

        SplitQuadratic(left0, left1, left2, localT,
            out _, out _, out _,
            out sub0, out sub1, out sub2);

        return true;
    }

    private bool IsQuadraticEffectivelyStraight(Vector3 p0, Vector3 p1, Vector3 p2)
    {
        return DistancePointToSegment(p1, p0, p2) <= 0.02f;
    }

    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;

        if (ab.sqrMagnitude < 0.0001f)
            return Vector3.Distance(point, a);

        float t = Vector3.Dot(point - a, ab) / Vector3.Dot(ab, ab);
        t = Mathf.Clamp01(t);

        Vector3 projection = a + ab * t;
        return Vector3.Distance(point, projection);
    }

    private RoadNodeV2 GetNearestNode(Vector3 position, float snapDistance)
    {
        float bestDistance = snapDistance;
        RoadNodeV2 bestNode = null;

        foreach (RoadNodeV2 node in nodes)
        {
            if (node == null)
                continue;

            float distance = Vector3.Distance(node.transform.position, position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                bestNode = node;
            }
        }

        return bestNode;
    }

    private RoadSegmentV2 FindExistingSegment(RoadNodeV2 startNode, RoadNodeV2 endNode)
    {
        foreach (RoadSegmentV2 segment in segments)
        {
            if (segment == null)
                continue;

            if (segment.StartNode == startNode && segment.EndNode == endNode)
                return segment;
        }

        return null;
    }

    private void EnsureRoots()
    {
        if (nodesRoot == null)
        {
            Transform existing = transform.Find("Nodes");
            if (existing != null)
                nodesRoot = existing;
            else
            {
                GameObject root = new GameObject("Nodes");
                root.transform.SetParent(transform);
                root.transform.localPosition = Vector3.zero;
                nodesRoot = root.transform;
            }
        }

        if (segmentsRoot == null)
        {
            Transform existing = transform.Find("Segments");
            if (existing != null)
                segmentsRoot = existing;
            else
            {
                GameObject root = new GameObject("Segments");
                root.transform.SetParent(transform);
                root.transform.localPosition = Vector3.zero;
                segmentsRoot = root.transform;
            }
        }
    }

    private void CleanupNulls()
    {
        nodes.RemoveAll(n => n == null);

        segments.RemoveAll(s =>
            s == null ||
            s.StartNode == null ||
            s.EndNode == null
        );

        allLanes.RemoveAll(l => l == null);
        allConnections.RemoveAll(c => c == null);
    }
}