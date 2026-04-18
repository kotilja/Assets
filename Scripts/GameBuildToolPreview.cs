using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GameBuildToolPreview : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoadBuildToolV2 buildTool;
    [SerializeField] private Camera targetCamera;

    [Header("Visual")]
    [SerializeField] private float lineWidth = 0.06f;
    [SerializeField] private float circleLineWidth = 0.04f;
    [SerializeField] private float pointRadius = 0.08f;
    [SerializeField] private int curveSamples = 24;
    [SerializeField] private int circleSegments = 28;
    [SerializeField] private float overlayZ = -0.2f;

    private LineRenderer primaryRenderer;
    private LineRenderer secondaryRenderer;
    private LineRenderer tertiaryRenderer;
    private Material previewMaterial;

    private void Awake()
    {
        if (buildTool == null)
            buildTool = FindFirstObjectByType<RoadBuildToolV2>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        EnsureRenderers();
    }

    private void Update()
    {
        if (buildTool == null || !buildTool.ToolEnabled)
        {
            HideAll();
            return;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null || !TryGetMouseWorldPoint(out Vector3 rawWorldPosition))
        {
            HideAll();
            return;
        }

        Vector3 previewWorldPosition = buildTool.GetPreviewWorldPosition(rawWorldPosition);
        DrawPreview(rawWorldPosition, previewWorldPosition);
    }

    private void DrawPreview(Vector3 rawWorldPosition, Vector3 previewWorldPosition)
    {
        HideAll();

        switch (buildTool.CurrentToolMode)
        {
            case RoadBuildToolV2.ToolMode.DrawRoad:
                DrawPoint(primaryRenderer, previewWorldPosition, buildTool.PreviewColor, pointRadius, lineWidth);

                if (buildTool.HasCurrentStartNode)
                {
                    DrawLine(secondaryRenderer, buildTool.CurrentStartPosition, previewWorldPosition, buildTool.PreviewColor, lineWidth);
                    DrawCircle(tertiaryRenderer, buildTool.CurrentStartPosition, buildTool.SnapDistance, buildTool.PreviewColor, circleLineWidth);
                }
                break;

            case RoadBuildToolV2.ToolMode.DrawCurveRoad:
                DrawPoint(primaryRenderer, previewWorldPosition, buildTool.PreviewColor, pointRadius, lineWidth);

                if (buildTool.HasCurrentStartNode && !buildTool.HasCurveControlPoint)
                {
                    DrawLine(secondaryRenderer, buildTool.CurrentStartPosition, previewWorldPosition, buildTool.PreviewColor, lineWidth);
                    DrawCircle(tertiaryRenderer, buildTool.CurrentStartPosition, buildTool.SnapDistance, buildTool.PreviewColor, circleLineWidth);
                }
                else if (buildTool.HasCurrentStartNode && buildTool.HasCurveControlPoint)
                {
                    DrawQuadraticCurve(
                        primaryRenderer,
                        buildTool.CurrentStartPosition,
                        buildTool.CurrentCurveControlPoint,
                        previewWorldPosition,
                        buildTool.PreviewColor,
                        lineWidth,
                        curveSamples
                    );

                    DrawLine(secondaryRenderer, buildTool.CurrentStartPosition, buildTool.CurrentCurveControlPoint, buildTool.PreviewColor, circleLineWidth);
                    DrawLine(tertiaryRenderer, buildTool.CurrentCurveControlPoint, previewWorldPosition, buildTool.PreviewColor, circleLineWidth);
                }
                break;

            case RoadBuildToolV2.ToolMode.PlaceHome:
                DrawPoint(primaryRenderer, rawWorldPosition, buildTool.HomePreviewColor, pointRadius, lineWidth);
                if (buildTool.HasBuildingStartPoint)
                    DrawRectangle(secondaryRenderer, buildTool.CurrentBuildingStartPoint, rawWorldPosition, buildTool.HomePreviewColor, lineWidth);
                break;

            case RoadBuildToolV2.ToolMode.PlaceOffice:
                DrawPoint(primaryRenderer, rawWorldPosition, buildTool.OfficePreviewColor, pointRadius, lineWidth);
                if (buildTool.HasBuildingStartPoint)
                    DrawRectangle(secondaryRenderer, buildTool.CurrentBuildingStartPoint, rawWorldPosition, buildTool.OfficePreviewColor, lineWidth);
                break;

            case RoadBuildToolV2.ToolMode.ParkingSpot:
                DrawPoint(primaryRenderer, previewWorldPosition, buildTool.ParkingPreviewColor, pointRadius, lineWidth);
                DrawCircle(secondaryRenderer, previewWorldPosition, 0.2f, buildTool.ParkingPreviewColor, circleLineWidth);
                break;

            case RoadBuildToolV2.ToolMode.DeleteRoad:
                DrawPoint(primaryRenderer, rawWorldPosition, buildTool.DeletePreviewColor, pointRadius, lineWidth);
                DrawCircle(secondaryRenderer, rawWorldPosition, buildTool.DeletePickDistance, buildTool.DeletePreviewColor, circleLineWidth);
                break;

            case RoadBuildToolV2.ToolMode.JunctionControl:
            case RoadBuildToolV2.ToolMode.JunctionKeepClear:
            case RoadBuildToolV2.ToolMode.JunctionSignals:
                DrawPoint(primaryRenderer, rawWorldPosition, buildTool.JunctionPreviewColor, pointRadius, lineWidth);
                DrawCircle(secondaryRenderer, rawWorldPosition, buildTool.JunctionPickDistance, buildTool.JunctionPreviewColor, circleLineWidth);
                break;

            case RoadBuildToolV2.ToolMode.JunctionTurns:
                DrawPoint(primaryRenderer, rawWorldPosition, buildTool.TurnEditPreviewColor, pointRadius, lineWidth);
                DrawCircle(secondaryRenderer, rawWorldPosition, buildTool.ApproachPickDistance, buildTool.TurnEditPreviewColor, circleLineWidth);
                break;

            case RoadBuildToolV2.ToolMode.LaneConnections:
                DrawPoint(primaryRenderer, rawWorldPosition, buildTool.TurnEditPreviewColor, pointRadius, lineWidth);
                DrawCircle(secondaryRenderer, rawWorldPosition, buildTool.LanePickDistance, buildTool.TurnEditPreviewColor, circleLineWidth);
                break;
        }
    }

    private void DrawLine(LineRenderer renderer, Vector3 a, Vector3 b, Color color, float width)
    {
        if (renderer == null)
            return;

        renderer.enabled = true;
        renderer.loop = false;
        renderer.positionCount = 2;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.SetPosition(0, WithOverlayZ(a));
        renderer.SetPosition(1, WithOverlayZ(b));
    }

    private void DrawPoint(LineRenderer renderer, Vector3 center, Color color, float radius, float width)
    {
        DrawCircle(renderer, center, radius, color, width);
    }

    private void DrawCircle(LineRenderer renderer, Vector3 center, float radius, Color color, float width)
    {
        if (renderer == null)
            return;

        renderer.enabled = true;
        renderer.loop = true;
        renderer.positionCount = circleSegments;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.startColor = color;
        renderer.endColor = color;

        for (int i = 0; i < circleSegments; i++)
        {
            float angle = i / (float)circleSegments * Mathf.PI * 2f;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            renderer.SetPosition(i, WithOverlayZ(point));
        }
    }

    private void DrawRectangle(LineRenderer renderer, Vector3 a, Vector3 b, Color color, float width)
    {
        if (renderer == null)
            return;

        Vector3 p0 = new Vector3(a.x, a.y, 0f);
        Vector3 p1 = new Vector3(b.x, a.y, 0f);
        Vector3 p2 = new Vector3(b.x, b.y, 0f);
        Vector3 p3 = new Vector3(a.x, b.y, 0f);

        renderer.enabled = true;
        renderer.loop = true;
        renderer.positionCount = 4;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.SetPosition(0, WithOverlayZ(p0));
        renderer.SetPosition(1, WithOverlayZ(p1));
        renderer.SetPosition(2, WithOverlayZ(p2));
        renderer.SetPosition(3, WithOverlayZ(p3));
    }

    private void DrawQuadraticCurve(
        LineRenderer renderer,
        Vector3 start,
        Vector3 control,
        Vector3 end,
        Color color,
        float width,
        int samples)
    {
        if (renderer == null)
            return;

        renderer.enabled = true;
        renderer.loop = false;
        renderer.positionCount = Mathf.Max(2, samples + 1);
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.startColor = color;
        renderer.endColor = color;

        int pointCount = renderer.positionCount;
        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            float u = 1f - t;
            Vector3 point = u * u * start + 2f * u * t * control + t * t * end;
            renderer.SetPosition(i, WithOverlayZ(point));
        }
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        if (targetCamera == null)
            return false;

        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);

        if (!plane.Raycast(ray, out float enter))
            return false;

        worldPoint = ray.GetPoint(enter);
        worldPoint.z = 0f;
        return true;
    }

    private Vector3 WithOverlayZ(Vector3 point)
    {
        return new Vector3(point.x, point.y, overlayZ);
    }

    private void EnsureRenderers()
    {
        primaryRenderer = EnsureRenderer("PrimaryPreview", 0);
        secondaryRenderer = EnsureRenderer("SecondaryPreview", 1);
        tertiaryRenderer = EnsureRenderer("TertiaryPreview", 2);
    }

    private LineRenderer EnsureRenderer(string objectName, int sortingOrder)
    {
        Transform existing = transform.Find(objectName);
        LineRenderer renderer = existing != null ? existing.GetComponent<LineRenderer>() : null;

        if (renderer == null)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            renderer = go.AddComponent<LineRenderer>();
        }

        ConfigureRenderer(renderer, sortingOrder);
        return renderer;
    }

    private void ConfigureRenderer(LineRenderer renderer, int sortingOrder)
    {
        if (renderer == null)
            return;

        if (previewMaterial == null)
            previewMaterial = new Material(Shader.Find("Sprites/Default"));

        renderer.sharedMaterial = previewMaterial;
        renderer.useWorldSpace = true;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.alignment = LineAlignment.TransformZ;
        renderer.numCapVertices = 4;
        renderer.numCornerVertices = 4;
        renderer.sortingOrder = 200 + sortingOrder;
        renderer.enabled = false;
    }

    private void HideAll()
    {
        if (primaryRenderer != null)
            primaryRenderer.enabled = false;

        if (secondaryRenderer != null)
            secondaryRenderer.enabled = false;

        if (tertiaryRenderer != null)
            tertiaryRenderer.enabled = false;
    }
}
