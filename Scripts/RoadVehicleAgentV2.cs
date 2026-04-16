using System.Collections.Generic;
using UnityEngine;

public class RoadVehicleAgentV2 : MonoBehaviour
{
    private class GateInfo
    {
        public RoadNodeV2 junctionNode;
        public RoadSegmentV2 incomingSegment;
        public RoadLaneDataV2 incomingLane;
        public RoadLaneDataV2 outgoingLane;
        public RoadLaneConnectionV2.MovementType movementType;
    }

    [SerializeField] private float speed = 3f;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float braking = 8f;
    [SerializeField] private float rotationSpeed = 540f;

    [SerializeField] private float lookAheadDistance = 0.25f;
    [SerializeField] private float safeDistance = 0.45f;
    [SerializeField] private float laneCheckWidth = 0.18f;
    [SerializeField] private float vehicleHalfLengthMin = 0.18f;
    [SerializeField] private float stopLineGap = 0.03f;

    [Header("Junction rules")]
    [SerializeField] private float gateApproachDistance = 0.35f;
    [SerializeField] private float junctionOccupancyMargin = 0.08f;
    [SerializeField] private float exitClearDistance = 0.8f;
    [SerializeField] private float deadlockResolveDelay = 1.25f;

    [Header("Lane planning")]
    [SerializeField] private bool planTurnsAhead = true;
    [SerializeField] private int lanePlanningLookaheadSegments = 3;

    private static readonly List<RoadVehicleAgentV2> activeVehicles = new List<RoadVehicleAgentV2>();

    private readonly List<Vector3> waypoints = new List<Vector3>();
    private readonly Dictionary<int, GateInfo> gatedWaypointIndices = new Dictionary<int, GateInfo>();

    private int currentWaypointIndex;
    private bool isInitialized;
    private float currentSpeed;

    private GateInfo currentJunctionGate;
    private int waitingGateIndex = -1;
    private float waitingGateStartTime = -1f;

    private void OnEnable()
    {
        if (!activeVehicles.Contains(this))
            activeVehicles.Add(this);
    }

    private void OnDisable()
    {
        activeVehicles.Remove(this);
    }


    public void Initialize(List<RoadLaneDataV2> lanePath, RoadLaneDataV2 initialLane = null)
    {
        if (lanePath == null || lanePath.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        RoadLaneDataV2 actualInitialLane = initialLane ?? lanePath[0];

        waypoints.Clear();
        gatedWaypointIndices.Clear();
        currentWaypointIndex = 0;
        currentJunctionGate = null;

        BuildWaypointPath(actualInitialLane, lanePath);

        if (waypoints.Count < 2)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = waypoints[0];
        RotateTowards(waypoints[1] - waypoints[0]);

        currentWaypointIndex = 1;
        currentSpeed = 0f;
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        UpdateCurrentJunctionState();

        if (currentWaypointIndex >= waypoints.Count)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 target = waypoints[currentWaypointIndex];
        Vector3 currentPosition = transform.position;
        Vector3 toTarget = target - currentPosition;

        float distanceToTarget = toTarget.magnitude;
        Vector3 moveDirection = distanceToTarget > 0.0001f
            ? toTarget / distanceToTarget
            : Vector3.zero;

        if (moveDirection.sqrMagnitude > 0.0001f)
            RotateTowards(GetLookAheadDirection(currentWaypointIndex));

        bool gateBlocked = gatedWaypointIndices.ContainsKey(currentWaypointIndex) && IsGateBlocked(currentWaypointIndex);

        float desiredSpeed = speed;

        if (gateBlocked || currentWaypointIndex == waypoints.Count - 1)
            desiredSpeed = GetApproachSpeed(distanceToTarget);

        float allowedMoveDistance = moveDirection.sqrMagnitude > 0.0001f
            ? GetAllowedMoveDistance(currentPosition, moveDirection, Mathf.Max(speed, currentSpeed) * Time.deltaTime)
            : 0f;

        if (Time.deltaTime > 0.0001f)
        {
            float trafficLimitedSpeed = allowedMoveDistance / Time.deltaTime;
            desiredSpeed = Mathf.Min(desiredSpeed, trafficLimitedSpeed);
        }

        float speedChangeRate = desiredSpeed < currentSpeed ? braking : acceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, speedChangeRate * Time.deltaTime);

        float moveDistance = Mathf.Min(currentSpeed * Time.deltaTime, distanceToTarget, allowedMoveDistance);

        if (moveDirection.sqrMagnitude > 0.0001f && moveDistance > 0f)
            transform.position = currentPosition + moveDirection * moveDistance;

        if (Vector3.Distance(transform.position, target) < 0.01f)
        {
            bool reachedGateBlocked = IsGateBlocked(currentWaypointIndex);
            UpdateGateWaitState(currentWaypointIndex, reachedGateBlocked);

            if (reachedGateBlocked)
                return;

            if (gatedWaypointIndices.TryGetValue(currentWaypointIndex, out GateInfo passedGate))
                currentJunctionGate = passedGate;

            ClearGateWaitState();
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Count)
                Destroy(gameObject);
        }
    }

    private float GetAllowedMoveDistance(Vector3 currentPosition, Vector3 moveDirection, float desiredMove)
    {
        if (moveDirection.sqrMagnitude < 0.0001f)
            return desiredMove;

        float allowed = desiredMove;
        float myHalfLength = GetProjectedHalfLengthAlong(moveDirection);

        for (int i = 0; i < activeVehicles.Count; i++)
        {
            RoadVehicleAgentV2 other = activeVehicles[i];

            if (other == null || other == this)
                continue;

            Vector3 delta = other.transform.position - currentPosition;

            float forward = Vector3.Dot(delta, moveDirection);
            if (forward <= 0f)
                continue;

            float lateral = Mathf.Abs(moveDirection.x * delta.y - moveDirection.y * delta.x);
            if (lateral > laneCheckWidth)
                continue;

            float otherHalfLength = other.GetProjectedHalfLengthAlong(moveDirection);
            float freeSpace = forward - myHalfLength - otherHalfLength - safeDistance;

            if (freeSpace < allowed)
                allowed = Mathf.Max(0f, freeSpace);
        }

        return allowed;
    }

    private float GetApproachSpeed(float distanceToTarget)
    {
        if (distanceToTarget <= 0f)
            return 0f;

        float decel = Mathf.Max(0.01f, braking);
        return Mathf.Min(speed, Mathf.Sqrt(2f * decel * distanceToTarget));
    }

    private void BuildWaypointPath(RoadLaneDataV2 initialLane, List<RoadLaneDataV2> lanePath)
    {
        if (lanePath == null || lanePath.Count == 0 || initialLane == null)
            return;

        AddLanePolyline(initialLane, includeFirstPoint: true, skipLastPoint: true);

        RoadLaneDataV2 activeLane = initialLane;

        for (int i = 0; i < lanePath.Count; i++)
        {
            bool hasNext = i < lanePath.Count - 1;

            if (!hasNext)
            {
                AddLanePolyline(activeLane, includeFirstPoint: false, skipLastPoint: false);
                continue;
            }

            RoadLaneDataV2 plannedNextLane = lanePath[i + 1];

            float signedAngle = Vector3.SignedAngle(
                activeLane.DirectionVector.normalized,
                plannedNextLane.DirectionVector.normalized,
                Vector3.forward
            );

            RoadLaneConnectionV2.MovementType movementType = GetMovementType(signedAngle);

            RoadLaneDataV2 plannedLane = GetPlannedLaneForCurrentSegment(
                activeLane,
                lanePath,
                i,
                movementType
            );

            if (plannedLane != null && plannedLane != activeLane)
            {
                List<Vector3> laneChangePoints = BuildMidSegmentLaneChange(activeLane, plannedLane);

                for (int j = 0; j < laneChangePoints.Count; j++)
                    AddPointIfFar(laneChangePoints[j]);

                activeLane = plannedLane;
            }

            AddLanePolyline(activeLane, includeFirstPoint: false, skipLastPoint: true);

            RoadLaneConnectionV2 connection = FindConnection(activeLane, plannedNextLane);

            int gateIndex = waypoints.Count;

            GateInfo gate = connection != null
                ? BuildGateFromRealConnection(connection)
                : BuildGateFromSyntheticTurn(activeLane, plannedNextLane);

            if (gate != null)
                gatedWaypointIndices[gateIndex] = gate;

            List<Vector3> turnPoints = connection != null
                ? BuildTurnFromConnection(activeLane, plannedNextLane, connection)
                : BuildNodeAnchoredTurn(activeLane, plannedNextLane);

            for (int j = 0; j < turnPoints.Count; j++)
                AddPointIfFar(turnPoints[j]);

            activeLane = plannedNextLane;
        }
    }

    private RoadLaneDataV2 GetPlannedLaneForCurrentSegment(
    RoadLaneDataV2 currentLane,
    List<RoadLaneDataV2> lanePath,
    int currentPathIndex,
    RoadLaneConnectionV2.MovementType immediateMovementType)
    {
        if (currentLane == null || currentLane.ownerSegment == null)
            return currentLane;

        List<RoadLaneDataV2> sameDirectionLanes = currentLane.ownerSegment.GetDrivingLanes(
            currentLane.fromNode,
            currentLane.toNode
        );

        if (sameDirectionLanes == null || sameDirectionLanes.Count <= 1)
            return currentLane;

        if (immediateMovementType == RoadLaneConnectionV2.MovementType.Left)
            return GetLaneForTurnPreparation(currentLane, sameDirectionLanes, immediateMovementType, false);

        if (immediateMovementType == RoadLaneConnectionV2.MovementType.Right)
            return GetLaneForTurnPreparation(currentLane, sameDirectionLanes, immediateMovementType, false);

        if (!planTurnsAhead)
            return currentLane;

        RoadLaneConnectionV2.MovementType futureMovement = FindUpcomingTurnMovement(
            lanePath,
            currentPathIndex,
            lanePlanningLookaheadSegments
        );

        if (futureMovement == RoadLaneConnectionV2.MovementType.Left ||
            futureMovement == RoadLaneConnectionV2.MovementType.Right)
        {
            return GetLaneForTurnPreparation(currentLane, sameDirectionLanes, futureMovement, true);
        }

        return currentLane;
    }

    private RoadLaneConnectionV2.MovementType FindUpcomingTurnMovement(
        List<RoadLaneDataV2> lanePath,
        int currentPathIndex,
        int maxSegmentsAhead)
    {
        if (lanePath == null || lanePath.Count < 2)
            return RoadLaneConnectionV2.MovementType.Straight;

        int checkedSegments = 0;

        for (int i = currentPathIndex + 1; i < lanePath.Count - 1; i++)
        {
            if (checkedSegments >= Mathf.Max(1, maxSegmentsAhead))
                break;

            RoadLaneDataV2 fromLane = lanePath[i];
            RoadLaneDataV2 toLane = lanePath[i + 1];

            if (fromLane == null || toLane == null)
                continue;

            float signedAngle = Vector3.SignedAngle(
                fromLane.DirectionVector.normalized,
                toLane.DirectionVector.normalized,
                Vector3.forward
            );

            RoadLaneConnectionV2.MovementType movementType = GetMovementType(signedAngle);

            if (movementType == RoadLaneConnectionV2.MovementType.Left ||
                movementType == RoadLaneConnectionV2.MovementType.Right)
            {
                return movementType;
            }

            checkedSegments++;
        }

        return RoadLaneConnectionV2.MovementType.Straight;
    }

    private RoadLaneDataV2 GetLaneForTurnPreparation(
        RoadLaneDataV2 currentLane,
        List<RoadLaneDataV2> sameDirectionLanes,
        RoadLaneConnectionV2.MovementType movementType,
        bool gradualShift)
    {
        if (currentLane == null || sameDirectionLanes == null || sameDirectionLanes.Count == 0)
            return currentLane;

        int currentIndex = currentLane.localLaneIndex;
        int minIndex = 0;
        int maxIndex = sameDirectionLanes.Count - 1;

        int targetIndex = currentIndex;

        switch (movementType)
        {
            case RoadLaneConnectionV2.MovementType.Left:
                targetIndex = gradualShift
                    ? Mathf.Min(currentIndex + 1, maxIndex)
                    : maxIndex;
                break;

            case RoadLaneConnectionV2.MovementType.Right:
                targetIndex = gradualShift
                    ? Mathf.Max(currentIndex - 1, minIndex)
                    : minIndex;
                break;

            default:
                return currentLane;
        }

        for (int i = 0; i < sameDirectionLanes.Count; i++)
        {
            RoadLaneDataV2 lane = sameDirectionLanes[i];
            if (lane != null && lane.localLaneIndex == targetIndex)
                return lane;
        }

        return currentLane;
    }

    private List<Vector3> BuildMidSegmentLaneChange(RoadLaneDataV2 fromLane, RoadLaneDataV2 toLane)
    {
        List<Vector3> points = new List<Vector3>();

        if (fromLane == null || toLane == null)
            return points;

        if (fromLane.ownerSegment != toLane.ownerSegment)
            return points;

        List<Vector3> fromPoints = GetLanePolyline(fromLane);
        List<Vector3> toPoints = GetLanePolyline(toLane);

        if (fromPoints.Count < 2 || toPoints.Count < 2)
            return points;

        Vector3 p0 = GetPointAlongPolylineNormalized(fromPoints, 0.40f);
        Vector3 p2 = GetPointAlongPolylineNormalized(toPoints, 0.72f);
        Vector3 p1 = Vector3.Lerp(p0, p2, 0.5f);

        points.Add(p0);
        AddPointIfFar(points, p1);
        AddPointIfFar(points, p2);

        return points;
    }

    private void AddLanePolyline(RoadLaneDataV2 lane, bool includeFirstPoint, bool skipLastPoint)
    {
        List<Vector3> polyline = GetLanePolyline(lane);
        if (polyline.Count == 0)
            return;

        int startIndex = includeFirstPoint ? 0 : 1;
        int endExclusive = skipLastPoint ? polyline.Count - 1 : polyline.Count;

        startIndex = Mathf.Clamp(startIndex, 0, polyline.Count);
        endExclusive = Mathf.Clamp(endExclusive, 0, polyline.Count);

        for (int i = startIndex; i < endExclusive; i++)
            AddPointIfFar(polyline[i]);
    }

    private List<Vector3> GetLanePolyline(RoadLaneDataV2 lane)
    {
        List<Vector3> result = new List<Vector3>();

        if (lane == null)
            return result;

        if (lane.sampledPoints != null && lane.sampledPoints.Count >= 2)
        {
            result.AddRange(lane.sampledPoints);
            return result;
        }

        result.Add(lane.start);
        result.Add(lane.end);
        return result;
    }

    private Vector3 GetPointAlongPolylineNormalized(List<Vector3> points, float normalizedT)
    {
        if (points == null || points.Count == 0)
            return Vector3.zero;

        if (points.Count == 1)
            return points[0];

        float totalLength = GetPolylineLength(points);
        if (totalLength < 0.0001f)
            return points[0];

        float targetDistance = Mathf.Clamp01(normalizedT) * totalLength;
        return GetPointAlongPolylineDistance(points, targetDistance);
    }

    private Vector3 GetPointAlongPolylineDistance(List<Vector3> points, float distance)
    {
        if (points == null || points.Count == 0)
            return Vector3.zero;

        if (points.Count == 1)
            return points[0];

        float remaining = Mathf.Max(0f, distance);

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
            float segLength = Vector3.Distance(a, b);

            if (segLength < 0.0001f)
                continue;

            if (remaining <= segLength)
            {
                float t = remaining / segLength;
                return Vector3.Lerp(a, b, t);
            }

            remaining -= segLength;
        }

        return points[points.Count - 1];
    }

    private float GetPolylineLength(List<Vector3> points)
    {
        if (points == null || points.Count < 2)
            return 0f;

        float length = 0f;

        for (int i = 0; i < points.Count - 1; i++)
            length += Vector3.Distance(points[i], points[i + 1]);

        return length;
    }


    private GateInfo BuildGateFromRealConnection(RoadLaneConnectionV2 connection)
    {
        if (connection == null || !connection.IsValid || connection.junctionNode == null)
            return null;

        return new GateInfo
        {
            junctionNode = connection.junctionNode,
            incomingSegment = connection.fromLane.ownerSegment,
            incomingLane = connection.fromLane,
            outgoingLane = connection.toLane,
            movementType = connection.movementType
        };
}

    private GateInfo BuildGateFromSyntheticTurn(RoadLaneDataV2 fromLane, RoadLaneDataV2 toLane)
    {
        if (fromLane == null || toLane == null)
            return null;

        RoadNodeV2 node = fromLane.toNode;
        if (node == null || node != toLane.fromNode)
            return null;

        float angle = Vector3.SignedAngle(
            fromLane.DirectionVector.normalized,
            toLane.DirectionVector.normalized,
            Vector3.forward
        );

        RoadLaneConnectionV2.MovementType movementType = GetMovementType(angle);

            return new GateInfo
            {
                junctionNode = node,
                incomingSegment = fromLane.ownerSegment,
                incomingLane = fromLane,
                outgoingLane = toLane,
                movementType = movementType
            };
    }

    private RoadLaneConnectionV2.MovementType GetMovementType(float signedAngle)
    {
        float absAngle = Mathf.Abs(signedAngle);

        if (absAngle < 20f)
            return RoadLaneConnectionV2.MovementType.Straight;

        return signedAngle > 0f
            ? RoadLaneConnectionV2.MovementType.Left
            : RoadLaneConnectionV2.MovementType.Right;
    }



    private List<Vector3> BuildSyntheticTurn(RoadLaneDataV2 fromLane, RoadLaneDataV2 toLane)
    {
        return BuildNodeAnchoredTurn(fromLane, toLane);
    }

    private List<Vector3> BuildTurnFromConnection(
    RoadLaneDataV2 fromLane,
    RoadLaneDataV2 toLane,
    RoadLaneConnectionV2 connection)
    {
        if (connection == null || connection.curvePoints == null || connection.curvePoints.Count < 2)
            return BuildNodeAnchoredTurn(fromLane, toLane);

        List<Vector3> points = new List<Vector3>();

        Vector3 inDir = fromLane != null
            ? fromLane.DirectionVector.normalized
            : Vector3.right;

        Vector3 firstCurvePoint = connection.curvePoints[0];
        Vector3 stopPoint = GetStopPointBeforeNode(fromLane, firstCurvePoint, inDir);

        points.Add(stopPoint);

        for (int i = 0; i < connection.curvePoints.Count; i++)
            AddPointIfFar(points, connection.curvePoints[i]);

        return points;
    }

    private List<Vector3> BuildNodeAnchoredTurn(RoadLaneDataV2 fromLane, RoadLaneDataV2 toLane)
    {
        List<Vector3> points = new List<Vector3>();

        if (fromLane == null || toLane == null)
            return points;

        RoadNodeV2 node = fromLane.toNode;
        if (node == null || node != toLane.fromNode)
            return points;

        GetNodeHalfExtents(node, out float halfX, out float halfY);

        const float margin = 0.02f;

        Vector3 fromAnchor = GetExitAnchor(node.transform.position, fromLane, halfX, halfY, margin);
        Vector3 toAnchor = GetEntryAnchor(node.transform.position, toLane, halfX, halfY, margin);

        Vector3 inDir = fromLane.DirectionVector.normalized;
        Vector3 outDir = toLane.DirectionVector.normalized;

        float signedAngle = Vector3.SignedAngle(inDir, outDir, Vector3.forward);
        float absAngle = Mathf.Abs(signedAngle);

        Vector3 stopPoint = GetStopPointBeforeNode(fromLane, fromAnchor, inDir);

        points.Add(stopPoint);
        AddPointIfFar(points, fromAnchor);

        if (absAngle < 20f || Vector3.Distance(fromAnchor, toAnchor) < 0.15f)
        {
            AddPointIfFar(points, Vector3.Lerp(fromAnchor, toAnchor, 0.5f));
            AddPointIfFar(points, toAnchor);
            return points;
        }

        if (TryGetAxisAlignedTurnCorner(fromAnchor, inDir, toAnchor, outDir, out Vector3 corner))
        {
            AddPointIfFar(points, corner);
            AddPointIfFar(points, toAnchor);
            return points;
        }

        AddPointIfFar(points, Vector3.Lerp(fromAnchor, toAnchor, 0.5f));
        AddPointIfFar(points, toAnchor);
        return points;
    }

    private Vector3 GetStopPointBeforeNode(RoadLaneDataV2 fromLane, Vector3 fromAnchor, Vector3 inDir)
    {
        float offset = 0.18f;

        if (fromLane != null && fromLane.ownerSegment != null)
            offset = fromLane.ownerSegment.StopLineOffset;

        Vector3 stopLinePoint;

        if (fromLane == null)
        {
            stopLinePoint = fromAnchor - inDir.normalized * offset;
        }
        else
        {
            // Точка, где визуально рисуется стоп-линия
            stopLinePoint = fromLane.end - inDir.normalized * offset;
        }

        float vehicleFrontOffset = GetProjectedHalfLengthAlong(inDir) + stopLineGap;
        return stopLinePoint - inDir.normalized * vehicleFrontOffset;
    }

    private float GetProjectedHalfLengthAlong(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return vehicleHalfLengthMin;

        direction.Normalize();

        float best = 0f;
        bool found = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled)
                continue;

            Bounds b = r.bounds;
            float projected =
                Mathf.Abs(direction.x) * b.extents.x +
                Mathf.Abs(direction.y) * b.extents.y;

            if (projected > best)
                best = projected;

            found = true;
        }

        Collider2D[] colliders2D = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders2D.Length; i++)
        {
            Collider2D c = colliders2D[i];
            if (c == null || !c.enabled)
                continue;

            Bounds b = c.bounds;
            float projected =
                Mathf.Abs(direction.x) * b.extents.x +
                Mathf.Abs(direction.y) * b.extents.y;

            if (projected > best)
                best = projected;

            found = true;
        }

        if (!found)
            return vehicleHalfLengthMin;

        return Mathf.Max(best, vehicleHalfLengthMin);
    }

    private void GetNodeHalfExtents(RoadNodeV2 node, out float halfX, out float halfY)
    {
        halfX = 0.3f;
        halfY = 0.3f;

        if (node == null)
            return;

        for (int i = 0; i < node.ConnectedSegments.Count; i++)
        {
            RoadSegmentV2 segment = node.ConnectedSegments[i];
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

    private Vector3 GetExitAnchor(Vector3 nodeCenter, RoadLaneDataV2 lane, float halfX, float halfY, float margin)
    {
        Vector3 dir = lane.DirectionVector.normalized;

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
        {
            float x = dir.x > 0f
                ? nodeCenter.x - halfX - margin
                : nodeCenter.x + halfX + margin;

            return new Vector3(x, lane.end.y, 0f);
        }
        else
        {
            float y = dir.y > 0f
                ? nodeCenter.y - halfY - margin
                : nodeCenter.y + halfY + margin;

            return new Vector3(lane.end.x, y, 0f);
        }
    }

    private Vector3 GetEntryAnchor(Vector3 nodeCenter, RoadLaneDataV2 lane, float halfX, float halfY, float margin)
    {
        Vector3 dir = lane.DirectionVector.normalized;

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
        {
            float x = dir.x > 0f
                ? nodeCenter.x + halfX + margin
                : nodeCenter.x - halfX - margin;

            return new Vector3(x, lane.start.y, 0f);
        }
        else
        {
            float y = dir.y > 0f
                ? nodeCenter.y + halfY + margin
                : nodeCenter.y - halfY - margin;

            return new Vector3(lane.start.x, y, 0f);
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





    private bool IsGateBlocked(int waypointIndex)
    {
        if (!gatedWaypointIndices.TryGetValue(waypointIndex, out GateInfo gate))
            return false;

        if (gate == null || gate.junctionNode == null || gate.incomingSegment == null)
            return false;

        if (IsIntersectionOccupiedByConflictingVehicle(gate))
            return true;

        if (IsExitLaneBlocked(gate.outgoingLane))
            return true;

        if (gate.junctionNode.UsesTrafficLight)
        {
            RoadNodeSignalV2 signal = gate.junctionNode.GetComponent<RoadNodeSignalV2>();
            if (signal == null)
                return true;

            return !signal.CanUseMovement(gate.incomingSegment, gate.movementType);
        }

        bool blockedByRight = HasVehicleFromRight(gate);
        bool blockedByOncoming = HasOncomingPriorityVehicle(gate);

        if ((blockedByRight || blockedByOncoming) && !HasDeadlockPriority(gate.junctionNode))
            return true;

        return false;
    }

    private void UpdateCurrentJunctionState()
    {
        if (currentJunctionGate == null || currentJunctionGate.junctionNode == null)
            return;

        if (!IsVehicleInsideIntersection(this, currentJunctionGate.junctionNode))
            currentJunctionGate = null;
    }

    private bool IsIntersectionOccupiedByConflictingVehicle(GateInfo myGate)
    {
        if (myGate == null || myGate.junctionNode == null)
            return false;

        for (int i = 0; i < activeVehicles.Count; i++)
        {
            RoadVehicleAgentV2 other = activeVehicles[i];

            if (other == null || other == this)
                continue;

            GateInfo otherGate = other.GetCurrentJunctionGateForNode(myGate.junctionNode);
            if (otherGate == null)
                continue;

            if (!other.IsVehicleInsideIntersection(other, myGate.junctionNode))
                continue;

            if (AreGateTrajectoriesConflicting(myGate, otherGate))
                return true;
        }

        return false;
    }

    private GateInfo GetCurrentJunctionGateForNode(RoadNodeV2 node)
    {
        if (currentJunctionGate == null || node == null)
            return null;

        return currentJunctionGate.junctionNode == node
            ? currentJunctionGate
            : null;
    }

    private bool AreGateTrajectoriesConflicting(GateInfo a, GateInfo b)
    {
        if (a == null || b == null)
            return true;

        if (a.junctionNode != b.junctionNode)
            return false;

        if (a.incomingLane == null || a.outgoingLane == null || b.incomingLane == null || b.outgoingLane == null)
            return true;

        if (AreParallelMovementsCompatible(a, b))
            return false;

        List<Vector3> pathA = BuildGateConflictPath(a);
        List<Vector3> pathB = BuildGateConflictPath(b);

        if (pathA.Count < 2 || pathB.Count < 2)
            return true;

        float clearance = Mathf.Max(0.06f, laneCheckWidth * 0.45f);
        return DoPolylinesConflict(pathA, pathB, clearance);
    }

    private bool AreParallelMovementsCompatible(GateInfo a, GateInfo b)
    {
        if (a == null || b == null)
            return false;

        if (a.incomingLane == null || a.outgoingLane == null || b.incomingLane == null || b.outgoingLane == null)
            return false;

        if (a.incomingSegment == null || b.incomingSegment == null)
            return false;

        if (a.incomingSegment != b.incomingSegment)
            return false;

        if (a.outgoingLane.ownerSegment == null || b.outgoingLane.ownerSegment == null)
            return false;

        if (a.outgoingLane.ownerSegment != b.outgoingLane.ownerSegment)
            return false;

        if (a.incomingLane == b.incomingLane)
            return false;

        if (a.outgoingLane == b.outgoingLane)
            return false;

        int incomingOrder = a.incomingLane.localLaneIndex.CompareTo(b.incomingLane.localLaneIndex);
        int outgoingOrder = a.outgoingLane.localLaneIndex.CompareTo(b.outgoingLane.localLaneIndex);

        // Если порядок полос сохраняется, траектории не пересекаются.
        if (incomingOrder == 0 || outgoingOrder == 0)
            return false;

        return incomingOrder == outgoingOrder;
    }

    private List<Vector3> BuildGateConflictPath(GateInfo gate)
    {
        List<Vector3> points = new List<Vector3>();

        if (gate == null || gate.incomingLane == null || gate.outgoingLane == null)
            return points;

        RoadLaneConnectionV2 connection = FindConnection(gate.incomingLane, gate.outgoingLane);

        if (connection != null && connection.curvePoints != null && connection.curvePoints.Count >= 2)
        {
            for (int i = 0; i < connection.curvePoints.Count; i++)
                AddPointIfFar(points, connection.curvePoints[i]);

            return points;
        }

        return BuildNodeAnchoredTurn(gate.incomingLane, gate.outgoingLane);
    }

    private bool DoPolylinesConflict(List<Vector3> a, List<Vector3> b, float clearance)
    {
        if (a == null || b == null || a.Count < 2 || b.Count < 2)
            return false;

        for (int i = 0; i < a.Count - 1; i++)
        {
            Vector3 a0 = a[i];
            Vector3 a1 = a[i + 1];

            for (int j = 0; j < b.Count - 1; j++)
            {
                Vector3 b0 = b[j];
                Vector3 b1 = b[j + 1];

                if (SegmentsIntersect2D(a0, a1, b0, b1))
                    return true;

                float distance = DistanceBetweenSegments2D(a0, a1, b0, b1);
                if (distance < clearance)
                    return true;
            }
        }

        return false;
    }

    private bool SegmentsIntersect2D(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
    {
        Vector2 p = new Vector2(a0.x, a0.y);
        Vector2 r = new Vector2(a1.x - a0.x, a1.y - a0.y);

        Vector2 q = new Vector2(b0.x, b0.y);
        Vector2 s = new Vector2(b1.x - b0.x, b1.y - b0.y);

        float rxs = r.x * s.y - r.y * s.x;
        float qpxr = (q.x - p.x) * r.y - (q.y - p.y) * r.x;

        if (Mathf.Abs(rxs) < 0.0001f && Mathf.Abs(qpxr) < 0.0001f)
            return false;

        if (Mathf.Abs(rxs) < 0.0001f)
            return false;

        float t = ((q.x - p.x) * s.y - (q.y - p.y) * s.x) / rxs;
        float u = ((q.x - p.x) * r.y - (q.y - p.y) * r.x) / rxs;

        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }

    private float DistanceBetweenSegments2D(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
    {
        float d1 = DistancePointToSegment2D(a0, b0, b1);
        float d2 = DistancePointToSegment2D(a1, b0, b1);
        float d3 = DistancePointToSegment2D(b0, a0, a1);
        float d4 = DistancePointToSegment2D(b1, a0, a1);

        return Mathf.Min(Mathf.Min(d1, d2), Mathf.Min(d3, d4));
    }

    private float DistancePointToSegment2D(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector2 p = new Vector2(point.x, point.y);
        Vector2 pa = new Vector2(a.x, a.y);
        Vector2 pb = new Vector2(b.x, b.y);

        Vector2 ab = pb - pa;
        float lenSq = ab.sqrMagnitude;

        if (lenSq < 0.0001f)
            return Vector2.Distance(p, pa);

        float t = Vector2.Dot(p - pa, ab) / lenSq;
        t = Mathf.Clamp01(t);

        Vector2 projection = pa + ab * t;
        return Vector2.Distance(p, projection);
    }

    private bool IsVehicleInsideIntersection(RoadVehicleAgentV2 vehicle, RoadNodeV2 node)
    {
        if (vehicle == null || node == null)
            return false;

        GetNodeHalfExtents(node, out float halfX, out float halfY);
        halfX += junctionOccupancyMargin;
        halfY += junctionOccupancyMargin;

        Vector3 center = node.transform.position;
        Vector3 p = vehicle.transform.position;

        return Mathf.Abs(p.x - center.x) <= halfX && Mathf.Abs(p.y - center.y) <= halfY;
    }

    private void UpdateGateWaitState(int waypointIndex, bool blocked)
    {
        if (!blocked)
        {
            ClearGateWaitState();
            return;
        }

        if (waitingGateIndex != waypointIndex)
        {
            waitingGateIndex = waypointIndex;
            waitingGateStartTime = Application.isPlaying ? Time.time : 0f;
        }
    }

    private void ClearGateWaitState()
    {
        waitingGateIndex = -1;
        waitingGateStartTime = -1f;
    }

    private float GetCurrentGateWaitTime()
    {
        if (!Application.isPlaying || waitingGateIndex < 0 || waitingGateStartTime < 0f)
            return 0f;

        return Time.time - waitingGateStartTime;
    }

    private bool IsIntersectionOccupied(RoadNodeV2 node)
    {
        if (node == null)
            return false;

        GetNodeHalfExtents(node, out float halfX, out float halfY);
        halfX += junctionOccupancyMargin;
        halfY += junctionOccupancyMargin;

        Vector3 center = node.transform.position;

        for (int i = 0; i < activeVehicles.Count; i++)
        {
            RoadVehicleAgentV2 other = activeVehicles[i];

            if (other == null || other == this)
                continue;

            Vector3 p = other.transform.position;

            if (Mathf.Abs(p.x - center.x) <= halfX && Mathf.Abs(p.y - center.y) <= halfY)
                return true;
        }

        return false;
    }

    private bool IsExitLaneBlocked(RoadLaneDataV2 lane)
    {
        if (lane == null)
            return false;

        Vector3 dir = lane.DirectionVector.normalized;
        Vector3 laneStart = lane.start;

        float clearDistance = Mathf.Max(
            exitClearDistance,
            GetProjectedHalfLengthAlong(dir) * 2f + safeDistance
        );

        float laneHalfWidth = 0.3f;
        if (lane.ownerSegment != null)
            laneHalfWidth = Mathf.Max(0.15f, lane.ownerSegment.LaneWidth * 0.45f);

        for (int i = 0; i < activeVehicles.Count; i++)
        {
            RoadVehicleAgentV2 other = activeVehicles[i];

            if (other == null || other == this)
                continue;

            Vector3 delta = other.transform.position - laneStart;
            float forward = Vector3.Dot(delta, dir);

            if (forward < -0.15f || forward > clearDistance)
                continue;

            float lateral = Mathf.Abs(dir.x * delta.y - dir.y * delta.x);
            if (lateral <= laneHalfWidth)
                return true;
        }

        return false;
    }

    private bool HasVehicleFromRight(GateInfo myGate)
    {
        if (myGate == null || myGate.junctionNode == null || myGate.incomingSegment == null)
            return false;

        Vector3 myIncomingDir = GetIncomingDirection(myGate.incomingSegment, myGate.junctionNode);

        for (int i = 0; i < activeVehicles.Count; i++)
        {
            RoadVehicleAgentV2 other = activeVehicles[i];

            if (!TryGetActiveGateAtNode(other, myGate.junctionNode, out GateInfo otherGate))
                continue;

            Vector3 otherIncomingDir = GetIncomingDirection(otherGate.incomingSegment, otherGate.junctionNode);
            float angle = Vector3.SignedAngle(myIncomingDir, otherIncomingDir, Vector3.forward);

            if (angle > 45f && angle < 135f)
                return true;
        }

        return false;
    }

    private bool HasOncomingPriorityVehicle(GateInfo myGate)
    {
        if (myGate == null || myGate.junctionNode == null || myGate.incomingSegment == null)
            return false;

        if (myGate.movementType != RoadLaneConnectionV2.MovementType.Left)
            return false;

        Vector3 myIncomingDir = GetIncomingDirection(myGate.incomingSegment, myGate.junctionNode);

        for (int i = 0; i < activeVehicles.Count; i++)
        {
            RoadVehicleAgentV2 other = activeVehicles[i];

            if (!TryGetActiveGateAtNode(other, myGate.junctionNode, out GateInfo otherGate))
                continue;

            Vector3 otherIncomingDir = GetIncomingDirection(otherGate.incomingSegment, otherGate.junctionNode);
            float absAngle = Mathf.Abs(Vector3.SignedAngle(myIncomingDir, otherIncomingDir, Vector3.forward));

            bool isOncoming = absAngle > 135f;
            if (!isOncoming)
                continue;

            if (otherGate.movementType == RoadLaneConnectionV2.MovementType.Straight ||
                otherGate.movementType == RoadLaneConnectionV2.MovementType.Right)
                return true;
        }

        return false;
    }

    private bool HasDeadlockPriority(RoadNodeV2 node)
    {
        if (node == null || !Application.isPlaying)
            return false;

        if (GetCurrentGateWaitTime() < deadlockResolveDelay)
            return false;

        int bestId = GetInstanceID();
        bool hasCompetitor = false;

        for (int i = 0; i < activeVehicles.Count; i++)
        {
            RoadVehicleAgentV2 other = activeVehicles[i];

            if (!TryGetActiveGateAtNode(other, node, out _))
                continue;

            hasCompetitor = true;

            if (other.GetInstanceID() < bestId)
                bestId = other.GetInstanceID();
        }

        return hasCompetitor && bestId == GetInstanceID();
    }

    private bool TryGetActiveGateAtNode(RoadVehicleAgentV2 vehicle, RoadNodeV2 node, out GateInfo gate)
    {
        gate = null;

        if (vehicle == null || vehicle == this || node == null)
            return false;

        if (vehicle.currentWaypointIndex >= vehicle.waypoints.Count)
            return false;

        if (!vehicle.gatedWaypointIndices.TryGetValue(vehicle.currentWaypointIndex, out gate))
            return false;

        if (gate == null || gate.junctionNode != node || gate.incomingSegment == null)
            return false;

        Vector3 gatePoint = vehicle.waypoints[vehicle.currentWaypointIndex];
        if (Vector3.Distance(vehicle.transform.position, gatePoint) > gateApproachDistance)
            return false;

        return true;
    }

    private Vector3 GetIncomingDirection(RoadSegmentV2 segment, RoadNodeV2 node)
    {
        if (segment == null || node == null)
            return Vector3.right;

        if (segment.EndNode == node && segment.StartNode != null)
            return (node.transform.position - segment.StartNode.transform.position).normalized;

        if (segment.StartNode == node && segment.EndNode != null)
            return (node.transform.position - segment.EndNode.transform.position).normalized;

        return Vector3.right;
    }

    private RoadLaneConnectionV2 FindConnection(RoadLaneDataV2 fromLane, RoadLaneDataV2 toLane)
    {
        if (fromLane == null || toLane == null)
            return null;

        foreach (RoadLaneConnectionV2 connection in fromLane.outgoingConnections)
        {
            if (connection == null || !connection.IsValid)
                continue;

            if (connection.toLane == toLane)
                return connection;
        }

        return null;
    }

    private void AddPointIfFar(List<Vector3> points, Vector3 point)
    {
        if (points == null)
            return;

        point.z = 0f;

        if (points.Count == 0)
        {
            points.Add(point);
            return;
        }

        if (Vector3.Distance(points[points.Count - 1], point) > 0.01f)
            points.Add(point);
    }

    private void AddPointIfFar(Vector3 point)
    {
        point.z = 0f;

        if (waypoints.Count == 0)
        {
            waypoints.Add(point);
            return;
        }

        if (Vector3.Distance(waypoints[waypoints.Count - 1], point) > 0.01f)
            waypoints.Add(point);
    }

    private Vector3 GetLookAheadDirection(int targetIndex)
    {
        if (targetIndex >= waypoints.Count)
            return Vector3.right;

        Vector3 from = transform.position;
        Vector3 to = waypoints[targetIndex];
        Vector3 currentDir = (to - from).normalized;

        if (currentDir.sqrMagnitude < 0.0001f)
            return Vector3.right;

        float distanceToCurrent = Vector3.Distance(from, to);

        if (distanceToCurrent < lookAheadDistance && targetIndex + 1 < waypoints.Count)
        {
            Vector3 nextDir = (waypoints[targetIndex + 1] - to).normalized;

            if (nextDir.sqrMagnitude > 0.0001f)
            {
                float t = 1f - Mathf.Clamp01(distanceToCurrent / lookAheadDistance);
                Vector3 blended = Vector3.Lerp(currentDir, nextDir, t).normalized;

                if (blended.sqrMagnitude > 0.0001f)
                    return blended;
            }
        }

        return currentDir;
    }

    private void RotateTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}