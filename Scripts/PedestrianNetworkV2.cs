using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class PedestrianNetworkV2 : MonoBehaviour
{
    [System.Serializable]
    public class PedestrianNodeDataV2
    {
        public int id;
        public Vector3 position;
        public bool isParkingAnchor;
        public bool isDestinationAnchor;
        public ParkingSpotV2 parkingSpot;
        public DestinationPointV2 destinationPoint;
    }

    [System.Serializable]
    public class PedestrianEdgeDataV2
    {
        public int fromNodeId;
        public int toNodeId;
        public float cost;
        public bool isOffroad;
        public List<Vector3> polyline = new List<Vector3>();
    }

    [Header("Sources")]
    [SerializeField] private RoadNetworkV2 roadNetwork;

    [Header("Build settings")]
    [SerializeField] private float nodeMergeDistance = 0.2f;
    [SerializeField] private float sidewalkLinkDistance = 0.75f;
    [SerializeField] private float parkingAnchorLinkDistance = 1.5f;
    [SerializeField] private float destinationAnchorLinkDistance = 2.0f;
    [SerializeField] private float offroadPenaltyMultiplier = 1.75f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;
    [SerializeField] private Color nodeColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color edgeColor = new Color(0.2f, 0.9f, 1f, 0.9f);
    [SerializeField] private Color offroadColor = new Color(1f, 0.8f, 0.2f, 0.9f);
    [SerializeField] private float nodeRadius = 0.06f;

    [SerializeField] private List<PedestrianNodeDataV2> nodes = new List<PedestrianNodeDataV2>();
    [SerializeField] private List<PedestrianEdgeDataV2> edges = new List<PedestrianEdgeDataV2>();

    private readonly Dictionary<int, List<PedestrianEdgeDataV2>> adjacency = new Dictionary<int, List<PedestrianEdgeDataV2>>();
    private int nextNodeId = 1;

    public IReadOnlyList<PedestrianNodeDataV2> Nodes => nodes;
    public IReadOnlyList<PedestrianEdgeDataV2> Edges => edges;

    private void Start()
    {
        RebuildGraph();
    }

    public void RebuildGraph()
    {
        nodes.Clear();
        edges.Clear();
        adjacency.Clear();
        nextNodeId = 1;

        BuildFromRoadSidewalks();
        BuildFromPedestrianPaths();
        BuildParkingAnchors();
        BuildDestinationAnchors();
        RebuildAdjacency();
    }

    public PedestrianNodeDataV2 GetNearestNode(Vector3 worldPosition, float maxDistance)
    {
        worldPosition.z = 0f;

        PedestrianNodeDataV2 best = null;
        float bestDistance = maxDistance;

        for (int i = 0; i < nodes.Count; i++)
        {
            PedestrianNodeDataV2 node = nodes[i];
            if (node == null)
                continue;

            float d = Vector3.Distance(worldPosition, node.position);
            if (d <= bestDistance)
            {
                bestDistance = d;
                best = node;
            }
        }

        return best;
    }

    public PedestrianNodeDataV2 GetNodeForParkingSpot(ParkingSpotV2 spot)
    {
        if (spot == null)
            return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            PedestrianNodeDataV2 node = nodes[i];
            if (node != null && node.parkingSpot == spot)
                return node;
        }

        return null;
    }

    public PedestrianNodeDataV2 GetNodeForDestination(DestinationPointV2 destination)
    {
        if (destination == null)
            return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            PedestrianNodeDataV2 node = nodes[i];
            if (node != null && node.destinationPoint == destination)
                return node;
        }

        return null;
    }

    public List<Vector3> FindPath(Vector3 startPosition, Vector3 endPosition)
    {
        PedestrianNodeDataV2 startNode = GetNearestNode(startPosition, destinationAnchorLinkDistance);
        PedestrianNodeDataV2 endNode = GetNearestNode(endPosition, destinationAnchorLinkDistance);

        if (startNode == null || endNode == null)
            return new List<Vector3>();

        return FindPath(startNode.id, endNode.id);
    }

    public List<Vector3> FindPath(ParkingSpotV2 startParking, DestinationPointV2 destination)
    {
        PedestrianNodeDataV2 startNode = GetNodeForParkingSpot(startParking);
        PedestrianNodeDataV2 endNode = GetNodeForDestination(destination);

        if (startNode == null || endNode == null)
            return new List<Vector3>();

        return FindPath(startNode.id, endNode.id);
    }

    public List<Vector3> FindPath(int startNodeId, int endNodeId)
    {
        List<Vector3> empty = new List<Vector3>();

        if (!adjacency.ContainsKey(startNodeId) || !adjacency.ContainsKey(endNodeId))
            return empty;

        Dictionary<int, float> dist = new Dictionary<int, float>();
        Dictionary<int, int> prev = new Dictionary<int, int>();
        HashSet<int> visited = new HashSet<int>();

        for (int i = 0; i < nodes.Count; i++)
        {
            PedestrianNodeDataV2 node = nodes[i];
            if (node == null)
                continue;

            dist[node.id] = float.MaxValue;
        }

        if (!dist.ContainsKey(startNodeId) || !dist.ContainsKey(endNodeId))
            return empty;

        dist[startNodeId] = 0f;

        while (true)
        {
            int current = -1;
            float best = float.MaxValue;

            foreach (KeyValuePair<int, float> pair in dist)
            {
                if (visited.Contains(pair.Key))
                    continue;

                if (pair.Value < best)
                {
                    best = pair.Value;
                    current = pair.Key;
                }
            }

            if (current < 0)
                break;

            if (current == endNodeId)
                break;

            visited.Add(current);

            if (!adjacency.TryGetValue(current, out List<PedestrianEdgeDataV2> outgoing))
                continue;

            for (int i = 0; i < outgoing.Count; i++)
            {
                PedestrianEdgeDataV2 edge = outgoing[i];
                if (edge == null)
                    continue;

                float newCost = dist[current] + edge.cost;
                if (!dist.ContainsKey(edge.toNodeId) || newCost < dist[edge.toNodeId])
                {
                    dist[edge.toNodeId] = newCost;
                    prev[edge.toNodeId] = current;
                }
            }
        }

        if (startNodeId != endNodeId && !prev.ContainsKey(endNodeId))
            return empty;

        List<int> nodePath = new List<int>();
        int walk = endNodeId;
        nodePath.Add(walk);

        while (walk != startNodeId)
        {
            if (!prev.TryGetValue(walk, out int parent))
                return empty;

            walk = parent;
            nodePath.Add(walk);
        }

        nodePath.Reverse();

        List<Vector3> result = new List<Vector3>();

        for (int i = 0; i < nodePath.Count - 1; i++)
        {
            PedestrianEdgeDataV2 edge = GetEdge(nodePath[i], nodePath[i + 1]);
            if (edge == null || edge.polyline == null || edge.polyline.Count < 2)
                continue;

            if (result.Count == 0)
            {
                for (int j = 0; j < edge.polyline.Count; j++)
                    AddPointIfFar(result, edge.polyline[j]);
            }
            else
            {
                for (int j = 1; j < edge.polyline.Count; j++)
                    AddPointIfFar(result, edge.polyline[j]);
            }
        }

        if (nodePath.Count == 1)
        {
            PedestrianNodeDataV2 node = GetNodeById(startNodeId);
            if (node != null)
                result.Add(node.position);
        }

        return result;
    }

    private void BuildFromRoadSidewalks()
    {
        if (roadNetwork == null)
            roadNetwork = GetComponent<RoadNetworkV2>();

        if (roadNetwork == null)
            return;

        IReadOnlyList<RoadSegmentV2> segments = roadNetwork.Segments;
        for (int i = 0; i < segments.Count; i++)
        {
            RoadSegmentV2 segment = segments[i];
            if (segment == null)
                continue;

            if (segment.HasLeftSidewalk)
                AddPolylineAsWalkable(segment.GetLeftSidewalkPolylineWorld(), false);

            if (segment.HasRightSidewalk)
                AddPolylineAsWalkable(segment.GetRightSidewalkPolylineWorld(), false);
        }
    }

    private void BuildFromPedestrianPaths()
    {
        PedestrianPathV2[] paths = FindObjectsByType<PedestrianPathV2>(FindObjectsSortMode.None);

        for (int i = 0; i < paths.Length; i++)
        {
            PedestrianPathV2 path = paths[i];
            if (path == null)
                continue;

            List<Vector3> polyline = path.GetPolylineWorld();
            AddPolylineAsWalkable(polyline, false);
        }
    }

    private void BuildParkingAnchors()
    {
        ParkingSpotV2[] spots = FindObjectsByType<ParkingSpotV2>(FindObjectsSortMode.None);

        for (int i = 0; i < spots.Length; i++)
        {
            ParkingSpotV2 spot = spots[i];
            if (spot == null)
                continue;

            PedestrianNodeDataV2 node = GetOrCreateNode(spot.PedestrianAnchorPoint);
            node.isParkingAnchor = true;
            node.parkingSpot = spot;

            LinkNodeToNearestSidewalk(node, parkingAnchorLinkDistance, true);
        }
    }

    private void BuildDestinationAnchors()
    {
        DestinationPointV2[] destinations = FindObjectsByType<DestinationPointV2>(FindObjectsSortMode.None);

        for (int i = 0; i < destinations.Length; i++)
        {
            DestinationPointV2 destination = destinations[i];
            if (destination == null)
                continue;

            PedestrianNodeDataV2 node = GetOrCreateNode(destination.Position);
            node.isDestinationAnchor = true;
            node.destinationPoint = destination;

            LinkNodeToNearestSidewalk(node, destinationAnchorLinkDistance, true);
        }
    }

    private void AddPolylineAsWalkable(List<Vector3> polyline, bool isOffroad)
    {
        if (polyline == null || polyline.Count < 2)
            return;

        List<int> nodeIds = new List<int>();

        for (int i = 0; i < polyline.Count; i++)
        {
            PedestrianNodeDataV2 node = GetOrCreateNode(polyline[i]);
            nodeIds.Add(node.id);
        }

        for (int i = 0; i < nodeIds.Count - 1; i++)
        {
            PedestrianNodeDataV2 a = GetNodeById(nodeIds[i]);
            PedestrianNodeDataV2 b = GetNodeById(nodeIds[i + 1]);

            if (a == null || b == null)
                continue;

            List<Vector3> edgePolyline = new List<Vector3> { a.position, b.position };
            float cost = Vector3.Distance(a.position, b.position);
            if (isOffroad)
                cost *= offroadPenaltyMultiplier;

            AddBidirectionalEdge(a.id, b.id, edgePolyline, cost, isOffroad);
        }

        LinkConsecutiveNearbyNodes(polyline);
    }

    private void LinkConsecutiveNearbyNodes(List<Vector3> polyline)
    {
        if (polyline == null || polyline.Count == 0)
            return;

        for (int i = 0; i < nodes.Count; i++)
        {
            PedestrianNodeDataV2 a = nodes[i];
            if (a == null)
                continue;

            for (int j = i + 1; j < nodes.Count; j++)
            {
                PedestrianNodeDataV2 b = nodes[j];
                if (b == null)
                    continue;

                float d = Vector3.Distance(a.position, b.position);
                if (d <= sidewalkLinkDistance)
                {
                    if (GetEdge(a.id, b.id) != null)
                        continue;

                    List<Vector3> poly = new List<Vector3> { a.position, b.position };
                    AddBidirectionalEdge(a.id, b.id, poly, d, false);
                }
            }
        }
    }

    private void LinkNodeToNearestSidewalk(PedestrianNodeDataV2 node, float maxDistance, bool isOffroad)
    {
        if (node == null)
            return;

        PedestrianNodeDataV2 best = null;
        float bestDistance = maxDistance;

        for (int i = 0; i < nodes.Count; i++)
        {
            PedestrianNodeDataV2 candidate = nodes[i];
            if (candidate == null || candidate.id == node.id)
                continue;

            if (candidate.isParkingAnchor || candidate.isDestinationAnchor)
                continue;

            float d = Vector3.Distance(node.position, candidate.position);
            if (d <= bestDistance)
            {
                bestDistance = d;
                best = candidate;
            }
        }

        if (best == null)
            return;

        List<Vector3> polyline = new List<Vector3> { node.position, best.position };
        float cost = bestDistance;
        if (isOffroad)
            cost *= offroadPenaltyMultiplier;

        AddBidirectionalEdge(node.id, best.id, polyline, cost, isOffroad);
    }

    private PedestrianNodeDataV2 GetOrCreateNode(Vector3 position)
    {
        position.z = 0f;

        for (int i = 0; i < nodes.Count; i++)
        {
            PedestrianNodeDataV2 node = nodes[i];
            if (node == null)
                continue;

            if (Vector3.Distance(node.position, position) <= nodeMergeDistance)
                return node;
        }

        PedestrianNodeDataV2 created = new PedestrianNodeDataV2
        {
            id = nextNodeId++,
            position = position
        };

        nodes.Add(created);
        return created;
    }

    private PedestrianNodeDataV2 GetNodeById(int nodeId)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            PedestrianNodeDataV2 node = nodes[i];
            if (node != null && node.id == nodeId)
                return node;
        }

        return null;
    }

    private void AddBidirectionalEdge(int fromId, int toId, List<Vector3> polyline, float cost, bool isOffroad)
    {
        AddEdge(fromId, toId, polyline, cost, isOffroad);

        List<Vector3> reversePolyline = new List<Vector3>(polyline);
        reversePolyline.Reverse();
        AddEdge(toId, fromId, reversePolyline, cost, isOffroad);
    }

    private void AddEdge(int fromId, int toId, List<Vector3> polyline, float cost, bool isOffroad)
    {
        if (fromId == toId)
            return;

        if (GetEdge(fromId, toId) != null)
            return;

        PedestrianEdgeDataV2 edge = new PedestrianEdgeDataV2
        {
            fromNodeId = fromId,
            toNodeId = toId,
            cost = Mathf.Max(0.001f, cost),
            isOffroad = isOffroad,
            polyline = new List<Vector3>(polyline)
        };

        edges.Add(edge);
    }

    private PedestrianEdgeDataV2 GetEdge(int fromId, int toId)
    {
        for (int i = 0; i < edges.Count; i++)
        {
            PedestrianEdgeDataV2 edge = edges[i];
            if (edge == null)
                continue;

            if (edge.fromNodeId == fromId && edge.toNodeId == toId)
                return edge;
        }

        return null;
    }

    private void RebuildAdjacency()
    {
        adjacency.Clear();

        for (int i = 0; i < nodes.Count; i++)
        {
            PedestrianNodeDataV2 node = nodes[i];
            if (node == null)
                continue;

            adjacency[node.id] = new List<PedestrianEdgeDataV2>();
        }

        for (int i = 0; i < edges.Count; i++)
        {
            PedestrianEdgeDataV2 edge = edges[i];
            if (edge == null)
                continue;

            if (!adjacency.ContainsKey(edge.fromNodeId))
                adjacency[edge.fromNodeId] = new List<PedestrianEdgeDataV2>();

            adjacency[edge.fromNodeId].Add(edge);
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

        if (Vector3.Distance(points[points.Count - 1], point) > 0.01f)
            points.Add(point);
    }

    private void OnDrawGizmos()
    {
        if (!drawDebug)
            return;

        for (int i = 0; i < edges.Count; i++)
        {
            PedestrianEdgeDataV2 edge = edges[i];
            if (edge == null || edge.polyline == null || edge.polyline.Count < 2)
                continue;

            Gizmos.color = edge.isOffroad ? offroadColor : edgeColor;

            for (int j = 0; j < edge.polyline.Count - 1; j++)
                Gizmos.DrawLine(edge.polyline[j], edge.polyline[j + 1]);
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            PedestrianNodeDataV2 node = nodes[i];
            if (node == null)
                continue;

            if (node.isParkingAnchor)
                Gizmos.color = Color.yellow;
            else if (node.isDestinationAnchor)
                Gizmos.color = Color.magenta;
            else
                Gizmos.color = nodeColor;

            Gizmos.DrawSphere(node.position, nodeRadius);
        }
    }
}