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
        EditorGUILayout.HelpBox(
            "ЛКМ в Scene View: поставить узел / создать сегмент. Shift + ЛКМ: удалить сегмент. Esc: завершить текущую цепочку.",
            MessageType.Info
        );

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

    private void OnSceneGUI()
    {
        if (!Tool.ToolEnabled || Tool.Network == null)
            return;

        Event e = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(controlId);

        Vector3 worldPosition = GetWorldPointOnPlane(e.mousePosition);
        DrawPreview(worldPosition, e.shift);

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Undo.IncrementCurrentGroup();

            if (e.shift)
                Tool.HandleDeleteClick(worldPosition);
            else
                Tool.HandleSceneClick(worldPosition);

            EditorUtility.SetDirty(Tool);
            EditorUtility.SetDirty(Tool.Network);
            e.Use();
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            Tool.ClearCurrentChain();
            EditorUtility.SetDirty(Tool);
            e.Use();
        }
    }

    private void DrawPreview(Vector3 worldPosition, bool deleteMode)
    {
        Handles.color = deleteMode ? Tool.DeletePreviewColor : Tool.PreviewColor;
        Handles.DrawSolidDisc(worldPosition, Vector3.forward, 0.06f);

        if (deleteMode)
        {
            Handles.DrawWireDisc(worldPosition, Vector3.forward, Tool.DeletePickDistance);
            return;
        }

        if (Tool.HasCurrentStartNode)
        {
            Handles.DrawDottedLine(Tool.CurrentStartPosition, worldPosition, 4f);
            Handles.DrawWireDisc(Tool.CurrentStartPosition, Vector3.forward, Tool.SnapDistance);
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