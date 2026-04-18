using System.Collections.Generic;
using UnityEngine;

public static class RoadPathfinderV2
{
    private class DirectedLeg
    {
        public RoadNodeV2 fromNode;
        public RoadNodeV2 toNode;
        public RoadSegmentV2 segment;
    }

    private class LegInfo
    {
        public RoadNodeV2 fromNode;
        public RoadNodeV2 toNode;
        public RoadSegmentV2 segment;
        public List<RoadLaneDataV2> candidates = new List<RoadLaneDataV2>();
    }

    private class PathCandidate
    {
        public List<RoadNodeV2> nodes = new List<RoadNodeV2>();
        public List<DirectedLeg> legs = new List<DirectedLeg>();
    }

    public static bool TryFindPath(
        RoadNetworkV2 network,
        RoadNodeV2 startNode,
        RoadNodeV2 targetNode,
        out List<RoadLaneDataV2> lanePath)
    {
        lanePath = null;

        if (network == null || startNode == null || targetNode == null)
            return false;

        if (startNode == targetNode)
            return false;

        Dictionary<RoadNodeV2, List<DirectedLeg>> outgoingByNode = BuildOutgoingLegs(network);

        if (!outgoingByNode.TryGetValue(startNode, out List<DirectedLeg> startOutgoing) || startOutgoing.Count == 0)
            return false;

        Queue<PathCandidate> queue = new Queue<PathCandidate>();
        PathCandidate initial = new PathCandidate();
        initial.nodes.Add(startNode);
        queue.Enqueue(initial);

        int maxDepth = Mathf.Max(2, network.Nodes.Count + 2);
        int safetyCounter = 0;
        const int maxIterations = 10000;

        while (queue.Count > 0 && safetyCounter < maxIterations)
        {
            safetyCounter++;

            PathCandidate candidate = queue.Dequeue();
            RoadNodeV2 currentNode = candidate.nodes[candidate.nodes.Count - 1];

            if (currentNode == targetNode)
            {
                if (TryBuildLanePathFromLegs(candidate.legs, out lanePath))
                    return true;

                continue;
            }

            if (candidate.legs.Count >= maxDepth)
                continue;

            if (!outgoingByNode.TryGetValue(currentNode, out List<DirectedLeg> outgoing))
                continue;

            for (int i = 0; i < outgoing.Count; i++)
            {
                DirectedLeg leg = outgoing[i];
                if (leg == null || leg.toNode == null || leg.segment == null)
                    continue;

                if (candidate.nodes.Contains(leg.toNode))
                    continue;

                PathCandidate next = new PathCandidate();
                next.nodes.AddRange(candidate.nodes);
                next.legs.AddRange(candidate.legs);

                next.nodes.Add(leg.toNode);
                next.legs.Add(leg);

                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static Dictionary<RoadNodeV2, List<DirectedLeg>> BuildOutgoingLegs(RoadNetworkV2 network)
    {
        Dictionary<RoadNodeV2, List<DirectedLeg>> outgoingByNode = new Dictionary<RoadNodeV2, List<DirectedLeg>>();

        foreach (RoadSegmentV2 segment in network.Segments)
        {
            if (segment == null)
                continue;

            if (segment.StartNode != null && segment.EndNode != null && segment.ForwardLanes > 0)
                AddOutgoingLeg(outgoingByNode, segment.StartNode, segment.EndNode, segment);

            if (segment.StartNode != null && segment.EndNode != null && segment.BackwardLanes > 0)
                AddOutgoingLeg(outgoingByNode, segment.EndNode, segment.StartNode, segment);
        }

        return outgoingByNode;
    }

    private static void AddOutgoingLeg(
        Dictionary<RoadNodeV2, List<DirectedLeg>> outgoingByNode,
        RoadNodeV2 fromNode,
        RoadNodeV2 toNode,
        RoadSegmentV2 segment)
    {
        if (fromNode == null || toNode == null || segment == null)
            return;

        if (!outgoingByNode.TryGetValue(fromNode, out List<DirectedLeg> list))
        {
            list = new List<DirectedLeg>();
            outgoingByNode[fromNode] = list;
        }

        list.Add(new DirectedLeg
        {
            fromNode = fromNode,
            toNode = toNode,
            segment = segment
        });
    }

    private static bool TryBuildLanePathFromLegs(
        List<DirectedLeg> directedLegs,
        out List<RoadLaneDataV2> lanePath)
    {
        lanePath = null;

        if (directedLegs == null || directedLegs.Count == 0)
            return false;

        List<LegInfo> legs = new List<LegInfo>();

        for (int i = 0; i < directedLegs.Count; i++)
        {
            DirectedLeg directedLeg = directedLegs[i];
            if (directedLeg == null || directedLeg.segment == null || directedLeg.fromNode == null || directedLeg.toNode == null)
                return false;

            List<RoadLaneDataV2> candidates = directedLeg.segment.GetDrivingLanes(directedLeg.fromNode, directedLeg.toNode);
            if (candidates == null || candidates.Count == 0)
                return false;

            legs.Add(new LegInfo
            {
                fromNode = directedLeg.fromNode,
                toNode = directedLeg.toNode,
                segment = directedLeg.segment,
                candidates = candidates
            });
        }

        Dictionary<RoadLaneDataV2, float>[] costToEnd = new Dictionary<RoadLaneDataV2, float>[legs.Count];
        Dictionary<RoadLaneDataV2, RoadLaneDataV2>[] nextChoice = new Dictionary<RoadLaneDataV2, RoadLaneDataV2>[legs.Count];

        for (int i = 0; i < legs.Count; i++)
        {
            costToEnd[i] = new Dictionary<RoadLaneDataV2, float>();
            nextChoice[i] = new Dictionary<RoadLaneDataV2, RoadLaneDataV2>();
        }

        int lastIndex = legs.Count - 1;

        for (int i = 0; i < legs[lastIndex].candidates.Count; i++)
        {
            RoadLaneDataV2 lane = legs[lastIndex].candidates[i];
            if (lane == null)
                continue;

            costToEnd[lastIndex][lane] = GetLanePreferenceCost(lane, null);
            nextChoice[lastIndex][lane] = null;
        }

        for (int legIndex = legs.Count - 2; legIndex >= 0; legIndex--)
        {
            LegInfo currentLeg = legs[legIndex];
            LegInfo nextLeg = legs[legIndex + 1];

            bool requireRealConnection = HasAnyRealConnection(currentLeg.candidates, nextLeg.candidates);

            for (int i = 0; i < currentLeg.candidates.Count; i++)
            {
                RoadLaneDataV2 currentLane = currentLeg.candidates[i];
                if (currentLane == null)
                    continue;

                float bestCost = float.MaxValue;
                RoadLaneDataV2 bestNextLane = null;

                for (int j = 0; j < nextLeg.candidates.Count; j++)
                {
                    RoadLaneDataV2 candidateNextLane = nextLeg.candidates[j];
                    if (candidateNextLane == null)
                        continue;

                    if (!costToEnd[legIndex + 1].TryGetValue(candidateNextLane, out float futureCost))
                        continue;

                    float transitionCost = GetTransitionCost(currentLane, candidateNextLane, requireRealConnection);

                    if (transitionCost >= 10000f)
                        continue;

                    float totalCost =
                        GetLanePreferenceCost(currentLane, nextLeg.segment) +
                        transitionCost +
                        futureCost;

                    if (totalCost < bestCost)
                    {
                        bestCost = totalCost;
                        bestNextLane = candidateNextLane;
                    }
                }

                if (bestNextLane != null)
                {
                    costToEnd[legIndex][currentLane] = bestCost;
                    nextChoice[legIndex][currentLane] = bestNextLane;
                }
            }
        }

        RoadLaneDataV2 firstLane = null;
        float bestFirstCost = float.MaxValue;

        foreach (KeyValuePair<RoadLaneDataV2, float> kv in costToEnd[0])
        {
            if (kv.Value < bestFirstCost)
            {
                bestFirstCost = kv.Value;
                firstLane = kv.Key;
            }
        }

        if (firstLane == null)
            return false;

        lanePath = new List<RoadLaneDataV2> { firstLane };

        RoadLaneDataV2 currentChosenLane = firstLane;

        for (int legIndex = 0; legIndex < legs.Count - 1; legIndex++)
        {
            if (!nextChoice[legIndex].TryGetValue(currentChosenLane, out RoadLaneDataV2 nextLane))
                return false;

            if (nextLane == null)
                return false;

            lanePath.Add(nextLane);
            currentChosenLane = nextLane;
        }

        return lanePath.Count == legs.Count;
    }

    private static float GetTransitionCost(RoadLaneDataV2 currentLane, RoadLaneDataV2 nextLane, bool requireRealConnection)
    {
        if (currentLane == null || nextLane == null)
            return 10000f;

        bool hasRealConnection = false;
        RoadLaneConnectionV2.MovementType movementType = InferMovementType(currentLane, nextLane);

        for (int i = 0; i < currentLane.outgoingConnections.Count; i++)
        {
            RoadLaneConnectionV2 connection = currentLane.outgoingConnections[i];

            if (connection == null || !connection.IsValid)
                continue;

            if (connection.toLane == nextLane)
            {
                hasRealConnection = true;
                movementType = connection.movementType;
                break;
            }
        }

        if (requireRealConnection && !hasRealConnection)
            return 10000f;

        float cost = 0f;

        if (!hasRealConnection)
            cost += 20f;

        cost += Mathf.Abs(currentLane.localLaneIndex - nextLane.localLaneIndex) * 2f;

        switch (movementType)
        {
            case RoadLaneConnectionV2.MovementType.Left:
                cost += 3f;
                break;

            case RoadLaneConnectionV2.MovementType.Right:
                cost += 1f;
                break;

            case RoadLaneConnectionV2.MovementType.UTurn:
                cost += 4f;
                break;

            case RoadLaneConnectionV2.MovementType.Straight:
                cost += 0f;
                break;
        }

        return cost;
    }

    private static bool HasAnyRealConnection(List<RoadLaneDataV2> currentCandidates, List<RoadLaneDataV2> nextCandidates)
    {
        if (currentCandidates == null || nextCandidates == null)
            return false;

        for (int i = 0; i < currentCandidates.Count; i++)
        {
            RoadLaneDataV2 currentLane = currentCandidates[i];
            if (currentLane == null)
                continue;

            for (int j = 0; j < currentLane.outgoingConnections.Count; j++)
            {
                RoadLaneConnectionV2 connection = currentLane.outgoingConnections[j];
                if (connection == null || !connection.IsValid || connection.toLane == null)
                    continue;

                if (nextCandidates.Contains(connection.toLane))
                    return true;
            }
        }

        return false;
    }

    private static RoadLaneConnectionV2.MovementType InferMovementType(RoadLaneDataV2 currentLane, RoadLaneDataV2 nextLane)
    {
        if (currentLane == null || nextLane == null)
            return RoadLaneConnectionV2.MovementType.Straight;

        float angle = Vector3.SignedAngle(
            currentLane.DirectionVector.normalized,
            nextLane.DirectionVector.normalized,
            Vector3.forward
        );

        float absAngle = Mathf.Abs(angle);

        if (absAngle < 20f)
            return RoadLaneConnectionV2.MovementType.Straight;

        if (absAngle >= 140f)
            return RoadLaneConnectionV2.MovementType.UTurn;

        return angle > 0f
            ? RoadLaneConnectionV2.MovementType.Left
            : RoadLaneConnectionV2.MovementType.Right;
    }

    private static float GetLanePreferenceCost(RoadLaneDataV2 lane, RoadSegmentV2 nextSegment)
    {
        if (lane == null)
            return 1000f;

        float cost = 0f;

        if (nextSegment == null)
            return cost;

        bool hasLeft = false;
        bool hasStraight = false;
        bool hasRight = false;

        for (int i = 0; i < lane.outgoingConnections.Count; i++)
        {
            RoadLaneConnectionV2 connection = lane.outgoingConnections[i];
            if (connection == null || !connection.IsValid)
                continue;

            if (connection.toLane == null || connection.toLane.ownerSegment != nextSegment)
                continue;

            switch (connection.movementType)
            {
                case RoadLaneConnectionV2.MovementType.Left:
                    hasLeft = true;
                    break;

                case RoadLaneConnectionV2.MovementType.Straight:
                    hasStraight = true;
                    break;

                case RoadLaneConnectionV2.MovementType.Right:
                    hasRight = true;
                    break;
            }
        }

        if (hasLeft)
            cost += lane.localLaneIndex * 0.5f;

        if (hasRight)
            cost += (10f - lane.localLaneIndex) * 0.1f;

        if (hasStraight)
            cost += Mathf.Abs(lane.localLaneIndex - 0.5f);

        return cost;
    }
}
