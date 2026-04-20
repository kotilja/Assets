using UnityEngine;

[DisallowMultipleComponent]
public class GameTimeSystem : MonoBehaviour
{
    private static readonly string[] MonthNames =
    {
        "Января",
        "Февраля",
        "Марта",
        "Апреля",
        "Мая",
        "Июня",
        "Июля",
        "Августа",
        "Сентября",
        "Октября",
        "Ноября",
        "Декабря"
    };

    [Header("Calendar")]
    [SerializeField] private int startYear = 2000;
    [SerializeField] private int startMonth = 3;
    [SerializeField] private int startDay = 1;
    [SerializeField] private int startHour = 12;
    [SerializeField] private int startMinute = 0;
    [SerializeField] private int daysPerMonth = 7;
    [SerializeField] private int monthsPerYear = 12;

    [Header("Time flow")]
    [SerializeField] private float gameMinutesPerRealSecond = 1f;
    [SerializeField] private float normalSpeed = 1f;
    [SerializeField] private float fastSpeed = 3f;
    [SerializeField] private float simulationSpeed = 1f;

    private double totalGameMinutes;

    public float SimulationSpeed => simulationSpeed;
    public int Year => startYear + GetElapsedMonths() / Mathf.Max(1, monthsPerYear);
    public int Month => 1 + (GetElapsedMonths() % Mathf.Max(1, monthsPerYear));
    public int Day => 1 + (GetElapsedDays() % Mathf.Max(1, daysPerMonth));
    public int Hour => Mathf.FloorToInt((float)(GetCurrentDayMinutes() / 60d)) % 24;
    public int Minute => Mathf.FloorToInt((float)(GetCurrentDayMinutes() % 60d));

    private void Awake()
    {
        ResetToStartDate();
        ApplySimulationSpeed();
    }

    private void Update()
    {
        AdvanceTime(Time.unscaledDeltaTime);
        ApplySimulationSpeed();
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
    }

    public void SetPaused()
    {
        simulationSpeed = 0f;
        ApplySimulationSpeed();
    }

    public void SetNormalSpeed()
    {
        simulationSpeed = Mathf.Max(0f, normalSpeed);
        ApplySimulationSpeed();
    }

    public void SetFastSpeed()
    {
        simulationSpeed = Mathf.Max(0f, fastSpeed);
        ApplySimulationSpeed();
    }

    public string GetFormattedDateTime()
    {
        int monthIndex = Mathf.Clamp(Month - 1, 0, MonthNames.Length - 1);
        return $"{Day} {MonthNames[monthIndex]} {Year}  {Hour:00}:{Minute:00}";
    }

    private void ResetToStartDate()
    {
        int normalizedMonth = Mathf.Clamp(startMonth, 1, Mathf.Max(1, monthsPerYear));
        int normalizedDay = Mathf.Clamp(startDay, 1, Mathf.Max(1, daysPerMonth));
        int normalizedHour = Mathf.Clamp(startHour, 0, 23);
        int normalizedMinute = Mathf.Clamp(startMinute, 0, 59);

        int monthsOffset = normalizedMonth - 1;
        int daysOffset = normalizedDay - 1;

        totalGameMinutes =
            monthsOffset * daysPerMonth * 24d * 60d +
            daysOffset * 24d * 60d +
            normalizedHour * 60d +
            normalizedMinute;
    }

    private void AdvanceTime(float realSeconds)
    {
        if (simulationSpeed <= 0f || realSeconds <= 0f)
            return;

        totalGameMinutes += realSeconds * Mathf.Max(0f, gameMinutesPerRealSecond) * simulationSpeed;
    }

    private void ApplySimulationSpeed()
    {
        Time.timeScale = simulationSpeed;
    }

    private int GetElapsedMonths()
    {
        int totalDays = GetElapsedDays();
        return totalDays / Mathf.Max(1, daysPerMonth);
    }

    private int GetElapsedDays()
    {
        return Mathf.FloorToInt((float)(totalGameMinutes / (24d * 60d)));
    }

    private double GetCurrentDayMinutes()
    {
        double dayLengthMinutes = 24d * 60d;
        double dayMinutes = totalGameMinutes % dayLengthMinutes;
        if (dayMinutes < 0d)
            dayMinutes += dayLengthMinutes;

        return dayMinutes;
    }
}
