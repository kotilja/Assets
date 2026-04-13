using System.Collections.Generic;
using UnityEngine;

public class TripDemandGenerator : MonoBehaviour
{
    public enum DemandMode
    {
        ResidentialToWork,
        WorkToResidential
    }

    [SerializeField] private TrafficManager trafficManager;

    [Header("Demand points")]
    [SerializeField] private List<TrafficPoint> residentialPoints = new List<TrafficPoint>();
    [SerializeField] private List<TrafficPoint> workPoints = new List<TrafficPoint>();

    [Header("Trip generation")]
    [SerializeField] private bool autoGenerate = true;
    [SerializeField] private DemandMode demandMode = DemandMode.ResidentialToWork;
    [SerializeField] private float tripInterval = 1.5f;
    [SerializeField] private float startDelay = 0f;
    [SerializeField] private int attemptsPerTick = 6;
    [SerializeField] private bool spawnImmediatelyAfterModeSwitch = false;

    [Header("Time of day cycle")]
    [SerializeField] private bool autoSwitchTimeOfDay = true;
    [SerializeField] private float morningDuration = 20f;
    [SerializeField] private float eveningDuration = 20f;

    [Header("Debug keys")]
    [SerializeField] private KeyCode generateOneTripKey = KeyCode.T;
    [SerializeField] private KeyCode toggleModeKey = KeyCode.Y;
    [SerializeField] private KeyCode toggleAutoKey = KeyCode.U;

    private float tripTimer;
    private bool started;

    private float modeTimer;

    public bool IsAutoGenerateEnabled => autoGenerate;
    public float CurrentTripInterval => tripInterval;
    public DemandMode CurrentMode => demandMode;
    public bool IsAutoSwitchTimeOfDayEnabled => autoSwitchTimeOfDay;

    public string GetTimeOfDayLabel()
    {
        return demandMode == DemandMode.ResidentialToWork
            ? "Утро: дом -> работа"
            : "Вечер: работа -> дом";
    }

    public float GetSecondsUntilNextTimeOfDay()
    {
        float duration = GetCurrentModeDuration();
        return Mathf.Max(0f, duration - modeTimer);
    }

    private void Start()
    {
        tripTimer = 0f;
        modeTimer = 0f;
        started = startDelay <= 0f;
    }

    private void Update()
    {
        HandleDebugInput();
        UpdateTimeOfDayCycle();
        UpdateTripGeneration();
    }

    private void HandleDebugInput()
    {
        if (generateOneTripKey != KeyCode.None && Input.GetKeyDown(generateOneTripKey))
            TryGenerateTripWithRetries();

        if (toggleModeKey != KeyCode.None && Input.GetKeyDown(toggleModeKey))
            ToggleMode();

        if (toggleAutoKey != KeyCode.None && Input.GetKeyDown(toggleAutoKey))
            autoGenerate = !autoGenerate;
    }

    private void UpdateTimeOfDayCycle()
    {
        if (!autoSwitchTimeOfDay)
            return;

        float currentDuration = GetCurrentModeDuration();

        if (currentDuration <= 0f)
            return;

        modeTimer += Time.deltaTime;

        if (modeTimer >= currentDuration)
        {
            ToggleMode();
        }
    }

    private void UpdateTripGeneration()
    {
        if (!autoGenerate)
            return;

        if (trafficManager == null)
            return;

        tripTimer += Time.deltaTime;

        if (!started)
        {
            if (tripTimer >= startDelay)
            {
                tripTimer = 0f;
                started = true;
            }

            return;
        }

        if (tripInterval <= 0f)
            return;

        if (tripTimer >= tripInterval)
        {
            tripTimer = 0f;
            TryGenerateTripWithRetries();
        }
    }

    public void ToggleMode()
    {
        demandMode = demandMode == DemandMode.ResidentialToWork
            ? DemandMode.WorkToResidential
            : DemandMode.ResidentialToWork;

        modeTimer = 0f;
        tripTimer = 0f;

        if (spawnImmediatelyAfterModeSwitch)
            TryGenerateTripWithRetries();
    }

    private float GetCurrentModeDuration()
    {
        return demandMode == DemandMode.ResidentialToWork
            ? morningDuration
            : eveningDuration;
    }

    private void TryGenerateTripWithRetries()
    {
        if (trafficManager == null)
            return;

        int tries = Mathf.Max(1, attemptsPerTick);

        for (int i = 0; i < tries; i++)
        {
            TrafficPoint fromPoint = GetRandomValidStartPoint(
                demandMode == DemandMode.ResidentialToWork ? residentialPoints : workPoints
            );

            TrafficPoint toPoint = GetRandomValidDestinationPoint(
                demandMode == DemandMode.ResidentialToWork ? workPoints : residentialPoints
            );

            if (fromPoint == null || toPoint == null)
                return;

            if (fromPoint == toPoint)
                continue;

            bool created = trafficManager.CreateTrip(fromPoint, toPoint);

            if (created)
                return;
        }
    }

    private TrafficPoint GetRandomValidStartPoint(List<TrafficPoint> points)
    {
        if (points == null || points.Count == 0)
            return null;

        List<TrafficPoint> validPoints = new List<TrafficPoint>();

        foreach (TrafficPoint point in points)
        {
            if (point == null)
                continue;

            if (point.ExitLane == null)
                continue;

            validPoints.Add(point);
        }

        if (validPoints.Count == 0)
            return null;

        int index = Random.Range(0, validPoints.Count);
        return validPoints[index];
    }

    private TrafficPoint GetRandomValidDestinationPoint(List<TrafficPoint> points)
    {
        if (points == null || points.Count == 0)
            return null;

        List<TrafficPoint> validPoints = new List<TrafficPoint>();

        foreach (TrafficPoint point in points)
        {
            if (point == null)
                continue;

            if (point.EntryLane == null)
                continue;

            validPoints.Add(point);
        }

        if (validPoints.Count == 0)
            return null;

        int index = Random.Range(0, validPoints.Count);
        return validPoints[index];
    }
}