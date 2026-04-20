using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameTimeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameTimeSystem gameTimeSystem;

    [Header("Layout")]
    [SerializeField] private Vector2 panelAnchorMin = new Vector2(0.5f, 1f);
    [SerializeField] private Vector2 panelAnchorMax = new Vector2(0.5f, 1f);
    [SerializeField] private Vector2 panelPivot = new Vector2(0.5f, 1f);
    [SerializeField] private Vector2 panelPosition = new Vector2(0f, -16f);
    [SerializeField] private float minPanelWidth = 760f;

    [Header("Style")]
    [SerializeField] private Color panelColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
    [SerializeField] private Color buttonColor = new Color(0.22f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color activeButtonColor = new Color(0.30f, 0.48f, 0.30f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private int dateFontSize = 20;
    [SerializeField] private int buttonFontSize = 18;

    private Canvas canvas;
    private RectTransform rootPanel;
    private Text dateText;
    private Button pauseButton;
    private Button normalButton;
    private Button fastButton;

    private void Awake()
    {
        if (gameTimeSystem == null)
            gameTimeSystem = FindFirstObjectByType<GameTimeSystem>();

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
        RefreshUI();
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

    private void EnsureUIBuilt()
    {
        if (rootPanel == null)
            BuildUI();
    }

    private void BuildUI()
    {
        ClearExistingRoot();

        rootPanel = CreatePanel("GameTimePanel", transform, panelColor);
        rootPanel.anchorMin = panelAnchorMin;
        rootPanel.anchorMax = panelAnchorMax;
        rootPanel.pivot = panelPivot;
        rootPanel.anchoredPosition = panelPosition;
        rootPanel.sizeDelta = new Vector2(minPanelWidth, 0f);

        HorizontalLayoutGroup layout = rootPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(16, 16, 10, 10);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        ContentSizeFitter fitter = rootPanel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        dateText = CreateLabel(rootPanel, "Дата", dateFontSize, 540f);
        pauseButton = CreateButton(rootPanel, "||", buttonFontSize, 42f, () => gameTimeSystem.SetPaused());
        normalButton = CreateButton(rootPanel, ">", buttonFontSize, 42f, () => gameTimeSystem.SetNormalSpeed());
        fastButton = CreateButton(rootPanel, ">>", buttonFontSize, 42f, () => gameTimeSystem.SetFastSpeed());
    }

    private void RefreshUI()
    {
        if (gameTimeSystem == null)
            return;

        if (dateText != null)
            dateText.text = gameTimeSystem.GetFormattedDateTime();

        ApplyButtonState(pauseButton, Mathf.Approximately(gameTimeSystem.SimulationSpeed, 0f));
        ApplyButtonState(normalButton, Mathf.Approximately(gameTimeSystem.SimulationSpeed, 1f));
        ApplyButtonState(fastButton, Mathf.Approximately(gameTimeSystem.SimulationSpeed, 3f));
    }

    private void ApplyButtonState(Button button, bool isActive)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = isActive ? activeButtonColor : buttonColor;
    }

    private RectTransform CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        Image image = panelObject.GetComponent<Image>();
        image.color = color;

        return panelObject.GetComponent<RectTransform>();
    }

    private Text CreateLabel(Transform parent, string value, int fontSize, float width)
    {
        GameObject labelObject = new GameObject("DateText", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        LayoutElement layoutElement = labelObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = 42f;

        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.color = textColor;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.text = value;

        return label;
    }

    private Button CreateButton(Transform parent, string label, int fontSize, float width, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = 42f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = buttonColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.text = label;

        return button;
    }

    private void ClearExistingRoot()
    {
        Transform existing = transform.Find("GameTimePanel");
        if (existing != null)
            Destroy(existing.gameObject);
    }
}
