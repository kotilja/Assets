using System.Collections.Generic;
using UnityEngine;

public class TrafficLightController : MonoBehaviour
{
    public enum LightSignal
    {
        Red,
        Yellow,
        Green
    }

    private enum CycleState
    {
        Phase1Green,
        AllYellowAfterPhase1,
        Phase2Green,
        AllYellowAfterPhase2
    }

    [SerializeField] private List<LanePath> phase1Lanes = new List<LanePath>();
    [SerializeField] private float phase1GreenDuration = 3f;
    [SerializeField] private float allYellowAfterPhase1Duration = 1f;

    [SerializeField] private List<LanePath> phase2Lanes = new List<LanePath>();
    [SerializeField] private float phase2GreenDuration = 3f;
    [SerializeField] private float allYellowAfterPhase2Duration = 1f;

    private CycleState currentState = CycleState.Phase1Green;
    private float stateTimer = 0f;

    private void Update()
    {
        stateTimer += Time.deltaTime;

        switch (currentState)
        {
            case CycleState.Phase1Green:
                if (stateTimer >= phase1GreenDuration)
                    SwitchState(CycleState.AllYellowAfterPhase1);
                break;

            case CycleState.AllYellowAfterPhase1:
                if (stateTimer >= allYellowAfterPhase1Duration)
                    SwitchState(CycleState.Phase2Green);
                break;

            case CycleState.Phase2Green:
                if (stateTimer >= phase2GreenDuration)
                    SwitchState(CycleState.AllYellowAfterPhase2);
                break;

            case CycleState.AllYellowAfterPhase2:
                if (stateTimer >= allYellowAfterPhase2Duration)
                    SwitchState(CycleState.Phase1Green);
                break;
        }
    }

    private void SwitchState(CycleState newState)
    {
        currentState = newState;
        stateTimer = 0f;
    }

    public LightSignal GetLightSignalForLane(LanePath lane)
    {
        if (lane == null)
            return LightSignal.Red;

        bool isPhase1Lane = phase1Lanes.Contains(lane);
        bool isPhase2Lane = phase2Lanes.Contains(lane);

        switch (currentState)
        {
            case CycleState.Phase1Green:
                return isPhase1Lane ? LightSignal.Green : LightSignal.Red;

            case CycleState.AllYellowAfterPhase1:
                if (isPhase1Lane || isPhase2Lane)
                    return LightSignal.Yellow;
                return LightSignal.Red;

            case CycleState.Phase2Green:
                return isPhase2Lane ? LightSignal.Green : LightSignal.Red;

            case CycleState.AllYellowAfterPhase2:
                if (isPhase1Lane || isPhase2Lane)
                    return LightSignal.Yellow;
                return LightSignal.Red;
        }

        return LightSignal.Red;
    }

    public string GetCurrentPhaseLabel()
    {
        switch (currentState)
        {
            case CycleState.Phase1Green:
                return "Верх: зелёный, низ: красный";

            case CycleState.AllYellowAfterPhase1:
                return "Обе стороны: жёлтый";

            case CycleState.Phase2Green:
                return "Верх: красный, низ: зелёный";

            case CycleState.AllYellowAfterPhase2:
                return "Обе стороны: жёлтый";
        }

        return "-";
    }

    public float GetSecondsUntilNextPhase()
    {
        float duration = GetCurrentStateDuration();
        return Mathf.Max(0f, duration - stateTimer);
    }

    private float GetCurrentStateDuration()
    {
        switch (currentState)
        {
            case CycleState.Phase1Green:
                return phase1GreenDuration;

            case CycleState.AllYellowAfterPhase1:
                return allYellowAfterPhase1Duration;

            case CycleState.Phase2Green:
                return phase2GreenDuration;

            case CycleState.AllYellowAfterPhase2:
                return allYellowAfterPhase2Duration;
        }

        return 0f;
    }
}