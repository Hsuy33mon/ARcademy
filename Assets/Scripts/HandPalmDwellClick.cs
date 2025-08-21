using UnityEngine;
using UnityEngine.UI;

public class HandPalmDwellClick : MonoBehaviour
{
    [Header("Refs")]
    public BodyDataReceiver body;
    public Canvas canvas;
    public RectTransform cursor;     // optional (just for centering the progress ring)
    public Image progressRing;       // optional: Image set to Filled/Radial 360

    [Header("Dwell Settings")]
    public float dwellSeconds = 5f;      // hold palm this long to click
    public float maxPointerSpeed = 1200f; // px/sec; moving too fast cancels dwell
    public bool requirePalm = true;       // true = open palm only (not pinch/fist)

    [Header("Scope (where to click)")]
    public RectTransform clickAreaRoot;   // e.g., your MenuBar Content (optional; if null, searches whole Canvas)

    RectTransform _canvasRect;
    Camera _uiCam;
    Button _hovered;
    float _dwell;
    Vector2 _lastScreen;

    void Start()
    {
        _canvasRect = (RectTransform)canvas.transform;
        _uiCam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
               ? null
               : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);

        // Hide ring at start
        if (progressRing) { progressRing.type = Image.Type.Filled; progressRing.fillMethod = Image.FillMethod.Radial360; progressRing.fillAmount = 0f; progressRing.raycastTarget = false; }
    }

    void Update()
    {
        if (!body || !body.IsDataValid) return;

        // Hand pointer 0..1 -> screen px
        Vector2 sp = new Vector2(body.pointerPosition.x * Screen.width,
                                 body.pointerPosition.y * Screen.height);

        // Position progress ring at cursor (if assigned)
        if (progressRing)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, sp, _uiCam, out var local);
            progressRing.rectTransform.anchoredPosition = local;
        }

        // Movement guard
        float speed = (Time.unscaledDeltaTime > 0f) ? (sp - _lastScreen).magnitude / Time.unscaledDeltaTime : 0f;
        _lastScreen = sp;

        // Find hovered Button (only under clickAreaRoot if provided)
        Button hit = FindHoveredButton(sp);
        if (hit != _hovered)
        {
            _hovered = hit;
            _dwell = 0f;
            if (progressRing) progressRing.fillAmount = 0f;
        }

        // Palm condition: not pinching, not fist (fallback if is_fist not provided)
        bool palmOk = !requirePalm || IsPalm(body);
        bool stable = speed <= maxPointerSpeed;

        if (_hovered && palmOk && stable)
        {
            _dwell += Time.unscaledDeltaTime;
            if (progressRing) progressRing.fillAmount = Mathf.Clamp01(_dwell / dwellSeconds);

            if (_dwell >= dwellSeconds)
            {
                _hovered.onClick.Invoke(); // trigger the button
                _dwell = 0f;
                if (progressRing) progressRing.fillAmount = 0f;
            }
        }
        else
        {
            _dwell = 0f;
            if (progressRing) progressRing.fillAmount = 0f;
        }
    }

    Button FindHoveredButton(Vector2 screenPoint)
    {
        // Limit search to a subtree (faster, safer), else scan whole canvas
        var root = clickAreaRoot ? clickAreaRoot : (RectTransform)canvas.transform;
        var buttons = root.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            if (!b || !b.gameObject.activeInHierarchy || !b.interactable) continue;
            var rt = (RectTransform)b.transform;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, _uiCam))
                return b;
        }
        return null;
    }

    bool IsPalm(BodyDataReceiver b)
    {
        // Best: if Python sends is_palm / is_fist, use them.
        // Fallback: “palm” = not pinching and not fist (if field exists).
        bool fist = false;
        try { fist = b.is_fist; } catch { /* field may not exist */ }
        return !b.is_pinching && !fist;
    }

    // Call this after you rebuild your menu bar (faculties -> careers) if you change the root
    public void SetClickRoot(RectTransform newRoot) => clickAreaRoot = newRoot;
}
