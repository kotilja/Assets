using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class ParkingSpotV2 : MonoBehaviour
{
    [SerializeField] private bool isOccupied = false;
    [SerializeField] private RoadSegmentV2 connectedRoadSegment;
    [SerializeField] private Vector3 localForward = Vector3.right;
    [SerializeField] private bool pedestrianAnchorOnLeftSide = true;
    [SerializeField] private float pedestrianAnchorDistance = 0.35f;
    [SerializeField] private float gizmoLength = 0.7f;
    [SerializeField] private float gizmoWidth = 0.32f;

    private bool delayedGraphRebuildQueued = false;

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
            Vector3 fwd = Forward;
            Vector3 side = pedestrianAnchorOnLeftSide
                ? new Vector3(-fwd.y, fwd.x, 0f)
                : new Vector3(fwd.y, -fwd.x, 0f);

            if (side.sqrMagnitude < 0.0001f)
                side = Vector3.up;

            Vector3 p = ParkingPosition + side.normalized * Mathf.Max(0.05f, pedestrianAnchorDistance);
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
        SyncPedestrianAnchorSideFromRoad();
    }

    public void SetPedestrianAnchorSide(bool onLeftSide)
    {
        pedestrianAnchorOnLeftSide = onLeftSide;
    }

    public void SetPedestrianAnchorDistance(float distance)
    {
        pedestrianAnchorDistance = Mathf.Max(0.05f, distance);
    }

    private void OnValidate()
    {
        SyncPedestrianAnchorSideFromRoad();

#if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        if (delayedGraphRebuildQueued)
            return;

        delayedGraphRebuildQueued = true;
        EditorApplication.delayCall += DelayedRebuildPedestrianGraph;
#endif
    }

#if UNITY_EDITOR
    private void DelayedRebuildPedestrianGraph()
    {
        delayedGraphRebuildQueued = false;

        if (this == null || Application.isPlaying)
            return;

        PedestrianNetworkV2 pedestrianNetwork = FindFirstObjectByType<PedestrianNetworkV2>();
        if (pedestrianNetwork != null)
            pedestrianNetwork.RebuildGraph();
    }
#endif

    private void SyncPedestrianAnchorSideFromRoad()
    {
        if (connectedRoadSegment == null)
            return;

        List<Vector3> polyline = connectedRoadSegment.GetCenterPolylineWorld();
        if (polyline == null || polyline.Count < 2)
            return;

        Vector3 position = ParkingPosition;
        Vector3 snappedPoint = ProjectPointOntoPolyline(position, polyline);
        Vector3 tangent = GetPolylineDirectionAtPoint(polyline, snappedPoint);
        Vector3 toParking = position - snappedPoint;

        pedestrianAnchorOnLeftSide = Vector3.Cross(tangent.normalized, toParking).z >= 0f;
    }

    private Vector3 ProjectPointOntoPolyline(Vector3 point, List<Vector3> polyline)
    {
        if (polyline == null || polyline.Count < 2)
            return point;

        Vector3 bestPoint = point;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            Vector3 candidate = ProjectPointOntoSegment(point, polyline[i], polyline[i + 1]);
            float distance = Vector3.Distance(point, candidate);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = candidate;
            }
        }

        return bestPoint;
    }

    private Vector3 GetPolylineDirectionAtPoint(List<Vector3> polyline, Vector3 point)
    {
        if (polyline == null || polyline.Count < 2)
            return Vector3.right;

        float bestDistance = float.MaxValue;
        Vector3 bestDirection = Vector3.right;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            Vector3 a = polyline[i];
            Vector3 b = polyline[i + 1];
            Vector3 projected = ProjectPointOntoSegment(point, a, b);
            float distance = Vector3.Distance(point, projected);

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestDirection = (b - a).normalized;
        }

        bestDirection.z = 0f;
        return bestDirection.sqrMagnitude < 0.0001f ? Vector3.right : bestDirection.normalized;
    }

    private Vector3 ProjectPointOntoSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;

        if (ab.sqrMagnitude < 0.0001f)
            return a;

        float t = Vector3.Dot(point - a, ab) / Vector3.Dot(ab, ab);
        t = Mathf.Clamp01(t);

        Vector3 projected = a + ab * t;
        projected.z = 0f;
        return projected;
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
