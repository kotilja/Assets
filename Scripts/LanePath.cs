using System.Collections.Generic;
using UnityEngine;

public class LanePath : MonoBehaviour
{
    [SerializeField] private List<LanePath> nextLanes = new List<LanePath>();
    [SerializeField] private float stopBeforeEndDistance = 0f;

    private Transform[] points;
    private float totalLength;

    private readonly List<VehicleAgent> vehiclesOnLane = new List<VehicleAgent>();

    public List<LanePath> NextLanes => nextLanes;
    public float TotalLength => totalLength;
    public float StopBeforeEndDistance => stopBeforeEndDistance;

    public int PointCount
    {
        get
        {
            if (points == null)
                CachePoints();

            return points.Length;
        }
    }

    private void Awake()
    {
        CachePoints();
    }

    private void OnValidate()
    {
        CachePoints();
    }

    private void CachePoints()
    {
        int childCount = transform.childCount;
        points = new Transform[childCount];

        for (int i = 0; i < childCount; i++)
        {
            points[i] = transform.GetChild(i);
        }

        totalLength = 0f;

        for (int i = 1; i < childCount; i++)
        {
            totalLength += Vector3.Distance(points[i - 1].position, points[i].position);
        }
    }

    public Vector3 GetPointPosition(int index)
    {
        if (points == null)
            CachePoints();

        return points[index].position;
    }

    public Vector3 GetPositionAtDistance(float distance)
    {
        if (points == null)
            CachePoints();

        if (points.Length == 0)
            return transform.position;

        if (points.Length == 1)
            return points[0].position;

        distance = Mathf.Clamp(distance, 0f, totalLength);

        float remaining = distance;

        for (int i = 1; i < points.Length; i++)
        {
            Vector3 a = points[i - 1].position;
            Vector3 b = points[i].position;
            float segmentLength = Vector3.Distance(a, b);

            if (remaining <= segmentLength)
            {
                float t = segmentLength > 0f ? remaining / segmentLength : 0f;
                return Vector3.Lerp(a, b, t);
            }

            remaining -= segmentLength;
        }

        return points[points.Length - 1].position;
    }

    public void RegisterVehicle(VehicleAgent vehicle)
    {
        if (vehicle == null)
            return;

        if (!vehiclesOnLane.Contains(vehicle))
            vehiclesOnLane.Add(vehicle);
    }

    public void UnregisterVehicle(VehicleAgent vehicle)
    {
        if (vehicle == null)
            return;

        vehiclesOnLane.Remove(vehicle);
    }

    public VehicleAgent GetVehicleAhead(VehicleAgent currentVehicle)
    {
        VehicleAgent nearestVehicle = null;
        float nearestDistance = float.MaxValue;

        foreach (VehicleAgent other in vehiclesOnLane)
        {
            if (other == null || other == currentVehicle)
                continue;

            float delta = other.DistanceOnLane - currentVehicle.DistanceOnLane;

            if (delta > 0f && delta < nearestDistance)
            {
                nearestDistance = delta;
                nearestVehicle = other;
            }
        }

        return nearestVehicle;
    }

    public bool IsStartBlocked(float minDistance)
    {
        foreach (VehicleAgent vehicle in vehiclesOnLane)
        {
            if (vehicle == null)
                continue;

            if (vehicle.DistanceOnLane < minDistance)
                return true;
        }

        return false;
    }

    public bool HasVehicleNearEnd(float distanceFromEnd)
    {
        foreach (VehicleAgent vehicle in vehiclesOnLane)
        {
            if (vehicle == null)
                continue;

            float remainingDistance = totalLength - vehicle.DistanceOnLane;

            if (remainingDistance >= 0f && remainingDistance <= distanceFromEnd)
                return true;
        }

        return false;
    }
public int GetActiveVehicleCount()
{
    int count = 0;

    foreach (VehicleAgent vehicle in vehiclesOnLane)
    {
        if (vehicle != null)
            count++;
    }

    return count;
}
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        int childCount = transform.childCount;

        for (int i = 1; i < childCount; i++)
        {
            Transform a = transform.GetChild(i - 1);
            Transform b = transform.GetChild(i);
            Gizmos.DrawLine(a.position, b.position);
        }
    }
}