using UnityEngine;

[ExecuteAlways]
public class RoadGraphDebuggerV2 : MonoBehaviour
{
    [SerializeField] private RoadNetworkV2 network;
    [SerializeField] private bool drawLaneIds = true;
    [SerializeField] private bool drawConnections = true;
    [SerializeField] private bool drawConnectionLabels = true;

    [SerializeField] private Color straightColor = Color.green;
    [SerializeField] private Color leftColor = Color.yellow;
    [SerializeField] private Color rightColor = Color.cyan;

    private void OnDrawGizmos()
    {
        if (network == null)
            return;

        if (drawConnections)
        {
            foreach (RoadLaneConnectionV2 connection in network.AllConnections)
            {
                if (connection == null || !connection.IsValid)
                    continue;

                Gizmos.color = GetColorForMovement(connection.movementType);

                if (connection.curvePoints != null && connection.curvePoints.Count >= 2)
                {
                    for (int i = 0; i < connection.curvePoints.Count - 1; i++)
                    {
                        Gizmos.DrawLine(connection.curvePoints[i], connection.curvePoints[i + 1]);
                    }
                }
                else
                {
                    Vector3 from = connection.fromLane.end;
                    Vector3 mid = connection.junctionPoint;
                    Vector3 to = connection.toLane.start;

                    Gizmos.DrawLine(from, mid);
                    Gizmos.DrawLine(mid, to);
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (network == null)
            return;

        if (drawLaneIds)
        {
            foreach (RoadLaneDataV2 lane in network.AllLanes)
            {
                if (lane == null)
                    continue;

                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(lane.MidPoint, $"L{lane.laneId}");
            }
        }

        if (drawConnectionLabels)
        {
            foreach (RoadLaneConnectionV2 connection in network.AllConnections)
            {
                if (connection == null || !connection.IsValid)
                    continue;

                string label = GetLabelForMovement(connection.movementType);
                Vector3 labelPos = connection.junctionPoint + new Vector3(0.08f, 0.08f, 0f);

                UnityEditor.Handles.color = GetColorForMovement(connection.movementType);
                UnityEditor.Handles.Label(labelPos, label);
            }
        }
    }
#endif

    private Color GetColorForMovement(RoadLaneConnectionV2.MovementType movementType)
    {
        switch (movementType)
        {
            case RoadLaneConnectionV2.MovementType.Straight:
                return straightColor;

            case RoadLaneConnectionV2.MovementType.Left:
                return leftColor;

            case RoadLaneConnectionV2.MovementType.Right:
                return rightColor;
        }

        return Color.magenta;
    }

    private string GetLabelForMovement(RoadLaneConnectionV2.MovementType movementType)
    {
        switch (movementType)
        {
            case RoadLaneConnectionV2.MovementType.Straight:
                return "S";

            case RoadLaneConnectionV2.MovementType.Left:
                return "L";

            case RoadLaneConnectionV2.MovementType.Right:
                return "R";
        }

        return "?";
    }
}