using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoadLaneConnectionV2
{
    public enum MovementType
    {
        Straight,
        Left,
        Right
    }

    public RoadLaneDataV2 fromLane;
    public RoadLaneDataV2 toLane;

    public RoadNodeV2 junctionNode;
    public Vector3 junctionPoint;
    public float turnScore;
    public MovementType movementType;

    public List<Vector3> curvePoints = new List<Vector3>();

    public bool IsValid => fromLane != null && toLane != null && junctionNode != null;
}