using System.Collections.Generic;
using UnityEngine;

public class RoadVehicleSpawnerV2 : MonoBehaviour
{
    [SerializeField] private RoadNetworkV2 network;
    [SerializeField] private RoadVehicleAgentV2 vehiclePrefab;

    [Header("Route")]
    [SerializeField] private bool useNearestNodeFromTransform = true;
    [SerializeField] private RoadNodeV2 startNode;
    [SerializeField] private RoadNodeV2 targetNode;
    [SerializeField] private float startNodeSearchRadius = 1.25f;

    [Header("Spawn behavior")]
    [SerializeField] private bool spawnOnPlay = false;
    [SerializeField] private bool autoSpawn = false;
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private int maxAliveVehicles = 0;
    [SerializeField] private bool spawnFromRightmostLane = true;

    [Header("Debug keys")]
    [SerializeField] private bool enableDebugKeyboard = false;
    [SerializeField] private KeyCode spawnKey = KeyCode.Space;
    [SerializeField] private KeyCode swapDirectionKey = KeyCode.R;

    [Header("Logging")]
    [SerializeField] private bool logPathResult = true;

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private float gizmoRadius = 0.18f;
    [SerializeField] private Color spawnerColor = new Color(0.2f, 1f, 1f, 0.9f);
    [SerializeField] private Color startLinkColor = new Color(0.3f, 1f, 0.3f, 0.9f);
    [SerializeField] private Color targetLinkColor = new Color(1f, 0.9f, 0.2f, 0.9f);

    private readonly List<RoadVehicleAgentV2> spawnedVehicles = new List<RoadVehicleAgentV2>();
    private float spawnTimer;

    private void Start()
    {
        if (spawnOnPlay)
            SpawnOne();
    }

    private void Update()
    {
        CleanupSpawnedVehicles();

        if (enableDebugKeyboard)
        {
            if (spawnKey != KeyCode.None && Input.GetKeyDown(spawnKey))
                SpawnOne();

            if (swapDirectionKey != KeyCode.None && Input.GetKeyDown(swapDirectionKey))
                SwapDirection();
        }

        if (!autoSpawn)
            return;

        float interval = Mathf.Max(0.05f, spawnInterval);
        spawnTimer += Time.deltaTime;

        while (spawnTimer >= interval)
        {
            spawnTimer -= interval;
            TrySpawnScheduled();
        }
    }

    public void SpawnOne()
    {
        RoadNodeV2 resolvedStartNode = GetResolvedStartNode();

        if (network == null || vehiclePrefab == null || resolvedStartNode == null || targetNode == null)
            return;

        if (resolvedStartNode == targetNode)
            return;

        bool found = RoadPathfinderV2.TryFindPath(
            network,
            resolvedStartNode,
            targetNode,
            out List<RoadLaneDataV2> lanePath
        );

        if (!found || lanePath == null || lanePath.Count == 0)
        {
            if (logPathResult)
                Debug.LogWarning($"V2 path not found: {GetNodeName(resolvedStartNode)} -> {GetNodeName(targetNode)}", this);

            return;
        }

        RoadLaneDataV2 spawnLane = GetSpawnLane(lanePath[0]);
        if (spawnLane == null)
            spawnLane = lanePath[0];

        Vector3 spawnPosition = spawnLane.start;
        RoadVehicleAgentV2 vehicle = Instantiate(vehiclePrefab, spawnPosition, Quaternion.identity);
        vehicle.Initialize(lanePath, spawnLane);

        spawnedVehicles.Add(vehicle);

        if (logPathResult)
            Debug.Log($"V2 path spawned: {GetNodeName(resolvedStartNode)} -> {GetNodeName(targetNode)}, lanes: {lanePath.Count}", this);
    }

    public void SwapDirection()
    {
        RoadNodeV2 resolvedStartNode = GetResolvedStartNode();
        RoadNodeV2 oldTarget = targetNode;

        if (resolvedStartNode == null)
            return;

        startNode = oldTarget;
        targetNode = resolvedStartNode;
        useNearestNodeFromTransform = false;
    }

    private void TrySpawnScheduled()
    {
        if (maxAliveVehicles > 0 && GetAliveVehicleCount() >= maxAliveVehicles)
            return;

        SpawnOne();
    }

    private int GetAliveVehicleCount()
    {
        CleanupSpawnedVehicles();
        return spawnedVehicles.Count;
    }

    private void CleanupSpawnedVehicles()
    {
        for (int i = spawnedVehicles.Count - 1; i >= 0; i--)
        {
            if (spawnedVehicles[i] == null)
                spawnedVehicles.RemoveAt(i);
        }
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

    private RoadNodeV2 GetResolvedStartNode()
    {
        if (!useNearestNodeFromTransform)
            return startNode;

        RoadNodeV2 nearest = FindNearestNodeToTransform();

        if (nearest != null)
            return nearest;

        return startNode;
    }

    private RoadNodeV2 FindNearestNodeToTransform()
    {
        if (network == null)
            return null;

        float bestDistance = Mathf.Max(0.01f, startNodeSearchRadius);
        RoadNodeV2 bestNode = null;

        for (int i = 0; i < network.Nodes.Count; i++)
        {
            RoadNodeV2 node = network.Nodes[i];
            if (node == null)
                continue;

            float distance = Vector3.Distance(transform.position, node.transform.position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                bestNode = node;
            }
        }

        return bestNode;
    }

    private string GetNodeName(RoadNodeV2 node)
    {
        return node == null ? "null" : node.name;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        DrawSpawnerGizmos(selectedOnly: false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        DrawSpawnerGizmos(selectedOnly: true);
    }

    private void DrawSpawnerGizmos(bool selectedOnly)
    {
        Gizmos.color = spawnerColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);

        if (useNearestNodeFromTransform)
        {
            Gizmos.color = new Color(spawnerColor.r, spawnerColor.g, spawnerColor.b, 0.35f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, startNodeSearchRadius));
        }

        RoadNodeV2 resolvedStartNode = GetResolvedStartNode();

        if (resolvedStartNode != null)
        {
            Gizmos.color = startLinkColor;
            Gizmos.DrawLine(transform.position, resolvedStartNode.transform.position);

            if (selectedOnly)
                Gizmos.DrawSphere(resolvedStartNode.transform.position, gizmoRadius * 0.75f);
        }

        if (targetNode != null)
        {
            Gizmos.color = targetLinkColor;
            Gizmos.DrawLine(transform.position, targetNode.transform.position);

            if (selectedOnly)
                Gizmos.DrawSphere(targetNode.transform.position, gizmoRadius * 0.75f);
        }
    }
}