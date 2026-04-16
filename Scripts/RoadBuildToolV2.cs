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
        DeleteRoad,
        JunctionControl,
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

    [Header("Scene preview")]
    [SerializeField] private Color previewColor = Color.cyan;
    [SerializeField] private Color deletePreviewColor = Color.red;
    [SerializeField] private Color junctionPreviewColor = Color.yellow;
    [SerializeField] private Color turnEditPreviewColor = new Color(1f, 0.4f, 1f, 1f);

    [SerializeField] private RoadNodeV2 currentStartNode;
    [SerializeField] private RoadNodeV2 selectedTurnNode;
    [SerializeField] private RoadSegmentV2 selectedIncomingSegment;

    [SerializeField] private int selectedFromLaneId;
    [SerializeField] private int selectedToLaneId;

    public RoadNetworkV2 Network => network;
    public bool ToolEnabled => toolEnabled;
    public ToolMode CurrentToolMode => toolMode;

    public bool HasCurrentStartNode => currentStartNode != null;
    public Vector3 CurrentStartPosition => currentStartNode != null ? currentStartNode.transform.position : Vector3.zero;

    public float SnapDistance => snapDistance;
    public float DeletePickDistance => deletePickDistance;
    public float JunctionPickDistance => junctionPickDistance;
    public float ApproachPickDistance => approachPickDistance;
    public float SegmentSnapDistance => segmentSnapDistance;

    public Color PreviewColor => previewColor;
    public Color DeletePreviewColor => deletePreviewColor;
    public Color JunctionPreviewColor => junctionPreviewColor;
    public Color TurnEditPreviewColor => turnEditPreviewColor;

    public RoadNodeV2 SelectedTurnNode => selectedTurnNode;
    public RoadSegmentV2 SelectedIncomingSegment => selectedIncomingSegment;

    public float LanePickDistance => lanePickDistance;

    public RoadLaneDataV2 SelectedFromLane =>
        network != null ? network.FindLaneById(selectedFromLaneId) : null;

    public RoadLaneDataV2 SelectedToLane =>
        network != null ? network.FindLaneById(selectedToLaneId) : null;

    public void SetToolMode(ToolMode mode)
    {
        toolMode = mode;
        currentStartNode = null;

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
    }

    public Vector3 GetPreviewWorldPosition(Vector3 rawWorldPosition)
    {
        rawWorldPosition.z = 0f;

        if (toolMode != ToolMode.DrawRoad)
            return rawWorldPosition;

        if (!snapToExistingSegments || network == null)
            return rawWorldPosition;

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

            case ToolMode.DeleteRoad:
                HandleDeleteClick(worldPosition);
                break;

            case ToolMode.JunctionControl:
                HandleJunctionControlClick(worldPosition);
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

    public void HandleDeleteClick(Vector3 worldPosition)
    {
        if (!toolEnabled || network == null)
            return;

        currentStartNode = null;
        network.DeleteNearestSegmentAtPoint(worldPosition, deletePickDistance);
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
        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(network);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
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
        EditorUtility.SetDirty(selectedTurnNode);
        EditorUtility.SetDirty(network);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
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
    EditorUtility.SetDirty(network);
    EditorSceneManager.MarkSceneDirty(gameObject.scene);
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
        EditorUtility.SetDirty(network);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
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
}