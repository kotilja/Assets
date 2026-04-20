using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class BuildingPseudo3DVisualV2 : MonoBehaviour
{
    private enum WallSide
    {
        North,
        East,
        South,
        West
    }

    private class WallRuntime
    {
        public WallSide side;
        public Transform root;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public Mesh mesh;
    }

    [Header("References")]
    [SerializeField] private Transform baseVisual;
    [SerializeField] private Transform roofVisual;
    [SerializeField] private Camera targetCamera;

    [Header("Height")]
    [SerializeField] private float visualHeight = 1f;
    [SerializeField] private float roofOffsetPerHeight = 0.18f;
    [SerializeField] private float maxRoofOffset = 1.2f;

    [Header("Walls")]
    [SerializeField] private int floorCount = 1;
    [SerializeField] private Sprite northWallSprite;
    [SerializeField] private Sprite eastWallSprite;
    [SerializeField] private Sprite southWallSprite;
    [SerializeField] private Sprite westWallSprite;
    [SerializeField] private Color wallColor = Color.white;
    [SerializeField] private float wallInset = 0.005f;
    [SerializeField] private float wallVisibilityThreshold = 0.02f;
    [SerializeField] private bool fitWallSpritesToBuildingSide = true;
    [SerializeField] private float wallDepthOffset = 0.001f;
    [SerializeField] private int wallSortingOrder = 5;

    [Header("Response")]
    [SerializeField] private float fullEffectDistance = 12f;
    [SerializeField] private float smoothing = 14f;
    [SerializeField] private bool updateInEditMode = true;

    private Vector3 baseInitialLocalPosition;
    private Vector3 roofInitialLocalPosition;
    private bool cachedInitialState;
    private Transform wallsRoot;
    private readonly List<WallRuntime> wallRuntimes = new List<WallRuntime>();
    private Material sharedWallMaterial;
    private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

    public float VisualHeight
    {
        get => visualHeight;
        set => visualHeight = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        AutoAssignReferences();
        CacheInitialState();
        EnsureWallRuntime();
        SnapToCurrentCameraState();
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        CacheInitialState();
        EnsureWallRuntime();
        SnapToCurrentCameraState();
    }

    private void OnValidate()
    {
        visualHeight = Mathf.Max(0f, visualHeight);
        roofOffsetPerHeight = Mathf.Max(0f, roofOffsetPerHeight);
        maxRoofOffset = Mathf.Max(0f, maxRoofOffset);
        floorCount = Mathf.Max(1, floorCount);
        fullEffectDistance = Mathf.Max(0.01f, fullEffectDistance);
        smoothing = Mathf.Max(0f, smoothing);
        wallInset = Mathf.Max(0f, wallInset);
        wallVisibilityThreshold = Mathf.Max(0f, wallVisibilityThreshold);
        wallDepthOffset = Mathf.Max(0f, wallDepthOffset);

        AutoAssignReferences();
        CacheInitialState();
        EnsureWallRuntime();
        SnapToCurrentCameraState();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying && !updateInEditMode)
            return;

        AutoAssignReferences();
        CacheInitialState();
        EnsureWallRuntime();
        ApplyVisualOffset(Application.isPlaying);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < wallRuntimes.Count; i++)
        {
            if (wallRuntimes[i] != null && wallRuntimes[i].mesh != null)
            {
                if (Application.isPlaying)
                    Destroy(wallRuntimes[i].mesh);
                else
                    DestroyImmediate(wallRuntimes[i].mesh);
            }
        }

        if (sharedWallMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(sharedWallMaterial);
            else
                DestroyImmediate(sharedWallMaterial);
        }
    }

    private void AutoAssignReferences()
    {
        if (baseVisual == null)
        {
            Transform child = transform.Find("Base");
            if (child != null)
                baseVisual = child;
        }

        if (roofVisual == null)
        {
            Transform child = transform.Find("Roof");
            if (child != null)
                roofVisual = child;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void CacheInitialState()
    {
        if (cachedInitialState)
            return;

        if (baseVisual != null)
            baseInitialLocalPosition = baseVisual.localPosition;

        if (roofVisual != null)
            roofInitialLocalPosition = roofVisual.localPosition;

        cachedInitialState = true;
    }

    private void EnsureWallRuntime()
    {
        if (wallsRoot == null)
        {
            Transform existing = transform.Find("GeneratedWalls");
            if (existing != null)
                wallsRoot = existing;
            else
            {
                GameObject go = new GameObject("GeneratedWalls");
                go.transform.SetParent(transform, false);
                wallsRoot = go.transform;
            }
        }

        if (sharedWallMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            sharedWallMaterial = new Material(shader);
            sharedWallMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        if (wallRuntimes.Count > 0)
            return;

        wallRuntimes.Add(CreateWallRuntime(WallSide.North));
        wallRuntimes.Add(CreateWallRuntime(WallSide.East));
        wallRuntimes.Add(CreateWallRuntime(WallSide.South));
        wallRuntimes.Add(CreateWallRuntime(WallSide.West));
    }

    private WallRuntime CreateWallRuntime(WallSide side)
    {
        GameObject sideObject = new GameObject(side + "Wall");
        sideObject.transform.SetParent(wallsRoot, false);

        MeshFilter meshFilter = sideObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = sideObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = sharedWallMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.sortingOrder = wallSortingOrder;

        Mesh mesh = new Mesh
        {
            name = side + "WallMesh"
        };
        meshFilter.sharedMesh = mesh;

        return new WallRuntime
        {
            side = side,
            root = sideObject.transform,
            meshFilter = meshFilter,
            meshRenderer = meshRenderer,
            mesh = mesh
        };
    }

    private void SnapToCurrentCameraState()
    {
        ApplyVisualOffset(false);
    }

    private void ApplyVisualOffset(bool smoothMotion)
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
        {
            ResetVisualsImmediate();
            return;
        }

        Vector3 buildingPosition = transform.position;
        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 toCamera = cameraPosition - buildingPosition;
        toCamera.z = 0f;

        float planarDistance = toCamera.magnitude;
        Vector3 planarDirection = planarDistance > 0.0001f ? toCamera / planarDistance : Vector3.zero;
        float effectT = Mathf.Clamp01(planarDistance / fullEffectDistance);
        float heightFactor = Mathf.Max(0f, visualHeight);

        Vector3 roofTargetOffset = planarDirection * Mathf.Min(maxRoofOffset, heightFactor * roofOffsetPerHeight * effectT);
        roofTargetOffset.z = 0f;

        if (baseVisual != null)
            baseVisual.localPosition = baseInitialLocalPosition;

        if (roofVisual != null)
        {
            Vector3 targetPosition = roofInitialLocalPosition + roofTargetOffset;
            roofVisual.localPosition = smoothMotion
                ? Vector3.Lerp(roofVisual.localPosition, targetPosition, 1f - Mathf.Exp(-smoothing * Time.deltaTime))
                : targetPosition;
        }

        UpdateWalls();
    }

    private void UpdateWalls()
    {
        if (baseVisual == null || roofVisual == null || wallsRoot == null)
        {
            SetWallsVisible(false);
            return;
        }

        SpriteRenderer baseRenderer = baseVisual.GetComponent<SpriteRenderer>();
        SpriteRenderer roofRenderer = roofVisual.GetComponent<SpriteRenderer>();
        if (baseRenderer == null || roofRenderer == null || baseRenderer.sprite == null || roofRenderer.sprite == null)
        {
            SetWallsVisible(false);
            return;
        }

        Vector2 baseSize = GetLocalSpriteSize(baseRenderer);
        Vector2 roofSize = GetLocalSpriteSize(roofRenderer);
        Vector3 roofOffset = roofVisual.localPosition - baseInitialLocalPosition;
        roofOffset.z = 0f;

        for (int i = 0; i < wallRuntimes.Count; i++)
        {
            WallRuntime runtime = wallRuntimes[i];
            Sprite wallSprite = GetWallSprite(runtime.side);
            Vector3 sideNormal = GetSideNormal(runtime.side);

            float visibility = roofOffset.sqrMagnitude > 0.0001f
                ? Vector3.Dot((-roofOffset).normalized, sideNormal)
                : 0f;

            bool isVisible = wallSprite != null &&
                             roofOffset.sqrMagnitude > wallVisibilityThreshold * wallVisibilityThreshold &&
                             visibility > wallVisibilityThreshold;

            if (!isVisible)
            {
                ClearWallMesh(runtime);
                SetWallVisible(runtime, false);
                continue;
            }

            BuildWallMesh(runtime, wallSprite, baseSize, roofSize, baseRenderer, roofRenderer);
            SetWallVisible(runtime, true);
        }
    }

    private void BuildWallMesh(
        WallRuntime runtime,
        Sprite sprite,
        Vector2 baseSize,
        Vector2 roofSize,
        SpriteRenderer baseRenderer,
        SpriteRenderer roofRenderer)
    {
        if (runtime == null || runtime.mesh == null || sprite == null)
            return;

        Vector3 baseStart;
        Vector3 baseEnd;
        Vector3 roofStart;
        Vector3 roofEnd;
        GetWallEdgePoints(runtime.side, baseInitialLocalPosition, baseSize, roofVisual.localPosition, roofSize, out baseStart, out baseEnd, out roofStart, out roofEnd);

        Vector3 sideNormal = GetSideNormal(runtime.side);
        baseStart += sideNormal * wallInset;
        baseEnd += sideNormal * wallInset;
        roofStart += sideNormal * wallInset;
        roofEnd += sideNormal * wallInset;

        int quadCount = Mathf.Max(1, floorCount);
        int vertexCount = quadCount * 4;
        int indexCount = quadCount * 6;

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[indexCount];

        Vector4 uvRect = GetSpriteUvRect(sprite);
        Vector3 meshDepth = Vector3.forward * wallDepthOffset;
        for (int floorIndex = 0; floorIndex < quadCount; floorIndex++)
        {
            float t0 = floorIndex / (float)quadCount;
            float t1 = (floorIndex + 1) / (float)quadCount;

            Vector3 v0 = Vector3.Lerp(baseStart, roofStart, t0) + meshDepth;
            Vector3 v1 = Vector3.Lerp(baseEnd, roofEnd, t0) + meshDepth;
            Vector3 v2 = Vector3.Lerp(baseEnd, roofEnd, t1) + meshDepth;
            Vector3 v3 = Vector3.Lerp(baseStart, roofStart, t1) + meshDepth;

            int vertexIndex = floorIndex * 4;
            vertices[vertexIndex + 0] = v0;
            vertices[vertexIndex + 1] = v1;
            vertices[vertexIndex + 2] = v2;
            vertices[vertexIndex + 3] = v3;

            float uMin = uvRect.x;
            float uMax = uvRect.z;
            float vMin = uvRect.y;
            float vMax = uvRect.w;

            uvs[vertexIndex + 0] = new Vector2(uMin, vMin);
            uvs[vertexIndex + 1] = new Vector2(uMax, vMin);
            uvs[vertexIndex + 2] = new Vector2(uMax, vMax);
            uvs[vertexIndex + 3] = new Vector2(uMin, vMax);

            int triangleIndex = floorIndex * 6;
            triangles[triangleIndex + 0] = vertexIndex + 0;
            triangles[triangleIndex + 1] = vertexIndex + 2;
            triangles[triangleIndex + 2] = vertexIndex + 1;
            triangles[triangleIndex + 3] = vertexIndex + 0;
            triangles[triangleIndex + 4] = vertexIndex + 3;
            triangles[triangleIndex + 5] = vertexIndex + 2;
        }

        runtime.mesh.Clear();
        runtime.mesh.vertices = vertices;
        runtime.mesh.uv = uvs;
        runtime.mesh.triangles = triangles;
        runtime.mesh.RecalculateBounds();
        runtime.mesh.RecalculateNormals();

        runtime.meshRenderer.sortingOrder = Mathf.Min(baseRenderer.sortingOrder, roofRenderer.sortingOrder);
        propertyBlock.Clear();
        propertyBlock.SetTexture("_MainTex", sprite.texture);
        propertyBlock.SetColor("_Color", wallColor);
        runtime.meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private void GetWallEdgePoints(
        WallSide side,
        Vector3 baseCenter,
        Vector2 baseSize,
        Vector3 roofCenter,
        Vector2 roofSize,
        out Vector3 baseStart,
        out Vector3 baseEnd,
        out Vector3 roofStart,
        out Vector3 roofEnd)
    {
        switch (side)
        {
            case WallSide.North:
                baseStart = baseCenter + new Vector3(-baseSize.x * 0.5f, baseSize.y * 0.5f, 0f);
                baseEnd = baseCenter + new Vector3(baseSize.x * 0.5f, baseSize.y * 0.5f, 0f);
                roofStart = roofCenter + new Vector3(-roofSize.x * 0.5f, roofSize.y * 0.5f, 0f);
                roofEnd = roofCenter + new Vector3(roofSize.x * 0.5f, roofSize.y * 0.5f, 0f);
                break;

            case WallSide.East:
                baseStart = baseCenter + new Vector3(baseSize.x * 0.5f, -baseSize.y * 0.5f, 0f);
                baseEnd = baseCenter + new Vector3(baseSize.x * 0.5f, baseSize.y * 0.5f, 0f);
                roofStart = roofCenter + new Vector3(roofSize.x * 0.5f, -roofSize.y * 0.5f, 0f);
                roofEnd = roofCenter + new Vector3(roofSize.x * 0.5f, roofSize.y * 0.5f, 0f);
                break;

            case WallSide.South:
                baseStart = baseCenter + new Vector3(baseSize.x * 0.5f, -baseSize.y * 0.5f, 0f);
                baseEnd = baseCenter + new Vector3(-baseSize.x * 0.5f, -baseSize.y * 0.5f, 0f);
                roofStart = roofCenter + new Vector3(roofSize.x * 0.5f, -roofSize.y * 0.5f, 0f);
                roofEnd = roofCenter + new Vector3(-roofSize.x * 0.5f, -roofSize.y * 0.5f, 0f);
                break;

            default:
                baseStart = baseCenter + new Vector3(-baseSize.x * 0.5f, baseSize.y * 0.5f, 0f);
                baseEnd = baseCenter + new Vector3(-baseSize.x * 0.5f, -baseSize.y * 0.5f, 0f);
                roofStart = roofCenter + new Vector3(-roofSize.x * 0.5f, roofSize.y * 0.5f, 0f);
                roofEnd = roofCenter + new Vector3(-roofSize.x * 0.5f, -roofSize.y * 0.5f, 0f);
                break;
        }
    }

    private Vector4 GetSpriteUvRect(Sprite sprite)
    {
        Rect textureRect = sprite.textureRect;
        Texture texture = sprite.texture;

        float uMin = textureRect.xMin / texture.width;
        float vMin = textureRect.yMin / texture.height;
        float uMax = textureRect.xMax / texture.width;
        float vMax = textureRect.yMax / texture.height;

        return new Vector4(uMin, vMin, uMax, vMax);
    }

    private void ClearWallMesh(WallRuntime runtime)
    {
        if (runtime == null || runtime.mesh == null)
            return;

        runtime.mesh.Clear();
    }

    private void SetWallVisible(WallRuntime runtime, bool isVisible)
    {
        if (runtime == null || runtime.meshRenderer == null)
            return;

        runtime.meshRenderer.enabled = isVisible;
    }

    private void SetWallsVisible(bool isVisible)
    {
        for (int i = 0; i < wallRuntimes.Count; i++)
            SetWallVisible(wallRuntimes[i], isVisible);
    }

    private Vector2 GetLocalSpriteSize(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return Vector2.one;

        Vector2 spriteSize = renderer.sprite.bounds.size;
        Vector3 scale = renderer.transform.localScale;
        return new Vector2(spriteSize.x * Mathf.Abs(scale.x), spriteSize.y * Mathf.Abs(scale.y));
    }

    private Vector3 GetSideNormal(WallSide side)
    {
        switch (side)
        {
            case WallSide.North:
                return Vector3.up;

            case WallSide.East:
                return Vector3.right;

            case WallSide.South:
                return Vector3.down;

            default:
                return Vector3.left;
        }
    }

    private Sprite GetWallSprite(WallSide side)
    {
        switch (side)
        {
            case WallSide.North:
                return northWallSprite;

            case WallSide.East:
                return eastWallSprite;

            case WallSide.South:
                return southWallSprite;

            default:
                return westWallSprite;
        }
    }

    private void ResetVisualsImmediate()
    {
        if (baseVisual != null)
            baseVisual.localPosition = baseInitialLocalPosition;

        if (roofVisual != null)
            roofVisual.localPosition = roofInitialLocalPosition;

        SetWallsVisible(false);
    }
}
