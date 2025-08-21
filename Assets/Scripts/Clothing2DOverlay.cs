using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
public class Clothing2DOverlay : MonoBehaviour
{
    [Header("References")]
    public BodyDataReceiver body;
    public Canvas canvas;                 // ← assign your Overlaycanvas
    public RectTransform clothingRect;    // ← the shirt Image's RectTransform (or a clean parent)

    [Header("Smoothing")]
    [Range(0f, 30f)] public float smoothing = 12f;

    [Header("Layout")]
    public bool mirrorX = false;           // selfie view => true

    [Header("Timeout")]
    public float hideAfterSecondsWithoutData = 0.5f;

    [Header("Fit (tweak)")]
    public float widthMultiplier = 1.35f;
    public float neckUpFromShoulders = 0.06f;
    public float collarDown = 0.02f;
    public bool rotateWithShoulders = true;

    // MediaPipe Pose indices
    const int LShoulder = 11, RShoulder = 12, LHip = 23, RHip = 24;

    float lastFreshDataTime;
    RectTransform _canvasRect;
    Camera _uiCam;            // ← correct camera for UI mapping
    Image _img;

    void Awake()
    {
        if (!clothingRect) clothingRect = GetComponent<RectTransform>();
        _img = clothingRect ? clothingRect.GetComponent<Image>() : null;

        // Hang from collar
        if (clothingRect) clothingRect.pivot = new Vector2(0.5f, 1f);
    }

    void Start()
    {
        _canvasRect = canvas ? (RectTransform)canvas.transform : null;
        _uiCam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? (canvas.worldCamera != null ? canvas.worldCamera : Camera.main)
                : null;

        if (clothingRect) clothingRect.gameObject.SetActive(true);
        if (clothingRect) { clothingRect.localEulerAngles = Vector3.zero; clothingRect.localScale = Vector3.one; }
    }

    void Update()
    {
        if (!clothingRect || _canvasRect == null || _img == null || body == null) return;

        // Atomic snapshot (avoids race during UDP write)
        List<Vector3> src = body.bodyLandmarks;
        Vector3[] L = (src != null) ? src.ToArray() : null;

        bool ok = body.bodyDetected && body.IsDataValid && L != null && L.Length >= 33;
        if (ok) lastFreshDataTime = Time.time;

        bool show = Time.time - lastFreshDataTime <= hideAfterSecondsWithoutData;
        if (!show) { if (clothingRect.gameObject.activeSelf) clothingRect.gameObject.SetActive(false); return; }
        else if (!clothingRect.gameObject.activeSelf) clothingRect.gameObject.SetActive(true);

        // Safe indexing
        Vector3 lmLS = L[LShoulder], lmRS = L[RShoulder], lmLH = L[LHip], lmRH = L[RHip];

        // Positions (mirrored if selfie)
        Vector2 sL = ToScreen(lmLS, mirrorX);
        Vector2 sR = ToScreen(lmRS, mirrorX);
        Vector2 hL = ToScreen(lmLH, mirrorX);
        Vector2 hR = ToScreen(lmRH, mirrorX);

        Vector2 shoulderCenter = (sL + sR) * 0.5f;
        Vector2 hipCenter      = (hL + hR) * 0.5f;
        Vector2 upDir          = (shoulderCenter - hipCenter).normalized;
        float   torsoLen       = Vector2.Distance(shoulderCenter, hipCenter);

        // Neck/top-of-image anchor
        Vector2 neck = shoulderCenter + upDir * (neckUpFromShoulders * torsoLen) - upDir * (collarDown * torsoLen);

        // Screen → Canvas local (use _uiCam if not Overlay)
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, neck, _uiCam, out var localPos);
        clothingRect.anchoredPosition = Vector2.Lerp(clothingRect.anchoredPosition, localPos, Time.deltaTime * smoothing);

        // Size from shoulder width
        float targetWidth = Vector2.Distance(sL, sR) * widthMultiplier;
        float aspect = _img.sprite ? (_img.sprite.rect.height / _img.sprite.rect.width) : 1.6f;
        Vector2 sizeTarget = new Vector2(targetWidth, targetWidth * aspect);
        clothingRect.sizeDelta = Vector2.Lerp(clothingRect.sizeDelta, sizeTarget, Time.deltaTime * (smoothing * 0.6f));

        // Rotation: compute from NON-mirrored points; negate if mirrored
        Vector2 sL_nom = ToScreen(lmLS, false);
        Vector2 sR_nom = ToScreen(lmRS, false);
        Vector2 dir    = (sR_nom.x >= sL_nom.x) ? (sR_nom - sL_nom) : (sL_nom - sR_nom);
        float angleZ   = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (mirrorX) angleZ = -angleZ;

        float z = rotateWithShoulders ? Mathf.LerpAngle(clothingRect.localEulerAngles.z, angleZ, Time.deltaTime * smoothing) : 0f;
        clothingRect.localRotation = Quaternion.AngleAxis(z, Vector3.forward); // lock X/Y
    }

    Vector2 ToScreen(Vector3 lm, bool mirrored)
    {
        float x = mirrored ? (1f - lm.x) : lm.x; // Mediapipe x in [0..1], origin top-left
        float y = 1f - lm.y;
        return new Vector2(Mathf.Clamp01(x) * Screen.width, Mathf.Clamp01(y) * Screen.height);
    }

    public void ToggleActive() => clothingRect.gameObject.SetActive(!clothingRect.gameObject.activeSelf);
}
