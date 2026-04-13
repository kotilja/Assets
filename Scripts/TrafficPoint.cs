using UnityEngine;

public class TrafficPoint : MonoBehaviour
{
    public enum PointType
    {
        Residential,
        Work,
        Commercial
    }

    [SerializeField] private string displayName;
    [SerializeField] private LanePath exitLane;
    [SerializeField] private LanePath entryLane;

    [Header("Demand settings")]
    [SerializeField] private PointType pointType = PointType.Residential;
    [SerializeField] private int capacity = 10;

    public string DisplayName => displayName;
    public LanePath ExitLane => exitLane;
    public LanePath EntryLane => entryLane;
    public PointType Type => pointType;
    public int Capacity => capacity;
}