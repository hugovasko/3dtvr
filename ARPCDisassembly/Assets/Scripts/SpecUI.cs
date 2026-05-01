using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space spec panel that floats above a disassembled component.
/// Builds its own Canvas + UI hierarchy at Awake so prefabs don't need any
/// scene-level UI setup. Always rotates to face the AR camera (billboard).
///
/// Activation flow:
///   ComponentController.Disassemble()  → SpecUI.Show(name, specs) → fades in
///   ComponentController.Assemble()     → SpecUI.Hide()             → fades out
///
/// Requires TextMeshPro Essentials to be imported once per project
/// (Window → TextMeshPro → Import TMP Essential Resources). Unity prompts
/// the first time a TMP component is referenced at runtime.
/// </summary>
public class SpecUI : MonoBehaviour
{
    [Tooltip("Local Y offset above the component root (meters).")]
    [SerializeField] private float verticalOffset = 0.20f;

    [Tooltip("Physical width × height of the panel in world meters.")]
    [SerializeField] private Vector2 panelSizeMeters = new(0.22f, 0.22f);

    [Tooltip("Seconds to fade panel in/out.")]
    [Range(0.05f, 2f)] [SerializeField] private float fadeDuration = 0.35f;

    // --- Internal cached refs (built at Awake) ---
    private Camera arCamera;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private TextMeshProUGUI titleText;
    private RectTransform specsContainer;
    private Coroutine fadeCoroutine;

    // --- Visual style constants ---
    private const float CANVAS_PIXELS_PER_METER = 1000f;     // 1mm = 1px in design space
    private static readonly Color BG_COLOR     = new(0.05f, 0.05f, 0.10f, 0.88f);
    private static readonly Color BORDER_COLOR = new(0.40f, 0.85f, 1.00f, 0.65f);
    private static readonly Color TITLE_COLOR  = new(0.45f, 0.90f, 1.00f);
    private static readonly Color LABEL_COLOR  = new(0.78f, 0.78f, 0.82f);
    private static readonly Color VALUE_COLOR  = Color.white;

    void Awake()
    {
        arCamera = Camera.main;
        BuildUI();
        canvasGroup.alpha = 0f;
        canvas.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!canvas.gameObject.activeSelf) return;
        if (arCamera == null) arCamera = Camera.main;
        if (arCamera == null) return;

        // Position the panel above the component in SCREEN space (using camera.up), not
        // in marker-local space. This way the panel always appears above the component
        // regardless of how the marker is oriented (flat on a table vs vertical on a screen).
        canvas.transform.position = transform.position + arCamera.transform.up * verticalOffset;

        // Billboard: canvas +Z points away from camera so its face is visible. Use the
        // camera's up vector so the panel rolls with the camera — critical for handheld AR
        // where the device is rarely held perfectly upright.
        Vector3 toPanel = canvas.transform.position - arCamera.transform.position;
        if (toPanel.sqrMagnitude > 0.0001f)
            canvas.transform.rotation = Quaternion.LookRotation(toPanel, arCamera.transform.up);
    }

    public void Show(string title, IList<ComponentController.SpecEntry> specs)
    {
        canvas.gameObject.SetActive(true);
        titleText.text = title;
        RebuildSpecs(specs);
        StartFade(targetAlpha: 1f, deactivateOnComplete: false);
    }

    public void Hide()
    {
        StartFade(targetAlpha: 0f, deactivateOnComplete: true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI construction (one-time at Awake)
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildUI()
    {
        // Root: world-space Canvas
        var canvasGo = new GameObject("SpecUI_Canvas");
        canvasGo.transform.SetParent(transform, worldPositionStays: false);
        canvasGo.transform.localPosition = new Vector3(0f, verticalOffset, 0f);

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 4f;  // sharper text in world space
        scaler.referencePixelsPerUnit = 100f;

        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;  // never swallow taps on the component itself

        // Convert physical meters → design pixels, then scale canvas down so 1mm = 1px maps correctly
        var canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = panelSizeMeters * CANVAS_PIXELS_PER_METER;
        canvasRect.localScale = Vector3.one / CANVAS_PIXELS_PER_METER;

        // Background panel
        var bg = AddRectChild(canvasGo, "Background", anchorMin: Vector2.zero, anchorMax: Vector2.one);
        var bgImage = bg.gameObject.AddComponent<Image>();
        bgImage.color = BG_COLOR;
        bgImage.raycastTarget = false;

        // Border (thin colored rect just inside the bg)
        var border = AddRectChild(canvasGo, "Border",
            anchorMin: new Vector2(0.005f, 0.005f),
            anchorMax: new Vector2(0.995f, 0.995f));
        var borderImg = border.gameObject.AddComponent<Image>();
        borderImg.color = new Color(0, 0, 0, 0);  // transparent fill
        var outline = border.gameObject.AddComponent<Outline>();
        outline.effectColor = BORDER_COLOR;
        outline.effectDistance = new Vector2(3, -3);

        // Title row (top ~20%)
        var title = AddRectChild(canvasGo, "Title",
            anchorMin: new Vector2(0.02f, 0.78f),
            anchorMax: new Vector2(0.98f, 0.98f));
        titleText = title.gameObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "Component";
        titleText.fontSize = 30;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = TITLE_COLOR;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 18;
        titleText.fontSizeMax = 36;
        titleText.raycastTarget = false;

        // Title underline (thin rect just below title)
        var underline = AddRectChild(canvasGo, "TitleUnderline",
            anchorMin: new Vector2(0.10f, 0.755f),
            anchorMax: new Vector2(0.90f, 0.770f));
        var underlineImg = underline.gameObject.AddComponent<Image>();
        underlineImg.color = BORDER_COLOR;
        underlineImg.raycastTarget = false;

        // Specs container (bottom ~73%)
        var specsGo = AddRectChild(canvasGo, "Specs",
            anchorMin: new Vector2(0.05f, 0.04f),
            anchorMax: new Vector2(0.95f, 0.74f));
        specsContainer = specsGo;

        var vlg = specsGo.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
    }

    private static RectTransform AddRectChild(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, worldPositionStays: false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Spec rows (rebuilt each time Show is called — supports dynamic content)
    // ─────────────────────────────────────────────────────────────────────────
    private void RebuildSpecs(IList<ComponentController.SpecEntry> specs)
    {
        // Clear existing rows
        for (int i = specsContainer.childCount - 1; i >= 0; i--)
            Destroy(specsContainer.GetChild(i).gameObject);
        if (specs == null) return;

        foreach (var entry in specs)
        {
            var rowGo = new GameObject($"Row_{entry.label}");
            rowGo.transform.SetParent(specsContainer, worldPositionStays: false);
            rowGo.AddComponent<RectTransform>();

            var le = rowGo.AddComponent<LayoutElement>();
            le.preferredHeight = 22;

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // Label cell (fixed width)
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rowGo.transform, worldPositionStays: false);
            var labelLE = labelGo.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 75;
            labelLE.flexibleWidth = 0;
            var labelText = labelGo.AddComponent<TextMeshProUGUI>();
            labelText.text = entry.label;
            labelText.fontSize = 14;
            labelText.color = LABEL_COLOR;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.raycastTarget = false;

            // Value cell (flexible width — fills remaining space)
            var valueGo = new GameObject("Value");
            valueGo.transform.SetParent(rowGo.transform, worldPositionStays: false);
            var valueLE = valueGo.AddComponent<LayoutElement>();
            valueLE.flexibleWidth = 1;
            var valueText = valueGo.AddComponent<TextMeshProUGUI>();
            valueText.text = entry.value;
            valueText.fontSize = 14;
            valueText.color = VALUE_COLOR;
            valueText.fontStyle = FontStyles.Bold;
            valueText.alignment = TextAlignmentOptions.MidlineLeft;
            valueText.textWrappingMode = TextWrappingModes.Normal;
            valueText.overflowMode = TextOverflowModes.Ellipsis;
            valueText.raycastTarget = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Fade in/out
    // ─────────────────────────────────────────────────────────────────────────
    private void StartFade(float targetAlpha, bool deactivateOnComplete)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, deactivateOnComplete));
    }

    private IEnumerator FadeRoutine(float target, bool deactivateOnComplete)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, target, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = target;
        if (deactivateOnComplete && Mathf.Approximately(target, 0f))
            canvas.gameObject.SetActive(false);
        fadeCoroutine = null;
    }
}
