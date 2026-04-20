using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class RoadBuildToolV2 : MonoBehaviour
{
    [System.Serializable]
    public class BuildingPrefabEntry
    {
        public string name;
        public GameObject prefab;
    }

    public enum ToolMode
    {
        None,
        DrawRoad,
        DrawCurveRoad,
        DrawPedestrianPath,
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
    [SerializeField] private PedestrianNetworkV2 pedestrianNetwork;
    [SerializeField] private bool toolEnabled = true;
    [SerializeField] private bool continueChain = true;

    [Header("Tool mode")]
    [SerializeField] private ToolMode toolMode = ToolMode.None;

    [Header("Road parameters")]
    [SerializeField] private int forwardLanes = 1;
    [SerializeField] private int backwardLanes = 1;
    [SerializeField] private float laneWidth = 0.6f;
    [SerializeField] private float speedLimit = 3f;
    [SerializeField] private float snapDistance = 0.4f;
    [SerializeField] private float minRoadSegmentLength = 0.75f;

    [Header("Draw assist")]
    [SerializeField] private bool snapToExistingSegments = true;
    [SerializeField] private float segmentSnapDistance = 0.35f;
    [SerializeField] private float minDistanceFromCurrentStartForSegmentSnap = 0.12f;
    [SerializeField] private float minChainTurnAngle = 90f;
    [SerializeField] private bool snapToGrid = true;
    [SerializeField] private float gridSize = 0.5f;

    [Header("Delete tool")]
    [SerializeField] private float deletePickDistance = 0.25f;

    [Header("Junction control tool")]
    [SerializeField] private float junctionPickDistance = 0.45f;

    [Header("Junction turns tool")]
    [SerializeField] private float approachPickDistance = 0.75f;
    [SerializeField] private float signalEditorPickRadius = 0.18f;

    [Header("Lane connections tool")]
    [SerializeField] private float lanePickDistance = 0.45f;

    [Header("Parking tool")]
    [SerializeField] private float parkingPickDistance = 0.9f;
    [SerializeField] private float parkingRoadOffset = 0.4f;
    [SerializeField] private float parkingSidewalkOffset = 0.8f;
    [SerializeField] private Color parkingPreviewColor = new Color(1f, 0.75f, 0.2f, 1f);

    [Header("Pedestrian path tool")]
    [SerializeField] private float pedestrianPathSnapDistance = 0.45f;
    [SerializeField] private float pedestrianPathDeletePickDistance = 0.3f;
    [SerializeField] private float minPedestrianPathLength = 0.35f;
    [SerializeField] private Color pedestrianPathPreviewColor = new Color(1f, 0.65f, 0.2f, 1f);

    [Header("Building tool")]
    [SerializeField] private float buildingDeletePickDistance = 0.6f;
    [SerializeField] private float buildingSidewalkSnapDistance = 1.5f;
    [SerializeField] private float buildingWalkableEdgeOffset = 0.16f;
    [SerializeField] private Color homePreviewColor = new Color(0.35f, 0.8f, 0.45f, 1f);
    [SerializeField] private Color officePreviewColor = new Color(0.35f, 0.55f, 0.95f, 1f);
    [SerializeField] private string residentialPrefabsFolder = "Assets/Prefabs/Residential";
    [SerializeField] private string officePrefabsFolder = "Assets/Prefabs/Office";
    [SerializeField] private List<BuildingPrefabEntry> residentialPrefabs = new List<BuildingPrefabEntry>();
    [SerializeField] private List<BuildingPrefabEntry> officePrefabs = new List<BuildingPrefabEntry>();
    [SerializeField] private int selectedResidentialPrefabIndex = 0;
    [SerializeField] private int selectedOfficePrefabIndex = 0;
    [SerializeField] private float currentBuildingRotationDegrees = 0f;

    [Header("Scene preview")]
    [SerializeField] private Color previewColor = Color.cyan;
    [SerializeField] private Color invalidPreviewColor = Color.red;
    [SerializeField] private Color deletePreviewColor = Color.red;
    [SerializeField] private Color junctionPreviewColor = Color.yellow;
    [SerializeField] private Color turnEditPreviewColor = new Color(1f, 0.4f, 1f, 1f);

    [SerializeField] private RoadNodeV2 currentStartNode;
    [SerializeField] private RoadNodeV2 currentChainPreviousNode;
    [SerializeField] private bool hasCurveControlPoint = false;
    [SerializeField] private Vector3 currentCurveControlPoint = Vector3.zero;
    [SerializeField] private bool hasPedestrianPathStart = false;
    [SerializeField] private Vector3 currentPedestrianPathStart = Vector3.zero;
    [SerializeField] private RoadNodeV2 selectedTurnNode;
    [SerializeField] private RoadSegmentV2 selectedIncomingSegment;
    [SerializeField] private RoadSegmentV2 selectedParkingSegment;
    [SerializeField] private bool selectedParkingOnLeftSide = true;
    [SerializeField] private Vector3 selectedParkingPosition = Vector3.zero;

    [SerializeField] private RoadNodeV2 selectedSignalNode;
    [SerializeField] private RoadSegmentV2 selectedSignalIncomingSegment;
    [SerializeField] private RoadNodeV2 selectedLaneConnectionNode;

    [SerializeField] private int selectedFromLaneId;
    [SerializeField] private int selectedToLaneId;

    

    public RoadNetworkV2 Network => network;
    public bool ToolEnabled => toolEnabled;
    public ToolMode CurrentToolMode => toolMode;

    public bool HasCurrentStartNode => currentStartNode != null;
    public Vector3 CurrentStartPosition => currentStartNode != null ? currentStartNode.transform.position : Vector3.zero;
    public bool HasCurveControlPoint => hasCurveControlPoint;
    public Vector3 CurrentCurveControlPoint => currentCurveControlPoint;
    public bool HasPedestrianPathStart => hasPedestrianPathStart;
    public Vector3 CurrentPedestrianPathStart => currentPedestrianPathStart;
    public int ForwardLanes => forwardLanes;
    public int BackwardLanes => backwardLanes;
    public float SpeedLimit => speedLimit;

    public float SnapDistance => snapDistance;
    public float MinRoadSegmentLength => minRoadSegmentLength;
    public float MinPedestrianPathLength => minPedestrianPathLength;
    public float DeletePickDistance => deletePickDistance;
    public float JunctionPickDistance => junctionPickDistance;
    public float ApproachPickDistance => approachPickDistance;
    public float SegmentSnapDistance => segmentSnapDistance;
    public float SignalEditorPickRadius => signalEditorPickRadius;
    public float MinChainTurnAngle => minChainTurnAngle;
    public bool SnapToGrid => snapToGrid;
    public float GridSize => Mathf.Max(0.05f, gridSize);

    public Color PreviewColor => previewColor;
    public Color InvalidPreviewColor => invalidPreviewColor;
    public Color ParkingPreviewColor => parkingPreviewColor;
    public Color PedestrianPathPreviewColor => pedestrianPathPreviewColor;
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

    public List<RoadSegmentV2> SelectedSignalIncomingSegments =>
        SelectedSignal != null ? SelectedSignal.GetIncomingSegments() : new List<RoadSegmentV2>();

    public float LanePickDistance => lanePickDistance;

    public RoadLaneDataV2 SelectedFromLane =>
        network != null ? network.FindLaneById(selectedFromLaneId) : null;

    public RoadLaneDataV2 SelectedToLane =>
        network != null ? network.FindLaneById(selectedToLaneId) : null;

    public RoadNodeV2 SelectedLaneConnectionNode => selectedLaneConnectionNode;
    public string ResidentialPrefabsFolder => residentialPrefabsFolder;
    public string OfficePrefabsFolder => officePrefabsFolder;

    public IReadOnlyList<BuildingPrefabEntry> ResidentialPrefabs => residentialPrefabs;
    public IReadOnlyList<BuildingPrefabEntry> OfficePrefabs => officePrefabs;
    public int SelectedResidentialPrefabIndex => selectedResidentialPrefabIndex;
    public int SelectedOfficePrefabIndex => selectedOfficePrefabIndex;
    public GameObject SelectedResidentialPrefab => GetSelectedBuildingPrefab(BuildingZoneV2.BuildingType.Home);
    public GameObject SelectedOfficePrefab => GetSelectedBuildingPrefab(BuildingZoneV2.BuildingType.Office);
    public float CurrentBuildingRotationDegrees => currentBuildingRotationDegrees;

    private void Awake()
    {
        RefreshBuildingPrefabCatalog();
    }

    private void OnValidate()
    {
        RefreshBuildingPrefabCatalog();
    }

    public void SetToolMode(ToolMode mode)
    {
        toolMode = mode;
        currentStartNode = null;
        currentChainPreviousNode = null;
        hasCurveControlPoint = false;
        currentCurveControlPoint = Vector3.zero;
        hasPedestrianPathStart = false;
        currentPedestrianPathStart = Vector3.zero;

        if (toolMode != ToolMode.JunctionTurns)
        {
            selectedTurnNode = null;
            selectedIncomingSegment = null;
        }

        if (toolMode != ToolMode.LaneConnections)
        {
            selectedFromLaneId = 0;
            selectedToLaneId = 0;
            selectedLaneConnectionNode = null;
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

    public void ClearActiveTool()
    {
        SetToolMode(ToolMode.None);
    }

    public void SetForwardLanes(int value)
    {
        forwardLanes = Mathf.Max(1, value);
    }

    public void SetBackwardLanes(int value)
    {
        backwardLanes = Mathf.Max(0, value);
    }

    public void SetSpeedLimit(float value)
    {
        speedLimit = Mathf.Max(0.1f, value);
    }

    public void AdjustForwardLanes(int delta)
    {
        SetForwardLanes(forwardLanes + delta);
    }

    public void AdjustBackwardLanes(int delta)
    {
        SetBackwardLanes(backwardLanes + delta);
    }

    public void AdjustSpeedLimit(float delta)
    {
        SetSpeedLimit(speedLimit + delta);
    }

    public void SetSnapToGrid(bool value)
    {
        snapToGrid = value;
    }

    public void SetGridSize(float value)
    {
        gridSize = Mathf.Max(0.05f, value);
    }

    public void AdjustCurrentBuildingRotation(float deltaDegrees)
    {
        currentBuildingRotationDegrees = Mathf.Repeat(currentBuildingRotationDegrees + deltaDegrees, 360f);
    }

    public void ResetCurrentBuildingRotation()
    {
        currentBuildingRotationDegrees = 0f;
    }

    public Vector3 GetPreviewWorldPosition(Vector3 rawWorldPosition)
    {
        rawWorldPosition.z = 0f;
        Vector3 gridSnappedWorldPosition = GetGridSnappedPosition(rawWorldPosition);

        if (toolMode == ToolMode.PlaceHome || toolMode == ToolMode.PlaceOffice)
            return gridSnappedWorldPosition;

        if (toolMode == ToolMode.DrawPedestrianPath)
            return GetPedestrianPathSnappedPosition(rawWorldPosition, gridSnappedWorldPosition);

        if (toolMode != ToolMode.DrawRoad && toolMode != ToolMode.DrawCurveRoad && toolMode != ToolMode.ParkingSpot)
            return rawWorldPosition;

        if (!snapToExistingSegments || network == null)
            return toolMode == ToolMode.ParkingSpot ? rawWorldPosition : gridSnappedWorldPosition;

        if (toolMode == ToolMode.DrawRoad)
        {
            if (network.GetNearestNode(rawWorldPosition, segmentSnapDistance) is RoadNodeV2 snappedNode)
            {
                Vector3 snappedNodePosition = snappedNode.transform.position;

                if (currentStartNode == null)
                    return snappedNodePosition;

                float nodeDistanceFromStart = Vector3.Distance(snappedNodePosition, currentStartNode.transform.position);
                if (nodeDistanceFromStart >= minDistanceFromCurrentStartForSegmentSnap)
                    return snappedNodePosition;
            }

            if (network.TryGetNearestPointOnSegment(
                rawWorldPosition,
                segmentSnapDistance,
                out Vector3 snappedPoint,
                out RoadSegmentV2 snappedSegment))
            {
                if (snappedSegment != null)
                {
                    if (currentStartNode == null)
                        return snappedPoint;

                    float distanceFromStart = Vector3.Distance(snappedPoint, currentStartNode.transform.position);

                    if (distanceFromStart >= minDistanceFromCurrentStartForSegmentSnap)
                        return snappedPoint;
                }
            }

            return gridSnappedWorldPosition;
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
        {
            if (network.GetNearestNode(rawWorldPosition, segmentSnapDistance) is RoadNodeV2 curveStartNode)
                return curveStartNode.transform.position;

            if (network.TryGetNearestPointOnSegment(
                rawWorldPosition,
                segmentSnapDistance,
                out Vector3 curveStartSnappedPoint,
                out RoadSegmentV2 curveStartSnappedSegment))
            {
                if (curveStartSnappedSegment != null)
                    return curveStartSnappedPoint;
            }

            return gridSnappedWorldPosition;
        }

        if (!hasCurveControlPoint)
            return gridSnappedWorldPosition;

        if (network.GetNearestNode(rawWorldPosition, segmentSnapDistance) is RoadNodeV2 curveEndNode)
        {
            Vector3 curveEndNodePosition = curveEndNode.transform.position;
            float nodeDistanceFromStart = Vector3.Distance(curveEndNodePosition, currentStartNode.transform.position);

            if (nodeDistanceFromStart >= minDistanceFromCurrentStartForSegmentSnap)
                return curveEndNodePosition;
        }

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

        return gridSnappedWorldPosition;
    }

    public Vector3 GetGridSnappedPosition(Vector3 worldPosition)
    {
        worldPosition.z = 0f;

        if (!snapToGrid)
            return worldPosition;

        float step = Mathf.Max(0.05f, gridSize);
        worldPosition.x = Mathf.Round(worldPosition.x / step) * step;
        worldPosition.y = Mathf.Round(worldPosition.y / step) * step;
        return worldPosition;
    }

    public void HandleSceneClick(Vector3 worldPosition)
    {
        if (!toolEnabled || network == null)
            return;

        worldPosition.z = 0f;

        switch (toolMode)
        {
            case ToolMode.None:
                break;

            case ToolMode.DrawRoad:
                HandleDrawClick(GetPreviewWorldPosition(worldPosition));
                break;

            case ToolMode.DrawCurveRoad:
                HandleDrawCurveClick(GetPreviewWorldPosition(worldPosition));
                break;

            case ToolMode.DrawPedestrianPath:
                HandleDrawPedestrianPathClick(GetPreviewWorldPosition(worldPosition));
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
            currentChainPreviousNode = null;
            return;
        }

        if (clickedNode == currentStartNode)
            return;

        if (!IsRoadSegmentValid(currentStartNode.transform.position, clickedNode.transform.position) ||
            !IsChainTurnValid(clickedNode.transform.position))
            return;

        network.CreateSegment(
            currentStartNode,
            clickedNode,
            forwardLanes,
            backwardLanes,
            laneWidth,
            speedLimit
        );

        if (continueChain)
        {
            currentChainPreviousNode = currentStartNode;
            currentStartNode = clickedNode;
        }
        else
        {
            currentStartNode = null;
            currentChainPreviousNode = null;
        }

        network.RefreshAll();
        RebuildPedestrianGraphIfPresent();
    }

    private void HandleDrawCurveClick(Vector3 worldPosition)
    {
        worldPosition.z = 0f;

        if (currentStartNode == null)
        {
            RoadNodeV2 clickedStartNode = network.GetOrCreateNodeNear(worldPosition, snapDistance);
            currentStartNode = clickedStartNode;
            currentChainPreviousNode = null;
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

        if (!IsCurveSegmentValid(currentStartNode.transform.position, previousControlPoint, clickedEndNode.transform.position) ||
            !IsChainTurnValid(clickedEndNode.transform.position))
            return;

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
            currentChainPreviousNode = currentStartNode;
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
            currentChainPreviousNode = null;
            hasCurveControlPoint = false;
            currentCurveControlPoint = Vector3.zero;
        }

        network.RefreshAll();
        RebuildPedestrianGraphIfPresent();
    }

    private void HandleDrawPedestrianPathClick(Vector3 worldPosition)
    {
        worldPosition.z = 0f;

        if (!hasPedestrianPathStart)
        {
            hasPedestrianPathStart = true;
            currentPedestrianPathStart = worldPosition;
            return;
        }

        if (!IsPedestrianPathPreviewValid(worldPosition))
            return;

        CreatePedestrianPath(currentPedestrianPathStart, worldPosition);

        if (continueChain)
            currentPedestrianPathStart = worldPosition;
        else
        {
            hasPedestrianPathStart = false;
            currentPedestrianPathStart = Vector3.zero;
        }
    }

    private Vector3 GetMirroredCurveControlPoint(Vector3 pivot, Vector3 previousControlPoint)
    {
        Vector3 mirrored = pivot + (pivot - previousControlPoint);
        mirrored.z = 0f;
        return mirrored;
    }

    public bool IsCurrentRoadPreviewValid(Vector3 previewWorldPosition)
    {
        previewWorldPosition.z = 0f;

        if (currentStartNode == null)
            return true;

        switch (toolMode)
        {
            case ToolMode.DrawRoad:
                return IsRoadSegmentValid(currentStartNode.transform.position, previewWorldPosition) &&
                       IsChainTurnValid(previewWorldPosition);

            case ToolMode.DrawCurveRoad:
                if (!hasCurveControlPoint)
                    return true;

                return IsCurveSegmentValid(
                    currentStartNode.transform.position,
                    currentCurveControlPoint,
                    previewWorldPosition
                ) && IsChainTurnValid(previewWorldPosition);

            default:
                return true;
        }
    }

    private bool IsRoadSegmentValid(Vector3 start, Vector3 end)
    {
        start.z = 0f;
        end.z = 0f;
        return Vector3.Distance(start, end) >= minRoadSegmentLength;
    }

    private bool IsCurveSegmentValid(Vector3 start, Vector3 control, Vector3 end)
    {
        start.z = 0f;
        control.z = 0f;
        end.z = 0f;

        return GetQuadraticCurveLength(start, control, end, 12) >= minRoadSegmentLength;
    }

    private bool IsChainTurnValid(Vector3 candidateEndPosition)
    {
        if (currentStartNode == null || currentChainPreviousNode == null)
            return true;

        Vector3 currentPosition = currentStartNode.transform.position;
        Vector3 previousDirection = (currentChainPreviousNode.transform.position - currentPosition).normalized;
        Vector3 nextDirection = (candidateEndPosition - currentPosition).normalized;

        previousDirection.z = 0f;
        nextDirection.z = 0f;

        if (previousDirection.sqrMagnitude < 0.0001f || nextDirection.sqrMagnitude < 0.0001f)
            return true;

        float turnAngle = Vector3.Angle(previousDirection, nextDirection);
        return turnAngle >= minChainTurnAngle;
    }

    private float GetQuadraticCurveLength(Vector3 start, Vector3 control, Vector3 end, int samples)
    {
        int sampleCount = Mathf.Max(2, samples);
        float length = 0f;
        Vector3 previous = start;

        for (int i = 1; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float u = 1f - t;
            Vector3 current = u * u * start + 2f * u * t * control + t * t * end;
            length += Vector3.Distance(previous, current);
            previous = current;
        }

        return length;
    }

    public void HandleDeleteClick(Vector3 worldPosition)
    {
        if (!toolEnabled || network == null)
            return;

        currentStartNode = null;
        hasPedestrianPathStart = false;
        currentPedestrianPathStart = Vector3.zero;

        if (DeleteNearestParkingSpotAtPoint(worldPosition, deletePickDistance))
            return;

        if (DeleteNearestBuildingAtPoint(worldPosition, Mathf.Max(deletePickDistance, buildingDeletePickDistance)))
            return;

        if (DeleteNearestPedestrianPathAtPoint(worldPosition, Mathf.Max(deletePickDistance, pedestrianPathDeletePickDistance)))
            return;

        network.DeleteNearestSegmentAtPoint(worldPosition, deletePickDistance);
        RebuildPedestrianGraphIfPresent();
    }

    private void HandleBuildingClick(Vector3 worldPosition, BuildingZoneV2.BuildingType buildingType)
    {
        currentStartNode = null;
        hasCurveControlPoint = false;
        currentCurveControlPoint = Vector3.zero;
        hasPedestrianPathStart = false;
        currentPedestrianPathStart = Vector3.zero;
        CreateBuildingFromSelectedPrefab(worldPosition, buildingType);
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

        Vector3 slotPosition = parkingPosition;
        slotPosition.z = 0f;

        ParkingSpotV2 spot = CreateParkingSpot(slotPosition, snappedSegment);
        if (spot != null)
            spot.SetPedestrianAnchorSide(onLeftSide);

        selectedParkingSegment = snappedSegment;
        selectedParkingOnLeftSide = onLeftSide;
        selectedParkingPosition = parkingPosition;

        if (spot == null)
            return;

        network.RefreshAll();

        RebuildPedestrianGraphIfPresent();
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

        RebuildPedestrianGraphIfPresent();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(network);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        return true;
    }

    public bool IsPedestrianPathPreviewValid(Vector3 previewWorldPosition)
    {
        previewWorldPosition.z = 0f;

        if (!hasPedestrianPathStart)
            return true;

        if (Vector3.Distance(currentPedestrianPathStart, previewWorldPosition) < Mathf.Max(0.05f, minPedestrianPathLength))
            return false;

        return !DoesPedestrianPathIntersectBuildings(currentPedestrianPathStart, previewWorldPosition);
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

        RebuildPedestrianGraphIfPresent();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif

        return true;
    }

    private bool DeleteNearestPedestrianPathAtPoint(Vector3 worldPosition, float pickDistance)
    {
        PedestrianPathV2[] paths = FindObjectsByType<PedestrianPathV2>(FindObjectsSortMode.None);
        if (paths == null || paths.Length == 0)
            return false;

        PedestrianPathV2 bestPath = null;
        float bestDistance = Mathf.Max(0f, pickDistance);

        for (int i = 0; i < paths.Length; i++)
        {
            PedestrianPathV2 path = paths[i];
            if (path == null)
                continue;

            List<Vector3> polyline = path.GetPolylineWorld();
            float distance = DistancePointToPolyline(worldPosition, polyline);
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            bestPath = path;
        }

        if (bestPath == null)
            return false;

#if UNITY_EDITOR
        Undo.DestroyObjectImmediate(bestPath.gameObject);
#else
        Destroy(bestPath.gameObject);
#endif

        PedestrianNetworkV2 pedestrianGraph = GetPedestrianNetwork();
        if (pedestrianGraph != null)
            pedestrianGraph.RebuildGraph();

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
        selectedSignalIncomingSegment = null;
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

    public int GetSelectedSignalPhaseCount()
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        return signal != null ? signal.GetPhaseCount() : 0;
    }

    public int GetSelectedSignalCurrentPhaseIndex()
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        return signal != null ? signal.CurrentPhaseIndex : -1;
    }

    public string GetSelectedSignalPhaseName(int phaseIndex)
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        if (signal == null || signal.Phases == null)
            return string.Empty;

        if (phaseIndex < 0 || phaseIndex >= signal.Phases.Count)
            return string.Empty;

        RoadNodeSignalV2.SignalPhase phase = signal.Phases[phaseIndex];
        return phase != null ? phase.name : string.Empty;
    }

    public void SetSelectedSignalPhase(int phaseIndex)
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        if (signal == null)
            return;

#if UNITY_EDITOR
        Undo.RecordObject(signal, "Set Signal Phase");
#endif

        signal.SetPhase(phaseIndex);
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

    public RoadNodeSignalV2.LampState GetSignalMovementState(
        RoadSegmentV2 incomingSegment,
        RoadLaneConnectionV2.MovementType movementType)
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        if (signal == null || incomingSegment == null)
            return RoadNodeSignalV2.LampState.Red;

        return signal.GetMovementStateInCurrentPhase(incomingSegment, movementType);
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

    public float GetSelectedSignalPhaseDuration()
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        return signal != null ? signal.GetCurrentPhaseDuration() : 0f;
    }

    public void SetSelectedSignalPhaseDuration(float duration)
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        if (signal == null)
            return;

#if UNITY_EDITOR
        Undo.RecordObject(signal, "Set Signal Phase Duration");
#endif

        signal.SetCurrentPhaseDuration(duration);
        signal.SyncFromNode();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(signal);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    public bool TryHandleSignalPhaseClick(Vector3 worldPosition)
    {
        RoadNodeSignalV2 signal = SelectedSignal;
        if (signal == null || selectedSignalNode == null)
            return false;

        List<RoadSegmentV2> incomingSegments = signal.GetIncomingSegments();
        for (int i = 0; i < incomingSegments.Count; i++)
        {
            RoadSegmentV2 segment = incomingSegments[i];
            if (segment == null)
                continue;

            if (TryHandleSignalMovementClick(segment, RoadLaneConnectionV2.MovementType.Left, worldPosition))
                return true;

            if (TryHandleSignalMovementClick(segment, RoadLaneConnectionV2.MovementType.Straight, worldPosition))
                return true;

            if (TryHandleSignalMovementClick(segment, RoadLaneConnectionV2.MovementType.Right, worldPosition))
                return true;
        }

        return false;
    }

    public bool TryGetSignalMovementEditorPosition(
        RoadSegmentV2 incomingSegment,
        RoadLaneConnectionV2.MovementType movementType,
        out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (selectedSignalNode == null || incomingSegment == null)
            return false;

        Vector3 nodePosition = selectedSignalNode.transform.position;
        Vector3 incomingDirection = GetIncomingSegmentDirectionTowardNode(incomingSegment, selectedSignalNode);
        if (incomingDirection.sqrMagnitude < 0.0001f)
            return false;

        Vector3 driverLeft = new Vector3(-incomingDirection.y, incomingDirection.x, 0f);
        Vector3 anchor = nodePosition - incomingDirection.normalized * 0.9f;
        float sideOffset = 0.28f;

        switch (movementType)
        {
            case RoadLaneConnectionV2.MovementType.Left:
                worldPosition = anchor + driverLeft * sideOffset;
                break;

            case RoadLaneConnectionV2.MovementType.Straight:
                worldPosition = anchor;
                break;

            case RoadLaneConnectionV2.MovementType.Right:
                worldPosition = anchor - driverLeft * sideOffset;
                break;

            default:
                return false;
        }

        worldPosition.z = 0f;
        return true;
    }

    private bool TryHandleSignalMovementClick(
        RoadSegmentV2 incomingSegment,
        RoadLaneConnectionV2.MovementType movementType,
        Vector3 worldPosition)
    {
        if (!TryGetSignalMovementEditorPosition(incomingSegment, movementType, out Vector3 circlePosition))
            return false;

        if (Vector3.Distance(circlePosition, worldPosition) > signalEditorPickRadius)
            return false;

        RoadNodeSignalV2 signal = SelectedSignal;
        if (signal == null)
            return false;

#if UNITY_EDITOR
        Undo.RecordObject(signal, "Cycle Signal Movement State");
#endif

        signal.CycleMovementStateInCurrentPhase(incomingSegment, movementType);
        signal.SyncFromNode();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(signal);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        return true;
    }

    private Vector3 GetIncomingSegmentDirectionTowardNode(RoadSegmentV2 segment, RoadNodeV2 node)
    {
        if (segment == null || node == null)
            return Vector3.zero;

        if (segment.EndNode == node && segment.StartNode != null)
            return (node.transform.position - segment.StartNode.transform.position).normalized;

        if (segment.StartNode == node && segment.EndNode != null)
            return (node.transform.position - segment.EndNode.transform.position).normalized;

        return Vector3.zero;
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
    selectedLaneConnectionNode = null;
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

    RoadNodeV2 clickedIntersection = network.GetNearestIntersectionNode(worldPosition, junctionPickDistance);
    if (clickedIntersection != null)
        selectedLaneConnectionNode = clickedIntersection;

    RoadLaneDataV2 currentFromLane = SelectedFromLane;

    if (currentFromLane == null)
    {
        RoadLaneDataV2 incomingLane = FindNearestIncomingLane(worldPosition, lanePickDistance);
        if (incomingLane != null)
        {
            selectedFromLaneId = incomingLane.laneId;
            selectedToLaneId = 0;
            selectedLaneConnectionNode = incomingLane.toNode;
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
        selectedLaneConnectionNode = newIncomingLane.toNode;
        return;
    }

    selectedFromLaneId = 0;
    selectedToLaneId = 0;

    if (clickedIntersection == null)
        selectedLaneConnectionNode = null;
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
        currentChainPreviousNode = null;
        hasCurveControlPoint = false;
        currentCurveControlPoint = Vector3.zero;
        hasPedestrianPathStart = false;
        currentPedestrianPathStart = Vector3.zero;
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

        RebuildPedestrianGraphIfPresent();
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

    private Vector3 GetPedestrianPathSnappedPosition(Vector3 rawWorldPosition, Vector3 gridSnappedWorldPosition)
    {
        rawWorldPosition.z = 0f;
        gridSnappedWorldPosition.z = 0f;

        PedestrianNetworkV2 pedestrianGraph = GetPedestrianNetwork();
        if (pedestrianGraph == null)
            return gridSnappedWorldPosition;

        PedestrianNetworkV2.PedestrianNodeDataV2 nearestNode = pedestrianGraph.GetNearestNode(rawWorldPosition, pedestrianPathSnapDistance);
        if (nearestNode != null)
            return nearestNode.position;

        if (pedestrianGraph.TryGetNearestWalkableAttachment(rawWorldPosition, pedestrianPathSnapDistance, true, out Vector3 attachmentPoint))
            return attachmentPoint;

        return gridSnappedWorldPosition;
    }

    private PedestrianPathV2 CreatePedestrianPath(Vector3 startPosition, Vector3 endPosition)
    {
        GameObject pathObject = new GameObject("PedestrianPath");
        pathObject.transform.SetParent(transform);
        pathObject.transform.position = Vector3.zero;
        pathObject.transform.rotation = Quaternion.identity;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(pathObject, "Create Pedestrian Path");
#endif

        PedestrianPathV2 path;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            path = Undo.AddComponent<PedestrianPathV2>(pathObject);
        else
            path = pathObject.AddComponent<PedestrianPathV2>();
#else
        path = pathObject.AddComponent<PedestrianPathV2>();
#endif

        if (path == null)
            return null;

        path.Initialize(startPosition, endPosition);

        PedestrianNetworkV2 pedestrianGraph = GetPedestrianNetwork();
        if (pedestrianGraph != null)
            pedestrianGraph.RebuildGraph();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(pathObject);
            EditorUtility.SetDirty(path);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        return path;
    }

    private PedestrianNetworkV2 GetPedestrianNetwork()
    {
        if (pedestrianNetwork == null)
            pedestrianNetwork = FindFirstObjectByType<PedestrianNetworkV2>();

        return pedestrianNetwork;
    }

    private void RebuildPedestrianGraphIfPresent()
    {
        PedestrianNetworkV2 pedestrianGraph = GetPedestrianNetwork();
        if (pedestrianGraph != null)
            pedestrianGraph.RebuildGraph();
    }

    public void RefreshBuildingPrefabCatalog()
    {
#if UNITY_EDITOR
        residentialPrefabs = LoadBuildingPrefabsFromFolder(residentialPrefabsFolder);
        officePrefabs = LoadBuildingPrefabsFromFolder(officePrefabsFolder);
        selectedResidentialPrefabIndex = ClampBuildingPrefabIndex(selectedResidentialPrefabIndex, residentialPrefabs);
        selectedOfficePrefabIndex = ClampBuildingPrefabIndex(selectedOfficePrefabIndex, officePrefabs);
#endif
    }

    public void SelectResidentialPrefab(int index)
    {
        selectedResidentialPrefabIndex = ClampBuildingPrefabIndex(index, residentialPrefabs);
    }

    public void SelectOfficePrefab(int index)
    {
        selectedOfficePrefabIndex = ClampBuildingPrefabIndex(index, officePrefabs);
    }

    public string GetSelectedBuildingPrefabName(BuildingZoneV2.BuildingType buildingType)
    {
        BuildingPrefabEntry entry = GetSelectedBuildingPrefabEntry(buildingType);
        return entry != null ? entry.name : string.Empty;
    }

    public Vector2 GetSelectedBuildingPrefabSize(BuildingZoneV2.BuildingType buildingType)
    {
        GameObject prefab = GetSelectedBuildingPrefab(buildingType);
        return GetBuildingPrefabSize(prefab);
    }

    public Vector3 GetSelectedBuildingPrefabCenterOffset(BuildingZoneV2.BuildingType buildingType)
    {
        GameObject prefab = GetSelectedBuildingPrefab(buildingType);
        return GetBuildingPrefabCenterOffset(prefab);
    }

    public bool TryGetBuildingPlacementPose(
        BuildingZoneV2.BuildingType buildingType,
        Vector3 worldPosition,
        out Vector3 rootPosition,
        out float rotationDegrees,
        out Vector2 size,
        out Vector3 centerOffset)
    {
        rootPosition = worldPosition;
        rotationDegrees = currentBuildingRotationDegrees;
        size = GetSelectedBuildingPrefabSize(buildingType);
        centerOffset = GetSelectedBuildingPrefabCenterOffset(buildingType);

        GameObject prefab = GetSelectedBuildingPrefab(buildingType);
        if (prefab == null)
            return false;

        Vector3 snappedPosition = GetPreviewWorldPosition(worldPosition);
        rootPosition = snappedPosition;

        if (TryGetNearestWalkableBuildingPlacement(snappedPosition, out Vector3 attachmentPoint, out Vector3 entranceFacingDirection))
        {
            Vector3 localEntrancePosition = GetBuildingPrefabEntranceLocalPosition(prefab);
            Vector3 localEntranceDirection = GetBuildingPrefabEntranceLocalDirection(prefab, centerOffset);

            float autoRotation = GetRotationFromDirection(localEntranceDirection, entranceFacingDirection);
            rotationDegrees = autoRotation;
            Quaternion rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            rootPosition = attachmentPoint - rotation * localEntrancePosition;
            rootPosition.z = 0f;
            return true;
        }

        rotationDegrees = currentBuildingRotationDegrees;
        rootPosition = snappedPosition;
        return true;
    }

    public bool IsBuildingPlacementValid(BuildingZoneV2.BuildingType buildingType, Vector3 worldPosition)
    {
        if (!TryGetBuildingPlacementPose(
            buildingType,
            worldPosition,
            out Vector3 rootPosition,
            out float rotationDegrees,
            out Vector2 size,
            out Vector3 centerOffset))
            return false;

        return !DoesBuildingOverlapExisting(rootPosition, rotationDegrees, size, centerOffset, null) &&
               !DoesBuildingOverlapRoad(rootPosition, rotationDegrees, size, centerOffset) &&
               !DoesBuildingOverlapWalkableNetwork(rootPosition, rotationDegrees, size, centerOffset);
    }

    private int ClampBuildingPrefabIndex(int index, List<BuildingPrefabEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return 0;

        return Mathf.Clamp(index, 0, entries.Count - 1);
    }

    private BuildingPrefabEntry GetSelectedBuildingPrefabEntry(BuildingZoneV2.BuildingType buildingType)
    {
        List<BuildingPrefabEntry> list = buildingType == BuildingZoneV2.BuildingType.Home ? residentialPrefabs : officePrefabs;
        int index = buildingType == BuildingZoneV2.BuildingType.Home ? selectedResidentialPrefabIndex : selectedOfficePrefabIndex;

        if (list == null || list.Count == 0)
            return null;

        index = Mathf.Clamp(index, 0, list.Count - 1);
        return list[index];
    }

    private GameObject GetSelectedBuildingPrefab(BuildingZoneV2.BuildingType buildingType)
    {
        BuildingPrefabEntry entry = GetSelectedBuildingPrefabEntry(buildingType);
        return entry != null ? entry.prefab : null;
    }

    private BuildingZoneV2 CreateBuildingFromSelectedPrefab(Vector3 position, BuildingZoneV2.BuildingType buildingType)
    {
        GameObject prefab = GetSelectedBuildingPrefab(buildingType);
        if (prefab == null)
            return null;

        if (!TryGetBuildingPlacementPose(buildingType, position, out Vector3 rootPosition, out float rotationDegrees, out Vector2 size, out Vector3 _))
            return null;

        Vector3 centerOffset = GetSelectedBuildingPrefabCenterOffset(buildingType);

        if (DoesBuildingOverlapExisting(rootPosition, rotationDegrees, size, centerOffset, null) ||
            DoesBuildingOverlapRoad(rootPosition, rotationDegrees, size, centerOffset) ||
            DoesBuildingOverlapWalkableNetwork(rootPosition, rotationDegrees, size, centerOffset))
            return null;

        GameObject buildingObject;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            buildingObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
            Undo.RegisterCreatedObjectUndo(buildingObject, "Create Building");
        }
        else
        {
            buildingObject = Instantiate(prefab, transform);
        }
#else
        buildingObject = Instantiate(prefab, transform);
#endif

        if (buildingObject == null)
            return null;

        buildingObject.transform.position = rootPosition;
        buildingObject.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        buildingObject.name = prefab.name;

        BuildingZoneV2 building = buildingObject.GetComponent<BuildingZoneV2>();
        if (building == null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                building = Undo.AddComponent<BuildingZoneV2>(buildingObject);
            else
                building = buildingObject.AddComponent<BuildingZoneV2>();
#else
                building = buildingObject.AddComponent<BuildingZoneV2>();
#endif
        }

        int capacity = Mathf.Max(1, building.Capacity);
        building.Initialize(buildingType, size, capacity);

        BuildingPseudo3DVisualV2 visualHelper = buildingObject.GetComponent<BuildingPseudo3DVisualV2>();
        if (visualHelper != null)
            visualHelper.SyncZoneFromSprite();

        RebuildPedestrianGraphIfPresent();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(buildingObject);
            EditorUtility.SetDirty(building);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        return building;
    }

    private bool TryGetNearestWalkableBuildingPlacement(Vector3 worldPosition, out Vector3 attachmentPoint, out Vector3 entranceFacingDirection)
    {
        attachmentPoint = worldPosition;
        entranceFacingDirection = Vector3.down;

        PedestrianNetworkV2 pedestrianGraph = GetPedestrianNetwork();
        if (pedestrianGraph == null)
            return false;

        if (!pedestrianGraph.TryGetNearestWalkableAttachment(
            worldPosition,
            Mathf.Max(0.1f, buildingSidewalkSnapDistance),
            true,
            out Vector3 projectedPoint,
            out Vector3 walkableDirection))
            return false;

        Vector3 toBuilding = worldPosition - projectedPoint;
        toBuilding.z = 0f;

        Vector3 normalA = new Vector3(-walkableDirection.y, walkableDirection.x, 0f);
        Vector3 normalB = -normalA;

        if (toBuilding.sqrMagnitude < 0.0001f)
            entranceFacingDirection = -normalA.normalized;
        else
            entranceFacingDirection = Vector3.Dot(normalA, toBuilding) >= Vector3.Dot(normalB, toBuilding)
                ? -normalA.normalized
                : -normalB.normalized;

        attachmentPoint = projectedPoint - entranceFacingDirection.normalized * Mathf.Max(0.01f, buildingWalkableEdgeOffset);
        attachmentPoint.z = 0f;
        return entranceFacingDirection.sqrMagnitude >= 0.0001f;
    }

    private bool TryGetNearestPointOnPolyline(List<Vector3> polyline, Vector3 point, out Vector3 nearestPoint, out float distance)
    {
        nearestPoint = Vector3.zero;
        distance = float.MaxValue;

        if (polyline == null || polyline.Count < 2)
            return false;

        bool found = false;
        for (int i = 0; i < polyline.Count - 1; i++)
        {
            Vector3 candidate = ProjectPointOntoSegment(point, polyline[i], polyline[i + 1]);
            float candidateDistance = Vector3.Distance(point, candidate);
            if (candidateDistance >= distance)
                continue;

            distance = candidateDistance;
            nearestPoint = candidate;
            found = true;
        }

        return found;
    }

    private Vector3 GetBuildingPrefabEntranceLocalPosition(GameObject prefab)
    {
        Transform root = prefab != null ? prefab.transform : null;
        Transform entrance = FindChildRecursive(root, "Entrance");
        if (entrance == null)
            return Vector3.zero;

        Vector3 localPosition = root != null
            ? root.InverseTransformPoint(entrance.position)
            : entrance.localPosition;
        localPosition.z = 0f;
        return localPosition;
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private Vector3 GetBuildingPrefabEntranceLocalDirection(GameObject prefab, Vector3 centerOffset)
    {
        Vector3 localEntrancePosition = GetBuildingPrefabEntranceLocalPosition(prefab);
        Vector3 localDirection = localEntrancePosition - centerOffset;
        localDirection.z = 0f;

        if (localDirection.sqrMagnitude < 0.0001f)
            return Vector3.down;

        return localDirection.normalized;
    }

    private float GetRotationFromDirection(Vector3 localDirection, Vector3 worldDirection)
    {
        localDirection.z = 0f;
        worldDirection.z = 0f;

        if (localDirection.sqrMagnitude < 0.0001f || worldDirection.sqrMagnitude < 0.0001f)
            return 0f;

        float localAngle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
        float worldAngle = Mathf.Atan2(worldDirection.y, worldDirection.x) * Mathf.Rad2Deg;
        return worldAngle - localAngle;
    }

    private Vector2 GetBuildingPrefabSize(GameObject prefab)
    {
        if (prefab == null)
            return new Vector2(1f, 1f);

        BuildingZoneV2 building = prefab.GetComponent<BuildingZoneV2>();
        if (building != null)
            return building.Size;

        SpriteRenderer renderer = GetBuildingPrefabSpriteRenderer(prefab);

        if (renderer != null && renderer.sprite != null)
        {
            Vector2 spriteSize = renderer.sprite.bounds.size;
            Vector3 scale = renderer.transform.localScale;
            return new Vector2(
                Mathf.Max(0.5f, Mathf.Abs(spriteSize.x * scale.x)),
                Mathf.Max(0.5f, Mathf.Abs(spriteSize.y * scale.y)));
        }

        return new Vector2(1f, 1f);
    }

    private Vector3 GetBuildingPrefabCenterOffset(GameObject prefab)
    {
        SpriteRenderer renderer = GetBuildingPrefabSpriteRenderer(prefab);
        if (renderer == null || renderer.sprite == null)
            return Vector3.zero;

        Vector3 offset = renderer.bounds.center - prefab.transform.position;
        offset.z = 0f;
        return offset;
    }

    private SpriteRenderer GetBuildingPrefabSpriteRenderer(GameObject prefab)
    {
        if (prefab == null)
            return null;

        SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
        if (renderer != null)
            return renderer;

        Transform baseChild = prefab.transform.Find("Base");
        if (baseChild != null)
            return baseChild.GetComponent<SpriteRenderer>();

        return null;
    }

#if UNITY_EDITOR
    private List<BuildingPrefabEntry> LoadBuildingPrefabsFromFolder(string folder)
    {
        List<BuildingPrefabEntry> result = new List<BuildingPrefabEntry>();
        if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
            return result;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<BuildingZoneV2>() == null)
                continue;

            result.Add(new BuildingPrefabEntry
            {
                name = prefab.name,
                prefab = prefab
            });
        }

        result.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        return result;
    }
#endif

    private bool DoesBuildingOverlapExisting(
        Vector3 rootPosition,
        float rotationDegrees,
        Vector2 size,
        Vector3 centerOffset,
        BuildingZoneV2 ignoreBuilding)
    {
        BuildingZoneV2[] buildings = FindObjectsByType<BuildingZoneV2>(FindObjectsSortMode.None);
        if (buildings == null || buildings.Length == 0)
            return false;

        Vector3[] candidateCorners = GetBuildingFootprintCorners(rootPosition, rotationDegrees, size, centerOffset);

        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingZoneV2 building = buildings[i];
            if (building == null || building == ignoreBuilding)
                continue;

            Vector3[] existingCorners = GetBuildingFootprintCorners(
                building.transform.position,
                building.transform.eulerAngles.z,
                building.Size,
                building.CenterOffset);

            if (DoConvexPolygonsOverlap(candidateCorners, existingCorners))
                return true;
        }

        return false;
    }

    private bool DoesBuildingOverlapRoad(Vector3 rootPosition, float rotationDegrees, Vector2 size, Vector3 centerOffset)
    {
        if (network == null)
            return false;

        Vector3[] buildingCorners = GetBuildingFootprintCorners(rootPosition, rotationDegrees, size, centerOffset);
        IReadOnlyList<RoadSegmentV2> segments = network.Segments;

        for (int i = 0; i < segments.Count; i++)
        {
            RoadSegmentV2 segment = segments[i];
            if (segment == null)
                continue;

            List<Vector3> centerPolyline = segment.GetCenterPolylineWorld();
            if (centerPolyline == null || centerPolyline.Count < 2)
                continue;

            float halfRoadWidth = Mathf.Max(0.05f, segment.TotalRoadWidth * 0.5f);

            for (int j = 0; j < centerPolyline.Count - 1; j++)
            {
                if (!TryBuildExpandedSegmentQuad(centerPolyline[j], centerPolyline[j + 1], halfRoadWidth, out Vector3[] roadQuad))
                    continue;

                if (DoConvexPolygonsOverlap(buildingCorners, roadQuad))
                    return true;
            }
        }

        return false;
    }

    private bool DoesBuildingOverlapWalkableNetwork(Vector3 rootPosition, float rotationDegrees, Vector2 size, Vector3 centerOffset)
    {
        Vector3[] buildingCorners = GetBuildingFootprintCorners(rootPosition, rotationDegrees, size, centerOffset);

        if (network != null)
        {
            IReadOnlyList<RoadSegmentV2> segments = network.Segments;
            for (int i = 0; i < segments.Count; i++)
            {
                RoadSegmentV2 segment = segments[i];
                if (segment == null)
                    continue;

                if (DoesPolylineIntersectPolygon(segment.GetLeftSidewalkPolylineWorld(), buildingCorners))
                    return true;

                if (DoesPolylineIntersectPolygon(segment.GetRightSidewalkPolylineWorld(), buildingCorners))
                    return true;
            }
        }

        PedestrianPathV2[] pedestrianPaths = FindObjectsByType<PedestrianPathV2>(FindObjectsSortMode.None);
        for (int i = 0; i < pedestrianPaths.Length; i++)
        {
            PedestrianPathV2 path = pedestrianPaths[i];
            if (path == null)
                continue;

            if (DoesPolylineIntersectPolygon(path.GetPolylineWorld(), buildingCorners))
                return true;
        }

        return false;
    }

    private bool DoesPedestrianPathIntersectBuildings(Vector3 startPosition, Vector3 endPosition)
    {
        BuildingZoneV2[] buildings = FindObjectsByType<BuildingZoneV2>(FindObjectsSortMode.None);
        if (buildings == null || buildings.Length == 0)
            return false;

        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingZoneV2 building = buildings[i];
            if (building == null)
                continue;

            Vector3[] corners = GetBuildingFootprintCorners(
                building.transform.position,
                building.transform.eulerAngles.z,
                building.Size,
                building.CenterOffset);

            if (DoesSegmentIntersectPolygon(startPosition, endPosition, corners))
                return true;
        }

        return false;
    }

    private bool DoesPolylineIntersectPolygon(List<Vector3> polyline, Vector3[] polygon)
    {
        if (polyline == null || polyline.Count < 2 || polygon == null || polygon.Length < 3)
            return false;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            if (DoesSegmentIntersectPolygon(polyline[i], polyline[i + 1], polygon))
                return true;
        }

        return false;
    }

    private Vector3[] GetBuildingFootprintCorners(Vector3 rootPosition, float rotationDegrees, Vector2 size, Vector3 centerOffset)
    {
        Quaternion rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        Vector3 center = rootPosition + rotation * centerOffset;
        Vector3 half = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);

        return new[]
        {
            center + rotation * new Vector3(-half.x, -half.y, 0f),
            center + rotation * new Vector3(half.x, -half.y, 0f),
            center + rotation * new Vector3(half.x, half.y, 0f),
            center + rotation * new Vector3(-half.x, half.y, 0f)
        };
    }

    private bool TryBuildExpandedSegmentQuad(Vector3 start, Vector3 end, float halfWidth, out Vector3[] quad)
    {
        quad = null;

        Vector3 direction = end - start;
        direction.z = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        direction.Normalize();
        Vector3 normal = new Vector3(-direction.y, direction.x, 0f) * Mathf.Max(0.01f, halfWidth);

        quad = new[]
        {
            start - normal,
            end - normal,
            end + normal,
            start + normal
        };
        return true;
    }

    private bool DoConvexPolygonsOverlap(Vector3[] a, Vector3[] b)
    {
        return !HasSeparatingAxis(a, b) && !HasSeparatingAxis(b, a);
    }

    private bool DoesSegmentIntersectPolygon(Vector3 start, Vector3 end, Vector3[] polygon)
    {
        if (polygon == null || polygon.Length < 3)
            return false;

        if (IsPointInsideConvexPolygon(start, polygon) || IsPointInsideConvexPolygon(end, polygon))
            return true;

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector3 a = polygon[i];
            Vector3 b = polygon[(i + 1) % polygon.Length];
            if (DoSegmentsIntersect(start, end, a, b))
                return true;
        }

        return false;
    }

    private bool IsPointInsideConvexPolygon(Vector3 point, Vector3[] polygon)
    {
        bool hasPositive = false;
        bool hasNegative = false;

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector3 a = polygon[i];
            Vector3 b = polygon[(i + 1) % polygon.Length];
            float cross = Cross2D(b - a, point - a);

            if (cross > 0.0001f)
                hasPositive = true;
            else if (cross < -0.0001f)
                hasNegative = true;

            if (hasPositive && hasNegative)
                return false;
        }

        return true;
    }

    private bool DoSegmentsIntersect(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2)
    {
        float d1 = Cross2D(a2 - a1, b1 - a1);
        float d2 = Cross2D(a2 - a1, b2 - a1);
        float d3 = Cross2D(b2 - b1, a1 - b1);
        float d4 = Cross2D(b2 - b1, a2 - b1);

        if (((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) &&
            ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f)))
            return true;

        if (Mathf.Abs(d1) <= 0.0001f && IsPointOnSegment(b1, a1, a2))
            return true;

        if (Mathf.Abs(d2) <= 0.0001f && IsPointOnSegment(b2, a1, a2))
            return true;

        if (Mathf.Abs(d3) <= 0.0001f && IsPointOnSegment(a1, b1, b2))
            return true;

        if (Mathf.Abs(d4) <= 0.0001f && IsPointOnSegment(a2, b1, b2))
            return true;

        return false;
    }

    private bool IsPointOnSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        return point.x >= Mathf.Min(segmentStart.x, segmentEnd.x) - 0.0001f &&
               point.x <= Mathf.Max(segmentStart.x, segmentEnd.x) + 0.0001f &&
               point.y >= Mathf.Min(segmentStart.y, segmentEnd.y) - 0.0001f &&
               point.y <= Mathf.Max(segmentStart.y, segmentEnd.y) + 0.0001f;
    }

    private float Cross2D(Vector3 a, Vector3 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private bool HasSeparatingAxis(Vector3[] source, Vector3[] target)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Vector3 p0 = source[i];
            Vector3 p1 = source[(i + 1) % source.Length];
            Vector3 edge = p1 - p0;
            Vector3 axis = new Vector3(-edge.y, edge.x, 0f);

            if (axis.sqrMagnitude < 0.0001f)
                continue;

            axis.Normalize();

            ProjectPolygonToAxis(source, axis, out float sourceMin, out float sourceMax);
            ProjectPolygonToAxis(target, axis, out float targetMin, out float targetMax);

            if (sourceMax < targetMin || targetMax < sourceMin)
                return true;
        }

        return false;
    }

    private void ProjectPolygonToAxis(Vector3[] polygon, Vector3 axis, out float min, out float max)
    {
        min = float.MaxValue;
        max = float.MinValue;

        for (int i = 0; i < polygon.Length; i++)
        {
            float projection = Vector3.Dot(polygon[i], axis);
            if (projection < min)
                min = projection;

            if (projection > max)
                max = projection;
        }
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

    private float DistancePointToPolyline(Vector3 point, List<Vector3> polyline)
    {
        if (polyline == null || polyline.Count == 0)
            return float.MaxValue;

        if (polyline.Count == 1)
            return Vector3.Distance(point, polyline[0]);

        float bestDistance = float.MaxValue;
        for (int i = 0; i < polyline.Count - 1; i++)
        {
            Vector3 projected = ProjectPointOntoSegment(point, polyline[i], polyline[i + 1]);
            float distance = Vector3.Distance(point, projected);
            if (distance < bestDistance)
                bestDistance = distance;
        }

        return bestDistance;
    }
}






