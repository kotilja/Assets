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
    [SerializeField] private float worldSquareSize = 10000f;

    private LineRenderer primaryRenderer;
    private LineRenderer secondaryRenderer;
    private LineRenderer tertiaryRenderer;
    private Material previewMaterial;
    private readonly List<LineRenderer> connectionRenderers = new List<LineRenderer>();
    private readonly List<LineRenderer> signalEditorRenderers = new List<LineRenderer>();

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
            case RoadBuildToolV2.ToolMode.None:
                break;

            case RoadBuildToolV2.ToolMode.DrawRoad:
                Color drawRoadColor = buildTool.IsCurrentRoadPreviewValid(previewWorldPosition)
                    ? buildTool.PreviewColor
                    : buildTool.InvalidPreviewColor;

                DrawPoint(primaryRenderer, previewWorldPosition, drawRoadColor, pointRadius, lineWidth);

                if (buildTool.HasCurrentStartNode)
                {
                    DrawLine(secondaryRenderer, buildTool.CurrentStartPosition, previewWorldPosition, drawRoadColor, lineWidth);
                    DrawCircle(tertiaryRenderer, buildTool.CurrentStartPosition, buildTool.SnapDistance, drawRoadColor, circleLineWidth);
                }
                break;

            case RoadBuildToolV2.ToolMode.DrawCurveRoad:
                Color drawCurveColor = buildTool.IsCurrentRoadPreviewValid(previewWorldPosition)
                    ? buildTool.PreviewColor
                    : buildTool.InvalidPreviewColor;

                DrawPoint(primaryRenderer, previewWorldPosition, drawCurveColor, pointRadius, lineWidth);

                if (buildTool.HasCurrentStartNode && !buildTool.HasCurveControlPoint)
                {
                    DrawLine(secondaryRenderer, buildTool.CurrentStartPosition, previewWorldPosition, drawCurveColor, lineWidth);
                    DrawCircle(tertiaryRenderer, buildTool.CurrentStartPosition, buildTool.SnapDistance, drawCurveColor, circleLineWidth);
                }
                else if (buildTool.HasCurrentStartNode && buildTool.HasCurveControlPoint)
                {
                    DrawQuadraticCurve(
                        primaryRenderer,
                        buildTool.CurrentStartPosition,
                        buildTool.CurrentCurveControlPoint,
                        previewWorldPosition,
                        drawCurveColor,
                        lineWidth,
                        curveSamples
                    );

                    DrawLine(secondaryRenderer, buildTool.CurrentStartPosition, buildTool.CurrentCurveControlPoint, drawCurveColor, circleLineWidth);
                    DrawLine(tertiaryRenderer, buildTool.CurrentCurveControlPoint, previewWorldPosition, drawCurveColor, circleLineWidth);
                }
                break;

            case RoadBuildToolV2.ToolMode.DrawPedestrianPath:
                Color pedestrianPathColor = buildTool.IsPedestrianPathPreviewValid(previewWorldPosition)
                    ? buildTool.PedestrianPathPreviewColor
                    : buildTool.InvalidPreviewColor;

                DrawPoint(primaryRenderer, previewWorldPosition, pedestrianPathColor, pointRadius, lineWidth);

                if (buildTool.HasPedestrianPathStart)
                    DrawLine(secondaryRenderer, buildTool.CurrentPedestrianPathStart, previewWorldPosition, pedestrianPathColor, lineWidth);
                break;

            case RoadBuildToolV2.ToolMode.PlaceHome:
                DrawBuildingPlacementPreview(previewWorldPosition, BuildingZoneV2.BuildingType.Home, buildTool.HomePreviewColor);
                break;

            case RoadBuildToolV2.ToolMode.PlaceOffice:
                DrawBuildingPlacementPreview(previewWorldPosition, BuildingZoneV2.BuildingType.Office, buildTool.OfficePreviewColor);
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
                DrawPoint(primaryRenderer, rawWorldPosition, buildTool.JunctionPreviewColor, pointRadius, lineWidth);
                DrawCircle(secondaryRenderer, rawWorldPosition, buildTool.JunctionPickDistance, buildTool.JunctionPreviewColor, circleLineWidth);
                break;

            case RoadBuildToolV2.ToolMode.JunctionSignals:
                DrawPoint(primaryRenderer, rawWorldPosition, buildTool.JunctionPreviewColor, pointRadius, lineWidth);
                DrawCircle(secondaryRenderer, rawWorldPosition, buildTool.JunctionPickDistance, buildTool.JunctionPreviewColor, circleLineWidth);
                DrawSignalPhaseEditor();
                break;

            case RoadBuildToolV2.ToolMode.JunctionTurns:
                DrawPoint(primaryRenderer, rawWorldPosition, buildTool.TurnEditPreviewColor, pointRadius, lineWidth);
                DrawCircle(secondaryRenderer, rawWorldPosition, buildTool.ApproachPickDistance, buildTool.TurnEditPreviewColor, circleLineWidth);
                break;

            case RoadBuildToolV2.ToolMode.LaneConnections:
                DrawPoint(primaryRenderer, rawWorldPosition, buildTool.TurnEditPreviewColor, pointRadius, lineWidth);
                DrawCircle(secondaryRenderer, rawWorldPosition, buildTool.LanePickDistance, buildTool.TurnEditPreviewColor, circleLineWidth);
                DrawSelectedLaneConnectionCurves();
                break;
        }
    }

    private void DrawSelectedLaneConnectionCurves()
    {
        if (buildTool == null || buildTool.Network == null)
            return;

        RoadNodeV2 node = buildTool.SelectedLaneConnectionNode;
        if (node == null && buildTool.SelectedFromLane != null)
            node = buildTool.SelectedFromLane.toNode;

        if (node == null)
            return;

        int rendererIndex = 0;
        IReadOnlyList<RoadLaneConnectionV2> allConnections = buildTool.Network.AllConnections;

        for (int i = 0; i < allConnections.Count; i++)
        {
            RoadLaneConnectionV2 connection = allConnections[i];
            if (connection == null || !connection.IsValid)
                continue;

            if (connection.connectionKind != RoadLaneConnectionV2.ConnectionKind.Junction)
                continue;

            if (connection.junctionNode != node)
                continue;

            LineRenderer renderer = GetConnectionRenderer(rendererIndex++);
            DrawConnectionCurve(renderer, connection, GetConnectionColor(connection), lineWidth);
        }
    }

    private void DrawSignalPhaseEditor()
    {
        if (buildTool == null || buildTool.SelectedSignal == null)
            return;

        int rendererIndex = 0;
        List<RoadSegmentV2> incomingSegments = buildTool.SelectedSignalIncomingSegments;

        for (int i = 0; i < incomingSegments.Count; i++)
        {
            RoadSegmentV2 segment = incomingSegments[i];
            if (segment == null)
                continue;

            rendererIndex = DrawSignalMovementCircle(rendererIndex, segment, RoadLaneConnectionV2.MovementType.Left);
            rendererIndex = DrawSignalMovementCircle(rendererIndex, segment, RoadLaneConnectionV2.MovementType.Straight);
            rendererIndex = DrawSignalMovementCircle(rendererIndex, segment, RoadLaneConnectionV2.MovementType.Right);
        }
    }

    private int DrawSignalMovementCircle(int rendererIndex, RoadSegmentV2 segment, RoadLaneConnectionV2.MovementType movementType)
    {
        if (!buildTool.TryGetSignalMovementEditorPosition(segment, movementType, out Vector3 position))
            return rendererIndex;

        RoadNodeSignalV2.LampState state = buildTool.GetSignalMovementState(segment, movementType);
        Color color = GetSignalStateColor(state);
        LineRenderer renderer = GetSignalEditorRenderer(rendererIndex++);
        DrawCircle(renderer, position, buildTool.SignalEditorPickRadius, color, circleLineWidth * 1.35f);
        return rendererIndex;
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

    private void DrawRotatedRectangle(LineRenderer renderer, Vector3 center, Vector2 size, float rotationDegrees, Color color, float width)
    {
        if (renderer == null)
            return;

        Vector3 half = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
        Quaternion rotation = Quaternion.Euler(0f, 0f, rotationDegrees);

        Vector3 p0 = center + rotation * new Vector3(-half.x, -half.y, 0f);
        Vector3 p1 = center + rotation * new Vector3(half.x, -half.y, 0f);
        Vector3 p2 = center + rotation * new Vector3(half.x, half.y, 0f);
        Vector3 p3 = center + rotation * new Vector3(-half.x, half.y, 0f);

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

    private void DrawBuildingPlacementPreview(Vector3 previewWorldPosition, BuildingZoneV2.BuildingType buildingType, Color color)
    {
        if (buildTool == null)
            return;

        Color previewColor = buildTool.IsBuildingPlacementValid(buildingType, previewWorldPosition)
            ? color
            : buildTool.InvalidPreviewColor;

        if (!buildTool.TryGetBuildingPlacementPose(
            buildingType,
            previewWorldPosition,
            out Vector3 rootPosition,
            out float rotationDegrees,
            out Vector2 size,
            out Vector3 centerOffset))
            return;

        Quaternion rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        Vector3 footprintCenter = rootPosition + rotation * centerOffset;

        DrawPoint(primaryRenderer, rootPosition, previewColor, pointRadius, lineWidth);
        DrawRotatedRectangle(secondaryRenderer, footprintCenter, size, rotationDegrees, previewColor, lineWidth);
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

    private void DrawConnectionCurve(LineRenderer renderer, RoadLaneConnectionV2 connection, Color color, float width)
    {
        if (renderer == null || connection == null)
            return;

        List<Vector3> points = connection.curvePoints;
        if (points == null || points.Count < 2)
            return;

        renderer.enabled = true;
        renderer.loop = false;
        renderer.positionCount = points.Count;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.startColor = color;
        renderer.endColor = color;

        for (int i = 0; i < points.Count; i++)
            renderer.SetPosition(i, WithOverlayZ(points[i]));
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
        worldPoint = ClampToWorldBounds(worldPoint);
        return true;
    }

    private Vector3 ClampToWorldBounds(Vector3 worldPoint)
    {
        float halfWorldSize = Mathf.Max(1f, worldSquareSize * 0.5f);
        worldPoint.x = Mathf.Clamp(worldPoint.x, -halfWorldSize, halfWorldSize);
        worldPoint.y = Mathf.Clamp(worldPoint.y, -halfWorldSize, halfWorldSize);
        worldPoint.z = 0f;
        return worldPoint;
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

    private LineRenderer GetConnectionRenderer(int index)
    {
        while (connectionRenderers.Count <= index)
        {
            string objectName = $"LaneConnectionPreview_{connectionRenderers.Count}";
            LineRenderer renderer = EnsureRenderer(objectName, 10 + connectionRenderers.Count);
            connectionRenderers.Add(renderer);
        }

        ConfigureRenderer(connectionRenderers[index], 10 + index);
        return connectionRenderers[index];
    }

    private LineRenderer GetSignalEditorRenderer(int index)
    {
        while (signalEditorRenderers.Count <= index)
        {
            string objectName = $"SignalEditorPreview_{signalEditorRenderers.Count}";
            LineRenderer renderer = EnsureRenderer(objectName, 40 + signalEditorRenderers.Count);
            signalEditorRenderers.Add(renderer);
        }

        ConfigureRenderer(signalEditorRenderers[index], 40 + index);
        return signalEditorRenderers[index];
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

        for (int i = 0; i < connectionRenderers.Count; i++)
        {
            if (connectionRenderers[i] != null)
                connectionRenderers[i].enabled = false;
        }

        for (int i = 0; i < signalEditorRenderers.Count; i++)
        {
            if (signalEditorRenderers[i] != null)
                signalEditorRenderers[i].enabled = false;
        }
    }

    private Color GetConnectionColor(RoadLaneConnectionV2 connection)
    {
        if (connection == null)
            return Color.magenta;

        if (buildTool != null && buildTool.SelectedFromLane != null && connection.fromLane == buildTool.SelectedFromLane)
            return Color.white;

        if (connection.isManual)
            return new Color(1f, 0.45f, 0.85f, 1f);

        switch (connection.movementType)
        {
            case RoadLaneConnectionV2.MovementType.Straight:
                return Color.green;

            case RoadLaneConnectionV2.MovementType.Left:
                return Color.yellow;

            case RoadLaneConnectionV2.MovementType.Right:
                return Color.cyan;

            case RoadLaneConnectionV2.MovementType.UTurn:
                return new Color(1f, 0.5f, 0.2f, 1f);
        }

        return Color.magenta;
    }

    private Color GetSignalStateColor(RoadNodeSignalV2.LampState state)
    {
        switch (state)
        {
            case RoadNodeSignalV2.LampState.Green:
                return Color.green;

            case RoadNodeSignalV2.LampState.Yellow:
                return Color.yellow;

            default:
                return Color.red;
        }
    }
}
