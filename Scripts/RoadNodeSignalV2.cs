using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class RoadNodeSignalV2 : MonoBehaviour
{
    private enum LampState
    {
        Red,
        Yellow,
        Green
    }

    private class SignalHeadVisual
    {
        public RoadSegmentV2 segment;
        public GameObject rootObject;
        public SpriteRenderer bodyRenderer;
        public SpriteRenderer lampRenderer;
    }

    [System.Serializable]
    public class SignalPhaseRule
    {
        public RoadSegmentV2 incomingSegment;
        public bool allowStraight = false;
        public bool allowLeft = false;
        public bool allowRight = false;

        public bool Allows(RoadLaneConnectionV2.MovementType movementType)
        {
            switch (movementType)
            {
                case RoadLaneConnectionV2.MovementType.Straight:
                    return allowStraight;

                case RoadLaneConnectionV2.MovementType.Left:
                    return allowLeft;

                case RoadLaneConnectionV2.MovementType.Right:
                    return allowRight;

                default:
                    return false;
            }
        }

        public bool HasAnyGreen()
        {
            return allowStraight || allowLeft || allowRight;
        }
    }

    [System.Serializable]
    public class SignalPhase
    {
        public string name = "Phase";
        public float duration = 6f;
        public List<SignalPhaseRule> rules = new List<SignalPhaseRule>();
    }

    [Header("Signal phases")]
    [SerializeField] private bool autoCycle = true;
    [SerializeField] private List<SignalPhase> phases = new List<SignalPhase>();
    [SerializeField] private int currentPhaseIndex = 0;
    [SerializeField] private float phaseTimer = 0f;

    [Header("Visuals")]
    [SerializeField] private float bodyWidth = 0.24f;
    [SerializeField] private float bodyHeight = 0.34f;
    [SerializeField] private float lampSize = 0.16f;
    [SerializeField] private float signalScale = 5f;
    [SerializeField] private float signalSideOffset = 0.02f;
    [SerializeField] private float extraRightOffset = 0.02f;
    [SerializeField] private float signalBackOffset = 0.12f;
    [SerializeField] private int bodySortingOrder = 60;
    [SerializeField] private int lampSortingOrder = 61;
    [SerializeField] private Color bodyColor = new Color(0.12f, 0.12f, 0.12f, 1f);
    [SerializeField] private Color redColor = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color yellowColor = new Color(1f, 0.9f, 0.15f, 1f);
    [SerializeField] private Color greenColor = new Color(0.2f, 1f, 0.25f, 1f);
    [SerializeField] private Color offColor = new Color(0.18f, 0.18f, 0.18f, 1f);

    private RoadNodeV2 node;
    private Transform signalsRoot;
    private readonly Dictionary<int, SignalHeadVisual> signalHeads = new Dictionary<int, SignalHeadVisual>();

#if UNITY_EDITOR
    private bool delayedSyncQueued;
#endif

    public IReadOnlyList<SignalPhase> Phases => phases;
    public int CurrentPhaseIndex => currentPhaseIndex;

    private static Sprite cachedSprite;

    private void OnEnable()
    {
        SyncFromNode();
    }

    private void Awake()
    {
        SyncFromNode();
    }

    private void Start()
    {
        SyncFromNode();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            SyncFromNode();
            return;
        }

        if (delayedSyncQueued)
            return;

        delayedSyncQueued = true;
        EditorApplication.delayCall += DelayedEditorSync;
#else
        SyncFromNode();
#endif
    }

#if UNITY_EDITOR
    private void DelayedEditorSync()
    {
        delayedSyncQueued = false;

        if (this == null)
            return;

        SyncFromNode();
    }
#endif

    private void OnDestroy()
    {
        if (signalsRoot != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(signalsRoot.gameObject);
            else
                Destroy(signalsRoot.gameObject);
#else
            Destroy(signalsRoot.gameObject);
#endif
        }
    }

    private void Update()
    {
        if (node == null)
            node = GetComponent<RoadNodeV2>();

        if (node == null)
            return;

        if (!node.UsesTrafficLight)
        {
            HideAllHeads();
            return;
        }

        EnsureDefaultPhases();
        EnsurePhaseCoverage();

        if (!Application.isPlaying)
        {
            RefreshSignalVisuals();
            return;
        }

        if (autoCycle && phases != null && phases.Count > 0)
        {
            SignalPhase phase = GetCurrentPhase();
            if (phase != null)
            {
                phaseTimer += Time.deltaTime;

                float duration = Mathf.Max(0.2f, phase.duration);
                if (phaseTimer >= duration)
                    AdvancePhase();
            }
        }

        RefreshSignalVisuals();
    }

    public void SyncFromNode()
    {
        node = GetComponent<RoadNodeV2>();

        if (node == null)
            return;

        EnsureDefaultPhases();
        EnsurePhaseCoverage();
        EnsureSignalsRoot();
        EnsureSignalHeads();
        RefreshSignalVisuals();
    }

    public void AdvancePhase()
    {
        if (phases == null || phases.Count == 0)
            return;

        currentPhaseIndex = (currentPhaseIndex + 1) % phases.Count;
        phaseTimer = 0f;
        RefreshSignalVisuals();
    }

    public void SetPhase(int phaseIndex)
    {
        if (phases == null || phases.Count == 0)
            return;

        currentPhaseIndex = Mathf.Clamp(phaseIndex, 0, phases.Count - 1);
        phaseTimer = 0f;
        RefreshSignalVisuals();
    }

    public int GetPhaseCount()
    {
        EnsureDefaultPhases();
        EnsurePhaseCoverage();
        return phases != null ? phases.Count : 0;
    }

    public void SelectNextPhase()
    {
        if (GetPhaseCount() == 0)
            return;

        currentPhaseIndex = (currentPhaseIndex + 1) % phases.Count;
        phaseTimer = 0f;
        RefreshSignalVisuals();
    }

    public void SelectPreviousPhase()
    {
        if (GetPhaseCount() == 0)
            return;

        currentPhaseIndex--;
        if (currentPhaseIndex < 0)
            currentPhaseIndex = phases.Count - 1;

        phaseTimer = 0f;
        RefreshSignalVisuals();
    }

    public void AddPhaseCopyOfCurrent()
    {
        EnsureDefaultPhases();
        EnsurePhaseCoverage();

        SignalPhase source = GetCurrentPhase();

        SignalPhase newPhase = new SignalPhase
        {
            name = source != null ? source.name + " Copy" : "Phase",
            duration = source != null ? source.duration : 6f,
            rules = new List<SignalPhaseRule>()
        };

        if (source != null && source.rules != null)
        {
            for (int i = 0; i < source.rules.Count; i++)
            {
                SignalPhaseRule rule = source.rules[i];
                if (rule == null)
                    continue;

                newPhase.rules.Add(new SignalPhaseRule
                {
                    incomingSegment = rule.incomingSegment,
                    allowStraight = rule.allowStraight,
                    allowLeft = rule.allowLeft,
                    allowRight = rule.allowRight
                });
            }
        }

        phases.Add(newPhase);
        currentPhaseIndex = phases.Count - 1;
        phaseTimer = 0f;

        EnsurePhaseCoverage();
        RefreshSignalVisuals();
    }

    public void RemoveCurrentPhase()
    {
        if (phases == null || phases.Count <= 1)
            return;

        currentPhaseIndex = Mathf.Clamp(currentPhaseIndex, 0, phases.Count - 1);
        phases.RemoveAt(currentPhaseIndex);

        currentPhaseIndex = Mathf.Clamp(currentPhaseIndex, 0, phases.Count - 1);
        phaseTimer = 0f;

        EnsurePhaseCoverage();
        RefreshSignalVisuals();
    }

    public bool GetMovementAllowedInCurrentPhase(RoadSegmentV2 incomingSegment, RoadLaneConnectionV2.MovementType movementType)
    {
        SignalPhase phase = GetCurrentPhase();
        SignalPhaseRule rule = FindRuleForSegment(phase, incomingSegment);

        if (rule == null)
            return false;

        return rule.Allows(movementType);
    }

    public void ToggleMovementInCurrentPhase(RoadSegmentV2 incomingSegment, RoadLaneConnectionV2.MovementType movementType)
    {
        SignalPhase phase = GetCurrentPhase();
        if (phase == null)
            return;

        SignalPhaseRule rule = FindRuleForSegment(phase, incomingSegment);
        if (rule == null)
            return;

        switch (movementType)
        {
            case RoadLaneConnectionV2.MovementType.Straight:
                rule.allowStraight = !rule.allowStraight;
                break;

            case RoadLaneConnectionV2.MovementType.Left:
                rule.allowLeft = !rule.allowLeft;
                break;

            case RoadLaneConnectionV2.MovementType.Right:
                rule.allowRight = !rule.allowRight;
                break;
        }

        RefreshSignalVisuals();
    }

    public string GetCurrentEditablePhaseName()
    {
        SignalPhase phase = GetCurrentPhase();
        return phase != null ? phase.name : "-";
    }

    public void ResetToDefaultPhases()
    {
        phases = BuildDefaultPhases();
        currentPhaseIndex = 0;
        phaseTimer = 0f;
        RefreshSignalVisuals();
    }

    public bool CanUseConnection(RoadLaneConnectionV2 connection)
    {
        if (connection == null || !connection.IsValid)
            return false;

        return CanUseMovement(connection.fromLane.ownerSegment, connection.movementType);
    }

    public bool CanUseMovement(RoadSegmentV2 incomingSegment, RoadLaneConnectionV2.MovementType movementType)
    {
        SignalPhase phase = GetCurrentPhase();
        if (phase == null || phase.rules == null || phase.rules.Count == 0)
            return false;

        SignalPhaseRule rule = FindRuleForSegment(phase, incomingSegment);
        if (rule == null)
            return false;

        return rule.Allows(movementType);
    }

    public string GetCurrentPhaseLabel()
    {
        SignalPhase phase = GetCurrentPhase();
        return phase != null ? phase.name : "-";
    }

    public float GetSecondsUntilNextPhase()
    {
        SignalPhase phase = GetCurrentPhase();
        if (phase == null)
            return 0f;

        return Mathf.Max(0f, Mathf.Max(0.2f, phase.duration) - phaseTimer);
    }

    private void EnsureDefaultPhases()
    {
        if (node == null)
            node = GetComponent<RoadNodeV2>();

        if (node == null || !node.IsIntersection)
            return;

        bool needsRebuild = phases == null || phases.Count == 0;

        if (!needsRebuild)
        {
            for (int i = 0; i < phases.Count; i++)
            {
                if (phases[i] == null || phases[i].rules == null)
                {
                    needsRebuild = true;
                    break;
                }
            }
        }

        if (!needsRebuild)
            return;

        phases = BuildDefaultPhases();
        currentPhaseIndex = Mathf.Clamp(currentPhaseIndex, 0, Mathf.Max(0, phases.Count - 1));
        phaseTimer = 0f;
    }

    private void EnsurePhaseCoverage()
    {
        if (phases == null)
            phases = new List<SignalPhase>();

        if (phases.Count == 0)
        {
            phases = BuildDefaultPhases();
            currentPhaseIndex = 0;
            phaseTimer = 0f;
        }

        List<RoadSegmentV2> incomingSegments = GetIncomingSegments();
        HashSet<RoadSegmentV2> validIncoming = new HashSet<RoadSegmentV2>(incomingSegments);

        for (int i = 0; i < phases.Count; i++)
        {
            SignalPhase phase = phases[i];
            if (phase == null)
                continue;

            if (phase.rules == null)
                phase.rules = new List<SignalPhaseRule>();

            for (int j = phase.rules.Count - 1; j >= 0; j--)
            {
                SignalPhaseRule rule = phase.rules[j];
                if (rule == null || rule.incomingSegment == null || !validIncoming.Contains(rule.incomingSegment))
                    phase.rules.RemoveAt(j);
            }

            for (int j = 0; j < incomingSegments.Count; j++)
            {
                RoadSegmentV2 incomingSegment = incomingSegments[j];
                if (incomingSegment == null)
                    continue;

                bool found = false;

                for (int k = 0; k < phase.rules.Count; k++)
                {
                    SignalPhaseRule rule = phase.rules[k];
                    if (rule != null && rule.incomingSegment == incomingSegment)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    phase.rules.Add(new SignalPhaseRule
                    {
                        incomingSegment = incomingSegment,
                        allowStraight = false,
                        allowLeft = false,
                        allowRight = false
                    });
                }
            }
        }

        currentPhaseIndex = Mathf.Clamp(currentPhaseIndex, 0, Mathf.Max(0, phases.Count - 1));
    }

    private List<SignalPhase> BuildDefaultPhases()
    {
        List<SignalPhase> result = new List<SignalPhase>();

        if (node == null)
            return result;

        List<RoadSegmentV2> incomingSegments = GetIncomingSegments();
        if (incomingSegments.Count == 0)
            return result;

        List<RoadSegmentV2> horizontal = new List<RoadSegmentV2>();
        List<RoadSegmentV2> vertical = new List<RoadSegmentV2>();

        for (int i = 0; i < incomingSegments.Count; i++)
        {
            RoadSegmentV2 segment = incomingSegments[i];
            if (segment == null)
                continue;

            Vector3 dir = GetIncomingDirection(segment).normalized;

            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                horizontal.Add(segment);
            else
                vertical.Add(segment);
        }

        if (vertical.Count > 0)
            result.Add(CreatePhase("NorthSouth", incomingSegments, vertical));

        if (horizontal.Count > 0)
            result.Add(CreatePhase("EastWest", incomingSegments, horizontal));

        if (result.Count == 0)
            result.Add(CreatePhase("All", incomingSegments, incomingSegments));

        return result;
    }

    private SignalPhase CreatePhase(
        string phaseName,
        List<RoadSegmentV2> allIncoming,
        List<RoadSegmentV2> activeIncoming)
    {
        HashSet<RoadSegmentV2> activeSet = new HashSet<RoadSegmentV2>(activeIncoming);

        SignalPhase phase = new SignalPhase
        {
            name = phaseName,
            duration = 6f,
            rules = new List<SignalPhaseRule>()
        };

        for (int i = 0; i < allIncoming.Count; i++)
        {
            RoadSegmentV2 segment = allIncoming[i];
            bool active = segment != null && activeSet.Contains(segment);

            phase.rules.Add(new SignalPhaseRule
            {
                incomingSegment = segment,
                allowStraight = active,
                allowLeft = active,
                allowRight = active
            });
        }

        return phase;
    }

    private List<RoadSegmentV2> GetIncomingSegments()
    {
        List<RoadSegmentV2> result = new List<RoadSegmentV2>();

        if (node == null)
            return result;

        for (int i = 0; i < node.ConnectedSegments.Count; i++)
        {
            RoadSegmentV2 segment = node.ConnectedSegments[i];
            if (segment == null)
                continue;

            bool incoming =
                (segment.EndNode == node && segment.ForwardLanes > 0) ||
                (segment.StartNode == node && segment.BackwardLanes > 0);

            if (incoming)
                result.Add(segment);
        }

        return result;
    }

    private Vector3 GetIncomingDirection(RoadSegmentV2 segment)
    {
        if (segment == null || node == null)
            return Vector3.right;

        if (segment.EndNode == node && segment.StartNode != null)
            return (node.transform.position - segment.StartNode.transform.position).normalized;

        if (segment.StartNode == node && segment.EndNode != null)
            return (node.transform.position - segment.EndNode.transform.position).normalized;

        return Vector3.right;
    }

    private SignalPhase GetCurrentPhase()
    {
        if (phases == null || phases.Count == 0)
            return null;

        currentPhaseIndex = Mathf.Clamp(currentPhaseIndex, 0, phases.Count - 1);
        return phases[currentPhaseIndex];
    }

    private SignalPhaseRule FindRuleForSegment(SignalPhase phase, RoadSegmentV2 incomingSegment)
    {
        if (phase == null || phase.rules == null || incomingSegment == null)
            return null;

        for (int i = 0; i < phase.rules.Count; i++)
        {
            SignalPhaseRule rule = phase.rules[i];
            if (rule != null && rule.incomingSegment == incomingSegment)
                return rule;
        }

        return null;
    }

    private Vector3 GetIncomingStopCenter(RoadSegmentV2 segment)
    {
        if (segment == null || node == null)
            return transform.position;

        List<RoadLaneDataV2> incomingLanes = null;

        if (segment.EndNode == node)
            incomingLanes = segment.GetDrivingLanes(segment.StartNode, segment.EndNode);
        else if (segment.StartNode == node)
            incomingLanes = segment.GetDrivingLanes(segment.EndNode, segment.StartNode);

        if (incomingLanes == null || incomingLanes.Count == 0)
            return transform.position;

        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < incomingLanes.Count; i++)
        {
            RoadLaneDataV2 lane = incomingLanes[i];
            if (lane == null)
                continue;

            sum += lane.end;
            count++;
        }

        if (count == 0)
            return transform.position;

        return sum / count;
    }

    private float GetIncomingCarriageHalfWidth(RoadSegmentV2 segment)
    {
        if (segment == null || node == null)
            return 0f;

        int incomingLaneCount = 0;

        if (segment.EndNode == node)
            incomingLaneCount = Mathf.Max(0, segment.ForwardLanes);
        else if (segment.StartNode == node)
            incomingLaneCount = Mathf.Max(0, segment.BackwardLanes);

        if (incomingLaneCount <= 0)
            return 0f;

        return incomingLaneCount * segment.LaneWidth * 0.5f;
    }

    private Vector3 GetSignalHeadPosition(RoadSegmentV2 segment)
    {
        Vector3 stopCenter = GetIncomingStopCenter(segment);
        Vector3 dir = GetIncomingDirection(segment);

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.right;

        Vector3 right = new Vector3(dir.y, -dir.x, 0f);

        float incomingHalfWidth = GetIncomingCarriageHalfWidth(segment);
        float sideOffset = incomingHalfWidth + signalSideOffset + extraRightOffset;

        Vector3 pos =
            stopCenter
            - dir * signalBackOffset
            + right * sideOffset;

        pos.z = 0f;
        return pos;
    }

    private void EnsureSignalsRoot()
    {
        string rootName = $"SignalHeads_Node_{GetInstanceID()}";

        if (signalsRoot != null)
            return;

        Transform parent = transform.parent;

        if (parent != null)
        {
            Transform existing = parent.Find(rootName);
            if (existing != null)
            {
                signalsRoot = existing;
                return;
            }
        }

        GameObject root = new GameObject(rootName);

        if (parent != null)
            root.transform.SetParent(parent);
        else
            root.transform.SetParent(null);

        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        signalsRoot = root.transform;
    }

    private void EnsureSignalHeads()
    {
        EnsureSignalsRoot();

        List<RoadSegmentV2> incomingSegments = GetIncomingSegments();
        HashSet<int> requiredIds = new HashSet<int>();

        for (int i = 0; i < incomingSegments.Count; i++)
        {
            RoadSegmentV2 segment = incomingSegments[i];
            if (segment == null)
                continue;

            requiredIds.Add(segment.Id);
            EnsureSignalHead(segment);
        }

        List<int> toRemove = new List<int>();

        foreach (KeyValuePair<int, SignalHeadVisual> pair in signalHeads)
        {
            if (!requiredIds.Contains(pair.Key))
                toRemove.Add(pair.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            int key = toRemove[i];

            if (signalHeads.TryGetValue(key, out SignalHeadVisual head))
            {
                if (head != null && head.rootObject != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(head.rootObject);
                    else
                        Destroy(head.rootObject);
#else
                    Destroy(head.rootObject);
#endif
                }
            }

            signalHeads.Remove(key);
        }
    }

    private void EnsureSignalHead(RoadSegmentV2 segment)
    {
        if (segment == null || signalsRoot == null)
            return;

        if (signalHeads.TryGetValue(segment.Id, out SignalHeadVisual existing) &&
            existing != null &&
            existing.rootObject != null)
        {
            existing.segment = segment;
            return;
        }

        GameObject root = new GameObject($"SignalHead_{segment.Id}");
        root.transform.SetParent(signalsRoot);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        SpriteRenderer body = root.AddComponent<SpriteRenderer>();
        body.sprite = GetWhiteSprite();
        body.sortingOrder = bodySortingOrder;

        GameObject lampObject = new GameObject("Lamp");
        lampObject.transform.SetParent(root.transform);
        lampObject.transform.localPosition = Vector3.zero;
        lampObject.transform.localRotation = Quaternion.identity;
        lampObject.transform.localScale = Vector3.one;

        SpriteRenderer lamp = lampObject.AddComponent<SpriteRenderer>();
        lamp.sprite = GetWhiteSprite();
        lamp.sortingOrder = lampSortingOrder;

        SignalHeadVisual head = new SignalHeadVisual
        {
            segment = segment,
            rootObject = root,
            bodyRenderer = body,
            lampRenderer = lamp
        };

        signalHeads[segment.Id] = head;
    }

    private void RefreshSignalVisuals()
    {
        if (node == null)
            return;

        bool visible = node.UsesTrafficLight;

        if (!visible)
        {
            HideAllHeads();
            return;
        }

        EnsureSignalHeads();

        foreach (KeyValuePair<int, SignalHeadVisual> pair in signalHeads)
        {
            SignalHeadVisual head = pair.Value;
            if (head == null || head.segment == null || head.rootObject == null)
                continue;

            SetHeadVisible(head, true);

            Vector3 pos = GetSignalHeadPosition(head.segment);
            head.rootObject.transform.position = pos;
            head.rootObject.transform.rotation = Quaternion.identity;
            head.rootObject.transform.localScale = Vector3.one;

            float scale = Mathf.Max(signalScale, 0.01f);
            float visibleBodyWidth = Mathf.Max(bodyWidth, 0.24f) * scale;
            float visibleBodyHeight = Mathf.Max(bodyHeight, 0.34f) * scale;
            float visibleLampSize = Mathf.Max(lampSize, 0.16f) * scale;

            if (head.bodyRenderer != null)
            {
                head.bodyRenderer.color = bodyColor;
                head.bodyRenderer.transform.localScale = new Vector3(visibleBodyWidth, visibleBodyHeight, 1f);
            }

            if (head.lampRenderer != null)
            {
                head.lampRenderer.color = GetLampColor(GetLampStateForSegment(head.segment));
                head.lampRenderer.transform.localPosition = Vector3.zero;
                head.lampRenderer.transform.localScale = new Vector3(visibleLampSize, visibleLampSize, 1f);
            }
        }
    }

    private void HideAllHeads()
    {
        foreach (KeyValuePair<int, SignalHeadVisual> pair in signalHeads)
        {
            SignalHeadVisual head = pair.Value;
            if (head == null)
                continue;

            SetHeadVisible(head, false);
        }
    }

    private void SetHeadVisible(SignalHeadVisual head, bool visible)
    {
        if (head == null)
            return;

        if (head.bodyRenderer != null)
            head.bodyRenderer.enabled = visible;

        if (head.lampRenderer != null)
            head.lampRenderer.enabled = visible;
    }

    private LampState GetLampStateForSegment(RoadSegmentV2 incomingSegment)
    {
        SignalPhase phase = GetCurrentPhase();
        if (phase == null)
            return LampState.Red;

        SignalPhaseRule rule = FindRuleForSegment(phase, incomingSegment);
        if (rule == null)
            return LampState.Red;

        return rule.HasAnyGreen() ? LampState.Green : LampState.Red;
    }

    private Color GetLampColor(LampState state)
    {
        switch (state)
        {
            case LampState.Red:
                return redColor;

            case LampState.Yellow:
                return yellowColor;

            case LampState.Green:
                return greenColor;

            default:
                return offColor;
        }
    }

    private static Sprite GetWhiteSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        Texture2D tex = Texture2D.whiteTexture;
        cachedSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        return cachedSprite;
    }
}