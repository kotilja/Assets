using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class GameBuildToolRuntimeInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoadBuildToolV2 buildTool;
    [SerializeField] private Camera targetCamera;

    [Header("Input")]
    [SerializeField] private bool useLeftClick = true;
    [SerializeField] private KeyCode clearChainKey = KeyCode.Escape;
    [SerializeField] private bool blockClicksOverUI = true;
    [SerializeField] private float worldSquareSize = 10000f;
    [SerializeField] private float buildingRotateSpeed = 2160f;
    [SerializeField] private KeyCode rotateBuildingLeftKey = KeyCode.Comma;
    [SerializeField] private KeyCode rotateBuildingRightKey = KeyCode.Period;
    [SerializeField] private float buildingRotateStepDegrees = 15f;

    private void Awake()
    {
        if (buildTool == null)
            buildTool = FindFirstObjectByType<RoadBuildToolV2>();

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (buildTool == null || !buildTool.ToolEnabled)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        HandleClearInput();
        HandleBuildingRotateStepInput();
        HandleBuildingRotationInput();
        HandlePrimaryClick();
    }

    private void HandlePrimaryClick()
    {
        if (!useLeftClick || !Input.GetMouseButtonDown(0))
            return;

        if (blockClicksOverUI && IsPointerOverUI())
            return;

        if (!TryGetMouseWorldPoint(out Vector3 worldPoint))
            return;

        if (buildTool.CurrentToolMode == RoadBuildToolV2.ToolMode.JunctionSignals &&
            buildTool.TryHandleSignalPhaseClick(worldPoint))
            return;

        buildTool.HandleSceneClick(worldPoint);
    }

    private void HandleClearInput()
    {
        if (clearChainKey == KeyCode.None || !Input.GetKeyDown(clearChainKey))
            return;

        buildTool.ClearCurrentChain();
        buildTool.ClearTurnSelection();
        buildTool.ClearLaneConnectionSelection();
        buildTool.ClearSignalSelection();
        buildTool.ClearActiveTool();
    }

    private void HandleBuildingRotationInput()
    {
        RoadBuildToolV2.ToolMode toolMode = buildTool.CurrentToolMode;
        bool isBuildingTool =
            toolMode == RoadBuildToolV2.ToolMode.PlaceHome ||
            toolMode == RoadBuildToolV2.ToolMode.PlaceOffice;

        if (!isBuildingTool || !Input.GetMouseButton(1))
            return;

        if (blockClicksOverUI && IsPointerOverUI())
            return;

        float mouseDeltaX = Input.GetAxisRaw("Mouse X");
        if (Mathf.Abs(mouseDeltaX) < 0.0001f)
            return;

        buildTool.AdjustCurrentBuildingRotation(-mouseDeltaX * buildingRotateSpeed * Time.unscaledDeltaTime);
    }

    private void HandleBuildingRotateStepInput()
    {
        RoadBuildToolV2.ToolMode toolMode = buildTool.CurrentToolMode;
        bool isBuildingTool =
            toolMode == RoadBuildToolV2.ToolMode.PlaceHome ||
            toolMode == RoadBuildToolV2.ToolMode.PlaceOffice;

        if (!isBuildingTool)
            return;

        if (Input.GetKeyDown(rotateBuildingLeftKey))
            buildTool.AdjustCurrentBuildingRotation(buildingRotateStepDegrees);

        if (Input.GetKeyDown(rotateBuildingRightKey))
            buildTool.AdjustCurrentBuildingRotation(-buildingRotateStepDegrees);
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        if (targetCamera == null)
            return false;

        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);

        if (!plane.Raycast(ray, out float enter))
            return false;

        worldPoint = ray.GetPoint(enter);
        worldPoint.z = 0f;
        worldPoint = ClampToWorldBounds(worldPoint);
        return true;
    }

    private Vector3 ClampToWorldBounds(Vector3 worldPoint)
    {
        float halfWorldSize = Mathf.Max(1f, worldSquareSize * 0.5f);
        worldPoint.x = Mathf.Clamp(worldPoint.x, -halfWorldSize, halfWorldSize);
        worldPoint.y = Mathf.Clamp(worldPoint.y, -halfWorldSize, halfWorldSize);
        worldPoint.z = 0f;
        return worldPoint;
    }
}
