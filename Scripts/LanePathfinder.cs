using System.Collections.Generic;

public class LanePathfinder
{
    public List<LanePath> FindPath(LanePath startLane, LanePath targetLane)
    {
        if (startLane == null || targetLane == null)
            return null;

        Queue<LanePath> queue = new Queue<LanePath>();
        Dictionary<LanePath, LanePath> cameFrom = new Dictionary<LanePath, LanePath>();
        HashSet<LanePath> visited = new HashSet<LanePath>();

        queue.Enqueue(startLane);
        visited.Add(startLane);

        while (queue.Count > 0)
        {
            LanePath current = queue.Dequeue();

            if (current == targetLane)
                return ReconstructPath(cameFrom, startLane, targetLane);

            foreach (LanePath nextLane in current.NextLanes)
            {
                if (nextLane == null)
                    continue;

                if (visited.Contains(nextLane))
                    continue;

                visited.Add(nextLane);
                cameFrom[nextLane] = current;
                queue.Enqueue(nextLane);
            }
        }

        return null;
    }

    private List<LanePath> ReconstructPath(
        Dictionary<LanePath, LanePath> cameFrom,
        LanePath startLane,
        LanePath targetLane)
    {
        List<LanePath> path = new List<LanePath>();
        LanePath current = targetLane;
        path.Add(current);

        while (current != startLane)
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}