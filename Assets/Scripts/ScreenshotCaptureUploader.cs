using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using TMPro;                   // for TMP_Text (optional)
using UnityEngine.UI;         // for CanvasGroup

[DisallowMultipleComponent]
public class ScreenshotCaptureUploader : MonoBehaviour
{
    [Header("Hide these while capturing (optional)")]
    public GameObject[] hideDuringShot;

    public enum UploadMode { Catbox, Imgur, Custom }
    [Header("Upload Mode")]
    public UploadMode uploadMode = UploadMode.Catbox;

    [Header("Imgur (optional)")]
    public string imgurClientId = "";          // used only if UploadMode = Imgur

    [Header("Custom endpoint (optional)")]
    public string customUploadEndpoint = "";   // expects JSON: {"url":"https://..."}

    [Header("Countdown")]
    public bool   useCountdown = true;
    [Range(1f, 10f)] public float countdownSeconds = 3f;
    public CanvasGroup countdownGroup;         // a small panel centered on screen (inactive by default)
    public TMP_Text countdownTMP;              // either TMP_Text...
    public Text     countdownText;             // ...or legacy Text
    public AudioSource sfx;                    // optional
    public AudioClip  beepClip;                // optional
    public AudioClip  shutterClip;             // optional

    [Header("Status (read-only)")]
    [SerializeField] string lastUrl;
    [SerializeField] bool   isBusy;

    Texture2D _lastShot;

    public string    LastUrl   => lastUrl;
    public bool      IsBusy    => isBusy;
    public Texture2D LastShot  => _lastShot;

    // -------- Events --------
    [Serializable] public class StringEvent  : UnityEvent<string>  {}
    [Serializable] public class TextureEvent : UnityEvent<Texture2D>{}

    public UnityEvent   onCaptureStarted;
    public UnityEvent   onCaptureFinished;
    public TextureEvent onPreviewReady;        // sends Texture2D (preview)
    public StringEvent  onUploadSucceeded;     // sends URL (for QR)
    public UnityEvent   onUploadFailed;

    // === PUBLIC ENTRY ===
    public void TriggerCaptureAndUpload()
    {
        if (!isBusy) StartCoroutine(CountdownThenCapture());
    }

    public void DiscardLastShot()
    {
        if (_lastShot) { Destroy(_lastShot); _lastShot = null; }
    }

    // ---------------- internal ----------------
    IEnumerator CountdownThenCapture()
    {
        if (useCountdown && countdownSeconds > 0.9f)
            yield return DoCountdown();

        yield return CaptureUploadRoutine();
    }

    IEnumerator DoCountdown()
    {
        if (countdownGroup)
        {
            countdownGroup.gameObject.SetActive(true);
            countdownGroup.alpha = 1f;
            countdownGroup.blocksRaycasts = true;
            countdownGroup.interactable = false;
        }

        float t = countdownSeconds;
        int lastWhole = -1;

        while (t > 0f)
        {
            int whole = Mathf.CeilToInt(t);
            if (whole != lastWhole)
            {
                if (countdownTMP)  countdownTMP.text  = whole.ToString();
                if (countdownText) countdownText.text = whole.ToString();
                if (sfx && beepClip) sfx.PlayOneShot(beepClip);
                lastWhole = whole;
            }
            yield return null;
            t -= Time.unscaledDeltaTime;
        }

        if (countdownGroup) countdownGroup.gameObject.SetActive(false);
        if (sfx && shutterClip) sfx.PlayOneShot(shutterClip);
    }

    IEnumerator CaptureUploadRoutine()
    {
        isBusy = true;
        onCaptureStarted?.Invoke();

        // 1) Capture
        SetHidden(true);
        yield return new WaitForEndOfFrame();
        var shot = ScreenCapture.CaptureScreenshotAsTexture();
        SetHidden(false);

        if (shot == null)
        {
            isBusy = false; onCaptureFinished?.Invoke(); onUploadFailed?.Invoke();
            yield break;
        }

        _lastShot = shot;
        onPreviewReady?.Invoke(shot);                  // show preview immediately

        // 2) Upload
        string url = null;
        byte[] png = shot.EncodeToPNG();

        if      (uploadMode == UploadMode.Catbox) yield return UploadToCatbox(png, u => url = u);
        else if (uploadMode == UploadMode.Imgur)  yield return UploadToImgur(png,  u => url = u);
        else                                      yield return UploadToCustom(png, u => url = u);

        // 3) Finish
        if (string.IsNullOrEmpty(url))
        {
            isBusy = false; onCaptureFinished?.Invoke(); onUploadFailed?.Invoke();
            yield break;
        }

        lastUrl = url;
        isBusy = false;
        onCaptureFinished?.Invoke();
        onUploadSucceeded?.Invoke(url);
    }

    void SetHidden(bool hidden)
    {
        if (hideDuringShot == null) return;
        foreach (var go in hideDuringShot) if (go) go.SetActive(!hidden);
    }

    // ----- Uploaders -----
    IEnumerator UploadToCatbox(byte[] png, Action<string> onDone)
    {
        onDone?.Invoke(null);
        WWWForm form = new WWWForm();
        form.AddField("reqtype", "fileupload");
        form.AddBinaryData("fileToUpload", png, $"ARshot_{DateTime.Now:yyyyMMdd_HHmmss}.png", "image/png");

        using (var req = UnityWebRequest.Post("https://catbox.moe/user/api.php", form))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;

            string resp = req.downloadHandler.text?.Trim();
            if (!string.IsNullOrEmpty(resp) && (resp.StartsWith("http://") || resp.StartsWith("https://")))
                onDone?.Invoke(resp);
        }
    }

    IEnumerator UploadToImgur(byte[] png, Action<string> onDone)
    {
        onDone?.Invoke(null);
        if (string.IsNullOrEmpty(imgurClientId)) yield break;

        string b64 = Convert.ToBase64String(png);
        WWWForm form = new WWWForm();
        form.AddField("image", b64);
        form.AddField("type", "base64");

        using (var req = UnityWebRequest.Post("https://api.imgur.com/3/image", form))
        {
            req.SetRequestHeader("Authorization", "Client-ID " + imgurClientId);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;

            string link = ExtractJsonValue(req.downloadHandler.text, "link");
            if (!string.IsNullOrEmpty(link)) onDone?.Invoke(link);
        }
    }

    IEnumerator UploadToCustom(byte[] png, Action<string> onDone)
    {
        onDone?.Invoke(null);
        if (string.IsNullOrEmpty(customUploadEndpoint)) yield break;

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", png, "image.png", "image/png");

        using (var req = UnityWebRequest.Post(customUploadEndpoint, form))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;

            string url = ExtractJsonValue(req.downloadHandler.text, "url");
            if (!string.IsNullOrEmpty(url)) onDone?.Invoke(url);
        }
    }

    string ExtractJsonValue(string json, string key)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return null;
        string pat = "\"" + key + "\"";
        int i = json.IndexOf(pat, StringComparison.OrdinalIgnoreCase); if (i < 0) return null;
        i = json.IndexOf(':', i); if (i < 0) return null;
        int q1 = json.IndexOf('"', i + 1); int q2 = json.IndexOf('"', q1 + 1);
        if (q1 < 0 || q2 < 0) return null;
        return json.Substring(q1 + 1, q2 - q1 - 1);
    }
}
