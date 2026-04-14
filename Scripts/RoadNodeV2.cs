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

    [SerializeField] private int id;
    [SerializeField] private float visualSize = 0.22f;

    [Header("Junction control")]
    [SerializeField] private JunctionControlMode controlMode = JunctionControlMode.RightHandRule;

    [Header("Junction rules")]
    [SerializeField] private bool allowStraight = true;
    [SerializeField] private bool allowLeft = true;
    [SerializeField] private bool allowRight = true;

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
            EnsureVisual();
        }
    }

    public void UnregisterSegment(RoadSegmentV2 segment)
    {
        if (segment == null)
            return;

        if (connectedSegments.Remove(segment))
            EnsureVisual();
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
        switch (movementType)
        {
            case RoadLaneConnectionV2.MovementType.Straight:
                return allowStraight;

            case RoadLaneConnectionV2.MovementType.Left:
                return allowLeft;

            case RoadLaneConnectionV2.MovementType.Right:
                return allowRight;
        }

        return true;
    }

    private void Awake()
    {
        EnsureVisual();
    }

    private void OnValidate()
    {
        EnsureVisual();
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