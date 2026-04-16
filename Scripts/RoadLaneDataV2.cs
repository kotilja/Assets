using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoadLaneDataV2
{
    public int laneId;
    public RoadSegmentV2 ownerSegment;
    public int localLaneIndex;
    public RoadLaneV2.LaneDirection direction;

    public Vector3 start;
    public Vector3 end;

    public RoadNodeV2 fromNode;
    public RoadNodeV2 toNode;

    public readonly List<Vector3> sampledPoints = new List<Vector3>();
    public readonly List<RoadLaneConnectionV2> outgoingConnections = new List<RoadLaneConnectionV2>();
    public readonly List<RoadLaneConnectionV2> incomingConnections = new List<RoadLaneConnectionV2>();
    

    public Vector3 DirectionVector
    {
        get
        {
            Vector3 dir = (end - start).normalized;
            return dir.sqrMagnitude < 0.0001f ? Vector3.right : dir;
        }
    }

    public Vector3 MidPoint => Vector3.Lerp(start, end, 0.5f);
}