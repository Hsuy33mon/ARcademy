using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class GlassBlurBinder : MonoBehaviour
{
    [Header("Webcam Source (assign ONE)")]
    // public Renderer webcamQuad;          // if your webcam is on a Quad (WebcamFullScreen)
    public RawImage webcamRawImage;      // if your webcam is already a UI RawImage

    [Header("Blur")]
    public Material blurMaterial;        // M_KawaseBlur
    [Range(1,4)] public int downsample = 2;
    [Range(1,6)] public int iterations = 3;

    Texture src; RawImage target; RenderTexture ping, pong;

    void Awake(){ target = GetComponent<RawImage>(); }

    void Start(){ FindSource(); Allocate(); }
    void OnDestroy(){ Release(); }

    void FindSource(){
        src = null;
        // if (webcamQuad && webcamQuad.material) src = webcamQuad.material.mainTexture;
        if (!src && webcamRawImage) src = webcamRawImage.texture;
        // if (!src) Debug.LogError("GlassBlurBinder: assign webcamQuad or webcamRawImage.");
    }

    void Allocate(){
        if (src == null) return;
        int w = Mathf.Max(64, src.width  >> downsample);
        int h = Mathf.Max(64, src.height >> downsample);
        Release();
        ping = new RenderTexture(w,h,0,RenderTextureFormat.ARGB32){ filterMode = FilterMode.Bilinear };
        pong = new RenderTexture(w,h,0,RenderTextureFormat.ARGB32){ filterMode = FilterMode.Bilinear };
        target.texture = ping;
    }

    void Release(){ if (ping){ ping.Release(); } if (pong){ pong.Release(); } ping=pong=null; }

    void LateUpdate(){
        if (src == null || blurMaterial == null || ping == null) return;
        // copy source → ping
        Graphics.Blit(src, ping);
        // blur ping ↔ pong
        for (int i=0;i<iterations;i++){
            blurMaterial.SetFloat("_Offset", 1f + i);
            Graphics.Blit(ping, pong, blurMaterial);
            var t = ping; ping = pong; pong = t;
        }
        // target.texture already points to ping
    }
}
