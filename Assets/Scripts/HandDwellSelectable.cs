using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[DefaultExecutionOrder(10000)]
public class HandDwellSelectable : MonoBehaviour
{
    [Header("Inputs")]
    public BodyDataReceiver body;
    public RectTransform targetRect;
    public bool mirrorX = false;

    [Header("Canvas/Camera (auto if empty)")]
    public Canvas canvas;
    public RectTransform viewport;
    Camera uiCam;

    [Header("Ring UI")]
    public Image progressRing;                // Image -> Filled -> Radial360
    public bool showRingOnlyOnHover = true;

    [Header("Behavior")]
    [Tooltip("Seconds hand must dwell before selection fires")]
    public float dwellSeconds = 2.0f;
    [Tooltip("Seconds to EMPTY fully once decay starts")]
    public float resetSeconds = 2.0f;

    [Header("Presence gate")]
    [Tooltip("Turn ON if BodyDataReceiver exposes hand flags (recommended).")]
    public bool requireHandDetected = true;
    public bool allowPointerAsPresence = false; // only used if requireHandDetected = true
    public bool onlyPalm = true;
    public float pinchDebounceSec = 0.20f;

    [Header("Stability")]
    public float enterPaddingPx = 60f;
    public float exitPaddingPx  = 40f;
    public float graceSeconds   = 0.40f;
    public float handSmoothTime = 0.10f;

    [Header("Palm reveal delay")]
    [Tooltip("Ring appears and progress starts only after palm is stable on this item for this many seconds.")]
    public float palmDelaySeconds = 3.0f;
    public bool resetProgressOnPinch = true;

    [Header("Anti-Auto-Select")]
    [Tooltip("Require pointer to have been OUTSIDE once after cooldown before starting dwell")]
    public bool requireEnterFromOutside = true;
    [Tooltip("Block dwell for a short time right after enable/menu build")]
    public float spawnBlockSeconds = 0.75f;

    [Header("After Select")]
    public float cooldownAfterSelect = 0.75f;

    [Header("Action")]
    public UnityEvent onSelected;

    // runtime
    float holdTime;
    bool  hovering;
    float lastInsideTime;
    float cooldownUntil;
    float spawnBlockUntil;
    bool  wasOutsideSinceCooldown;

    Vector2 smoothedScreenPos, smoothedVel;
    float lastPinchTime = -999f;

    // palm-stability gate
    float palmOnThisItemStart = -1f;   // when palm became valid while inside this item
    bool  ringReadyThisFrame;          // ring visibility gate this frame

    void Reset()
    {
        targetRect = GetComponent<RectTransform>();
        if (!progressRing)
        {
            var tr = transform.Find("ring") ?? transform.Find("Ring");
            if (tr) progressRing = tr.GetComponent<Image>();
        }
    }

    void Awake()
    {
        if (!targetRect) targetRect = (RectTransform)transform;
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        uiCam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
              ? (canvas.worldCamera != null ? canvas.worldCamera : Camera.main)
              : null;

        if (!progressRing)
        {
            var tr = transform.Find("ring") ?? transform.Find("Ring");
            if (tr) progressRing = tr.GetComponent<Image>();
        }

        if (progressRing)
        {
            progressRing.raycastTarget = false;
            progressRing.type = Image.Type.Filled;
            progressRing.fillMethod = Image.FillMethod.Radial360;
            progressRing.fillAmount = 0f;
            progressRing.enabled = !showRingOnlyOnHover;
            progressRing.transform.SetAsLastSibling();
        }

        spawnBlockUntil = Time.unscaledTime + Mathf.Max(0f, spawnBlockSeconds);
        wasOutsideSinceCooldown = false;
    }

    void Update()
    {
        // cooldown/initial block
        if (Time.unscaledTime < cooldownUntil || Time.unscaledTime < spawnBlockUntil)
        { DecayAndDraw(); return; }

        if (body == null || targetRect == null || !body.IsDataValid)
        { DecayAndDraw(); return; }

        // -------- pinch gate --------
        if (onlyPalm && body.is_pinching)
        {
            lastPinchTime = Time.unscaledTime;
            if (resetProgressOnPinch)
            {
                holdTime = 0f;
                palmOnThisItemStart = -1f;
            }
            DrawRing(0f, false); // hide ring during pinch
            return;
        }
        if (onlyPalm && (Time.unscaledTime - lastPinchTime) < pinchDebounceSec)
        {
            // brief cooldown after pinch ends
            DrawRing(Mathf.Clamp01(holdTime / dwellSeconds), false);
            return;
        }
        // ----------------------------

        // screen-space pointer
        Vector2 p01 = body.pointerPosition;
        if (mirrorX) p01.x = 1f - p01.x;
        p01 = Vector2.Min(Vector2.one, Vector2.Max(Vector2.zero, p01));
        Vector2 rawScreen = new Vector2(p01.x * Screen.width, p01.y * Screen.height);

        // smooth pointer
        smoothedScreenPos = Vector2.SmoothDamp(
            smoothedScreenPos, rawScreen, ref smoothedVel,
            Mathf.Max(0.0001f, handSmoothTime), Mathf.Infinity, Time.unscaledDeltaTime
        );

        // viewport guard
        bool insideViewport = true;
        if (viewport)
            insideViewport = RectTransformUtility.RectangleContainsScreenPoint(viewport, smoothedScreenPos, uiCam);

        // hysteresis on this item
        bool insideEnter = insideViewport && RectContainsScreenPointPadded(targetRect, smoothedScreenPos, uiCam, enterPaddingPx);
        bool insideExit  = insideViewport && RectContainsScreenPointPadded(targetRect, smoothedScreenPos, uiCam, exitPaddingPx);
        bool insideNow   = hovering ? insideExit : insideEnter;

        if (!insideNow) wasOutsideSinceCooldown = true;
        if (insideNow)  lastInsideTime = Time.unscaledTime;

        // presence using BodyDataReceiver (with grace)
        bool presenceNow;
        if (requireHandDetected)
        {
            presenceNow = body.HandPresentWithGrace(0.35f);
            if (!presenceNow && allowPointerAsPresence) presenceNow = body.IsDataValid;
        }
        else
        {
            presenceNow = body.HandPresentWithGrace(0.35f);
        }

        // --- palm stability window on THIS item ---
        ringReadyThisFrame = false;
        if (insideNow && presenceNow && (!requireEnterFromOutside || wasOutsideSinceCooldown))
        {
            if (palmOnThisItemStart < 0f) palmOnThisItemStart = Time.unscaledTime;
            float palmHeld = Time.unscaledTime - palmOnThisItemStart;

            if (palmHeld >= palmDelaySeconds)
            {
                ringReadyThisFrame = true; // ring may show and progress may start
            }
        }
        else
        {
            // any interruption resets the palm-stability timer
            palmOnThisItemStart = -1f;
        }

        if (ringReadyThisFrame)
        {
            hovering = true;
            holdTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(holdTime / dwellSeconds);
            DrawRing(t, true);

            if (t >= 1f)
            {
                GetComponent<Button>()?.onClick?.Invoke();
                onSelected?.Invoke();
                StartCooldown(cooldownAfterSelect);
            }
        }
        else
        {
            // While waiting for the palm delay, keep progress at zero and ring hidden.
            bool outsideForLong = (Time.unscaledTime - lastInsideTime) > graceSeconds;
            if (outsideForLong) { DecayAndDraw(); }  // (holdTime likely zero)
            else                { DrawRing(0f, false); }
        }
    }

    void DecayAndDraw()
    {
        if (holdTime > 0f)
        {
            float decayPerSec = dwellSeconds / Mathf.Max(0.001f, resetSeconds);
            holdTime = Mathf.Max(0f, holdTime - decayPerSec * Time.unscaledDeltaTime);
        }
        hovering = false;
        DrawRing(Mathf.Clamp01(holdTime / dwellSeconds), false);
    }

    void DrawRing(float amount, bool allowVisible)
    {
        if (!progressRing) return;
        progressRing.fillAmount = amount;
        // Ring is visible only when allowed (after palm delay) and hovering (or has >0)
        bool shouldShow = allowVisible && (hovering || amount > 0f);
        progressRing.enabled = showRingOnlyOnHover ? shouldShow : allowVisible;
    }

    // Called by UI flow and after select
    public void StartCooldown(float seconds)
    {
        float s = Mathf.Max(0f, seconds);
        cooldownUntil = Time.unscaledTime + s;
        spawnBlockUntil = Mathf.Max(spawnBlockUntil, cooldownUntil);

        holdTime = 0f;
        hovering = false;
        wasOutsideSinceCooldown = false;

        palmOnThisItemStart = -1f; // must hold palm again
        DrawRing(0f, false);
    }

    static bool RectContainsScreenPointPadded(RectTransform rt, Vector2 screenPt, Camera cam, float padPx)
    {
        if (padPx <= 0f)
            return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPt, cam);

        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, c[0]); // bottom-left
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, c[2]); // top-right

        Rect r = Rect.MinMaxRect(
            min.x - padPx, min.y - padPx,
            max.x + padPx, max.y + padPx
        );
        return r.Contains(screenPt);
    }
}
