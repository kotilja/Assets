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
        public bool isBuildingAnchor;
        public bool isCrosswalkHub;
        public ParkingSpotV2 parkingSpot;
        public DestinationPointV2 destinationPoint;
        public BuildingZoneV2 buildingZone;
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

    private class IntersectionCornerCandidate
    {
        public PedestrianNodeDataV2 node;
        public RoadSegmentV2 segment;
        public Vector3 direction;
    }

    [Header("Sources")]
    [SerializeField] private RoadNetworkV2 roadNetwork;

    [Header("Build settings")]
    [SerializeField] private float nodeMergeDistance = 0.2f;
    [SerializeField] private float crosswalkInset = 0.08f;
    [SerializeField] private float intersectionCornerLinkDistance = 0.7f;
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
        BuildSimpleJointOuterConnections();
        BuildFromPedestrianPaths();
        BuildIntersectionCrosswalks();
        BuildParkingAnchors();
        BuildDestinationAnchors();
        BuildBuildingAnchors();
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
        List<PedestrianNodeDataV2> startCandidates = GetNearbyNodes(startPosition, destinationAnchorLinkDistance, 6);
        List<PedestrianNodeDataV2> endCandidates = GetNearbyNodes(endPosition, destinationAnchorLinkDistance, 6);

        if (startCandidates.Count == 0 || endCandidates.Count == 0)
            return new List<Vector3>();

        List<Vector3> bestPath = new List<Vector3>();
        float bestCost = float.MaxValue;

        for (int i = 0; i < startCandidates.Count; i++)
        {
            PedestrianNodeDataV2 startNode = startCandidates[i];
            if (startNode == null)
                continue;

            for (int j = 0; j < endCandidates.Count; j++)
            {
                PedestrianNodeDataV2 endNode = endCandidates[j];
                if (endNode == null)
                    continue;

                List<Vector3> path = FindPath(startNode.id, endNode.id);
                if (path == null || path.Count < 2)
                    continue;

                float cost = GetPolylineLength(path);
                if (cost >= bestCost)
                    continue;

                bestCost = cost;
                bestPath = path;
            }
        }

        return bestPath;
    }

    public List<Vector3> FindPath(ParkingSpotV2 startParking, DestinationPointV2 destination)
    {
        PedestrianNodeDataV2 startNode = GetNodeForParkingSpot(startParking);
        PedestrianNodeDataV2 endNode = GetNodeForDestination(destination);

        if (startNode == null || endNode == null)
            return new List<Vector3>();

        return FindPath(startNode.id, endNode.id);
    }

    public List<Vector3> FindPath(Vector3 startPosition, ParkingSpotV2 parkingSpot)
    {
        PedestrianNodeDataV2 endNode = GetNodeForParkingSpot(parkingSpot);
        if (endNode == null)
            return new List<Vector3>();

        return FindPathToNode(startPosition, endNode.id, parkingAnchorLinkDistance);
    }

    public List<Vector3> FindPath(Vector3 startPosition, DestinationPointV2 destination)
    {
        PedestrianNodeDataV2 endNode = GetNodeForDestination(destination);
        if (endNode == null)
            return new List<Vector3>();

        return FindPathToNode(startPosition, endNode.id, destinationAnchorLinkDistance);
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

    private void BuildIntersectionCrosswalks()
    {
        RoadNodeV2[] roadNodes = FindObjectsByType<RoadNodeV2>(FindObjectsSortMode.None);

        for (int i = 0; i < roadNodes.Length; i++)
        {
            RoadNodeV2 roadNode = roadNodes[i];
            if (roadNode == null || !roadNode.IsIntersection)
                continue;

            List<IntersectionCornerCandidate> cornerCandidates = new List<IntersectionCornerCandidate>();

            for (int j = 0; j < roadNode.ConnectedSegments.Count; j++)
            {
                RoadSegmentV2 segment = roadNode.ConnectedSegments[j];
                if (segment == null)
                    continue;

                if (!TryGetCrosswalkEndpoints(
                    segment,
                    roadNode,
                    out Vector3 rawLeftExit,
                    out Vector3 rawRightExit,
                    out Vector3 leftExit,
                    out Vector3 rightExit))
                    continue;

                PedestrianNodeDataV2 rawLeftNode = GetOrCreateNode(rawLeftExit);
                PedestrianNodeDataV2 rawRightNode = GetOrCreateNode(rawRightExit);
                PedestrianNodeDataV2 leftNode = GetOrCreateNode(leftExit);
                PedestrianNodeDataV2 rightNode = GetOrCreateNode(rightExit);

                if (TryGetSidewalkEndpoint(segment, roadNode, true, out Vector3 _, out Vector3 leftDirection))
                {
                    cornerCandidates.Add(new IntersectionCornerCandidate
                    {
                        node = leftNode,
                        segment = segment,
                        direction = leftDirection
                    });
                }

                if (TryGetSidewalkEndpoint(segment, roadNode, false, out Vector3 _, out Vector3 rightDirection))
                {
                    cornerCandidates.Add(new IntersectionCornerCandidate
                    {
                        node = rightNode,
                        segment = segment,
                        direction = rightDirection
                    });
                }

                AddSingleDirectionEdge(rawLeftNode, leftNode, false);
                AddSingleDirectionEdge(rawRightNode, rightNode, false);

                List<Vector3> crosswalkPolyline = new List<Vector3> { leftNode.position, rightNode.position };
                float cost = Vector3.Distance(leftNode.position, rightNode.position);
                AddBidirectionalEdge(leftNode.id, rightNode.id, crosswalkPolyline, cost, false);
            }

            ConnectIntersectionCornerLoop(roadNode, cornerCandidates);
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

            PedestrianNodeDataV2 node = CreateStrictNode(destination.Position);
            node.isDestinationAnchor = true;
            node.destinationPoint = destination;

            if (!LinkNodeToNearestWalkableEdge(node, float.MaxValue, true, true))
                LinkNodeToNearestSidewalk(node, float.MaxValue, true);
        }
    }

    private void BuildBuildingAnchors()
    {
        BuildingZoneV2[] buildings = FindObjectsByType<BuildingZoneV2>(FindObjectsSortMode.None);

        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingZoneV2 building = buildings[i];
            if (building == null)
                continue;

            Vector3 entrancePoint = GetBestBuildingEntrancePoint(building);
            PedestrianNodeDataV2 node = CreateStrictNode(entrancePoint);
            node.isBuildingAnchor = true;
            node.buildingZone = building;

            if (!LinkNodeToNearestWalkableEdge(node, float.MaxValue, true, true))
                LinkNodeToNearestSidewalk(node, float.MaxValue, true);
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
    }

    private bool LinkNodeToNearestSidewalk(PedestrianNodeDataV2 node, float maxDistance, bool isOffroad)
    {
        if (node == null)
            return false;

        PedestrianNodeDataV2 best = null;
        float bestDistance = maxDistance;

        for (int i = 0; i < nodes.Count; i++)
        {
            PedestrianNodeDataV2 candidate = nodes[i];
            if (candidate == null || candidate.id == node.id)
                continue;

            if (candidate.isParkingAnchor || candidate.isDestinationAnchor || candidate.isBuildingAnchor)
                continue;

            if (candidate.isCrosswalkHub)
                continue;

            float d = Vector3.Distance(node.position, candidate.position);
            if (d <= bestDistance)
            {
                bestDistance = d;
                best = candidate;
            }
        }

        if (best == null)
            return false;

        List<Vector3> polyline = new List<Vector3> { node.position, best.position };
        float cost = bestDistance;
        if (isOffroad)
            cost *= offroadPenaltyMultiplier;

        AddBidirectionalEdge(node.id, best.id, polyline, cost, isOffroad);
        return true;
    }

    private bool LinkNodeToNearestWalkableEdge(
        PedestrianNodeDataV2 node,
        float maxDistance,
        bool isOffroad,
        bool allowCrosswalkHubs)
    {
        if (node == null)
            return false;

        PedestrianEdgeDataV2 bestEdge = null;
        PedestrianNodeDataV2 bestA = null;
        PedestrianNodeDataV2 bestB = null;
        Vector3 bestPoint = Vector3.zero;
        float bestDistance = maxDistance;

        for (int i = 0; i < edges.Count; i++)
        {
            PedestrianEdgeDataV2 edge = edges[i];
            if (edge == null || edge.polyline == null || edge.polyline.Count < 2)
                continue;

            if (edge.isOffroad)
                continue;

            PedestrianNodeDataV2 edgeFrom = GetNodeById(edge.fromNodeId);
            PedestrianNodeDataV2 edgeTo = GetNodeById(edge.toNodeId);
            if (edgeFrom == null || edgeTo == null)
                continue;

            if (edgeFrom.isParkingAnchor || edgeFrom.isDestinationAnchor || edgeFrom.isBuildingAnchor)
                continue;

            if (edgeFrom.isCrosswalkHub && !allowCrosswalkHubs)
                continue;

            if (edgeTo.isParkingAnchor || edgeTo.isDestinationAnchor || edgeTo.isBuildingAnchor)
                continue;

            if (edgeTo.isCrosswalkHub && !allowCrosswalkHubs)
                continue;

            for (int j = 0; j < edge.polyline.Count - 1; j++)
            {
                Vector3 segmentA = edge.polyline[j];
                Vector3 segmentB = edge.polyline[j + 1];
                Vector3 projected = ClosestPointOnSegment(node.position, segmentA, segmentB);
                float distance = Vector3.Distance(node.position, projected);
                if (distance > bestDistance)
                    continue;

                bestDistance = distance;
                bestEdge = edge;
                bestA = GetOrCreateNode(segmentA);
                bestB = GetOrCreateNode(segmentB);
                bestPoint = projected;
            }
        }

        if (bestEdge == null || bestA == null || bestB == null)
            return false;

        PedestrianNodeDataV2 projectedNode = CreateProjectedNode(bestPoint);

        List<Vector3> anchorPolyline = new List<Vector3> { node.position, projectedNode.position };
        float anchorCost = Vector3.Distance(node.position, projectedNode.position);
        if (isOffroad)
            anchorCost *= offroadPenaltyMultiplier;
        AddBidirectionalEdge(node.id, projectedNode.id, anchorPolyline, anchorCost, isOffroad);

        List<Vector3> firstHalfPolyline = new List<Vector3> { projectedNode.position, bestA.position };
        float firstHalfCost = Vector3.Distance(projectedNode.position, bestA.position);
        AddBidirectionalEdge(projectedNode.id, bestA.id, firstHalfPolyline, firstHalfCost, false);

        List<Vector3> secondHalfPolyline = new List<Vector3> { projectedNode.position, bestB.position };
        float secondHalfCost = Vector3.Distance(projectedNode.position, bestB.position);
        AddBidirectionalEdge(projectedNode.id, bestB.id, secondHalfPolyline, secondHalfCost, false);

        return true;
    }

    private void ConnectIntersectionCornerLoop(
        RoadNodeV2 roadNode,
        List<IntersectionCornerCandidate> cornerCandidates)
    {
        if (roadNode == null || cornerCandidates == null || cornerCandidates.Count < 2)
            return;

        Vector3 center = roadNode.transform.position;
        List<RoadSegmentV2> orderedSegments = new List<RoadSegmentV2>();

        for (int i = 0; i < cornerCandidates.Count; i++)
        {
            IntersectionCornerCandidate candidate = cornerCandidates[i];
            if (candidate == null || candidate.segment == null)
                continue;

            if (!orderedSegments.Contains(candidate.segment))
                orderedSegments.Add(candidate.segment);
        }

        orderedSegments.Sort((a, b) =>
        {
            Vector3 dirA = GetArmDirection(a, roadNode);
            Vector3 dirB = GetArmDirection(b, roadNode);
            float angleA = Mathf.Atan2(dirA.y, dirA.x);
            float angleB = Mathf.Atan2(dirB.y, dirB.x);
            return angleA.CompareTo(angleB);
        });

        for (int i = 0; i < orderedSegments.Count; i++)
        {
            RoadSegmentV2 currentSegment = orderedSegments[i];
            RoadSegmentV2 nextSegment = orderedSegments[(i + 1) % orderedSegments.Count];

            if (currentSegment == null || nextSegment == null || currentSegment == nextSegment)
                continue;

            if (!TryGetNearestCornerPair(
                cornerCandidates,
                currentSegment,
                nextSegment,
                out IntersectionCornerCandidate currentCandidate,
                out IntersectionCornerCandidate nextCandidate))
                continue;

            PedestrianNodeDataV2 currentNode = currentCandidate.node;
            PedestrianNodeDataV2 nextNode = nextCandidate.node;
            float distance = Vector3.Distance(currentNode.position, nextNode.position);
            float maxCornerDistance = GetMaxCornerConnectionDistance(roadNode, currentSegment, nextSegment);

            if (distance <= maxCornerDistance)
            {
                List<Vector3> polyline = new List<Vector3> { currentNode.position, nextNode.position };
                AddBidirectionalEdge(currentNode.id, nextNode.id, polyline, distance, false);
                continue;
            }

            Vector3 apexPoint = GetOuterJointApexPoint(
                center,
                currentNode.position,
                currentCandidate.direction,
                nextNode.position,
                nextCandidate.direction);

            PedestrianNodeDataV2 apexNode = CreateStrictNode(apexPoint);

            AddBidirectionalEdge(
                currentNode.id,
                apexNode.id,
                new List<Vector3> { currentNode.position, apexNode.position },
                Vector3.Distance(currentNode.position, apexNode.position),
                false);

            AddBidirectionalEdge(
                apexNode.id,
                nextNode.id,
                new List<Vector3> { apexNode.position, nextNode.position },
                Vector3.Distance(apexNode.position, nextNode.position),
                false);
        }
    }

    private bool TryGetNearestCornerPair(
        List<IntersectionCornerCandidate> cornerCandidates,
        RoadSegmentV2 firstSegment,
        RoadSegmentV2 secondSegment,
        out IntersectionCornerCandidate firstCorner,
        out IntersectionCornerCandidate secondCorner)
    {
        firstCorner = null;
        secondCorner = null;

        if (cornerCandidates == null || firstSegment == null || secondSegment == null)
            return false;

        float bestDistance = float.MaxValue;

        for (int i = 0; i < cornerCandidates.Count; i++)
        {
            IntersectionCornerCandidate firstCandidate = cornerCandidates[i];
            if (firstCandidate == null || firstCandidate.segment != firstSegment || firstCandidate.node == null)
                continue;

            for (int j = 0; j < cornerCandidates.Count; j++)
            {
                IntersectionCornerCandidate secondCandidate = cornerCandidates[j];
                if (secondCandidate == null || secondCandidate.segment != secondSegment || secondCandidate.node == null)
                    continue;

                float distance = Vector3.Distance(firstCandidate.node.position, secondCandidate.node.position);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                firstCorner = firstCandidate;
                secondCorner = secondCandidate;
            }
        }

        return firstCorner != null && secondCorner != null;
    }

    private void BuildSimpleJointOuterConnections()
    {
        if (roadNetwork == null)
            roadNetwork = GetComponent<RoadNetworkV2>();

        if (roadNetwork == null)
            return;

        IReadOnlyList<RoadNodeV2> roadNodes = roadNetwork.Nodes;
        for (int i = 0; i < roadNodes.Count; i++)
        {
            RoadNodeV2 node = roadNodes[i];
            if (node == null || node.ConnectedSegments == null || node.ConnectedSegments.Count != 2)
                continue;

            RoadSegmentV2 firstSegment = node.ConnectedSegments[0];
            RoadSegmentV2 secondSegment = node.ConnectedSegments[1];
            if (firstSegment == null || secondSegment == null)
                continue;

            if (!TryGetRoadDirection(firstSegment, node, out Vector3 firstDir) ||
                !TryGetRoadDirection(secondSegment, node, out Vector3 secondDir))
                continue;

            float jointAngle = Mathf.Abs(Vector3.SignedAngle(firstDir, secondDir, Vector3.forward));
            if (jointAngle <= 90f)
                continue;

            if (!TryGetSidewalkEndpoint(firstSegment, node, true, out Vector3 firstLeftPoint, out Vector3 firstLeftDir) ||
                !TryGetSidewalkEndpoint(firstSegment, node, false, out Vector3 firstRightPoint, out Vector3 firstRightDir) ||
                !TryGetSidewalkEndpoint(secondSegment, node, true, out Vector3 secondLeftPoint, out Vector3 secondLeftDir) ||
                !TryGetSidewalkEndpoint(secondSegment, node, false, out Vector3 secondRightPoint, out Vector3 secondRightDir))
                continue;

            bool firstLeftInside = IsPointInsideJointWedge(node.transform.position, firstDir, secondDir, firstLeftPoint);
            bool firstRightInside = IsPointInsideJointWedge(node.transform.position, firstDir, secondDir, firstRightPoint);
            bool secondLeftInside = IsPointInsideJointWedge(node.transform.position, firstDir, secondDir, secondLeftPoint);
            bool secondRightInside = IsPointInsideJointWedge(node.transform.position, firstDir, secondDir, secondRightPoint);

            if (firstLeftInside == firstRightInside || secondLeftInside == secondRightInside)
                continue;

            Vector3 firstOuterPoint = firstLeftInside ? firstRightPoint : firstLeftPoint;
            Vector3 firstOuterDir = firstLeftInside ? firstRightDir : firstLeftDir;
            Vector3 secondOuterPoint = secondLeftInside ? secondRightPoint : secondLeftPoint;
            Vector3 secondOuterDir = secondLeftInside ? secondRightDir : secondLeftDir;

            Vector3 apexPoint = GetOuterJointApexPoint(node.transform.position, firstOuterPoint, firstOuterDir, secondOuterPoint, secondOuterDir);
            PedestrianNodeDataV2 apexNode = CreateStrictNode(apexPoint);
            PedestrianNodeDataV2 firstNode = GetOrCreateNode(firstOuterPoint);
            PedestrianNodeDataV2 secondNode = GetOrCreateNode(secondOuterPoint);

            AddBidirectionalEdge(
                firstNode.id,
                apexNode.id,
                new List<Vector3> { firstNode.position, apexNode.position },
                Vector3.Distance(firstNode.position, apexNode.position),
                false);

            AddBidirectionalEdge(
                secondNode.id,
                apexNode.id,
                new List<Vector3> { secondNode.position, apexNode.position },
                Vector3.Distance(secondNode.position, apexNode.position),
                false);
        }
    }

    private bool TryGetSidewalkEndpoint(
        RoadSegmentV2 segment,
        RoadNodeV2 node,
        bool leftSide,
        out Vector3 point,
        out Vector3 direction)
    {
        point = Vector3.zero;
        direction = Vector3.zero;

        if (segment == null || node == null)
            return false;

        List<Vector3> polyline = leftSide
            ? segment.GetLeftSidewalkPolylineWorld()
            : segment.GetRightSidewalkPolylineWorld();

        if (polyline == null || polyline.Count < 2)
            return false;

        if (segment.StartNode == node)
        {
            point = polyline[0];
            direction = (polyline[1] - polyline[0]).normalized;
            return direction.sqrMagnitude > 0.0001f;
        }

        if (segment.EndNode == node)
        {
            point = polyline[polyline.Count - 1];
            direction = (polyline[polyline.Count - 2] - polyline[polyline.Count - 1]).normalized;
            return direction.sqrMagnitude > 0.0001f;
        }

        return false;
    }

    private bool TryGetRoadDirection(RoadSegmentV2 segment, RoadNodeV2 node, out Vector3 direction)
    {
        direction = Vector3.zero;

        if (segment == null || node == null)
            return false;

        if (segment.StartNode == node && segment.EndNode != null)
            direction = (segment.EndNode.transform.position - node.transform.position).normalized;
        else if (segment.EndNode == node && segment.StartNode != null)
            direction = (segment.StartNode.transform.position - node.transform.position).normalized;

        direction.z = 0f;
        return direction.sqrMagnitude > 0.0001f;
    }

    private bool IsPointInsideJointWedge(Vector3 center, Vector3 firstDir, Vector3 secondDir, Vector3 point)
    {
        Vector3 toPoint = point - center;
        toPoint.z = 0f;

        if (toPoint.sqrMagnitude < 0.0001f)
            return false;

        float angleA = Mathf.Atan2(firstDir.y, firstDir.x) * Mathf.Rad2Deg;
        float angleB = Mathf.Atan2(secondDir.y, secondDir.x) * Mathf.Rad2Deg;
        float pointAngle = Mathf.Atan2(toPoint.y, toPoint.x) * Mathf.Rad2Deg;
        float deltaAB = Mathf.DeltaAngle(angleA, angleB);
        float deltaAP = Mathf.DeltaAngle(angleA, pointAngle);

        if (Mathf.Abs(deltaAB) < 1f)
            return false;

        if (deltaAB > 0f)
            return deltaAP > 0f && deltaAP < deltaAB;

        return deltaAP < 0f && deltaAP > deltaAB;
    }

    private Vector3 GetOuterJointApexPoint(
        Vector3 center,
        Vector3 firstPoint,
        Vector3 firstDir,
        Vector3 secondPoint,
        Vector3 secondDir)
    {
        if (TryGetLineIntersection(firstPoint, firstDir, secondPoint, secondDir, out Vector3 intersection))
        {
            float maxRadius = Mathf.Max(
                Vector3.Distance(center, firstPoint),
                Vector3.Distance(center, secondPoint)) + 3f;

            if (Vector3.Distance(center, intersection) <= maxRadius)
            {
                intersection.z = 0f;
                return intersection;
            }
        }

        Vector3 midpoint = (firstPoint + secondPoint) * 0.5f;
        midpoint.z = 0f;
        return midpoint;
    }

    private bool TryGetLineIntersection(
        Vector3 pointA,
        Vector3 dirA,
        Vector3 pointB,
        Vector3 dirB,
        out Vector3 intersection)
    {
        intersection = Vector3.zero;

        Vector2 p = new Vector2(pointA.x, pointA.y);
        Vector2 r = new Vector2(dirA.x, dirA.y);
        Vector2 q = new Vector2(pointB.x, pointB.y);
        Vector2 s = new Vector2(dirB.x, dirB.y);

        float cross = r.x * s.y - r.y * s.x;
        if (Mathf.Abs(cross) <= 0.0001f)
            return false;

        Vector2 qp = q - p;
        float t = (qp.x * s.y - qp.y * s.x) / cross;
        Vector2 hit = p + r * t;
        intersection = new Vector3(hit.x, hit.y, 0f);
        return true;
    }

    private float GetMaxCornerConnectionDistance(
        RoadNodeV2 roadNode,
        RoadSegmentV2 firstSegment,
        RoadSegmentV2 secondSegment)
    {
        float baseDistance = Mathf.Max(0.5f, intersectionCornerLinkDistance);

        if (roadNode == null)
            return baseDistance;

        float maxRoadHalfWidth = 0f;

        if (firstSegment != null)
            maxRoadHalfWidth = Mathf.Max(maxRoadHalfWidth, firstSegment.TotalRoadWidth * 0.5f + firstSegment.SidewalkWidth);

        if (secondSegment != null)
            maxRoadHalfWidth = Mathf.Max(maxRoadHalfWidth, secondSegment.TotalRoadWidth * 0.5f + secondSegment.SidewalkWidth);

        for (int i = 0; i < roadNode.ConnectedSegments.Count; i++)
        {
            RoadSegmentV2 segment = roadNode.ConnectedSegments[i];
            if (segment == null)
                continue;

            maxRoadHalfWidth = Mathf.Max(maxRoadHalfWidth, segment.TotalRoadWidth * 0.5f + segment.SidewalkWidth);
        }

        return Mathf.Max(baseDistance, maxRoadHalfWidth * 2.5f);
    }

    private Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float abSqr = Vector3.SqrMagnitude(ab);
        if (abSqr <= 0.000001f)
            return a;

        float t = Vector3.Dot(point - a, ab) / abSqr;
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

    private PedestrianNodeDataV2 CreateProjectedNode(Vector3 position)
    {
        position.z = 0f;

        PedestrianNodeDataV2 created = new PedestrianNodeDataV2
        {
            id = nextNodeId++,
            position = position
        };

        nodes.Add(created);
        return created;
    }

    private bool TryGetCrosswalkEndpoints(
        RoadSegmentV2 segment,
        RoadNodeV2 intersectionNode,
        out Vector3 rawLeftExit,
        out Vector3 rawRightExit,
        out Vector3 leftExit,
        out Vector3 rightExit)
    {
        rawLeftExit = Vector3.zero;
        rawRightExit = Vector3.zero;
        leftExit = Vector3.zero;
        rightExit = Vector3.zero;

        if (segment == null || intersectionNode == null)
            return false;

        List<Vector3> leftPolyline = segment.GetLeftSidewalkPolylineWorld();
        List<Vector3> rightPolyline = segment.GetRightSidewalkPolylineWorld();

        if (leftPolyline == null || rightPolyline == null || leftPolyline.Count < 2 || rightPolyline.Count < 2)
            return false;

        bool useStart = segment.StartNode == intersectionNode;
        bool useEnd = segment.EndNode == intersectionNode;
        if (!useStart && !useEnd)
            return false;

        rawLeftExit = useStart ? leftPolyline[0] : leftPolyline[leftPolyline.Count - 1];
        rawRightExit = useStart ? rightPolyline[0] : rightPolyline[rightPolyline.Count - 1];

        leftExit = rawLeftExit;
        rightExit = rawRightExit;

        Vector3 armDirection = GetArmDirection(segment, intersectionNode);
        if (armDirection.sqrMagnitude > 0.0001f)
        {
            float inset = Mathf.Max(0.04f, segment.JunctionInset * 0.9f + crosswalkInset);
            Vector3 shift = -armDirection.normalized * inset;
            leftExit += shift;
            rightExit += shift;
        }

        leftExit.z = 0f;
        rightExit.z = 0f;
        return true;
    }

    private void AddSingleDirectionEdge(PedestrianNodeDataV2 from, PedestrianNodeDataV2 to, bool isOffroad)
    {
        if (from == null || to == null || from.id == to.id)
            return;

        List<Vector3> polyline = new List<Vector3> { from.position, to.position };
        float cost = Vector3.Distance(from.position, to.position);
        if (isOffroad)
            cost *= offroadPenaltyMultiplier;

        AddBidirectionalEdge(from.id, to.id, polyline, cost, isOffroad);
    }

    private Vector3 GetArmDirection(RoadSegmentV2 segment, RoadNodeV2 intersectionNode)
    {
        if (segment == null || intersectionNode == null)
            return Vector3.zero;

        Vector3 direction = Vector3.zero;

        if (segment.StartNode == intersectionNode && segment.EndNode != null)
            direction = segment.EndNode.transform.position - intersectionNode.transform.position;
        else if (segment.EndNode == intersectionNode && segment.StartNode != null)
            direction = segment.StartNode.transform.position - intersectionNode.transform.position;

        direction.z = 0f;
        return direction.normalized;
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

    private PedestrianNodeDataV2 CreateStrictNode(Vector3 position)
    {
        position.z = 0f;

        PedestrianNodeDataV2 created = new PedestrianNodeDataV2
        {
            id = nextNodeId++,
            position = position
        };

        nodes.Add(created);
        return created;
    }

    private List<PedestrianNodeDataV2> GetNearbyNodes(Vector3 position, float maxDistance, int maxCount)
    {
        List<PedestrianNodeDataV2> result = new List<PedestrianNodeDataV2>();

        for (int i = 0; i < nodes.Count; i++)
        {
            PedestrianNodeDataV2 node = nodes[i];
            if (node == null)
                continue;

            float distance = Vector3.Distance(position, node.position);
            if (distance > maxDistance)
                continue;

            result.Add(node);
        }

        result.Sort((a, b) =>
        {
            float da = Vector3.Distance(position, a.position);
            float db = Vector3.Distance(position, b.position);
            return da.CompareTo(db);
        });

        if (result.Count > maxCount)
            result.RemoveRange(maxCount, result.Count - maxCount);

        return result;
    }

    private List<Vector3> FindPathToNode(Vector3 startPosition, int endNodeId, float startSearchRadius)
    {
        List<Vector3> attachedPath = BuildPathFromWorldAttachment(startPosition, endNodeId, startSearchRadius);
        if (attachedPath.Count > 0)
            return attachedPath;

        return FindPathToNodeByNearbyNodes(startPosition, endNodeId, startSearchRadius);
    }

    private List<Vector3> BuildPathFromWorldAttachment(Vector3 startPosition, int endNodeId, float startSearchRadius)
    {
        List<Vector3> bestPath = new List<Vector3>();
        float bestCost = float.MaxValue;

        float[] searchRadii = new float[]
        {
            Mathf.Max(0.5f, startSearchRadius),
            Mathf.Max(1.0f, startSearchRadius * 2f),
            Mathf.Max(1.5f, startSearchRadius * 3f),
            Mathf.Max(2.0f, startSearchRadius * 4f),
            float.MaxValue
        };

        for (int radiusIndex = 0; radiusIndex < searchRadii.Length; radiusIndex++)
        {
            if (!TryFindNearestWalkableAttachment(
                startPosition,
                searchRadii[radiusIndex],
                true,
                out Vector3 projectedPoint,
                out PedestrianNodeDataV2 edgeStart,
                out PedestrianNodeDataV2 edgeEnd,
                out float attachmentDistance))
            {
                continue;
            }

            PedestrianNodeDataV2[] attachmentNodes = new PedestrianNodeDataV2[] { edgeStart, edgeEnd };

            for (int i = 0; i < attachmentNodes.Length; i++)
            {
                PedestrianNodeDataV2 startNode = attachmentNodes[i];
                if (startNode == null)
                    continue;

                List<Vector3> path = FindPath(startNode.id, endNodeId);
                if (path == null || path.Count == 0)
                    continue;

                List<Vector3> candidatePath = new List<Vector3>();
                AddPointIfFar(candidatePath, projectedPoint);
                AddPointIfFar(candidatePath, startNode.position);

                for (int j = 1; j < path.Count; j++)
                    AddPointIfFar(candidatePath, path[j]);

                float cost =
                    attachmentDistance * offroadPenaltyMultiplier +
                    Vector3.Distance(projectedPoint, startNode.position) +
                    GetPolylineLength(path);

                if (cost >= bestCost)
                    continue;

                bestCost = cost;
                bestPath = candidatePath;
            }

            if (bestPath.Count > 0)
                break;
        }

        return bestPath;
    }

    private List<Vector3> FindPathToNodeByNearbyNodes(Vector3 startPosition, int endNodeId, float startSearchRadius)
    {
        List<Vector3> bestPath = new List<Vector3>();
        float bestCost = float.MaxValue;

        float[] searchRadii = new float[]
        {
            Mathf.Max(0.5f, startSearchRadius),
            Mathf.Max(1.0f, startSearchRadius * 2f),
            Mathf.Max(1.5f, startSearchRadius * 3f),
            Mathf.Max(2.0f, startSearchRadius * 4f),
            float.MaxValue
        };

        for (int radiusIndex = 0; radiusIndex < searchRadii.Length; radiusIndex++)
        {
            List<PedestrianNodeDataV2> startCandidates = GetNearbyNodes(startPosition, searchRadii[radiusIndex], 6);
            if (startCandidates.Count == 0)
                continue;

            for (int i = 0; i < startCandidates.Count; i++)
            {
                PedestrianNodeDataV2 startNode = startCandidates[i];
                if (startNode == null)
                    continue;

                List<Vector3> path = FindPath(startNode.id, endNodeId);
                if (path == null || path.Count < 2)
                    continue;

                float cost = Vector3.Distance(startPosition, startNode.position) * offroadPenaltyMultiplier + GetPolylineLength(path);
                if (cost >= bestCost)
                    continue;

                bestCost = cost;
                bestPath = path;
            }

            if (bestPath.Count > 0)
                break;
        }

        return bestPath;
    }

    private bool TryFindNearestWalkableAttachment(
        Vector3 position,
        float maxDistance,
        bool allowCrosswalkHubs,
        out Vector3 projectedPoint,
        out PedestrianNodeDataV2 edgeStart,
        out PedestrianNodeDataV2 edgeEnd,
        out float distance)
    {
        projectedPoint = Vector3.zero;
        edgeStart = null;
        edgeEnd = null;
        distance = maxDistance;

        for (int i = 0; i < edges.Count; i++)
        {
            PedestrianEdgeDataV2 edge = edges[i];
            if (edge == null || edge.polyline == null || edge.polyline.Count < 2)
                continue;

            if (edge.isOffroad)
                continue;

            PedestrianNodeDataV2 fromNode = GetNodeById(edge.fromNodeId);
            PedestrianNodeDataV2 toNode = GetNodeById(edge.toNodeId);
            if (fromNode == null || toNode == null)
                continue;

            if (fromNode.isParkingAnchor || fromNode.isDestinationAnchor || fromNode.isBuildingAnchor)
                continue;

            if (toNode.isParkingAnchor || toNode.isDestinationAnchor || toNode.isBuildingAnchor)
                continue;

            if (!allowCrosswalkHubs && (fromNode.isCrosswalkHub || toNode.isCrosswalkHub))
                continue;

            for (int j = 0; j < edge.polyline.Count - 1; j++)
            {
                Vector3 a = edge.polyline[j];
                Vector3 b = edge.polyline[j + 1];
                Vector3 candidatePoint = ClosestPointOnSegment(position, a, b);
                float candidateDistance = Vector3.Distance(position, candidatePoint);
                if (candidateDistance > distance)
                    continue;

                distance = candidateDistance;
                projectedPoint = candidatePoint;
                edgeStart = fromNode;
                edgeEnd = toNode;
            }
        }

        return edgeStart != null && edgeEnd != null;
    }

    private float GetPolylineLength(List<Vector3> polyline)
    {
        float length = 0f;

        if (polyline == null)
            return length;

        for (int i = 0; i < polyline.Count - 1; i++)
            length += Vector3.Distance(polyline[i], polyline[i + 1]);

        return length;
    }

    private Vector3 GetBestBuildingEntrancePoint(BuildingZoneV2 building)
    {
        if (building == null)
            return Vector3.zero;

        Vector3 bestWalkablePoint = building.Position;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < edges.Count; i++)
        {
            PedestrianEdgeDataV2 edge = edges[i];
            if (edge == null || edge.isOffroad || edge.polyline == null || edge.polyline.Count < 2)
                continue;

            for (int j = 0; j < edge.polyline.Count - 1; j++)
            {
                Vector3 projected = ClosestPointOnSegment(building.Position, edge.polyline[j], edge.polyline[j + 1]);
                float distance = Vector3.Distance(building.Position, projected);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestWalkablePoint = projected;
            }
        }

        return building.GetClosestPointOnPerimeter(bestWalkablePoint);
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
            else if (node.isBuildingAnchor)
                Gizmos.color = new Color(0.4f, 0.7f, 1f, 1f);
            else
                Gizmos.color = nodeColor;

            Gizmos.DrawSphere(node.position, nodeRadius);
        }
    }
}
