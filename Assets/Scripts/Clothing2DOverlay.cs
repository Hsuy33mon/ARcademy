using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
public class Clothing2DOverlay : MonoBehaviour
{
    [Header("References")]
    public BodyDataReceiver body;
    public Canvas canvas;
    public RectTransform clothingRect;

    [Header("Smoothing")]
    [Range(0f, 30f)] public float smoothing = 12f;

    [Header("Layout")]
    public bool mirrorX = true;   // match your webcam (selfie view => true)

    [Header("Timeout")]
    public float hideAfterSecondsWithoutData = 0.5f;

    [Header("Fit (tweak these)")]
    public float widthMultiplier = 1.35f;
    public float neckUpFromShoulders = 0.06f;
    public float collarDown = 0.02f;
    public bool rotateWithShoulders = true;

    const int LShoulder = 11, RShoulder = 12, LHip = 23, RHip = 24;

    float lastFreshDataTime;
    Image _img;
    RectTransform _canvasRect;

    void Awake()
    {
        if (!clothingRect) clothingRect = GetComponent<RectTransform>();
        _img = clothingRect ? clothingRect.GetComponent<Image>() : null;
        if (clothingRect) clothingRect.pivot = new Vector2(0.5f, 1f); // hang from collar
    }

    void Start()
    {
        _canvasRect = canvas ? (RectTransform)canvas.transform : null;
        if (clothingRect) clothingRect.gameObject.SetActive(true);
    }

    void Update()
    {
        if (!clothingRect || !_canvasRect || _img == null || body == null) return;

        // --- take an atomic snapshot of landmarks (avoids race while UDP updates) ---
        List<Vector3> src = body.bodyLandmarks;
        Vector3[] L = (src != null) ? src.ToArray() : null;

        bool ok = body.bodyDetected && body.IsDataValid && L != null && L.Length >= 33; // need full pose set
        if (ok) lastFreshDataTime = Time.time;

        bool show = Time.time - lastFreshDataTime <= hideAfterSecondsWithoutData;
        if (!show) { if (clothingRect.gameObject.activeSelf) clothingRect.gameObject.SetActive(false); return; }
        else if (!clothingRect.gameObject.activeSelf) clothingRect.gameObject.SetActive(true);

        // --- safe indexing (we know L.Length >= 33) ---
        Vector3 lmLS = L[LShoulder];
        Vector3 lmRS = L[RShoulder];
        Vector3 lmLH = L[LHip];
        Vector3 lmRH = L[RHip];

        // positions use mirror
        Vector2 sL = ToScreen(lmLS, mirrorX);
        Vector2 sR = ToScreen(lmRS, mirrorX);
        Vector2 hL = ToScreen(lmLH, mirrorX);
        Vector2 hR = ToScreen(lmRH, mirrorX);

        Vector2 shoulderCenter = (sL + sR) * 0.5f;
        Vector2 hipCenter      = (hL + hR) * 0.5f;
        Vector2 upDir          = (shoulderCenter - hipCenter).normalized;
        float torsoLen         = Vector2.Distance(shoulderCenter, hipCenter);

        // neck/top-of-image anchor
        Vector2 neckApprox = shoulderCenter + upDir * (neckUpFromShoulders * torsoLen) - upDir * (collarDown * torsoLen);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, neckApprox, null, out var localPos);
        clothingRect.anchoredPosition = Vector2.Lerp(clothingRect.anchoredPosition, localPos, Time.deltaTime * smoothing);

        // width from shoulder distance
        float targetWidth = Vector2.Distance(sL, sR) * widthMultiplier;
        float aspect = _img.sprite ? (_img.sprite.rect.height / _img.sprite.rect.width) : 1.6f;
        Vector2 sizeTarget = new Vector2(targetWidth, targetWidth * aspect);
        clothingRect.sizeDelta = Vector2.Lerp(clothingRect.sizeDelta, sizeTarget, Time.deltaTime * (smoothing * 0.6f));

        // --- mirror-safe rotation: compute from NON-mirrored points; negate if mirrored ---
        Vector2 sL_nom = ToScreen(lmLS, false);
        Vector2 sR_nom = ToScreen(lmRS, false);
        Vector2 dir    = (sR_nom.x >= sL_nom.x) ? (sR_nom - sL_nom) : (sL_nom - sR_nom);
        float angleZ   = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (mirrorX) angleZ = -angleZ;

        float z = rotateWithShoulders ? Mathf.LerpAngle(clothingRect.localEulerAngles.z, angleZ, Time.deltaTime * smoothing) : 0f;
        clothingRect.localRotation = Quaternion.AngleAxis(z, Vector3.forward); // lock X/Y to 0
    }

    Vector2 ToScreen(Vector3 lm, bool mirrored)
    {
        float x = mirrored ? (1f - lm.x) : lm.x;
        float y = 1f - lm.y;
        return new Vector2(Mathf.Clamp01(x) * Screen.width, Mathf.Clamp01(y) * Screen.height);
    }

    public void ToggleActive() => clothingRect.gameObject.SetActive(!clothingRect.gameObject.activeSelf);
}
