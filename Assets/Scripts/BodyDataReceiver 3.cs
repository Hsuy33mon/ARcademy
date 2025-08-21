using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class BodyDataReceiver : MonoBehaviour
{
    public int port = 5065;
    public float dataTimeout = 0.2f;

    [Header("Pointer / Gestures")]
    public Vector2 pointerPosition;
    public bool is_pinching;
    public bool is_fist;                             // ✅ optional, if your sender provides it
    public bool inActivationZone;
    public bool inGoBackZone;

    [Header("Body Landmarks (Pose)")]
    public List<Vector3> bodyLandmarks = new List<Vector3>();
    public bool bodyDetected;

    [Header("Hand Landmarks")]
    public List<Vector3> leftHandLandmarks  = new List<Vector3>();   // 21 points
    public List<Vector3> rightHandLandmarks = new List<Vector3>();   // 21 points
    public bool leftHandDetected;
    public bool rightHandDetected;

    [Header("Debug")]
    public float lastUpdateTime;

    private UdpClient client;
    private Thread receiveThread;
    private bool running;
    private string latestJson;

    void Start() => StartReceiver();

    void Update()
    {
        if (string.IsNullOrEmpty(latestJson)) return;

        try
        {
            var data = JsonUtility.FromJson<IncomingPayload>(latestJson);

            // ----- pointer / zones / gestures -----
            if (data.pointer_position != null)
                pointerPosition = new Vector2(data.pointer_position.x, data.pointer_position.y);

            inActivationZone = data.in_activation;
            inGoBackZone     = data.in_goback;
            is_pinching      = data.is_pinching;
            is_fist          = data.is_fist;               // will be false if key absent
            lastUpdateTime   = Time.time;

            // ----- body (pose) landmarks -----
            bodyLandmarks.Clear();
            // bodyDetected = data.body_landmarks != null && data.body_landmarks.Length > 0;
            int poseCount = (data.body_landmarks != null) ? data.body_landmarks.Length : 0;
            bodyDetected = poseCount >= 33;  
            if (bodyDetected)
            {
                foreach (var lm in data.body_landmarks)
                    bodyLandmarks.Add(new Vector3(lm.x, lm.y, lm.z));
            }

            // ----- hand landmarks -----
            leftHandLandmarks.Clear();
            rightHandLandmarks.Clear();

            leftHandDetected  = data.left_hand_detected  && data.left_hand  != null && data.left_hand.Length  > 0;
            rightHandDetected = data.right_hand_detected && data.right_hand != null && data.right_hand.Length > 0;

            if (leftHandDetected)
                foreach (var lm in data.left_hand)
                    leftHandLandmarks.Add(new Vector3(lm.x, lm.y, lm.z));

            if (rightHandDetected)
                foreach (var lm in data.right_hand)
                    rightHandLandmarks.Add(new Vector3(lm.x, lm.y, lm.z));

            latestJson = null; // consumed
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Parse error: {e.Message}\nJSON: {latestJson}");
            latestJson = null;
        }
    }

    public bool IsDataValid => Time.time - lastUpdateTime < dataTimeout;

    void StartReceiver()
    {
        running = true;
        receiveThread = new Thread(() =>
        {
            using (client = new UdpClient(port))
            {
                while (running)
                {
                    try
                    {
                        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);
                        byte[] data = client.Receive(ref anyIP);
                        latestJson = Encoding.UTF8.GetString(data);
                    }
                    catch (Exception e) { Debug.LogWarning(e.Message); }
                }
            }
        });
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void OnDisable()
    {
        running = false;
        client?.Close();
    }

    // ===== JSON data models =====
    [Serializable] private class HandPosition { public float x, y; }
    [Serializable] private class Landmark     { public float x, y, z, visibility; }

    [Serializable]
    private class IncomingPayload
    {
        // existing
        public bool hand_detected;
        public HandPosition pointer_position;
        public bool in_activation;
        public bool in_goback;
        public float timestamp;
        public bool is_pinching;
        public Landmark[] body_landmarks;

        // NEW: hands
        public bool left_hand_detected, right_hand_detected;
        public Landmark[] left_hand;   // 21
        public Landmark[] right_hand;  // 21

        // NEW: optional gesture
        public bool is_fist;           // optional; defaults to false if missing
    }
}
