using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class RoadBuildToolV2 : MonoBehaviour
{
    public enum ToolMode
    {
        DrawRoad,
        DrawCurveRoad,
        PlaceHome,
        PlaceOffice,
        ParkingSpot,
        DeleteRoad,
        JunctionControl,
        JunctionKeepClear,
        JunctionSignals,
        JunctionTurns,
        LaneConnections
    }

    [SerializeField] private RoadNetworkV2 network;
    [SerializeField] private bool toolEnabled = true;
    [SerializeField] private bool continueChain = true;

    [Header("Tool mode")]
    [SerializeField] private ToolMode toolMode = ToolMode.DrawRoad;

    [Header("Road parameters")]
    [SerializeField] private int forwardLanes = 1;
    [SerializeField] private int backwardLanes = 1;
    [SerializeField] private float laneWidth = 0.6f;
    [SerializeField] private float speedLimit = 3f;
    [SerializeField] private float snapDistance = 0.4f;

    [Header("Draw assist")]
    [SerializeField] private bool snapToExistingSegments = true;
    [SerializeField] private float segmentSnapDistance = 0.35f;
    [SerializeField] private float minDistanceFromCurrentStartForSegmentSnap = 0.12f;

    [Header("Delete tool")]
    [SerializeField] private float deletePickDistance = 0.25f;

    [Header("Junction control tool")]
    [SerializeField] private float junctionPickDistance = 0.45f;

    [Header("Junction turns tool")]
    [SerializeField] private float approachPickDistance = 0.75f;

    [Header("Lane connections tool")]
    [SerializeField] private float lanePickDistance = 0.45f;

    [Header("Parking tool")]
    [SerializeField] private float parkingPickDistance = 0.9f;
    [SerializeField] private float parkingRoadOffset = 0.4f;
    [SerializeField] private float parkingSidewalkOffset = 0.8f;
    [SerializeField] private Color parkingPreviewColor = new Color(1f, 0.75f, 0.2f, 1f);

    [Header("Building tool")]
    [SerializeField] private float minBuildingSize = 0.8f;
    [SerializeField] private float buildingDeletePickDistance = 0.6f;
    [SerializeField] private Color homePreviewColor = new Color(0.35f, 0.8f, 0.45f, 1f);
    [SerializeField] private Color officePreviewColor = new Color(0.35f, 0.55f, 0.95f, 1f);

    [Header("Scene preview")]
    [SerializeField] private Color previewColor = Color.cyan;
    [SerializeField] private Color deletePreviewColor = Color.red;
    [SerializeField] private Color junctionPreviewColor = Color.yellow;
    [SerializeField] private Color turnEditPreviewColor = new Color(1f, 0.4f, 1f, 1f);

    [SerializeField] private RoadNodeV2 currentStartNode;
    [SerializeField] private bool hasCurveControlPoint = false;
    [SerializeField] private Vector3 currentCurveControlPoint = Vector3.zero;
    [SerializeField] private bool hasBuildingStartPoint = false;
    [SerializeField] private Vector3 currentBuildingStartPoint = Vector3.zero;
    [SerializeField] private RoadNodeV2 selectedTurnNode;
    [SerializeField] private RoadSegmentV2 selectedIncomingSegment;
    [SerializeField] private RoadSegmentV2 selectedParkingSegment;
    [SerializeField] private bool selectedParkingOnLeftSide = true;
    [SerializeField] private Vector3 selectedParkingPosition = Vector3.zero;

    [SerializeField] private RoadNodeV2 selectedSignalNode;
    [SerializeField] private RoadSegmentV2 selectedSignalIncomingSegment;

    [SerializeField] private int selectedFromLaneId;
    [SerializeField] private int selectedToLaneId;

    

    public RoadNetworkV2 Network => network;
    public bool ToolEnabled => toolEnabled;
    public ToolMode CurrentToolMode => toolMode;

    public bool HasCurrentStartNode => currentStartNode != null;
    public Vector3 CurrentStartPosition => currentStartNode != null ? currentStartNode.transform.position : Vector3.zero;
    public bool HasCurveControlPoint => hasCurveControlPoint;
    public Vector3 CurrentCurveControlPoint => currentCurveControlPoint;
    public bool HasBuildingStartPoint => hasBuildingStartPoint;
    public Vector3 CurrentBuildingStartPoint => currentBuildingStartPoint;

    public float SnapDistance => snapDistance;
    public float DeletePickDistance => deletePickDistance;
    public float JunctionPickDistance => junctionPickDistance;
    public float ApproachPickDistance => approachPickDistance;
    public float SegmentSnapDistance => segmentSnapDistance;

    public Color PreviewColor => previewColor;
    public Color ParkingPreviewColor => parkingPreviewColor;
    public Color HomePreviewColor => homePreviewColor;
    public Color OfficePreviewColor => officePreviewColor;
    public Color DeletePreviewColor => deletePreviewColor;
    public Color JunctionPreviewColor => junctionPreviewColor;
    public Color TurnEditPreviewColor => turnEditPreviewColor;

    public RoadNodeV2 SelectedTurnNode => selectedTurnNode;
    public RoadSegmentV2 SelectedIncomingSegment => selectedIncomingSegment;

    public RoadNodeV2 SelectedSignalNode => selectedSignalNode;
    public RoadSegmentV2 SelectedParkingSegment => selectedParkingSegment;
    public bool SelectedParkingOnLeftSide => selectedParkingOnLeftSide;
    public Vector3 SelectedParkingPosition => selectedParkingPosition;
    public RoadSegmentV2 SelectedSignalIncomingSegment => selectedSignalIncomingSegment;

    public RoadNodeSignalV2 SelectedSignal =>
        selectedSignalNode != null ? selectedSignalNode.GetComponent<RoadNodeSignalV2>() : null;

    public float LanePickDistance => lanePickDistance;

    public RoadLaneDataV2 SelectedFromLane =>
        network != null ? network.FindLaneById(selectedFromLaneId) : null;

    public RoadLaneDataV2 SelectedToLane =>
        network != null ? network.FindLaneById(selectedToLaneId) : null;

    public void SetToolMode(ToolMode mode)
    {
        toolMode = mode;
        currentStartNode = null;
        hasCurveControlPoint = false;
        currentCurveControlPoint = Vector3.zero;
        hasBuildingStartPoint = false;
        currentBuildingStartPoint = Vector3.zero;

        if (toolMode != ToolMode.JunctionTurns)
        {
            selectedTurnNode = null;
            selectedIncomingSegment = null;
        }

        if (toolMode != ToolMode.LaneConnections)
        {
            selectedFromLaneId = 0;
            selectedToLaneId = 0;
        }

        if (toolMode != ToolMode.JunctionSignals)
        {
            selectedSignalNode = null;
            selectedSignalIncomingSegment = null;
        }

        if (toolMode != ToolMode.ParkingSpot)
        {
            selectedParkingSegment = null;
            selectedParkingPosition = Vector3.zero;
        }
    }

    public Vector3 GetPreviewWorldPosition(Vector3 rawWorldPosition)
    {
        rawWorldPosition.z = 0f;

        if (toolMode != ToolMode.DrawRoad && toolMode != ToolMode.DrawCurveRoad && toolMode != ToolMode.ParkingSpot)
            return rawWorldPosition;

        if (!snapToExistingSegments || network == null)
            return rawWorldPosition;

        if (toolMode == ToolMode.DrawRoad)
        {
            if (currentStartNode == null)
                return rawWorldPosition;

            if (network.TryGetNearestPointOnSegment(
                rawWorldPosition,
                segmentSnapDistance,
                out Vector3 snappedPoint,
                out RoadSegmentV2 snappedSegment))
            {
                if (snappedSegment != null)
                {
                    float distanceFromStart = Vector3.Distance(snappedPoint, currentStartNode.transform.position);

                    if (distanceFromStart >= minDistanceFromCurrentStartForSegmentSnap)
                        return snappedPoint;
                }
            }

            return rawWorldPosition;
        }

        if (toolMode == ToolMode.ParkingSpot)
        {
            if (network.TryGetNearestPointOnSegment(
                rawWorldPosition,
                parkingPickDistance,
                out Vector3 parkingSnappedPoint,
                out RoadSegmentV2 parkingSnappedSegment))
            {
                if (parkingSnappedSegment != null)
                    return parkingSnappedPoint;
            }

            return rawWorldPosition;
        }

        // DrawCurveRoad:
        // 1-й клик — старт
        // 2-й клик — control point (не снапаем)
        // 3-й клик — конец дороги (снапаем)
        if (currentStartNode == null)
            return rawWorldPosition;

        if (!hasCurveControlPoint)
            return rawWorldPosition;

        if (network.TryGetNearestPointOnSegment(
            rawWorldPosition,
            segmentSnapDistance,
            out Vector3 curveEndSnappedPoint,
            out RoadSegmentV2 curveEndSnappedSegment))
        {
            if (curveEndSnappedSegment != null)
            {
                float distanceFromStart = Vector3.Distance(curveEndSnappedPoint, currentStartNode.transform.position);

                if (distanceFromStart >= minDistanceFromCurrentStartForSegmentSnap)
                    return curveEndSnappedPoint;
            }
        }

        return rawWorldPosition;
    }

    public void HandleSceneClick(Vector3 worldPosition)
    {
        if (!toolEnabled || network == null)
            return;

        worldPosition.z = 0f;

        switch (toolMode)
        {
            case ToolMode.DrawRoad:
                HandleDrawClick(GetPreviewWorldPosition(worldPosition));
                break;

            case ToolMode.DrawCurveRoad:
                HandleDrawCurveClick(GetPreviewWorldPosition(worldPosition));
                break;

            case ToolMode.PlaceHome:
                HandleBuildingClick(GetPreviewWorldPosition(worldPosition), BuildingZoneV2.BuildingType.Home);
                break;

            case ToolMode.PlaceOffice:
                HandleBuildingClick(GetPreviewWorldPosition(worldPosition), BuildingZoneV2.BuildingType.Office);
                break;

            case ToolMode.ParkingSpot:
                HandleParkingSpotClick(worldPosition);
                break;

            case ToolMode.DeleteRoad:
                HandleDeleteClick(worldPosition);
                break;

            case ToolMode.JunctionControl:
                HandleJunctionControlClick(worldPosition);
                break;

            case ToolMode.JunctionKeepClear:
                HandleJunctionKeepClearClick(worldPosition);
                break;

            case ToolMode.JunctionSignals:
                HandleJunctionSignalsClick(worldPosition);
                break;

            case ToolMode.JunctionTurns:
                HandleJunctionTurnsClick(worldPosition);
                break;

            case ToolMode.LaneConnections:
                HandleLaneConnectionsClick(worldPosition);
                break;

        }
    }

    private void HandleDrawClick(Vector3 worldPosition)
    {
        RoadNodeV2 clickedNode = network.GetOrCreateNodeNear(worldPosition, snapDistance);

        if (currentStartNode == null)
        {
            currentStartNode = clickedNode;
            return;
        }

        if (clickedNode == currentStartNode)
            return;

        network.CreateSegment(
            currentStartNode,
            clickedNode,
            forwardLanes,
            backwardLanes,
            laneWidth,
            speedLimit
        );

        currentStartNode = continueChain ? clickedNode : null;
        network.RefreshAll();
    }

    private void HandleDrawCurveClick(Vector3 worldPosition)
    {
        worldPosition.z = 0f;

        if (currentStartNode == null)
        {
            RoadNodeV2 clickedStartNode = network.GetOrCreateNodeNear(worldPosition, snapDistance);
            currentStartNode = clickedStartNode;
            hasCurveControlPoint = false;
            currentCurveControlPoint = Vector3.zero;
            return;
        }

        if (!hasCurveControlPoint)
        {
            if (Vector3.Distance(currentStartNode.transform.position, worldPosition) < 0.05f)
                return;

            hasCurveControlPoint = true;
            currentCurveControlPoint = worldPosition;
            return;
        }

        RoadNodeV2 clickedEndNode = network.GetOrCreateNodeNear(worldPosition, snapDistance);

        if (clickedEndNode == null || clickedEndNode == currentStartNode)
            return;

        Vector3 previousControlPoint = currentCurveControlPoint;

        network.CreateCurvedSegment(
            currentStartNode,
            clickedEndNode,
            previousControlPoint,
            forwardLanes,
            backwardLanes,
            laneWidth,
            speedLimit
        );

        if (continueChain)
        {
            currentStartNode = clickedEndNode;
            currentCurveControlPoint = GetMirroredCurveControlPoint(
                clickedEndNode.transform.position,
                previousControlPoint
            );
            hasCurveControlPoint = true;
        }
        else
        {
            currentStartNode = null;
            hasCurveControlPoint = false;
            currentCurveControlPoint = Vector3.zero;
        }

        network.RefreshAll();
    }

    private Vector3 GetMirroredCurveControlPoint(Vector3 pivot, Vector3 previousControlPoint)
    {
        Vector3 mirrored = pivot + (pivot - previousControlPoint);
        mirrored.z = 0f;
        return mirrored;
    }

    public void HandleDeleteClick(Vector3 worldPosition)
    {
        if (!toolEnabled || network == null)
            return;

        currentStartNode = null;

        if (DeleteNearestParkingSpotAtPoint(worldPosition, deletePickDistance))
            return;

        if (DeleteNearestBuildingAtPoint(worldPosition, Mathf.Max(deletePickDistance, buildingDeletePickDistance)))
            return;

        network.DeleteNearestSegmentAtPoint(worldPosition, deletePickDistance);
    }

    private void HandleBuildingClick(Vector3 worldPosition, BuildingZoneV2.BuildingType buildingType)
    {
        currentStartNode = null;
        hasCurveControlPoint = false;
        currentCurveControlPoint = Vector3.zero;

        if (!hasBuildingStartPoint)
        {
            hasBuildingStartPoint = true;
            currentBuildingStartPoint = worldPosition;
            return;
        }

        Vector3 endPoint = worldPosition;
        Vector2 size = new Vector2(
            Mathf.Max(minBuildingSize, Mathf.Abs(endPoint.x - currentBuildingStartPoint.x)),
            Mathf.Max(minBuildingSize, Mathf.Abs(endPoint.y - currentBuildingStartPoint.y))
        );

        Vector3 center = new Vector3(
            (currentBuildingStartPoint.x + endPoint.x) * 0.5f,
            (currentBuildingStartPoint.y + endPoint.y) * 0.5f,
            0f
        );

        CreateBuildingZone(center, size, buildingType);

        hasBuildingStartPoint = false;
        currentBuildingStartPoint = Vector3.zero;
    }

    public void HandleParkingSpotClick(Vector3 worldPosition)
    {
        if (!toolEnabled || network == null)
            return;

        currentStartNode = null;

        if (!network.TryGetNearestPointOnSegment(
            worldPosition,
            parkingPickDistance,
            out Vector3 snappedPoint,
            out RoadSegmentV2 snappedSegment))
        {
            selectedParkingSegment = null;
            selectedParkingPosition = Vector3.zero;
            return;
        }

        Vector3 parkingPosition = CalculateParkingPosition(
            snappedSegment,
            snappedPoint,
            worldPosition,
            out bool onLeftSide
        );

        ParkingSpotV2 spot = CreateParkingSpot(parkingPosition, snappedSegment);
        if (spot != null)
            spot.SetPedestrianAnchorSide(onLeftSide);

        selectedParkingSegment = snappedSegment;
        selectedParkingOnLeftSide = onLeftSide;
        selectedParkingPosition = parkingPosition;

        if (spot == null)
            return;

        network.RefreshAll();
    }

    private bool DeleteNearestParkingSpotAtPoint(Vector3 worldPosition, float pickDistance)
    {
        ParkingSpotV2[] parkingSpots = FindObjectsByType<ParkingSpotV2>(FindObjectsSortMode.None);
        if (parkingSpots == null || parkingSpots.Length == 0)
            return false;

        ParkingSpotV2 bestSpot = null;
        float bestDistance = Mathf.Max(0f, pickDistance);

        for (int i = 0; i < parkingSpots.Length; i++)
        {
            ParkingSpotV2 spot = parkingSpots[i];
            if (spot == null)
                continue;

            float distance = Vector3.Distance(worldPosition, spot.ParkingPosition);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                bestSpot = spot;
            }
        }

        if (bestSpot == null)
            return false;

#if UNITY_EDITOR
        Undo.DestroyObjectImmediate(bestSpot.gameObject);
#else
        Destroy(bestSpot.gameObject);
#endif

        network.RefreshAll();

        PedestrianNetworkV2 pedestrianNetwork = FindFirstObjectByType<PedestrianNetworkV2>();
        if (pedestrianNetwork != null)
            pedestrianNetwork.RebuildGraph();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(network);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        return true;
    }

    private bool DeleteNearestBuildingAtPoint(Vector3 worldPosition, float pickDistance)
    {
        BuildingZoneV2[] buildings = FindObjectsByType<BuildingZoneV2>(FindObjectsSortMode.None);
        if (buildings == null || buildings.Length == 0)
            return false;

        BuildingZoneV2 bestBuilding = null;
        float bestDistance = Mathf.Max(0f, pickDistance);

        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingZoneV2 building = buildings[i];
            if (building == null)
                continue;

            float distance = DistancePointToBuilding(worldPosition, building);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                bestBuilding = building;
            }
        }

        if (bestBuilding == null)
            return false;

#if UNITY_EDITOR
        Undo.DestroyObjectImmediate(bestBuilding.gameObject);
#else
        Destroy(bestBuilding.gameObject);
#endif

        PedestrianNetworkV2 pedestrianNetwork = FindFirstObjectByType<PedestrianNetworkV2>();
        if (pedestrianNetwork != null)
            pedestrianNetwork.RebuildGraph();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif

        return true;
    }

    public void HandleJunctionControlClick(Vector3 worldPosition)
    {
        if (!toolEnabled || network == null)
            return;

        currentStartNode = null;

        RoadNodeV2 node = network.GetNearestIntersectionNode(worldPosition, junctionPickDistance);
        if (node == null)
            return;

#if UNITY_EDITOR
        Undo.RecordObject(node, "Toggle Junction Control Mode");
#endif

        node.ToggleControlMode();

        RoadNodeSignalV2 signal = node.GetComponent<RoadNodeSignalV2>();

        if (node.UsesTrafficLight)
        {
            if (signal == null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    signal = Undo.AddComponent<RoadNodeSignalV2>(node.gameObject);
                else
                    signal = node.gameObject.AddComponent<RoadNodeSignalV2>();
#else
                signal = node.gameObject.AddComponent<RoadNodeSignalV2>();
#endif
            }

            if (signal != null)
                signal.SyncFromNode();
        }
        else
        {
            if (signal != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.DestroyObjectImmediate(signal);
                else
                    Destroy(signal);
#else
                Destroy(signal);
#endif
            }
        }

        network.RefreshAll();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(node);
            EditorUtility.SetDirty(network);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    public void HandleJunctionTurnsClick(Vector3 worldPosition)
    {
        if (!toolEnabled || network == null)
            return;

        currentStartNode = null;

        RoadNodeV2 node = network.GetNearestIntersectionNode(worldPosition, junctionPickDistance);
        if (node == null)
        {
            selectedTurnNode = null;
            selectedIncomingSegment = null;
            return;
        }

        selectedTurnNode = node;
        selectedIncomingSegment = FindNearestIncomingSegment(node, worldPosition, approachPickDistance);
    }

    public void HandleJunctionKeepClearClick(Vector3 worldPosition)
    {
        if (!toolEnabled || network == null)
            return;

        currentStartNode = null;

        RoadNodeV2 node = network.GetNearestIntersectionNode(worldPosition, junctionPickDistance);
        if (node == null)
            return;

#if UNITY_EDITOR
    Undo.RecordObject(node, "Toggle Junction Keep Clear");
#endif

        node.ToggleKeepIntersectionClear();
        network.RefreshAll();

#if UNITY_EDITOR
    if (!Application.isPlaying)
    {
        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(network);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
    }

    public void HandleJunctionSignalsClick(Vector3 worldPosition)
    {
        if (!toolEnabled || network == null)
            return;

        currentStartNode = null;

        RoadNodeV2 node = network.GetNearestIntersectionNode(worldPosition, junctionPickDistance);
        if (node == null || !node.UsesTrafficLight)
        {
            selectedSignalNode = null;
            selectedSignalIncomingSegment = null;
            return;
        }

        RoadNodeSignalV2 signal = node.GetComponent<RoadNodeSignalV2>();
        if (signal == null)
        {
#if UNITY_EDITOR
        signal = Undo.AddComponent<RoadNodeSignalV2>(node.gameObject);
#else
            signal = node.gameObject.AddComponent<RoadNodeSignalV2>();
#endif
        }

        signal.SyncFromNode();

        selectedSignalNode = node;
        selectedSignalIncomingSegment = FindNearestIncomingSegment(node, worldPosition, approachPickDistance);
    }

    public void SelectPreviousSignalPhase()
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        if (signal == null)
            return;

#if UNITY_EDITOR
    Undo.RecordObject(signal, "Previous Signal Phase");
#endif

        signal.SelectPreviousPhase();
        signal.SyncFromNode();

#if UNITY_EDITOR
    if (!Application.isPlaying)
    {
        EditorUtility.SetDirty(signal);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
    }

    public void SelectNextSignalPhase()
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        if (signal == null)
            return;

#if UNITY_EDITOR
    Undo.RecordObject(signal, "Next Signal Phase");
#endif

        signal.SelectNextPhase();
        signal.SyncFromNode();

#if UNITY_EDITOR
    if (!Application.isPlaying)
    {
        EditorUtility.SetDirty(signal);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
    }

    public void AddSignalPhase()
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        if (signal == null)
            return;

#if UNITY_EDITOR
    Undo.RecordObject(signal, "Add Signal Phase");
#endif

        signal.AddPhaseCopyOfCurrent();
        signal.SyncFromNode();

#if UNITY_EDITOR
    if (!Application.isPlaying)
    {
        EditorUtility.SetDirty(signal);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
    }

    public void RemoveSignalPhase()
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        if (signal == null)
            return;

#if UNITY_EDITOR
    Undo.RecordObject(signal, "Remove Signal Phase");
#endif

        signal.RemoveCurrentPhase();
        signal.SyncFromNode();

#if UNITY_EDITOR
    if (!Application.isPlaying)
    {
        EditorUtility.SetDirty(signal);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
    }

    public bool GetSelectedSignalMovementAllowed(RoadLaneConnectionV2.MovementType movementType)
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        if (signal == null || selectedSignalIncomingSegment == null)
            return false;

        return signal.GetMovementAllowedInCurrentPhase(selectedSignalIncomingSegment, movementType);
    }

    public void ToggleSelectedSignalMovement(RoadLaneConnectionV2.MovementType movementType)
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        if (signal == null || selectedSignalIncomingSegment == null)
            return;

#if UNITY_EDITOR
    Undo.RecordObject(signal, "Toggle Signal Movement");
#endif

        signal.ToggleMovementInCurrentPhase(selectedSignalIncomingSegment, movementType);
        signal.SyncFromNode();

#if UNITY_EDITOR
    if (!Application.isPlaying)
    {
        EditorUtility.SetDirty(signal);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
    }

    public void ClearSignalSelection()
    {
        selectedSignalNode = null;
        selectedSignalIncomingSegment = null;
    }

    public void ToggleSelectedApproachMovement(RoadLaneConnectionV2.MovementType movementType)
    {
        if (selectedTurnNode == null || selectedIncomingSegment == null)
            return;

#if UNITY_EDITOR
        Undo.RecordObject(selectedTurnNode, "Toggle Approach Movement");
#endif

        selectedTurnNode.ToggleApproachMovement(selectedIncomingSegment, movementType);
        network.RefreshAll();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(selectedTurnNode);
            EditorUtility.SetDirty(network);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    public bool GetSelectedApproachMovementAllowed(RoadLaneConnectionV2.MovementType movementType)
    {
        if (selectedTurnNode == null || selectedIncomingSegment == null)
            return false;

        return selectedTurnNode.AllowsMovement(selectedIncomingSegment, movementType);
    }

public void ClearLaneConnectionSelection()
{
    selectedFromLaneId = 0;
    selectedToLaneId = 0;
}

public bool SelectedManualConnectionExists()
{
    if (network == null || selectedFromLaneId <= 0 || selectedToLaneId <= 0)
        return false;

    return network.HasManualConnection(selectedFromLaneId, selectedToLaneId);
}

public bool SelectedFromLaneHasManualConnections()
{
    if (network == null || selectedFromLaneId <= 0)
        return false;

    return network.HasManualConnectionsForLane(selectedFromLaneId);
}

public void ClearManualConnectionsForSelectedLane()
{
    if (network == null || selectedFromLaneId <= 0)
        return;

#if UNITY_EDITOR
    Undo.RecordObject(network, "Clear Manual Lane Connections");
#endif

    network.ClearManualConnectionsForLane(selectedFromLaneId);

#if UNITY_EDITOR
    if (!Application.isPlaying)
    {
        EditorUtility.SetDirty(network);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}

private void HandleLaneConnectionsClick(Vector3 worldPosition)
{
    if (!toolEnabled || network == null)
        return;

    currentStartNode = null;
    selectedTurnNode = null;
    selectedIncomingSegment = null;

    RoadLaneDataV2 currentFromLane = SelectedFromLane;

    if (currentFromLane == null)
    {
        RoadLaneDataV2 incomingLane = FindNearestIncomingLane(worldPosition, lanePickDistance);
        if (incomingLane != null)
        {
            selectedFromLaneId = incomingLane.laneId;
            selectedToLaneId = 0;
        }

        return;
    }

    RoadLaneDataV2 outgoingLane = FindNearestOutgoingLane(currentFromLane.toNode, worldPosition, lanePickDistance);

    if (outgoingLane != null)
    {
#if UNITY_EDITOR
        Undo.RecordObject(network, "Toggle Manual Lane Connection");
#endif

        selectedToLaneId = outgoingLane.laneId;
        network.ToggleManualLaneConnection(currentFromLane.laneId, outgoingLane.laneId);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(network);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
        return;
    }

    RoadLaneDataV2 newIncomingLane = FindNearestIncomingLane(worldPosition, lanePickDistance);
    if (newIncomingLane != null)
    {
        selectedFromLaneId = newIncomingLane.laneId;
        selectedToLaneId = 0;
        return;
    }

    selectedFromLaneId = 0;
    selectedToLaneId = 0;
}

private RoadLaneDataV2 FindNearestIncomingLane(Vector3 worldPosition, float maxDistance)
{
    if (network == null)
        return null;

    float bestDistance = maxDistance;
    RoadLaneDataV2 bestLane = null;

    for (int i = 0; i < network.AllLanes.Count; i++)
    {
        RoadLaneDataV2 lane = network.AllLanes[i];
        if (lane == null || lane.toNode == null)
            continue;

        if (lane.toNode.ConnectedSegments.Count <= 2)
            continue;

        float distance = DistancePointToSegment(worldPosition, lane.start, lane.end);
        if (distance <= bestDistance)
        {
            bestDistance = distance;
            bestLane = lane;
        }
    }

    return bestLane;
}

private RoadLaneDataV2 FindNearestOutgoingLane(RoadNodeV2 node, Vector3 worldPosition, float maxDistance)
{
    if (network == null || node == null)
        return null;

    float bestDistance = maxDistance;
    RoadLaneDataV2 bestLane = null;

    for (int i = 0; i < network.AllLanes.Count; i++)
    {
        RoadLaneDataV2 lane = network.AllLanes[i];
        if (lane == null || lane.fromNode != node)
            continue;

        float distance = DistancePointToSegment(worldPosition, lane.start, lane.end);
        if (distance <= bestDistance)
        {
            bestDistance = distance;
            bestLane = lane;
        }
    }

    return bestLane;
}

    private float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;

            if (ab.sqrMagnitude < 0.0001f)
                return Vector3.Distance(point, a);

            float t = Vector3.Dot(point - a, ab) / Vector3.Dot(ab, ab);
            t = Mathf.Clamp01(t);

            Vector3 projection = a + ab * t;
            return Vector3.Distance(point, projection);
        }

    public void ClearCurrentChain()
    {
        currentStartNode = null;
        hasCurveControlPoint = false;
        currentCurveControlPoint = Vector3.zero;
    }

    public void ClearTurnSelection()
    {
        selectedTurnNode = null;
        selectedIncomingSegment = null;
    }

    public void RefreshNetworkVisuals()
    {
        if (network != null)
            network.RefreshAll();

        PedestrianNetworkV2 pedestrianNetwork = FindFirstObjectByType<PedestrianNetworkV2>();
        if (pedestrianNetwork != null)
            pedestrianNetwork.RebuildGraph();
    }

    private RoadSegmentV2 FindNearestIncomingSegment(RoadNodeV2 node, Vector3 worldPosition, float maxDistance)
    {
        if (node == null)
            return null;

        float bestDistance = maxDistance;
        RoadSegmentV2 best = null;

        for (int i = 0; i < node.ConnectedSegments.Count; i++)
        {
            RoadSegmentV2 segment = node.ConnectedSegments[i];
            if (segment == null)
                continue;

            if (!IsIncomingForNode(segment, node))
                continue;

            Vector3 probe = GetIncomingProbePoint(segment, node);
            float distance = Vector3.Distance(probe, worldPosition);

            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = segment;
            }
        }

        return best;
    }

    private bool IsIncomingForNode(RoadSegmentV2 segment, RoadNodeV2 node)
    {
        if (segment == null || node == null)
            return false;

        if (segment.EndNode == node && segment.ForwardLanes > 0)
            return true;

        if (segment.StartNode == node && segment.BackwardLanes > 0)
            return true;

        return false;
    }

    private Vector3 GetIncomingProbePoint(RoadSegmentV2 segment, RoadNodeV2 node)
    {
        if (segment == null || node == null)
            return Vector3.zero;

        Vector3 nodePos = node.transform.position;
        Vector3 otherPos = nodePos;

        if (segment.EndNode == node && segment.StartNode != null)
            otherPos = segment.StartNode.transform.position;
        else if (segment.StartNode == node && segment.EndNode != null)
            otherPos = segment.EndNode.transform.position;

        Vector3 dir = (nodePos - otherPos).normalized;
        return nodePos - dir * 0.55f;
    }

    private ParkingSpotV2 CreateParkingSpot(Vector3 position, RoadSegmentV2 connectedSegment)
    {
        GameObject parkingObject = new GameObject("ParkingSpot");
        parkingObject.transform.SetParent(transform);
        parkingObject.transform.position = position;

        Vector3 forward = Vector3.right;
        if (connectedSegment != null)
        {
            List<Vector3> polyline = connectedSegment.GetCenterPolylineWorld();
            if (polyline != null && polyline.Count >= 2)
                forward = GetPolylineDirectionAtPoint(polyline, position);
        }

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.right;

        float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
        parkingObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(parkingObject, "Create Parking Spot");
#endif

        ParkingSpotV2 spot;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            spot = Undo.AddComponent<ParkingSpotV2>(parkingObject);
        else
            spot = parkingObject.AddComponent<ParkingSpotV2>();
#else
        spot = parkingObject.AddComponent<ParkingSpotV2>();
#endif

        if (connectedSegment != null)
            spot.SetConnectedRoadSegment(connectedSegment);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(spot);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        return spot;
    }

    private BuildingZoneV2 CreateBuildingZone(Vector3 center, Vector2 size, BuildingZoneV2.BuildingType buildingType)
    {
        GameObject buildingObject = new GameObject(buildingType == BuildingZoneV2.BuildingType.Home ? "Home" : "Office");
        buildingObject.transform.SetParent(transform);
        buildingObject.transform.position = center;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(buildingObject, "Create Building");
#endif

        BuildingZoneV2 building;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            building = Undo.AddComponent<BuildingZoneV2>(buildingObject);
        else
            building = buildingObject.AddComponent<BuildingZoneV2>();
#else
        building = buildingObject.AddComponent<BuildingZoneV2>();
#endif

        building.Initialize(buildingType, size, 2);

        PedestrianNetworkV2 pedestrianNetwork = FindFirstObjectByType<PedestrianNetworkV2>();
        if (pedestrianNetwork != null)
            pedestrianNetwork.RebuildGraph();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(building);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        return building;
    }

    private float DistancePointToBuilding(Vector3 worldPosition, BuildingZoneV2 building)
    {
        if (building == null)
            return float.MaxValue;

        Vector2 half = building.Size * 0.5f;
        Vector3 center = building.Position;

        float dx = Mathf.Max(0f, Mathf.Abs(worldPosition.x - center.x) - half.x);
        float dy = Mathf.Max(0f, Mathf.Abs(worldPosition.y - center.y) - half.y);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private Vector3 CalculateParkingPosition(
        RoadSegmentV2 segment,
        Vector3 snappedPoint,
        Vector3 clickPosition,
        out bool onLeftSide)
    {
        onLeftSide = true;

        if (segment == null)
            return snappedPoint;

        List<Vector3> polyline = segment.GetCenterPolylineWorld();
        Vector3 tangent = GetPolylineDirectionAtPoint(polyline, snappedPoint);
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.right;

        Vector3 toClick = clickPosition - snappedPoint;
        float sideSign = Vector3.Cross(tangent.normalized, toClick).z;
        onLeftSide = sideSign >= 0f;

        Vector3 normal = onLeftSide
            ? new Vector3(-tangent.y, tangent.x, 0f)
            : new Vector3(tangent.y, -tangent.x, 0f);

        float offset = Mathf.Max(parkingRoadOffset, segment.TotalRoadWidth * 0.5f + parkingSidewalkOffset);
        Vector3 parkingPosition = snappedPoint + normal.normalized * offset;
        parkingPosition.z = 0f;
        return parkingPosition;
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

            if (distance > bestDistance)
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
}






