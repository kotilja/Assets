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

        if (GUILayout.Button("Сбросить текущую цепочку"))
        {
            Tool.ClearCurrentChain();
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

        EditorGUILayout.EndHorizontal();
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