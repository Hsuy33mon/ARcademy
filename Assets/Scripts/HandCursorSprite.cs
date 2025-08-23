using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class HandCursorSprite : MonoBehaviour
{
    // Public inspector fields
    [Header("Refs")]
    public BodyDataReceiver body;
    public RectTransform cursor;

    [Header("Sprites")]
    public Sprite palmSprite;
    public Sprite pinchSprite;

    [Header("Size Control")]
    public bool overrideSizeInScript = false;
    public bool usePercent = false;
    [Range(0.005f, 0.2f)] public float normalH = 0.045f;
    [Range(0.005f, 0.2f)] public float pinchH = 0.055f;
    public Vector2 normalSizePx = new Vector2(80, 80);
    public Vector2 pinchSizePx = new Vector2(95, 95);

    [Header("Presence")]
    [Range(0f, 0.2f)] public float showDeadzone01 = 0.010f;
    [Range(0f, 0.2f)] public float hideDeadzone01 = 0.020f;
    public bool pinchCountsAsPresence = true;
    [Range(0f, 1f)] public float hideGraceSec = 0.30f;

    [Header("Smoothing (1€ filter)")]
    public float minCutoff = 7.0f;
    public float beta = 0.55f;
    public float dCutoff = 1.5f;

    [Header("Prediction")]
    public float extraLead = 0.006f;
    public float maxPredictStep01 = 0.06f;

    [Header("Misc")]
    public bool mirrorX = false;
    public Vector2 palmOffsetPx = new Vector2(0, -60);
    public Vector2 pinchOffsetPx = new Vector2(0, -40);

    [Header("Hand Movement Filter")]
    public float movementThreshold = 0.02f;

    [Header("Anti-Blink / Fade")]
    public float validGraceSec = 0.50f;
    public float minVisibleSec = 0.35f;
    public float appearFadeSec = 0.06f;
    public float disappearFadeSec = 0.12f;

    // Private member variables
    Image img;
    Vector2 xHat, dxHat;
    bool hasPrev;
    int lastMoveFrame = -1;
    bool wasPresent;
    float lastSeenTime;
    float lastValidTime;
    float lastVisibleOnTime;
    float curAlpha = 0f;
    float alphaVel = 0f;

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
        if (overrideSizeInScript) ApplySize(false);
    }

    void OnEnable() { Canvas.willRenderCanvases += MoveNow; Application.onBeforeRender += MoveNow; }
    void OnDisable() { Canvas.willRenderCanvases -= MoveNow; Application.onBeforeRender -= MoveNow; }

    // Helper functions
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
        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        bool fresh = body && body.IsDataValid;
        if (fresh) lastValidTime = now;

        bool withinValidGrace = (!fresh && (now - lastValidTime) <= validGraceSec);
        if (!fresh && !withinValidGrace)
        {
            float sinceSeen = now - lastSeenTime;
            float sinceShown = now - lastVisibleOnTime;
            if (sinceSeen > hideGraceSec && sinceShown >= minVisibleSec) FadeTo(0f, disappearFadeSec, dt);
            else { FadeTo(1f, appearFadeSec, dt); ApplyPosition(xHat); }
            return;
        }

        Vector2 p01 = body.pointerPosition;
        if (mirrorX) p01.x = 1f - p01.x;

        Vector2 delta = p01 - xHat;
        if (hasPrev && delta.magnitude < movementThreshold)
        { wasPresent = true; lastSeenTime = now; FadeTo(1f, appearFadeSec, dt); ApplyPosition(xHat); return; }

        float dist = Mathf.Max(Mathf.Abs(p01.x - 0.5f), Mathf.Abs(p01.y - 0.5f));
        bool presentNow = wasPresent
            ? (dist > hideDeadzone01) || (body.is_pinching && pinchCountsAsPresence)
            : (dist > showDeadzone01) || (body.is_pinching && pinchCountsAsPresence);

        if (!presentNow)
        {
            float sinceSeen = now - lastSeenTime;
            float sinceShown = now - lastVisibleOnTime;
            if (sinceSeen > hideGraceSec && sinceShown >= minVisibleSec) FadeTo(0f, disappearFadeSec, dt);
            else { FadeTo(1f, appearFadeSec, dt); ApplyPosition(xHat); }
            return;
        }

        wasPresent = true;
        lastSeenTime = now;
        FadeTo(1f, appearFadeSec, dt);

        if (!hasPrev) { xHat = p01; dxHat = Vector2.zero; hasPrev = true; }
        else
        {
            Vector2 dxRaw = (p01 - xHat) / dt;
            dxHat = Vector2.Lerp(dxHat, dxRaw, AlphaCoef(dCutoff, dt));
            float cutoff = Mathf.Max(0.01f, minCutoff + beta * dxHat.magnitude);
            xHat = Vector2.Lerp(xHat, p01, AlphaCoef(cutoff, dt));
        }

        float sampleAge = Mathf.Clamp(now - body.lastUpdateTime, 0f, 0.05f);
        float leadSec = sampleAge + extraLead;
        Vector2 lead = xHat + dxHat * leadSec;
        Vector2 step = lead - xHat;
        step.x = Mathf.Clamp(step.x, -maxPredictStep01, maxPredictStep01);
        step.y = Mathf.Clamp(step.y, -maxPredictStep01, maxPredictStep01);
        Vector2 lead01 = new Vector2(Mathf.Clamp01(xHat.x + step.x), Mathf.Clamp01(xHat.y + step.y));

        bool pinching = body.is_pinching;
        ApplyPosition(lead01, pinching);
        if (img) img.sprite = (pinching && pinchSprite) ? pinchSprite : palmSprite;
        if (overrideSizeInScript) ApplySize(pinching);
    }

    void FadeTo(float targetAlpha, float fadeTime, float dt)
    {
        if (!img) return;
        if (targetAlpha > 0.99f && curAlpha < 0.99f) lastVisibleOnTime = Time.unscaledTime;
        float smooth = Mathf.Max(0.0001f, fadeTime);
        curAlpha = Mathf.SmoothDamp(curAlpha, targetAlpha, ref alphaVel, smooth, Mathf.Infinity, dt);
        var c = img.color;
        c.a = curAlpha;
        img.color = c;
    }

    void SetAlphaImmediate(float a)
    {
        curAlpha = Mathf.Clamp01(a);
        alphaVel = 0f;
        if (!img) return;
        var c = img.color;
        c.a = curAlpha;
        img.color = c;
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

    // The key change is here: we now convert from screen space to local anchored position
    void ApplyPosition(Vector2 p01, bool pinching)
    {
        if (!cursor) return;

        // Get the parent of the cursor's RectTransform, which is the Canvas or another UI element
        RectTransform parentRect = cursor.parent.GetComponent<RectTransform>();
        if (!parentRect) return;

        // Convert the normalized 0..1 coordinates to screen-space pixel coordinates
        Vector2 screenPoint = new Vector2(p01.x * Screen.width, p01.y * Screen.height);

        // Convert the screen point to a local point within the parent's RectTransform
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, null, out localPoint))
        {
            // Apply the offset based on the current state (pinch vs palm)
            Vector2 offset = pinching ? pinchOffsetPx : palmOffsetPx;
            cursor.anchoredPosition = localPoint + offset;
        }
    }

    void ApplyPosition(Vector2 p01)
    {
        ApplyPosition(p01, body ? body.is_pinching : false);
    }
}