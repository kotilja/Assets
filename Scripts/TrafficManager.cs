using System.Collections.Generic;
using UnityEngine;

public class TrafficManager : MonoBehaviour
{
    [SerializeField] private VehicleAgent vehiclePrefab;
    [SerializeField] private float spawnClearance = 1.2f;

    public bool CreateTrip(TrafficPoint from, TrafficPoint to)
    {
        if (from == null || to == null)
            return false;

        if (from.ExitLane == null)
            return false;

        if (to.EntryLane == null)
            return false;

        if (vehiclePrefab == null)
            return false;

        LanePathfinder pathfinder = new LanePathfinder();
        List<LanePath> route = pathfinder.FindPath(from.ExitLane, to.EntryLane);

        if (route == null || route.Count == 0)
            return false;

        LanePath startLane = route[0];

        if (startLane.IsStartBlocked(spawnClearance))
            return false;

        Vector3 spawnPosition = startLane.GetPositionAtDistance(0f);
        VehicleAgent vehicle = Instantiate(vehiclePrefab, spawnPosition, Quaternion.identity);
        vehicle.Initialize(route, to.Type);

        return true;
    }
}