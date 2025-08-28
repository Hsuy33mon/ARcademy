
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using ZXing;         
using ZXing.QrCode;   
using TMPro;

[DisallowMultipleComponent]
public class QRDisplayController : MonoBehaviour
{
    [Header("Panel + Widgets")]
    public GameObject panel;            // inactive by default
    public Image panelBackground;       // the Image on SharePanel (backdrop)
    public RawImage previewImage;       // screenshot
    public RawImage qrImage;            // QR code
    public Text urlLabel;               // optional legacy
    public TMP_Text urlLabelTMP;        // optional TMP

    [Header("Backdrop")]
    [Range(0f,1f)] public float backdropOpacity = 0.90f; // dim background strongly
    public Color  backdropColor = Color.black;

    [Header("Disable while open")]
    public GameObject[] disableWhileOpen;  // drag UI groups you want inactive behind the panel
    readonly System.Collections.Generic.List<GameObject> _disabled = new();

    [Header("QR Options")]
    [Range(128, 1024)] public int qrSize = 512;
    [Range(0, 8)]     public int qrMargin = 1;
    public bool copyUrlToClipboard = true;

    [Header("Events")]
    public UnityEvent onClosed;

    Texture2D _qrTex;
    string _lastUrl;

    // === called by Uploader.onPreviewReady (Dynamic Texture2D) ===
    public void SetPreview(Texture2D tex)
    {
        EnsurePanelOpen();
        if (previewImage)
        {
            previewImage.texture = tex;
            // force full opacity in case inspector had low alpha
            var c = previewImage.color; c.a = 1f; previewImage.color = c;
            previewImage.enabled = (tex != null);
        }
    }

    // === called by Uploader.onUploadSucceeded (Dynamic string) ===
    public void ShowQR(string url)
    {
        _lastUrl = url;
        if (copyUrlToClipboard && !string.IsNullOrEmpty(url)) GUIUtility.systemCopyBuffer = url;
        if (urlLabel)    urlLabel.text = url;
        if (urlLabelTMP) urlLabelTMP.text = url;

        EnsurePanelOpen();

        if (_qrTex) { Destroy(_qrTex); _qrTex = null; }
        _qrTex = GenerateQR(url);

        if (qrImage)
        {
            qrImage.texture = _qrTex;
            var c = qrImage.color; c.a = 1f; qrImage.color = c;  // force opaque
            qrImage.enabled = (_qrTex != null);
        }
    }

    public void Close()
    {
        if (panel) panel.SetActive(false);

        if (qrImage) qrImage.texture = null;
        if (_qrTex) { Destroy(_qrTex); _qrTex = null; }
        if (previewImage) previewImage.texture = null;

        // restore everything we disabled
        foreach (var go in _disabled) if (go) go.SetActive(true);
        _disabled.Clear();

        onClosed?.Invoke(); // wire to Uploader.DiscardLastShot()
    }

    public void OpenURLInBrowser()
    {
        if (!string.IsNullOrEmpty(_lastUrl)) Application.OpenURL(_lastUrl);
    }

    // ---------- helpers ----------
    void EnsurePanelOpen()
    {
        if (panelBackground)
        {
            var c = backdropColor; c.a = backdropOpacity;
            panelBackground.color = c;
            panelBackground.raycastTarget = true;
        }

        if (panel && !panel.activeSelf)
        {
            // deactivate background UI now
            _disabled.Clear();
            foreach (var go in disableWhileOpen)
                if (go && go.activeSelf) { go.SetActive(false); _disabled.Add(go); }

            panel.SetActive(true);
        }
    }

    Texture2D GenerateQR(string text)
    {
#if NO_ZXING
        // placeholder if ZXing isn't installed yet
        var tex = new Texture2D(qrSize, qrSize, TextureFormat.RGBA32, false);
        var cols = new Color32[qrSize * qrSize];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255,255,255,255);
        tex.SetPixels32(cols); tex.Apply();
        return tex;
#else
        var writer = new ZXing.BarcodeWriterPixelData
        {
            Format = ZXing.BarcodeFormat.QR_CODE,
            Options = new ZXing.QrCode.QrCodeEncodingOptions
            {
                Height = qrSize,
                Width  = qrSize,
                Margin = qrMargin
            }
        };
        var pixelData = writer.Write(text);
        var tex = new Texture2D(pixelData.Width, pixelData.Height, TextureFormat.RGBA32, false);
        tex.LoadRawTextureData(pixelData.Pixels);
        tex.Apply();
        return tex;
#endif
    }
}
