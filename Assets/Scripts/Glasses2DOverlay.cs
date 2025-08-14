using UnityEngine;
using UnityEngine.UI;

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
    public bool mirrorX = false; // tick if your video is mirrored (selfie view)

    [Header("Fit (tweak)")]
    [Tooltip("Glasses width = eye-outer distance * this")]
    public float widthMultiplier = 1.15f;     // try 1.10–1.30
    [Tooltip("Move along face normal; +up, -down (as a fraction of eye width)")]
    public float verticalOffset = -0.05f;     // slight down from eye center
    public bool rotateWithEyes = true;

    // Pose landmark indices (MediaPipe Pose)
    const int LEyeOut = 3, REyeOut = 6;
    const int LEar = 7, REar = 8;
    const int LShoulder = 11, RShoulder = 12;
    const int LHip = 23, RHip = 24;

    float lastFreshDataTime;
    RectTransform canvasRect;
    Image img;

    void Awake()
    {
        if (!glassesRect) glassesRect = GetComponent<RectTransform>();
        img = glassesRect ? glassesRect.GetComponent<Image>() : null;

        if (glassesRect) glassesRect.pivot = new Vector2(0.5f, 0.5f); // rotate around center
    }

    void Start()
    {
        canvasRect = canvas ? canvas.transform as RectTransform : null;
        if (glassesRect) glassesRect.gameObject.SetActive(true);
    }

    void Update()
    {
        bool ok = body && body.bodyDetected && body.bodyLandmarks != null && body.bodyLandmarks.Count >= 25 && body.IsDataValid;
        if (ok) lastFreshDataTime = Time.time;
        if (!ok && Time.time - lastFreshDataTime > hideAfterSecondsWithoutData)
        {
            if (glassesRect && glassesRect.gameObject.activeSelf) glassesRect.gameObject.SetActive(false);
            return;
        }
        else if (glassesRect && !glassesRect.gameObject.activeSelf) glassesRect.gameObject.SetActive(true);

        if (!canvasRect || !glassesRect || img == null) return;

        // --- read pose landmarks ---
        Vector2 eL = ToScreen(body.bodyLandmarks[LEyeOut], mirrorX);
        Vector2 eR = ToScreen(body.bodyLandmarks[REyeOut], mirrorX);

        // Fallbacks if eyes unreliable: use ears/shoulders for "up" direction
        Vector2 earL = ToScreen(body.bodyLandmarks[LEar], mirrorX);
        Vector2 earR = ToScreen(body.bodyLandmarks[REar], mirrorX);
        Vector2 shL  = ToScreen(body.bodyLandmarks[LShoulder], mirrorX);
        Vector2 shR  = ToScreen(body.bodyLandmarks[RShoulder], mirrorX);
        Vector2 hipL = ToScreen(body.bodyLandmarks[LHip], mirrorX);
        Vector2 hipR = ToScreen(body.bodyLandmarks[RHip], mirrorX);

        Vector2 eyeCenter = (eL + eR) * 0.5f;
        float   eyeWidth  = Vector2.Distance(eL, eR);

        // Face "up" is perpendicular to the eye line; choose the sign that matches torso up
        Vector2 eyeLine   = (eR - eL);
        Vector2 faceUp    = new Vector2(-eyeLine.y, eyeLine.x).normalized; // 90° CCW
        Vector2 torsoUp   = (( (shL+shR)*0.5f ) - ( (hipL+hipR)*0.5f )).normalized;
        if (Vector2.Dot(faceUp, torsoUp) < 0) faceUp = -faceUp; // ensure it points upward

        // --- target position (slightly down from eye center) ---
        Vector2 targetScreen = eyeCenter + faceUp * (verticalOffset * eyeWidth);

        // to canvas local
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, targetScreen, null, out var local);
        glassesRect.anchoredPosition = Vector2.Lerp(glassesRect.anchoredPosition, local, Time.deltaTime * smoothing);

        // --- size ---
        float targetWidth = eyeWidth * widthMultiplier;
        float aspect = img.sprite ? (img.sprite.rect.height / img.sprite.rect.width) : 0.35f;
        Vector2 targetSize = new Vector2(targetWidth, targetWidth * aspect);
        glassesRect.sizeDelta = Vector2.Lerp(glassesRect.sizeDelta, targetSize, Time.deltaTime * (smoothing * 0.6f));

        // --- rotation ---
        float angleZ = Mathf.Atan2(eyeLine.y, eyeLine.x) * Mathf.Rad2Deg;
        float z = rotateWithEyes ? Mathf.LerpAngle(glassesRect.localEulerAngles.z, angleZ, Time.deltaTime * smoothing) : 0f;
        glassesRect.localEulerAngles = new Vector3(0, 0, z);
    }

    Vector2 ToScreen(Vector3 lm, bool mirrored)
    {
        float x = mirrored ? (1f - lm.x) : lm.x;
        float y = 1f - lm.y;
        return new Vector2(Mathf.Clamp01(x) * Screen.width, Mathf.Clamp01(y) * Screen.height);
    }
}
