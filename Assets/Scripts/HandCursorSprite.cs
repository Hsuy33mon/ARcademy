using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class HandCursorSprite : MonoBehaviour
{
    [Header("Refs")]
    public BodyDataReceiver body;
    public RectTransform cursor;
    public Canvas canvas; // auto-detected if empty

    [Header("Sprites")]
    public Sprite palmSprite;
    public Sprite pinchSprite;

    [Header("Size Control")]
    public bool overrideSizeInScript = false;
    public bool usePercent = false;
    [Range(0.005f, 0.2f)] public float normalH = 0.045f;
    [Range(0.005f, 0.2f)] public float pinchH  = 0.055f;
    public Vector2 normalSizePx = new Vector2(80, 80);
    public Vector2 pinchSizePx  = new Vector2(95, 95);

    [Header("Presence")]
    [Range(0f, 0.2f)] public float showDeadzone01 = 0.010f;
    [Range(0f, 0.2f)] public float hideDeadzone01 = 0.020f;
    public bool pinchCountsAsPresence = true;
    [Range(0f, 1f)] public float hideGraceSec = 0.40f;

    [Header("Smoothing (1€)")]
    public float minCutoff = 6.5f; // snappier
    public float beta      = 0.7f;
    public float dCutoff   = 6.0f;

    [Header("Prediction")]
    public float extraLead = 0.006f;
    public float maxPredictStep01 = 0.02f;

    [Header("Misc")]
    public bool mirrorX = false;               // PYTHON already mirrored
    public Vector2 palmOffsetPx  = new Vector2(0, -60);
    public Vector2 pinchOffsetPx = new Vector2(0, -40);

    [Header("Noise Gate (0..1)")]
    public float movementThreshold01 = 0.003f;

    [Header("Anti-Blink / Fade")]
    public float validGraceSec    = 0.80f;
    public float minVisibleSec    = 0.20f;
    public float appearFadeSec    = 0.06f;
    public float disappearFadeSec = 0.10f;

    [Header("Pinch Debounce / Hysteresis")]
    public float pinchSmoothSec = 0.06f;
    [Range(0f,1f)] public float pinchOnThreshold  = 0.65f;
    [Range(0f,1f)] public float pinchOffThreshold = 0.35f;

    Image img;
    Vector2 xHat, dxHat;
    bool hasPrev, wasPresent;
    int lastMoveFrame = -1;
    float lastSeenTime, lastValidTime, lastVisibleOnTime;
    float curAlpha = 0f, alphaVel = 0f;
    float pinchLP = 0f; bool pinchStable = false;
    Camera _uiCam;

    void Awake()
    {
        if (!cursor) cursor = (RectTransform)transform;
        img = GetComponent<Image>();
        if (img)
        {
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.type = Image.Type.Simple;
            if (palmSprite) img.sprite = palmSprite;
            SetAlphaImmediate(0f);
        }
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        _uiCam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
               ? (canvas.worldCamera ? canvas.worldCamera : Camera.main)
               : null;

        if (overrideSizeInScript) ApplySize(false);
    }

    void OnEnable()
    {
        Canvas.willRenderCanvases += MoveNow;
        Application.onBeforeRender += MoveNow;
    }

    void OnDisable()
    {
        Canvas.willRenderCanvases -= MoveNow;
        Application.onBeforeRender -= MoveNow;
    }

    float AlphaCoef(float cutoff, float dt)
    {
        float tau = 1f / (2f * Mathf.PI * Mathf.Max(0.0001f, cutoff));
        return 1f / (1f + tau / Mathf.Max(0.0001f, dt));
    }

    void MoveNow()
    {
        if (lastMoveFrame == Time.frameCount) return;
        lastMoveFrame = Time.frameCount;

        float now = Time.unscaledTime;
        float dt  = Mathf.Max(0.0001f, Time.unscaledDeltaTime);

        bool fresh = body && body.IsDataValid;
        if (fresh) lastValidTime = now;
        bool withinValidGrace = (!fresh && (now - lastValidTime) <= validGraceSec);

        if (!fresh && !withinValidGrace)
        {
            float sinceSeen  = now - lastSeenTime;
            float sinceShown = now - lastVisibleOnTime;
            if (sinceSeen > hideGraceSec && sinceShown >= minVisibleSec) FadeTo(0f, disappearFadeSec, dt);
            else { FadeTo(1f, appearFadeSec, dt); ApplyPosition(xHat, pinchStable); }
            return;
        }

        // Input
        Vector2 p01 = body.pointerPosition;
        if (mirrorX) p01.x = 1f - p01.x;
        p01 = new Vector2(Mathf.Clamp01(p01.x), Mathf.Clamp01(p01.y));

        // Debounce pinch (LP + hysteresis), even though Python already helps
        float target = (body != null && body.is_pinching) ? 1f : 0f;
        float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, pinchSmoothSec));
        pinchLP = Mathf.Lerp(pinchLP, target, k);
        if (!pinchStable && pinchLP >= pinchOnThreshold)  pinchStable = true;
        if ( pinchStable && pinchLP <= pinchOffThreshold) pinchStable = false;

        // Presence around center (or via pinch)
        float dist = Mathf.Max(Mathf.Abs(p01.x - 0.5f), Mathf.Abs(p01.y - 0.5f));
        bool presentNow = wasPresent
            ? (dist > hideDeadzone01) || (pinchStable && pinchCountsAsPresence)
            : (dist > showDeadzone01) || (pinchStable && pinchCountsAsPresence);

        if (!presentNow)
        {
            float sinceSeen  = now - lastSeenTime;
            float sinceShown = now - lastVisibleOnTime;
            if (sinceSeen > hideGraceSec && sinceShown >= minVisibleSec) FadeTo(0f, disappearFadeSec, dt);
            else { FadeTo(1f, appearFadeSec, dt); ApplyPosition(xHat, pinchStable); }
            return;
        }

        wasPresent   = true;
        lastSeenTime = now;
        FadeTo(1f, appearFadeSec, dt);

        // 1€ filtering (no hard freeze on small moves)
        if (!hasPrev) { xHat = p01; dxHat = Vector2.zero; hasPrev = true; }
        else
        {
            Vector2 delta = p01 - xHat;
            if (delta.magnitude < movementThreshold01) p01 = xHat + delta * 0.25f;

            Vector2 dxRaw = (p01 - xHat) / dt;
            dxHat = Vector2.Lerp(dxHat, dxRaw, AlphaCoef(dCutoff, dt));
            float cutoff = Mathf.Max(0.01f, minCutoff + beta * dxHat.magnitude);
            xHat = Vector2.Lerp(xHat, p01, AlphaCoef(cutoff, dt));
        }

        // Latency compensation + limited prediction
        float sampleAge = Mathf.Clamp(now - body.lastUpdateTime, 0f, 0.05f);
        float leadSec   = sampleAge + extraLead;
        Vector2 lead    = xHat + dxHat * leadSec;
        Vector2 step    = lead - xHat;
        step.x = Mathf.Clamp(step.x, -maxPredictStep01, maxPredictStep01);
        step.y = Mathf.Clamp(step.y, -maxPredictStep01, maxPredictStep01);
        Vector2 lead01  = new Vector2(Mathf.Clamp01(xHat.x + step.x), Mathf.Clamp01(xHat.y + step.y));

        ApplyPosition(lead01, pinchStable);

        if (img) img.sprite = (pinchStable && pinchSprite) ? pinchSprite : palmSprite;
        if (overrideSizeInScript) ApplySize(pinchStable);
    }

    void FadeTo(float targetAlpha, float fadeTime, float dt)
    {
        if (!img) return;
        if (targetAlpha > 0.99f && curAlpha < 0.99f) lastVisibleOnTime = Time.unscaledTime;
        float smooth = Mathf.Max(0.0001f, fadeTime);
        curAlpha = Mathf.SmoothDamp(curAlpha, targetAlpha, ref alphaVel, smooth, Mathf.Infinity, dt);
        var c = img.color; c.a = curAlpha; img.color = c;
    }

    void SetAlphaImmediate(float a)
    {
        curAlpha = Mathf.Clamp01(a); alphaVel = 0f;
        if (!img) return; var c = img.color; c.a = curAlpha; img.color = c;
    }

    void ApplySize(bool pinching)
    {
        if (!cursor) return;
        if (usePercent)
        {
            float h = Screen.height * (pinching ? pinchH : normalH);
            cursor.sizeDelta = new Vector2(h, h);
        }
        else cursor.sizeDelta = pinching ? pinchSizePx : normalSizePx;
    }

    void ApplyPosition(Vector2 p01, bool pinching)
    {
        if (!cursor) return;
        RectTransform parentRect = cursor.parent as RectTransform;
        if (!parentRect) return;

        Vector2 screenPoint = new Vector2(p01.x * Screen.width, p01.y * Screen.height);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, _uiCam, out var localPoint))
        {
            Vector2 offset = pinching ? pinchOffsetPx : palmOffsetPx;
            cursor.anchoredPosition = localPoint + offset;
        }
    }
}
