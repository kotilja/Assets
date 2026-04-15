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
        JunctionTurns
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

    [Header("Delete tool")]
    [SerializeField] private float deletePickDistance = 0.25f;

    [Header("Junction control tool")]
    [SerializeField] private float junctionPickDistance = 0.45f;

    [Header("Junction turns tool")]
    [SerializeField] private float approachPickDistance = 0.75f;

    [Header("Scene preview")]
    [SerializeField] private Color previewColor = Color.cyan;
    [SerializeField] private Color deletePreviewColor = Color.red;
    [SerializeField] private Color junctionPreviewColor = Color.yellow;
    [SerializeField] private Color turnEditPreviewColor = new Color(1f, 0.4f, 1f, 1f);

    [SerializeField] private RoadNodeV2 currentStartNode;
    [SerializeField] private RoadNodeV2 selectedTurnNode;
    [SerializeField] private RoadSegmentV2 selectedIncomingSegment;

    public RoadNetworkV2 Network => network;
    public bool ToolEnabled => toolEnabled;
    public ToolMode CurrentToolMode => toolMode;

    public bool HasCurrentStartNode => currentStartNode != null;
    public Vector3 CurrentStartPosition => currentStartNode != null ? currentStartNode.transform.position : Vector3.zero;

    public float SnapDistance => snapDistance;
    public float DeletePickDistance => deletePickDistance;
    public float JunctionPickDistance => junctionPickDistance;
    public float ApproachPickDistance => approachPickDistance;

    public Color PreviewColor => previewColor;
    public Color DeletePreviewColor => deletePreviewColor;
    public Color JunctionPreviewColor => junctionPreviewColor;
    public Color TurnEditPreviewColor => turnEditPreviewColor;

    public RoadNodeV2 SelectedTurnNode => selectedTurnNode;
    public RoadSegmentV2 SelectedIncomingSegment => selectedIncomingSegment;

    public void SetToolMode(ToolMode mode)
    {
        toolMode = mode;
        currentStartNode = null;

        if (toolMode != ToolMode.JunctionTurns)
        {
            selectedTurnNode = null;
            selectedIncomingSegment = null;
        }
    }

    public void HandleSceneClick(Vector3 worldPosition)
    {
        if (!toolEnabled || network == null)
            return;

        worldPosition.z = 0f;

        switch (toolMode)
        {
            case ToolMode.DrawRoad:
                HandleDrawClick(worldPosition);
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