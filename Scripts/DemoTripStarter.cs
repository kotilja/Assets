using UnityEngine;

public class DemoTripStarter : MonoBehaviour
{
    [SerializeField] private TrafficManager trafficManager;
    [SerializeField] private TrafficPoint upperStartPoint;
    [SerializeField] private TrafficPoint lowerStartPoint;
    [SerializeField] private TrafficPoint destinationPoint;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            trafficManager.CreateTrip(upperStartPoint, destinationPoint);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            trafficManager.CreateTrip(lowerStartPoint, destinationPoint);
        }
    }
}