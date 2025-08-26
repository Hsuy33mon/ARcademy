using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class HandPinchSelectable : MonoBehaviour
{
    [Header("Input/Space")]
    public BodyDataReceiver body;
    public Canvas canvas;
    public RectTransform viewport;
    public bool mirrorX = true;

    [Header("Hitbox")]
    public float enterPaddingPx = 10f;
    public float exitPaddingPx  = 6f;

    [Header("Visuals")]
    public Image highlight;  // background to tint
    public Color normal = new Color(0.18f,0.18f,0.18f,1f);
    public Color hover  = new Color(0.28f,0.28f,0.28f,1f);
    public Color down   = new Color(0.45f,0.45f,0.45f,1f);

    [Header("Timing")]
    public float cooldownAfterPinch = 0.25f;
    public float smoothTime = 0.08f;

    [Header("Event")]
    public UnityEvent onPinch;

    RectTransform _rt;
    Camera _uiCam;
    Vector2 _sm, _vel;
    bool _hovering, _prevPinch;
    float _coolUntil;

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

    void Update()
    {
        if (!body || !body.IsDataValid) { SetColor(normal); return; }
        if (Time.unscaledTime < _coolUntil) { SetColor(normal); return; }

        // pointer 0..1 → screen
        Vector2 p01 = body.pointerPosition;
        if (mirrorX) p01.x = 1f - p01.x;
        var raw = new Vector2(Mathf.Clamp01(p01.x) * Screen.width,
                              Mathf.Clamp01(p01.y) * Screen.height);
        _sm = Vector2.SmoothDamp(_sm, raw, ref _vel, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        // optional viewport fence
        if (viewport && !RectTransformUtility.RectangleContainsScreenPoint(viewport, _sm, _uiCam))
        { _hovering = false; SetColor(normal); _prevPinch = false; return; }

        // padded hit test
        bool insideEnter = RectContainsPadded(_rt, _sm, _uiCam, enterPaddingPx);
        bool insideExit  = RectContainsPadded(_rt, _sm, _uiCam, exitPaddingPx);
        bool insideNow   = _hovering ? insideExit : insideEnter;

        if (insideNow)
        {
            _hovering = true;
            bool pinching = body.is_pinching;

            if (pinching && !_prevPinch)  // pinch edge
            {
                SetColor(down);
                onPinch?.Invoke();
                StartCooldown(cooldownAfterPinch);
            }
            else
            {
                SetColor(hover);
            }
            _prevPinch = pinching;
        }
        else
        {
            _hovering = false;
            _prevPinch = false;
            SetColor(normal);
        }
    }

    public void StartCooldown(float sec)
    {
        _coolUntil = Time.unscaledTime + Mathf.Max(0f, sec);
        _prevPinch = false;
        _hovering  = false;
        SetColor(normal);
    }

    static bool RectContainsPadded(RectTransform rt, Vector2 screen, Camera cam, float pad)
    {
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
        Rect r = Rect.MinMaxRect(min.x - pad, min.y - pad, max.x + pad, max.y + pad);
        return r.Contains(screen);
    }

    void SetColor(Color c) { if (highlight) highlight.color = c; }
}
