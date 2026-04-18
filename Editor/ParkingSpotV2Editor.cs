using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ParkingSpotV2Editor
{
    [MenuItem("GameObject/2nd City/Parking Spot", false, 10)]
    private static void CreateParkingSpot()
    {
        GameObject go = new GameObject("ParkingSpot");
        Undo.RegisterCreatedObjectUndo(go, "Create Parking Spot");

        ParkingSpotV2 spot = Undo.AddComponent<ParkingSpotV2>(go);

        Vector3 position = GetCreationPosition(out RoadSegmentV2 selectedSegment);
        go.transform.position = position;

        if (selectedSegment != null)
        {
            spot.SetConnectedRoadSegment(selectedSegment);
            spot.SetPedestrianAnchorSide(IsParkingOnLeftSide(selectedSegment, position));
        }
        else
            TryConnectToNearestRoadSegment(spot, position);

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    [MenuItem("GameObject/2nd City/Parking Spot", true)]
    private static bool ValidateCreateParkingSpot()
    {
        return true;
    }

    private static Vector3 GetCreationPosition(out RoadSegmentV2 selectedSegment)
    {
        selectedSegment = null;

        GameObject activeObject = Selection.activeGameObject;
        if (activeObject != null)
        {
            selectedSegment = activeObject.GetComponent<RoadSegmentV2>();
            if (selectedSegment != null)
                return GetParkingPositionOnSegment(selectedSegment);

            RoadNodeV2 selectedNode = activeObject.GetComponent<RoadNodeV2>();
            if (selectedNode != null)
                return selectedNode.transform.position + new Vector3(0.75f, 0.25f, 0f);
        }

        if (SceneView.lastActiveSceneView != null)
        {
            Vector3 pivot = SceneView.lastActiveSceneView.pivot;
            pivot.z = 0f;
            return pivot;
        }

        return Vector3.zero;
    }

    private static Vector3 GetParkingPositionOnSegment(RoadSegmentV2 segment)
    {
        if (segment == null)
            return Vector3.zero;

        RoadNodeV2 startNode = segment.StartNode;
        RoadNodeV2 endNode = segment.EndNode;

        if (startNode != null && endNode != null)
        {
            Vector3 a = startNode.transform.position;
            Vector3 b = endNode.transform.position;
            Vector3 midpoint = Vector3.Lerp(a, b, 0.5f);
            Vector3 dir = (b - a).normalized;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.right;

            Vector3 normal = new Vector3(-dir.y, dir.x, 0f);
            midpoint += normal * (segment.LaneWidth * 1.5f + 0.35f);
            midpoint.z = 0f;
            return midpoint;
        }

        if (startNode != null)
            return startNode.transform.position + new Vector3(0.75f, 0.25f, 0f);

        if (endNode != null)
            return endNode.transform.position + new Vector3(-0.75f, 0.25f, 0f);

        return segment.transform.position;
    }

    private static void TryConnectToNearestRoadSegment(ParkingSpotV2 spot, Vector3 position)
    {
        if (spot == null)
            return;

        RoadNetworkV2 roadNetwork = Object.FindFirstObjectByType<RoadNetworkV2>();
        if (roadNetwork == null)
            return;

        if (roadNetwork.TryGetNearestPointOnSegment(position, 1.5f, out _, out RoadSegmentV2 nearestSegment) && nearestSegment != null)
        {
            spot.SetConnectedRoadSegment(nearestSegment);
            spot.SetPedestrianAnchorSide(IsParkingOnLeftSide(nearestSegment, position));
        }
    }

    private static bool IsParkingOnLeftSide(RoadSegmentV2 segment, Vector3 parkingPosition)
    {
        if (segment == null)
            return true;

        List<Vector3> polyline = segment.GetCenterPolylineWorld();
        if (polyline == null || polyline.Count < 2)
            return true;

        Vector3 snappedPoint = ProjectOntoPolyline(parkingPosition, polyline);
        Vector3 tangent = GetDirectionAtPoint(polyline, snappedPoint);
        Vector3 toParking = parkingPosition - snappedPoint;

        return Vector3.Cross(tangent.normalized, toParking).z >= 0f;
    }

    private static Vector3 GetDirectionAtPoint(List<Vector3> polyline, Vector3 point)
    {
        if (polyline == null || polyline.Count < 2)
            return Vector3.right;

        float bestDistance = float.MaxValue;
        Vector3 bestDirection = Vector3.right;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            Vector3 a = polyline[i];
            Vector3 b = polyline[i + 1];
            Vector3 projected = ProjectOntoSegment(point, a, b);
            float distance = Vector3.Distance(point, projected);

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestDirection = (b - a).normalized;
        }

        bestDirection.z = 0f;
        return bestDirection.sqrMagnitude < 0.0001f ? Vector3.right : bestDirection.normalized;
    }

    private static Vector3 ProjectOntoPolyline(Vector3 point, List<Vector3> polyline)
    {
        if (polyline == null || polyline.Count < 2)
            return point;

        Vector3 bestPoint = point;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            Vector3 candidate = ProjectOntoSegment(point, polyline[i], polyline[i + 1]);
            float distance = Vector3.Distance(point, candidate);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = candidate;
            }
        }

        return bestPoint;
    }

    private static Vector3 ProjectOntoSegment(Vector3 point, Vector3 a, Vector3 b)
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
}
