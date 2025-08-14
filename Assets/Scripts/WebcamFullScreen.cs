// using UnityEngine;
// using UnityEngine.UI;

// public class WebcamFullScreen : MonoBehaviour
// {
//     void Start()
//     {
//         RawImage rawImage = GetComponent<RawImage>();
//         WebCamTexture webcamTexture = new WebCamTexture();

//         rawImage.texture = webcamTexture;
//         rawImage.material.mainTexture = webcamTexture;
//         webcamTexture.Play();

//         rawImage.rectTransform.localScale = new Vector3(-1, 1, 1);
//     }
// }
using UnityEngine;

public class WebcamFullScreen : MonoBehaviour
{
    public Renderer quadRenderer;

    void Start()
    {
        WebCamTexture webcamTexture = new WebCamTexture();
        webcamTexture.Play();

        Material mat = quadRenderer.material;
        mat.mainTexture = webcamTexture;

        // Flip horizontally using UV, not scale
        mat.mainTextureScale = new Vector2(-1, 1);
        mat.mainTextureOffset = new Vector2(1, 0);
    }
}


