using System.Collections.Generic;
using UnityEngine;

public class TrafficStatsManager : MonoBehaviour
{
    [System.Serializable]
    public class LaneDebugInfo
    {
        public string label;
        public LanePath lane;
    }

    public enum FlowDirection
    {
        Unknown,
        Upper,
        Lower
    }

    public static TrafficStatsManager Instance { get; private set; }

    [Header("Demand generator")]
    [SerializeField] private TripDemandGenerator demandGenerator;

    [Header("Traffic light")]
    [SerializeField] private TrafficLightController trafficLightController;

    [Header("Start lanes for direction detection")]
    [SerializeField] private LanePath upperStartLane;
    [SerializeField] private LanePath lowerStartLane;

    [Header("Queue lanes to monitor")]
    [SerializeField] private List<LanePath> upperQueueLanes = new List<LanePath>();
    [SerializeField] private List<LanePath> lowerQueueLanes = new List<LanePath>();

    [Header("Road load debug")]
    [SerializeField] private List<LaneDebugInfo> monitoredLanes = new List<LaneDebugInfo>();

    private int upperSpawned;
    private int lowerSpawned;

    private int upperArrived;
    private int lowerArrived;

    private float upperTotalWaitTime;
    private float lowerTotalWaitTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public FlowDirection GetDirectionForStartLane(LanePath startLane)
    {
        if (startLane == null)
            return FlowDirection.Unknown;

        if (startLane == upperStartLane)
            return FlowDirection.Upper;

        if (startLane == lowerStartLane)
            return FlowDirection.Lower;

        return FlowDirection.Unknown;
    }

    public void ReportVehicleSpawned(FlowDirection direction)
    {
        switch (direction)
        {
            case FlowDirection.Upper:
                upperSpawned++;
                break;

            case FlowDirection.Lower:
                lowerSpawned++;
                break;
        }
    }

    public void ReportVehicleArrived(FlowDirection direction, float waitTime)
    {
        switch (direction)
        {
            case FlowDirection.Upper:
                upperArrived++;
                upperTotalWaitTime += waitTime;
                break;

            case FlowDirection.Lower:
                lowerArrived++;
                lowerTotalWaitTime += waitTime;
                break;
        }
    }

    private int GetQueueCount(List<LanePath> lanes)
    {
        int count = 0;

        foreach (LanePath lane in lanes)
        {
            if (lane == null)
                continue;

            count += lane.GetActiveVehicleCount();
        }

        return count;
    }

    private int GetLaneLoad(LanePath lane)
    {
        if (lane == null)
            return 0;

        return lane.GetActiveVehicleCount();
    }

    private float GetAverageWait(FlowDirection direction)
    {
        switch (direction)
        {
            case FlowDirection.Upper:
                if (upperArrived == 0)
                    return 0f;
                return upperTotalWaitTime / upperArrived;

            case FlowDirection.Lower:
                if (lowerArrived == 0)
                    return 0f;
                return lowerTotalWaitTime / lowerArrived;
        }

        return 0f;
    }

    private int GetActiveVehicleCount()
    {
        return (upperSpawned + lowerSpawned) - (upperArrived + lowerArrived);
    }

    private string GetTimeOfDayText()
    {
        if (demandGenerator == null)
            return "-";

        return demandGenerator.GetTimeOfDayLabel();
    }

    private string GetAutoGenText()
    {
        if (demandGenerator == null)
            return "-";

        return demandGenerator.IsAutoGenerateEnabled ? "вкл" : "выкл";
    }

    private string GetTripIntervalText()
    {
        if (demandGenerator == null)
            return "-";

        return demandGenerator.CurrentTripInterval.ToString("F1") + " s";
    }

    private string GetAutoTimeSwitchText()
    {
        if (demandGenerator == null)
            return "-";

        return demandGenerator.IsAutoSwitchTimeOfDayEnabled ? "вкл" : "выкл";
    }

    private string GetTimeUntilNextModeText()
    {
        if (demandGenerator == null)
            return "-";

        return demandGenerator.GetSecondsUntilNextTimeOfDay().ToString("F1") + " s";
    }

    private string GetTrafficLightPhaseText()
    {
        if (trafficLightController == null)
            return "-";

        return trafficLightController.GetCurrentPhaseLabel();
    }

    private string GetTrafficLightCountdownText()
    {
        if (trafficLightController == null)
            return "-";

        return trafficLightController.GetSecondsUntilNextPhase().ToString("F1") + " s";
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10f, 10f, 390f, 520f), GUI.skin.box);

        GUILayout.Label("Traffic Stats");
        GUILayout.Space(5f);

        GUILayout.Label("Время дня: " + GetTimeOfDayText());
        GUILayout.Label("Автогенерация: " + GetAutoGenText());
        GUILayout.Label("Интервал поездок: " + GetTripIntervalText());
        GUILayout.Label("Автосмена времени суток: " + GetAutoTimeSwitchText());
        GUILayout.Label("До следующего времени суток: " + GetTimeUntilNextModeText());

        GUILayout.Space(8f);
        GUILayout.Label("Фаза светофора: " + GetTrafficLightPhaseText());
        GUILayout.Label("До следующей фазы: " + GetTrafficLightCountdownText());

        GUILayout.Space(8f);
        GUILayout.Label("Загруженность полос:");

        foreach (LaneDebugInfo info in monitoredLanes)
        {
            if (info == null)
                continue;

            string label = string.IsNullOrWhiteSpace(info.label) ? "Lane" : info.label;
            GUILayout.Label(label + ": " + GetLaneLoad(info.lane));
        }

        GUILayout.Space(8f);
        GUILayout.Label("Upper queue: " + GetQueueCount(upperQueueLanes));
        GUILayout.Label("Lower queue: " + GetQueueCount(lowerQueueLanes));

        GUILayout.Space(8f);
        GUILayout.Label("Upper passed: " + upperArrived);
        GUILayout.Label("Lower passed: " + lowerArrived);

        GUILayout.Space(8f);
        GUILayout.Label("Upper avg wait: " + GetAverageWait(FlowDirection.Upper).ToString("F2") + " s");
        GUILayout.Label("Lower avg wait: " + GetAverageWait(FlowDirection.Lower).ToString("F2") + " s");

        GUILayout.Space(8f);
        GUILayout.Label("Active vehicles: " + GetActiveVehicleCount());

        GUILayout.EndArea();
    }
}