using UnityEngine; using UnityEngine.UI;
public class WebcamToRawImage : MonoBehaviour {
  public RawImage target;
  void Start(){ var cam=new WebCamTexture(); target.texture=cam; cam.Play(); target.uvRect=new Rect(1,0,-1,1); }
}
