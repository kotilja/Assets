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

        Vector3 worldPosition = GetWorldPointOnPlane(e.mousePosition);
        DrawPreview(worldPosition);
        DrawTurnSelectionOverlay();

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
        }
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