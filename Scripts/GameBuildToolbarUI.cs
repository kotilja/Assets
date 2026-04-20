using System.Collections.Generic;
using System.Globalization;
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
    [SerializeField] private float panelWidth = 320f;
    [SerializeField] private Vector2 floatingSignalsPanelPosition = new Vector2(352f, -16f);
    [SerializeField] private float floatingSignalsPanelWidth = 300f;

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
    private RectTransform floatingSignalsPanel;

    private RectTransform constructionGroup;
    private RectTransform homesGroup;
    private RectTransform roadsGroup;
    private RectTransform settingsGroup;
    private RectTransform roadSettingsPanel;
    private RectTransform turnsPanel;
    private RectTransform laneConnectionsPanel;
    private RectTransform signalsPhaseListGroup;

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

    private Button turnStraightButton;
    private Button turnLeftButton;
    private Button turnRightButton;
    private Button clearLaneConnectionsButton;

    private Button previousPhaseButton;
    private Button nextPhaseButton;
    private Button addPhaseButton;
    private Button removePhaseButton;

    private Text roadForwardValueText;
    private Text roadBackwardValueText;
    private Text roadSpeedValueText;
    private Text turnsStatusText;
    private Text laneConnectionsStatusText;
    private Text signalsStatusText;
    private Text signalsPhaseText;
    private InputField signalsDurationInput;

    private bool suppressSignalsDurationCallbacks;
    private readonly List<Button> signalPhaseButtons = new List<Button>();
    private int cachedSignalInstanceId = 0;
    private int cachedSignalPhaseCount = -1;

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

        if (GetComponent<GraphicRaycaster>() == null)
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
        ClearExistingPanels();

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
        constructionGroup = CreateVerticalGroup("ConstructionGroup", rootPanel, 18f);

        homesButton = CreateActionButton(constructionGroup, "Дома", subFontSize, secondaryButtonColor, ToggleHomesGroup);
        homesGroup = CreateVerticalGroup("HomesGroup", constructionGroup, 14f);
        housingButton = CreateToolButton(homesGroup, "Жилье", RoadBuildToolV2.ToolMode.PlaceHome);
        officesButton = CreateToolButton(homesGroup, "Офисы", RoadBuildToolV2.ToolMode.PlaceOffice);

        roadsButton = CreateActionButton(constructionGroup, "Дороги", subFontSize, secondaryButtonColor, ToggleRoadsGroup);
        roadsGroup = CreateVerticalGroup("RoadsGroup", constructionGroup, 14f);
        drawRoadButton = CreateToolButton(roadsGroup, "Рисование", RoadBuildToolV2.ToolMode.DrawRoad);
        drawCurveButton = CreateToolButton(roadsGroup, "Кривая", RoadBuildToolV2.ToolMode.DrawCurveRoad);
        parkingButton = CreateToolButton(roadsGroup, "Parking", RoadBuildToolV2.ToolMode.ParkingSpot);

        roadSettingsPanel = CreateVerticalGroup("RoadSettingsPanel", rootPanel, 10f);
        CreateSectionLabel(roadSettingsPanel, "Параметры дороги");
        CreateNumericStepper(roadSettingsPanel, "Полос вперед", HandleDecreaseForwardLanes, out roadForwardValueText, HandleIncreaseForwardLanes);
        CreateNumericStepper(roadSettingsPanel, "Полос назад", HandleDecreaseBackwardLanes, out roadBackwardValueText, HandleIncreaseBackwardLanes);
        CreateNumericStepper(roadSettingsPanel, "Скорость", HandleDecreaseSpeedLimit, out roadSpeedValueText, HandleIncreaseSpeedLimit);

        settingsButton = CreateActionButton(rootPanel, "Настройки", fontSize, primaryButtonColor, ToggleSettingsGroup);
        settingsGroup = CreateVerticalGroup("SettingsGroup", rootPanel, 18f);
        junctionControlButton = CreateToolButton(settingsGroup, "Перекрестки", RoadBuildToolV2.ToolMode.JunctionControl);
        keepClearButton = CreateToolButton(settingsGroup, "Keep Clear", RoadBuildToolV2.ToolMode.JunctionKeepClear);
        signalsButton = CreateToolButton(settingsGroup, "Фазы", RoadBuildToolV2.ToolMode.JunctionSignals);
        turnsButton = CreateToolButton(settingsGroup, "Маневры", RoadBuildToolV2.ToolMode.JunctionTurns);
        laneConnectionsButton = CreateToolButton(settingsGroup, "Связи полос", RoadBuildToolV2.ToolMode.LaneConnections);

        turnsPanel = CreateVerticalGroup("TurnsPanel", rootPanel, 10f);
        CreateSectionLabel(turnsPanel, "Маневры");
        turnsStatusText = CreateInfoLabel(turnsPanel, "Кликни по въезду на перекресток");
        RectTransform turnMovesRow = CreateHorizontalGroup("TurnMovesRow", turnsPanel, 4f);
        turnStraightButton = CreateActionButton(turnMovesRow, "Прямо", subFontSize, secondaryButtonColor, HandleToggleTurnStraight);
        turnLeftButton = CreateActionButton(turnMovesRow, "Лево", subFontSize, secondaryButtonColor, HandleToggleTurnLeft);
        turnRightButton = CreateActionButton(turnMovesRow, "Право", subFontSize, secondaryButtonColor, HandleToggleTurnRight);

        laneConnectionsPanel = CreateVerticalGroup("LaneConnectionsPanel", rootPanel, 10f);
        CreateSectionLabel(laneConnectionsPanel, "Связи полос");
        laneConnectionsStatusText = CreateInfoLabel(laneConnectionsPanel, "Кликни по входящей полосе");
        clearLaneConnectionsButton = CreateActionButton(laneConnectionsPanel, "Очистить связи выбранной полосы", subFontSize, secondaryButtonColor, HandleClearLaneConnections);

        deleteButton = CreateToolButton(rootPanel, "Удаление", RoadBuildToolV2.ToolMode.DeleteRoad, fontSize, primaryButtonColor);

        BuildSignalsPanel();

        SetGroupVisible(constructionGroup, true);
        SetGroupVisible(homesGroup, false);
        SetGroupVisible(roadsGroup, false);
        SetGroupVisible(settingsGroup, false);
        SetGroupVisible(roadSettingsPanel, false);
        SetGroupVisible(turnsPanel, false);
        SetGroupVisible(laneConnectionsPanel, false);
        SetGroupVisible(floatingSignalsPanel, false);
    }

    private void BuildSignalsPanel()
    {
        floatingSignalsPanel = CreatePanel("SignalsEditorPanel", transform, panelColor);
        floatingSignalsPanel.anchorMin = panelAnchorMin;
        floatingSignalsPanel.anchorMax = panelAnchorMax;
        floatingSignalsPanel.pivot = panelPivot;
        floatingSignalsPanel.anchoredPosition = floatingSignalsPanelPosition;
        floatingSignalsPanel.sizeDelta = new Vector2(floatingSignalsPanelWidth, 0f);

        VerticalLayoutGroup layout = floatingSignalsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = floatingSignalsPanel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        CreateSectionLabel(floatingSignalsPanel, "Редактор фаз");
        signalsStatusText = CreateInfoLabel(floatingSignalsPanel, "Кликни по перекрестку со светофором");
        signalsPhaseText = CreateValueLabelBlock(floatingSignalsPanel, "Текущая фаза: -");
        CreateSectionLabel(floatingSignalsPanel, "Список фаз");
        signalsPhaseListGroup = CreateVerticalGroup("SignalsPhaseList", floatingSignalsPanel, 0f);
        signalsDurationInput = CreateLabeledInputField(floatingSignalsPanel, "Длительность фазы");

        RectTransform navigationRow = CreateHorizontalGroup("SignalsNavigationRow", floatingSignalsPanel, 4f);
        previousPhaseButton = CreateActionButton(navigationRow, "<", subFontSize, secondaryButtonColor, HandlePreviousPhase);
        nextPhaseButton = CreateActionButton(navigationRow, ">", subFontSize, secondaryButtonColor, HandleNextPhase);

        addPhaseButton = CreateActionButton(floatingSignalsPanel, "Добавить фазу", subFontSize, secondaryButtonColor, HandleAddPhase);
        removePhaseButton = CreateActionButton(floatingSignalsPanel, "Удалить фазу", subFontSize, secondaryButtonColor, HandleRemovePhase);

        CreateInfoLabel(
            floatingSignalsPanel,
            "Над каждым въездом: левый круг — налево, центральный — прямо, правый — направо. Клик по кругу меняет цвет."
        );

        if (signalsDurationInput != null)
            signalsDurationInput.onEndEdit.AddListener(HandleSignalDurationEdited);
    }

    private void ToggleConstructionGroup()
    {
        ToggleGroup(ref constructionGroup, "ConstructionGroup");
    }

    private void ToggleHomesGroup()
    {
        ToggleGroup(ref homesGroup, "HomesGroup");
    }

    private void ToggleRoadsGroup()
    {
        ToggleGroup(ref roadsGroup, "RoadsGroup");
    }

    private void ToggleSettingsGroup()
    {
        ToggleGroup(ref settingsGroup, "SettingsGroup");
    }

    private void ToggleGroup(ref RectTransform group, string groupName)
    {
        if (group == null)
            group = FindGroup(groupName);

        if (group == null)
            return;

        SetGroupVisible(group, !group.gameObject.activeSelf);
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

        RefreshRoadSettingsPanel();
        RefreshRuntimePanels();
    }

    private void RefreshRoadSettingsPanel()
    {
        bool show = buildTool.CurrentToolMode == RoadBuildToolV2.ToolMode.DrawRoad ||
                    buildTool.CurrentToolMode == RoadBuildToolV2.ToolMode.DrawCurveRoad;

        SetGroupVisible(roadSettingsPanel, show);

        if (roadForwardValueText != null)
            roadForwardValueText.text = buildTool.ForwardLanes.ToString();

        if (roadBackwardValueText != null)
            roadBackwardValueText.text = buildTool.BackwardLanes.ToString();

        if (roadSpeedValueText != null)
            roadSpeedValueText.text = buildTool.SpeedLimit.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private void RefreshRuntimePanels()
    {
        SetGroupVisible(turnsPanel, buildTool.CurrentToolMode == RoadBuildToolV2.ToolMode.JunctionTurns);
        SetGroupVisible(laneConnectionsPanel, buildTool.CurrentToolMode == RoadBuildToolV2.ToolMode.LaneConnections);
        SetGroupVisible(floatingSignalsPanel, buildTool.CurrentToolMode == RoadBuildToolV2.ToolMode.JunctionSignals);

        RefreshTurnsPanel();
        RefreshLaneConnectionsPanel();
        RefreshSignalsPanel();
    }

    private void RefreshTurnsPanel()
    {
        if (turnsStatusText == null)
            return;

        bool hasSelection = buildTool.SelectedTurnNode != null && buildTool.SelectedIncomingSegment != null;
        turnsStatusText.text = hasSelection
            ? "Въезд выбран. Включай нужные маневры."
            : "Кликни по въезду на перекресток";

        RefreshMovementButton(turnStraightButton, hasSelection, buildTool.GetSelectedApproachMovementAllowed(RoadLaneConnectionV2.MovementType.Straight));
        RefreshMovementButton(turnLeftButton, hasSelection, buildTool.GetSelectedApproachMovementAllowed(RoadLaneConnectionV2.MovementType.Left));
        RefreshMovementButton(turnRightButton, hasSelection, buildTool.GetSelectedApproachMovementAllowed(RoadLaneConnectionV2.MovementType.Right));
    }

    private void RefreshLaneConnectionsPanel()
    {
        if (laneConnectionsStatusText == null)
            return;

        bool hasFromLane = buildTool.SelectedFromLane != null;
        bool hasToLane = buildTool.SelectedToLane != null;

        if (!hasFromLane)
            laneConnectionsStatusText.text = "Кликни по входящей полосе";
        else if (!hasToLane)
            laneConnectionsStatusText.text = "Теперь кликни по исходящей полосе";
        else
            laneConnectionsStatusText.text = buildTool.SelectedManualConnectionExists()
                ? "Связь между полосами включена"
                : "Связь между полосами выключена";

        SetButtonInteractable(clearLaneConnectionsButton, hasFromLane && buildTool.SelectedFromLaneHasManualConnections());
    }

    private void RefreshSignalsPanel()
    {
        if (signalsStatusText == null)
            return;

        bool show = buildTool.CurrentToolMode == RoadBuildToolV2.ToolMode.JunctionSignals;
        SetGroupVisible(floatingSignalsPanel, show);
        if (!show)
            return;

        bool hasSignal = buildTool.SelectedSignal != null;
        signalsStatusText.text = hasSignal
            ? "Выбран перекресток. Круги над въездами редактируют сигналы текущей фазы."
            : "Кликни по перекрестку со светофором";

        if (signalsPhaseText != null)
        {
            signalsPhaseText.text = hasSignal
                ? $"Текущая фаза: {buildTool.SelectedSignal.GetCurrentEditablePhaseName()}"
                : "Текущая фаза: -";
        }

        if (signalsDurationInput != null)
        {
            suppressSignalsDurationCallbacks = true;
            signalsDurationInput.interactable = hasSignal;
            signalsDurationInput.text = hasSignal
                ? buildTool.GetSelectedSignalPhaseDuration().ToString("0.0", CultureInfo.InvariantCulture)
                : string.Empty;
            suppressSignalsDurationCallbacks = false;
        }

        RefreshSignalsPhaseList(hasSignal);

        SetButtonInteractable(previousPhaseButton, hasSignal);
        SetButtonInteractable(nextPhaseButton, hasSignal);
        SetButtonInteractable(addPhaseButton, hasSignal);
        SetButtonInteractable(removePhaseButton, hasSignal);
    }

    private void RefreshSignalsPhaseList(bool hasSignal)
    {
        if (signalsPhaseListGroup == null)
            return;

        int signalInstanceId = hasSignal ? buildTool.SelectedSignal.GetInstanceID() : 0;
        int phaseCount = hasSignal ? buildTool.GetSelectedSignalPhaseCount() : 0;

        if (signalInstanceId != cachedSignalInstanceId || phaseCount != cachedSignalPhaseCount)
        {
            RebuildSignalsPhaseList(signalInstanceId, phaseCount);
        }

        int selectedPhaseIndex = hasSignal ? buildTool.GetSelectedSignalCurrentPhaseIndex() : -1;

        for (int i = 0; i < signalPhaseButtons.Count; i++)
        {
            Button button = signalPhaseButtons[i];
            if (button == null)
                continue;

            button.interactable = hasSignal;

            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = i == selectedPhaseIndex ? selectedButtonColor : secondaryButtonColor;
        }
    }

    private void RebuildSignalsPhaseList(int signalInstanceId, int phaseCount)
    {
        ClearSignalPhaseListObjects();

        cachedSignalInstanceId = signalInstanceId;
        cachedSignalPhaseCount = phaseCount;

        if (buildTool == null || buildTool.SelectedSignal == null || phaseCount <= 0)
        {
            CreateInfoLabel(signalsPhaseListGroup, "Фазы отсутствуют");
            return;
        }

        for (int i = 0; i < phaseCount; i++)
        {
            int phaseIndex = i;
            string phaseName = buildTool.GetSelectedSignalPhaseName(i);
            if (string.IsNullOrWhiteSpace(phaseName))
                phaseName = $"Фаза {i + 1}";

            Button phaseButton = CreateActionButton(
                signalsPhaseListGroup,
                $"{i + 1}. {phaseName}",
                subFontSize,
                secondaryButtonColor,
                () => HandleSelectSignalPhase(phaseIndex));

            signalPhaseButtons.Add(phaseButton);
        }
    }

    private void ClearSignalPhaseListObjects()
    {
        signalPhaseButtons.Clear();

        if (signalsPhaseListGroup == null)
            return;

        for (int i = signalsPhaseListGroup.childCount - 1; i >= 0; i--)
            Destroy(signalsPhaseListGroup.GetChild(i).gameObject);
    }

    private void HandleSelectSignalPhase(int phaseIndex)
    {
        if (buildTool == null)
            return;

        buildTool.SetSelectedSignalPhase(phaseIndex);
        RefreshSelection();
    }

    private void HandleSignalDurationEdited(string value)
    {
        if (suppressSignalsDurationCallbacks || buildTool == null || buildTool.SelectedSignal == null)
            return;

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float duration) &&
            !float.TryParse(value, out duration))
            return;

        buildTool.SetSelectedSignalPhaseDuration(duration);
        RefreshSelection();
    }

    private void HandleDecreaseForwardLanes()
    {
        buildTool.AdjustForwardLanes(-1);
        RefreshSelection();
    }

    private void HandleIncreaseForwardLanes()
    {
        buildTool.AdjustForwardLanes(1);
        RefreshSelection();
    }

    private void HandleDecreaseBackwardLanes()
    {
        buildTool.AdjustBackwardLanes(-1);
        RefreshSelection();
    }

    private void HandleIncreaseBackwardLanes()
    {
        buildTool.AdjustBackwardLanes(1);
        RefreshSelection();
    }

    private void HandleDecreaseSpeedLimit()
    {
        buildTool.AdjustSpeedLimit(-0.5f);
        RefreshSelection();
    }

    private void HandleIncreaseSpeedLimit()
    {
        buildTool.AdjustSpeedLimit(0.5f);
        RefreshSelection();
    }

    private void HandlePreviousPhase()
    {
        buildTool.SelectPreviousSignalPhase();
        RefreshSelection();
    }

    private void HandleNextPhase()
    {
        buildTool.SelectNextSignalPhase();
        RefreshSelection();
    }

    private void HandleAddPhase()
    {
        buildTool.AddSignalPhase();
        RefreshSelection();
    }

    private void HandleRemovePhase()
    {
        buildTool.RemoveSignalPhase();
        RefreshSelection();
    }

    private void HandleToggleTurnStraight()
    {
        ToggleTurnMovement(RoadLaneConnectionV2.MovementType.Straight);
    }

    private void HandleToggleTurnLeft()
    {
        ToggleTurnMovement(RoadLaneConnectionV2.MovementType.Left);
    }

    private void HandleToggleTurnRight()
    {
        ToggleTurnMovement(RoadLaneConnectionV2.MovementType.Right);
    }

    private void ToggleTurnMovement(RoadLaneConnectionV2.MovementType movementType)
    {
        buildTool.ToggleSelectedApproachMovement(movementType);
        RefreshSelection();
    }

    private void HandleClearLaneConnections()
    {
        buildTool.ClearManualConnectionsForSelectedLane();
        RefreshSelection();
    }

    private void ApplySelection(Button button, RoadBuildToolV2.ToolMode mode)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image == null)
            return;

        image.color = buildTool.CurrentToolMode == mode
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

    private RectTransform CreateVerticalGroup(string objectName, Transform parent, float leftPadding)
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

    private RectTransform CreateHorizontalGroup(string objectName, Transform parent, float spacing)
    {
        RectTransform group = CreatePanel(objectName, parent, new Color(0f, 0f, 0f, 0f));

        HorizontalLayoutGroup layout = group.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = group.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        return group;
    }

    private void CreateNumericStepper(
        Transform parent,
        string label,
        UnityEngine.Events.UnityAction onDecrease,
        out Text valueText,
        UnityEngine.Events.UnityAction onIncrease)
    {
        RectTransform group = CreateVerticalGroup($"{label}_Group", parent, 0f);
        CreateSectionLabel(group, label);

        RectTransform row = CreateHorizontalGroup($"{label}_Row", group, 4f);
        CreateActionButton(row, "-", subFontSize, secondaryButtonColor, onDecrease);
        valueText = CreateValueLabel(row, "0");
        CreateActionButton(row, "+", subFontSize, secondaryButtonColor, onIncrease);
    }

    private Text CreateValueLabel(Transform parent, string value)
    {
        GameObject labelObject = new GameObject("Value", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        LayoutElement layoutElement = labelObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 30f;
        layoutElement.preferredWidth = 70f;

        Text text = labelObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = subFontSize;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        return text;
    }

    private Text CreateValueLabelBlock(Transform parent, string value)
    {
        GameObject labelObject = new GameObject("ValueBlock", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        LayoutElement layoutElement = labelObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 26f;

        Text text = labelObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        return text;
    }

    private InputField CreateLabeledInputField(Transform parent, string label)
    {
        RectTransform group = CreateVerticalGroup($"{label}_InputGroup", parent, 0f);
        CreateSectionLabel(group, label);

        GameObject fieldObject = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(InputField));
        fieldObject.transform.SetParent(group, false);

        Image background = fieldObject.GetComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.08f, 1f);

        LayoutElement layoutElement = fieldObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 32f;

        InputField input = fieldObject.GetComponent<InputField>();
        input.contentType = InputField.ContentType.DecimalNumber;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(fieldObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        placeholderObject.transform.SetParent(fieldObject.transform, false);
        RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(8f, 4f);
        placeholderRect.offsetMax = new Vector2(-8f, -4f);

        Text placeholder = placeholderObject.GetComponent<Text>();
        placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholder.fontSize = 14;
        placeholder.color = new Color(1f, 1f, 1f, 0.45f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.text = "6.0";

        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
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
        text.alignment = TextAnchor.MiddleCenter;
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

    private Text CreateSectionLabel(Transform parent, string label)
    {
        GameObject labelObject = new GameObject(label, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        LayoutElement layoutElement = labelObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 24f;

        Text text = labelObject.GetComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = subFontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        return text;
    }

    private Text CreateInfoLabel(Transform parent, string label)
    {
        GameObject labelObject = new GameObject(label, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        LayoutElement layoutElement = labelObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 42f;

        Text text = labelObject.GetComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.color = textColor;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    private void RefreshMovementButton(Button button, bool interactable, bool isAllowed)
    {
        if (button == null)
            return;

        button.interactable = interactable;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = isAllowed ? selectedButtonColor : secondaryButtonColor;
    }

    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private void SetGroupVisible(RectTransform group, bool isVisible)
    {
        if (group != null)
            group.gameObject.SetActive(isVisible);
    }

    private void ClearExistingPanels()
    {
        Transform toolbar = transform.Find("BuildToolbar");
        if (toolbar != null)
            Destroy(toolbar.gameObject);

        Transform signals = transform.Find("SignalsEditorPanel");
        if (signals != null)
            Destroy(signals.gameObject);

        signalPhaseButtons.Clear();
        cachedSignalInstanceId = 0;
        cachedSignalPhaseCount = -1;
    }

    private RectTransform FindGroup(string groupName)
    {
        Transform existing = transform.Find($"BuildToolbar/{groupName}");
        return existing as RectTransform;
    }
}
