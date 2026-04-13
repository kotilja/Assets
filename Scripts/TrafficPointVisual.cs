using UnityEngine;

[ExecuteAlways]
public class TrafficPointVisual : MonoBehaviour
{
    [SerializeField] private TrafficPoint trafficPoint;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private int sortingOrder = 20;
    [SerializeField] private Vector3 visualScale = new Vector3(0.6f, 0.6f, 1f);

    private void Reset()
    {
        if (trafficPoint == null)
            trafficPoint = GetComponent<TrafficPoint>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        ApplyVisual();
    }

    private void Awake()
    {
        ApplyVisual();
    }

    private void OnValidate()
    {
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (trafficPoint == null)
            trafficPoint = GetComponent<TrafficPoint>();

        if (spriteRenderer == null)
            return;

        spriteRenderer.color = GetColorForType();
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.transform.localScale = visualScale;
    }

    private Color GetColorForType()
    {
        if (trafficPoint == null)
            return Color.white;

        switch (trafficPoint.Type)
        {
            case TrafficPoint.PointType.Residential:
                return new Color(0.2f, 1f, 0.2f);

            case TrafficPoint.PointType.Work:
                return new Color(0.2f, 0.5f, 1f);

            case TrafficPoint.PointType.Commercial:
                return new Color(1f, 0.8f, 0.2f);
        }

        return Color.white;
    }
}