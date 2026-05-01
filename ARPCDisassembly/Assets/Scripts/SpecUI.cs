using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space HUD that displays the currently disassembled component's specs.
/// Singleton — one panel for the entire scene, anchored at the bottom of the screen.
/// Always renders on top of 3D content (no depth occlusion issues like world-space UI had).
///
/// Activation flow:
///   ComponentController.Disassemble()  → SpecUI.Instance.Show(this, name, specs)
///   ComponentController.Assemble()     → SpecUI.Instance.Hide(this)
///
/// Owner tracking (via the `object owner` parameter) prevents one component's Hide()
/// call from clearing another component's panel: if the user disassembles A, then B
/// (panel switches to B), then assembles A, A's Hide() is ignored because B is still
/// the active owner.
/// </summary>
public class SpecUI : MonoBehaviour
{
    private static SpecUI _instance;
    public static SpecUI Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[SpecUI Singleton]");
                _instance = go.AddComponent<SpecUI>();
            }
            return _instance;
        }
    }

    [Tooltip("Seconds to fade panel in/out.")]
    [Range(0.05f, 2f)] [SerializeField] private float fadeDuration = 0.30f;

    // --- Internal cached refs (built at Awake) ---
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private TextMeshProUGUI titleText;
    private RectTransform specsContainer;
    private Coroutine fadeCoroutine;
    private object currentOwner;

    // --- Visual style constants ---
    private static readonly Color BG_COLOR     = new(0.05f, 0.05f, 0.10f, 0.92f);
    private static readonly Color BORDER_COLOR = new(0.40f, 0.85f, 1.00f, 0.65f);
    private static readonly Color TITLE_COLOR  = new(0.45f, 0.90f, 1.00f);
    private static readonly Color LABEL_COLOR  = new(0.78f, 0.78f, 0.82f);
    private static readonly Color VALUE_COLOR  = Color.white;

    void Awake()
    {
        // If a singleton already exists (e.g. duplicate SpecUI components left over on
        // prefabs from the previous world-space architecture), destroy this one silently.
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Detach from any prefab parent so we stay alive even when a marker is lost
        // (Vuforia disables the marker prefab, which would otherwise disable us too).
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        BuildUI();
        canvasGroup.alpha = 0f;
        canvas.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            if (canvas != null) Destroy(canvas.gameObject);
            _instance = null;
        }
    }

    public void Show(object owner, string title, IList<ComponentController.SpecEntry> specs)
    {
        currentOwner = owner;
        canvas.gameObject.SetActive(true);
        titleText.text = title;
        RebuildSpecs(specs);
        StartFade(targetAlpha: 1f, deactivateOnComplete: false);
    }

    public void Hide(object owner)
    {
        // Only hide if the caller is the current owner — prevents stale Hide() calls from
        // clobbering the panel when another component took over.
        if (currentOwner != owner) return;
        currentOwner = null;
        StartFade(targetAlpha: 0f, deactivateOnComplete: true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI construction (one-time at Awake)
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildUI()
    {
        // Canvas at SCENE ROOT (not parented to this transform) — guarantees the HUD stays
        // alive even if this script lives on a prefab whose marker gets lost. ScreenSpaceOverlay
        // auto-renders on top of all 3D content, so no overlay shaders needed.
        var canvasGo = new GameObject("SpecUI_Canvas");
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;  // never swallow taps on the AR view behind
        canvasGroup.interactable = false;

        // Panel — bottom-anchored, auto-grows upward to fit content. Using ContentSizeFitter
        // + VerticalLayoutGroup so the panel adapts to whichever component's specs are showing
        // (motherboard has 6 single-line rows; PSU has 6 rows with multi-line wrapped values).
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, worldPositionStays: false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.04f, 0f);
        panelRect.anchorMax = new Vector2(0.96f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0, 80);  // 80 design-px above screen bottom (above home indicator)

        var bgImage = panelGo.AddComponent<Image>();
        bgImage.color = BG_COLOR;
        bgImage.raycastTarget = false;

        var outline = panelGo.AddComponent<Outline>();
        outline.effectColor = BORDER_COLOR;
        outline.effectDistance = new Vector2(3, -3);

        var panelVLG = panelGo.AddComponent<VerticalLayoutGroup>();
        panelVLG.padding = new RectOffset(28, 28, 24, 24);
        panelVLG.spacing = 10;
        panelVLG.childAlignment = TextAnchor.UpperCenter;
        panelVLG.childControlWidth = true;
        panelVLG.childControlHeight = true;
        panelVLG.childForceExpandWidth = true;
        panelVLG.childForceExpandHeight = false;

        var panelFitter = panelGo.AddComponent<ContentSizeFitter>();
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Title (auto-positioned by panel VLG)
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panelGo.transform, worldPositionStays: false);
        titleGo.AddComponent<RectTransform>();
        var titleLE = titleGo.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 80;
        titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = "Component";
        titleText.fontSize = 60;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = TITLE_COLOR;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 32;
        titleText.fontSizeMax = 72;
        titleText.raycastTarget = false;

        // Title underline (thin horizontal bar)
        var underlineGo = new GameObject("TitleUnderline");
        underlineGo.transform.SetParent(panelGo.transform, worldPositionStays: false);
        underlineGo.AddComponent<RectTransform>();
        var underlineLE = underlineGo.AddComponent<LayoutElement>();
        underlineLE.preferredHeight = 4;
        var underlineImg = underlineGo.AddComponent<Image>();
        underlineImg.color = BORDER_COLOR;
        underlineImg.raycastTarget = false;

        // Specs container — its rows auto-size based on text content (so a row with a
        // wrapped multi-line value grows tall enough to show both lines).
        var specsGo = new GameObject("Specs");
        specsGo.transform.SetParent(panelGo.transform, worldPositionStays: false);
        specsContainer = specsGo.AddComponent<RectTransform>();

        var specsVLG = specsGo.AddComponent<VerticalLayoutGroup>();
        specsVLG.spacing = 6;
        specsVLG.padding = new RectOffset(8, 8, 12, 4);
        specsVLG.childAlignment = TextAnchor.UpperLeft;
        specsVLG.childControlWidth = true;
        specsVLG.childControlHeight = true;
        specsVLG.childForceExpandWidth = true;
        specsVLG.childForceExpandHeight = false;

        var specsFitter = specsGo.AddComponent<ContentSizeFitter>();
        specsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
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
        for (int i = specsContainer.childCount - 1; i >= 0; i--)
            Destroy(specsContainer.GetChild(i).gameObject);
        if (specs == null) return;

        foreach (var entry in specs)
        {
            var rowGo = new GameObject($"Row_{entry.label}");
            rowGo.transform.SetParent(specsContainer, worldPositionStays: false);
            rowGo.AddComponent<RectTransform>();

            var le = rowGo.AddComponent<LayoutElement>();
            le.minHeight = 48;  // floor — short rows still readable

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14;
            hlg.childAlignment = TextAnchor.UpperLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Row auto-grows tall enough to fit a wrapped multi-line value
            var rowFitter = rowGo.AddComponent<ContentSizeFitter>();
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Label cell — fixed width
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rowGo.transform, worldPositionStays: false);
            var labelLE = labelGo.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 280;
            labelLE.flexibleWidth = 0;
            var labelText = labelGo.AddComponent<TextMeshProUGUI>();
            labelText.text = entry.label;
            labelText.fontSize = 32;
            labelText.color = LABEL_COLOR;
            labelText.alignment = TextAlignmentOptions.TopLeft;
            labelText.raycastTarget = false;

            // Value cell — flexible width, wraps onto multiple lines if needed
            var valueGo = new GameObject("Value");
            valueGo.transform.SetParent(rowGo.transform, worldPositionStays: false);
            var valueLE = valueGo.AddComponent<LayoutElement>();
            valueLE.flexibleWidth = 1;
            var valueText = valueGo.AddComponent<TextMeshProUGUI>();
            valueText.text = entry.value;
            valueText.fontSize = 32;
            valueText.color = VALUE_COLOR;
            valueText.fontStyle = FontStyles.Bold;
            valueText.alignment = TextAlignmentOptions.TopLeft;
            valueText.textWrappingMode = TextWrappingModes.Normal;
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
