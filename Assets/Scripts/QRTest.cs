using UnityEngine;
using UnityEngine.UI;
using ZXing;

public class QRTest : MonoBehaviour {
    public RawImage qrDisplay;

    void Start() {
        var writer = new BarcodeWriter {
            Format = BarcodeFormat.QR_CODE,
            Options = new ZXing.Common.EncodingOptions {
                Width = 256,
                Height = 256,
                Margin = 1
            }
        };

        var tex = new Texture2D(256, 256);
        var colors = writer.Write("https://vmes.au.edu"); 
        tex.SetPixels32(colors);
        tex.Apply();

        qrDisplay.texture = tex;
    }
}
