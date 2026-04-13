using System.Collections.Generic;
using UnityEngine;

public class RoadNodeSignalV2 : MonoBehaviour
{
    private enum CycleState
    {
        Phase1Green,
        Phase1Yellow,
        Phase2Green,
        Phase2Yellow
    }

    [Header("Incoming segments by phase")]
    [SerializeField] private List<RoadSegmentV2> phase1IncomingSegments = new List<RoadSegmentV2>();
    [SerializeField] private List<RoadSegmentV2> phase2IncomingSegments = new List<RoadSegmentV2>();

    [Header("Timings")]
    [SerializeField] private float phase1GreenDuration = 6f;
    [SerializeField] private float phase1YellowDuration = 1.5f;
    [SerializeField] private float phase2GreenDuration = 6f;
    [SerializeField] private float phase2YellowDuration = 1.5f;

    [Header("Allowed movements on green")]
    [SerializeField] private bool allowStraightOnGreen = true;
    [SerializeField] private bool allowLeftOnGreen = true;
    [SerializeField] private bool allowRightOnGreen = true;

    private CycleState currentState = CycleState.Phase1Green;
    private float stateTimer = 0f;

    private void Update()
    {
        stateTimer += Time.deltaTime;

        switch (currentState)
        {
            case CycleState.Phase1Green:
                if (stateTimer >= phase1GreenDuration)
                    SwitchState(CycleState.Phase1Yellow);
                break;

            case CycleState.Phase1Yellow:
                if (stateTimer >= phase1YellowDuration)
                    SwitchState(CycleState.Phase2Green);
                break;

            case CycleState.Phase2Green:
                if (stateTimer >= phase2GreenDuration)
                    SwitchState(CycleState.Phase2Yellow);
                break;

            case CycleState.Phase2Yellow:
                if (stateTimer >= phase2YellowDuration)
                    SwitchState(CycleState.Phase1Green);
                break;
        }
    }

    private void SwitchState(CycleState newState)
    {
        currentState = newState;
        stateTimer = 0f;
    }

    public bool CanUseConnection(RoadLaneConnectionV2 connection)
    {
        if (connection == null || !connection.IsValid)
            return false;

        return CanUseMovement(connection.fromLane.ownerSegment, connection.movementType);
    }

    public bool CanUseMovement(RoadSegmentV2 incomingSegment, RoadLaneConnectionV2.MovementType movementType)
    {
        if (incomingSegment == null)
            return false;

        if (!MovementTypeAllowed(movementType))
            return false;

        bool inPhase1 = phase1IncomingSegments.Contains(incomingSegment);
        bool inPhase2 = phase2IncomingSegments.Contains(incomingSegment);

        switch (currentState)
        {
            case CycleState.Phase1Green:
                return inPhase1;

            case CycleState.Phase1Yellow:
                return false;

            case CycleState.Phase2Green:
                return inPhase2;

            case CycleState.Phase2Yellow:
                return false;
        }

        return false;
    }

    private bool MovementTypeAllowed(RoadLaneConnectionV2.MovementType movementType)
    {
        switch (movementType)
        {
            case RoadLaneConnectionV2.MovementType.Straight:
                return allowStraightOnGreen;

            case RoadLaneConnectionV2.MovementType.Left:
                return allowLeftOnGreen;

            case RoadLaneConnectionV2.MovementType.Right:
                return allowRightOnGreen;
        }

        return true;
    }

    public string GetCurrentPhaseLabel()
    {
        switch (currentState)
        {
            case CycleState.Phase1Green:
                return "Фаза 1: зелёный";

            case CycleState.Phase1Yellow:
                return "Фаза 1: жёлтый";

            case CycleState.Phase2Green:
                return "Фаза 2: зелёный";

            case CycleState.Phase2Yellow:
                return "Фаза 2: жёлтый";
        }

        return "-";
    }

    public float GetSecondsUntilNextPhase()
    {
        return Mathf.Max(0f, GetCurrentStateDuration() - stateTimer);
    }

    private float GetCurrentStateDuration()
    {
        switch (currentState)
        {
            case CycleState.Phase1Green:
                return phase1GreenDuration;

            case CycleState.Phase1Yellow:
                return phase1YellowDuration;

            case CycleState.Phase2Green:
                return phase2GreenDuration;

            case CycleState.Phase2Yellow:
                return phase2YellowDuration;
        }

        return 0f;
    }
}