using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
public class Glasses2DOverlay : MonoBehaviour
{
    [Header("References")]
    public BodyDataReceiver body;
    public Canvas canvas;
    public RectTransform glassesRect;

    [Header("Smoothing")]
    [Range(0f, 30f)] public float smoothing = 12f;

    [Header("Timeout")]
    public float hideAfterSecondsWithoutData = 0.5f;

    [Header("Layout")]
    public bool mirrorX = false;

    [Header("Fit (tweak)")]
    public float widthMultiplier = 1.15f;
    public float verticalOffset = -0.05f;
    public bool rotateWithEyes = false;

    // Indices
    const int LEyeOut = 3, REyeOut = 6, LShoulder = 11, RShoulder = 12, LHip = 23, RHip = 24;

    float lastFreshDataTime;
    RectTransform _canvasRect;
    Camera _uiCam;
    Image _img;

    void Awake()
    {
        if (!glassesRect) glassesRect = GetComponent<RectTransform>();
        _img = glassesRect ? glassesRect.GetComponent<Image>() : null;
        if (glassesRect) glassesRect.pivot = new Vector2(0.5f, 0.5f);
    }

    void Start()
    {
        _canvasRect = canvas ? (RectTransform)canvas.transform : null;
        _uiCam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? (canvas.worldCamera != null ? canvas.worldCamera : Camera.main)
                : null;

        if (glassesRect) glassesRect.gameObject.SetActive(true);
        if (glassesRect) { glassesRect.localEulerAngles = Vector3.zero; glassesRect.localScale = Vector3.one; }
    }

    void Update()
    {
        if (!glassesRect || _canvasRect == null || _img == null || body == null) return;

        // Snapshot
        List<Vector3> src = body.bodyLandmarks;
        Vector3[] L = (src != null) ? src.ToArray() : null;

        bool ok = body.bodyDetected && body.IsDataValid && L != null && L.Length >= 33;
        if (ok) lastFreshDataTime = Time.time;

        bool show = Time.time - lastFreshDataTime <= hideAfterSecondsWithoutData;
        if (!show) { if (glassesRect.gameObject.activeSelf) glassesRect.gameObject.SetActive(false); return; }
        else if (!glassesRect.gameObject.activeSelf) glassesRect.gameObject.SetActive(true);

        // Safe indexing
        Vector3 lmEL = L[LEyeOut], lmER = L[REyeOut], lmSL = L[LShoulder], lmSR = L[RShoulder], lmHL = L[LHip], lmHR = L[RHip];

        // Positions (mirrored)
        Vector2 eL  = ToScreen(lmEL, mirrorX);
        Vector2 eR  = ToScreen(lmER, mirrorX);
        Vector2 shL = ToScreen(lmSL, mirrorX);
        Vector2 shR = ToScreen(lmSR, mirrorX);
        Vector2 hipL= ToScreen(lmHL, mirrorX);
        Vector2 hipR= ToScreen(lmHR, mirrorX);

        Vector2 eyeCenter = (eL + eR) * 0.5f;
        float   eyeWidth  = Vector2.Distance(eL, eR);

        // Stable up from torso
        Vector2 torsoUp = ((shL + shR) * 0.5f - (hipL + hipR) * 0.5f).normalized;

        Vector2 targetScreen = eyeCenter + torsoUp * (verticalOffset * eyeWidth);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, targetScreen, _uiCam, out var local);
        glassesRect.anchoredPosition = Vector2.Lerp(glassesRect.anchoredPosition, local, Time.deltaTime * smoothing);

        // Size
        float targetWidth = eyeWidth * widthMultiplier;
        float aspect = _img.sprite ? (_img.sprite.rect.height / _img.sprite.rect.width) : 0.35f;
        Vector2 sizeTarget = new Vector2(targetWidth, targetWidth * aspect);
        glassesRect.sizeDelta = Vector2.Lerp(glassesRect.sizeDelta, sizeTarget, Time.deltaTime * (smoothing * 0.6f));

        // Rotation from NON-mirrored eyes; negate if mirrored
        Vector2 eL_nom = ToScreen(lmEL, false);
        Vector2 eR_nom = ToScreen(lmER, false);
        Vector2 dir    = (eR_nom.x >= eL_nom.x) ? (eR_nom - eL_nom) : (eL_nom - eR_nom);
        float angleZ   = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (mirrorX) angleZ = -angleZ;

        float z = rotateWithEyes ? Mathf.LerpAngle(glassesRect.localEulerAngles.z, angleZ, Time.deltaTime * smoothing) : 0f;
        glassesRect.localRotation = Quaternion.AngleAxis(z, Vector3.forward);
    }

    Vector2 ToScreen(Vector3 lm, bool mirrored)
    {
        float x = mirrored ? (1f - lm.x) : lm.x;
        float y = 1f - lm.y;
        return new Vector2(Mathf.Clamp01(x) * Screen.width, Mathf.Clamp01(y) * Screen.height);
    }
}
