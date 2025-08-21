using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
public class Helmet2DOverlay : MonoBehaviour
{
    [Header("References")]
    public BodyDataReceiver body;
    public Canvas canvas;
    public RectTransform helmetRect;

    [Header("Smoothing")]
    [Range(0f, 30f)] public float smoothing = 12f;

    [Header("Timeout")]
    public float hideAfterSecondsWithoutData = 0.5f;

    [Header("Layout")]
    public bool mirrorX = false;

    [Header("Fit (tweak)")]
    public float widthMultiplier = 1.60f;  // ear width -> helmet width
    public float upFromEars = 0.60f;       // +up from ear center (in ear widths)
    public float crownDown  = 0.05f;       // small settle down
    public bool rotateWithEars = true;

    // Mediapipe Pose indices (we only need up to 24 here)
    const int LEyeOut = 3, REyeOut = 6, LEar = 7, REar = 8, LShoulder = 11, RShoulder = 12, LHip = 23, RHip = 24;

    float lastFreshDataTime;
    RectTransform _canvasRect;
    Camera _uiCam;
    Image _img;

    void Awake()
    {
        if (!helmetRect) helmetRect = GetComponent<RectTransform>();
        if (!canvas)     canvas     = GetComponentInParent<Canvas>();
        _img = helmetRect ? helmetRect.GetComponent<Image>() : null;

        // Good UI defaults
        if (helmetRect)
        {
            helmetRect.anchorMin = helmetRect.anchorMax = new Vector2(0.5f, 0.5f); // center anchors
            helmetRect.pivot     = new Vector2(0.5f, 0.0f);                        // sit on head from bottom-center
            helmetRect.localEulerAngles = Vector3.zero;
            helmetRect.localScale       = Vector3.one;
        }
    }

    void Start()
    {
        _canvasRect = canvas ? (RectTransform)canvas.transform : null;
        _uiCam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
               ? (canvas.worldCamera != null ? canvas.worldCamera : Camera.main)
               : null;

        if (helmetRect) helmetRect.gameObject.SetActive(true);
    }

    void Update()
    {
        if (!helmetRect || _canvasRect == null || _img == null || body == null) return;

        // --- snapshot landmarks (avoid race while UDP writes) ---
        List<Vector3> src = body.bodyLandmarks;
        Vector3[] L = (src != null) ? src.ToArray() : null;

        // We need indices up to 24 -> require at least 25 points
        bool have = body.bodyDetected && body.IsDataValid && L != null && L.Length >= 25;
        if (have) lastFreshDataTime = Time.time;

        bool show = (Time.time - lastFreshDataTime) <= hideAfterSecondsWithoutData;
        if (!show) { SetVisible(false); return; }
        SetVisible(true);

        // If this frame is incomplete, keep last pose (don't index)
        if (!have) return;

        // --- Safe indexing from here ---
        Vector3 lmEL = L[LEar], lmER = L[REar], lmSL = L[LShoulder], lmSR = L[RShoulder], lmHL = L[LHip], lmHR = L[RHip];

        // Positions (mirror for placement)
        Vector2 earL = ToScreen(lmEL, mirrorX);
        Vector2 earR = ToScreen(lmER, mirrorX);
        Vector2 shL  = ToScreen(lmSL, mirrorX);
        Vector2 shR  = ToScreen(lmSR, mirrorX);
        Vector2 hipL = ToScreen(lmHL, mirrorX);
        Vector2 hipR = ToScreen(lmHR, mirrorX);

        Vector2 earCenter = (earL + earR) * 0.5f;
        float   earWidth  = Mathf.Max(4f, Vector2.Distance(earL, earR)); // avoid zero

        Vector2 torsoUp   = ((shL + shR) * 0.5f - (hipL + hipR) * 0.5f).normalized;
        Vector2 crown     = earCenter + torsoUp * (upFromEars * earWidth) - torsoUp * (crownDown * earWidth);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, crown, _uiCam, out var local);
        helmetRect.anchoredPosition = Vector2.Lerp(helmetRect.anchoredPosition, local, Time.deltaTime * smoothing);

        // Size
        float targetWidth = earWidth * widthMultiplier;
        float aspect = _img.sprite ? (_img.sprite.rect.height / _img.sprite.rect.width) : 1.0f;
        Vector2 sizeTarget = new Vector2(targetWidth, targetWidth * aspect);
        helmetRect.sizeDelta = Vector2.Lerp(helmetRect.sizeDelta, sizeTarget, Time.deltaTime * (smoothing * 0.6f));

        // Rotation from NON-mirrored ears; fallback to eyes/shoulders; negate when mirrored
        Vector2 eL_nom = ToScreen(lmEL, false);
        Vector2 eR_nom = ToScreen(lmER, false);
        if (Vector2.Distance(eL_nom, eR_nom) < 5f) { eL_nom = ToScreen(L[LEyeOut], false); eR_nom = ToScreen(L[REyeOut], false); }
        if (Vector2.Distance(eL_nom, eR_nom) < 5f) { eL_nom = ToScreen(lmSL,    false); eR_nom = ToScreen(lmSR,    false); }

        Vector2 dir = (eR_nom.x >= eL_nom.x) ? (eR_nom - eL_nom) : (eL_nom - eR_nom); // enforce +X direction
        float angleZ = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (mirrorX) angleZ = -angleZ;

        float z = rotateWithEars ? Mathf.LerpAngle(helmetRect.localEulerAngles.z, angleZ, Time.deltaTime * smoothing) : 0f;
        helmetRect.localRotation = Quaternion.AngleAxis(z, Vector3.forward); // lock X/Y
    }

    void SetVisible(bool on)
    {
        if (!helmetRect) return;
        if (helmetRect.gameObject.activeSelf != on) helmetRect.gameObject.SetActive(on);
    }

    Vector2 ToScreen(Vector3 lm, bool mirrored)
    {
        float x = mirrored ? (1f - lm.x) : lm.x;
        float y = 1f - lm.y;
        return new Vector2(Mathf.Clamp01(x) * Screen.width, Mathf.Clamp01(y) * Screen.height);
    }
}
