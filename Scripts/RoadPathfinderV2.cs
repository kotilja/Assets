using System.Collections.Generic;
using UnityEngine;

public static class RoadPathfinderV2
{
    private class NodeStep
    {
        public RoadNodeV2 previousNode;
        public RoadSegmentV2 viaSegment;
    }

    private class LegInfo
    {
        public RoadNodeV2 fromNode;
        public RoadNodeV2 toNode;
        public RoadSegmentV2 segment;
        public List<RoadLaneDataV2> candidates = new List<RoadLaneDataV2>();
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

        if (!TryBuildNodePath(network, startNode, targetNode, out List<RoadNodeV2> nodePath, out Dictionary<RoadNodeV2, NodeStep> cameFrom))
            return false;

        return TryBuildLanePathFromNodePath(nodePath, cameFrom, out lanePath);
    }

    private static bool TryBuildNodePath(
        RoadNetworkV2 network,
        RoadNodeV2 startNode,
        RoadNodeV2 targetNode,
        out List<RoadNodeV2> nodePath,
        out Dictionary<RoadNodeV2, NodeStep> cameFrom)
    {
        nodePath = null;
        cameFrom = new Dictionary<RoadNodeV2, NodeStep>();

        Queue<RoadNodeV2> queue = new Queue<RoadNodeV2>();
        HashSet<RoadNodeV2> visited = new HashSet<RoadNodeV2>();

        queue.Enqueue(startNode);
        visited.Add(startNode);

        bool found = false;

        while (queue.Count > 0)
        {
            RoadNodeV2 currentNode = queue.Dequeue();

            if (currentNode == targetNode)
            {
                found = true;
                break;
            }

            foreach (RoadSegmentV2 segment in network.Segments)
            {
                if (segment == null)
                    continue;

                RoadNodeV2 nextNode = null;
                bool canTravel = false;

                if (segment.StartNode == currentNode && segment.ForwardLanes > 0)
                {
                    nextNode = segment.EndNode;
                    canTravel = true;
                }
                else if (segment.EndNode == currentNode && segment.BackwardLanes > 0)
                {
                    nextNode = segment.StartNode;
                    canTravel = true;
                }

                if (!canTravel || nextNode == null)
                    continue;

                if (visited.Contains(nextNode))
                    continue;

                visited.Add(nextNode);
                cameFrom[nextNode] = new NodeStep
                {
                    previousNode = currentNode,
                    viaSegment = segment
                };

                queue.Enqueue(nextNode);
            }
        }

        if (!found)
            return false;

        nodePath = new List<RoadNodeV2>();
        RoadNodeV2 current = targetNode;
        nodePath.Add(current);

        while (current != startNode)
        {
            if (!cameFrom.TryGetValue(current, out NodeStep step))
                return false;

            current = step.previousNode;
            nodePath.Add(current);
        }

        nodePath.Reverse();
        return nodePath.Count >= 2;
    }

    private static bool TryBuildLanePathFromNodePath(
        List<RoadNodeV2> nodePath,
        Dictionary<RoadNodeV2, NodeStep> cameFrom,
        out List<RoadLaneDataV2> lanePath)
    {
        lanePath = null;

        List<LegInfo> legs = new List<LegInfo>();

        for (int i = 0; i < nodePath.Count - 1; i++)
        {
            RoadNodeV2 fromNode = nodePath[i];
            RoadNodeV2 toNode = nodePath[i + 1];

            if (!cameFrom.TryGetValue(toNode, out NodeStep step))
                return false;

            if (step.viaSegment == null)
                return false;

            List<RoadLaneDataV2> candidates = step.viaSegment.GetDrivingLanes(fromNode, toNode);
            if (candidates == null || candidates.Count == 0)
                return false;

            legs.Add(new LegInfo
            {
                fromNode = fromNode,
                toNode = toNode,
                segment = step.viaSegment,
                candidates = candidates
            });
        }

        if (legs.Count == 0)
            return false;

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