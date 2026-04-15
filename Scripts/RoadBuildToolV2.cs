using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class RoadNodeV2 : MonoBehaviour
{
    public enum JunctionControlMode
    {
        RightHandRule,
        TrafficLight
    }

    [System.Serializable]
    public class ApproachRule
    {
        public RoadSegmentV2 incomingSegment;
        public bool allowStraight = true;
        public bool allowLeft = true;
        public bool allowRight = true;
    }

    [SerializeField] private int id;
    [SerializeField] private float visualSize = 0.22f;

    [Header("Junction control")]
    [SerializeField] private JunctionControlMode controlMode = JunctionControlMode.RightHandRule;

    [Header("Default junction rules")]
    [SerializeField] private bool allowStraight = true;
    [SerializeField] private bool allowLeft = true;
    [SerializeField] private bool allowRight = true;

    [Header("Per-approach rules")]
    [SerializeField] private List<ApproachRule> approachRules = new List<ApproachRule>();

    private readonly List<RoadSegmentV2> connectedSegments = new List<RoadSegmentV2>();
    private SpriteRenderer spriteRenderer;

    private static Sprite cachedSprite;

    public int Id => id;
    public IReadOnlyList<RoadSegmentV2> ConnectedSegments => connectedSegments;

    public bool AllowStraight => allowStraight;
    public bool AllowLeft => allowLeft;
    public bool AllowRight => allowRight;

    public JunctionControlMode ControlMode => controlMode;
    public bool IsIntersection => connectedSegments.Count > 2;
    public bool UsesTrafficLight => IsIntersection && controlMode == JunctionControlMode.TrafficLight;

    public void Initialize(int newId)
    {
        id = newId;
        gameObject.name = $"RoadNode_{id}";
        EnsureVisual();
    }

    public void RegisterSegment(RoadSegmentV2 segment)
    {
        if (segment == null)
            return;

        if (!connectedSegments.Contains(segment))
        {
            connectedSegments.Add(segment);
            EnsureApproachRuleEntries();
            EnsureVisual();
        }
    }

    public void UnregisterSegment(RoadSegmentV2 segment)
    {
        if (segment == null)
            return;

        if (connectedSegments.Remove(segment))
        {
            RemoveApproachRuleEntriesForMissingSegments();
            EnsureVisual();
        }
    }

    public void SetControlMode(JunctionControlMode mode)
    {
        controlMode = mode;
        EnsureVisual();
    }

    public void ToggleControlMode()
    {
        controlMode = controlMode == JunctionControlMode.RightHandRule
            ? JunctionControlMode.TrafficLight
            : JunctionControlMode.RightHandRule;

        EnsureVisual();
    }

    public bool AllowsMovement(RoadLaneConnectionV2.MovementType movementType)
    {
        return AllowsMovement(null, movementType);
    }

    public bool AllowsMovement(RoadSegmentV2 incomingSegment, RoadLaneConnectionV2.MovementType movementType)
    {
        ApproachRule rule = GetApproachRule(incomingSegment);

        switch (movementType)
        {
            case RoadLaneConnectionV2.MovementType.Straight:
                return rule != null ? rule.allowStraight : allowStraight;

            case RoadLaneConnectionV2.MovementType.Left:
                return rule != null ? rule.allowLeft : allowLeft;

            case RoadLaneConnectionV2.MovementType.Right:
                return rule != null ? rule.allowRight : allowRight;
        }

        return true;
    }

    public bool TryGetApproachRule(RoadSegmentV2 incomingSegment, out ApproachRule rule)
    {
        rule = GetApproachRule(incomingSegment);
        return rule != null;
    }

    public void SetApproachMovement(RoadSegmentV2 incomingSegment, RoadLaneConnectionV2.MovementType movementType, bool allowed)
    {
        ApproachRule rule = GetOrCreateApproachRule(incomingSegment);
        if (rule == null)
            return;

        switch (movementType)
        {
            case RoadLaneConnectionV2.MovementType.Straight:
                rule.allowStraight = allowed;
                break;

            case RoadLaneConnectionV2.MovementType.Left:
                rule.allowLeft = allowed;
                break;

            case RoadLaneConnectionV2.MovementType.Right:
                rule.allowRight = allowed;
                break;
        }
    }

    public void ToggleApproachMovement(RoadSegmentV2 incomingSegment, RoadLaneConnectionV2.MovementType movementType)
    {
        ApproachRule rule = GetOrCreateApproachRule(incomingSegment);
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
    }

    private ApproachRule GetApproachRule(RoadSegmentV2 incomingSegment)
    {
        if (incomingSegment == null)
            return null;

        for (int i = 0; i < approachRules.Count; i++)
        {
            ApproachRule rule = approachRules[i];
            if (rule == null)
                continue;

            if (rule.incomingSegment == incomingSegment)
                return rule;
        }

        return null;
    }

    private ApproachRule GetOrCreateApproachRule(RoadSegmentV2 incomingSegment)
    {
        if (incomingSegment == null)
            return null;

        ApproachRule rule = GetApproachRule(incomingSegment);
        if (rule != null)
            return rule;

        rule = new ApproachRule
        {
            incomingSegment = incomingSegment,
            allowStraight = allowStraight,
            allowLeft = allowLeft,
            allowRight = allowRight
        };

        approachRules.Add(rule);
        return rule;
    }

    private void Awake()
    {
        EnsureApproachRuleEntries();
        EnsureVisual();
    }

    private void OnValidate()
    {
        EnsureApproachRuleEntries();
        RemoveApproachRuleEntriesForMissingSegments();
        EnsureVisual();
    }

    private void EnsureApproachRuleEntries()
    {
        for (int i = 0; i < connectedSegments.Count; i++)
        {
            RoadSegmentV2 segment = connectedSegments[i];
            if (segment == null)
                continue;

            bool hasRule = false;

            for (int j = 0; j < approachRules.Count; j++)
            {
                ApproachRule rule = approachRules[j];
                if (rule == null)
                    continue;

                if (rule.incomingSegment == segment)
                {
                    hasRule = true;
                    break;
                }
            }

            if (!hasRule)
            {
                approachRules.Add(new ApproachRule
                {
                    incomingSegment = segment,
                    allowStraight = allowStraight,
                    allowLeft = allowLeft,
                    allowRight = allowRight
                });
            }
        }
    }

    private void RemoveApproachRuleEntriesForMissingSegments()
    {
        for (int i = approachRules.Count - 1; i >= 0; i--)
        {
            ApproachRule rule = approachRules[i];

            if (rule == null || rule.incomingSegment == null)
            {
                approachRules.RemoveAt(i);
                continue;
            }

            if (!connectedSegments.Contains(rule.incomingSegment))
                approachRules.RemoveAt(i);
        }
    }

    private void EnsureVisual()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = GetWhiteSprite();
        spriteRenderer.color = GetNodeColor();
        spriteRenderer.sortingOrder = 30;

        transform.localScale = new Vector3(visualSize, visualSize, 1f);
    }

    private Color GetNodeColor()
    {
        if (!IsIntersection)
            return new Color(1f, 0.65f, 0.1f, 0.95f);

        if (controlMode == JunctionControlMode.TrafficLight)
            return new Color(0.2f, 0.9f, 1f, 0.95f);

        return new Color(1f, 0.65f, 0.1f, 0.95f);
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