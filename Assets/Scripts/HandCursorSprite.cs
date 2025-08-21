using UnityEngine;
using UnityEngine.UI;
 
[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class HandCursorSprite : MonoBehaviour
{
    [Header("Refs")]
    public BodyDataReceiver body;             // drag BodyTracker here
    public RectTransform cursor;
 
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
    [Range(0f, 1f)] public float hideGraceSec = 0.30f;
 
    [Header("Smoothing (1€ filter)")]
    public float minCutoff = 7.0f;
    public float beta      = 0.55f;
    public float dCutoff   = 1.5f;
 
    [Header("Prediction")]
    [Range(0f, 0.02f)] public float extraLead = 0.006f;
    [Range(0.0f, 0.2f)] public float maxPredictStep01 = 0.06f;
 
    [Header("Misc")]
    public bool mirrorX = false;
    public Vector2 pixelOffset = Vector2.zero;
 
    // NEW: threshold to ignore stationary hand
    [Header("Hand Movement Filter")]
    public float movementThreshold = 0.02f;   // adjust sensitivity
 
    Image img;
    Vector2 xHat, dxHat;
    bool hasPrev;
    int lastMoveFrame = -1;
    bool wasPresent;
    float lastPresentTime;
 
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
        }
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
 
    float Alpha(float cutoff, float dt)
    {
        float tau = 1f / (2f * Mathf.PI * Mathf.Max(0.0001f, cutoff));
        return 1f / (1f + tau / Mathf.Max(0.0001f, dt));
    }
 
    void MoveNow()
    {
        if (lastMoveFrame == Time.frameCount) return;
        lastMoveFrame = Time.frameCount;
 
        float now = Time.unscaledTime;
 
        bool fresh = body && body.IsDataValid;
        if (!fresh && (now - lastPresentTime) > hideGraceSec)
        { SetVisible(false); hasPrev = false; wasPresent = false; return; }
        if (!fresh) { SetVisible(true); ApplyPosition(xHat); return; }
 
        Vector2 p01 = body.pointerPosition;
        if (mirrorX) p01.x = 1f - p01.x;
 
        // -------- Ignore if hand hardly moved --------
        Vector2 delta = p01 - xHat;
        if (hasPrev && delta.magnitude < movementThreshold)
        {
            return;
        }
        // ---------------------------------------------
 
        float dist = Mathf.Max(Mathf.Abs(p01.x - 0.5f), Mathf.Abs(p01.y - 0.5f));
        bool presentNow = wasPresent
            ? (dist > hideDeadzone01) || (pinchCountsAsPresence && body.is_pinching)
            : (dist > showDeadzone01) || (pinchCountsAsPresence && body.is_pinching);
 
        if (!presentNow)
        {
            if ((now - lastPresentTime) > hideGraceSec)
            { SetVisible(false); hasPrev = false; wasPresent = false; return; }
            SetVisible(true); ApplyPosition(xHat); return;
        }
 
        wasPresent = true;
        lastPresentTime = now;
        SetVisible(true);
 
        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        if (!hasPrev) { xHat = p01; dxHat = Vector2.zero; hasPrev = true; }
        else
        {
            Vector2 dxRaw = (p01 - xHat) / dt;
            dxHat = Vector2.Lerp(dxHat, dxRaw, Alpha(dCutoff, dt));
            float cutoff = Mathf.Max(0.01f, minCutoff + beta * dxHat.magnitude);
            xHat = Vector2.Lerp(xHat, p01, Alpha(cutoff, dt));
        }
 
        float sampleAge = Mathf.Clamp(now - body.lastUpdateTime, 0f, 0.05f);
        float leadSec   = sampleAge + extraLead;
 
        Vector2 lead = xHat + dxHat * leadSec;
        Vector2 step = lead - xHat;
        step.x = Mathf.Clamp(step.x, -maxPredictStep01, maxPredictStep01);
        step.y = Mathf.Clamp(step.y, -maxPredictStep01, maxPredictStep01);
 
        Vector2 lead01 = new Vector2(
            Mathf.Clamp01(xHat.x + step.x),
            Mathf.Clamp01(xHat.y + step.y)
        );
 
        // ---- Shift from fingertip to palm center ----
        Vector2 palmShift01 = body.is_pinching ? new Vector2(0f, -0.03f) : new Vector2(0f, -0.07f);
        if (mirrorX) palmShift01.x = -palmShift01.x;
 
        Vector2 target01 = new Vector2(
            Mathf.Clamp01(lead01.x + palmShift01.x),
            Mathf.Clamp01(lead01.y + palmShift01.y)
        );
 
        ApplyPosition(target01);
 
        bool pinching = body.is_pinching;
        if (img) img.sprite = pinching && pinchSprite ? pinchSprite : palmSprite;
 
        if (overrideSizeInScript) ApplySize(pinching);
    }
 
    void ApplySize(bool pinching)
    {
        if (!cursor) return;
        if (usePercent)
        {
            float h = Screen.height * (pinching ? pinchH : normalH);
            cursor.sizeDelta = new Vector2(h, h);
        }
        else
        {
            cursor.sizeDelta = pinching ? pinchSizePx : normalSizePx;
        }
    }
 
    void ApplyPosition(Vector2 p01)
    {
        cursor.position = new Vector2(p01.x * Screen.width, p01.y * Screen.height) + pixelOffset;
    }
 
    void SetVisible(bool on)
    {
        if (!img) return;
        var c = img.color; c.a = on ? 1f : 0f; img.color = c;
    }
}