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

    [SerializeField] private Transform nodesRoot;
    [SerializeField] private Transform segmentsRoot;

    [SerializeField] private List<RoadNodeV2> nodes = new List<RoadNodeV2>();
    [SerializeField] private List<RoadSegmentV2> segments = new List<RoadSegmentV2>();

    [Header("Generated lane graph")]
    [SerializeField] private List<RoadLaneDataV2> allLanes = new List<RoadLaneDataV2>();
    [SerializeField] private List<RoadLaneConnectionV2> allConnections = new List<RoadLaneConnectionV2>();

    [Header("Connection generation")]
    [SerializeField] private float straightAngleThreshold = 30f;
    [SerializeField] private float maxTurnAngle = 140f;

    [Header("Intersection handling")]
    [SerializeField] private float intersectionSnapDistance = 0.05f;
    [SerializeField] private float intersectionEpsilon = 0.001f;

    [SerializeField] private int nextNodeId = 1;
    [SerializeField] private int nextSegmentId = 1;

    public IReadOnlyList<RoadNodeV2> Nodes => nodes;
    public IReadOnlyList<RoadSegmentV2> Segments => segments;
    public IReadOnlyList<RoadLaneDataV2> AllLanes => allLanes;
    public IReadOnlyList<RoadLaneConnectionV2> AllConnections => allConnections;

    public void RefreshAll()
    {
        CleanupNulls();

        foreach (RoadSegmentV2 segment in segments)
        {
            if (segment != null)
                segment.RefreshVisual();
        }

        RebuildLaneGraph();
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

        foreach (RoadNodeV2 node in nodes)
        {
            if (node == null)
                continue;

            if (!incomingByNode.TryGetValue(node, out List<RoadLaneDataV2> incoming))
                continue;

            if (!outgoingByNode.TryGetValue(node, out List<RoadLaneDataV2> outgoing))
                continue;

            foreach (RoadLaneDataV2 inLane in incoming)
            {
                BuildBestConnectionsForIncomingLane(node, inLane, outgoing);
            }
        }
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

        int incomingLaneCount = GetDirectionalLaneCount(incomingLane);
        int leftmostIncomingIndex = Mathf.Max(0, incomingLaneCount - 1);
        int rightmostIncomingIndex = 0;

        RoadLaneDataV2 bestStraight = GetClosestLaneByIndex(straightCandidates, incomingLane.localLaneIndex);
        AddConnectionIfUnique(node, incomingLane, bestStraight);

        // Направо — только с крайней правой полосы
        if (incomingLane.localLaneIndex == rightmostIncomingIndex)
        {
            RoadLaneDataV2 bestRight = GetExtremeLaneByIndex(rightCandidates, preferHighestIndex: false);
            AddConnectionIfUnique(node, incomingLane, bestRight);
        }

        // Налево — только с крайней левой полосы
        if (incomingLane.localLaneIndex == leftmostIncomingIndex)
        {
            RoadLaneDataV2 bestLeft = GetExtremeLaneByIndex(leftCandidates, preferHighestIndex: true);
            AddConnectionIfUnique(node, incomingLane, bestLeft);
        }
    }

    private int GetDirectionalLaneCount(RoadLaneDataV2 lane)
    {
        if (lane == null || lane.ownerSegment == null)
            return 1;

        List<RoadLaneDataV2> lanes = lane.ownerSegment.GetDrivingLanes(lane.fromNode, lane.toNode);
        return Mathf.Max(1, lanes.Count);
    }

    private RoadLaneDataV2 GetClosestLaneByIndex(List<RoadLaneDataV2> candidates, int targetIndex)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        RoadLaneDataV2 best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            RoadLaneDataV2 candidate = candidates[i];
            if (candidate == null)
                continue;

            float score = Mathf.Abs(candidate.localLaneIndex - targetIndex);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private RoadLaneDataV2 GetExtremeLaneByIndex(List<RoadLaneDataV2> candidates, bool preferHighestIndex)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        RoadLaneDataV2 best = null;

        for (int i = 0; i < candidates.Count; i++)
        {
            RoadLaneDataV2 candidate = candidates[i];
            if (candidate == null)
                continue;

            if (best == null)
            {
                best = candidate;
                continue;
            }

            if (preferHighestIndex)
            {
                if (candidate.localLaneIndex > best.localLaneIndex)
                    best = candidate;
            }
            else
            {
                if (candidate.localLaneIndex < best.localLaneIndex)
                    best = candidate;
            }
        }

        return best;
    }

    private int GetDirectionalLaneCount(RoadLaneDataV2 lane)
    {
        if (lane == null || lane.ownerSegment == null)
            return 1;

        List<RoadLaneDataV2> lanes = lane.ownerSegment.GetDrivingLanes(lane.fromNode, lane.toNode);
        return Mathf.Max(1, lanes.Count);
    }

    private RoadLaneDataV2 GetClosestLaneByIndex(List<RoadLaneDataV2> candidates, int targetIndex)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        RoadLaneDataV2 best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            RoadLaneDataV2 candidate = candidates[i];
            if (candidate == null)
                continue;

            float score = Mathf.Abs(candidate.localLaneIndex - targetIndex);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private RoadLaneDataV2 GetExtremeLaneByIndex(List<RoadLaneDataV2> candidates, bool preferHighestIndex)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        RoadLaneDataV2 best = null;

        for (int i = 0; i < candidates.Count; i++)
        {
            RoadLaneDataV2 candidate = candidates[i];
            if (candidate == null)
                continue;

            if (best == null)
            {
                best = candidate;
                continue;
            }

            if (preferHighestIndex)
            {
                if (candidate.localLaneIndex > best.localLaneIndex)
                    best = candidate;
            }
            else
            {
                if (candidate.localLaneIndex < best.localLaneIndex)
                    best = candidate;
            }
        }

        return best;
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

    if (!node.AllowsMovement(movementType))
        return;

    RoadLaneConnectionV2 connection = new RoadLaneConnectionV2
    {
        fromLane = fromLane,
        toLane = toLane,
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

        // Почти прямой переход
        if (absAngle < 20f || Vector3.Distance(p0, p2) < 0.15f)
        {
            points.Add(p0);
            AddPointIfFar(points, Vector3.Lerp(p0, p2, 0.5f));
            AddPointIfFar(points, p2);
            return points;
        }

        if (TryGetAxisAlignedTurnCorner(p0, inDir, p2, outDir, out Vector3 corner))
        {
            points.Add(p0);
            AddPointIfFar(points, corner);
            AddPointIfFar(points, p2);
            return points;
        }

        // fallback
        points.Add(p0);
        AddPointIfFar(points, Vector3.Lerp(p0, p2, 0.5f));
        AddPointIfFar(points, p2);
        return points;
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

        Vector3 a = startNode.transform.position;
        Vector3 b = endNode.transform.position;

        List<RoadSegmentV2> snapshot = new List<RoadSegmentV2>(segments);
        List<ExistingSegmentIntersection> foundIntersections = new List<ExistingSegmentIntersection>();
        List<NewSegmentSplitPoint> splitPoints = new List<NewSegmentSplitPoint>
        {
            new NewSegmentSplitPoint { t = 0f, node = startNode },
            new NewSegmentSplitPoint { t = 1f, node = endNode }
        };

        foreach (RoadSegmentV2 segment in snapshot)
        {
            if (segment == null || segment.StartNode == null || segment.EndNode == null)
                continue;

            Vector3 c = segment.StartNode.transform.position;
            Vector3 d = segment.EndNode.transform.position;

            if (!TryGetSegmentIntersection(a, b, c, d, out Vector3 point, out float tNew, out float tExisting))
                continue;

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
                startNode,
                endNode,
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

            SplitExistingSegment(intersection.segment, intersection.node);
            alreadySplit.Add(intersection.segment);
        }

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

private void SplitExistingSegment(RoadSegmentV2 segment, RoadNodeV2 splitNode)
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

    DeleteSegmentInternal(segment, deleteOrphanNodes: false);

    CreateSegmentRaw(oldStart, splitNode, oldForward, oldBackward, oldLaneWidth, oldSpeedLimit);
    CreateSegmentRaw(splitNode, oldEnd, oldForward, oldBackward, oldLaneWidth, oldSpeedLimit);

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

        segments.Add(segment);

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(segmentObject, "Create Road Segment");
#endif

        return segment;
    }

    public bool DeleteNearestSegmentAtPoint(Vector3 point, float pickDistance)
    {
        CleanupNulls();

        RoadSegmentV2 bestSegment = null;
        float bestDistance = float.MaxValue;

        foreach (RoadSegmentV2 segment in segments)
        {
            if (segment == null || segment.StartNode == null || segment.EndNode == null)
                continue;

            Vector3 a = segment.StartNode.transform.position;
            Vector3 b = segment.EndNode.transform.position;

            float distance = DistancePointToSegment(point, a, b);
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

    DeleteSegmentInternal(segment, deleteOrphanNodes: true);
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
        segments.RemoveAll(s => s == null);
        allLanes.RemoveAll(l => l == null);
        allConnections.RemoveAll(c => c == null);
    }
}