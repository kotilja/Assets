using System.Collections.Generic;
using UnityEngine;

public class VehicleAgent : MonoBehaviour
{
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float acceleration = 4f;
    [SerializeField] private float braking = 8f;
    [SerializeField] private float safeDistance = 1.0f;
    [SerializeField] private float lookAheadDistance = 0.2f;

    private List<LanePath> route = new List<LanePath>();
    private int currentLaneIndex;
    private float distanceOnLane;
    private float currentSpeed;
    private LanePath currentLane;
    private bool isInitialized;

    private TrafficStatsManager.FlowDirection flowDirection = TrafficStatsManager.FlowDirection.Unknown;
    private float totalWaitTime;

    private TrafficPoint.PointType destinationType = TrafficPoint.PointType.Work;
    private SpriteRenderer spriteRenderer;

    public float DistanceOnLane => distanceOnLane;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(List<LanePath> newRoute, TrafficPoint.PointType targetType)
    {
        if (newRoute == null || newRoute.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        route = new List<LanePath>(newRoute);
        currentLaneIndex = 0;
        distanceOnLane = 0f;
        currentSpeed = 0f;
        totalWaitTime = 0f;
        destinationType = targetType;

        ApplyDestinationColor();

        SetCurrentLane(route[currentLaneIndex]);
        transform.position = currentLane.GetPositionAtDistance(distanceOnLane);
        UpdateTransformPositionAndRotation();

        if (TrafficStatsManager.Instance != null)
        {
            flowDirection = TrafficStatsManager.Instance.GetDirectionForStartLane(route[0]);
            TrafficStatsManager.Instance.ReportVehicleSpawned(flowDirection);
        }

        isInitialized = true;
    }

    private void ApplyDestinationColor()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            return;

        switch (destinationType)
        {
            case TrafficPoint.PointType.Residential:
                spriteRenderer.color = new Color(0.2f, 1f, 0.2f);
                break;

            case TrafficPoint.PointType.Work:
                spriteRenderer.color = new Color(0.2f, 0.5f, 1f);
                break;

            case TrafficPoint.PointType.Commercial:
                spriteRenderer.color = new Color(1f, 0.8f, 0.2f);
                break;
        }
    }

    private void Update()
    {
        if (!isInitialized || currentLane == null)
            return;

        float desiredSpeed = maxSpeed;
        float hardMaxDistanceThisFrame = float.PositiveInfinity;

        VehicleAgent vehicleAhead = currentLane.GetVehicleAhead(this);
        if (vehicleAhead != null)
        {
            float gapToVehicle = vehicleAhead.DistanceOnLane - distanceOnLane - safeDistance;
            desiredSpeed = Mathf.Min(desiredSpeed, CalculateTargetSpeedForStoppingDistance(gapToVehicle));
        }

        bool hasNextLane = currentLaneIndex + 1 < route.Count;

        if (hasNextLane)
        {
            LanePath nextLane = route[currentLaneIndex + 1];

            if (!CanEnterNextLane(nextLane))
            {
                float stopDistance = GetStopDistanceOnCurrentLane();
                float gapToStopLine = stopDistance - distanceOnLane;

                desiredSpeed = Mathf.Min(
                    desiredSpeed,
                    CalculateTargetSpeedForStoppingDistance(gapToStopLine)
                );

                hardMaxDistanceThisFrame = Mathf.Max(distanceOnLane, stopDistance);
            }
        }

        UpdateSpeed(desiredSpeed);
        UpdateWaitTime();

        float moveDistance = currentSpeed * Time.deltaTime;
        float targetDistance = distanceOnLane + moveDistance;

        if (hardMaxDistanceThisFrame < float.PositiveInfinity)
            targetDistance = Mathf.Min(targetDistance, hardMaxDistanceThisFrame);

        if (targetDistance < currentLane.TotalLength)
        {
            distanceOnLane = targetDistance;
            UpdateTransformPositionAndRotation();
            return;
        }

        float remainingDistance = targetDistance - currentLane.TotalLength;
        hasNextLane = currentLaneIndex + 1 < route.Count;

        if (!hasNextLane)
        {
            Arrive();
            return;
        }

        LanePath nextAllowedLane = route[currentLaneIndex + 1];

        if (!CanEnterNextLane(nextAllowedLane))
        {
            float stopDistance = GetStopDistanceOnCurrentLane();

            distanceOnLane = Mathf.Min(
                Mathf.Max(distanceOnLane, stopDistance),
                currentLane.TotalLength
            );

            currentSpeed = 0f;
            UpdateTransformPositionAndRotation();
            return;
        }

        currentLane.UnregisterVehicle(this);

        currentLaneIndex++;
        SetCurrentLane(nextAllowedLane);

        distanceOnLane = Mathf.Clamp(remainingDistance, 0f, currentLane.TotalLength);
        UpdateTransformPositionAndRotation();
    }

    private void UpdateWaitTime()
    {
        bool isWaiting = currentSpeed < 0.05f && currentLane != null && currentLane.StopBeforeEndDistance > 0f;

        if (isWaiting)
            totalWaitTime += Time.deltaTime;
    }

    private float GetStopDistanceOnCurrentLane()
    {
        return Mathf.Max(0f, currentLane.TotalLength - currentLane.StopBeforeEndDistance);
    }

    private void UpdateSpeed(float desiredSpeed)
    {
        desiredSpeed = Mathf.Clamp(desiredSpeed, 0f, maxSpeed);

        float rate = desiredSpeed < currentSpeed ? braking : acceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, rate * Time.deltaTime);
    }

    private float CalculateTargetSpeedForStoppingDistance(float gap)
    {
        if (gap <= 0f)
            return 0f;

        float brakingDistance = Mathf.Max(gap, 0.01f);
        float targetSpeed = Mathf.Sqrt(2f * braking * brakingDistance);

        return Mathf.Clamp(targetSpeed, 0f, maxSpeed);
    }

    private bool CanEnterNextLane(LanePath nextLane)
    {
        if (nextLane == null)
            return false;

        if (nextLane.IsStartBlocked(safeDistance))
            return false;

        TrafficLightController trafficLight = nextLane.GetComponent<TrafficLightController>();
        if (trafficLight != null)
        {
            TrafficLightController.LightSignal signal =
                trafficLight.GetLightSignalForLane(currentLane);

            if (signal != TrafficLightController.LightSignal.Green)
                return false;
        }

        LaneMergeController mergeController = nextLane.GetComponent<LaneMergeController>();
        if (mergeController != null)
        {
            if (!mergeController.CanEnterFrom(currentLane))
                return false;
        }

        return true;
    }

    private void UpdateTransformPositionAndRotation()
    {
        Vector3 currentPos = currentLane.GetPositionAtDistance(distanceOnLane);
        transform.position = currentPos;

        Vector3 direction = GetForwardDirection();

        if (direction.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private Vector3 GetForwardDirection()
    {
        float forwardDistance = distanceOnLane + lookAheadDistance;

        if (forwardDistance <= currentLane.TotalLength)
        {
            Vector3 a = currentLane.GetPositionAtDistance(distanceOnLane);
            Vector3 b = currentLane.GetPositionAtDistance(forwardDistance);
            return (b - a).normalized;
        }

        bool hasNextLane = currentLaneIndex + 1 < route.Count;

        if (hasNextLane)
        {
            LanePath nextLane = route[currentLaneIndex + 1];
            Vector3 a = currentLane.GetPositionAtDistance(distanceOnLane);
            Vector3 b = nextLane.GetPositionAtDistance(
                Mathf.Min(forwardDistance - currentLane.TotalLength, nextLane.TotalLength)
            );
            return (b - a).normalized;
        }

        Vector3 current = currentLane.GetPositionAtDistance(distanceOnLane);
        Vector3 back = currentLane.GetPositionAtDistance(Mathf.Max(distanceOnLane - 0.05f, 0f));
        return (current - back).normalized;
    }

    private void SetCurrentLane(LanePath lane)
    {
        currentLane = lane;
        currentLane.RegisterVehicle(this);
    }

    private void Arrive()
    {
        if (TrafficStatsManager.Instance != null)
            TrafficStatsManager.Instance.ReportVehicleArrived(flowDirection, totalWaitTime);

        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (currentLane != null)
            currentLane.UnregisterVehicle(this);
    }
}