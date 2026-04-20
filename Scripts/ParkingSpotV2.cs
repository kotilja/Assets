using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class ParkingSpotV2 : MonoBehaviour
{
    [SerializeField] private bool isOccupied = false;
    [SerializeField] private RoadSegmentV2 connectedRoadSegment;
    [SerializeField] private Vector3 localForward = Vector3.right;
    [SerializeField] private bool pedestrianAnchorOnLeftSide = true;
    [SerializeField] private float pedestrianAnchorDistance = 0.35f;
    [SerializeField] private float gizmoLength = 0.7f;
    [SerializeField] private float gizmoWidth = 0.32f;
    [SerializeField] private Vector2 visualSize = new Vector2(0.88f, 0.5f);
    [SerializeField] private Color pavementColor = new Color(0.22f, 0.22f, 0.24f, 1f);
    [SerializeField] private Color occupiedColor = new Color(0.33f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color markingColor = Color.white;
    [SerializeField] private int bodySortingOrder = 12;
    [SerializeField] private int markingSortingOrder = 13;

    private bool delayedGraphRebuildQueued = false;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer markingRenderer;

    private static Sprite solidSquareSprite;
    private static Sprite parkingMarkSprite;

    public bool IsOccupied => isOccupied;
    public RoadSegmentV2 ConnectedRoadSegment => connectedRoadSegment;

    public Vector3 ParkingPosition => transform.position;

    public Vector3 Forward
    {
        get
        {
            Vector3 f = transform.TransformDirection(localForward.normalized);
            f.z = 0f;
            return f.sqrMagnitude < 0.0001f ? Vector3.right : f.normalized;
        }
    }

    public Vector3 PedestrianAnchorPoint
    {
        get
        {
            Vector3 fwd = Forward;
            Vector3 side = pedestrianAnchorOnLeftSide
                ? new Vector3(-fwd.y, fwd.x, 0f)
                : new Vector3(fwd.y, -fwd.x, 0f);

            if (side.sqrMagnitude < 0.0001f)
                side = Vector3.up;

            Vector3 p = ParkingPosition + side.normalized * Mathf.Max(0.05f, pedestrianAnchorDistance);
            p.z = 0f;
            return p;
        }
    }

    public bool CanUse()
    {
        return !isOccupied;
    }

    public bool Reserve()
    {
        if (isOccupied)
            return false;

        isOccupied = true;
        UpdateVisuals();
        return true;
    }

    public void Release()
    {
        isOccupied = false;
        UpdateVisuals();
    }

    public void SetConnectedRoadSegment(RoadSegmentV2 segment)
    {
        connectedRoadSegment = segment;
        SyncPedestrianAnchorSideFromRoad();
        UpdateVisuals();
    }

    public void SetPedestrianAnchorSide(bool onLeftSide)
    {
        pedestrianAnchorOnLeftSide = onLeftSide;
    }

    public void SetPedestrianAnchorDistance(float distance)
    {
        pedestrianAnchorDistance = Mathf.Max(0.05f, distance);
    }

    private void Awake()
    {
        EnsureVisuals();
    }

    private void OnEnable()
    {
        EnsureVisuals();
    }

    private void OnValidate()
    {
        SyncPedestrianAnchorSideFromRoad();
        EnsureVisuals();

#if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        if (delayedGraphRebuildQueued)
            return;

        delayedGraphRebuildQueued = true;
        EditorApplication.delayCall += DelayedRebuildPedestrianGraph;
#endif
    }

#if UNITY_EDITOR
    private void DelayedRebuildPedestrianGraph()
    {
        delayedGraphRebuildQueued = false;

        if (this == null || Application.isPlaying)
            return;

        PedestrianNetworkV2 pedestrianNetwork = FindFirstObjectByType<PedestrianNetworkV2>();
        if (pedestrianNetwork != null)
            pedestrianNetwork.RebuildGraph();
    }
#endif

    private void SyncPedestrianAnchorSideFromRoad()
    {
        if (connectedRoadSegment == null)
            return;

        List<Vector3> polyline = connectedRoadSegment.GetCenterPolylineWorld();
        if (polyline == null || polyline.Count < 2)
            return;

        Vector3 position = ParkingPosition;
        Vector3 snappedPoint = ProjectPointOntoPolyline(position, polyline);
        Vector3 tangent = GetPolylineDirectionAtPoint(polyline, snappedPoint);
        Vector3 toParking = position - snappedPoint;

        pedestrianAnchorOnLeftSide = Vector3.Cross(tangent.normalized, toParking).z >= 0f;
    }

    private void EnsureVisuals()
    {
        EnsureGeneratedSprites();
        EnsureRendererReferences();
        UpdateVisuals();
    }

    private void EnsureRendererReferences()
    {
        bodyRenderer = EnsureVisualRenderer("Body", ref bodyRenderer, solidSquareSprite, bodySortingOrder, SpriteDrawMode.Simple);
        markingRenderer = EnsureVisualRenderer("Marking", ref markingRenderer, parkingMarkSprite, markingSortingOrder, SpriteDrawMode.Simple);
    }

    private SpriteRenderer EnsureVisualRenderer(string childName, ref SpriteRenderer renderer, Sprite sprite, int sortingOrder, SpriteDrawMode drawMode)
    {
        if (renderer == null)
        {
            Transform existing = transform.Find(childName);
            if (existing != null)
                renderer = existing.GetComponent<SpriteRenderer>();
        }

        if (renderer == null)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            renderer = child.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = sprite;
        renderer.drawMode = drawMode;
        renderer.sortingOrder = sortingOrder;
        renderer.maskInteraction = SpriteMaskInteraction.None;
        return renderer;
    }

    private void UpdateVisuals()
    {
        if (bodyRenderer == null || markingRenderer == null)
            return;

        Vector2 clampedSize = new Vector2(
            Mathf.Max(0.3f, visualSize.x),
            Mathf.Max(0.2f, visualSize.y));
        Vector2 bodySpriteSize = solidSquareSprite != null ? solidSquareSprite.bounds.size : Vector2.one;

        bodyRenderer.color = isOccupied ? occupiedColor : pavementColor;
        bodyRenderer.transform.localPosition = Vector3.zero;
        bodyRenderer.transform.localRotation = Quaternion.identity;
        bodyRenderer.transform.localScale = new Vector3(
            Mathf.Max(0.01f, clampedSize.x / Mathf.Max(0.01f, bodySpriteSize.x)),
            Mathf.Max(0.01f, clampedSize.y / Mathf.Max(0.01f, bodySpriteSize.y)),
            1f);

        markingRenderer.color = markingColor;
        Vector2 markingSize = new Vector2(clampedSize.y * 0.5f, clampedSize.y * 0.5f);
        Vector2 markingSpriteSize = parkingMarkSprite != null ? parkingMarkSprite.bounds.size : Vector2.one;
        markingRenderer.transform.localPosition = new Vector3(0f, 0f, -0.001f);
        markingRenderer.transform.localRotation = Quaternion.identity;
        markingRenderer.transform.localScale = new Vector3(
            Mathf.Max(0.01f, markingSize.x / Mathf.Max(0.01f, markingSpriteSize.x)),
            Mathf.Max(0.01f, markingSize.y / Mathf.Max(0.01f, markingSpriteSize.y)),
            1f);
    }

    private void EnsureGeneratedSprites()
    {
        if (solidSquareSprite == null)
            solidSquareSprite = CreateSolidSquareSprite();

        if (parkingMarkSprite == null)
            parkingMarkSprite = CreateParkingMarkSprite();
    }

    private static Sprite CreateSolidSquareSprite()
    {
        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 16f);
    }

    private static Sprite CreateParkingMarkSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 white = new Color32(255, 255, 255, 255);
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        for (int x = 7; x < size - 7; x++)
        {
            SetPixel(pixels, size, x, 7, white);
            SetPixel(pixels, size, x, size - 8, white);
        }

        for (int y = 7; y < size - 7; y++)
        {
            SetPixel(pixels, size, 7, y, white);
            SetPixel(pixels, size, size - 8, y, white);
        }

        for (int y = 15; y <= 48; y++)
        {
            for (int x = 20; x <= 26; x++)
                SetPixel(pixels, size, x, y, white);
        }

        for (int x = 20; x <= 40; x++)
        {
            for (int y = 41; y <= 47; y++)
                SetPixel(pixels, size, x, y, white);

            for (int y = 28; y <= 34; y++)
                SetPixel(pixels, size, x, y, white);
        }

        for (int y = 34; y <= 47; y++)
        {
            for (int x = 34; x <= 40; x++)
                SetPixel(pixels, size, x, y, white);
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static void SetPixel(Color32[] pixels, int width, int x, int y, Color32 color)
    {
        if (x < 0 || y < 0 || x >= width || y >= width)
            return;

        pixels[y * width + x] = color;
    }

    private Vector3 ProjectPointOntoPolyline(Vector3 point, List<Vector3> polyline)
    {
        if (polyline == null || polyline.Count < 2)
            return point;

        Vector3 bestPoint = point;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            Vector3 candidate = ProjectPointOntoSegment(point, polyline[i], polyline[i + 1]);
            float distance = Vector3.Distance(point, candidate);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = candidate;
            }
        }

        return bestPoint;
    }

    private Vector3 GetPolylineDirectionAtPoint(List<Vector3> polyline, Vector3 point)
    {
        if (polyline == null || polyline.Count < 2)
            return Vector3.right;

        float bestDistance = float.MaxValue;
        Vector3 bestDirection = Vector3.right;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            Vector3 a = polyline[i];
            Vector3 b = polyline[i + 1];
            Vector3 projected = ProjectPointOntoSegment(point, a, b);
            float distance = Vector3.Distance(point, projected);

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestDirection = (b - a).normalized;
        }

        bestDirection.z = 0f;
        return bestDirection.sqrMagnitude < 0.0001f ? Vector3.right : bestDirection.normalized;
    }

    private Vector3 ProjectPointOntoSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;

        if (ab.sqrMagnitude < 0.0001f)
            return a;

        float t = Vector3.Dot(point - a, ab) / Vector3.Dot(ab, ab);
        t = Mathf.Clamp01(t);

        Vector3 projected = a + ab * t;
        projected.z = 0f;
        return projected;
    }

    private void OnDrawGizmos()
    {
        Vector3 pos = ParkingPosition;
        Vector3 fwd = Forward;
        Vector3 right = new Vector3(fwd.y, -fwd.x, 0f);

        Vector3 halfL = fwd * (gizmoLength * 0.5f);
        Vector3 halfW = right * (gizmoWidth * 0.5f);

        Vector3 p0 = pos - halfL - halfW;
        Vector3 p1 = pos + halfL - halfW;
        Vector3 p2 = pos + halfL + halfW;
        Vector3 p3 = pos - halfL + halfW;

        Gizmos.color = isOccupied ? new Color(1f, 0.3f, 0.3f, 1f) : new Color(0.3f, 1f, 0.3f, 1f);
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p0);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pos, pos + fwd * 0.7f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(PedestrianAnchorPoint, 0.06f);
    }
}
