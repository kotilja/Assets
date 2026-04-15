using System.Collections.Generic;
using UnityEngine;

public class RoadVehicleAgentV2 : MonoBehaviour
{
    private class GateInfo
    {
        public RoadNodeV2 junctionNode;
        public RoadSegmentV2 incomingSegment;
        public RoadLaneConnectionV2.MovementType movementType;
    }

    [SerializeField] private float speed = 3f;
    [SerializeField] private float lookAheadDistance = 0.25f;
    [SerializeField] private float safeDistance = 0.45f;
    [SerializeField] private float laneCheckWidth = 0.18f;
    [SerializeField] private float vehicleHalfLengthMin = 0.18f;
    [SerializeField] private float stopLineGap = 0.03f;

    private static readonly List<RoadVehicleAgentV2> activeVehicles = new List<RoadVehicleAgentV2>();

    private readonly List<Vector3> waypoints = new List<Vector3>();
    private readonly Dictionary<int, GateInfo> gatedWaypointIndices = new Dictionary<int, GateInfo>();

    private int currentWaypointIndex;
    private bool isInitialized;

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

        BuildWaypointPath(actualInitialLane, lanePath);

        if (waypoints.Count < 2)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = waypoints[0];
        RotateTowards(waypoints[1] - waypoints[0]);

        currentWaypointIndex = 1;
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        if (currentWaypointIndex >= waypoints.Count)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 target = waypoints[currentWaypointIndex];
        Vector3 currentPosition = transform.position;

        Vector3 direction = target - currentPosition;
        if (direction.sqrMagnitude > 0.0001f)
            RotateTowards(GetLookAheadDirection(currentWaypointIndex));

        float maxMove = speed * Time.deltaTime;
        maxMove = Mathf.Min(maxMove, GetAllowedMoveDistance(currentPosition, direction.normalized, maxMove));

        transform.position = Vector3.MoveTowards(
            currentPosition,
            target,
            maxMove
        );

        if (Vector3.Distance(transform.position, target) < 0.01f)
        {
            if (IsGateBlocked(currentWaypointIndex))
                return;

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

    private void BuildWaypointPath(RoadLaneDataV2 initialLane, List<RoadLaneDataV2> lanePath)
    {
        if (lanePath == null || lanePath.Count == 0 || initialLane == null)
            return;

        AddPointIfFar(initialLane.start);

        RoadLaneDataV2 activeLane = initialLane;

        for (int i = 0; i < lanePath.Count; i++)
        {
            bool hasNext = i < lanePath.Count - 1;

            if (!hasNext)
            {
                AddPointIfFar(activeLane.end);
                continue;
            }

            RoadLaneDataV2 plannedNextLane = lanePath[i + 1];

            float signedAngle = Vector3.SignedAngle(
                activeLane.DirectionVector.normalized,
                plannedNextLane.DirectionVector.normalized,
                Vector3.forward
            );

            RoadLaneConnectionV2.MovementType movementType = GetMovementType(signedAngle);

            RoadLaneDataV2 requiredTurnLane = GetRequiredTurnLane(activeLane, movementType);

            if (requiredTurnLane != null && requiredTurnLane != activeLane)
            {
                List<Vector3> laneChangePoints = BuildMidSegmentLaneChange(activeLane, requiredTurnLane);

                for (int j = 0; j < laneChangePoints.Count; j++)
                    AddPointIfFar(laneChangePoints[j]);

                activeLane = requiredTurnLane;
            }

            RoadLaneConnectionV2 connection = FindConnection(activeLane, plannedNextLane);

            int gateIndex = waypoints.Count;

            GateInfo gate = connection != null
                ? BuildGateFromRealConnection(connection)
                : BuildGateFromSyntheticTurn(activeLane, plannedNextLane);

            if (gate != null)
                gatedWaypointIndices[gateIndex] = gate;

            List<Vector3> turnPoints = BuildNodeAnchoredTurn(activeLane, plannedNextLane);

            for (int j = 0; j < turnPoints.Count; j++)
                AddPointIfFar(turnPoints[j]);

            activeLane = plannedNextLane;
        }
    }

    private RoadLaneDataV2 GetRequiredTurnLane(
    RoadLaneDataV2 currentLane,
    RoadLaneConnectionV2.MovementType movementType)
    {
        if (currentLane == null || currentLane.ownerSegment == null)
            return currentLane;

        List<RoadLaneDataV2> sameDirectionLanes = currentLane.ownerSegment.GetDrivingLanes(
            currentLane.fromNode,
            currentLane.toNode
        );

        if (sameDirectionLanes == null || sameDirectionLanes.Count <= 1)
            return currentLane;

        RoadLaneDataV2 best = currentLane;

        switch (movementType)
        {
            case RoadLaneConnectionV2.MovementType.Left:
                for (int i = 0; i < sameDirectionLanes.Count; i++)
                {
                    RoadLaneDataV2 lane = sameDirectionLanes[i];
                    if (lane != null && lane.localLaneIndex > best.localLaneIndex)
                        best = lane;
                }
                return best;

            case RoadLaneConnectionV2.MovementType.Right:
                for (int i = 0; i < sameDirectionLanes.Count; i++)
                {
                    RoadLaneDataV2 lane = sameDirectionLanes[i];
                    if (lane != null && lane.localLaneIndex < best.localLaneIndex)
                        best = lane;
                }
                return best;

            case RoadLaneConnectionV2.MovementType.Straight:
            default:
                return currentLane;
        }
    }

    private List<Vector3> BuildMidSegmentLaneChange(RoadLaneDataV2 fromLane, RoadLaneDataV2 toLane)
    {
        List<Vector3> points = new List<Vector3>();

        if (fromLane == null || toLane == null)
            return points;

        if (fromLane.ownerSegment != toLane.ownerSegment)
            return points;

        Vector3 p0 = Vector3.Lerp(fromLane.start, fromLane.end, 0.40f);
        Vector3 p2 = Vector3.Lerp(toLane.start, toLane.end, 0.72f);
        Vector3 p1 = Vector3.Lerp(p0, p2, 0.5f);

        points.Add(p0);
        AddPointIfFar(points, p1);
        AddPointIfFar(points, p2);

        return points;
    }




    private GateInfo BuildGateFromRealConnection(RoadLaneConnectionV2 connection)
    {
        if (connection == null || !connection.IsValid || connection.junctionNode == null)
            return null;

        return new GateInfo
        {
            junctionNode = connection.junctionNode,
            incomingSegment = connection.fromLane.ownerSegment,
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

        RoadNodeSignalV2 signal = gate.junctionNode.GetComponent<RoadNodeSignalV2>();
        if (signal == null)
            return false;

        return !signal.CanUseMovement(gate.incomingSegment, gate.movementType);
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
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}