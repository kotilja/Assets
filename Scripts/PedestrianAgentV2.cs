using System.Collections.Generic;
using UnityEngine;

public class PedestrianAgentV2 : MonoBehaviour
{
    public enum PedestrianGoalType
    {
        None,
        ParkingSpot,
        DestinationPoint,
        WorldPoint
    }

    private enum JourneyPhase
    {
        Idle,
        WalkingToParking,
        RidingToDropoff,
        WalkingToDestination,
        Free
    }

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.2f;
    [SerializeField] private float arrivalDistance = 0.08f;
    [SerializeField] private float rotationSpeed = 720f;

    [Header("Debug")]
    [SerializeField] private bool drawPathGizmos = true;
    [SerializeField] private Color pathColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private float gizmoRadius = 0.05f;

    private PedestrianNetworkV2 pedestrianNetwork;
    private PedestrianSpawnerV2 ownerSpawner;

    private PedestrianGoalType goalType = PedestrianGoalType.None;
    private JourneyPhase journeyPhase = JourneyPhase.Idle;

    private ParkingSpotV2 targetParkingSpot;
    private ParkingSpotV2 dropoffParkingSpot;
    private DestinationPointV2 targetDestination;
    private Vector3 targetWorldPoint;
    private BuildingZoneV2 homeBuilding;
    private BuildingZoneV2 officeBuilding;
    private BuildingZoneV2 currentTargetBuilding;
    private bool commuteCycleEnabled = false;
    private bool nextLegTargetsOffice = false;
    private bool firstCommuteLeg = false;

    private RoadVehicleSpawnerV2 vehicleSpawner;
    private RoadVehicleAgentV2 activeRideVehicle;

    private readonly List<Vector3> currentPath = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private bool initialized = false;
    private bool arrived = false;

    public bool IsInitialized => initialized;
    public bool HasArrived => arrived;
    public PedestrianGoalType GoalType => goalType;
    public IReadOnlyList<Vector3> CurrentPath => currentPath;

    public void InitializeFree(PedestrianNetworkV2 network, PedestrianSpawnerV2 spawner = null)
    {
        transform.SetParent(null, true);
        pedestrianNetwork = network;
        ownerSpawner = spawner;
        vehicleSpawner = null;
        activeRideVehicle = null;
        commuteCycleEnabled = false;
        homeBuilding = null;
        officeBuilding = null;
        currentTargetBuilding = null;
        targetParkingSpot = null;
        dropoffParkingSpot = null;
        targetDestination = null;
        firstCommuteLeg = false;
        goalType = PedestrianGoalType.None;
        journeyPhase = JourneyPhase.Free;
        initialized = true;
        arrived = false;
        ClearPath();
    }

    public void InitializeToParking(PedestrianNetworkV2 network, ParkingSpotV2 parkingSpot, PedestrianSpawnerV2 spawner = null)
    {
        transform.SetParent(null, true);
        pedestrianNetwork = network;
        ownerSpawner = spawner;
        vehicleSpawner = null;
        activeRideVehicle = null;
        commuteCycleEnabled = false;
        homeBuilding = null;
        officeBuilding = null;
        currentTargetBuilding = null;
        targetParkingSpot = parkingSpot;
        dropoffParkingSpot = null;
        targetDestination = null;
        firstCommuteLeg = false;
        goalType = PedestrianGoalType.ParkingSpot;
        journeyPhase = JourneyPhase.WalkingToParking;
        initialized = true;
        arrived = false;

        RebuildPathToParking(targetParkingSpot);
    }

    public void InitializeToDestination(PedestrianNetworkV2 network, DestinationPointV2 destination, PedestrianSpawnerV2 spawner = null)
    {
        transform.SetParent(null, true);
        pedestrianNetwork = network;
        ownerSpawner = spawner;
        vehicleSpawner = null;
        activeRideVehicle = null;
        commuteCycleEnabled = false;
        homeBuilding = null;
        officeBuilding = null;
        currentTargetBuilding = null;
        targetDestination = destination;
        targetParkingSpot = null;
        dropoffParkingSpot = null;
        firstCommuteLeg = false;
        goalType = PedestrianGoalType.DestinationPoint;
        journeyPhase = JourneyPhase.WalkingToDestination;
        initialized = true;
        arrived = false;

        RebuildPathToDestination(targetDestination);
    }

    public void InitializeToWorldPoint(PedestrianNetworkV2 network, Vector3 worldPoint, PedestrianSpawnerV2 spawner = null)
    {
        transform.SetParent(null, true);
        pedestrianNetwork = network;
        ownerSpawner = spawner;
        vehicleSpawner = null;
        activeRideVehicle = null;
        commuteCycleEnabled = false;
        homeBuilding = null;
        officeBuilding = null;
        currentTargetBuilding = null;
        targetWorldPoint = worldPoint;
        targetWorldPoint.z = 0f;
        targetParkingSpot = null;
        dropoffParkingSpot = null;
        targetDestination = null;
        firstCommuteLeg = false;
        goalType = PedestrianGoalType.WorldPoint;
        journeyPhase = JourneyPhase.WalkingToDestination;
        initialized = true;
        arrived = false;

        RebuildPathToWorldPoint();
    }

    public void InitializeTrip(
        PedestrianNetworkV2 network,
        ParkingSpotV2 pickupParkingSpot,
        RoadVehicleSpawnerV2 roadVehicleSpawner,
        ParkingSpotV2 targetParkingSpot,
        DestinationPointV2 destination,
        PedestrianSpawnerV2 spawner = null)
    {
        transform.SetParent(null, true);
        pedestrianNetwork = network;
        ownerSpawner = spawner;
        vehicleSpawner = roadVehicleSpawner;
        this.targetParkingSpot = pickupParkingSpot;
        dropoffParkingSpot = targetParkingSpot;
        targetDestination = destination;
        goalType = PedestrianGoalType.DestinationPoint;
        journeyPhase = JourneyPhase.WalkingToParking;
        activeRideVehicle = null;
        firstCommuteLeg = false;
        commuteCycleEnabled = false;
        homeBuilding = null;
        officeBuilding = null;
        currentTargetBuilding = null;
        initialized = true;
        arrived = false;

        RebuildPathToParking(this.targetParkingSpot);
    }

    public void InitializeCommuteCycle(
        PedestrianNetworkV2 network,
        RoadVehicleSpawnerV2 roadVehicleSpawner,
        BuildingZoneV2 home,
        BuildingZoneV2 office,
        PedestrianSpawnerV2 spawner = null)
    {
        transform.SetParent(null, true);
        pedestrianNetwork = network;
        ownerSpawner = spawner;
        vehicleSpawner = roadVehicleSpawner;
        activeRideVehicle = null;
        homeBuilding = home;
        officeBuilding = office;
        currentTargetBuilding = homeBuilding;
        commuteCycleEnabled = homeBuilding != null && officeBuilding != null;
        nextLegTargetsOffice = true;
        firstCommuteLeg = true;
        targetParkingSpot = null;
        dropoffParkingSpot = null;
        targetDestination = null;
        targetWorldPoint = homeBuilding != null
            ? homeBuilding.GetClosestPointOnPerimeter(transform.position)
            : transform.position;
        goalType = PedestrianGoalType.WorldPoint;
        journeyPhase = JourneyPhase.WalkingToDestination;
        initialized = true;
        arrived = false;

        StartCommuteLeg(currentTargetBuilding);
    }

    public void RebuildPath()
    {
        switch (journeyPhase)
        {
            case JourneyPhase.WalkingToParking:
                RebuildPathToParking(targetParkingSpot);
                break;

            case JourneyPhase.WalkingToDestination:
                if (goalType == PedestrianGoalType.WorldPoint)
                    RebuildPathToWorldPoint();
                else
                    RebuildPathToDestination(targetDestination);
                break;

            case JourneyPhase.RidingToDropoff:
                ClearPath();
                break;

            case JourneyPhase.Free:
                ClearPath();
                break;

            default:
                switch (goalType)
                {
                    case PedestrianGoalType.ParkingSpot:
                        RebuildPathToParking(targetParkingSpot);
                        break;

                    case PedestrianGoalType.DestinationPoint:
                        RebuildPathToDestination(targetDestination);
                        break;

                    case PedestrianGoalType.WorldPoint:
                        RebuildPathToWorldPoint();
                        break;
                }
                break;
        }
    }

    private void Update()
    {
        if (!initialized || arrived)
            return;

        if (journeyPhase == JourneyPhase.RidingToDropoff)
        {
            if (activeRideVehicle == null)
                BeginWalkToDestination();

            return;
        }

        if (currentPath.Count == 0 || currentWaypointIndex >= currentPath.Count)
        {
            if (TryRecoverMissingPath())
                return;

            arrived = true;
            OnArrived();
            return;
        }

        if (currentPath.Count < 2 && ShouldKeepWaitingForNetworkGoal())
        {
            if (TryRecoverMissingPath())
                return;
        }

        Vector3 target = currentPath[currentWaypointIndex];
        target.z = 0f;

        Vector3 current = transform.position;
        current.z = 0f;

        Vector3 toTarget = target - current;
        float distance = toTarget.magnitude;

        if (distance <= arrivalDistance)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= currentPath.Count)
            {
                arrived = true;
                OnArrived();
            }

            return;
        }

        Vector3 direction = toTarget / Mathf.Max(distance, 0.0001f);

        float step = walkSpeed * Time.deltaTime;
        Vector3 next = Vector3.MoveTowards(current, target, step);
        next.z = 0f;
        transform.position = next;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void RebuildPathToParking(ParkingSpotV2 parkingSpot)
    {
        ClearPath();

        if (pedestrianNetwork == null || parkingSpot == null)
            return;

        List<Vector3> graphPath = pedestrianNetwork.FindPath(transform.position, parkingSpot);
        SetPath(graphPath, parkingSpot.PedestrianAnchorPoint, false);
    }

    private void RebuildPathToDestination(DestinationPointV2 destination)
    {
        ClearPath();

        if (pedestrianNetwork == null || destination == null)
            return;

        List<Vector3> graphPath = pedestrianNetwork.FindPath(transform.position, destination);
        SetPath(graphPath, destination.Position, false);
    }

    private void RebuildPathToWorldPoint()
    {
        ClearPath();

        if (pedestrianNetwork == null)
            return;

        List<Vector3> graphPath = pedestrianNetwork.FindPath(transform.position, targetWorldPoint);
        SetPath(graphPath, targetWorldPoint, true);
    }

    private void SetPath(List<Vector3> graphPath, Vector3 fallbackGoal, bool appendFallbackGoal)
    {
        ClearPath();

        Vector3 start = transform.position;
        start.z = 0f;

        AddPointIfFar(currentPath, start);

        if (graphPath != null && graphPath.Count > 0)
        {
            for (int i = 0; i < graphPath.Count; i++)
                AddPointIfFar(currentPath, graphPath[i]);

            if (appendFallbackGoal)
            {
                fallbackGoal.z = 0f;
                if (currentPath.Count == 0 || Vector3.Distance(currentPath[currentPath.Count - 1], fallbackGoal) > 0.02f)
                    AddPointIfFar(currentPath, fallbackGoal);
            }
        }
        else
        {
            if (appendFallbackGoal)
            {
                fallbackGoal.z = 0f;
                AddPointIfFar(currentPath, fallbackGoal);
            }
        }
    }

    private void ClearPath()
    {
        currentPath.Clear();
        currentWaypointIndex = 0;
        arrived = false;
    }

    private bool TryRecoverMissingPath()
    {
        if (goalType == PedestrianGoalType.WorldPoint || journeyPhase == JourneyPhase.Free)
            return false;

        Vector3 finalGoal = GetFinalGoalPosition();
        if (finalGoal == Vector3.zero)
            return false;

        if (Vector3.Distance(transform.position, finalGoal) <= arrivalDistance)
            return false;

        RebuildPath();
        return currentPath.Count < 2;
    }

    private bool ShouldKeepWaitingForNetworkGoal()
    {
        return goalType == PedestrianGoalType.ParkingSpot ||
               goalType == PedestrianGoalType.DestinationPoint ||
               journeyPhase == JourneyPhase.WalkingToParking ||
               journeyPhase == JourneyPhase.WalkingToDestination;
    }

    private Vector3 GetFinalGoalPosition()
    {
        switch (journeyPhase)
        {
            case JourneyPhase.WalkingToParking:
                if (targetParkingSpot != null)
                    return targetParkingSpot.PedestrianAnchorPoint;
                break;

            case JourneyPhase.WalkingToDestination:
                if (goalType == PedestrianGoalType.WorldPoint)
                    return targetWorldPoint;

                if (targetDestination != null)
                    return targetDestination.Position;
                break;
        }

        switch (goalType)
        {
            case PedestrianGoalType.ParkingSpot:
                if (targetParkingSpot != null)
                    return targetParkingSpot.PedestrianAnchorPoint;
                break;

            case PedestrianGoalType.DestinationPoint:
                if (targetDestination != null)
                    return targetDestination.Position;
                break;

            case PedestrianGoalType.WorldPoint:
                return targetWorldPoint;
        }

        return Vector3.zero;
    }

    private void AddPointIfFar(List<Vector3> points, Vector3 point)
    {
        point.z = 0f;

        if (points.Count == 0)
        {
            points.Add(point);
            return;
        }

        if (Vector3.Distance(points[points.Count - 1], point) > 0.02f)
            points.Add(point);
    }

    private void OnArrived()
    {
        if (commuteCycleEnabled && currentTargetBuilding != null && journeyPhase == JourneyPhase.WalkingToDestination)
        {
            AdvanceCommuteCycle();
            return;
        }

        switch (journeyPhase)
        {
            case JourneyPhase.WalkingToParking:
                BeginRide();
                break;

            case JourneyPhase.WalkingToDestination:
                Debug.Log($"Pedestrian arrived at destination: {targetDestination}", this);
                break;

            case JourneyPhase.Free:
                break;

            default:
                switch (goalType)
                {
                    case PedestrianGoalType.ParkingSpot:
                        Debug.Log($"Pedestrian arrived at parking spot: {targetParkingSpot}", this);
                        break;

                    case PedestrianGoalType.DestinationPoint:
                        Debug.Log($"Pedestrian arrived at destination: {targetDestination}", this);
                        break;

                    case PedestrianGoalType.WorldPoint:
                        Debug.Log("Pedestrian arrived at world point.", this);
                        break;
                }
                break;
        }
    }

    private void BeginRide()
    {
        if (vehicleSpawner == null || targetParkingSpot == null || dropoffParkingSpot == null || targetDestination == null)
        {
            BeginWalkToDestination();
            return;
        }

        if (!dropoffParkingSpot.CanUse())
        {
            Debug.LogWarning("Trip dropoff parking is occupied, skipping ride.", this);
            BeginWalkToDestination();
            return;
        }

        RoadVehicleAgentV2 vehicle;
        bool spawned = vehicleSpawner.TrySpawnTripFromParking(
            targetParkingSpot,
            dropoffParkingSpot,
            OnRideCompleted,
            out vehicle
        );

        if (!spawned || vehicle == null)
        {
            BeginWalkToDestination();
            return;
        }

        activeRideVehicle = vehicle;
        journeyPhase = JourneyPhase.RidingToDropoff;
        ClearPath();

        transform.SetParent(activeRideVehicle.transform, true);
        transform.localPosition = Vector3.zero;
    }

    private void OnRideCompleted(RoadVehicleAgentV2 vehicle)
    {
        if (vehicle != null && transform.parent == vehicle.transform)
            transform.SetParent(null, true);

        activeRideVehicle = null;

        if (dropoffParkingSpot != null)
            transform.position = dropoffParkingSpot.PedestrianAnchorPoint;

        BeginWalkToDestination();
    }

    private void BeginWalkToDestination()
    {
        if (transform.parent != null)
            transform.SetParent(null, true);

        activeRideVehicle = null;
        journeyPhase = JourneyPhase.WalkingToDestination;
        arrived = false;

        if (goalType == PedestrianGoalType.WorldPoint)
            RebuildPathToWorldPoint();
        else
            RebuildPathToDestination(targetDestination);
    }

    private void StartCommuteLeg(BuildingZoneV2 targetBuilding)
    {
        currentTargetBuilding = targetBuilding;
        if (currentTargetBuilding == null)
        {
            InitializeFree(pedestrianNetwork, ownerSpawner);
            return;
        }

        targetWorldPoint = currentTargetBuilding.GetClosestPointOnPerimeter(transform.position);
        targetWorldPoint.z = 0f;
        goalType = PedestrianGoalType.WorldPoint;
        targetDestination = null;
        targetParkingSpot = null;
        dropoffParkingSpot = null;
        arrived = false;

        if (TryStartDriveToBuilding(currentTargetBuilding))
            return;

        journeyPhase = JourneyPhase.WalkingToDestination;
        RebuildPathToWorldPoint();
    }

    private void AdvanceCommuteCycle()
    {
        BuildingZoneV2 nextTarget = nextLegTargetsOffice ? officeBuilding : homeBuilding;
        nextLegTargetsOffice = !nextLegTargetsOffice;
        firstCommuteLeg = false;
        StartCommuteLeg(nextTarget);
    }

    private bool TryStartDriveToBuilding(BuildingZoneV2 targetBuilding)
    {
        if (vehicleSpawner == null || targetBuilding == null)
            return false;

        if (!TryGetBestParkingForBuilding(targetBuilding, out ParkingSpotV2 bestParkingSpot, out float drivingScore))
            return false;

        Vector3 targetEntrance = targetBuilding.GetClosestPointOnPerimeter(transform.position);
        float walkingScore = GetPedestrianTravelScore(transform.position, targetEntrance);
        if (!firstCommuteLeg && walkingScore <= drivingScore)
            return false;

        if (!vehicleSpawner.TrySpawnTripToParkingFromWorld(transform.position, bestParkingSpot, OnCommuteRideCompleted, out RoadVehicleAgentV2 vehicle))
            return false;

        activeRideVehicle = vehicle;
        dropoffParkingSpot = bestParkingSpot;
        journeyPhase = JourneyPhase.RidingToDropoff;
        ClearPath();
        transform.SetParent(activeRideVehicle.transform, true);
        transform.localPosition = Vector3.zero;
        return true;
    }

    private bool TryGetBestParkingForBuilding(BuildingZoneV2 targetBuilding, out ParkingSpotV2 bestParkingSpot, out float bestScore)
    {
        bestParkingSpot = null;
        bestScore = float.MaxValue;

        if (vehicleSpawner == null || targetBuilding == null)
            return false;

        ParkingSpotV2[] parkingSpots = FindObjectsByType<ParkingSpotV2>(FindObjectsSortMode.None);
        if (parkingSpots == null || parkingSpots.Length == 0)
            return false;

        for (int i = 0; i < parkingSpots.Length; i++)
        {
            ParkingSpotV2 parkingSpot = parkingSpots[i];
            if (parkingSpot == null || !parkingSpot.CanUse())
                continue;

            if (!vehicleSpawner.TryGetTripPathToParkingFromWorld(transform.position, parkingSpot, out List<RoadLaneDataV2> lanePath))
                continue;

            float score = GetLanePathLength(lanePath);
            Vector3 entrancePoint = targetBuilding.GetClosestPointOnPerimeter(parkingSpot.PedestrianAnchorPoint);
            score += GetPedestrianTravelScore(parkingSpot.PedestrianAnchorPoint, entrancePoint);

            if (score >= bestScore)
                continue;

            bestScore = score;
            bestParkingSpot = parkingSpot;
        }

        return bestParkingSpot != null;
    }

    private float GetPedestrianTravelScore(Vector3 startPosition, Vector3 endPosition)
    {
        if (pedestrianNetwork == null)
            return Vector3.Distance(startPosition, endPosition);

        List<Vector3> path = pedestrianNetwork.FindPath(startPosition, endPosition);
        if (path == null || path.Count < 2)
            return Vector3.Distance(startPosition, endPosition);

        return GetPolylineLength(path);
    }

    private float GetLanePathLength(List<RoadLaneDataV2> lanePath)
    {
        if (lanePath == null || lanePath.Count == 0)
            return 0f;

        float length = 0f;

        for (int i = 0; i < lanePath.Count; i++)
        {
            RoadLaneDataV2 lane = lanePath[i];
            if (lane == null)
                continue;

            length += Vector3.Distance(lane.start, lane.end);
        }

        return length;
    }

    private float GetPolylineLength(List<Vector3> polyline)
    {
        if (polyline == null || polyline.Count < 2)
            return 0f;

        float length = 0f;

        for (int i = 0; i < polyline.Count - 1; i++)
            length += Vector3.Distance(polyline[i], polyline[i + 1]);

        return length;
    }

    private void OnCommuteRideCompleted(RoadVehicleAgentV2 vehicle)
    {
        if (vehicle != null && transform.parent == vehicle.transform)
            transform.SetParent(null, true);

        activeRideVehicle = null;

        if (dropoffParkingSpot != null)
            transform.position = dropoffParkingSpot.PedestrianAnchorPoint;
        else if (vehicle != null)
            transform.position = new Vector3(vehicle.transform.position.x, vehicle.transform.position.y, 0f);

        journeyPhase = JourneyPhase.WalkingToDestination;
        RebuildPathToWorldPoint();
    }

    private void OnDestroy()
    {
        if (homeBuilding != null)
            homeBuilding.ReleaseSlot();

        if (officeBuilding != null)
            officeBuilding.ReleaseSlot();

        if (ownerSpawner != null)
            ownerSpawner.NotifyAgentDestroyed(this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(transform.position, gizmoRadius);

        if (!drawPathGizmos || currentPath == null || currentPath.Count == 0)
            return;

        Gizmos.color = pathColor;

        for (int i = 0; i < currentPath.Count; i++)
            Gizmos.DrawSphere(currentPath[i], gizmoRadius * 0.7f);

        for (int i = 0; i < currentPath.Count - 1; i++)
            Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
    }
#endif
}
