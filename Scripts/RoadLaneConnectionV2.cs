using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoadLaneConnectionV2
{
    public enum ConnectionKind
    {
        Junction,
        LaneChange
    }

    public enum MovementType
    {
        Straight,
        Left,
        Right,
        LaneChangeLeft,
        LaneChangeRight
    }

    public RoadLaneDataV2 fromLane;
    public RoadLaneDataV2 toLane;

    public ConnectionKind connectionKind;

    public RoadNodeV2 junctionNode;
    public Vector3 junctionPoint;

    public float turnScore;
    public MovementType movementType;

    public float fromDistanceOnLane;
    public float toDistanceOnLane;

    public List<Vector3> curvePoints = new List<Vector3>();

    public bool IsValid
    {
        get
        {
            if (fromLane == null || toLane == null)
                return false;

            if (connectionKind == ConnectionKind.Junction)
                return junctionNode != null;

            return true;
        }
    }
}