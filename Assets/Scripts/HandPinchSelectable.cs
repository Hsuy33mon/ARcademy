using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class HandPinchSelectable : MonoBehaviour
{
    [Header("Input/Space")]
    public BodyDataReceiver body;
    public Canvas canvas;                 // assign SAME canvas as the key (ModelCanvas)
    public RectTransform viewport;
    public bool mirrorX = false;          // Python already mirrored

    [Header("Hitbox Padding (pixels)")]
    public Vector2 enterPaddingPx = new Vector2(2f, 6f); // small horiz, a bit more vertical
    public Vector2 exitPaddingPx  = new Vector2(1f, 4f);

    [Header("Exclusive Hover")]
    [Tooltip("Only one selectable (closest center) can hover/click per frame")]
    public bool exclusiveHover = true;

    [Header("Visuals")]
    public Image highlight;
    public Color normal = new Color(0.18f,0.18f,0.18f,1f);
    public Color hover  = new Color(0.28f,0.28f,0.28f,1f);
    public Color down   = new Color(0.45f,0.45f,0.45f,1f);

    [Header("Timing")]
    public float blockOnEnableSec = 0.35f;      // ignore right after enable
    public float smoothTime = 0.08f;
    public bool  requireFreshEnter = true;      // must leave once then re-enter
    public float minHoverBeforePinchSec = 0.08f;// tiny dwell before click
    public float cooldownAfterPinch = 0.25f;

    [Header("Pinch Debounce / Hysteresis")]
    public float pinchSmoothSec = 0.06f;
    [Range(0f,1f)] public float pinchOnThreshold  = 0.65f;
    [Range(0f,1f)] public float pinchOffThreshold = 0.35f;

    [Header("Event")]
    public UnityEvent onPinch;

    RectTransform _rt;
    Camera _uiCam;
    Vector2 _sm, _vel;
    bool _hovering;
    float _coolUntil;
    float _justEnabledUntil;
    float _hoverSince = -1f;

    // pinch state
    float _pinchLP = 0f;
    bool  _pinchStable = false;

    // fresh-enter gate
    bool _everExited = false;

    void Awake()
    {
        _rt = (RectTransform)transform;
        if (!highlight) highlight = GetComponent<Image>();
        var can = canvas ? canvas : GetComponentInParent<Canvas>();
        _uiCam = (can && can.renderMode != RenderMode.ScreenSpaceOverlay)
               ? (can.worldCamera ? can.worldCamera : Camera.main)
               : null;
        SetColor(normal);
    }

    void OnEnable()
    {
        _justEnabledUntil = Time.unscaledTime + Mathf.Max(0f, blockOnEnableSec);
        _coolUntil = 0f;
        _hoverSince = -1f;
        _everExited = false;
        _pinchLP = 0f;
        _pinchStable = false;
        _hovering = false;
        SetColor(normal);
    }

    void Update()
    {
        if (!body || !body.IsDataValid) { SetColor(normal); return; }

        float now = Time.unscaledTime;
        if (now < _justEnabledUntil) { SetColor(normal); return; }
        if (now < _coolUntil)       { SetColor(normal); return; }

        // pointer 0..1 → screen
        Vector2 p01 = body.pointerPosition;
        if (mirrorX) p01.x = 1f - p01.x;
        var raw = new Vector2(Mathf.Clamp01(p01.x) * Screen.width,
                              Mathf.Clamp01(p01.y) * Screen.height);
        _sm = Vector2.SmoothDamp(_sm, raw, ref _vel, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        // optional viewport fence
        if (viewport && !RectTransformUtility.RectangleContainsScreenPoint(viewport, _sm, _uiCam))
        { ResetHover(); SetColor(normal); return; }

        // padded hit test with enter/exit hysteresis
        bool insideEnter = RectContainsPadded(_rt, _sm, _uiCam, enterPaddingPx);
        bool insideExit  = RectContainsPadded(_rt, _sm, _uiCam, exitPaddingPx);
        bool insideNow   = _hovering ? insideExit : insideEnter;

        // require fresh enter after enable
        if (requireFreshEnter && !_everExited)
        {
            if (!insideExit) _everExited = true; // user has left once
            SetColor(normal);
            ResetHover();
            return;
        }

        // EXCLUSIVE HOVER: only the closest center wins this frame
        if (exclusiveHover && insideNow)
        {
            float myDist = DistanceToCenterPx(_rt, _sm, _uiCam);
            if (!HoverResolver.Consider(this, myDist))
            {
                // someone closer is already the winner
                ResetHover();
                SetColor(normal);
                return;
            }
        }

        // debounce pinch (LP + hysteresis)
        float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.0001f, pinchSmoothSec));
        _pinchLP = Mathf.Lerp(_pinchLP, body.is_pinching ? 1f : 0f, k);
        if (!_pinchStable && _pinchLP >= pinchOnThreshold)  _pinchStable = true;
        if ( _pinchStable && _pinchLP <= pinchOffThreshold) _pinchStable = false;

        if (insideNow)
        {
            if (!_hovering) { _hovering = true; _hoverSince = now; }

            bool hoverReady = _hoverSince > 0f && (now - _hoverSince) >= minHoverBeforePinchSec;

            if (_pinchStable && hoverReady)
            {
                SetColor(down);
                onPinch?.Invoke();
                StartCooldown(cooldownAfterPinch);
                _pinchStable = false; // consume click
                _pinchLP = 0f;
                _hoverSince = -1f;
            }
            else
            {
                SetColor(hover);
            }
        }
        else
        {
            if (_hovering && !insideExit) _everExited = true;
            ResetHover();
            SetColor(normal);
        }
    }

    void ResetHover()
    {
        _hovering = false;
        _hoverSince = -1f;
    }

    public void StartCooldown(float sec)
    {
        _coolUntil = Time.unscaledTime + Mathf.Max(0f, sec);
        ResetHover();
        SetColor(normal);
    }

    static bool RectContainsPadded(RectTransform rt, Vector2 screen, Camera cam, Vector2 pad)
    {
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
        Rect r = Rect.MinMaxRect(min.x - pad.x, min.y - pad.y, max.x + pad.x, max.y + pad.y);
        return r.Contains(screen);
    }

    static float DistanceToCenterPx(RectTransform rt, Vector2 screen, Camera cam)
    {
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
        Vector2 center = (min + max) * 0.5f;
        return Vector2.Distance(screen, center);
    }

    void SetColor(Color c) { if (highlight) highlight.color = c; }

    // ---- global per-frame winner (closest center) ----
    static class HoverResolver
    {
        static int _frame = -1;
        static object _winner = null;
        static float _best = float.PositiveInfinity;

        public static bool Consider(object who, float dist)
        {
            int f = Time.frameCount;
            if (f != _frame) { _frame = f; _winner = null; _best = float.PositiveInfinity; }
            if (dist < _best || _winner == null)
            {
                _best = dist; _winner = who; return true;
            }
            return ReferenceEquals(_winner, who);
        }
    }
}
