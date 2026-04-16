using UnityEngine;

[ExecuteAlways]
public class ParkingSpotV2 : MonoBehaviour
{
    [SerializeField] private bool isOccupied = false;
    [SerializeField] private RoadSegmentV2 connectedRoadSegment;
    [SerializeField] private Vector3 localForward = Vector3.right;
    [SerializeField] private Vector3 localPedestrianAnchor = new Vector3(0f, 0.8f, 0f);
    [SerializeField] private float gizmoLength = 0.7f;
    [SerializeField] private float gizmoWidth = 0.32f;

    public bool IsOccupied => isOccupied;
    public RoadSegmentV2 ConnectedRoadSegment => connectedRoadSegment;

    public Vector3 ParkingPosition => transform.position;

    public Vector3 Forward
    {
        get
        {
            Vector3 f = transform.TransformDirection(localForward.normalized);
            f.z = 0f;
            return f.sqrMagnitude < 0.0001f ? Vector3.right : f.normalized;
        }
    }

    public Vector3 PedestrianAnchorPoint
    {
        get
        {
            Vector3 p = transform.TransformPoint(localPedestrianAnchor);
            p.z = 0f;
            return p;
        }
    }

    public bool CanUse()
    {
        return !isOccupied;
    }

    public bool Reserve()
    {
        if (isOccupied)
            return false;

        isOccupied = true;
        return true;
    }

    public void Release()
    {
        isOccupied = false;
    }

    public void SetConnectedRoadSegment(RoadSegmentV2 segment)
    {
        connectedRoadSegment = segment;
    }

    private void OnDrawGizmos()
    {
        Vector3 pos = ParkingPosition;
        Vector3 fwd = Forward;
        Vector3 right = new Vector3(fwd.y, -fwd.x, 0f);

        Vector3 halfL = fwd * (gizmoLength * 0.5f);
        Vector3 halfW = right * (gizmoWidth * 0.5f);

        Vector3 p0 = pos - halfL - halfW;
        Vector3 p1 = pos + halfL - halfW;
        Vector3 p2 = pos + halfL + halfW;
        Vector3 p3 = pos - halfL + halfW;

        Gizmos.color = isOccupied ? new Color(1f, 0.3f, 0.3f, 1f) : new Color(0.3f, 1f, 0.3f, 1f);
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p0);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pos, pos + fwd * 0.7f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(PedestrianAnchorPoint, 0.06f);
    }
}