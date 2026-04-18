using System.Collections.Generic;
using UnityEngine;

public class PedestrianSpawnerV2 : MonoBehaviour
{
    [SerializeField] private PedestrianNetworkV2 pedestrianNetwork;
    [SerializeField] private PedestrianAgentV2 pedestrianPrefab;
    [SerializeField] private RoadVehicleSpawnerV2 roadVehicleSpawner;
    [SerializeField] private DestinationPointV2 defaultDestination;
    [SerializeField] private ParkingSpotV2 defaultParkingSpot;
    [SerializeField] private ParkingSpotV2 targetParkingSpot;
    [SerializeField] private Transform defaultTargetTransform;

    [Header("Trip sequence")]
    [SerializeField] private bool useTripSequence = true;
    [SerializeField] private bool autoResolveTripParking = true;
    [SerializeField] private bool useCommuteBuildings = true;

    [Header("Spawn")]
    [SerializeField] private bool spawnOnStart = false;
    [SerializeField] private float spawnRadius = 0.1f;
    [SerializeField] private int maxAliveAgents = 10;

    private int aliveAgents = 0;

    public PedestrianNetworkV2 PedestrianNetwork => pedestrianNetwork;
    public DestinationPointV2 DefaultDestination => defaultDestination;
    public ParkingSpotV2 DefaultParkingSpot => defaultParkingSpot;
    public ParkingSpotV2 TargetParkingSpot => targetParkingSpot;

    private void Start()
    {
        if (pedestrianNetwork == null)
            pedestrianNetwork = FindFirstObjectByType<PedestrianNetworkV2>();

        if (roadVehicleSpawner == null)
            roadVehicleSpawner = FindFirstObjectByType<RoadVehicleSpawnerV2>();

        if (pedestrianNetwork != null)
            pedestrianNetwork.RebuildGraph();

        if (spawnOnStart)
            SpawnOne();
    }

    public PedestrianAgentV2 SpawnOne()
    {
        if (pedestrianPrefab == null || pedestrianNetwork == null)
            return null;

        if (aliveAgents >= maxAliveAgents)
            return null;

        Vector3 spawnPosition = transform.position + (Vector3)(Random.insideUnitCircle * spawnRadius);
        spawnPosition.z = 0f;

        PedestrianAgentV2 agent = Instantiate(pedestrianPrefab, spawnPosition, Quaternion.identity);

        if (useCommuteBuildings && TryAssignCommuteBuildings(spawnPosition, out BuildingZoneV2 homeBuilding, out BuildingZoneV2 officeBuilding))
        {
            agent.InitializeCommuteCycle(pedestrianNetwork, roadVehicleSpawner, homeBuilding, officeBuilding, this);
        }
        else if (useTripSequence && roadVehicleSpawner != null && defaultDestination != null)
        {
            if (TryResolveTripParking(spawnPosition, out ParkingSpotV2 pickupParkingSpot, out ParkingSpotV2 dropoffParkingSpot))
                agent.InitializeTrip(pedestrianNetwork, pickupParkingSpot, roadVehicleSpawner, dropoffParkingSpot, defaultDestination, this);
            else if (defaultParkingSpot != null && targetParkingSpot != null)
                agent.InitializeTrip(pedestrianNetwork, defaultParkingSpot, roadVehicleSpawner, targetParkingSpot, defaultDestination, this);
            else if (defaultParkingSpot != null)
                agent.InitializeToParking(pedestrianNetwork, defaultParkingSpot, this);
            else
                agent.InitializeToDestination(pedestrianNetwork, defaultDestination, this);
        }
        else if (defaultParkingSpot != null)
            agent.InitializeToParking(pedestrianNetwork, defaultParkingSpot, this);
        else if (defaultDestination != null)
            agent.InitializeToDestination(pedestrianNetwork, defaultDestination, this);
        else if (defaultTargetTransform != null)
            agent.InitializeToWorldPoint(pedestrianNetwork, defaultTargetTransform.position, this);
        else
            agent.InitializeFree(pedestrianNetwork, this);

        aliveAgents++;
        return agent;
    }

    private bool TryAssignCommuteBuildings(
        Vector3 spawnPosition,
        out BuildingZoneV2 homeBuilding,
        out BuildingZoneV2 officeBuilding)
    {
        homeBuilding = null;
        officeBuilding = null;

        BuildingZoneV2[] buildings = FindObjectsByType<BuildingZoneV2>(FindObjectsSortMode.None);
        if (buildings == null || buildings.Length == 0)
            return false;

        float bestHomeDistance = float.MaxValue;

        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingZoneV2 building = buildings[i];
            if (building == null || building.Type != BuildingZoneV2.BuildingType.Home || !building.HasFreeSlot)
                continue;

            float distance = Vector3.Distance(spawnPosition, building.EntrancePoint);
            if (distance >= bestHomeDistance)
                continue;

            bestHomeDistance = distance;
            homeBuilding = building;
        }

        if (homeBuilding == null || !homeBuilding.TryReserveSlot())
            return false;

        float bestOfficeDistance = float.MaxValue;

        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingZoneV2 building = buildings[i];
            if (building == null || building.Type != BuildingZoneV2.BuildingType.Office || !building.HasFreeSlot)
                continue;

            float distance = Vector3.Distance(homeBuilding.EntrancePoint, building.EntrancePoint);
            if (distance >= bestOfficeDistance)
                continue;

            bestOfficeDistance = distance;
            officeBuilding = building;
        }

        if (officeBuilding == null || !officeBuilding.TryReserveSlot())
        {
            homeBuilding.ReleaseSlot();
            homeBuilding = null;
            return false;
        }

        return true;
    }

    private bool TryResolveTripParking(Vector3 spawnPosition, out ParkingSpotV2 pickupParkingSpot, out ParkingSpotV2 dropoffParkingSpot)
    {
        pickupParkingSpot = null;
        dropoffParkingSpot = null;

        if (!autoResolveTripParking || roadVehicleSpawner == null || defaultDestination == null)
            return false;

        ParkingSpotV2[] parkingSpots = FindObjectsByType<ParkingSpotV2>(FindObjectsSortMode.None);
        if (parkingSpots == null || parkingSpots.Length == 0)
            return false;

        Vector3 destinationPosition = defaultDestination.Position;
        float bestScore = float.MaxValue;

        for (int i = 0; i < parkingSpots.Length; i++)
        {
            ParkingSpotV2 pickupCandidate = parkingSpots[i];
            if (!IsUsableParkingSpot(pickupCandidate))
                continue;

            for (int j = 0; j < parkingSpots.Length; j++)
            {
                ParkingSpotV2 dropoffCandidate = parkingSpots[j];
                if (pickupCandidate == dropoffCandidate || !IsUsableParkingSpot(dropoffCandidate))
                    continue;

                if (!roadVehicleSpawner.TryGetTripPathFromParking(pickupCandidate, dropoffCandidate, out List<RoadLaneDataV2> lanePath))
                    continue;

                float score = Vector3.Distance(spawnPosition, pickupCandidate.PedestrianAnchorPoint);
                score += GetLanePathLength(lanePath);
                score += Vector3.Distance(dropoffCandidate.PedestrianAnchorPoint, destinationPosition);

                if (score >= bestScore)
                    continue;

                bestScore = score;
                pickupParkingSpot = pickupCandidate;
                dropoffParkingSpot = dropoffCandidate;
            }
        }

        return pickupParkingSpot != null && dropoffParkingSpot != null;
    }

    private bool IsUsableParkingSpot(ParkingSpotV2 parkingSpot)
    {
        return parkingSpot != null && parkingSpot.CanUse();
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

    public void NotifyAgentDestroyed(PedestrianAgentV2 agent)
    {
        aliveAgents = Mathf.Max(0, aliveAgents - 1);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 1f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        Gizmos.DrawSphere(transform.position, 0.08f);
    }
#endif
}
