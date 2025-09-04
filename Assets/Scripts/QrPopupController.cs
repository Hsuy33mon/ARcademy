using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class QrPopupController : MonoBehaviour
{
    [Header("Popup")]
    public GameObject qrPanel;          // parent panel/card (inactive by default)
    public CanvasGroup qrCanvasGroup;   // optional (for fade + raycast block)
    public bool fade = true;
    public float fadeDuration = 0.2f;

    [Header("QR Image (pick one)")]
    public RawImage qrRawImage;         // if you have a Texture
    public Texture qrTexture;
    public Image qrImage;               // if you have a Sprite
    public Sprite qrSprite;

    [Header("Dwell buttons")]
    public HandDwellSelectable logoDwell;   // LogoBtn's HandDwellSelectable
    public HandDwellSelectable closeDwell;  // CloseBtn's HandDwellSelectable

    [Header("Optional: disable while open")]
    public List<GameObject> disableWhileOpen;  // e.g., DescriptionPanel, SalaryPanel

    void Awake()
    {
        if (qrPanel) qrPanel.SetActive(false);

        // Preload your QR visual
        if (qrRawImage && qrTexture) qrRawImage.texture = qrTexture;
        if (qrImage && qrSprite) { qrImage.sprite = qrSprite; qrImage.enabled = true; }

        // Make sure CanvasGroup starts at 0 alpha if using fade
        if (qrCanvasGroup) qrCanvasGroup.alpha = 0f;
    }

    // Hook this to LogoBtn -> HandDwellSelectable -> OnSelected
    public void OpenQr()
    {
        // Optionally disable underlying UI while QR is open
        if (disableWhileOpen != null)
            foreach (var go in disableWhileOpen) if (go) go.SetActive(false);

        if (qrPanel && !qrPanel.activeSelf) qrPanel.SetActive(true);

        if (fade && qrCanvasGroup) StartCoroutine(FadeTo(1f));
        else if (qrCanvasGroup) qrCanvasGroup.alpha = 1f;

        // Small cooldowns so they don't retrigger immediately
        logoDwell?.StartCooldown(0.75f);
        closeDwell?.StartCooldown(0.75f);
    }

    // Hook this to CloseBtn -> HandDwellSelectable -> OnSelected
    public void CloseQr()
    {
        if (fade && qrCanvasGroup)
            StartCoroutine(FadeTo(0f, () => { if (qrPanel) qrPanel.SetActive(false); }));
        else
        {
            if (qrCanvasGroup) qrCanvasGroup.alpha = 0f;
            if (qrPanel) qrPanel.SetActive(false);
        }

        if (disableWhileOpen != null)
            foreach (var go in disableWhileOpen) if (go) go.SetActive(true);

        logoDwell?.StartCooldown(0.5f);
    }

    IEnumerator FadeTo(float target, System.Action after = null)
    {
        if (!qrCanvasGroup) { after?.Invoke(); yield break; }
        float start = qrCanvasGroup.alpha, t = 0f;
        while (t < fadeDuration) {
            t += Time.deltaTime;
            qrCanvasGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        qrCanvasGroup.alpha = target;
        after?.Invoke();
    }
}
