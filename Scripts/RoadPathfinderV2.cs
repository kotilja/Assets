using System.Collections.Generic;

public static class RoadPathfinderV2
{
    private class NodeStep
    {
        public RoadNodeV2 previousNode;
        public RoadSegmentV2 viaSegment;
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

        Queue<RoadNodeV2> queue = new Queue<RoadNodeV2>();
        Dictionary<RoadNodeV2, NodeStep> cameFrom = new Dictionary<RoadNodeV2, NodeStep>();
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

        return TryBuildLanePathFromNodePath(startNode, targetNode, cameFrom, out lanePath);
    }

    private static bool TryBuildLanePathFromNodePath(
        RoadNodeV2 startNode,
        RoadNodeV2 targetNode,
        Dictionary<RoadNodeV2, NodeStep> cameFrom,
        out List<RoadLaneDataV2> lanePath)
    {
        lanePath = new List<RoadLaneDataV2>();

        List<RoadNodeV2> nodePath = new List<RoadNodeV2>();
        RoadNodeV2 current = targetNode;
        nodePath.Add(current);

        while (current != startNode)
        {
            if (!cameFrom.TryGetValue(current, out NodeStep step))
            {
                lanePath = null;
                return false;
            }

            current = step.previousNode;
            nodePath.Add(current);
        }

        nodePath.Reverse();

        RoadLaneDataV2 previousLane = null;

        for (int i = 0; i < nodePath.Count - 1; i++)
        {
            RoadNodeV2 fromNode = nodePath[i];
            RoadNodeV2 toNode = nodePath[i + 1];

            if (!cameFrom.TryGetValue(toNode, out NodeStep step))
            {
                lanePath = null;
                return false;
            }

            RoadSegmentV2 segment = step.viaSegment;
            if (segment == null)
            {
                lanePath = null;
                return false;
            }

            List<RoadLaneDataV2> candidates = segment.GetDrivingLanes(fromNode, toNode);
            if (candidates == null || candidates.Count == 0)
            {
                lanePath = null;
                return false;
            }

            RoadLaneDataV2 chosenLane = ChooseBestLane(previousLane, candidates);
            if (chosenLane == null)
            {
                lanePath = null;
                return false;
            }

            lanePath.Add(chosenLane);
            previousLane = chosenLane;
        }

        return lanePath.Count > 0;
    }

    private static RoadLaneDataV2 ChooseBestLane(RoadLaneDataV2 previousLane, List<RoadLaneDataV2> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        if (previousLane == null)
            return candidates[0];

        RoadLaneDataV2 bestLane = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            RoadLaneDataV2 candidate = candidates[i];
            if (candidate == null)
                continue;

            float score = 0f;

            // Предпочитаем реальное connection
            bool hasRealConnection = false;
            foreach (RoadLaneConnectionV2 connection in previousLane.outgoingConnections)
            {
                if (connection == null || !connection.IsValid)
                    continue;

                if (connection.toLane == candidate)
                {
                    hasRealConnection = true;
                    break;
                }
            }

            score += hasRealConnection ? 0f : 100f;

            // Предпочитаем близкий индекс полосы
            score += UnityEngine.Mathf.Abs(candidate.localLaneIndex - previousLane.localLaneIndex);

            if (score < bestScore)
            {
                bestScore = score;
                bestLane = candidate;
            }
        }

        return bestLane;
    }
}