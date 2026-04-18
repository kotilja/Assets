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
        return true;
    }
}
