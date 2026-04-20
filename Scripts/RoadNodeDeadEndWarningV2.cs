using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class RoadNodeDeadEndWarningV2 : MonoBehaviour
{
    [SerializeField] private RoadNodeV2 node;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.95f, 0f);
    [SerializeField] private float signWidth = 1.05f;
    [SerializeField] private float signHeight = 0.92f;
    [SerializeField] private float borderWidth = 0.12f;
    [SerializeField] private int sortingOrder = 70;
    [SerializeField] private Color borderColor = new Color(0.9f, 0.12f, 0.12f, 1f);
    [SerializeField] private Color fillColor = Color.white;
    [SerializeField] private Color symbolColor = new Color(0.05f, 0.05f, 0.05f, 1f);

    private MeshFilter fillMeshFilter;
    private MeshRenderer fillMeshRenderer;
    private LineRenderer borderRenderer;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer dotRenderer;

    private static Material cachedMeshMaterial;
    private static Material cachedLineMaterial;
    private static Sprite cachedSprite;

    public void SyncFromNode()
    {
        if (node == null)
            node = GetComponent<RoadNodeV2>();

        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (node == null)
            node = GetComponent<RoadNodeV2>();

        EnsureParts();
        UpdateTriangleFill();
        UpdateTriangleBorder();
        UpdateExclamationMark();
    }

    public void ClearVisuals()
    {
        if (fillMeshRenderer != null)
            fillMeshRenderer.enabled = false;

        if (borderRenderer != null)
            borderRenderer.enabled = false;

        if (bodyRenderer != null)
            bodyRenderer.enabled = false;

        if (dotRenderer != null)
            dotRenderer.enabled = false;
    }

    private void EnsureParts()
    {
        EnsureFill();
        EnsureBorder();
        EnsureExclamationSprites();
    }

    private void EnsureFill()
    {
        Transform child = transform.Find("DeadEndWarningFill");
        GameObject go = child != null ? child.gameObject : new GameObject("DeadEndWarningFill");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        fillMeshFilter = go.GetComponent<MeshFilter>();
        if (fillMeshFilter == null)
            fillMeshFilter = go.AddComponent<MeshFilter>();

        fillMeshRenderer = go.GetComponent<MeshRenderer>();
        if (fillMeshRenderer == null)
            fillMeshRenderer = go.AddComponent<MeshRenderer>();

        if (cachedMeshMaterial == null)
            cachedMeshMaterial = new Material(Shader.Find("Sprites/Default"));

        fillMeshRenderer.sharedMaterial = cachedMeshMaterial;
        fillMeshRenderer.sortingOrder = sortingOrder;
    }

    private void EnsureBorder()
    {
        Transform child = transform.Find("DeadEndWarningBorder");
        GameObject go = child != null ? child.gameObject : new GameObject("DeadEndWarningBorder");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        borderRenderer = go.GetComponent<LineRenderer>();
        if (borderRenderer == null)
            borderRenderer = go.AddComponent<LineRenderer>();

        if (cachedLineMaterial == null)
            cachedLineMaterial = new Material(Shader.Find("Sprites/Default"));

        borderRenderer.sharedMaterial = cachedLineMaterial;
        borderRenderer.useWorldSpace = false;
        borderRenderer.loop = true;
        borderRenderer.alignment = LineAlignment.TransformZ;
        borderRenderer.textureMode = LineTextureMode.Stretch;
        borderRenderer.numCapVertices = 8;
        borderRenderer.numCornerVertices = 8;
        borderRenderer.sortingOrder = sortingOrder + 1;
    }

    private void EnsureExclamationSprites()
    {
        bodyRenderer = EnsureSymbolSprite(
            "DeadEndWarningBody",
            new Vector3(0f, 0.08f, 0f),
            new Vector3(signWidth * 0.16f, signHeight * 0.54f, 1f),
            sortingOrder + 2);

        dotRenderer = EnsureSymbolSprite(
            "DeadEndWarningDot",
            new Vector3(0f, -signHeight * 0.28f, 0f),
            new Vector3(signWidth * 0.18f, signWidth * 0.18f, 1f),
            sortingOrder + 2);
    }

    private SpriteRenderer EnsureSymbolSprite(string objectName, Vector3 localPosition, Vector3 localScale, int order)
    {
        Transform child = transform.Find(objectName);
        GameObject go = child != null ? child.gameObject : new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localOffset + localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = go.AddComponent<SpriteRenderer>();

        renderer.sprite = GetWhiteSprite();
        renderer.color = symbolColor;
        renderer.sortingOrder = order;

        return renderer;
    }

    private void UpdateTriangleFill()
    {
        if (fillMeshFilter == null || fillMeshRenderer == null)
            return;

        float halfWidth = signWidth * 0.5f;
        float halfHeight = signHeight * 0.5f;

        Mesh mesh = new Mesh();
        mesh.name = "DeadEndWarningTriangleFill";
        mesh.vertices = new[]
        {
            new Vector3(0f, halfHeight, 0f),
            new Vector3(-halfWidth, -halfHeight, 0f),
            new Vector3(halfWidth, -halfHeight, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.colors = new[] { fillColor, fillColor, fillColor };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        fillMeshFilter.sharedMesh = mesh;
        fillMeshRenderer.enabled = true;
    }

    private void UpdateTriangleBorder()
    {
        if (borderRenderer == null)
            return;

        float halfWidth = signWidth * 0.5f;
        float halfHeight = signHeight * 0.5f;
        float inset = borderWidth * 0.5f;

        borderRenderer.enabled = true;
        borderRenderer.positionCount = 3;
        borderRenderer.startWidth = borderWidth;
        borderRenderer.endWidth = borderWidth;
        borderRenderer.startColor = borderColor;
        borderRenderer.endColor = borderColor;
        borderRenderer.SetPosition(0, new Vector3(0f, halfHeight - inset, 0f));
        borderRenderer.SetPosition(1, new Vector3(-halfWidth + inset, -halfHeight + inset, 0f));
        borderRenderer.SetPosition(2, new Vector3(halfWidth - inset, -halfHeight + inset, 0f));
    }

    private void UpdateExclamationMark()
    {
        if (bodyRenderer != null)
            bodyRenderer.enabled = true;

        if (dotRenderer != null)
            dotRenderer.enabled = true;
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
