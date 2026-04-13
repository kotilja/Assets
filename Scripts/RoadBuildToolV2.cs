using UnityEngine;

public class RoadBuildToolV2 : MonoBehaviour
{
    [SerializeField] private RoadNetworkV2 network;
    [SerializeField] private bool toolEnabled = true;
    [SerializeField] private bool continueChain = true;

    [Header("Road parameters")]
    [SerializeField] private int forwardLanes = 1;
    [SerializeField] private int backwardLanes = 1;
    [SerializeField] private float laneWidth = 0.6f;
    [SerializeField] private float speedLimit = 3f;
    [SerializeField] private float snapDistance = 0.4f;

    [Header("Delete tool")]
    [SerializeField] private float deletePickDistance = 0.25f;

    [Header("Scene preview")]
    [SerializeField] private Color previewColor = Color.cyan;
    [SerializeField] private Color deletePreviewColor = Color.red;

    [SerializeField] private RoadNodeV2 currentStartNode;

    public RoadNetworkV2 Network => network;
    public bool ToolEnabled => toolEnabled;
    public bool HasCurrentStartNode => currentStartNode != null;
    public Vector3 CurrentStartPosition => currentStartNode != null ? currentStartNode.transform.position : Vector3.zero;
    public float SnapDistance => snapDistance;
    public float DeletePickDistance => deletePickDistance;
    public Color PreviewColor => previewColor;
    public Color DeletePreviewColor => deletePreviewColor;

    public void HandleSceneClick(Vector3 worldPosition)
    {
        if (!toolEnabled || network == null)
            return;

        worldPosition.z = 0f;

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