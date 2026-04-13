using UnityEngine;

public class RoadSignalDebugUIV2 : MonoBehaviour
{
    [SerializeField] private RoadNodeSignalV2 signal;
    [SerializeField] private Vector2 screenPosition = new Vector2(10f, 10f);

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(screenPosition.x, screenPosition.y, 280f, 90f), GUI.skin.box);

        GUILayout.Label("V2 Signal Debug");

        if (signal == null)
        {
            GUILayout.Label("Сигнал не назначен");
        }
        else
        {
            GUILayout.Label("Фаза: " + signal.GetCurrentPhaseLabel());
            GUILayout.Label("До следующей фазы: " + signal.GetSecondsUntilNextPhase().ToString("F1") + " s");
        }

        GUILayout.EndArea();
    }
}