using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandLandmarkOverlay : MonoBehaviour
{
    [Header("Inputs")]
    public BodyDataReceiver body;                 // drag BodyTracker here
    public RectTransform canvasRect;              // drag your Canvas (RectTransform)
    public RectTransform dotPrefab;               // drag DotPrefab (Image with RectTransform)
    public bool mirrorX = true;                   // mirror to match webcam

    [Header("Options")]
    public bool showOnlyIndexTip = false;         // if true, shows just one dot

    readonly List<RectTransform> pool = new List<RectTransform>();

    void Awake()
    {
        if (!canvasRect) canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
    }

    void EnsurePoolSize(int count)
    {
        while (pool.Count < count)
        {
            var dot = Instantiate(dotPrefab, canvasRect);
            dot.gameObject.SetActive(true);
            pool.Add(dot);
        }
        for (int i = 0; i < pool.Count; i++)
            pool[i].gameObject.SetActive(i < count);
    }

    void Update()
    {
        if (!body || !body.IsDataValid || !body.bodyDetected || canvasRect == null || dotPrefab == null)
        {
            // hide all if no data
            EnsurePoolSize(0);
            return;
        }

        int count = showOnlyIndexTip ? 1 : body.bodyLandmarks.Count;
        EnsurePoolSize(count);

        float w = canvasRect.rect.width;
        float h = canvasRect.rect.height;

        for (int i = 0; i < count; i++)
        {
            Vector3 lm = body.bodyLandmarks[showOnlyIndexTip ? 20 : i]; // 20 ~ right index MCP-ish; change if you want
            float x = mirrorX ? (1f - lm.x) : lm.x;   // normalized
            float y = lm.y;                            // normalized (already flipped by Python to 0..1 bottom->top)

            // convert normalized [0..1] to canvas local space (centered)
            Vector2 pos = new Vector2((x - 0.5f) * w, (y - 0.5f) * h);
            pool[i].anchoredPosition = pos;
        }
    }
}
