using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class BuildingZoneV2 : MonoBehaviour
{
    public enum BuildingType
    {
        Home,
        Office
    }

    [SerializeField] private BuildingType buildingType = BuildingType.Home;
    [SerializeField] private Vector2 size = new Vector2(2f, 2f);
    [SerializeField] private int capacity = 2;
    [SerializeField] private int occupiedSlots = 0;
    [SerializeField] private float gizmoHeight = 0.01f;
    [SerializeField] private Color homeColor = new Color(0.35f, 0.8f, 0.45f, 0.9f);
    [SerializeField] private Color officeColor = new Color(0.35f, 0.55f, 0.95f, 0.9f);
    [SerializeField] private Color outlineColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);

    private bool delayedGraphRebuildQueued = false;

    public BuildingType Type => buildingType;
    public int Capacity => Mathf.Max(1, capacity);
    public int OccupiedSlots => occupiedSlots;
    public bool HasFreeSlot => occupiedSlots < Capacity;
    public Vector2 Size => new Vector2(Mathf.Max(0.5f, size.x), Mathf.Max(0.5f, size.y));
    public Vector3 Position => transform.position;
    public Vector3 EntrancePoint => GetClosestPointOnPerimeter(transform.position + Vector3.down);

    public void Initialize(BuildingType type, Vector2 rectSize, int slotCapacity = 2)
    {
        buildingType = type;
        size = new Vector2(Mathf.Max(0.5f, rectSize.x), Mathf.Max(0.5f, rectSize.y));
        capacity = Mathf.Max(1, slotCapacity);
        occupiedSlots = 0;
        gameObject.name = $"{buildingType}_{GetInstanceID()}";
    }

    public bool TryReserveSlot()
    {
        if (!HasFreeSlot)
            return false;

        occupiedSlots++;
        return true;
    }

    public void ReleaseSlot()
    {
        occupiedSlots = Mathf.Max(0, occupiedSlots - 1);
    }

    public void SetSize(Vector2 rectSize)
    {
        size = new Vector2(Mathf.Max(0.5f, rectSize.x), Mathf.Max(0.5f, rectSize.y));
    }

    public Vector3 GetClosestPointOnPerimeter(Vector3 referencePoint)
    {
        Vector2 half = Size * 0.5f;
        Vector3 center = Position;
        referencePoint.z = 0f;

        float left = center.x - half.x;
        float right = center.x + half.x;
        float bottom = center.y - half.y;
        float top = center.y + half.y;

        Vector3 leftPoint = new Vector3(left, Mathf.Clamp(referencePoint.y, bottom, top), 0f);
        Vector3 rightPoint = new Vector3(right, Mathf.Clamp(referencePoint.y, bottom, top), 0f);
        Vector3 bottomPoint = new Vector3(Mathf.Clamp(referencePoint.x, left, right), bottom, 0f);
        Vector3 topPoint = new Vector3(Mathf.Clamp(referencePoint.x, left, right), top, 0f);

        Vector3 bestPoint = leftPoint;
        float bestDistance = Vector3.SqrMagnitude(referencePoint - leftPoint);

        float candidateDistance = Vector3.SqrMagnitude(referencePoint - rightPoint);
        if (candidateDistance < bestDistance)
        {
            bestDistance = candidateDistance;
            bestPoint = rightPoint;
        }

        candidateDistance = Vector3.SqrMagnitude(referencePoint - bottomPoint);
        if (candidateDistance < bestDistance)
        {
            bestDistance = candidateDistance;
            bestPoint = bottomPoint;
        }

        candidateDistance = Vector3.SqrMagnitude(referencePoint - topPoint);
        if (candidateDistance < bestDistance)
            bestPoint = topPoint;

        return bestPoint;
    }

    private void OnValidate()
    {
        size = new Vector2(Mathf.Max(0.5f, size.x), Mathf.Max(0.5f, size.y));
        capacity = Mathf.Max(1, capacity);
        occupiedSlots = Mathf.Clamp(occupiedSlots, 0, capacity);

#if UNITY_EDITOR
        if (Application.isPlaying || delayedGraphRebuildQueued)
            return;

        delayedGraphRebuildQueued = true;
        EditorApplication.delayCall += DelayedRebuildPedestrianGraph;
#endif
    }

#if UNITY_EDITOR
    private void DelayedRebuildPedestrianGraph()
    {
        delayedGraphRebuildQueued = false;

        if (this == null || Application.isPlaying)
            return;

        PedestrianNetworkV2 pedestrianNetwork = FindFirstObjectByType<PedestrianNetworkV2>();
        if (pedestrianNetwork != null)
            pedestrianNetwork.RebuildGraph();
    }
#endif

    private void OnDrawGizmos()
    {
        Vector2 half = Size * 0.5f;
        Vector3 p0 = transform.position + new Vector3(-half.x, -half.y, gizmoHeight);
        Vector3 p1 = transform.position + new Vector3(half.x, -half.y, gizmoHeight);
        Vector3 p2 = transform.position + new Vector3(half.x, half.y, gizmoHeight);
        Vector3 p3 = transform.position + new Vector3(-half.x, half.y, gizmoHeight);

        Color fillColor = buildingType == BuildingType.Home ? homeColor : officeColor;
        Gizmos.color = fillColor;
        Gizmos.DrawCube(transform.position + new Vector3(0f, 0f, gizmoHeight), new Vector3(Size.x, Size.y, 0.02f));

        Gizmos.color = outlineColor;
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p0);

        Gizmos.color = Color.white;
        Gizmos.DrawSphere(EntrancePoint + new Vector3(0f, 0f, gizmoHeight), 0.06f);
    }
}
