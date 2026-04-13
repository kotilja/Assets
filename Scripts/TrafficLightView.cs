using UnityEngine;

public class TrafficLightView : MonoBehaviour
{
    [SerializeField] private TrafficLightController trafficLightController;
    [SerializeField] private LanePath watchedLane;

    [SerializeField] private SpriteRenderer redLamp;
    [SerializeField] private SpriteRenderer yellowLamp;
    [SerializeField] private SpriteRenderer greenLamp;

    private void Update()
    {
        if (trafficLightController == null || watchedLane == null)
            return;

        TrafficLightController.LightSignal signal =
            trafficLightController.GetLightSignalForLane(watchedLane);

        if (redLamp != null)
            redLamp.enabled = signal == TrafficLightController.LightSignal.Red;

        if (yellowLamp != null)
            yellowLamp.enabled = signal == TrafficLightController.LightSignal.Yellow;

        if (greenLamp != null)
            greenLamp.enabled = signal == TrafficLightController.LightSignal.Green;
    }
}