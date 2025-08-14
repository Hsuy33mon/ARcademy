using UnityEngine;
using UnityEngine.UI;
public class HandScrollController : MonoBehaviour
{
    public BodyDataReceiver bodyData;  // drag BodyTracker here
    public ScrollRect scrollRect;      // drag the Scroll View (the GO with ScrollRect)
    [Header("Behavior")]
    public bool RequirePinchToScroll = true;
    [Range(0.0f, 0.2f)] public float deadZone = 0.02f; // ignore tiny jitters
    public float scrollSpeed = 1.5f;                   // tune to taste
    public bool invertDirection = true;                // new toggle
 
    float lastX = 0.5f;
    bool hasLast = false;
    void Update()
    {
        if (!bodyData || !scrollRect || !bodyData.IsDataValid) return;
        if (RequirePinchToScroll && !bodyData.is_pinching)  // uses the bool from your receiver
        {
            hasLast = false;
            return;
        }
        float x = Mathf.Clamp01(bodyData.pointerPosition.x); // normalized 0..1
        if (!hasLast) { lastX = x; hasLast = true; return; }
        float dx = x - lastX;
 
        // Flip direction if needed
        if (invertDirection) dx = -dx;
 
        if (Mathf.Abs(dx) < deadZone) { lastX = x; return; }
        // ScrollRect horizontalNormalizedPosition goes 0..1 (right is 1)
        float newPos = Mathf.Clamp01(scrollRect.horizontalNormalizedPosition + dx * scrollSpeed);
        scrollRect.horizontalNormalizedPosition = newPos;
        lastX = x;
    }
}