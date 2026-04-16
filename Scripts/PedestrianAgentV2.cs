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
    private ParkingSpotV2 targetParkingSpot;
    private DestinationPointV2 targetDestination;
    private Vector3 targetWorldPoint;

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
        pedestrianNetwork = network;
        ownerSpawner = spawner;
        goalType = PedestrianGoalType.None;
        initialized = true;
        arrived = false;
        currentPath.Clear();
        currentWaypointIndex = 0;
    }

    public void InitializeToParking(PedestrianNetworkV2 network, ParkingSpotV2 parkingSpot, PedestrianSpawnerV2 spawner = null)
    {
        pedestrianNetwork = network;
        ownerSpawner = spawner;
        targetParkingSpot = parkingSpot;
        targetDestination = null;
        goalType = PedestrianGoalType.ParkingSpot;
        initialized = true;
        arrived = false;

        RebuildPathToParking();
    }

    public void InitializeToDestination(PedestrianNetworkV2 network, DestinationPointV2 destination, PedestrianSpawnerV2 spawner = null)
    {
        pedestrianNetwork = network;
        ownerSpawner = spawner;
        targetDestination = destination;
        targetParkingSpot = null;
        goalType = PedestrianGoalType.DestinationPoint;
        initialized = true;
        arrived = false;

        RebuildPathToDestination();
    }

    public void InitializeToWorldPoint(PedestrianNetworkV2 network, Vector3 worldPoint, PedestrianSpawnerV2 spawner = null)
    {
        pedestrianNetwork = network;
        ownerSpawner = spawner;
        targetWorldPoint = worldPoint;
        targetWorldPoint.z = 0f;
        targetParkingSpot = null;
        targetDestination = null;
        goalType = PedestrianGoalType.WorldPoint;
        initialized = true;
        arrived = false;

        RebuildPathToWorldPoint();
    }

    public void RebuildPath()
    {
        switch (goalType)
        {
            case PedestrianGoalType.ParkingSpot:
                RebuildPathToParking();
                break;

            case PedestrianGoalType.DestinationPoint:
                RebuildPathToDestination();
                break;

            case PedestrianGoalType.WorldPoint:
                RebuildPathToWorldPoint();
                break;
        }
    }

    private void Update()
    {
        if (!initialized || arrived)
            return;

        if (currentPath.Count == 0 || currentWaypointIndex >= currentPath.Count)
        {
            arrived = true;
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

    private void RebuildPathToParking()
    {
        currentPath.Clear();
        currentWaypointIndex = 0;
        arrived = false;

        if (pedestrianNetwork == null || targetParkingSpot == null)
            return;

        List<Vector3> graphPath = pedestrianNetwork.FindPath(transform.position, targetParkingSpot.PedestrianAnchorPoint);
        SetPath(graphPath, targetParkingSpot.PedestrianAnchorPoint);
    }

    private void RebuildPathToDestination()
    {
        currentPath.Clear();
        currentWaypointIndex = 0;
        arrived = false;

        if (pedestrianNetwork == null || targetDestination == null)
            return;

        List<Vector3> graphPath = pedestrianNetwork.FindPath(transform.position, targetDestination.Position);
        SetPath(graphPath, targetDestination.Position);
    }

    private void RebuildPathToWorldPoint()
    {
        currentPath.Clear();
        currentWaypointIndex = 0;
        arrived = false;

        if (pedestrianNetwork == null)
            return;

        List<Vector3> graphPath = pedestrianNetwork.FindPath(transform.position, targetWorldPoint);
        SetPath(graphPath, targetWorldPoint);
    }

    private void SetPath(List<Vector3> graphPath, Vector3 fallbackGoal)
    {
        currentPath.Clear();
        currentWaypointIndex = 0;

        Vector3 start = transform.position;
        start.z = 0f;

        AddPointIfFar(currentPath, start);

        if (graphPath != null && graphPath.Count > 0)
        {
            for (int i = 0; i < graphPath.Count; i++)
                AddPointIfFar(currentPath, graphPath[i]);
        }
        else
        {
            fallbackGoal.z = 0f;
            AddPointIfFar(currentPath, fallbackGoal);
        }
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
    }

    private void OnDestroy()
    {
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