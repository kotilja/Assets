using System.Collections.Generic;
using UnityEngine;

public class RoadVehicleSpawnerV2 : MonoBehaviour
{
    [SerializeField] private RoadNetworkV2 network;
    [SerializeField] private RoadVehicleAgentV2 vehiclePrefab;

    [Header("Debug route")]
    [SerializeField] private RoadNodeV2 startNode;
    [SerializeField] private RoadNodeV2 targetNode;

    [Header("Spawn behavior")]
    [SerializeField] private bool spawnFromRightmostLane = true;

    [Header("Debug keys")]
    [SerializeField] private KeyCode spawnKey = KeyCode.Space;
    [SerializeField] private KeyCode swapDirectionKey = KeyCode.R;

    [Header("Logging")]
    [SerializeField] private bool logPathResult = true;

    private void Update()
    {
        if (spawnKey != KeyCode.None && Input.GetKeyDown(spawnKey))
            SpawnOne();

        if (swapDirectionKey != KeyCode.None && Input.GetKeyDown(swapDirectionKey))
            SwapDirection();
    }

    public void SpawnOne()
    {
        if (network == null || vehiclePrefab == null || startNode == null || targetNode == null)
            return;

        bool found = RoadPathfinderV2.TryFindPath(network, startNode, targetNode, out List<RoadLaneDataV2> lanePath);

        if (!found || lanePath == null || lanePath.Count == 0)
        {
            if (logPathResult)
                Debug.LogWarning($"V2 path not found: {GetNodeName(startNode)} -> {GetNodeName(targetNode)}");

            return;
        }

        RoadLaneDataV2 spawnLane = GetSpawnLane(lanePath[0]);
        if (spawnLane == null)
            spawnLane = lanePath[0];

        Vector3 spawnPosition = spawnLane.start;
        RoadVehicleAgentV2 vehicle = Instantiate(vehiclePrefab, spawnPosition, Quaternion.identity);
        vehicle.Initialize(lanePath, spawnLane);

        if (logPathResult)
            Debug.Log($"V2 path spawned: {GetNodeName(startNode)} -> {GetNodeName(targetNode)}, lanes: {lanePath.Count}");
    }

    private RoadLaneDataV2 GetSpawnLane(RoadLaneDataV2 plannedLane)
    {
        if (plannedLane == null || plannedLane.ownerSegment == null)
            return plannedLane;

        if (!spawnFromRightmostLane)
            return plannedLane;

        List<RoadLaneDataV2> candidates = plannedLane.ownerSegment.GetDrivingLanes(plannedLane.fromNode, plannedLane.toNode);
        if (candidates == null || candidates.Count == 0)
            return plannedLane;

        RoadLaneDataV2 best = plannedLane;

        for (int i = 0; i < candidates.Count; i++)
        {
            RoadLaneDataV2 lane = candidates[i];
            if (lane == null)
                continue;

            if (best == null || lane.localLaneIndex < best.localLaneIndex)
                best = lane;
        }

        return best;
    }

    public void SwapDirection()
    {
        RoadNodeV2 temp = startNode;
        startNode = targetNode;
        targetNode = temp;
    }

    private string GetNodeName(RoadNodeV2 node)
    {
        return node == null ? "null" : node.name;
    }
}