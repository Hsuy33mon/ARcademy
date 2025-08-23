using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class BodyDataReceiver : MonoBehaviour
{
    [Header("Network")]
    public int port = 5065;

    [Header("Timings")]
    [Tooltip("How long received data stays 'fresh' before IsDataValid turns false.")]
    public float dataTimeout = 0.40f; // was 0.20f; a bit longer is safer for UDP/MediaPipe hiccups

    [Header("Pointer / Gestures")]
    public Vector2 pointerPosition;       // normalized [0..1] screen space
    public bool is_pinching;
    public bool is_fist;                  // optional if sender provides it
    public bool inActivationZone;
    public bool inGoBackZone;

    [Header("Body Landmarks (Pose)")]
    public List<Vector3> bodyLandmarks = new List<Vector3>(); // expected 33
    public bool bodyDetected;

    [Header("Hand Landmarks")]
    public List<Vector3> leftHandLandmarks  = new List<Vector3>();   // 21 points
    public List<Vector3> rightHandLandmarks = new List<Vector3>();   // 21 points
    public bool leftHandDetected;
    public bool rightHandDetected;

    [Header("Presence (derived)")]
    [Tooltip("Single combined flag (sender 'hand_detected' OR any hand landmarks present).")]
    public bool handDetected;
    [Tooltip("When a hand/pointer was last seen (used for grace windows).")]
    public float lastHandSeenTime;
    [Tooltip("Last pointer pos for motion detection/arming.")]
    public Vector2 lastPointerPos;

    [Header("Debug")]
    public float lastUpdateTime;          // when the last packet was parsed
    public string lastSender;             // IP of last UDP sender (optional)
    public string lastError;

    // -------- internals --------
    private UdpClient client;
    private Thread receiveThread;
    private volatile bool running;
    private string latestJson;            // exchanged atomically with Interlocked

    // ---------- Unity lifecycle ----------
    void Start() => StartReceiver();

    void Update()
    {
        // Atomically grab & clear latest JSON (so we don’t parse same packet twice)
        string json = Interlocked.Exchange(ref latestJson, null);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var data = JsonUtility.FromJson<IncomingPayload>(json);

            // ----- pointer / zones / gestures -----
            if (data.pointer_position != null)
            {
                var newP = new Vector2(data.pointer_position.x, data.pointer_position.y);
                // clamp to [0..1]
                newP = Vector2.Min(Vector2.one, Vector2.Max(Vector2.zero, newP));

                // treat small movement as "activity" so presence can be armed once
                if (Vector2.Distance(newP, lastPointerPos) > 0.01f)  // ~1% of screen
                    lastHandSeenTime = Time.time;

                lastPointerPos  = newP;
                pointerPosition = newP;
            }

            inActivationZone = data.in_activation;
            inGoBackZone     = data.in_goback;
            is_pinching      = data.is_pinching;
            is_fist          = data.is_fist;
            lastUpdateTime   = Time.time;

            // ----- body (pose) landmarks -----
            bodyLandmarks.Clear();
            int poseCount = (data.body_landmarks != null) ? data.body_landmarks.Length : 0;
            bodyDetected = poseCount >= 33;  // Mediapipe pose has 33
            if (bodyDetected)
            {
                foreach (var lm in data.body_landmarks)
                    bodyLandmarks.Add(new Vector3(lm.x, lm.y, lm.z));
            }

            // ----- hand landmarks & detected flags -----
            leftHandLandmarks.Clear();
            rightHandLandmarks.Clear();

            bool lhFromArray = (data.left_hand  != null && data.left_hand.Length  > 0);
            bool rhFromArray = (data.right_hand != null && data.right_hand.Length > 0);

            leftHandDetected  = data.left_hand_detected  || lhFromArray;
            rightHandDetected = data.right_hand_detected || rhFromArray;

            if (leftHandDetected && data.left_hand != null)
                foreach (var lm in data.left_hand)
                    leftHandLandmarks.Add(new Vector3(lm.x, lm.y, lm.z));

            if (rightHandDetected && data.right_hand != null)
                foreach (var lm in data.right_hand)
                    rightHandLandmarks.Add(new Vector3(lm.x, lm.y, lm.z));

            // Combined presence: single flag OR either detailed hand present
            handDetected = data.hand_detected || leftHandDetected || rightHandDetected;
            if (handDetected) lastHandSeenTime = Time.time;
        }
        catch (Exception e)
        {
            lastError = e.Message;
            Debug.LogWarning($"[BodyDataReceiver] Parse error: {e.Message}\nJSON: {json}");
        }
    }

    void OnDisable()  => StopReceiver();
    void OnDestroy()  => StopReceiver();
    void OnApplicationQuit() => StopReceiver();

    // ---------- Public convenience gates ----------
    // "Is data fresh?" — used by UI as a coarse gate
    public bool IsDataValid => Time.time - lastUpdateTime < dataTimeout;

    // "Is a hand present *now*?"
    public bool HandPresentNow => handDetected;

    // "Has a hand been present within a short grace window?"
    // Use this from UI to avoid flicker during brief drops.
    public bool HandPresentWithGrace(float graceSec = 0.25f)
        => (Time.time - lastHandSeenTime) <= graceSec;

    // ---------- UDP ----------
    public void StartReceiver()
    {
        StopReceiver();

        running = true;
        receiveThread = new Thread(() =>
        {
            try
            {
                using (client = new UdpClient(port))
                {
                    // Optional: widen buffer for larger/rapid packets
                    client.Client.ReceiveBufferSize = 1 << 16; // 64 KB

                    while (running)
                    {
                        try
                        {
                            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);
                            byte[] bytes = client.Receive(ref anyIP);
                            string json = Encoding.UTF8.GetString(bytes);

                            // stash sender for debugging
                            lastSender = anyIP.Address.ToString();

                            // Atomically publish latest packet (drop older unparsed if any)
                            Interlocked.Exchange(ref latestJson, json);
                        }
                        catch (SocketException se)
                        {
                            // Ignore timeouts/interrupts during shutdown
                            if (running) Debug.LogWarning($"[BodyDataReceiver] Socket: {se.Message}");
                        }
                        catch (Exception ex)
                        {
                            if (running) Debug.LogWarning($"[BodyDataReceiver] Receive: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception openEx)
            {
                Debug.LogError($"[BodyDataReceiver] Cannot open UDP {port}: {openEx.Message}");
            }
        });

        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    public void StopReceiver()
    {
        running = false;
        try { client?.Close(); } catch { /* ignore */ }
        client = null;

        if (receiveThread != null)
        {
            // Give the thread a moment to exit
            try { if (!receiveThread.Join(100)) receiveThread.Interrupt(); } catch { /* ignore */ }
            receiveThread = null;
        }
    }

    // ---------- JSON models ----------
    [Serializable] private class HandPosition { public float x, y; }
    [Serializable] private class Landmark     { public float x, y, z, visibility; }

    [Serializable]
    private class IncomingPayload
    {
        // Core
        public bool hand_detected;
        public HandPosition pointer_position;
        public bool in_activation;
        public bool in_goback;
        public float timestamp;
        public bool is_pinching;

        // Pose
        public Landmark[] body_landmarks;

        // Hands
        public bool left_hand_detected, right_hand_detected;
        public Landmark[] left_hand;   // 21
        public Landmark[] right_hand;  // 21

        // Optional gesture
        public bool is_fist;
    }
}
