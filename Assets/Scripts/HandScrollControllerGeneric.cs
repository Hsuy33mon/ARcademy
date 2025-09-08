using UnityEngine;
using UnityEngine.UI;

public class HandScrollControllerGeneric : MonoBehaviour
{
    public enum Axis { Horizontal, Vertical }

    public BodyDataReceiver bodyData;
    public ScrollRect scrollRect;
    public Axis axis = Axis.Horizontal;

    [Header("Behavior")]
    public bool RequirePinchToScroll = true;
    [Range(0f, 0.2f)] public float deadZone = 0.02f;
    public float scrollSpeed = 1.5f;
    public bool invertDirection = true; // toggle to your preference

    float last = 0.5f;
    bool hasLast = false;

    void OnEnable() { hasLast = false; }

    void Update()
    {
        if (!bodyData || !scrollRect || !bodyData.IsDataValid) return;
        if (RequirePinchToScroll && !bodyData.is_pinching) { hasLast = false; return; }

        float pos = axis == Axis.Horizontal ? bodyData.pointerPosition.x : bodyData.pointerPosition.y;
        pos = Mathf.Clamp01(pos);

        if (!hasLast) { last = pos; hasLast = true; return; }

        float d = pos - last;
        if (invertDirection) d = -d;
        if (Mathf.Abs(d) < deadZone) { last = pos; return; }

        if (axis == Axis.Horizontal)
        {
            float newPos = Mathf.Clamp01(scrollRect.horizontalNormalizedPosition + d * scrollSpeed);
            scrollRect.horizontalNormalizedPosition = newPos;
        }
        else
        {
            float newPos = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + d * scrollSpeed);
            scrollRect.verticalNormalizedPosition = newPos;
        }

        last = pos;
    }
}
