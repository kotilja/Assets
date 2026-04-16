using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class PedestrianPathV2 : MonoBehaviour
{
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private bool isCurved = false;
    [SerializeField] private Vector3 controlPoint = Vector3.zero;
    [SerializeField] private int sampleCount = 12;

    [Header("Visual")]
    [SerializeField] private float width = 0.18f;
    [SerializeField] private Color color = new Color(0.85f, 0.6f, 0.25f, 1f);
    [SerializeField] private int sortingOrder = 40;

    private LineRenderer lineRenderer;
    private Material cachedMaterial;

    public Transform StartPoint => startPoint;
    public Transform EndPoint => endPoint;

    public List<Vector3> GetPolylineWorld()
    {
        List<Vector3> points = new List<Vector3>();

        if (startPoint == null || endPoint == null)
            return points;

        Vector3 a = startPoint.position;
        Vector3 b = endPoint.position;
        a.z = 0f;
        b.z = 0f;

        if (!isCurved)
        {
            points.Add(a);
            points.Add(b);
            return points;
        }

        int samples = Mathf.Clamp(sampleCount, 4, 32);
        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            points.Add(EvaluateQuadraticBezier(a, controlPoint, b, t));
        }

        return points;
    }

    public void RefreshVisual()
    {
        EnsureRenderer();

        List<Vector3> points = GetPolylineWorld();
        if (points.Count < 2)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.positionCount = points.Count;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.sortingOrder = sortingOrder;

        for (int i = 0; i < points.Count; i++)
            lineRenderer.SetPosition(i, points[i]);
    }

    private Vector3 EvaluateQuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

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

    private void EnsureRenderer()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        if (cachedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            cachedMaterial = new Material(shader);
            cachedMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        lineRenderer.sharedMaterial = cachedMaterial;
        lineRenderer.useWorldSpace = true;
        lineRenderer.numCapVertices = 0;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.alignment = LineAlignment.TransformZ;
    }
}