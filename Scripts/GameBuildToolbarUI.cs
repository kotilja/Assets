using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameBuildToolbarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoadBuildToolV2 buildTool;

    [Header("Layout")]
    [SerializeField] private Vector2 panelAnchorMin = new Vector2(0f, 1f);
    [SerializeField] private Vector2 panelAnchorMax = new Vector2(0f, 1f);
    [SerializeField] private Vector2 panelPivot = new Vector2(0f, 1f);
    [SerializeField] private Vector2 panelPosition = new Vector2(16f, -16f);
    [SerializeField] private float panelWidth = 260f;

    [Header("Style")]
    [SerializeField] private Color panelColor = new Color(0.12f, 0.12f, 0.12f, 0.92f);
    [SerializeField] private Color primaryButtonColor = new Color(0.24f, 0.24f, 0.24f, 1f);
    [SerializeField] private Color secondaryButtonColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color selectedButtonColor = new Color(0.30f, 0.48f, 0.30f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private int fontSize = 18;
    [SerializeField] private int subFontSize = 16;

    private Canvas canvas;
    private RectTransform rootPanel;

    private RectTransform constructionGroup;
    private RectTransform homesGroup;
    private RectTransform roadsGroup;
    private RectTransform settingsGroup;

    private Button constructionButton;
    private Button settingsButton;
    private Button deleteButton;

    private Button homesButton;
    private Button roadsButton;

    private Button housingButton;
    private Button officesButton;
    private Button drawRoadButton;
    private Button drawCurveButton;
    private Button parkingButton;

    private Button junctionControlButton;
    private Button keepClearButton;
    private Button signalsButton;
    private Button turnsButton;
    private Button laneConnectionsButton;

    private void Awake()
    {
        if (buildTool == null)
            buildTool = FindFirstObjectByType<RoadBuildToolV2>();

        EnsureCanvas();
        EnsureEventSystem();
        EnsureUIBuilt();
    }

    private void OnEnable()
    {
        EnsureUIBuilt();
    }

    private void Update()
    {
        EnsureUIBuilt();
        RefreshSelection();
    }

    private void EnsureUIBuilt()
    {
        if (rootPanel == null)
            BuildUI();

        RefreshSelection();
    }

    private void EnsureCanvas()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void BuildUI()
    {
        ClearExistingRoot();

        rootPanel = CreatePanel("BuildToolbar", transform, panelColor);
        rootPanel.anchorMin = panelAnchorMin;
        rootPanel.anchorMax = panelAnchorMax;
        rootPanel.pivot = panelPivot;
        rootPanel.anchoredPosition = panelPosition;
        rootPanel.sizeDelta = new Vector2(panelWidth, 0f);

        VerticalLayoutGroup rootLayout = rootPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.spacing = 6f;
        rootLayout.padding = new RectOffset(10, 10, 10, 10);
        rootLayout.childControlHeight = true;
        rootLayout.childControlWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = true;

        ContentSizeFitter fitter = rootPanel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        constructionButton = CreateActionButton(rootPanel, "Строительство", fontSize, primaryButtonColor, ToggleConstructionGroup);
        constructionGroup = CreateGroup("ConstructionGroup", rootPanel, 18f);

        homesButton = CreateActionButton(constructionGroup, "Дома", subFontSize, secondaryButtonColor, ToggleHomesGroup);
        homesGroup = CreateGroup("HomesGroup", constructionGroup, 14f);
        housingButton = CreateToolButton(homesGroup, "Жилье", RoadBuildToolV2.ToolMode.PlaceHome);
        officesButton = CreateToolButton(homesGroup, "Офисы", RoadBuildToolV2.ToolMode.PlaceOffice);

        roadsButton = CreateActionButton(constructionGroup, "Дороги", subFontSize, secondaryButtonColor, ToggleRoadsGroup);
        roadsGroup = CreateGroup("RoadsGroup", constructionGroup, 14f);
        drawRoadButton = CreateToolButton(roadsGroup, "Рисование", RoadBuildToolV2.ToolMode.DrawRoad);
        drawCurveButton = CreateToolButton(roadsGroup, "Кривая", RoadBuildToolV2.ToolMode.DrawCurveRoad);
        parkingButton = CreateToolButton(roadsGroup, "Parking", RoadBuildToolV2.ToolMode.ParkingSpot);

        settingsButton = CreateActionButton(rootPanel, "Настройки", fontSize, primaryButtonColor, ToggleSettingsGroup);
        settingsGroup = CreateGroup("SettingsGroup", rootPanel, 18f);
        junctionControlButton = CreateToolButton(settingsGroup, "Перекрестки", RoadBuildToolV2.ToolMode.JunctionControl);
        keepClearButton = CreateToolButton(settingsGroup, "Keep Clear", RoadBuildToolV2.ToolMode.JunctionKeepClear);
        signalsButton = CreateToolButton(settingsGroup, "Фазы", RoadBuildToolV2.ToolMode.JunctionSignals);
        turnsButton = CreateToolButton(settingsGroup, "Маневры", RoadBuildToolV2.ToolMode.JunctionTurns);
        laneConnectionsButton = CreateToolButton(settingsGroup, "Связи полос", RoadBuildToolV2.ToolMode.LaneConnections);

        deleteButton = CreateToolButton(rootPanel, "Удаление", RoadBuildToolV2.ToolMode.DeleteRoad, fontSize, primaryButtonColor);

        SetGroupVisible(constructionGroup, true);
        SetGroupVisible(homesGroup, false);
        SetGroupVisible(roadsGroup, false);
        SetGroupVisible(settingsGroup, false);
    }

    private void ToggleConstructionGroup()
    {
        if (constructionGroup == null)
            constructionGroup = FindGroup("ConstructionGroup");

        if (constructionGroup == null)
            return;

        SetGroupVisible(constructionGroup, !constructionGroup.gameObject.activeSelf);
    }

    private void ToggleHomesGroup()
    {
        if (homesGroup == null)
            homesGroup = FindGroup("HomesGroup");

        if (homesGroup == null)
            return;

        SetGroupVisible(homesGroup, !homesGroup.gameObject.activeSelf);
    }

    private void ToggleRoadsGroup()
    {
        if (roadsGroup == null)
            roadsGroup = FindGroup("RoadsGroup");

        if (roadsGroup == null)
            return;

        SetGroupVisible(roadsGroup, !roadsGroup.gameObject.activeSelf);
    }

    private void ToggleSettingsGroup()
    {
        if (settingsGroup == null)
            settingsGroup = FindGroup("SettingsGroup");

        if (settingsGroup == null)
            return;

        SetGroupVisible(settingsGroup, !settingsGroup.gameObject.activeSelf);
    }

    private void SetToolMode(RoadBuildToolV2.ToolMode mode)
    {
        if (buildTool == null)
            return;

        buildTool.SetToolMode(mode);
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        if (buildTool == null)
            return;

        ApplySelection(housingButton, RoadBuildToolV2.ToolMode.PlaceHome);
        ApplySelection(officesButton, RoadBuildToolV2.ToolMode.PlaceOffice);
        ApplySelection(drawRoadButton, RoadBuildToolV2.ToolMode.DrawRoad);
        ApplySelection(drawCurveButton, RoadBuildToolV2.ToolMode.DrawCurveRoad);
        ApplySelection(parkingButton, RoadBuildToolV2.ToolMode.ParkingSpot);
        ApplySelection(deleteButton, RoadBuildToolV2.ToolMode.DeleteRoad);
        ApplySelection(junctionControlButton, RoadBuildToolV2.ToolMode.JunctionControl);
        ApplySelection(keepClearButton, RoadBuildToolV2.ToolMode.JunctionKeepClear);
        ApplySelection(signalsButton, RoadBuildToolV2.ToolMode.JunctionSignals);
        ApplySelection(turnsButton, RoadBuildToolV2.ToolMode.JunctionTurns);
        ApplySelection(laneConnectionsButton, RoadBuildToolV2.ToolMode.LaneConnections);
    }

    private void ApplySelection(Button button, RoadBuildToolV2.ToolMode mode)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image == null)
            return;

        image.color = buildTool != null && buildTool.CurrentToolMode == mode
            ? selectedButtonColor
            : GetDefaultButtonColor(button);
    }

    private Color GetDefaultButtonColor(Button button)
    {
        if (button == constructionButton || button == settingsButton || button == deleteButton)
            return primaryButtonColor;

        return secondaryButtonColor;
    }

    private RectTransform CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        Image image = panelObject.GetComponent<Image>();
        image.color = color;

        return panelObject.GetComponent<RectTransform>();
    }

    private RectTransform CreateGroup(string objectName, Transform parent, float leftPadding)
    {
        RectTransform group = CreatePanel(objectName, parent, new Color(0f, 0f, 0f, 0f));

        VerticalLayoutGroup layout = group.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(Mathf.RoundToInt(leftPadding), 0, 0, 0);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = group.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        return group;
    }

    private Button CreateToolButton(Transform parent, string label, RoadBuildToolV2.ToolMode mode)
    {
        return CreateToolButton(parent, label, mode, subFontSize, secondaryButtonColor);
    }

    private Button CreateToolButton(Transform parent, string label, RoadBuildToolV2.ToolMode mode, int useFontSize, Color color)
    {
        return CreateActionButton(parent, label, useFontSize, color, () => SetToolMode(mode));
    }

    private Button CreateActionButton(Transform parent, string label, int useFontSize, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = useFontSize >= fontSize ? 34f : 30f;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);

        Text text = textObject.GetComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = useFontSize;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.75f);
        button.colors = colors;

        return button;
    }

    private void SetGroupVisible(RectTransform group, bool isVisible)
    {
        if (group == null)
            return;

        group.gameObject.SetActive(isVisible);
    }

    private void ClearExistingRoot()
    {
        Transform existing = transform.Find("BuildToolbar");
        if (existing != null)
            Destroy(existing.gameObject);
    }

    private RectTransform FindGroup(string groupName)
    {
        Transform existing = transform.Find($"BuildToolbar/{groupName}");
        return existing != null ? existing as RectTransform : null;
    }
}
