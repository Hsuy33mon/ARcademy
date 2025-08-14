using UnityEngine;

[RequireComponent(typeof(Transform))]
public class FitQuadToCamera : MonoBehaviour
{
    public Camera targetCamera;
    public float distance = 3.0f; // Far enough behind jersey

    void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        // Calculate size of quad to cover camera view at given distance
        float height = 2.0f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * targetCamera.aspect;

        // Move quad in front of camera by 'distance' units
        transform.position = targetCamera.transform.position + targetCamera.transform.forward * distance;
        transform.LookAt(targetCamera.transform.position + targetCamera.transform.forward * (distance + 1));
        transform.localScale = new Vector3(width, height, 1); // Match full screen
    }
}
