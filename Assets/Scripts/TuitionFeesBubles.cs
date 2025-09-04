using UnityEngine;
using System.Collections.Generic;

public class TuitionFeeBubbles : MonoBehaviour
{
    public BodyDataReceiver body;
    public Canvas canvas;
    public List<RectTransform> cards = new List<RectTransform>(); // Year1..Year4 in your own positions

    [Header("Follow group around user")]
    public bool followUser = true;                 // turn OFF if you don't want the group to track the body
    public Vector2 fallbackViewport01 = new Vector2(0.5f, 0.5f); // center if body not found

    [Header("Floating motion only")]
    public float bobAmplitude = 8f;               // pixels up/down
    public float bobSpeed = 1.2f;                 // cycles per second-ish
    public float phaseOffsetPerCard = 0.8f;       // radian offset between cards for variety
    public bool alternateUpDown = true;           // even cards go opposite direction

    RectTransform canvasRect, anchor;             // TuitionGroup rect
    Vector2[] basePos;                            // cached per-card starting positions

    void Awake()
    {
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        if (canvas) canvasRect = canvas.GetComponent<RectTransform>();
        anchor = GetComponent<RectTransform>();
        CacheBasePositions();
    }

    void OnEnable() => CacheBasePositions();

    void CacheBasePositions()
    {
        if (cards == null) return;
        basePos = new Vector2[cards.Count];
        for (int i = 0; i < cards.Count; i++)
            if (cards[i]) basePos[i] = cards[i].anchoredPosition;   // keep YOUR inspector positions
    }

    void Update()
    {
        if (!isActiveAndEnabled) return;

        // Move the WHOLE group to the user's center (children keep their inspector offsets)
        if (followUser && canvasRect && anchor)
        {
            Vector2 c01 = fallbackViewport01;
            if (body && body.bodyDetected && body.bodyLandmarks.Count >= 25)
            {
                var lSh = body.bodyLandmarks[11];
                var rSh = body.bodyLandmarks[12];
                var lHp = body.bodyLandmarks[23];
                var rHp = body.bodyLandmarks[24];
                c01 = new Vector2(
                    (lSh.x + rSh.x + lHp.x + rHp.x) * 0.25f,
                    (lSh.y + rSh.y + lHp.y + rHp.y) * 0.25f
                );
            }

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                new Vector2(c01.x * Screen.width, c01.y * Screen.height),
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out local
            );
            anchor.anchoredPosition = local;
        }

        // Float each card around its ORIGINAL inspector position
        float t = Time.time * bobSpeed;
        for (int i = 0; i < cards.Count; i++)
        {
            var rt = cards[i];
            if (!rt) continue;

            float dir = alternateUpDown ? ((i % 2 == 0) ? 1f : -1f) : 1f;
            float y = Mathf.Sin(t + i * phaseOffsetPerCard) * bobAmplitude * dir;

            // keep your X/Y; add only vertical bob
            rt.anchoredPosition = basePos[i] + new Vector2(0f, y);
        }
    }
}
