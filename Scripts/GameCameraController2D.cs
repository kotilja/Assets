using UnityEngine;

[DisallowMultipleComponent]
public class GameCameraController2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float fastMoveMultiplier = 2f;

    [Header("Pan")]
    [SerializeField] private float middleMousePanSensitivity = 1f;

    [Header("Zoom")]
    [SerializeField] private float zoomStepPerScroll = 2f;
    [SerializeField] private float zoomSmoothing = 12f;
    [SerializeField] private float minOrthographicSize = 3f;
    [SerializeField] private float maxOrthographicSize = 60f;
    [SerializeField] private bool zoomTowardMouse = true;

    private Camera targetCamera;
    private bool isPanning;
    private float targetOrthographicSize;
    private Vector3 lastMouseScreenPosition;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
            targetOrthographicSize = targetCamera.orthographicSize;
    }

    private void Update()
    {
        if (targetCamera == null || !targetCamera.orthographic)
            return;

        HandleKeyboardMove();
        HandleMousePan();
        HandleMouseZoom();
        UpdateZoomSmoothing();
    }

    private void HandleKeyboardMove()
    {
        Vector3 move = Vector3.zero;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            move.y += 1f;

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            move.y -= 1f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            move.x -= 1f;

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            move.x += 1f;

        if (move.sqrMagnitude < 0.0001f)
            return;

        move.Normalize();

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= Mathf.Max(1f, fastMoveMultiplier);

        float zoomFactor = Mathf.Max(0.25f, targetCamera.orthographicSize / 10f);
        Vector3 delta = move * speed * zoomFactor * Time.unscaledDeltaTime;
        delta.z = 0f;

        targetCamera.transform.position += delta;
    }

    private void HandleMousePan()
    {
        if (Input.GetMouseButtonDown(2))
        {
            isPanning = true;
            lastMouseScreenPosition = Input.mousePosition;
            return;
        }

        if (Input.GetMouseButtonUp(2))
        {
            isPanning = false;
            return;
        }

        if (!isPanning || !Input.GetMouseButton(2))
            return;

        Vector3 currentMouseScreenPosition = Input.mousePosition;
        Vector3 mouseDelta = currentMouseScreenPosition - lastMouseScreenPosition;
        lastMouseScreenPosition = currentMouseScreenPosition;

        if (mouseDelta.sqrMagnitude < 0.0001f)
            return;

        float unitsPerPixel = (targetCamera.orthographicSize * 2f) / Mathf.Max(1f, Screen.height);
        Vector3 delta = new Vector3(
            -mouseDelta.x,
            -mouseDelta.y,
            0f
        ) * unitsPerPixel * Mathf.Max(0.01f, middleMousePanSensitivity);
        delta.z = 0f;

        targetCamera.transform.position += delta;
    }

    private void HandleMouseZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f)
            return;

        Vector3 beforeZoomMouseWorld = Vector3.zero;
        bool hasMouseWorld = zoomTowardMouse && TryGetMouseWorldPoint(out beforeZoomMouseWorld);

        float zoomStep = scroll * zoomStepPerScroll;
        targetOrthographicSize = Mathf.Clamp(
            targetOrthographicSize - zoomStep,
            minOrthographicSize,
            maxOrthographicSize
        );

        if (!hasMouseWorld)
            return;

        float currentSize = Mathf.Max(0.0001f, targetCamera.orthographicSize);
        float sizeRatio = targetOrthographicSize / currentSize;
        Vector3 cameraToMouse = beforeZoomMouseWorld - targetCamera.transform.position;
        Vector3 targetCameraPosition = beforeZoomMouseWorld - cameraToMouse * sizeRatio;
        targetCameraPosition.z = targetCamera.transform.position.z;
        targetCamera.transform.position = targetCameraPosition;
    }

    private void UpdateZoomSmoothing()
    {
        if (targetCamera == null)
            return;

        float smooth = Mathf.Max(0.01f, zoomSmoothing);
        targetCamera.orthographicSize = Mathf.Lerp(
            targetCamera.orthographicSize,
            targetOrthographicSize,
            1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime)
        );
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        if (targetCamera == null)
            return false;

        Vector3 mousePosition = Input.mousePosition;
        float distance = Mathf.Abs(targetCamera.transform.position.z);

        worldPoint = targetCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, distance));
        worldPoint.z = 0f;
        return true;
    }
}
