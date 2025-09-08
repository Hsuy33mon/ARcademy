using UnityEngine;
using UnityEngine.UI;
using ZXing;
using ZXing.QrCode;

public class QrFromUrlZxing : MonoBehaviour
{
    public RawImage target;     // assign QrDisplay
    public int size = 512;      // 320–1024; bigger = crisper

    Texture2D tex;

    public void SetUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !target) return;

        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions { Width = size, Height = size, Margin = 1 }
        };

        var pixels = writer.Write(url);

        if (tex == null || tex.width != size || tex.height != size)
        {
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point; // sharp edges
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        target.texture = tex;
        // Remove the next line if you prefer a fixed RectTransform size
        // target.SetNativeSize();
    }
}
