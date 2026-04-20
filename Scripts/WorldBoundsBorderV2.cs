using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class WorldBoundsBorderV2 : MonoBehaviour
{
    [SerializeField] private float minX = -5000f;
    [SerializeField] private float maxX = 5000f;
    [SerializeField] private float minY = -5000f;
    [SerializeField] private float maxY = 5000f;

    [SerializeField] private float lineWidth = 20f;
    [SerializeField] private Color borderColor = Color.red;
    [SerializeField] private int sortingOrder = 1000;

    private LineRenderer lineRenderer;
    private Material cachedMaterial;

    private void Awake()
    {
        RefreshVisual();
    }

    private void Start()
    {
        RefreshVisual();
    }

    private void OnValidate()
    {
        RefreshVisual();
    }

    [ContextMenu("Refresh Border")]
    public void RefreshVisual()
    {
        EnsureRenderer();

        Vector3 p0 = new Vector3(minX, minY, 0f);
        Vector3 p1 = new Vector3(maxX, minY, 0f);
        Vector3 p2 = new Vector3(maxX, maxY, 0f);
        Vector3 p3 = new Vector3(minX, maxY, 0f);

        lineRenderer.loop = true;
        lineRenderer.positionCount = 4;
        lineRenderer.SetPosition(0, p0);
        lineRenderer.SetPosition(1, p1);
        lineRenderer.SetPosition(2, p2);
        lineRenderer.SetPosition(3, p3);

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = borderColor;
        lineRenderer.endColor = borderColor;
        lineRenderer.sortingOrder = sortingOrder;
    }

    private void EnsureRenderer()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

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
        lineRenderer.numCapVertices = 0;
        lineRenderer.numCornerVertices = 0;
    }
}