using UnityEngine;

[ExecuteAlways]
public class RoadLaneV2 : MonoBehaviour
{
    public enum LaneDirection
    {
        Forward,
        Backward
    }

    [SerializeField] private int laneIndex;
    [SerializeField] private LaneDirection direction;
    [SerializeField] private float width = 0.08f;

    private LineRenderer lineRenderer;
    private Material cachedMaterial;

    public int LaneIndex => laneIndex;
    public LaneDirection Direction => direction;

    public void Initialize(int newLaneIndex, LaneDirection newDirection)
    {
        laneIndex = newLaneIndex;
        direction = newDirection;
        gameObject.name = $"Lane_{direction}_{laneIndex}";
    }

    public void UpdateVisual(
        Vector3 start,
        Vector3 end,
        float newWidth,
        Color color,
        int sortingOrder)
    {
        width = Mathf.Max(0.02f, newWidth);

        EnsureLineRenderer();

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.sortingOrder = sortingOrder;
    }

    private void Awake()
    {
        EnsureLineRenderer();
    }

    private void OnValidate()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            return;

        ConfigureLineRenderer();
    }

    private void EnsureLineRenderer()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        ConfigureLineRenderer();
    }

    private void ConfigureLineRenderer()
    {
        if (cachedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            cachedMaterial = new Material(shader);
            cachedMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        lineRenderer.sharedMaterial = cachedMaterial;
        lineRenderer.useWorldSpace = true;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.numCapVertices = 2;
        lineRenderer.numCornerVertices = 2;
    }
}