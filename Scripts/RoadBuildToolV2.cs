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
        JunctionControl
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

    [Header("Scene preview")]
    [SerializeField] private Color previewColor = Color.cyan;
    [SerializeField] private Color deletePreviewColor = Color.red;
    [SerializeField] private Color junctionPreviewColor = Color.yellow;

    [SerializeField] private RoadNodeV2 currentStartNode;

    public RoadNetworkV2 Network => network;
    public bool ToolEnabled => toolEnabled;
    public ToolMode CurrentToolMode => toolMode;

    public bool HasCurrentStartNode => currentStartNode != null;
    public Vector3 CurrentStartPosition => currentStartNode != null ? currentStartNode.transform.position : Vector3.zero;

    public float SnapDistance => snapDistance;
    public float DeletePickDistance => deletePickDistance;
    public float JunctionPickDistance => junctionPickDistance;

    public Color PreviewColor => previewColor;
    public Color DeletePreviewColor => deletePreviewColor;
    public Color JunctionPreviewColor => junctionPreviewColor;

    public void SetToolMode(ToolMode mode)
    {
        toolMode = mode;
        currentStartNode = null;
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
        network.RefreshAll();

#if UNITY_EDITOR
        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(network);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    public void ClearCurrentChain()
    {
        currentStartNode = null;
    }

    public void RefreshNetworkVisuals()
    {
        if (network != null)
            network.RefreshAll();
    }
}