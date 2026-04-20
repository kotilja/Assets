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

        if (Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.JunctionSignals)
            DrawSignalEditorPanel();

        if (Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.LaneConnections)
            DrawLaneConnectionsPanel();

        if (Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.ParkingSpot)
            DrawParkingEditorPanel();

        if (Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.PlaceHome ||
            Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.PlaceOffice)
            DrawBuildingPrefabPanel();

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
        DrawModeButtonRow(
            ("Прямая", RoadBuildToolV2.ToolMode.DrawRoad),
            ("Кривая", RoadBuildToolV2.ToolMode.DrawCurveRoad),
            ("Пешеходная", RoadBuildToolV2.ToolMode.DrawPedestrianPath)
        );

        DrawModeButtonRow(
            ("Home", RoadBuildToolV2.ToolMode.PlaceHome)
        );

        DrawModeButtonRow(
            ("Office", RoadBuildToolV2.ToolMode.PlaceOffice),
            ("Parking", RoadBuildToolV2.ToolMode.ParkingSpot),
            ("Удаление", RoadBuildToolV2.ToolMode.DeleteRoad),
            ("Перекрестки", RoadBuildToolV2.ToolMode.JunctionControl)
        );

        DrawModeButtonRow(
            ("KeepClear", RoadBuildToolV2.ToolMode.JunctionKeepClear),
            ("Фазы", RoadBuildToolV2.ToolMode.JunctionSignals),
            ("Манёвры", RoadBuildToolV2.ToolMode.JunctionTurns)
        );

        DrawModeButtonRow(
            ("Связи полос", RoadBuildToolV2.ToolMode.LaneConnections)
        );
    }

    private void DrawModeButtonRow(params (string label, RoadBuildToolV2.ToolMode mode)[] buttons)
    {
        if (buttons == null || buttons.Length == 0)
            return;

        for (int rowStart = 0; rowStart < buttons.Length; rowStart += 3)
        {
            EditorGUILayout.BeginHorizontal();

            int count = Mathf.Min(3, buttons.Length - rowStart);

            for (int i = 0; i < count; i++)
            {
                int index = rowStart + i;
                if (GUILayout.Button(buttons[index].label))
                {
                    Tool.SetToolMode(buttons[index].mode);
                    EditorUtility.SetDirty(Tool);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.EndHorizontal();
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

    private void DrawSignalEditorPanel()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Редактор фаз светофора", EditorStyles.boldLabel);

        if (Tool.SelectedSignalNode == null || Tool.SelectedSignal == null)
        {
            EditorGUILayout.HelpBox("Кликни по светофорному перекрестку в Scene View.", MessageType.Info);
            return;
        }

        EditorGUILayout.ObjectField("Узел", Tool.SelectedSignalNode, typeof(RoadNodeV2), true);

        EditorGUILayout.LabelField("Текущая фаза", Tool.SelectedSignal.GetCurrentPhaseLabel());
        EditorGUILayout.LabelField("Количество фаз", Tool.SelectedSignal.GetPhaseCount().ToString());

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("◀ Пред. фаза"))
        {
            Tool.SelectPreviousSignalPhase();
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("След. фаза ▶"))
        {
            Tool.SelectNextSignalPhase();
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("+ Фаза"))
        {
            Tool.AddSignalPhase();
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("- Фаза"))
        {
            Tool.RemoveSignalPhase();
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.ObjectField("Входящий сегмент", Tool.SelectedSignalIncomingSegment, typeof(RoadSegmentV2), true);

        if (Tool.SelectedSignalIncomingSegment == null)
        {
            EditorGUILayout.HelpBox("Кликни ближе к нужному входящему сегменту перекрестка.", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginHorizontal();

        DrawSignalMovementToggleButton("← Налево", RoadLaneConnectionV2.MovementType.Left);
        DrawSignalMovementToggleButton("↑ Прямо", RoadLaneConnectionV2.MovementType.Straight);
        DrawSignalMovementToggleButton("→ Направо", RoadLaneConnectionV2.MovementType.Right);

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Сбросить выбор сигнала"))
        {
            Tool.ClearSignalSelection();
            EditorUtility.SetDirty(Tool);
            SceneView.RepaintAll();
        }
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

    private void DrawParkingEditorPanel()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Parking placement", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Click a road segment to place a parking spot on the nearest side of the road.",
            MessageType.Info
        );

        if (Tool.SelectedParkingSegment != null)
        {
            EditorGUILayout.ObjectField("Segment", Tool.SelectedParkingSegment, typeof(RoadSegmentV2), true);
            EditorGUILayout.LabelField("Side", Tool.SelectedParkingOnLeftSide ? "Left" : "Right");
            EditorGUILayout.Vector3Field("Position", Tool.SelectedParkingPosition);
        }
    }

    private void DrawBuildingPrefabPanel()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Building prefabs", EditorStyles.boldLabel);

        if (GUILayout.Button("Refresh prefab catalog"))
        {
            Tool.RefreshBuildingPrefabCatalog();
            EditorUtility.SetDirty(Tool);
        }

        if (Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.PlaceHome)
        {
            DrawBuildingPrefabSelection(
                Tool.ResidentialPrefabs,
                Tool.SelectedResidentialPrefabIndex,
                Tool.SelectResidentialPrefab);
        }
        else if (Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.PlaceOffice)
        {
            DrawBuildingPrefabSelection(
                Tool.OfficePrefabs,
                Tool.SelectedOfficePrefabIndex,
                Tool.SelectOfficePrefab);
        }
    }

    private void DrawBuildingPrefabSelection(
        System.Collections.Generic.IReadOnlyList<RoadBuildToolV2.BuildingPrefabEntry> prefabs,
        int selectedIndex,
        System.Action<int> onSelect)
    {
        if (prefabs == null || prefabs.Count == 0)
        {
            EditorGUILayout.HelpBox("No prefabs found in the configured folder.", MessageType.Warning);
            return;
        }

        string[] names = new string[prefabs.Count];
        for (int i = 0; i < prefabs.Count; i++)
            names[i] = prefabs[i] != null ? prefabs[i].name : $"Prefab {i + 1}";

        int newIndex = EditorGUILayout.Popup("Selected prefab", selectedIndex, names);
        if (newIndex != selectedIndex)
        {
            onSelect?.Invoke(newIndex);
            EditorUtility.SetDirty(Tool);
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

    private void DrawSignalMovementToggleButton(string label, RoadLaneConnectionV2.MovementType movementType)
    {
        bool allowed = Tool.GetSelectedSignalMovementAllowed(movementType);
        Color oldColor = GUI.backgroundColor;
        GUI.backgroundColor = allowed ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);

        if (GUILayout.Button(label, GUILayout.Height(28f)))
        {
            Tool.ToggleSelectedSignalMovement(movementType);
            EditorUtility.SetDirty(Tool);
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

            case RoadBuildToolV2.ToolMode.DrawCurveRoad:
                return "Режим кривой: ЛКМ точка A — старт, ЛКМ точка B — точка изгиба, затем конец дороги идет за курсором. Третий ЛКМ фиксирует конец C. При continueChain следующая кривая продолжается симметрично.";

            case RoadBuildToolV2.ToolMode.DrawPedestrianPath:
                return "Pedestrian path mode: first click starts a walkway, second click finishes it. Endpoints snap to sidewalks and pedestrian graph points.";

            case RoadBuildToolV2.ToolMode.PlaceHome:
                return "Home mode: single click places the selected residential prefab.";

            case RoadBuildToolV2.ToolMode.PlaceOffice:
                return "Office mode: single click places the selected office prefab.";

            case RoadBuildToolV2.ToolMode.ParkingSpot:
                return "Parking mode: click a road segment to create a parking spot on the nearest side.";

            case RoadBuildToolV2.ToolMode.DeleteRoad:
                return "Режим удаления: ЛКМ удаляет ближайший сегмент дороги.";

            case RoadBuildToolV2.ToolMode.JunctionControl:
                return "Режим перекрестков: ЛКМ по существующему перекрестку переключает режим RightHandRule / TrafficLight.";

            case RoadBuildToolV2.ToolMode.JunctionKeepClear:
                return "Режим keep-clear: ЛКМ по перекрестку включает или выключает вафельную разметку и правило 'не занимать перекресток'.";

            case RoadBuildToolV2.ToolMode.JunctionSignals:
                return "Режим фаз: ЛКМ по светофорному перекрестку выбирает его, ЛКМ ближе к входящему сегменту выбирает подход. В инспекторе переключаются фазы и настраиваются ← ↑ → для выбранного входа.";

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
        Vector3 clickWorldPosition =
            Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.ParkingSpot ||
            Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.PlaceHome ||
            Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.PlaceOffice
            ? worldPosition
            : worldPosition;

        DrawPreview(worldPosition);
        DrawDrawAssistOverlay(rawWorldPosition, worldPosition);
        DrawTurnSelectionOverlay();
        DrawSignalSelectionOverlay();
        DrawLaneConnectionOverlay();

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Undo.IncrementCurrentGroup();

            Tool.HandleSceneClick(clickWorldPosition);

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

            case RoadBuildToolV2.ToolMode.DrawCurveRoad:
                Handles.color = Tool.PreviewColor;
                Handles.DrawSolidDisc(worldPosition, Vector3.forward, 0.06f);

                if (Tool.HasCurrentStartNode && !Tool.HasCurveControlPoint)
                {
                    Handles.DrawDottedLine(Tool.CurrentStartPosition, worldPosition, 4f);
                    Handles.DrawWireDisc(Tool.CurrentStartPosition, Vector3.forward, Tool.SnapDistance);
                }
                else if (Tool.HasCurrentStartNode && Tool.HasCurveControlPoint)
                {
                    DrawQuadraticPreview(
                        Tool.CurrentStartPosition,
                        Tool.CurrentCurveControlPoint,
                        worldPosition
                    );

                    Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
                    Handles.DrawWireDisc(Tool.CurrentStartPosition, Vector3.forward, Tool.SnapDistance);
                    Handles.DrawSolidDisc(Tool.CurrentCurveControlPoint, Vector3.forward, 0.05f);
                    Handles.DrawDottedLine(Tool.CurrentStartPosition, Tool.CurrentCurveControlPoint, 3f);
                    Handles.DrawDottedLine(Tool.CurrentCurveControlPoint, worldPosition, 3f);
                }
                break;

            case RoadBuildToolV2.ToolMode.DrawPedestrianPath:
                Handles.color = Tool.IsPedestrianPathPreviewValid(worldPosition)
                    ? Tool.PedestrianPathPreviewColor
                    : Tool.InvalidPreviewColor;
                Handles.DrawSolidDisc(worldPosition, Vector3.forward, 0.06f);

                if (Tool.HasPedestrianPathStart)
                    Handles.DrawDottedLine(Tool.CurrentPedestrianPathStart, worldPosition, 4f);
                break;

            case RoadBuildToolV2.ToolMode.PlaceHome:
                DrawBuildingPreview(
                    worldPosition + Tool.GetSelectedBuildingPrefabCenterOffset(BuildingZoneV2.BuildingType.Home),
                    Tool.GetSelectedBuildingPrefabSize(BuildingZoneV2.BuildingType.Home),
                    Tool.HomePreviewColor);
                break;

            case RoadBuildToolV2.ToolMode.PlaceOffice:
                DrawBuildingPreview(
                    worldPosition + Tool.GetSelectedBuildingPrefabCenterOffset(BuildingZoneV2.BuildingType.Office),
                    Tool.GetSelectedBuildingPrefabSize(BuildingZoneV2.BuildingType.Office),
                    Tool.OfficePreviewColor);
                break;

            case RoadBuildToolV2.ToolMode.ParkingSpot:
                Handles.color = Tool.ParkingPreviewColor;
                Handles.DrawSolidDisc(worldPosition, Vector3.forward, 0.07f);
                Handles.DrawWireDisc(worldPosition, Vector3.forward, 0.22f);

                if (Tool.SelectedParkingSegment != null)
                {
                    Handles.color = Tool.ParkingPreviewColor;
                    Handles.DrawLine(Tool.SelectedParkingPosition, worldPosition);
                    Handles.DrawWireDisc(Tool.SelectedParkingPosition, Vector3.forward, 0.12f);
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

            case RoadBuildToolV2.ToolMode.JunctionSignals:
                Handles.color = Tool.JunctionPreviewColor;
                Handles.DrawSolidDisc(worldPosition, Vector3.forward, 0.06f);
                Handles.DrawWireDisc(worldPosition, Vector3.forward, Tool.JunctionPickDistance);
                break;

            case RoadBuildToolV2.ToolMode.JunctionKeepClear:
                Handles.color = Tool.JunctionPreviewColor;
                Handles.DrawSolidDisc(worldPosition, Vector3.forward, 0.06f);
                Handles.DrawWireDisc(worldPosition, Vector3.forward, Tool.JunctionPickDistance);
                break;
        }
    }

    private void DrawBuildingPreview(Vector3 worldPosition, Vector2 size, Color color)
    {
        Handles.color = color;
        Handles.DrawSolidDisc(worldPosition, Vector3.forward, 0.06f);
        Vector3 half = new Vector3(size.x, size.y, 0f) * 0.5f;
        Vector3 p0 = worldPosition + new Vector3(-half.x, -half.y, 0f);
        Vector3 p1 = worldPosition + new Vector3(half.x, -half.y, 0f);
        Vector3 p2 = worldPosition + new Vector3(half.x, half.y, 0f);
        Vector3 p3 = worldPosition + new Vector3(-half.x, half.y, 0f);

        Handles.DrawSolidRectangleWithOutline(
            new Vector3[] { p0, p1, p2, p3 },
            new Color(color.r, color.g, color.b, 0.12f),
            color
        );
    }

    private void DrawDrawAssistOverlay(Vector3 rawWorldPosition, Vector3 snappedWorldPosition)
    {
        if (Tool.CurrentToolMode != RoadBuildToolV2.ToolMode.DrawRoad &&
            Tool.CurrentToolMode != RoadBuildToolV2.ToolMode.DrawCurveRoad)
            return;

        if (!Tool.HasCurrentStartNode)
            return;

        if (Tool.CurrentToolMode == RoadBuildToolV2.ToolMode.DrawCurveRoad && !Tool.HasCurveControlPoint)
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

    private void DrawSignalSelectionOverlay()
    {
        if (Tool.CurrentToolMode != RoadBuildToolV2.ToolMode.JunctionSignals)
            return;

        if (Tool.SelectedSignalNode != null)
        {
            Handles.color = new Color(0.2f, 1f, 1f, 1f);
            Handles.DrawWireDisc(Tool.SelectedSignalNode.transform.position, Vector3.forward, 0.42f);
        }

        if (Tool.SelectedSignalIncomingSegment != null && Tool.SelectedSignalNode != null)
        {
            Vector3 nodePos = Tool.SelectedSignalNode.transform.position;
            Vector3 otherPos = nodePos;

            if (Tool.SelectedSignalIncomingSegment.EndNode == Tool.SelectedSignalNode && Tool.SelectedSignalIncomingSegment.StartNode != null)
                otherPos = Tool.SelectedSignalIncomingSegment.StartNode.transform.position;
            else if (Tool.SelectedSignalIncomingSegment.StartNode == Tool.SelectedSignalNode && Tool.SelectedSignalIncomingSegment.EndNode != null)
                otherPos = Tool.SelectedSignalIncomingSegment.EndNode.transform.position;

            Vector3 dir = (nodePos - otherPos).normalized;
            Vector3 probe = nodePos - dir * 0.55f;

            Handles.color = new Color(0.2f, 1f, 1f, 1f);
            Handles.DrawLine(otherPos, nodePos);
            Handles.DrawSolidDisc(probe, Vector3.forward, 0.08f);

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = Color.white;

            string phaseLabel = Tool.SelectedSignal != null ? Tool.SelectedSignal.GetCurrentPhaseLabel() : "-";

            Handles.Label(
                probe + new Vector3(0.08f, 0.08f, 0f),
                $"{phaseLabel}   ← {(Tool.GetSelectedSignalMovementAllowed(RoadLaneConnectionV2.MovementType.Left) ? "ON" : "OFF")}  " +
                $"↑ {(Tool.GetSelectedSignalMovementAllowed(RoadLaneConnectionV2.MovementType.Straight) ? "ON" : "OFF")}  " +
                $"→ {(Tool.GetSelectedSignalMovementAllowed(RoadLaneConnectionV2.MovementType.Right) ? "ON" : "OFF")}",
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

    private void DrawQuadraticPreview(Vector3 a, Vector3 control, Vector3 b)
    {
        const int samples = 24;
        Vector3 prev = a;

        Handles.color = new Color(0.2f, 1f, 1f, 1f);

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            float u = 1f - t;
            Vector3 p = u * u * a + 2f * u * t * control + t * t * b;

            Handles.DrawLine(prev, p);
            prev = p;
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
