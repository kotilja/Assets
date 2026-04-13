using System.Collections.Generic;
using UnityEngine;

public class LaneMergeController : MonoBehaviour
{
    [SerializeField] private List<LanePath> priorityIncomingLanes = new List<LanePath>();
    [SerializeField] private float priorityCheckDistance = 1.2f;

    public bool CanEnterFrom(LanePath fromLane)
    {
        if (fromLane == null)
            return false;

        if (priorityIncomingLanes.Contains(fromLane))
            return true;

        foreach (LanePath priorityLane in priorityIncomingLanes)
        {
            if (priorityLane == null)
                continue;

            if (priorityLane == fromLane)
                continue;

            if (priorityLane.HasVehicleNearEnd(priorityCheckDistance))
                return false;
        }

        return true;
    }
}