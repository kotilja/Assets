using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoadBuildToolV2))]
public class RoadBuildToolV2Editor : Editor
{
    private RoadBuildToolV2 Tool => (RoadBuildToolV2)target;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        DrawModeButtons();

        EditorGUILayout.Space(10f);
        EditorGUILayout.HelpBox(GetHelpText(), MessageType.Info);

        if (Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.JunctionTurns)
            DrawTurnEditorPanel();

        if (Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.LaneConnections)
            DrawLaneConnectionsPanel();

        if (GUILayout.Button("Сбросить текущую цепочку"))
        {
            Tool.ClearCurrentChain();
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Сбросить выбор подъезда"))
        {
            Tool.ClearTurnSelection();
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Обновить визуал дорог"))
        {
            Tool.RefreshNetworkVisuals();
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }
    }

    private void DrawModeButtons()
    {
        EditorGUILayout.LabelField("Быстрое переключение режима", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Рисование"))
        {
            Tool.SetToolMode(RoadBuildToolV2.ToolMode.DrawRoad);
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Удаление"))
        {
            Tool.SetToolMode(RoadBuildToolV2.ToolMode.DeleteRoad);
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Перекрестки"))
        {
            Tool.SetToolMode(RoadBuildToolV2.ToolMode.JunctionControl);
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Манёвры"))
        {
            Tool.SetToolMode(RoadBuildToolV2.ToolMode.JunctionTurns);
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Связи полос"))
        {
            Tool.SetToolMode(RoadBuildToolV2.ToolMode.LaneConnections);
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }


    }

    private void DrawTurnEditorPanel()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Редактор манёвров подъезда", EditorStyles.boldLabel);

        if (Tool.SelectedTurnNode == null)
        {
            EditorGUILayout.HelpBox("Выбери перекресток и входящий подъезд кликом в Scene View.", MessageType.Info);
            return;
        }

        EditorGUILayout.ObjectField("Узел", Tool.SelectedTurnNode, typeof(RoadNodeV2), true);
        EditorGUILayout.ObjectField("Входящий сегмент", Tool.SelectedIncomingSegment, typeof(RoadSegmentV2), true);

        if (Tool.SelectedIncomingSegment == null)
        {
            EditorGUILayout.HelpBox("Кликни ближе к нужному входящему подъезду.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();

        DrawMovementToggleButton("← Налево", RoadLaneConnectionV2.MovementType.Left);
        DrawMovementToggleButton("↑ Прямо", RoadLaneConnectionV2.MovementType.Straight);
        DrawMovementToggleButton("→ Направо", RoadLaneConnectionV2.MovementType.Right);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawLaneConnectionsPanel()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Редактор ручных связей полос", EditorStyles.boldLabel);

        EditorGUILayout.LabelField(
            "Входящая полоса",
            Tool.SelectedFromLane != null
                ? GetLaneLabel(Tool.SelectedFromLane)
                : "не выбрана"
        );

        EditorGUILayout.LabelField(
            "Выходящая полоса",
            Tool.SelectedToLane != null
                ? GetLaneLabel(Tool.SelectedToLane)
                : "не выбрана"
        );

        if (Tool.SelectedFromLane == null)
        {
            EditorGUILayout.HelpBox("Кликни по входящей полосе перед перекрестком.", MessageType.Info);
            return;
        }

        if (Tool.SelectedToLane == null)
        {
            EditorGUILayout.HelpBox("Теперь кликни по выходящей полосе после перекрестка.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                Tool.SelectedManualConnectionExists()
                    ? "Ручная связь существует. Повторный клик по этой выходящей полосе удалит ее."
                    : "Ручной связи нет. Клик по этой выходящей полосе создаст ее.",
                MessageType.None
            );
        }

        if (Tool.SelectedFromLaneHasManualConnections())
        {
            if (GUILayout.Button("Очистить все ручные связи у выбранной входящей полосы"))
            {
                Tool.ClearManualConnectionsForSelectedLane();
                EditorUtility.SetDirty(Tool);
                EditorUtility.SetDirty(Tool.Network);
                SceneView.RepaintAll();
            }
        }

        if (GUILayout.Button("Сбросить выбор полос"))
        {
            Tool.ClearLaneConnectionSelection();
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }
    }

    private void DrawMovementToggleButton(string label, RoadLaneConnectionV2.MovementType movementType)
    {
        bool allowed = Tool.GetSelectedApproachMovementAllowed(movementType);
        Color oldColor = GUI.backgroundColor;
        GUI.backgroundColor = allowed ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);

        if (GUILayout.Button(label, GUILayout.Height(28f)))
        {
            Tool.ToggleSelectedApproachMovement(movementType);
            EditorUtility.SetDirty(Tool);
            EditorUtility.SetDirty(Tool.Network);
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = oldColor;
    }

    private string GetHelpText()
    {
        switch (Tool.CurrentToolMode)
        {
            case RoadBuildToolV2.ToolMode.DrawRoad:
                return "Режим рисования: ЛКМ ставит узел или создает сегмент. Esc завершает текущую цепочку.";

            case RoadBuildToolV2.ToolMode.DeleteRoad:
                return "Режим удаления: ЛКМ удаляет ближайший сегмент дороги.";

            case RoadBuildToolV2.ToolMode.JunctionControl:
                return "Режим перекрестков: ЛКМ по существующему перекрестку переключает режим RightHandRule / TrafficLight.";

            case RoadBuildToolV2.ToolMode.JunctionTurns:
                return "Режим манёвров: ЛКМ по перекрестку и ближе к нужному подъезду выбирает входящий сегмент. В инспекторе включаются и выключаются ← ↑ →.";
            case RoadBuildToolV2.ToolMode.LaneConnections:
                return "Режим ручных связей полос: сначала ЛКМ по входящей полосе перед перекрестком, потом ЛКМ по выходящей полосе после перекрестка. Повторный клик по той же выходящей полосе снимает связь.";

            default:
                return "-";
        }
    }

    private void OnSceneGUI()
    {
        if (!Tool.ToolEnabled || Tool.Network == null)
            return;

        Event e = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(controlId);

        Vector3 rawWorldPosition = GetWorldPointOnPlane(e.mousePosition);
        Vector3 worldPosition = Tool.GetPreviewWorldPosition(rawWorldPosition);

        DrawPreview(worldPosition);
        DrawDrawAssistOverlay(rawWorldPosition, worldPosition);
        DrawTurnSelectionOverlay();
        DrawLaneConnectionOverlay();

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Undo.IncrementCurrentGroup();

            Tool.HandleSceneClick(worldPosition);

            EditorUtility.SetDirty(Tool);
            EditorUtility.SetDirty(Tool.Network);
            SceneView.RepaintAll();
            e.Use();
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            Tool.ClearCurrentChain();
            Tool.ClearTurnSelection();
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
            e.Use();
        }
    }

    private void DrawPreview(Vector3 worldPosition)
    {
        switch (Tool.CurrentToolMode)
        {
            case RoadBuildToolV2.ToolMode.DrawRoad:
                Handles.color = Tool.PreviewColor;
                Handles.DrawSolidDisc(worldPosition, Vector3.forward, 0.06f);

                if (Tool.HasCurrentStartNode)
                {
                    Handles.DrawDottedLine(Tool.CurrentStartPosition, worldPosition, 4f);
                    Handles.DrawWireDisc(Tool.CurrentStartPosition, Vector3.forward, Tool.SnapDistance);
                }
                break;

            case RoadBuildToolV2.ToolMode.DeleteRoad:
                Handles.color = Tool.DeletePreviewColor;
                Handles.DrawSolidDisc(worldPosition, Vector3.forward, 0.06f);
                Handles.DrawWireDisc(worldPosition, Vector3.forward, Tool.DeletePickDistance);
                break;

            case RoadBuildToolV2.ToolMode.JunctionControl:
                Handles.color = Tool.JunctionPreviewColor;
                Handles.DrawSolidDisc(worldPosition, Vector3.forward, 0.06f);
                Handles.DrawWireDisc(worldPosition, Vector3.forward, Tool.JunctionPickDistance);
                break;

            case RoadBuildToolV2.ToolMode.JunctionTurns:
                Handles.color = Tool.TurnEditPreviewColor;
                Handles.DrawSolidDisc(worldPosition, Vector3.forward, 0.06f);
                Handles.DrawWireDisc(worldPosition, Vector3.forward, Tool.ApproachPickDistance);
                break;

            case RoadBuildToolV2.ToolMode.LaneConnections:
                Handles.color = Tool.TurnEditPreviewColor;
                Handles.DrawSolidDisc(worldPosition, Vector3.forward, 0.06f);
                Handles.DrawWireDisc(worldPosition, Vector3.forward, Tool.LanePickDistance);
                 break;
        }
    }

    private void DrawDrawAssistOverlay(Vector3 rawWorldPosition, Vector3 snappedWorldPosition)
    {
        if (Tool.CurrentToolMode != RoadBuildToolV2.ToolMode.DrawRoad)
            return;

        if (!Tool.HasCurrentStartNode)
            return;

        if (Vector3.Distance(rawWorldPosition, snappedWorldPosition) < 0.001f)
            return;

        Handles.color = new Color(1f, 0.8f, 0.2f, 1f);
        Handles.DrawDottedLine(rawWorldPosition, snappedWorldPosition, 3f);
        Handles.DrawWireDisc(snappedWorldPosition, Vector3.forward, 0.08f);
    }

    private void DrawTurnSelectionOverlay()
    {
        if (Tool.CurrentToolMode != RoadBuildToolV2.ToolMode.JunctionTurns)
            return;

        if (Tool.SelectedTurnNode != null)
        {
            Handles.color = new Color(1f, 0.3f, 1f, 1f);
            Handles.DrawWireDisc(Tool.SelectedTurnNode.transform.position, Vector3.forward, 0.35f);
        }

        if (Tool.SelectedIncomingSegment != null && Tool.SelectedTurnNode != null)
        {
            Vector3 nodePos = Tool.SelectedTurnNode.transform.position;
            Vector3 otherPos = nodePos;

            if (Tool.SelectedIncomingSegment.EndNode == Tool.SelectedTurnNode && Tool.SelectedIncomingSegment.StartNode != null)
                otherPos = Tool.SelectedIncomingSegment.StartNode.transform.position;
            else if (Tool.SelectedIncomingSegment.StartNode == Tool.SelectedTurnNode && Tool.SelectedIncomingSegment.EndNode != null)
                otherPos = Tool.SelectedIncomingSegment.EndNode.transform.position;

            Vector3 dir = (nodePos - otherPos).normalized;
            Vector3 probe = nodePos - dir * 0.55f;

            Handles.color = new Color(1f, 0.2f, 1f, 1f);
            Handles.DrawLine(otherPos, nodePos);
            Handles.DrawSolidDisc(probe, Vector3.forward, 0.08f);

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = Color.white;

            Handles.Label(
                probe + new Vector3(0.08f, 0.08f, 0f),
                $"← {(Tool.GetSelectedApproachMovementAllowed(RoadLaneConnectionV2.MovementType.Left) ? "ON" : "OFF")}  " +
                $"↑ {(Tool.GetSelectedApproachMovementAllowed(RoadLaneConnectionV2.MovementType.Straight) ? "ON" : "OFF")}  " +
                $"→ {(Tool.GetSelectedApproachMovementAllowed(RoadLaneConnectionV2.MovementType.Right) ? "ON" : "OFF")}",
                style
            );
        }
    }


    private void DrawLaneConnectionOverlay()
    {
        if (Tool.CurrentToolMode != RoadBuildToolV2.ToolMode.LaneConnections)
            return;

        if (Tool.Network != null)
        {
            for (int i = 0; i < Tool.Network.AllConnections.Count; i++)
            {
                RoadLaneConnectionV2 connection = Tool.Network.AllConnections[i];
                if (connection == null || !connection.IsValid)
                    continue;

                if (connection.connectionKind != RoadLaneConnectionV2.ConnectionKind.Junction)
                    continue;

                if (!connection.isManual)
                    continue;

                bool isSelected =
                    Tool.SelectedFromLane != null &&
                    Tool.SelectedToLane != null &&
                    connection.fromLane == Tool.SelectedFromLane &&
                    connection.toLane == Tool.SelectedToLane;

                Handles.color = isSelected
                    ? new Color(0.2f, 1f, 0.2f, 1f)
                    : new Color(0.2f, 0.9f, 1f, 0.95f);

                Vector3[] points = GetConnectionDrawPoints(connection);
                if (points.Length >= 2)
                    Handles.DrawAAPolyLine(4f, points);

                if (points.Length > 0)
                {
                    Handles.DrawSolidDisc(points[0], Vector3.forward, 0.035f);
                    Handles.DrawSolidDisc(points[points.Length - 1], Vector3.forward, 0.035f);
                }
            }
        }

        if (Tool.SelectedFromLane != null)
        {
            Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
            Handles.DrawLine(Tool.SelectedFromLane.start, Tool.SelectedFromLane.end);
            Handles.DrawWireDisc(Tool.SelectedFromLane.MidPoint, Vector3.forward, 0.10f);
        }

        if (Tool.SelectedToLane != null)
        {
            Handles.color = Tool.SelectedManualConnectionExists()
                ? new Color(0.2f, 1f, 0.2f, 1f)
                : new Color(1f, 0.5f, 0.2f, 1f);

            Handles.DrawLine(Tool.SelectedToLane.start, Tool.SelectedToLane.end);
            Handles.DrawWireDisc(Tool.SelectedToLane.MidPoint, Vector3.forward, 0.10f);
        }
    }

    private Vector3[] GetConnectionDrawPoints(RoadLaneConnectionV2 connection)
    {
        if (connection == null)
            return new Vector3[0];

        if (connection.curvePoints != null && connection.curvePoints.Count >= 2)
        {
            Vector3[] result = new Vector3[connection.curvePoints.Count];

            for (int i = 0; i < connection.curvePoints.Count; i++)
            {
                Vector3 p = connection.curvePoints[i];
                p.z = 0f;
                result[i] = p;
            }

            return result;
        }

        if (connection.fromLane != null && connection.toLane != null)
        {
            return new[]
            {
            connection.fromLane.end,
            connection.toLane.start
        };
        }

        return new Vector3[0];
    }

    private string GetLaneLabel(RoadLaneDataV2 lane)
    {
        if (lane == null)
            return "null";

        string segmentName = lane.ownerSegment != null ? lane.ownerSegment.name : "no-segment";
        string fromName = lane.fromNode != null ? lane.fromNode.name : "null";
        string toName = lane.toNode != null ? lane.toNode.name : "null";

        return $"LaneId={lane.laneId}, {segmentName}, {fromName} -> {toName}, idx={lane.localLaneIndex}";
    }

    private Vector3 GetWorldPointOnPlane(Vector2 mousePosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane plane = new Plane(Vector3.forward, Vector3.zero);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 point = ray.GetPoint(enter);
            point.z = 0f;
            return point;
        }

        return Vector3.zero;
    }
}