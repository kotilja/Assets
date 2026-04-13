using UnityEngine;

public class TrafficFlowSpawner : MonoBehaviour
{
    [SerializeField] private TrafficManager trafficManager;
    [SerializeField] private TrafficPoint fromPoint;
    [SerializeField] private TrafficPoint toPoint;

    [SerializeField] private bool autoSpawn = true;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float startDelay = 0f;

    [Header("Optional debug key")]
    [SerializeField] private KeyCode toggleKey = KeyCode.None;

    private float timer;
    private bool started;

    private void Start()
    {
        timer = 0f;
        started = startDelay <= 0f;
    }

    private void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
        {
            autoSpawn = !autoSpawn;
        }

        if (!autoSpawn)
            return;

        if (trafficManager == null || fromPoint == null || toPoint == null)
            return;

        timer += Time.deltaTime;

        if (!started)
        {
            if (timer >= startDelay)
            {
                timer = 0f;
                started = true;
            }

            return;
        }

        if (spawnInterval <= 0f)
            return;

        if (timer >= spawnInterval)
        {
            timer = 0f;
            trafficManager.CreateTrip(fromPoint, toPoint);
        }
    }

    public void SpawnNow()
    {
        if (trafficManager == null || fromPoint == null || toPoint == null)
            return;

        trafficManager.CreateTrip(fromPoint, toPoint);
    }

    public void SetAutoSpawn(bool value)
    {
        autoSpawn = value;
    }
}